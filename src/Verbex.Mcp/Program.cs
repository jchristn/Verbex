namespace Verbex.Mcp
{
    using System;
    using System.Collections.Concurrent;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Verbex.Models;
    using Voltaic.Core;
    using Voltaic.Mcp;

    /// <summary>
    /// Verbex MCP Server - Exposes InvertedIndex as MCP tools for RAG applications.
    /// Supports multiple transports: stdio (for Claude Code), HTTP/SSE, WebSocket.
    /// </summary>
    public class Program
    {
        private static readonly ConcurrentDictionary<string, InvertedIndex> _Indices = new();
        private static readonly string _DefaultIndexName = "default";
        private static readonly string _StorageDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".verbex-mcp",
            "indices");

        private static readonly JsonSerializerOptions _JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        /// <summary>
        /// Serializes an object to JSON string for MCP tool responses.
        /// </summary>
        private static string ToJson(object obj)
        {
            return JsonSerializer.Serialize(obj, _JsonOptions);
        }

        public static async Task Main(string[] args)
        {
            string transport = "stdio";
            string host = "127.0.0.1";
            int port = 8200;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--transport" && i + 1 < args.Length)
                    transport = args[++i].ToLowerInvariant();
                else if (args[i] == "--host" && i + 1 < args.Length)
                    host = args[++i];
                else if (args[i] == "--port" && i + 1 < args.Length)
                    port = int.Parse(args[++i]);
            }

            Directory.CreateDirectory(_StorageDirectory);

            CancellationTokenSource cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            switch (transport)
            {
                case "stdio":
                    await RunStdioServerAsync(cts.Token).ConfigureAwait(false);
                    break;
                case "http":
                    await RunHttpServerAsync(host, port, cts.Token).ConfigureAwait(false);
                    break;
                case "websocket":
                    await RunWebSocketServerAsync(host, port, cts.Token).ConfigureAwait(false);
                    break;
                default:
                    Console.Error.WriteLine($"Unknown transport: {transport}");
                    Console.Error.WriteLine("Usage: verbex-mcp [--transport stdio|http|websocket] [--host 127.0.0.1] [--port 8200]");
                    Environment.Exit(1);
                    break;
            }

            await CleanupIndicesAsync().ConfigureAwait(false);
        }

        private static async Task RunStdioServerAsync(CancellationToken token)
        {
            McpServer server = new McpServer();
            RegisterAllTools(server);
            await server.RunAsync(token).ConfigureAwait(false);
        }

        private static async Task RunHttpServerAsync(string host, int port, CancellationToken token)
        {
            McpHttpServer server = new McpHttpServer(host, port);
            server.ServerName = "Verbex MCP Server";
            server.ServerVersion = "1.0.0";
            server.Log += (sender, message) => Console.WriteLine($"[Verbex MCP] {message}");
            server.ClientConnected += (sender, client) => Console.WriteLine($"[Verbex MCP] Client connected: {client.SessionId}");
            server.ClientDisconnected += (sender, client) => Console.WriteLine($"[Verbex MCP] Client disconnected: {client.SessionId}");

            RegisterAllToolsHttp(server);

            Console.WriteLine($"[Verbex MCP] HTTP server starting on http://{host}:{port}");
            await server.StartAsync(token).ConfigureAwait(false);
        }

        private static async Task RunWebSocketServerAsync(string host, int port, CancellationToken token)
        {
            McpWebsocketsServer server = new McpWebsocketsServer(host, port);
            server.ServerName = "Verbex MCP Server";
            server.ServerVersion = "1.0.0";
            server.Log += (sender, message) => Console.WriteLine($"[Verbex MCP] {message}");
            server.ClientConnected += (sender, client) => Console.WriteLine($"[Verbex MCP] Client connected: {client.SessionId}");
            server.ClientDisconnected += (sender, client) => Console.WriteLine($"[Verbex MCP] Client disconnected: {client.SessionId}");

            RegisterAllTools(server);

            Console.WriteLine($"[Verbex MCP] WebSocket server starting on ws://{host}:{port}");
            await server.StartAsync(token).ConfigureAwait(false);
        }

        private static void RegisterAllTools(McpServer server)
        {
            server.RegisterMethod("verbex_search", SearchHandler);
            server.RegisterMethod("verbex_add_document", AddDocumentHandler);
            server.RegisterMethod("verbex_get_document", GetDocumentHandler);
            server.RegisterMethod("verbex_list_documents", ListDocumentsHandler);
            server.RegisterMethod("verbex_delete_document", DeleteDocumentHandler);
            server.RegisterMethod("verbex_statistics", StatisticsHandler);
            server.RegisterMethod("verbex_list_indices", ListIndicesHandler);
            server.RegisterMethod("verbex_create_index", CreateIndexHandler);
            server.RegisterMethod("verbex_delete_index", DeleteIndexHandler);
            server.RegisterMethod("verbex_add_labels", AddLabelsHandler);
            server.RegisterMethod("verbex_add_tags", AddTagsHandler);
            server.RegisterMethod("verbex_index_exists", IndexExistsHandler);
            server.RegisterMethod("verbex_document_exists", DocumentExistsHandler);
        }

        private static void RegisterAllTools(McpWebsocketsServer server)
        {
            server.RegisterMethod("verbex_search", SearchHandler);
            server.RegisterMethod("verbex_add_document", AddDocumentHandler);
            server.RegisterMethod("verbex_get_document", GetDocumentHandler);
            server.RegisterMethod("verbex_list_documents", ListDocumentsHandler);
            server.RegisterMethod("verbex_delete_document", DeleteDocumentHandler);
            server.RegisterMethod("verbex_statistics", StatisticsHandler);
            server.RegisterMethod("verbex_list_indices", ListIndicesHandler);
            server.RegisterMethod("verbex_create_index", CreateIndexHandler);
            server.RegisterMethod("verbex_delete_index", DeleteIndexHandler);
            server.RegisterMethod("verbex_add_labels", AddLabelsHandler);
            server.RegisterMethod("verbex_add_tags", AddTagsHandler);
            server.RegisterMethod("verbex_index_exists", IndexExistsHandler);
            server.RegisterMethod("verbex_document_exists", DocumentExistsHandler);
        }

        private static void RegisterAllToolsHttp(McpHttpServer server)
        {
            server.RegisterTool(
                "verbex_search",
                "Search indexed documents using TF-IDF relevance scoring. Returns matching documents ranked by relevance. Use this for RAG retrieval.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        index = new { type = "string", description = "Index name to search (default: 'default')" },
                        query = new { type = "string", description = "Search query terms" },
                        maxResults = new { type = "integer", description = "Maximum results to return (default: 10)" },
                        useAndLogic = new { type = "boolean", description = "Use AND logic instead of OR (default: false)" },
                        labels = new { type = "array", items = new { type = "string" }, description = "Filter by labels (AND logic)" },
                        tags = new { type = "object", additionalProperties = new { type = "string" }, description = "Filter by tags (AND logic)" },
                        includeMatchedTerms = new { type = "boolean", description = "Include matched query terms for each result (default: false)" },
                        includeTermDetails = new { type = "boolean", description = "Include per-term score and frequency details for each result (default: false)" },
                        includeDocumentTermStats = new { type = "boolean", description = "Include whole-document term statistics for each result using one grouped query (default: false)" }
                    },
                    required = new[] { "query" }
                },
                SearchHandler);

            server.RegisterTool(
                "verbex_add_document",
                "Add a document to the search index with optional metadata. The document content will be tokenized and indexed for full-text search.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        index = new { type = "string", description = "Index name (default: 'default')" },
                        name = new { type = "string", description = "Document name/title" },
                        content = new { type = "string", description = "Document content to index" },
                        labels = new { type = "array", items = new { type = "string" }, description = "Labels for categorization" },
                        tags = new { type = "object", additionalProperties = new { type = "string" }, description = "Key-value tags for metadata" }
                    },
                    required = new[] { "name", "content" }
                },
                AddDocumentHandler);

            server.RegisterTool(
                "verbex_get_document",
                "Retrieve a specific document by ID with full content and metadata.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        index = new { type = "string", description = "Index name (default: 'default')" },
                        documentId = new { type = "string", description = "Document ID to retrieve" }
                    },
                    required = new[] { "documentId" }
                },
                GetDocumentHandler);

            server.RegisterTool(
                "verbex_list_documents",
                "List all documents in an index with pagination.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        index = new { type = "string", description = "Index name (default: 'default')" },
                        limit = new { type = "integer", description = "Maximum documents to return (default: 100)" },
                        offset = new { type = "integer", description = "Offset for pagination (default: 0)" }
                    }
                },
                ListDocumentsHandler);

            server.RegisterTool(
                "verbex_delete_document",
                "Remove a document from the index.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        index = new { type = "string", description = "Index name (default: 'default')" },
                        documentId = new { type = "string", description = "Document ID to delete" }
                    },
                    required = new[] { "documentId" }
                },
                DeleteDocumentHandler);

            server.RegisterTool(
                "verbex_statistics",
                "Get statistics about an index including document count, term count, and storage details.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        index = new { type = "string", description = "Index name (default: 'default')" }
                    }
                },
                StatisticsHandler);

            server.RegisterTool(
                "verbex_list_indices",
                "List all available indices.",
                new
                {
                    type = "object",
                    properties = new { }
                },
                ListIndicesHandler);

            server.RegisterTool(
                "verbex_create_index",
                "Create a new search index with optional configuration.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        name = new { type = "string", description = "Index name" },
                        inMemory = new { type = "boolean", description = "Use in-memory storage (default: false for persistent)" },
                        enableLemmatizer = new { type = "boolean", description = "Enable word lemmatization (default: false)" },
                        enableStopWords = new { type = "boolean", description = "Enable stop word removal (default: false)" },
                        minTokenLength = new { type = "integer", description = "Minimum token length (default: 0 = disabled)" },
                        maxTokenLength = new { type = "integer", description = "Maximum token length (default: 0 = disabled)" }
                    },
                    required = new[] { "name" }
                },
                CreateIndexHandler);

            server.RegisterTool(
                "verbex_delete_index",
                "Delete an index and all its documents.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        name = new { type = "string", description = "Index name to delete" }
                    },
                    required = new[] { "name" }
                },
                DeleteIndexHandler);

            server.RegisterTool(
                "verbex_add_labels",
                "Add labels to a document for categorization.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        index = new { type = "string", description = "Index name (default: 'default')" },
                        documentId = new { type = "string", description = "Document ID" },
                        labels = new { type = "array", items = new { type = "string" }, description = "Labels to add" }
                    },
                    required = new[] { "documentId", "labels" }
                },
                AddLabelsHandler);

            server.RegisterTool(
                "verbex_add_tags",
                "Add key-value tags to a document for metadata.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        index = new { type = "string", description = "Index name (default: 'default')" },
                        documentId = new { type = "string", description = "Document ID" },
                        tags = new { type = "object", additionalProperties = new { type = "string" }, description = "Tags to add" }
                    },
                    required = new[] { "documentId", "tags" }
                },
                AddTagsHandler);

            server.RegisterTool(
                "verbex_index_exists",
                "Check if an index exists. Returns true if the index exists, false otherwise.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        name = new { type = "string", description = "Index name to check" }
                    },
                    required = new[] { "name" }
                },
                IndexExistsHandler);

            server.RegisterTool(
                "verbex_document_exists",
                "Check if a document exists in an index. Returns true if the document exists, false otherwise.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        index = new { type = "string", description = "Index name (default: 'default')" },
                        documentId = new { type = "string", description = "Document ID to check" }
                    },
                    required = new[] { "documentId" }
                },
                DocumentExistsHandler);
        }

        #region Tool Handlers

        private static object SearchHandler(RpcParameters args)
        {
            string indexName = GetString(args, "index", _DefaultIndexName);
            string query = GetString(args, "query", "");
            int maxResults = GetInt(args, "maxResults", 10);
            bool useAndLogic = GetBool(args, "useAndLogic", false);
            List<string>? labels = GetStringList(args, "labels");
            Dictionary<string, string>? tags = GetStringDict(args, "tags");
            bool includeMatchedTerms = GetBool(args, "includeMatchedTerms", false);
            bool includeTermDetails = GetBool(args, "includeTermDetails", false);
            bool includeDocumentTermStats = GetBool(args, "includeDocumentTermStats", false);

            if (string.IsNullOrWhiteSpace(query))
                return ToJson(new { error = "Query is required" });

            InvertedIndex index = GetOrCreateIndexAsync(indexName).GetAwaiter().GetResult();
            SearchResults results = index.SearchAsync(query, maxResults, useAndLogic, labels, tags).GetAwaiter().GetResult();
            Dictionary<string, DocumentTermStats> statsByDocumentId = includeDocumentTermStats
                ? index.GetDocumentTermStatsAsync(results.Results.Select(r => r.DocumentId)).GetAwaiter().GetResult()
                : new Dictionary<string, DocumentTermStats>();

            List<Dictionary<string, object?>> resultItems = results.Results.Select(r =>
            {
                Dictionary<string, object?> item = new Dictionary<string, object?>
                {
                    ["documentId"] = r.DocumentId,
                    ["documentName"] = r.Document?.DocumentPath,
                    ["score"] = r.Score,
                    ["matchedTermCount"] = r.MatchedTermCount,
                    ["totalTermMatches"] = r.TotalTermMatches,
                    ["termScores"] = r.TermScores
                };

                if (includeMatchedTerms || includeTermDetails)
                {
                    item["matchedTerms"] = r.TermScores.Keys
                        .OrderBy(term => term, StringComparer.Ordinal)
                        .ToList();
                }

                if (includeTermDetails)
                {
                    item["termDetails"] = r.TermScores
                        .Select(kvp => new
                        {
                            term = kvp.Key,
                            score = kvp.Value,
                            frequency = r.TermFrequencies.TryGetValue(kvp.Key, out int frequency) ? frequency : 0
                        })
                        .OrderByDescending(detail => detail.score)
                        .ThenBy(detail => detail.term, StringComparer.Ordinal)
                        .ToList();
                }

                if (includeDocumentTermStats
                    && statsByDocumentId.TryGetValue(r.DocumentId, out DocumentTermStats? stats))
                {
                    item["documentTermStats"] = new
                    {
                        uniqueTermCount = stats.UniqueTermCount,
                        totalTermOccurrences = stats.TotalTermOccurrences
                    };
                }

                return item;
            }).ToList();

            return ToJson(new
            {
                totalCount = results.TotalCount,
                searchTimeMs = results.SearchTime.TotalMilliseconds,
                results = resultItems
            });
        }

        private static object AddDocumentHandler(RpcParameters args)
        {
            string indexName = GetString(args, "index", _DefaultIndexName);
            string name = GetString(args, "name", "");
            string content = GetString(args, "content", "");
            List<string>? labels = GetStringList(args, "labels");
            Dictionary<string, string>? tags = GetStringDict(args, "tags");

            if (string.IsNullOrWhiteSpace(name))
                return ToJson(new { error = "Document name is required" });
            if (string.IsNullOrWhiteSpace(content))
                return ToJson(new { error = "Document content is required" });

            InvertedIndex index = GetOrCreateIndexAsync(indexName).GetAwaiter().GetResult();
            string documentId = index.AddDocumentAsync(name, content).GetAwaiter().GetResult();

            if (labels != null && labels.Count > 0)
                index.AddLabelsBatchAsync(documentId, labels).GetAwaiter().GetResult();

            if (tags != null && tags.Count > 0)
                index.AddTagsBatchAsync(documentId, tags).GetAwaiter().GetResult();

            return ToJson(new
            {
                success = true,
                documentId = documentId,
                indexName = indexName
            });
        }

        private static object GetDocumentHandler(RpcParameters args)
        {
            string indexName = GetString(args, "index", _DefaultIndexName);
            string documentId = GetString(args, "documentId", "");

            if (string.IsNullOrWhiteSpace(documentId))
                return ToJson(new { error = "Document ID is required" });

            InvertedIndex index = GetOrCreateIndexAsync(indexName).GetAwaiter().GetResult();
            DocumentMetadata? doc = index.GetDocumentWithMetadataAsync(documentId).GetAwaiter().GetResult();

            if (doc == null)
                return ToJson(new { error = "Document not found" });

            return ToJson(new
            {
                documentId = doc.DocumentId,
                documentPath = doc.DocumentPath,
                documentLength = doc.DocumentLength,
                indexedDate = doc.IndexedDate,
                lastModified = doc.LastModified,
                labels = doc.Labels.ToList(),
                tags = doc.Tags.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                customMetadata = doc.CustomMetadata
            });
        }

        private static object ListDocumentsHandler(RpcParameters args)
        {
            string indexName = GetString(args, "index", _DefaultIndexName);
            int limit = GetInt(args, "limit", 100);
            int offset = GetInt(args, "offset", 0);

            InvertedIndex index = GetOrCreateIndexAsync(indexName).GetAwaiter().GetResult();
            List<DocumentMetadata> docs = index.GetDocumentsAsync(limit, offset).GetAwaiter().GetResult();

            return ToJson(new
            {
                count = docs.Count,
                documents = docs.Select(d => new
                {
                    documentId = d.DocumentId,
                    documentPath = d.DocumentPath,
                    documentLength = d.DocumentLength,
                    indexedDate = d.IndexedDate
                }).ToList()
            });
        }

        private static object DeleteDocumentHandler(RpcParameters args)
        {
            string indexName = GetString(args, "index", _DefaultIndexName);
            string documentId = GetString(args, "documentId", "");

            if (string.IsNullOrWhiteSpace(documentId))
                return ToJson(new { error = "Document ID is required" });

            InvertedIndex index = GetOrCreateIndexAsync(indexName).GetAwaiter().GetResult();
            bool removed = index.RemoveDocumentAsync(documentId).GetAwaiter().GetResult();

            return ToJson(new { success = removed });
        }

        private static object StatisticsHandler(RpcParameters args)
        {
            string indexName = GetString(args, "index", _DefaultIndexName);

            InvertedIndex index = GetOrCreateIndexAsync(indexName).GetAwaiter().GetResult();
            IndexStatistics stats = index.GetStatisticsAsync().GetAwaiter().GetResult();

            return ToJson(new
            {
                indexName = indexName,
                documentCount = stats.DocumentCount,
                termCount = stats.TermCount,
                postingCount = stats.PostingCount,
                totalDocumentSize = stats.TotalDocumentSize,
                averageDocumentLength = stats.AverageDocumentLength,
                totalTermOccurrences = stats.TotalTermOccurrences,
                averageTermsPerDocument = stats.AverageTermsPerDocument
            });
        }

        private static object ListIndicesHandler(RpcParameters args)
        {
            List<string> indices = _Indices.Keys.ToList();

            if (Directory.Exists(_StorageDirectory))
            {
                foreach (string dir in Directory.GetDirectories(_StorageDirectory))
                {
                    string name = Path.GetFileName(dir);
                    if (!indices.Contains(name))
                        indices.Add(name);
                }
            }

            return ToJson(new { indices = indices });
        }

        private static object CreateIndexHandler(RpcParameters args)
        {
            string name = GetString(args, "name", "");
            bool inMemory = GetBool(args, "inMemory", false);
            bool enableLemmatizer = GetBool(args, "enableLemmatizer", false);
            bool enableStopWords = GetBool(args, "enableStopWords", false);
            int minTokenLength = GetInt(args, "minTokenLength", 0);
            int maxTokenLength = GetInt(args, "maxTokenLength", 0);

            if (string.IsNullOrWhiteSpace(name))
                return ToJson(new { error = "Index name is required" });

            if (_Indices.ContainsKey(name))
                return ToJson(new { error = "Index already exists" });

            VerbexConfiguration config;
            if (inMemory)
            {
                config = VerbexConfiguration.CreateInMemory();
            }
            else
            {
                string indexDir = Path.Combine(_StorageDirectory, name);
                config = VerbexConfiguration.CreateOnDisk(indexDir, "index.db");
            }

            if (enableLemmatizer)
                config.Lemmatizer = new BasicLemmatizer();
            if (enableStopWords)
                config.StopWordRemover = new BasicStopWordRemover();
            if (minTokenLength > 0)
                config.MinTokenLength = minTokenLength;
            if (maxTokenLength > 0)
                config.MaxTokenLength = maxTokenLength;

            InvertedIndex index = new InvertedIndex(name, config);
            index.OpenAsync().GetAwaiter().GetResult();
            _Indices[name] = index;

            return ToJson(new
            {
                success = true,
                indexName = name,
                inMemory = inMemory
            });
        }

        private static object DeleteIndexHandler(RpcParameters args)
        {
            string name = GetString(args, "name", "");

            if (string.IsNullOrWhiteSpace(name))
                return ToJson(new { error = "Index name is required" });

            if (_Indices.TryRemove(name, out InvertedIndex? index))
            {
                try
                {
                    index.CloseAsync().GetAwaiter().GetResult();
                }
                catch
                {
                    // Ignore close errors
                }
                index.Dispose();
            }

            // Give the system time to release file handles
            Thread.Sleep(100);

            string indexDir = Path.Combine(_StorageDirectory, name);
            if (Directory.Exists(indexDir))
            {
                try
                {
                    Directory.Delete(indexDir, true);
                }
                catch (IOException)
                {
                    // Retry after a short delay
                    Thread.Sleep(500);
                    Directory.Delete(indexDir, true);
                }
            }

            return ToJson(new { success = true });
        }

        private static object AddLabelsHandler(RpcParameters args)
        {
            string indexName = GetString(args, "index", _DefaultIndexName);
            string documentId = GetString(args, "documentId", "");
            List<string>? labels = GetStringList(args, "labels");

            if (string.IsNullOrWhiteSpace(documentId))
                return ToJson(new { error = "Document ID is required" });
            if (labels == null || labels.Count == 0)
                return ToJson(new { error = "Labels are required" });

            InvertedIndex index = GetOrCreateIndexAsync(indexName).GetAwaiter().GetResult();
            index.AddLabelsBatchAsync(documentId, labels).GetAwaiter().GetResult();

            return ToJson(new { success = true });
        }

        private static object AddTagsHandler(RpcParameters args)
        {
            string indexName = GetString(args, "index", _DefaultIndexName);
            string documentId = GetString(args, "documentId", "");
            Dictionary<string, string>? tags = GetStringDict(args, "tags");

            if (string.IsNullOrWhiteSpace(documentId))
                return ToJson(new { error = "Document ID is required" });
            if (tags == null || tags.Count == 0)
                return ToJson(new { error = "Tags are required" });

            InvertedIndex index = GetOrCreateIndexAsync(indexName).GetAwaiter().GetResult();
            index.AddTagsBatchAsync(documentId, tags).GetAwaiter().GetResult();

            return ToJson(new { success = true });
        }

        private static object IndexExistsHandler(RpcParameters args)
        {
            string name = GetString(args, "name", "");

            if (string.IsNullOrWhiteSpace(name))
                return ToJson(new { error = "Index name is required" });

            bool exists = _Indices.ContainsKey(name);

            // Also check for persistent index on disk
            if (!exists)
            {
                string indexDir = Path.Combine(_StorageDirectory, name);
                exists = Directory.Exists(indexDir) && File.Exists(Path.Combine(indexDir, "index.db"));
            }

            return ToJson(new { exists = exists, indexName = name });
        }

        private static object DocumentExistsHandler(RpcParameters args)
        {
            string indexName = GetString(args, "index", _DefaultIndexName);
            string documentId = GetString(args, "documentId", "");

            if (string.IsNullOrWhiteSpace(documentId))
                return ToJson(new { error = "Document ID is required" });

            InvertedIndex index = GetOrCreateIndexAsync(indexName).GetAwaiter().GetResult();
            bool exists = index.DocumentExistsAsync(documentId).GetAwaiter().GetResult();

            return ToJson(new { exists = exists, documentId = documentId, indexName = indexName });
        }

        #endregion

        #region Helper Methods

        private static async Task<InvertedIndex> GetOrCreateIndexAsync(string indexName)
        {
            if (_Indices.TryGetValue(indexName, out InvertedIndex? existing))
                return existing;

            string indexDir = Path.Combine(_StorageDirectory, indexName);
            VerbexConfiguration config;

            if (Directory.Exists(indexDir) && File.Exists(Path.Combine(indexDir, "index.db")))
            {
                config = VerbexConfiguration.CreateOnDisk(indexDir, "index.db");
            }
            else
            {
                Directory.CreateDirectory(indexDir);
                config = VerbexConfiguration.CreateOnDisk(indexDir, "index.db");
            }

            InvertedIndex index = new InvertedIndex(indexName, config);
            await index.OpenAsync().ConfigureAwait(false);

            _Indices[indexName] = index;
            return index;
        }

        private static async Task CleanupIndicesAsync()
        {
            foreach (KeyValuePair<string, InvertedIndex> kvp in _Indices)
            {
                try
                {
                    await kvp.Value.CloseAsync().ConfigureAwait(false);
                    kvp.Value.Dispose();
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
            _Indices.Clear();
        }

        private static JsonElement? ToElement(RpcParameters args)
        {
            if (args != null && args.HasValue && !string.IsNullOrEmpty(args.RawJson))
            {
                try
                {
                    return JsonSerializer.Deserialize<JsonElement>(args.RawJson);
                }
                catch (JsonException)
                {
                    return null;
                }
            }
            return null;
        }

        private static string GetString(RpcParameters args, string property, string defaultValue)
        {
            JsonElement? root = ToElement(args);
            if (root.HasValue && root.Value.TryGetProperty(property, out JsonElement prop) && prop.ValueKind == JsonValueKind.String)
                return prop.GetString() ?? defaultValue;
            return defaultValue;
        }

        private static int GetInt(RpcParameters args, string property, int defaultValue)
        {
            JsonElement? root = ToElement(args);
            if (root.HasValue && root.Value.TryGetProperty(property, out JsonElement prop) && prop.ValueKind == JsonValueKind.Number)
                return prop.GetInt32();
            return defaultValue;
        }

        private static bool GetBool(RpcParameters args, string property, bool defaultValue)
        {
            JsonElement? root = ToElement(args);
            if (root.HasValue && root.Value.TryGetProperty(property, out JsonElement prop))
            {
                if (prop.ValueKind == JsonValueKind.True) return true;
                if (prop.ValueKind == JsonValueKind.False) return false;
            }
            return defaultValue;
        }

        private static List<string>? GetStringList(RpcParameters args, string property)
        {
            JsonElement? root = ToElement(args);
            if (root.HasValue && root.Value.TryGetProperty(property, out JsonElement prop) && prop.ValueKind == JsonValueKind.Array)
            {
                List<string> result = new List<string>();
                foreach (JsonElement item in prop.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                        result.Add(item.GetString() ?? "");
                }
                return result.Count > 0 ? result : null;
            }
            return null;
        }

        private static Dictionary<string, string>? GetStringDict(RpcParameters args, string property)
        {
            JsonElement? root = ToElement(args);
            if (root.HasValue && root.Value.TryGetProperty(property, out JsonElement prop) && prop.ValueKind == JsonValueKind.Object)
            {
                Dictionary<string, string> result = new Dictionary<string, string>();
                foreach (JsonProperty item in prop.EnumerateObject())
                {
                    if (item.Value.ValueKind == JsonValueKind.String)
                        result[item.Name] = item.Value.GetString() ?? "";
                }
                return result.Count > 0 ? result : null;
            }
            return null;
        }

        #endregion
    }
}
