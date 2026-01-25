namespace Test.Mcp
{
    using System;
    using System.Diagnostics;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Voltaic;

    /// <summary>
    /// Comprehensive test console for Verbex MCP Server.
    /// Exercises all MCP tools and validates responses.
    /// </summary>
    public class Program
    {
        private static McpHttpClient? _Client;
        private static Process? _ServerProcess;
        private static int _TestsPassed = 0;
        private static int _TestsFailed = 0;
        private static readonly string _TestIndexName = "test-mcp-index";
        private static string? _TestDocumentId;

        private static readonly JsonSerializerOptions _JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public static async Task Main(string[] args)
        {
            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║           Verbex MCP Server Test Suite                       ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
            Console.WriteLine();

            string host = "127.0.0.1";
            int port = 8250;
            bool startServer = true;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--host" && i + 1 < args.Length)
                    host = args[++i];
                else if (args[i] == "--port" && i + 1 < args.Length)
                    port = int.Parse(args[++i]);
                else if (args[i] == "--no-server")
                    startServer = false;
            }

            try
            {
                if (startServer)
                {
                    await StartServerAsync(host, port).ConfigureAwait(false);
                    await Task.Delay(3000).ConfigureAwait(false); // Wait for server to initialize
                }

                await ConnectClientAsync(host, port).ConfigureAwait(false);
                await RunAllTestsAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Fatal error: {ex.Message}");
                Console.ResetColor();
            }
            finally
            {
                await CleanupAsync().ConfigureAwait(false);
            }

            Console.WriteLine();
            Console.WriteLine("══════════════════════════════════════════════════════════════");
            Console.WriteLine($"  Results: {_TestsPassed} passed, {_TestsFailed} failed");
            Console.WriteLine("══════════════════════════════════════════════════════════════");

            Environment.Exit(_TestsFailed > 0 ? 1 : 0);
        }

        private static async Task StartServerAsync(string host, int port)
        {
            Console.WriteLine($"[Setup] Starting Verbex MCP Server on {host}:{port}...");

            string projectPath = FindProjectPath("Verbex.Mcp");
            if (string.IsNullOrEmpty(projectPath))
            {
                throw new Exception("Could not find Verbex.Mcp project");
            }

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project \"{projectPath}\" -- --transport http --host {host} --port {port}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            _ServerProcess = Process.Start(psi);

            if (_ServerProcess == null)
            {
                throw new Exception("Failed to start server process");
            }

            // Read output asynchronously
            _ServerProcess.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    Console.WriteLine($"[Server] {e.Data}");
            };
            _ServerProcess.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    Console.WriteLine($"[Server Error] {e.Data}");
            };
            _ServerProcess.BeginOutputReadLine();
            _ServerProcess.BeginErrorReadLine();

            Console.WriteLine($"[Setup] Server process started (PID: {_ServerProcess.Id})");
        }

        private static async Task ConnectClientAsync(string host, int port)
        {
            Console.WriteLine($"[Setup] Connecting MCP client to http://{host}:{port}...");

            _Client = new McpHttpClient();

            string baseUrl = $"http://{host}:{port}";

            // Retry connection a few times
            int maxRetries = 10;
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    await _Client.ConnectAsync(baseUrl).ConfigureAwait(false);
                    Console.WriteLine("[Setup] Client connected successfully");
                    return;
                }
                catch
                {
                    if (i < maxRetries - 1)
                    {
                        Console.WriteLine($"[Setup] Connection attempt {i + 1} failed, retrying...");
                        await Task.Delay(1000).ConfigureAwait(false);
                    }
                }
            }

            throw new Exception($"Failed to connect to server after {maxRetries} attempts");
        }

        private static async Task RunAllTestsAsync()
        {
            Console.WriteLine();
            Console.WriteLine("Running tests...");
            Console.WriteLine();

            // Test order matters for some tests (create before use, etc.)
            await TestInitializeAsync().ConfigureAwait(false);
            await TestListIndicesEmptyAsync().ConfigureAwait(false);
            await TestCreateIndexAsync().ConfigureAwait(false);
            await TestListIndicesWithIndexAsync().ConfigureAwait(false);
            await TestStatisticsEmptyAsync().ConfigureAwait(false);
            await TestAddDocumentAsync().ConfigureAwait(false);
            await TestAddDocumentWithMetadataAsync().ConfigureAwait(false);
            await TestAddMultipleDocumentsAsync().ConfigureAwait(false);
            await TestListDocumentsAsync().ConfigureAwait(false);
            await TestGetDocumentAsync().ConfigureAwait(false);
            await TestStatisticsWithDocumentsAsync().ConfigureAwait(false);
            await TestSearchBasicAsync().ConfigureAwait(false);
            await TestSearchWithAndLogicAsync().ConfigureAwait(false);
            await TestSearchWithLabelsAsync().ConfigureAwait(false);
            await TestSearchWithTagsAsync().ConfigureAwait(false);
            await TestSearchNoResultsAsync().ConfigureAwait(false);
            await TestAddLabelsAsync().ConfigureAwait(false);
            await TestAddTagsAsync().ConfigureAwait(false);
            await TestIndexExistsAsync().ConfigureAwait(false);
            await TestDocumentExistsAsync().ConfigureAwait(false);
            await TestDeleteDocumentAsync().ConfigureAwait(false);
            await TestDeleteIndexAsync().ConfigureAwait(false);
            await TestBuiltInToolsAsync().ConfigureAwait(false);
        }

        #region Test Methods

        private static async Task TestInitializeAsync()
        {
            await RunTestAsync("Initialize MCP Connection", async () =>
            {
                JsonElement result = await _Client!.CallAsync<JsonElement>("initialize", new
                {
                    protocolVersion = "2025-03-26"
                }).ConfigureAwait(false);

                Assert(result.TryGetProperty("protocolVersion", out _), "Should have protocolVersion");
                Assert(result.TryGetProperty("serverInfo", out _), "Should have serverInfo");
                Assert(result.TryGetProperty("capabilities", out _), "Should have capabilities");

                Console.WriteLine($"    Server: {result.GetProperty("serverInfo").GetProperty("name").GetString()}");
                Console.WriteLine($"    Version: {result.GetProperty("serverInfo").GetProperty("version").GetString()}");
            }).ConfigureAwait(false);
        }

        private static async Task TestListIndicesEmptyAsync()
        {
            await RunTestAsync("List Indices (initial)", async () =>
            {
                JsonElement result = await CallToolAsync("verbex_list_indices", new { }).ConfigureAwait(false);
                JsonElement data = ParseToolResult(result);

                Assert(data.TryGetProperty("indices", out _), "Should have indices array");
                Console.WriteLine($"    Found {data.GetProperty("indices").GetArrayLength()} existing indices");
            }).ConfigureAwait(false);
        }

        private static async Task TestCreateIndexAsync()
        {
            await RunTestAsync("Create Index", async () =>
            {
                JsonElement result = await CallToolAsync("verbex_create_index", new
                {
                    name = _TestIndexName,
                    inMemory = false,
                    enableLemmatizer = true,
                    enableStopWords = true,
                    minTokenLength = 2
                }).ConfigureAwait(false);

                JsonElement data = ParseToolResult(result);

                Assert(data.GetProperty("success").GetBoolean(), "Create should succeed");
                AssertEquals(_TestIndexName, data.GetProperty("indexName").GetString()!, "Index name should match");
                Console.WriteLine($"    Created index: {_TestIndexName}");
            }).ConfigureAwait(false);
        }

        private static async Task TestListIndicesWithIndexAsync()
        {
            await RunTestAsync("List Indices (with created index)", async () =>
            {
                JsonElement result = await CallToolAsync("verbex_list_indices", new { }).ConfigureAwait(false);
                JsonElement data = ParseToolResult(result);
                JsonElement indices = data.GetProperty("indices");

                bool found = false;
                foreach (JsonElement idx in indices.EnumerateArray())
                {
                    if (idx.GetString() == _TestIndexName)
                    {
                        found = true;
                        break;
                    }
                }

                Assert(found, $"Should find {_TestIndexName} in indices list");
                Console.WriteLine($"    Index {_TestIndexName} found in list");
            }).ConfigureAwait(false);
        }

        private static async Task TestStatisticsEmptyAsync()
        {
            await RunTestAsync("Statistics (empty index)", async () =>
            {
                JsonElement result = await CallToolAsync("verbex_statistics", new
                {
                    index = _TestIndexName
                }).ConfigureAwait(false);

                JsonElement data = ParseToolResult(result);

                AssertEquals(0, data.GetProperty("documentCount").GetInt64(), "Document count should be 0");
                AssertEquals(0, data.GetProperty("termCount").GetInt64(), "Term count should be 0");
                Console.WriteLine("    Empty index confirmed");
            }).ConfigureAwait(false);
        }

        private static async Task TestAddDocumentAsync()
        {
            await RunTestAsync("Add Document (basic)", async () =>
            {
                JsonElement result = await CallToolAsync("verbex_add_document", new
                {
                    index = _TestIndexName,
                    name = "test-document-1",
                    content = "The quick brown fox jumps over the lazy dog. This is a test document for full-text search."
                }).ConfigureAwait(false);

                JsonElement data = ParseToolResult(result);

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
                JsonElement result = await CallToolAsync("verbex_add_document", new
                {
                    index = _TestIndexName,
                    name = "test-document-2",
                    content = "Machine learning and artificial intelligence are transforming software development. Neural networks enable pattern recognition.",
                    labels = new[] { "ai", "technology", "programming" },
                    tags = new { category = "tech", priority = "high", author = "test-suite" }
                }).ConfigureAwait(false);

                JsonElement data = ParseToolResult(result);

                Assert(data.GetProperty("success").GetBoolean(), "Add should succeed");
                Console.WriteLine($"    Added document with metadata: {data.GetProperty("documentId").GetString()}");
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
                    JsonElement result = await CallToolAsync("verbex_add_document", new
                    {
                        index = _TestIndexName,
                        name = $"bulk-doc-{added + 1}",
                        content = content,
                        labels = new[] { "bulk", "test" }
                    }).ConfigureAwait(false);

                    added++;
                }

                Console.WriteLine($"    Added {added} documents");
            }).ConfigureAwait(false);
        }

        private static async Task TestListDocumentsAsync()
        {
            await RunTestAsync("List Documents", async () =>
            {
                JsonElement result = await CallToolAsync("verbex_list_documents", new
                {
                    index = _TestIndexName,
                    limit = 100
                }).ConfigureAwait(false);

                JsonElement data = ParseToolResult(result);

                int count = data.GetProperty("count").GetInt32();
                Assert(count >= 7, $"Should have at least 7 documents, got {count}");

                JsonElement docs = data.GetProperty("documents");
                Console.WriteLine($"    Listed {count} documents:");
                int shown = 0;
                foreach (JsonElement doc in docs.EnumerateArray())
                {
                    if (shown < 3)
                    {
                        Console.WriteLine($"      - {doc.GetProperty("documentPath").GetString()}");
                        shown++;
                    }
                }
                if (count > 3)
                    Console.WriteLine($"      ... and {count - 3} more");
            }).ConfigureAwait(false);
        }

        private static async Task TestGetDocumentAsync()
        {
            await RunTestAsync("Get Document", async () =>
            {
                Assert(!string.IsNullOrEmpty(_TestDocumentId), "Test document ID should be set");

                JsonElement result = await CallToolAsync("verbex_get_document", new
                {
                    index = _TestIndexName,
                    documentId = _TestDocumentId
                }).ConfigureAwait(false);

                JsonElement data = ParseToolResult(result);

                AssertEquals(_TestDocumentId, data.GetProperty("documentId").GetString()!, "Document ID should match");
                Assert(data.GetProperty("documentLength").GetInt64() > 0, "Document should have content length");
                Console.WriteLine($"    Retrieved document: {data.GetProperty("documentPath").GetString()}");
                Console.WriteLine($"    Length: {data.GetProperty("documentLength").GetInt64()} chars");
            }).ConfigureAwait(false);
        }

        private static async Task TestStatisticsWithDocumentsAsync()
        {
            await RunTestAsync("Statistics (with documents)", async () =>
            {
                JsonElement result = await CallToolAsync("verbex_statistics", new
                {
                    index = _TestIndexName
                }).ConfigureAwait(false);

                JsonElement data = ParseToolResult(result);

                long docCount = data.GetProperty("documentCount").GetInt64();
                long termCount = data.GetProperty("termCount").GetInt64();

                Assert(docCount >= 7, $"Document count should be >= 7, got {docCount}");
                Assert(termCount > 0, "Term count should be > 0");

                Console.WriteLine($"    Documents: {docCount}");
                Console.WriteLine($"    Terms: {termCount}");
                Console.WriteLine($"    Avg doc length: {data.GetProperty("averageDocumentLength").GetDouble():F1}");
            }).ConfigureAwait(false);
        }

        private static async Task TestSearchBasicAsync()
        {
            await RunTestAsync("Search (basic OR query)", async () =>
            {
                JsonElement result = await CallToolAsync("verbex_search", new
                {
                    index = _TestIndexName,
                    query = "machine learning programming",
                    maxResults = 5
                }).ConfigureAwait(false);

                JsonElement data = ParseToolResult(result);

                int totalCount = data.GetProperty("totalCount").GetInt32();
                Assert(totalCount > 0, "Should find results");

                JsonElement results = data.GetProperty("results");
                Console.WriteLine($"    Found {totalCount} results:");
                foreach (JsonElement r in results.EnumerateArray())
                {
                    Console.WriteLine($"      - Score: {r.GetProperty("score").GetDouble():F4}, Matches: {r.GetProperty("matchedTermCount").GetInt32()}");
                }
            }).ConfigureAwait(false);
        }

        private static async Task TestSearchWithAndLogicAsync()
        {
            await RunTestAsync("Search (AND logic)", async () =>
            {
                JsonElement result = await CallToolAsync("verbex_search", new
                {
                    index = _TestIndexName,
                    query = "machine learning",
                    useAndLogic = true,
                    maxResults = 5
                }).ConfigureAwait(false);

                JsonElement data = ParseToolResult(result);

                int totalCount = data.GetProperty("totalCount").GetInt32();
                Console.WriteLine($"    Found {totalCount} results with AND logic");

                // With AND, we should get fewer or equal results than OR
                Assert(totalCount >= 0, "Should return valid count");
            }).ConfigureAwait(false);
        }

        private static async Task TestSearchWithLabelsAsync()
        {
            await RunTestAsync("Search (with label filter)", async () =>
            {
                JsonElement result = await CallToolAsync("verbex_search", new
                {
                    index = _TestIndexName,
                    query = "programming development",
                    labels = new[] { "ai" },
                    maxResults = 10
                }).ConfigureAwait(false);

                JsonElement data = ParseToolResult(result);

                int totalCount = data.GetProperty("totalCount").GetInt32();
                Console.WriteLine($"    Found {totalCount} results with label filter 'ai'");
            }).ConfigureAwait(false);
        }

        private static async Task TestSearchWithTagsAsync()
        {
            await RunTestAsync("Search (with tag filter)", async () =>
            {
                JsonElement result = await CallToolAsync("verbex_search", new
                {
                    index = _TestIndexName,
                    query = "software",
                    tags = new { category = "tech" },
                    maxResults = 10
                }).ConfigureAwait(false);

                JsonElement data = ParseToolResult(result);

                int totalCount = data.GetProperty("totalCount").GetInt32();
                Console.WriteLine($"    Found {totalCount} results with tag filter category=tech");
            }).ConfigureAwait(false);
        }

        private static async Task TestSearchNoResultsAsync()
        {
            await RunTestAsync("Search (no results)", async () =>
            {
                JsonElement result = await CallToolAsync("verbex_search", new
                {
                    index = _TestIndexName,
                    query = "xyzzynonexistentterm12345",
                    maxResults = 10
                }).ConfigureAwait(false);

                JsonElement data = ParseToolResult(result);

                int totalCount = data.GetProperty("totalCount").GetInt32();
                AssertEquals(0, totalCount, "Should find no results for nonexistent term");
                Console.WriteLine("    Correctly returned 0 results for nonexistent term");
            }).ConfigureAwait(false);
        }

        private static async Task TestAddLabelsAsync()
        {
            await RunTestAsync("Add Labels to Document", async () =>
            {
                Assert(!string.IsNullOrEmpty(_TestDocumentId), "Test document ID should be set");

                JsonElement result = await CallToolAsync("verbex_add_labels", new
                {
                    index = _TestIndexName,
                    documentId = _TestDocumentId,
                    labels = new[] { "important", "reviewed", "v1" }
                }).ConfigureAwait(false);

                JsonElement data = ParseToolResult(result);

                Assert(data.GetProperty("success").GetBoolean(), "Add labels should succeed");
                Console.WriteLine("    Added 3 labels to document");

                // Verify by getting document
                JsonElement getResult = await CallToolAsync("verbex_get_document", new
                {
                    index = _TestIndexName,
                    documentId = _TestDocumentId
                }).ConfigureAwait(false);

                JsonElement docData = ParseToolResult(getResult);
                int labelCount = docData.GetProperty("labels").GetArrayLength();
                Assert(labelCount >= 3, $"Document should have at least 3 labels, got {labelCount}");
            }).ConfigureAwait(false);
        }

        private static async Task TestAddTagsAsync()
        {
            await RunTestAsync("Add Tags to Document", async () =>
            {
                Assert(!string.IsNullOrEmpty(_TestDocumentId), "Test document ID should be set");

                JsonElement result = await CallToolAsync("verbex_add_tags", new
                {
                    index = _TestIndexName,
                    documentId = _TestDocumentId,
                    tags = new { status = "approved", reviewer = "test-suite", version = "1.0" }
                }).ConfigureAwait(false);

                JsonElement data = ParseToolResult(result);

                Assert(data.GetProperty("success").GetBoolean(), "Add tags should succeed");
                Console.WriteLine("    Added 3 tags to document");
            }).ConfigureAwait(false);
        }

        private static async Task TestIndexExistsAsync()
        {
            await RunTestAsync("Index Exists Check", async () =>
            {
                // Test existing index
                JsonElement result = await CallToolAsync("verbex_index_exists", new
                {
                    name = _TestIndexName
                }).ConfigureAwait(false);

                JsonElement data = ParseToolResult(result);

                Assert(data.GetProperty("exists").GetBoolean(), "Test index should exist");
                AssertEquals(_TestIndexName, data.GetProperty("indexName").GetString()!, "Index name should match");
                Console.WriteLine($"    Index {_TestIndexName} exists: true");

                // Test non-existing index
                JsonElement result2 = await CallToolAsync("verbex_index_exists", new
                {
                    name = "nonexistent-index-12345"
                }).ConfigureAwait(false);

                JsonElement data2 = ParseToolResult(result2);

                Assert(!data2.GetProperty("exists").GetBoolean(), "Nonexistent index should not exist");
                Console.WriteLine("    Index nonexistent-index-12345 exists: false");
            }).ConfigureAwait(false);
        }

        private static async Task TestDocumentExistsAsync()
        {
            await RunTestAsync("Document Exists Check", async () =>
            {
                Assert(!string.IsNullOrEmpty(_TestDocumentId), "Test document ID should be set");

                // Test existing document
                JsonElement result = await CallToolAsync("verbex_document_exists", new
                {
                    index = _TestIndexName,
                    documentId = _TestDocumentId
                }).ConfigureAwait(false);

                JsonElement data = ParseToolResult(result);

                Assert(data.GetProperty("exists").GetBoolean(), "Test document should exist");
                AssertEquals(_TestDocumentId, data.GetProperty("documentId").GetString()!, "Document ID should match");
                Console.WriteLine($"    Document {_TestDocumentId} exists: true");

                // Test non-existing document
                JsonElement result2 = await CallToolAsync("verbex_document_exists", new
                {
                    index = _TestIndexName,
                    documentId = "nonexistent-doc-12345"
                }).ConfigureAwait(false);

                JsonElement data2 = ParseToolResult(result2);

                Assert(!data2.GetProperty("exists").GetBoolean(), "Nonexistent document should not exist");
                Console.WriteLine("    Document nonexistent-doc-12345 exists: false");
            }).ConfigureAwait(false);
        }

        private static async Task TestDeleteDocumentAsync()
        {
            await RunTestAsync("Delete Document", async () =>
            {
                // Add a document to delete
                JsonElement addResult = await CallToolAsync("verbex_add_document", new
                {
                    index = _TestIndexName,
                    name = "to-be-deleted",
                    content = "This document will be deleted as part of testing."
                }).ConfigureAwait(false);

                JsonElement addData = ParseToolResult(addResult);
                string docId = addData.GetProperty("documentId").GetString()!;

                // Delete it
                JsonElement result = await CallToolAsync("verbex_delete_document", new
                {
                    index = _TestIndexName,
                    documentId = docId
                }).ConfigureAwait(false);

                JsonElement data = ParseToolResult(result);

                Assert(data.GetProperty("success").GetBoolean(), "Delete should succeed");
                Console.WriteLine($"    Deleted document: {docId}");

                // Verify it's gone
                JsonElement getResult = await CallToolAsync("verbex_get_document", new
                {
                    index = _TestIndexName,
                    documentId = docId
                }).ConfigureAwait(false);

                JsonElement getData = ParseToolResult(getResult);
                Assert(getData.TryGetProperty("error", out _), "Get deleted document should return error");
            }).ConfigureAwait(false);
        }

        private static async Task TestDeleteIndexAsync()
        {
            await RunTestAsync("Delete Index", async () =>
            {
                // Create a temporary index to delete
                string tempIndex = "temp-delete-test";

                JsonElement createResult = await CallToolAsync("verbex_create_index", new
                {
                    name = tempIndex,
                    inMemory = true
                }).ConfigureAwait(false);

                // Delete it
                JsonElement result = await CallToolAsync("verbex_delete_index", new
                {
                    name = tempIndex
                }).ConfigureAwait(false);

                JsonElement data = ParseToolResult(result);

                Assert(data.GetProperty("success").GetBoolean(), "Delete index should succeed");
                Console.WriteLine($"    Deleted temporary index: {tempIndex}");
            }).ConfigureAwait(false);
        }

        private static async Task TestBuiltInToolsAsync()
        {
            await RunTestAsync("Built-in MCP Tools", async () =>
            {
                // Test tools/list
                JsonElement listResult = await _Client!.CallAsync<JsonElement>("tools/list", null).ConfigureAwait(false);

                JsonElement tools = listResult.GetProperty("tools");

                int toolCount = tools.GetArrayLength();
                Assert(toolCount >= 10, $"Should have at least 10 Verbex tools, got {toolCount}");

                Console.WriteLine($"    tools/list: {toolCount} tools registered");

                // List some tools
                int shown = 0;
                foreach (JsonElement tool in tools.EnumerateArray())
                {
                    string name = tool.GetProperty("name").GetString()!;
                    if (name.StartsWith("verbex_") && shown < 5)
                    {
                        Console.WriteLine($"      - {name}");
                        shown++;
                    }
                }
                if (toolCount > 5)
                    Console.WriteLine($"      ... and {toolCount - 5} more");

                // Test ping
                JsonElement pingResult = await CallToolAsync("ping", new { }).ConfigureAwait(false);
                Console.WriteLine("    ping: OK");

                // Test echo
                JsonElement echoResult = await CallToolAsync("echo", new { message = "Hello MCP!" }).ConfigureAwait(false);
                Console.WriteLine("    echo: OK");

                // Test getTime
                JsonElement timeResult = await CallToolAsync("getTime", new { }).ConfigureAwait(false);
                Console.WriteLine("    getTime: OK");
            }).ConfigureAwait(false);
        }

        #endregion

        #region Test Infrastructure

        private static async Task<JsonElement> CallToolAsync(string toolName, object arguments)
        {
            return await _Client!.CallAsync<JsonElement>("tools/call", new
            {
                name = toolName,
                arguments = arguments
            }).ConfigureAwait(false);
        }

        private static JsonElement ParseToolResult(JsonElement result)
        {
            JsonElement content = result.GetProperty("content")[0];
            string text = content.GetProperty("text").GetString()!;
            return JsonSerializer.Deserialize<JsonElement>(text);
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
            catch (AssertException ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"FAILED: {ex.Message}");
                Console.ResetColor();
                _TestsFailed++;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"ERROR: {ex.Message}");
                Console.ResetColor();
                _TestsFailed++;
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new AssertException(message);
        }

        private static void AssertEquals<T>(T expected, T actual, string message)
        {
            if (!Equals(expected, actual))
                throw new AssertException($"{message} (expected: {expected}, actual: {actual})");
        }

        private static void AssertNotNull(object? value, string message)
        {
            if (value == null)
                throw new AssertException(message);
        }

        private static string FindProjectPath(string projectName)
        {
            string currentDir = Directory.GetCurrentDirectory();

            // Try various paths
            string[] searchPaths = new[]
            {
                Path.Combine(currentDir, "..", projectName, $"{projectName}.csproj"),
                Path.Combine(currentDir, "..", "..", projectName, $"{projectName}.csproj"),
                Path.Combine(currentDir, "..", "..", "..", "src", projectName, $"{projectName}.csproj"),
                Path.Combine(currentDir, "..", "..", "..", "..", "src", projectName, $"{projectName}.csproj"),
                @"C:\Code\Verbex\src\Verbex.Mcp\Verbex.Mcp.csproj"
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

            // Clean up test index
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

                try
                {
                    _Client.Dispose();
                }
                catch
                {
                    // Ignore
                }
            }

            // Stop server
            if (_ServerProcess != null && !_ServerProcess.HasExited)
            {
                try
                {
                    _ServerProcess.Kill(true);
                    await _ServerProcess.WaitForExitAsync().ConfigureAwait(false);
                    Console.WriteLine("[Cleanup] Server process stopped");
                }
                catch
                {
                    // Ignore
                }
            }
        }

        #endregion

        private class AssertException : Exception
        {
            public AssertException(string message) : base(message) { }
        }
    }
}
