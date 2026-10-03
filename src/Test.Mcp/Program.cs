namespace Test.Mcp
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Voltaic.Core;
    using Voltaic.Mcp;

    /// <summary>
    /// Comprehensive test console for Verbex MCP Server.
    /// Exercises every Verbex MCP tool over HTTP JSON-RPC, then validates tool discovery and invocation over
    /// Streamable HTTP (handshake era), stateless Streamable HTTP (2026-07-28, as used by Claude Code 2.1.x),
    /// stdio, and WebSocket.
    /// </summary>
    public class Program
    {
        private static readonly string[] _ExpectedTools = new[]
        {
            "verbex_search",
            "verbex_add_document",
            "verbex_get_document",
            "verbex_list_documents",
            "verbex_delete_document",
            "verbex_statistics",
            "verbex_list_indices",
            "verbex_create_index",
            "verbex_delete_index",
            "verbex_add_labels",
            "verbex_add_tags",
            "verbex_index_exists",
            "verbex_document_exists"
        };

        // Published by Voltaic 1.x on every server; Voltaic 2.x publishes none of them unless opted in.
        private static readonly string[] _VoltaicDemoTools = new[] { "ping", "echo", "getTime", "getSessions", "getClients" };

        private static readonly List<Process> _ServerProcesses = new List<Process>();
        private static McpHttpClient? _Client;
        private static int _TestsPassed = 0;
        private static int _TestsFailed = 0;
        private static readonly List<string> _Failures = new List<string>();
        private static readonly string _TestIndexName = "test-mcp-index";
        private static string? _TestDocumentId;
        private static string? _MetadataDocumentId;
        private static string _TempRoot = string.Empty;
        private static string _StorageDirectory = string.Empty;
        private static bool _Verbose = false;
        private static bool _StartServer = true;

        public static async Task Main(string[] args)
        {
            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║           Verbex MCP Server Test Suite                       ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
            Console.WriteLine();

            string host = "127.0.0.1";
            int port = 8250;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--host" && i + 1 < args.Length)
                    host = args[++i];
                else if (args[i] == "--port" && i + 1 < args.Length)
                    port = int.Parse(args[++i]);
                else if (args[i] == "--no-server")
                    _StartServer = false;
                else if (args[i] == "--verbose")
                    _Verbose = true;
            }

            string baseUrl = $"http://{host}:{port}";
            string? serverDll = null;

            try
            {
                if (_StartServer)
                {
                    // Isolate the server's indices in a temp directory and keep telemetry off for the run.
                    _TempRoot = Path.Combine(Path.GetTempPath(), "verbex-mcp-test-" + Guid.NewGuid().ToString("N"));
                    _StorageDirectory = Path.Combine(_TempRoot, "indices");
                    Directory.CreateDirectory(_StorageDirectory);
                    Environment.SetEnvironmentVariable("VERBEX_MCP_STORAGE_DIR", _StorageDirectory);
                    Environment.SetEnvironmentVariable("VERBEX_TELEMETRY_ENABLE", "false");

                    serverDll = BuildServer();
                    StartServer("http", serverDll, host, port);
                }

                _Client = await ConnectHttpClientAsync(baseUrl).ConfigureAwait(false);

                Section("HTTP JSON-RPC (/rpc): full tool coverage");
                await RunHttpToolTestsAsync().ConfigureAwait(false);

                Section("Streamable HTTP (/mcp), handshake era");
                await RunStreamableHandshakeTestsAsync(baseUrl).ConfigureAwait(false);

                Section("Streamable HTTP (/mcp), stateless 2026-07-28 (Claude Code 2.1.x path)");
                await RunStatelessTestsAsync(baseUrl).ConfigureAwait(false);

                if (_StartServer && serverDll != null)
                {
                    Section("stdio transport");
                    await RunStdioTestsAsync(serverDll).ConfigureAwait(false);

                    Section("WebSocket transport");
                    await RunWebSocketTestsAsync(serverDll, host, port + 1).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Fatal error: {ex.Message}");
                Console.ResetColor();
                _TestsFailed++;
                _Failures.Add("Fatal: " + ex.Message);
            }
            finally
            {
                await CleanupAsync().ConfigureAwait(false);
            }

            Console.WriteLine();
            Console.WriteLine("══════════════════════════════════════════════════════════════");
            Console.WriteLine($"  Results: {_TestsPassed} passed, {_TestsFailed} failed");
            foreach (string failure in _Failures)
                Console.WriteLine($"    FAILED: {failure}");
            Console.WriteLine("══════════════════════════════════════════════════════════════");

            Environment.Exit(_TestsFailed > 0 ? 1 : 0);
        }

        #region Server-Lifecycle

        private static string BuildServer()
        {
            string projectPath = FindProjectPath("Verbex.Mcp");
            if (string.IsNullOrEmpty(projectPath))
                throw new InvalidOperationException("Could not find Verbex.Mcp project");

            // Run the server on the same framework and configuration as this harness.
            string tfm = "net" + Environment.Version.Major + ".0";
            string configuration = AppContext.BaseDirectory.Contains(
                Path.DirectorySeparatorChar + "Release" + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase) ? "Release" : "Debug";

            Console.WriteLine($"[Setup] Building Verbex.Mcp ({configuration}, {tfm})...");
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            foreach (string arg in new[] { "build", projectPath, "-f", tfm, "-c", configuration, "-v", "q", "-nologo" })
                psi.ArgumentList.Add(arg);

            using Process build = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start dotnet build");
            string output = build.StandardOutput.ReadToEnd() + build.StandardError.ReadToEnd();
            build.WaitForExit();
            if (build.ExitCode != 0)
                throw new InvalidOperationException("Verbex.Mcp build failed:" + Environment.NewLine + output);

            string dll = Path.Combine(Path.GetDirectoryName(projectPath)!, "bin", configuration, tfm, "verbex-mcp.dll");
            if (!File.Exists(dll))
                throw new InvalidOperationException("Built server not found at " + dll);

            return dll;
        }

        private static Process StartServer(string transport, string serverDll, string host, int port)
        {
            Console.WriteLine($"[Setup] Starting Verbex MCP Server ({transport}) on {host}:{port}...");

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            foreach (string arg in new[] { serverDll, "--transport", transport, "--host", host, "--port", port.ToString() })
                psi.ArgumentList.Add(arg);

            Process process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start server process");
            _ServerProcesses.Add(process);

            process.OutputDataReceived += (s, e) =>
            {
                if (_Verbose && !string.IsNullOrEmpty(e.Data))
                    Console.WriteLine($"[Server] {e.Data}");
            };
            process.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    Console.WriteLine($"[Server Error] {e.Data}");
            };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            Console.WriteLine($"[Setup] Server process started (PID: {process.Id})");
            return process;
        }

        private static async Task<McpHttpClient> ConnectHttpClientAsync(string baseUrl)
        {
            Console.WriteLine($"[Setup] Connecting MCP client to {baseUrl}...");

            // ConnectAsync pings the server and returns false (rather than throwing) until it is listening.
            for (int attempt = 1; attempt <= 60; attempt++)
            {
                McpHttpClient client = new McpHttpClient();
                if (await client.ConnectAsync(baseUrl).ConfigureAwait(false))
                {
                    Console.WriteLine("[Setup] Client connected successfully");
                    return client;
                }

                client.Dispose();
                await Task.Delay(500).ConfigureAwait(false);
            }

            throw new InvalidOperationException($"Failed to connect to {baseUrl} after 60 attempts");
        }

        #endregion

        #region HTTP-Tool-Tests

        private static async Task RunHttpToolTestsAsync()
        {
            // Test order matters for some tests (create before use, etc.)
            await TestInitializeAsync().ConfigureAwait(false);
            await TestToolsListAsync().ConfigureAwait(false);
            await TestListIndicesEmptyAsync().ConfigureAwait(false);
            await TestCreateIndexAsync().ConfigureAwait(false);
            await TestCreateIndexDuplicateAsync().ConfigureAwait(false);
            await TestListIndicesWithIndexAsync().ConfigureAwait(false);
            await TestStatisticsEmptyAsync().ConfigureAwait(false);
            await TestAddDocumentAsync().ConfigureAwait(false);
            await TestAddDocumentWithMetadataAsync().ConfigureAwait(false);
            await TestAddMultipleDocumentsAsync().ConfigureAwait(false);
            await TestAddDocumentValidationAsync().ConfigureAwait(false);
            await TestListDocumentsAsync().ConfigureAwait(false);
            await TestListDocumentsPaginationAsync().ConfigureAwait(false);
            await TestGetDocumentAsync().ConfigureAwait(false);
            await TestGetDocumentMetadataAsync().ConfigureAwait(false);
            await TestStatisticsWithDocumentsAsync().ConfigureAwait(false);
            await TestSearchBasicAsync().ConfigureAwait(false);
            await TestSearchWithAndLogicAsync().ConfigureAwait(false);
            await TestSearchWithLabelsAsync().ConfigureAwait(false);
            await TestSearchWithTagsAsync().ConfigureAwait(false);
            await TestSearchDetailsAsync().ConfigureAwait(false);
            await TestSearchNoResultsAsync().ConfigureAwait(false);
            await TestSearchEmptyQueryAsync().ConfigureAwait(false);
            await TestAddLabelsAsync().ConfigureAwait(false);
            await TestAddTagsAsync().ConfigureAwait(false);
            await TestIndexExistsAsync().ConfigureAwait(false);
            await TestDocumentExistsAsync().ConfigureAwait(false);
            await TestDeleteDocumentAsync().ConfigureAwait(false);
            await TestDeleteIndexAsync().ConfigureAwait(false);
            await TestInvalidIndexNamesAsync().ConfigureAwait(false);
            await TestConcurrentAddsAsync().ConfigureAwait(false);
            await TestUnknownToolAsync().ConfigureAwait(false);
            await TestProtocolPingAsync().ConfigureAwait(false);
            await TestNoDiagnosticToolsAsync().ConfigureAwait(false);
            await TestBareToolMethodRejectedAsync().ConfigureAwait(false);
            await TestTagSchemaValidationAsync().ConfigureAwait(false);
        }

        private static async Task TestInitializeAsync()
        {
            await RunTestAsync("Initialize MCP Connection", async () =>
            {
                // Voltaic 2.1+ clients perform the initialize handshake on connect.
                Assert(!String.IsNullOrEmpty(_Client!.SessionId), "Connect should have opened a session");
                AssertEquals(McpProtocol.LatestProtocolVersion, _Client.ProtocolVersion, "Negotiated protocol version");
                Console.WriteLine($"    Negotiated: {_Client.ProtocolVersion}");

                // A second initialize on the same session is rejected.
                await AssertSecondInitializeRejectedAsync((m, p) => _Client.CallAsync<JsonElement>(m, p)).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private static async Task TestToolsListAsync()
        {
            await RunTestAsync("tools/list advertises all Verbex tools with schemas", async () =>
            {
                JsonElement listResult = await _Client!.CallAsync<JsonElement>("tools/list", null).ConfigureAwait(false);
                AssertVerbexTools(listResult);
            }).ConfigureAwait(false);
        }

        private static async Task TestListIndicesEmptyAsync()
        {
            await RunTestAsync("List Indices (initial)", async () =>
            {
                JsonElement data = await CallToolDataAsync("verbex_list_indices", new { }).ConfigureAwait(false);

                Assert(data.TryGetProperty("indices", out _), "Should have indices array");
                Console.WriteLine($"    Found {data.GetProperty("indices").GetArrayLength()} existing indices");
            }).ConfigureAwait(false);
        }

        private static async Task TestCreateIndexAsync()
        {
            await RunTestAsync("Create Index", async () =>
            {
                JsonElement data = await CallToolDataAsync("verbex_create_index", new
                {
                    name = _TestIndexName,
                    inMemory = false,
                    enableLemmatizer = true,
                    enableStopWords = true,
                    minTokenLength = 2
                }).ConfigureAwait(false);

                Assert(data.GetProperty("success").GetBoolean(), "Create should succeed");
                AssertEquals(_TestIndexName, data.GetProperty("indexName").GetString()!, "Index name should match");
                Console.WriteLine($"    Created index: {_TestIndexName}");
            }).ConfigureAwait(false);
        }

        private static async Task TestCreateIndexDuplicateAsync()
        {
            await RunTestAsync("Create Index (duplicate is an isError result)", async () =>
            {
                JsonElement result = await CallToolAsync("verbex_create_index", new { name = _TestIndexName }).ConfigureAwait(false);
                AssertToolError(result, "already exists");
            }).ConfigureAwait(false);
        }

        private static async Task TestListIndicesWithIndexAsync()
        {
            await RunTestAsync("List Indices (with created index)", async () =>
            {
                JsonElement data = await CallToolDataAsync("verbex_list_indices", new { }).ConfigureAwait(false);
                List<string> names = data.GetProperty("indices").EnumerateArray().Select(e => e.GetString() ?? "").ToList();

                Assert(names.Contains(_TestIndexName), $"Should find {_TestIndexName} in indices list");
                Console.WriteLine($"    Index {_TestIndexName} found in list");
            }).ConfigureAwait(false);
        }

        private static async Task TestStatisticsEmptyAsync()
        {
            await RunTestAsync("Statistics (empty index)", async () =>
            {
                JsonElement data = await CallToolDataAsync("verbex_statistics", new { index = _TestIndexName }).ConfigureAwait(false);

                AssertEquals(0L, data.GetProperty("documentCount").GetInt64(), "Document count should be 0");
                AssertEquals(0L, data.GetProperty("termCount").GetInt64(), "Term count should be 0");
                Console.WriteLine("    Empty index confirmed");
            }).ConfigureAwait(false);
        }

        private static async Task TestAddDocumentAsync()
        {
            await RunTestAsync("Add Document (basic)", async () =>
            {
                JsonElement data = await CallToolDataAsync("verbex_add_document", new
                {
                    index = _TestIndexName,
                    name = "test-document-1",
                    content = "The quick brown fox jumps over the lazy dog. This is a test document for full-text search."
                }).ConfigureAwait(false);

                Assert(data.GetProperty("success").GetBoolean(), "Add should succeed");
                Assert(data.TryGetProperty("documentId", out JsonElement docId), "Should return documentId");

                _TestDocumentId = docId.GetString();
                Console.WriteLine($"    Added document: {_TestDocumentId}");
            }).ConfigureAwait(false);
        }

        private static async Task TestAddDocumentWithMetadataAsync()
        {
            await RunTestAsync("Add Document (with labels and tags)", async () =>
            {
                JsonElement data = await CallToolDataAsync("verbex_add_document", new
                {
                    index = _TestIndexName,
                    name = "test-document-2",
                    content = "Machine learning and artificial intelligence are transforming software development. Neural networks enable pattern recognition.",
                    labels = new[] { "ai", "technology", "programming" },
                    tags = new { category = "tech", priority = "high", author = "test-suite" }
                }).ConfigureAwait(false);

                Assert(data.GetProperty("success").GetBoolean(), "Add should succeed");
                _MetadataDocumentId = data.GetProperty("documentId").GetString();
                Console.WriteLine($"    Added document with metadata: {_MetadataDocumentId}");
            }).ConfigureAwait(false);
        }

        private static async Task TestAddMultipleDocumentsAsync()
        {
            await RunTestAsync("Add Multiple Documents", async () =>
            {
                string[] documents = new[]
                {
                    "Python programming language is excellent for data science and machine learning applications.",
                    "JavaScript frameworks like React and Vue enable modern web development with component architecture.",
                    "Database optimization involves indexing strategies and query performance tuning.",
                    "Cloud computing platforms provide scalable infrastructure for enterprise applications.",
                    "API design best practices include RESTful endpoints and proper authentication mechanisms."
                };

                int added = 0;
                foreach (string content in documents)
                {
                    JsonElement data = await CallToolDataAsync("verbex_add_document", new
                    {
                        index = _TestIndexName,
                        name = $"bulk-doc-{added + 1}",
                        content = content,
                        labels = new[] { "bulk", "test" }
                    }).ConfigureAwait(false);

                    Assert(data.GetProperty("success").GetBoolean(), $"Add of bulk-doc-{added + 1} should succeed");
                    added++;
                }

                Console.WriteLine($"    Added {added} documents");
            }).ConfigureAwait(false);
        }

        private static async Task TestAddDocumentValidationAsync()
        {
            await RunTestAsync("Add Document (missing content is an isError result)", async () =>
            {
                JsonElement result = await CallToolAsync("verbex_add_document", new
                {
                    index = _TestIndexName,
                    name = "no-content",
                    content = "   "
                }).ConfigureAwait(false);

                AssertToolError(result, "content is required");
            }).ConfigureAwait(false);
        }

        private static async Task TestListDocumentsAsync()
        {
            await RunTestAsync("List Documents", async () =>
            {
                JsonElement data = await CallToolDataAsync("verbex_list_documents", new
                {
                    index = _TestIndexName,
                    limit = 100
                }).ConfigureAwait(false);

                int count = data.GetProperty("count").GetInt32();
                AssertEquals(7, count, "Document count");

                Console.WriteLine($"    Listed {count} documents:");
                foreach (JsonElement doc in data.GetProperty("documents").EnumerateArray().Take(3))
                    Console.WriteLine($"      - {doc.GetProperty("documentPath").GetString()}");
                if (count > 3)
                    Console.WriteLine($"      ... and {count - 3} more");
            }).ConfigureAwait(false);
        }

        private static async Task TestListDocumentsPaginationAsync()
        {
            await RunTestAsync("List Documents (pagination)", async () =>
            {
                JsonElement page1 = await CallToolDataAsync("verbex_list_documents", new { index = _TestIndexName, limit = 4, offset = 0 }).ConfigureAwait(false);
                JsonElement page2 = await CallToolDataAsync("verbex_list_documents", new { index = _TestIndexName, limit = 4, offset = 4 }).ConfigureAwait(false);

                AssertEquals(4, page1.GetProperty("count").GetInt32(), "Page 1 size");
                AssertEquals(3, page2.GetProperty("count").GetInt32(), "Page 2 size");

                HashSet<string> ids = new HashSet<string>();
                foreach (JsonElement doc in page1.GetProperty("documents").EnumerateArray().Concat(page2.GetProperty("documents").EnumerateArray()))
                    ids.Add(doc.GetProperty("documentId").GetString()!);
                AssertEquals(7, ids.Count, "Pages should not overlap");
            }).ConfigureAwait(false);
        }

        private static async Task TestGetDocumentAsync()
        {
            await RunTestAsync("Get Document", async () =>
            {
                Assert(!string.IsNullOrEmpty(_TestDocumentId), "Test document ID should be set");

                JsonElement data = await CallToolDataAsync("verbex_get_document", new
                {
                    index = _TestIndexName,
                    documentId = _TestDocumentId
                }).ConfigureAwait(false);

                AssertEquals(_TestDocumentId, data.GetProperty("documentId").GetString()!, "Document ID should match");
                AssertEquals("test-document-1", data.GetProperty("documentPath").GetString(), "Document name");
                Assert(data.GetProperty("documentLength").GetInt64() > 0, "Document should have content length");
                Console.WriteLine($"    Retrieved document: {data.GetProperty("documentPath").GetString()}");
                Console.WriteLine($"    Length: {data.GetProperty("documentLength").GetInt64()} chars");
            }).ConfigureAwait(false);
        }

        private static async Task TestGetDocumentMetadataAsync()
        {
            await RunTestAsync("Get Document (labels and tags round-trip)", async () =>
            {
                Assert(!string.IsNullOrEmpty(_MetadataDocumentId), "Metadata document ID should be set");

                JsonElement data = await CallToolDataAsync("verbex_get_document", new
                {
                    index = _TestIndexName,
                    documentId = _MetadataDocumentId
                }).ConfigureAwait(false);

                List<string> labels = data.GetProperty("labels").EnumerateArray().Select(e => e.GetString() ?? "").ToList();
                foreach (string expected in new[] { "ai", "technology", "programming" })
                    Assert(labels.Contains(expected), $"Labels should contain '{expected}' (got: {string.Join(",", labels)})");

                JsonElement tags = data.GetProperty("tags");
                AssertEquals("tech", tags.GetProperty("category").GetString(), "Tag category");
                AssertEquals("high", tags.GetProperty("priority").GetString(), "Tag priority");
                AssertEquals("test-suite", tags.GetProperty("author").GetString(), "Tag author");
            }).ConfigureAwait(false);
        }

        private static async Task TestStatisticsWithDocumentsAsync()
        {
            await RunTestAsync("Statistics (with documents)", async () =>
            {
                JsonElement data = await CallToolDataAsync("verbex_statistics", new { index = _TestIndexName }).ConfigureAwait(false);

                long docCount = data.GetProperty("documentCount").GetInt64();
                long termCount = data.GetProperty("termCount").GetInt64();

                AssertEquals(7L, docCount, "Document count");
                Assert(termCount > 0, "Term count should be > 0");

                long postingCount = data.GetProperty("postingCount").GetInt64();
                double avgTerms = data.GetProperty("averageTermsPerDocument").GetDouble();
                Assert(postingCount >= termCount, $"Posting count ({postingCount}) should be >= term count ({termCount})");
                Assert(Math.Abs(avgTerms - Math.Round((double)postingCount / docCount, 4)) < 0.0001,
                    $"averageTermsPerDocument ({avgTerms}) should equal postingCount / documentCount");

                Console.WriteLine($"    Documents: {docCount}");
                Console.WriteLine($"    Terms: {termCount}");
                Console.WriteLine($"    Avg doc length: {data.GetProperty("averageDocumentLength").GetDouble():F1}");
            }).ConfigureAwait(false);
        }

        private static async Task TestSearchBasicAsync()
        {
            await RunTestAsync("Search (basic OR query)", async () =>
            {
                JsonElement data = await CallToolDataAsync("verbex_search", new
                {
                    index = _TestIndexName,
                    query = "machine learning programming",
                    maxResults = 5
                }).ConfigureAwait(false);

                int totalCount = data.GetProperty("totalCount").GetInt32();
                Assert(totalCount > 0, "Should find results");

                JsonElement results = data.GetProperty("results");
                Assert(results.GetArrayLength() <= 5, "maxResults should cap result count");

                double previous = double.MaxValue;
                Console.WriteLine($"    Found {totalCount} results:");
                foreach (JsonElement r in results.EnumerateArray())
                {
                    double score = r.GetProperty("score").GetDouble();
                    Assert(score <= previous, "Results should be ordered by descending score");
                    previous = score;
                    Console.WriteLine($"      - {r.GetProperty("documentName").GetString()} score {score:F4}, matches {r.GetProperty("matchedTermCount").GetInt32()}");
                }
            }).ConfigureAwait(false);
        }

        private static async Task TestSearchWithAndLogicAsync()
        {
            await RunTestAsync("Search (AND logic)", async () =>
            {
                JsonElement orData = await CallToolDataAsync("verbex_search", new
                {
                    index = _TestIndexName,
                    query = "machine database",
                    maxResults = 10
                }).ConfigureAwait(false);

                JsonElement andData = await CallToolDataAsync("verbex_search", new
                {
                    index = _TestIndexName,
                    query = "machine database",
                    useAndLogic = true,
                    maxResults = 10
                }).ConfigureAwait(false);

                int orCount = orData.GetProperty("totalCount").GetInt32();
                int andCount = andData.GetProperty("totalCount").GetInt32();
                Console.WriteLine($"    OR: {orCount} results, AND: {andCount} results");

                Assert(orCount >= 2, $"OR should match documents with either term (got {orCount})");
                AssertEquals(0, andCount, "No document contains both 'machine' and 'database'");
            }).ConfigureAwait(false);
        }

        private static async Task TestSearchWithLabelsAsync()
        {
            await RunTestAsync("Search (with label filter)", async () =>
            {
                JsonElement data = await CallToolDataAsync("verbex_search", new
                {
                    index = _TestIndexName,
                    query = "programming development",
                    labels = new[] { "ai" },
                    maxResults = 10
                }).ConfigureAwait(false);

                List<string> ids = data.GetProperty("results").EnumerateArray().Select(r => r.GetProperty("documentId").GetString() ?? "").ToList();
                Console.WriteLine($"    Found {ids.Count} results with label filter 'ai'");

                AssertEquals(1, ids.Count, "Only the 'ai'-labeled document should match");
                AssertEquals(_MetadataDocumentId, ids[0], "Matched document");
            }).ConfigureAwait(false);
        }

        private static async Task TestSearchWithTagsAsync()
        {
            await RunTestAsync("Search (with tag filter)", async () =>
            {
                JsonElement data = await CallToolDataAsync("verbex_search", new
                {
                    index = _TestIndexName,
                    query = "software",
                    tags = new { category = "tech" },
                    maxResults = 10
                }).ConfigureAwait(false);

                List<string> ids = data.GetProperty("results").EnumerateArray().Select(r => r.GetProperty("documentId").GetString() ?? "").ToList();
                Console.WriteLine($"    Found {ids.Count} results with tag filter category=tech");

                AssertEquals(1, ids.Count, "Only the category=tech document should match");
                AssertEquals(_MetadataDocumentId, ids[0], "Matched document");

                JsonElement none = await CallToolDataAsync("verbex_search", new
                {
                    index = _TestIndexName,
                    query = "software",
                    tags = new { category = "nonexistent" },
                    maxResults = 10
                }).ConfigureAwait(false);
                AssertEquals(0, none.GetProperty("results").GetArrayLength(), "Non-matching tag should filter everything out");
            }).ConfigureAwait(false);
        }

        private static async Task TestSearchDetailsAsync()
        {
            await RunTestAsync("Search (matched terms, term details, document term stats)", async () =>
            {
                JsonElement data = await CallToolDataAsync("verbex_search", new
                {
                    index = _TestIndexName,
                    query = "machine learning",
                    maxResults = 3,
                    includeMatchedTerms = true,
                    includeTermDetails = true,
                    includeDocumentTermStats = true
                }).ConfigureAwait(false);

                JsonElement results = data.GetProperty("results");
                Assert(results.GetArrayLength() > 0, "Should find results");

                foreach (JsonElement r in results.EnumerateArray())
                {
                    Assert(r.GetProperty("matchedTerms").GetArrayLength() > 0, "matchedTerms should be populated");
                    JsonElement details = r.GetProperty("termDetails");
                    Assert(details.GetArrayLength() > 0, "termDetails should be populated");
                    foreach (JsonElement d in details.EnumerateArray())
                        Assert(d.GetProperty("frequency").GetInt32() > 0, "termDetails frequency should be > 0");

                    JsonElement stats = r.GetProperty("documentTermStats");
                    Assert(stats.GetProperty("uniqueTermCount").GetInt32() > 0, "uniqueTermCount should be > 0");
                    Assert(stats.GetProperty("totalTermOccurrences").GetInt32() >= stats.GetProperty("uniqueTermCount").GetInt32(),
                        "totalTermOccurrences should be >= uniqueTermCount");
                }

                JsonElement plain = await CallToolDataAsync("verbex_search", new { index = _TestIndexName, query = "machine learning" }).ConfigureAwait(false);
                JsonElement first = plain.GetProperty("results")[0];
                Assert(!first.TryGetProperty("termDetails", out _), "termDetails should be omitted by default");
                Assert(!first.TryGetProperty("documentTermStats", out _), "documentTermStats should be omitted by default");
            }).ConfigureAwait(false);
        }

        private static async Task TestSearchNoResultsAsync()
        {
            await RunTestAsync("Search (no results)", async () =>
            {
                JsonElement data = await CallToolDataAsync("verbex_search", new
                {
                    index = _TestIndexName,
                    query = "xyzzynonexistentterm12345",
                    maxResults = 10
                }).ConfigureAwait(false);

                AssertEquals(0, data.GetProperty("totalCount").GetInt32(), "Should find no results for nonexistent term");
                Console.WriteLine("    Correctly returned 0 results for nonexistent term");
            }).ConfigureAwait(false);
        }

        private static async Task TestSearchEmptyQueryAsync()
        {
            await RunTestAsync("Search (empty query is an isError result)", async () =>
            {
                JsonElement result = await CallToolAsync("verbex_search", new { index = _TestIndexName, query = "" }).ConfigureAwait(false);
                AssertToolError(result, "Query is required");
            }).ConfigureAwait(false);
        }

        private static async Task TestAddLabelsAsync()
        {
            await RunTestAsync("Add Labels to Document", async () =>
            {
                Assert(!string.IsNullOrEmpty(_TestDocumentId), "Test document ID should be set");

                JsonElement data = await CallToolDataAsync("verbex_add_labels", new
                {
                    index = _TestIndexName,
                    documentId = _TestDocumentId,
                    labels = new[] { "important", "reviewed", "v1" }
                }).ConfigureAwait(false);

                Assert(data.GetProperty("success").GetBoolean(), "Add labels should succeed");

                JsonElement docData = await CallToolDataAsync("verbex_get_document", new
                {
                    index = _TestIndexName,
                    documentId = _TestDocumentId
                }).ConfigureAwait(false);

                List<string> labels = docData.GetProperty("labels").EnumerateArray().Select(e => e.GetString() ?? "").ToList();
                foreach (string expected in new[] { "important", "reviewed", "v1" })
                    Assert(labels.Contains(expected), $"Labels should contain '{expected}'");
                Console.WriteLine("    Added 3 labels to document");
            }).ConfigureAwait(false);
        }

        private static async Task TestAddTagsAsync()
        {
            await RunTestAsync("Add Tags to Document", async () =>
            {
                Assert(!string.IsNullOrEmpty(_TestDocumentId), "Test document ID should be set");

                JsonElement data = await CallToolDataAsync("verbex_add_tags", new
                {
                    index = _TestIndexName,
                    documentId = _TestDocumentId,
                    tags = new { status = "approved", reviewer = "test-suite", version = "1.0" }
                }).ConfigureAwait(false);

                Assert(data.GetProperty("success").GetBoolean(), "Add tags should succeed");

                JsonElement docData = await CallToolDataAsync("verbex_get_document", new
                {
                    index = _TestIndexName,
                    documentId = _TestDocumentId
                }).ConfigureAwait(false);

                AssertEquals("approved", docData.GetProperty("tags").GetProperty("status").GetString(), "Tag status");
                AssertEquals("1.0", docData.GetProperty("tags").GetProperty("version").GetString(), "Tag version");
                Console.WriteLine("    Added 3 tags to document");
            }).ConfigureAwait(false);
        }

        private static async Task TestIndexExistsAsync()
        {
            await RunTestAsync("Index Exists Check", async () =>
            {
                JsonElement data = await CallToolDataAsync("verbex_index_exists", new { name = _TestIndexName }).ConfigureAwait(false);

                Assert(data.GetProperty("exists").GetBoolean(), "Test index should exist");
                AssertEquals(_TestIndexName, data.GetProperty("indexName").GetString()!, "Index name should match");
                Console.WriteLine($"    Index {_TestIndexName} exists: true");

                JsonElement data2 = await CallToolDataAsync("verbex_index_exists", new { name = "nonexistent-index-12345" }).ConfigureAwait(false);

                Assert(!data2.GetProperty("exists").GetBoolean(), "Nonexistent index should not exist");
                Console.WriteLine("    Index nonexistent-index-12345 exists: false");
            }).ConfigureAwait(false);
        }

        private static async Task TestDocumentExistsAsync()
        {
            await RunTestAsync("Document Exists Check", async () =>
            {
                Assert(!string.IsNullOrEmpty(_TestDocumentId), "Test document ID should be set");

                JsonElement data = await CallToolDataAsync("verbex_document_exists", new
                {
                    index = _TestIndexName,
                    documentId = _TestDocumentId
                }).ConfigureAwait(false);

                Assert(data.GetProperty("exists").GetBoolean(), "Test document should exist");
                AssertEquals(_TestDocumentId, data.GetProperty("documentId").GetString()!, "Document ID should match");
                Console.WriteLine($"    Document {_TestDocumentId} exists: true");

                JsonElement data2 = await CallToolDataAsync("verbex_document_exists", new
                {
                    index = _TestIndexName,
                    documentId = "nonexistent-doc-12345"
                }).ConfigureAwait(false);

                Assert(!data2.GetProperty("exists").GetBoolean(), "Nonexistent document should not exist");
                Console.WriteLine("    Document nonexistent-doc-12345 exists: false");
            }).ConfigureAwait(false);
        }

        private static async Task TestDeleteDocumentAsync()
        {
            await RunTestAsync("Delete Document", async () =>
            {
                JsonElement addData = await CallToolDataAsync("verbex_add_document", new
                {
                    index = _TestIndexName,
                    name = "to-be-deleted",
                    content = "This document will be deleted as part of testing."
                }).ConfigureAwait(false);

                string docId = addData.GetProperty("documentId").GetString()!;

                JsonElement data = await CallToolDataAsync("verbex_delete_document", new
                {
                    index = _TestIndexName,
                    documentId = docId
                }).ConfigureAwait(false);

                Assert(data.GetProperty("success").GetBoolean(), "Delete should succeed");
                Console.WriteLine($"    Deleted document: {docId}");

                JsonElement getResult = await CallToolAsync("verbex_get_document", new
                {
                    index = _TestIndexName,
                    documentId = docId
                }).ConfigureAwait(false);
                AssertToolError(getResult, "not found");

                JsonElement again = await CallToolDataAsync("verbex_delete_document", new
                {
                    index = _TestIndexName,
                    documentId = docId
                }).ConfigureAwait(false);
                Assert(!again.GetProperty("success").GetBoolean(), "Deleting an already-deleted document should report success=false");
            }).ConfigureAwait(false);
        }

        private static async Task TestDeleteIndexAsync()
        {
            await RunTestAsync("Delete Index", async () =>
            {
                string tempIndex = "temp-delete-test";

                JsonElement created = await CallToolDataAsync("verbex_create_index", new { name = tempIndex, inMemory = true }).ConfigureAwait(false);
                Assert(created.GetProperty("success").GetBoolean(), "Create should succeed");

                JsonElement data = await CallToolDataAsync("verbex_delete_index", new { name = tempIndex }).ConfigureAwait(false);
                Assert(data.GetProperty("success").GetBoolean(), "Delete index should succeed");

                JsonElement exists = await CallToolDataAsync("verbex_index_exists", new { name = tempIndex }).ConfigureAwait(false);
                Assert(!exists.GetProperty("exists").GetBoolean(), "Deleted index should no longer exist");
                Console.WriteLine($"    Deleted temporary index: {tempIndex}");
            }).ConfigureAwait(false);
        }

        private static async Task TestInvalidIndexNamesAsync()
        {
            await RunTestAsync("Invalid index names are rejected (no path traversal)", async () =>
            {
                // A directory beside the storage root: '../<name>' from the storage root would resolve to it.
                string sentinelName = "sentinel-" + Guid.NewGuid().ToString("N");
                string? sentinelDir = null;
                if (_StartServer)
                {
                    sentinelDir = Path.Combine(_TempRoot, sentinelName);
                    Directory.CreateDirectory(sentinelDir);
                    File.WriteAllText(Path.Combine(sentinelDir, "keep.txt"), "must survive");
                }

                string[] badNames = new[] { "../" + sentinelName, "..", ".", "a/b", "a\\b", "..\\" + sentinelName };
                foreach (string bad in badNames)
                {
                    AssertToolError(await CallToolAsync("verbex_delete_index", new { name = bad }).ConfigureAwait(false), "Invalid index name");
                    AssertToolError(await CallToolAsync("verbex_create_index", new { name = bad }).ConfigureAwait(false), "Invalid index name");
                    AssertToolError(await CallToolAsync("verbex_index_exists", new { name = bad }).ConfigureAwait(false), "Invalid index name");
                    AssertToolError(await CallToolAsync("verbex_search", new { index = bad, query = "test" }).ConfigureAwait(false), "Invalid index name");
                    AssertToolError(await CallToolAsync("verbex_add_document", new { index = bad, name = "x", content = "y" }).ConfigureAwait(false), "Invalid index name");
                }

                if (sentinelDir != null)
                {
                    Assert(File.Exists(Path.Combine(sentinelDir, "keep.txt")), "Directory outside the storage root must not be deleted");
                    Directory.Delete(sentinelDir, true);
                }

                Console.WriteLine($"    Rejected {badNames.Length} unsafe names across 5 tools");
            }).ConfigureAwait(false);
        }

        private static async Task TestConcurrentAddsAsync()
        {
            await RunTestAsync("Concurrent adds to a not-yet-open index", async () =>
            {
                string index = "concurrent-" + Guid.NewGuid().ToString("N").Substring(0, 8);
                int count = 12;

                Task<JsonElement>[] tasks = Enumerable.Range(0, count).Select(i => CallToolDataAsync("verbex_add_document", new
                {
                    index = index,
                    name = $"concurrent-{i}",
                    content = $"Concurrent document number {i} about parallel indexing."
                })).ToArray();

                JsonElement[] results = await Task.WhenAll(tasks).ConfigureAwait(false);
                foreach (JsonElement r in results)
                    Assert(r.GetProperty("success").GetBoolean(), "Each concurrent add should succeed");

                JsonElement stats = await CallToolDataAsync("verbex_statistics", new { index = index }).ConfigureAwait(false);
                AssertEquals((long)count, stats.GetProperty("documentCount").GetInt64(), "All concurrent documents should be indexed");

                JsonElement deleted = await CallToolDataAsync("verbex_delete_index", new { name = index }).ConfigureAwait(false);
                Assert(deleted.GetProperty("success").GetBoolean(), "Cleanup delete should succeed");
                Console.WriteLine($"    {count} concurrent adds indexed");
            }).ConfigureAwait(false);
        }

        private static async Task TestUnknownToolAsync()
        {
            await RunTestAsync("Unknown tool returns a JSON-RPC error", async () =>
            {
                bool threw = false;
                try
                {
                    await CallToolAsync("verbex_does_not_exist", new { }).ConfigureAwait(false);
                }
                catch (Exception e) when (e is not TestAssertException)
                {
                    threw = true;
                    Assert(e.Message.Contains("not found", StringComparison.OrdinalIgnoreCase), "Error should say the tool was not found: " + e.Message);
                }

                Assert(threw, "Calling an unknown tool should fail");
            }).ConfigureAwait(false);
        }

        private static async Task TestProtocolPingAsync()
        {
            await RunTestAsync("Protocol ping returns an empty result", async () =>
            {
                // Voltaic 2.x answers ping per the MCP spec with {} rather than the v1.x "pong" string.
                await _Client!.PingAsync().ConfigureAwait(false);

                JsonElement result = await _Client!.CallAsync<JsonElement>("ping", null).ConfigureAwait(false);
                AssertEquals(JsonValueKind.Object, result.ValueKind, "ping result kind");
                AssertEquals(0, result.EnumerateObject().Count(), "ping result should be an empty object: " + result.GetRawText());
            }).ConfigureAwait(false);
        }

        private static async Task TestNoDiagnosticToolsAsync()
        {
            await RunTestAsync("Voltaic demo tools are not published or callable", async () =>
            {
                JsonElement list = await _Client!.CallAsync<JsonElement>("tools/list", null).ConfigureAwait(false);
                List<string> names = list.GetProperty("tools").EnumerateArray().Select(t => t.GetProperty("name").GetString() ?? "").ToList();
                foreach (string demo in _VoltaicDemoTools)
                    Assert(!names.Contains(demo), $"tools/list should not include Voltaic demo tool '{demo}'");

                foreach (string demo in _VoltaicDemoTools)
                {
                    bool threw = false;
                    try
                    {
                        await CallToolAsync(demo, new { }).ConfigureAwait(false);
                    }
                    catch (Exception e) when (e is not TestAssertException)
                    {
                        threw = true;
                        Assert(e.Message.Contains("not found", StringComparison.OrdinalIgnoreCase), $"tools/call {demo} should report not found: " + e.Message);
                    }

                    Assert(threw, $"tools/call {demo} should fail");
                }

                Console.WriteLine($"    {_VoltaicDemoTools.Length} demo tools absent from tools/list and rejected by tools/call");
            }).ConfigureAwait(false);
        }

        private static async Task TestBareToolMethodRejectedAsync()
        {
            await RunTestAsync("Tools are not callable as bare JSON-RPC methods (-32601)", async () =>
            {
                bool threw = false;
                try
                {
                    await _Client!.CallAsync<JsonElement>("verbex_list_indices", new { }).ConfigureAwait(false);
                }
                catch (Exception e) when (e is not TestAssertException)
                {
                    threw = true;
                    Assert(e.Message.Contains("-32601"), "Bare tool call should fail with -32601: " + e.Message);
                }

                Assert(threw, "Calling a tool by its bare name should fail");

                JsonElement viaToolsCall = await CallToolDataAsync("verbex_list_indices", new { }).ConfigureAwait(false);
                Assert(viaToolsCall.TryGetProperty("indices", out _), "The same tool should succeed through tools/call");
            }).ConfigureAwait(false);
        }

        private static async Task TestTagSchemaValidationAsync()
        {
            await RunTestAsync("Tag values must be strings (additionalProperties schema is enforced as an isError result)", async () =>
            {
                // Positive: string tag values pass validation and are stored.
                JsonElement ok = await CallToolDataAsync("verbex_add_document", new
                {
                    index = _TestIndexName,
                    name = "schema-string-tags",
                    content = "Schema validation positive case.",
                    tags = new { stage = "validated" }
                }).ConfigureAwait(false);
                Assert(ok.GetProperty("success").GetBoolean(), "String tag values should be accepted");

                // Negative: non-string tag values fail input validation before the handler runs, for each tool
                // whose schema declares tags as additionalProperties { type: string }. Since Voltaic 2.1 this is an
                // isError tool result (as the 2025-11-25 specification requires), not a JSON-RPC -32602 error.
                object[] badCalls = new object[]
                {
                    ToolCall("verbex_add_document", new { index = _TestIndexName, name = "schema-bad-tags", content = "Should not be indexed.", tags = new { priority = 5 } }),
                    ToolCall("verbex_add_tags", new { index = _TestIndexName, documentId = ok.GetProperty("documentId").GetString(), tags = new { reviewed = true } }),
                    ToolCall("verbex_search", new { index = _TestIndexName, query = "schema", tags = new { nested = new { a = "b" } } })
                };

                foreach (object call in badCalls)
                {
                    JsonElement result = await _Client!.CallAsync<JsonElement>("tools/call", call).ConfigureAwait(false);
                    Assert(result.TryGetProperty("isError", out JsonElement isError) && isError.ValueKind == JsonValueKind.True,
                        "Non-string tag value should be rejected with isError: " + JsonSerializer.Serialize(call) + " -> " + result.GetRawText());
                    string text = result.GetProperty("content")[0].GetProperty("text").GetString() ?? "";
                    Assert(text.Contains("tags", StringComparison.OrdinalIgnoreCase), "Validation error should name the tags argument: " + text);
                }

                JsonElement list = await CallToolDataAsync("verbex_list_documents", new { index = _TestIndexName, limit = 1000 }).ConfigureAwait(false);
                bool indexed = list.GetProperty("documents").EnumerateArray().Any(d => d.GetProperty("documentPath").GetString() == "schema-bad-tags");
                Assert(!indexed, "A rejected call must not index the document");

                JsonElement removed = await CallToolDataAsync("verbex_delete_document", new { index = _TestIndexName, documentId = ok.GetProperty("documentId").GetString() }).ConfigureAwait(false);
                Assert(removed.GetProperty("success").GetBoolean(), "Cleanup delete should succeed");
            }).ConfigureAwait(false);
        }

        #endregion

        #region Transport-Tests

        private static async Task RunStreamableHandshakeTestsAsync(string baseUrl)
        {
            using McpHttpClient client = new McpHttpClient();

            await RunTestAsync("Streamable HTTP: connect + initialize (2025-11-25)", async () =>
            {
                client.ProtocolVersion = "2025-11-25";
                Assert(await client.ConnectStreamableAsync(baseUrl).ConfigureAwait(false), "ConnectStreamableAsync should succeed");
                Assert(!String.IsNullOrEmpty(client.SessionId), "Connect should have opened a session");
                AssertEquals("2025-11-25", client.ProtocolVersion, "Negotiated protocol version");
                await AssertSecondInitializeRejectedAsync((m, p) => client.CallAsync<JsonElement>(m, p)).ConfigureAwait(false);
            }).ConfigureAwait(false);

            await RunTransportSmokeAsync("Streamable HTTP", (m, p) => client.CallAsync<JsonElement>(m, p)).ConfigureAwait(false);
        }

        private static async Task RunStatelessTestsAsync(string baseUrl)
        {
            using McpHttpClient client = new McpHttpClient();

            await RunTestAsync("Stateless: server/discover", async () =>
            {
                Assert(await client.ConnectStatelessAsync(baseUrl).ConfigureAwait(false), "ConnectStatelessAsync should succeed");
                Assert(client.IsStateless, "Client should be in stateless mode");
                McpDiscoverResult discover = await client.DiscoverAsync().ConfigureAwait(false);
                Assert(discover.SupportedVersions != null && discover.SupportedVersions.Contains("2026-07-28"), "Server should advertise 2026-07-28");
            }).ConfigureAwait(false);

            await RunTestAsync("Stateless: tools/list carries resultType and lists Verbex tools", async () =>
            {
                // Claude Code 2.1.x rejects stateless results without resultType, which made it see zero tools
                // before Voltaic 1.1.0.
                JsonElement list = await client.CallStatelessAsync<JsonElement>("tools/list").ConfigureAwait(false);
                AssertEquals("complete", list.GetProperty("resultType").GetString(), "tools/list resultType");
                Assert(list.TryGetProperty("ttlMs", out _), "tools/list should carry ttlMs");
                Assert(list.TryGetProperty("cacheScope", out _), "tools/list should carry cacheScope");
                AssertVerbexTools(list);
            }).ConfigureAwait(false);

            await RunTransportSmokeAsync("Stateless", async (m, p) =>
            {
                if (m != "tools/call")
                    return await client.CallStatelessAsync<JsonElement>(m).ConfigureAwait(false);

                JsonElement callParams = JsonSerializer.SerializeToElement(p);
                string name = callParams.GetProperty("name").GetString()!;
                JsonRpcResponse response = await client.CallToolStatelessAsync(name, callParams.GetProperty("arguments")).ConfigureAwait(false);
                if (response.Error != null)
                    throw new McpProtocolException(response.Error.Code, response.Error.Message ?? "tools/call failed");

                JsonElement result = JsonSerializer.SerializeToElement(response.Result);
                AssertEquals("complete", result.GetProperty("resultType").GetString(), "tools/call resultType");
                return result;
            }).ConfigureAwait(false);
        }

        private static async Task RunStdioTestsAsync(string serverDll)
        {
            using McpClient client = new McpClient();

            await RunTestAsync("stdio: launch + initialize", async () =>
            {
                Assert(await client.LaunchServerAsync("dotnet", new[] { serverDll, "--transport", "stdio" }).ConfigureAwait(false), "Server should launch");
                AssertInitializeResult(client.InitializeResult);
                await AssertSecondInitializeRejectedAsync((m, p) => client.CallAsync<JsonElement>(m, p)).ConfigureAwait(false);
            }).ConfigureAwait(false);

            await RunTransportSmokeAsync("stdio", (m, p) => client.CallAsync<JsonElement>(m, p)).ConfigureAwait(false);
        }

        private static async Task RunWebSocketTestsAsync(string serverDll, string host, int port)
        {
            StartServer("websocket", serverDll, host, port);
            using McpWebsocketsClient client = new McpWebsocketsClient();

            await RunTestAsync("WebSocket: connect + initialize", async () =>
            {
                string url = $"ws://{host}:{port}/mcp";
                bool connected = false;
                for (int attempt = 0; attempt < 60 && !connected; attempt++)
                {
                    connected = await client.ConnectAsync(url).ConfigureAwait(false);
                    if (!connected) await Task.Delay(500).ConfigureAwait(false);
                }
                Assert(connected, "WebSocket client should connect to " + url);
                AssertInitializeResult(client.InitializeResult);
                await AssertSecondInitializeRejectedAsync((m, p) => client.CallAsync<JsonElement>(m, p)).ConfigureAwait(false);
            }).ConfigureAwait(false);

            await RunTransportSmokeAsync("WebSocket", (m, p) => client.CallAsync<JsonElement>(m, p)).ConfigureAwait(false);
        }

        private static void AssertInitializeResult(JsonElement? initializeResult)
        {
            Assert(initializeResult.HasValue, "Client should hold the initialize result after connecting");
            JsonElement init = initializeResult!.Value;
            Assert(init.TryGetProperty("protocolVersion", out _), "Should have protocolVersion");
            Assert(init.TryGetProperty("capabilities", out _), "Should have capabilities");
            AssertEquals("Verbex MCP Server", init.GetProperty("serverInfo").GetProperty("name").GetString(), "Server name");
        }

        private static async Task AssertSecondInitializeRejectedAsync(Func<string, object?, Task<JsonElement>> rpc)
        {
            bool threw = false;
            try
            {
                await rpc("initialize", new
                {
                    protocolVersion = McpProtocol.LatestProtocolVersion,
                    capabilities = new { },
                    clientInfo = new { name = "verbex-test", version = "1.0.0" }
                }).ConfigureAwait(false);
            }
            catch (Exception e) when (e is not TestAssertException)
            {
                threw = true;
                Assert(e.Message.Contains("-32600") || e.Message.Contains("already initialized"), "Second initialize should fail with -32600: " + e.Message);
            }

            Assert(threw, "A second initialize on an initialized session should be rejected");
        }

        /// <summary>
        /// Transport-independent checks: discovery, a full write/read/search/delete cycle, and isError reporting.
        /// </summary>
        private static async Task RunTransportSmokeAsync(string label, Func<string, object?, Task<JsonElement>> rpc)
        {
            string index = label.ToLowerInvariant().Replace(' ', '-') + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);

            await RunTestAsync($"{label}: tools/list advertises all Verbex tools", async () =>
            {
                JsonElement list = await rpc("tools/list", null).ConfigureAwait(false);
                AssertVerbexTools(list);
            }).ConfigureAwait(false);

            await RunTestAsync($"{label}: ping returns an empty result", async () =>
            {
                if (label == "Stateless")
                {
                    // The stateless 2026-07-28 revision removed ping; servers answer -32601.
                    bool threw = false;
                    try
                    {
                        await rpc("ping", null).ConfigureAwait(false);
                    }
                    catch (Exception e) when (e is not TestAssertException)
                    {
                        threw = true;
                        Assert(e.Message.Contains("-32601") || e.Message.Contains("does not exist"), "Stateless ping should be method-not-found: " + e.Message);
                    }

                    Assert(threw, "Stateless ping should be rejected");
                    return;
                }

                // {} under handshake revisions.
                JsonElement pong = await rpc("ping", null).ConfigureAwait(false);
                AssertEquals(JsonValueKind.Object, pong.ValueKind, "ping result kind");
                List<string> props = pong.EnumerateObject().Select(p => p.Name).Where(n => n != "resultType").ToList();
                Assert(!props.Any(), "ping result should carry no payload: " + pong.GetRawText());
            }).ConfigureAwait(false);

            await RunTestAsync($"{label}: create, add, search, get, delete", async () =>
            {
                JsonElement created = ParseToolResult(await rpc("tools/call", ToolCall("verbex_create_index", new { name = index })).ConfigureAwait(false));
                Assert(created.GetProperty("success").GetBoolean(), "Create should succeed");

                JsonElement added = ParseToolResult(await rpc("tools/call", ToolCall("verbex_add_document", new
                {
                    index = index,
                    name = "transport-doc",
                    content = "Transport parity check for the Verbex quasar retrieval engine.",
                    labels = new[] { "parity" }
                })).ConfigureAwait(false));
                string docId = added.GetProperty("documentId").GetString()!;

                JsonElement search = ParseToolResult(await rpc("tools/call", ToolCall("verbex_search", new { index = index, query = "quasar", labels = new[] { "parity" } })).ConfigureAwait(false));
                AssertEquals(1, search.GetProperty("totalCount").GetInt32(), "Search should find the document");
                AssertEquals(docId, search.GetProperty("results")[0].GetProperty("documentId").GetString(), "Search hit");

                JsonElement doc = ParseToolResult(await rpc("tools/call", ToolCall("verbex_get_document", new { index = index, documentId = docId })).ConfigureAwait(false));
                AssertEquals("transport-doc", doc.GetProperty("documentPath").GetString(), "Document name");

                JsonElement deleted = ParseToolResult(await rpc("tools/call", ToolCall("verbex_delete_index", new { name = index })).ConfigureAwait(false));
                Assert(deleted.GetProperty("success").GetBoolean(), "Delete index should succeed");
            }).ConfigureAwait(false);

            await RunTestAsync($"{label}: tool errors are isError results", async () =>
            {
                JsonElement result = await rpc("tools/call", ToolCall("verbex_search", new { index = index, query = " " })).ConfigureAwait(false);
                AssertToolError(result, "Query is required");
            }).ConfigureAwait(false);
        }

        #endregion

        #region Test-Infrastructure

        private static void Section(string title)
        {
            Console.WriteLine();
            Console.WriteLine($"── {title} ──");
        }

        private static object ToolCall(string name, object arguments)
        {
            return new { name = name, arguments = arguments };
        }

        private static async Task<JsonElement> CallToolAsync(string toolName, object arguments)
        {
            return await _Client!.CallAsync<JsonElement>("tools/call", ToolCall(toolName, arguments)).ConfigureAwait(false);
        }

        /// <summary>
        /// Calls a tool, asserts it did not report isError, and returns its parsed JSON payload.
        /// </summary>
        private static async Task<JsonElement> CallToolDataAsync(string toolName, object arguments)
        {
            JsonElement result = await CallToolAsync(toolName, arguments).ConfigureAwait(false);
            return ParseToolResult(result);
        }

        private static JsonElement ParseToolResult(JsonElement result)
        {
            JsonElement data = ParseToolText(result);
            if (result.TryGetProperty("isError", out JsonElement isError) && isError.ValueKind == JsonValueKind.True)
                throw new TestAssertException("Tool returned isError: " + data.GetRawText());
            return data;
        }

        private static JsonElement ParseToolText(JsonElement result)
        {
            JsonElement content = result.GetProperty("content")[0];
            AssertEquals("text", content.GetProperty("type").GetString(), "Tool content type");
            string text = content.GetProperty("text").GetString()!;
            return JsonSerializer.Deserialize<JsonElement>(text);
        }

        private static void AssertToolError(JsonElement result, string expectedFragment)
        {
            Assert(result.TryGetProperty("isError", out JsonElement isError) && isError.ValueKind == JsonValueKind.True,
                "Result should be flagged isError: " + result.GetRawText());
            JsonElement data = ParseToolText(result);
            string message = data.GetProperty("error").GetString() ?? "";
            Assert(message.Contains(expectedFragment, StringComparison.OrdinalIgnoreCase),
                $"Error should mention '{expectedFragment}' (got '{message}')");
        }

        private static void AssertVerbexTools(JsonElement listResult)
        {
            Dictionary<string, JsonElement> tools = new Dictionary<string, JsonElement>();
            foreach (JsonElement tool in listResult.GetProperty("tools").EnumerateArray())
                tools[tool.GetProperty("name").GetString()!] = tool;

            foreach (string expected in _ExpectedTools)
            {
                Assert(tools.TryGetValue(expected, out JsonElement tool), $"tools/list should include {expected} (got: {string.Join(", ", tools.Keys)})");
                Assert(!string.IsNullOrWhiteSpace(tool.GetProperty("description").GetString()), $"{expected} should have a description");
                AssertEquals("object", tool.GetProperty("inputSchema").GetProperty("type").GetString(), $"{expected} inputSchema type");
            }

            List<string> unexpected = tools.Keys.Where(k => !_ExpectedTools.Contains(k)).ToList();
            Assert(!unexpected.Any(), $"tools/list should contain only Verbex tools (unexpected: {string.Join(", ", unexpected)})");

            Console.WriteLine($"    tools/list: exactly the {_ExpectedTools.Length} Verbex tools");
        }

        private static async Task RunTestAsync(string testName, Func<Task> test)
        {
            Console.Write($"  [{(_TestsPassed + _TestsFailed + 1):D2}] {testName}... ");

            try
            {
                await test().ConfigureAwait(false);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("PASSED");
                Console.ResetColor();
                _TestsPassed++;
            }
            catch (TestAssertException ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"FAILED: {ex.Message}");
                Console.ResetColor();
                _TestsFailed++;
                _Failures.Add($"{testName}: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"ERROR: {ex.GetType().Name}: {ex.Message}");
                Console.ResetColor();
                _TestsFailed++;
                _Failures.Add($"{testName}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new TestAssertException(message);
        }

        private static void AssertEquals<T>(T expected, T actual, string message)
        {
            if (!Equals(expected, actual))
                throw new TestAssertException($"{message} (expected: {expected}, actual: {actual})");
        }

        private static string FindProjectPath(string projectName)
        {
            string currentDir = Directory.GetCurrentDirectory();
            string baseDir = AppContext.BaseDirectory;

            string[] searchPaths = new[]
            {
                Path.Combine(currentDir, "..", projectName, $"{projectName}.csproj"),
                Path.Combine(currentDir, "src", projectName, $"{projectName}.csproj"),
                Path.Combine(currentDir, projectName, $"{projectName}.csproj"),
                Path.Combine(baseDir, "..", "..", "..", "..", projectName, $"{projectName}.csproj")
            };

            foreach (string path in searchPaths)
            {
                string fullPath = Path.GetFullPath(path);
                if (File.Exists(fullPath))
                    return fullPath;
            }

            return string.Empty;
        }

        private static async Task CleanupAsync()
        {
            Console.WriteLine();
            Console.WriteLine("[Cleanup] Cleaning up...");

            if (_Client != null)
            {
                try
                {
                    await CallToolAsync("verbex_delete_index", new { name = _TestIndexName }).ConfigureAwait(false);
                    Console.WriteLine($"[Cleanup] Deleted test index: {_TestIndexName}");
                }
                catch
                {
                    // Ignore cleanup errors
                }

                _Client.Dispose();
            }

            foreach (Process process in _ServerProcesses)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(true);
                        await process.WaitForExitAsync().ConfigureAwait(false);
                    }
                    process.Dispose();
                }
                catch
                {
                    // Ignore
                }
            }
            Console.WriteLine("[Cleanup] Server processes stopped");

            if (!string.IsNullOrEmpty(_TempRoot) && Directory.Exists(_TempRoot))
            {
                try
                {
                    Directory.Delete(_TempRoot, true);
                }
                catch (IOException)
                {
                    Console.WriteLine($"[Cleanup] Could not remove {_TempRoot}");
                }
            }
        }

        #endregion
    }
}
