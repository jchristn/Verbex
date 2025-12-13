namespace Verbex.Sdk.TestHarness
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Verbex.Sdk;

    /// <summary>
    /// Result of a single test.
    /// </summary>
    internal class TestResult
    {
        public string Name { get; }
        public bool Passed { get; }
        public string Message { get; }
        public double DurationMs { get; }

        public TestResult(string name, bool passed, string message, double durationMs)
        {
            Name = name;
            Passed = passed;
            Message = message;
            DurationMs = durationMs;
        }
    }

    /// <summary>
    /// Test harness for Verbex SDK.
    /// </summary>
    internal class TestHarness
    {
        private readonly string _Endpoint;
        private readonly string _AccessKey;
        private readonly string _TestIndexId;
        private readonly List<string> _TestDocuments;
        private readonly List<TestResult> _Results;
        private VerbexClient? _Client;
        private int _Passed;
        private int _Failed;

        public TestHarness(string endpoint, string accessKey)
        {
            _Endpoint = endpoint;
            _AccessKey = accessKey;
            _TestIndexId = $"test-index-{Guid.NewGuid().ToString("N")[..8]}";
            _TestDocuments = new List<string>();
            _Results = new List<TestResult>();
            _Passed = 0;
            _Failed = 0;
        }

        private void PrintHeader(string text)
        {
            Console.WriteLine();
            Console.WriteLine(new string('=', 60));
            Console.WriteLine($"  {text}");
            Console.WriteLine(new string('=', 60));
        }

        private void PrintSubheader(string text)
        {
            Console.WriteLine();
            Console.WriteLine($"--- {text} ---");
        }

        private void PrintResult(TestResult result)
        {
            string status = result.Passed ? "PASS" : "FAIL";
            Console.WriteLine($"  [{status}] {result.Name} ({result.DurationMs:F2}ms)");
            if (!string.IsNullOrEmpty(result.Message) && !result.Passed)
            {
                Console.WriteLine($"         Error: {result.Message}");
            }
        }

        private async Task<TestResult> RunTestAsync(string name, Func<Task> testFunc)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            TestResult result;
            try
            {
                await testFunc().ConfigureAwait(false);
                stopwatch.Stop();
                result = new TestResult(name, true, string.Empty, stopwatch.Elapsed.TotalMilliseconds);
                _Passed++;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                string message = $"{ex.GetType().Name}: {ex.Message}";
                result = new TestResult(name, false, message, stopwatch.Elapsed.TotalMilliseconds);
                _Failed++;
            }

            _Results.Add(result);
            PrintResult(result);
            return result;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception(message);
            }
        }

        private static void AssertNotNull(object? value, string fieldName)
        {
            Assert(value != null, $"{fieldName} should not be null");
        }

        private static void AssertEquals<T>(T actual, T expected, string fieldName)
        {
            Assert(Equals(actual, expected), $"{fieldName} expected '{expected}', got '{actual}'");
        }

        private static void AssertTrue(bool value, string fieldName)
        {
            Assert(value, $"{fieldName} should be True");
        }

        private static void AssertGreaterThan(int actual, int expected, string fieldName)
        {
            Assert(actual > expected, $"{fieldName} expected > {expected}, got {actual}");
        }

        // ==================== Health Tests ====================

        private async Task TestRootHealthCheckAsync()
        {
            ApiResponse<HealthData> response = await _Client!.RootHealthCheckAsync().ConfigureAwait(false);
            AssertTrue(response.Success, "response.Success");
            AssertEquals(response.StatusCode, 200, "response.StatusCode");
            AssertNotNull(response.Data, "response.Data");
            AssertEquals(response.Data!.Status, "Healthy", "data.Status");
            AssertNotNull(response.Data.Version, "data.Version");
            AssertNotNull(response.Data.Timestamp, "data.Timestamp");
        }

        private async Task TestHealthEndpointAsync()
        {
            ApiResponse<HealthData> response = await _Client!.HealthCheckAsync().ConfigureAwait(false);
            AssertTrue(response.Success, "response.Success");
            AssertEquals(response.StatusCode, 200, "response.StatusCode");
            AssertNotNull(response.Data, "response.Data");
            AssertEquals(response.Data!.Status, "Healthy", "data.Status");
            AssertNotNull(response.Data.Version, "data.Version");
            AssertNotNull(response.Data.Timestamp, "data.Timestamp");
        }

        // ==================== Authentication Tests ====================

        private async Task TestLoginSuccessAsync()
        {
            ApiResponse<LoginData> response = await _Client!.LoginAsync("admin", "password").ConfigureAwait(false);
            AssertTrue(response.Success, "response.Success");
            AssertEquals(response.StatusCode, 200, "response.StatusCode");
            AssertNotNull(response.Data, "response.Data");
            AssertNotNull(response.Data!.Token, "data.Token");
            AssertEquals(response.Data.Username, "admin", "data.Username");
        }

        private async Task TestLoginInvalidCredentialsAsync()
        {
            try
            {
                await _Client!.LoginAsync("invalid", "invalid").ConfigureAwait(false);
                Assert(false, "Should have thrown VerbexException");
            }
            catch (VerbexException ex)
            {
                AssertEquals(ex.StatusCode, 401, "error.StatusCode");
                AssertNotNull(ex.Message, "error.Message");
            }
        }

        private async Task TestValidateTokenAsync()
        {
            ApiResponse<ValidationData> response = await _Client!.ValidateTokenAsync().ConfigureAwait(false);
            AssertTrue(response.Success, "response.Success");
            AssertEquals(response.StatusCode, 200, "response.StatusCode");
            AssertNotNull(response.Data, "response.Data");
            AssertTrue(response.Data!.Valid, "data.Valid");
        }

        private async Task TestValidateInvalidTokenAsync()
        {
            using VerbexClient invalidClient = new VerbexClient(_Endpoint, "invalid-token");
            try
            {
                await invalidClient.ValidateTokenAsync().ConfigureAwait(false);
                Assert(false, "Should have thrown VerbexException");
            }
            catch (VerbexException ex)
            {
                AssertEquals(ex.StatusCode, 401, "error.StatusCode");
            }
        }

        // ==================== Index Management Tests ====================

        private async Task TestListIndicesInitialAsync()
        {
            ApiResponse<IndicesListData> response = await _Client!.ListIndicesAsync().ConfigureAwait(false);
            AssertTrue(response.Success, "response.Success");
            AssertEquals(response.StatusCode, 200, "response.StatusCode");
            AssertNotNull(response.Data, "response.Data");
            AssertNotNull(response.Data!.Indices, "data.Indices");
        }

        private async Task TestCreateIndexAsync()
        {
            ApiResponse<CreateIndexData> response = await _Client!.CreateIndexAsync(
                id: _TestIndexId,
                name: "Test Index",
                description: "A test index for SDK validation",
                inMemory: true,
                storageMode: "MemoryOnly"
            ).ConfigureAwait(false);
            AssertTrue(response.Success, "response.Success");
            AssertEquals(response.StatusCode, 201, "response.StatusCode");
            AssertNotNull(response.Data, "response.Data");
            AssertNotNull(response.Data!.Message, "data.Message");
            AssertNotNull(response.Data.Index, "data.Index");
            AssertEquals(response.Data.Index!.Id, _TestIndexId, "index.Id");
            AssertEquals(response.Data.Index.Name, "Test Index", "index.Name");
        }

        private async Task TestCreateDuplicateIndexAsync()
        {
            try
            {
                await _Client!.CreateIndexAsync(id: _TestIndexId, name: "Duplicate").ConfigureAwait(false);
                Assert(false, "Should have thrown VerbexException for duplicate");
            }
            catch (VerbexException ex)
            {
                AssertEquals(ex.StatusCode, 409, "error.StatusCode");
            }
        }

        private async Task TestGetIndexAsync()
        {
            ApiResponse<IndexInfo> response = await _Client!.GetIndexAsync(_TestIndexId).ConfigureAwait(false);
            AssertTrue(response.Success, "response.Success");
            AssertEquals(response.StatusCode, 200, "response.StatusCode");
            AssertNotNull(response.Data, "response.Data");
            AssertEquals(response.Data!.Id, _TestIndexId, "data.Id");
            AssertEquals(response.Data.Name, "Test Index", "data.Name");
            AssertNotNull(response.Data.CreatedUtc, "data.CreatedUtc");
        }

        private async Task TestGetIndexNotFoundAsync()
        {
            try
            {
                await _Client!.GetIndexAsync("non-existent-index-12345").ConfigureAwait(false);
                Assert(false, "Should have thrown VerbexException for not found");
            }
            catch (VerbexException ex)
            {
                AssertEquals(ex.StatusCode, 404, "error.StatusCode");
            }
        }

        private async Task TestListIndicesAfterCreateAsync()
        {
            List<IndexInfo> indices = await _Client!.GetIndicesAsync().ConfigureAwait(false);
            bool found = indices.Exists(idx => idx.Id == _TestIndexId);
            AssertTrue(found, "test index should be in list");
        }

        private async Task TestCreateIndexWithLabelsAndTagsAsync()
        {
            string indexId = $"test-labeled-{Guid.NewGuid().ToString("N")[..8]}";
            List<string> labels = new List<string> { "test", "labeled" };
            Dictionary<string, string> tags = new Dictionary<string, string>
            {
                { "environment", "testing" },
                { "owner", "sdk-harness" }
            };
            ApiResponse<CreateIndexData> response = await _Client!.CreateIndexAsync(
                id: indexId,
                name: "Labeled Test Index",
                description: "An index with labels and tags",
                inMemory: true,
                storageMode: "MemoryOnly",
                labels: labels,
                tags: tags
            ).ConfigureAwait(false);
            AssertTrue(response.Success, "response.Success");
            AssertEquals(response.StatusCode, 201, "response.StatusCode");
            AssertNotNull(response.Data, "response.Data");
            AssertNotNull(response.Data!.Index, "data.Index");
            // Clean up
            await _Client!.DeleteIndexAsync(indexId).ConfigureAwait(false);
        }

        private async Task TestGetIndexWithLabelsAndTagsAsync()
        {
            string indexId = $"test-labeled-get-{Guid.NewGuid().ToString("N")[..8]}";
            List<string> labels = new List<string> { "retrieval", "test" };
            Dictionary<string, string> tags = new Dictionary<string, string>
            {
                { "purpose", "verification" },
                { "version", "1.0" }
            };
            await _Client!.CreateIndexAsync(
                id: indexId,
                name: "Get Labeled Index",
                inMemory: true,
                labels: labels,
                tags: tags
            ).ConfigureAwait(false);
            ApiResponse<IndexInfo> response = await _Client!.GetIndexAsync(indexId).ConfigureAwait(false);
            AssertTrue(response.Success, "response.Success");
            AssertNotNull(response.Data, "response.Data");
            AssertNotNull(response.Data!.Labels, "data.Labels");
            AssertNotNull(response.Data.Tags, "data.Tags");
            AssertEquals(response.Data.Labels!.Count, 2, "labels count");
            AssertEquals(response.Data.Tags!.Count, 2, "tags count");
            // Clean up
            await _Client!.DeleteIndexAsync(indexId).ConfigureAwait(false);
        }

        // ==================== Document Management Tests ====================

        private async Task TestListDocumentsEmptyAsync()
        {
            ApiResponse<DocumentsListData> response = await _Client!.ListDocumentsAsync(_TestIndexId).ConfigureAwait(false);
            AssertTrue(response.Success, "response.Success");
            AssertEquals(response.StatusCode, 200, "response.StatusCode");
            AssertNotNull(response.Data, "response.Data");
            AssertNotNull(response.Data!.Documents, "data.Documents");
            AssertEquals(response.Data.Count, 0, "data.Count");
        }

        private async Task TestAddDocumentAsync()
        {
            ApiResponse<AddDocumentData> response = await _Client!.AddDocumentAsync(
                _TestIndexId,
                "The quick brown fox jumps over the lazy dog."
            ).ConfigureAwait(false);
            AssertTrue(response.Success, "response.Success");
            AssertEquals(response.StatusCode, 201, "response.StatusCode");
            AssertNotNull(response.Data, "response.Data");
            AssertNotNull(response.Data!.DocumentId, "data.DocumentId");
            AssertNotNull(response.Data.Message, "data.Message");
            _TestDocuments.Add(response.Data.DocumentId!);
        }

        private async Task TestAddDocumentWithIdAsync()
        {
            string docId = Guid.NewGuid().ToString();
            ApiResponse<AddDocumentData> response = await _Client!.AddDocumentAsync(
                _TestIndexId,
                "C# is a versatile programming language used for enterprise applications and game development.",
                docId
            ).ConfigureAwait(false);
            AssertTrue(response.Success, "response.Success");
            AssertEquals(response.StatusCode, 201, "response.StatusCode");
            AssertEquals(response.Data!.DocumentId, docId, "data.DocumentId");
            _TestDocuments.Add(docId);
        }

        private async Task TestAddMultipleDocumentsAsync()
        {
            string[] docs = new[]
            {
                "Machine learning algorithms can identify patterns in large datasets.",
                "Natural language processing enables computers to understand human language.",
                "Deep learning neural networks have revolutionized image recognition.",
                "Cloud computing provides scalable infrastructure for modern applications."
            };
            foreach (string content in docs)
            {
                ApiResponse<AddDocumentData> response = await _Client!.AddDocumentAsync(_TestIndexId, content).ConfigureAwait(false);
                AssertTrue(response.Success, "response.Success");
                _TestDocuments.Add(response.Data!.DocumentId!);
            }
        }

        private async Task TestListDocumentsAfterAddAsync()
        {
            ApiResponse<DocumentsListData> response = await _Client!.ListDocumentsAsync(_TestIndexId).ConfigureAwait(false);
            AssertTrue(response.Success, "response.Success");
            AssertEquals(response.Data!.Count, _TestDocuments.Count, "data.Count");
            AssertEquals(response.Data.Documents.Count, _TestDocuments.Count, "documents length");
            foreach (DocumentInfo doc in response.Data.Documents)
            {
                AssertNotNull(doc.Id, "document.Id");
            }
        }

        private async Task TestGetDocumentAsync()
        {
            string docId = _TestDocuments[0];
            ApiResponse<DocumentInfo> response = await _Client!.GetDocumentAsync(_TestIndexId, docId).ConfigureAwait(false);
            AssertTrue(response.Success, "response.Success");
            AssertEquals(response.StatusCode, 200, "response.StatusCode");
            AssertNotNull(response.Data, "response.Data");
            AssertEquals(response.Data!.Id, docId, "data.Id");
        }

        private async Task TestGetDocumentNotFoundAsync()
        {
            string fakeId = Guid.NewGuid().ToString();
            try
            {
                await _Client!.GetDocumentAsync(_TestIndexId, fakeId).ConfigureAwait(false);
                Assert(false, "Should have thrown VerbexException for not found");
            }
            catch (VerbexException ex)
            {
                AssertEquals(ex.StatusCode, 404, "error.StatusCode");
            }
        }

        private async Task TestAddDocumentWithLabelsAndTagsAsync()
        {
            List<string> labels = new List<string> { "important", "reviewed" };
            Dictionary<string, string> tags = new Dictionary<string, string>
            {
                { "author", "test-harness" },
                { "category", "technical" }
            };
            ApiResponse<AddDocumentData> response = await _Client!.AddDocumentAsync(
                _TestIndexId,
                "This document has labels and tags for testing metadata support.",
                null,
                labels,
                tags
            ).ConfigureAwait(false);
            AssertTrue(response.Success, "response.Success");
            AssertEquals(response.StatusCode, 201, "response.StatusCode");
            AssertNotNull(response.Data, "response.Data");
            AssertNotNull(response.Data!.DocumentId, "data.DocumentId");
            _TestDocuments.Add(response.Data.DocumentId!);
        }

        private async Task TestGetDocumentWithLabelsAndTagsAsync()
        {
            string docId = Guid.NewGuid().ToString();
            List<string> labels = new List<string> { "verification", "metadata" };
            Dictionary<string, string> tags = new Dictionary<string, string>
            {
                { "source", "sdk-test" },
                { "priority", "high" }
            };
            await _Client!.AddDocumentAsync(
                _TestIndexId,
                "Document for verifying labels and tags retrieval.",
                docId,
                labels,
                tags
            ).ConfigureAwait(false);
            ApiResponse<DocumentInfo> response = await _Client!.GetDocumentAsync(_TestIndexId, docId).ConfigureAwait(false);
            AssertTrue(response.Success, "response.Success");
            AssertNotNull(response.Data, "response.Data");
            AssertNotNull(response.Data!.Labels, "data.Labels");
            AssertNotNull(response.Data.Tags, "data.Tags");
            AssertEquals(response.Data.Labels!.Count, 2, "labels count");
            AssertEquals(response.Data.Tags!.Count, 2, "tags count");
            _TestDocuments.Add(docId);
        }

        // ==================== Search Tests ====================

        private async Task TestSearchBasicAsync()
        {
            ApiResponse<SearchData> response = await _Client!.SearchAsync(_TestIndexId, "fox").ConfigureAwait(false);
            AssertTrue(response.Success, "response.Success");
            AssertEquals(response.StatusCode, 200, "response.StatusCode");
            AssertNotNull(response.Data, "response.Data");
            AssertEquals(response.Data!.Query, "fox", "data.Query");
            AssertNotNull(response.Data.Results, "data.Results");
        }

        private async Task TestSearchWithResultsAsync()
        {
            ApiResponse<SearchData> response = await _Client!.SearchAsync(_TestIndexId, "learning").ConfigureAwait(false);
            AssertTrue(response.Success, "response.Success");
            List<SearchResult> results = response.Data!.Results;
            AssertGreaterThan(results.Count, 0, "results count");
            foreach (SearchResult result in results)
            {
                AssertNotNull(result.DocumentId, "result.DocumentId");
            }
        }

        private async Task TestSearchMultipleTermsAsync()
        {
            ApiResponse<SearchData> response = await _Client!.SearchAsync(_TestIndexId, "machine learning").ConfigureAwait(false);
            AssertTrue(response.Success, "response.Success");
            AssertNotNull(response.Data!.Results, "data.Results");
        }

        private async Task TestSearchMaxResultsAsync()
        {
            ApiResponse<SearchData> response = await _Client!.SearchAsync(_TestIndexId, "the", 2).ConfigureAwait(false);
            AssertTrue(response.Success, "response.Success");
            AssertEquals(response.Data!.MaxResults, 2, "data.MaxResults");
        }

        private async Task TestSearchNoResultsAsync()
        {
            ApiResponse<SearchData> response = await _Client!.SearchAsync(_TestIndexId, "xyznonexistent12345").ConfigureAwait(false);
            AssertTrue(response.Success, "response.Success");
            AssertEquals(response.Data!.Results.Count, 0, "results should be empty");
        }

        private async Task TestSearchDocumentsHelperAsync()
        {
            SearchData? searchResponse = await _Client!.SearchDocumentsAsync(_TestIndexId, "programming").ConfigureAwait(false);
            AssertNotNull(searchResponse, "searchResponse");
            AssertEquals(searchResponse!.Query, "programming", "Query");
            AssertNotNull(searchResponse.Results, "Results");
        }

        private async Task TestSearchWithLabelFilterAsync()
        {
            // First add a document with labels
            string docId = Guid.NewGuid().ToString();
            List<string> labels = new List<string> { "searchtest", "filterable" };
            await _Client!.AddDocumentAsync(
                _TestIndexId,
                "This document contains searchable content with labels for filter testing.",
                docId,
                labels,
                null
            ).ConfigureAwait(false);
            _TestDocuments.Add(docId);

            // Search with matching label filter
            ApiResponse<SearchData> response = await _Client!.SearchAsync(
                _TestIndexId,
                "searchable",
                100,
                new List<string> { "searchtest" },
                null
            ).ConfigureAwait(false);
            AssertTrue(response.Success, "response.Success");
            AssertGreaterThan(response.Data!.Results.Count, 0, "should find documents with matching label");

            // Search with non-matching label filter
            ApiResponse<SearchData> noMatchResponse = await _Client!.SearchAsync(
                _TestIndexId,
                "searchable",
                100,
                new List<string> { "nonexistentlabel99" },
                null
            ).ConfigureAwait(false);
            AssertTrue(noMatchResponse.Success, "noMatchResponse.Success");
            AssertEquals(noMatchResponse.Data!.Results.Count, 0, "should find no documents with non-matching label");
        }

        private async Task TestSearchWithTagFilterAsync()
        {
            // First add a document with tags
            string docId = Guid.NewGuid().ToString();
            Dictionary<string, string> tags = new Dictionary<string, string>
            {
                { "searchcategory", "testfilter" },
                { "searchpriority", "high" }
            };
            await _Client!.AddDocumentAsync(
                _TestIndexId,
                "This document contains taggable content for tag filter testing.",
                docId,
                null,
                tags
            ).ConfigureAwait(false);
            _TestDocuments.Add(docId);

            // Search with matching tag filter
            ApiResponse<SearchData> response = await _Client!.SearchAsync(
                _TestIndexId,
                "taggable",
                100,
                null,
                new Dictionary<string, object> { { "searchcategory", "testfilter" } }
            ).ConfigureAwait(false);
            AssertTrue(response.Success, "response.Success");
            AssertGreaterThan(response.Data!.Results.Count, 0, "should find documents with matching tag");

            // Search with non-matching tag filter
            ApiResponse<SearchData> noMatchResponse = await _Client!.SearchAsync(
                _TestIndexId,
                "taggable",
                100,
                null,
                new Dictionary<string, object> { { "searchcategory", "wrongvalue" } }
            ).ConfigureAwait(false);
            AssertTrue(noMatchResponse.Success, "noMatchResponse.Success");
            AssertEquals(noMatchResponse.Data!.Results.Count, 0, "should find no documents with non-matching tag");
        }

        private async Task TestSearchWithLabelsAndTagsAsync()
        {
            // First add a document with both labels and tags
            string docId = Guid.NewGuid().ToString();
            List<string> labels = new List<string> { "combined", "fulltest" };
            Dictionary<string, string> tags = new Dictionary<string, string>
            {
                { "combinedcategory", "both" }
            };
            await _Client!.AddDocumentAsync(
                _TestIndexId,
                "This document has combined labels and tags for comprehensive filter testing.",
                docId,
                labels,
                tags
            ).ConfigureAwait(false);
            _TestDocuments.Add(docId);

            // Search with both label and tag filters
            ApiResponse<SearchData> response = await _Client!.SearchAsync(
                _TestIndexId,
                "comprehensive",
                100,
                new List<string> { "combined" },
                new Dictionary<string, object> { { "combinedcategory", "both" } }
            ).ConfigureAwait(false);
            AssertTrue(response.Success, "response.Success");
            AssertGreaterThan(response.Data!.Results.Count, 0, "should find documents matching both label and tag");
        }

        // ==================== Document Deletion Tests ====================

        private async Task TestDeleteDocumentAsync()
        {
            if (_TestDocuments.Count == 0)
            {
                throw new Exception("No test documents to delete");
            }
            string docId = _TestDocuments[^1];
            _TestDocuments.RemoveAt(_TestDocuments.Count - 1);
            ApiResponse<DeleteDocumentData> response = await _Client!.DeleteDocumentAsync(_TestIndexId, docId).ConfigureAwait(false);
            AssertTrue(response.Success, "response.Success");
            AssertEquals(response.StatusCode, 200, "response.StatusCode");
            AssertNotNull(response.Data, "response.Data");
            AssertEquals(response.Data!.DocumentId, docId, "data.DocumentId");
            AssertNotNull(response.Data.Message, "data.Message");
        }

        private async Task TestDeleteDocumentNotFoundAsync()
        {
            string fakeId = Guid.NewGuid().ToString();
            try
            {
                await _Client!.DeleteDocumentAsync(_TestIndexId, fakeId).ConfigureAwait(false);
                Assert(false, "Should have thrown VerbexException for not found");
            }
            catch (VerbexException ex)
            {
                AssertEquals(ex.StatusCode, 404, "error.StatusCode");
            }
        }

        private async Task TestVerifyDocumentDeletedAsync()
        {
            if (_TestDocuments.Count == 0)
            {
                return;
            }
            string docId = _TestDocuments[^1];
            _TestDocuments.RemoveAt(_TestDocuments.Count - 1);
            await _Client!.DeleteDocumentAsync(_TestIndexId, docId).ConfigureAwait(false);
            try
            {
                await _Client!.GetDocumentAsync(_TestIndexId, docId).ConfigureAwait(false);
                Assert(false, "Should have thrown VerbexException for deleted document");
            }
            catch (VerbexException ex)
            {
                AssertEquals(ex.StatusCode, 404, "error.StatusCode");
            }
        }

        // ==================== Index Deletion Tests ====================

        private async Task TestDeleteIndexAsync()
        {
            ApiResponse<DeleteIndexData> response = await _Client!.DeleteIndexAsync(_TestIndexId).ConfigureAwait(false);
            AssertTrue(response.Success, "response.Success");
            AssertEquals(response.StatusCode, 200, "response.StatusCode");
            AssertNotNull(response.Data, "response.Data");
            AssertEquals(response.Data!.IndexId, _TestIndexId, "data.IndexId");
            AssertNotNull(response.Data.Message, "data.Message");
        }

        private async Task TestDeleteIndexNotFoundAsync()
        {
            try
            {
                await _Client!.DeleteIndexAsync("non-existent-index-67890").ConfigureAwait(false);
                Assert(false, "Should have thrown VerbexException for not found");
            }
            catch (VerbexException ex)
            {
                AssertEquals(ex.StatusCode, 404, "error.StatusCode");
            }
        }

        private async Task TestVerifyIndexDeletedAsync()
        {
            try
            {
                await _Client!.GetIndexAsync(_TestIndexId).ConfigureAwait(false);
                Assert(false, "Should have thrown VerbexException for deleted index");
            }
            catch (VerbexException ex)
            {
                AssertEquals(ex.StatusCode, 404, "error.StatusCode");
            }
        }

        public async Task<int> RunAsync()
        {
            Stopwatch totalStopwatch = Stopwatch.StartNew();

            PrintHeader("Verbex SDK Test Harness - C#");
            Console.WriteLine($"  Endpoint: {_Endpoint}");
            Console.WriteLine($"  Test Index: {_TestIndexId}");
            Console.WriteLine($"  Started: {DateTime.UtcNow:O}");

            _Client = new VerbexClient(_Endpoint, _AccessKey);

            try
            {
                // Health Tests
                PrintSubheader("Health Checks");
                await RunTestAsync("Root health check", TestRootHealthCheckAsync).ConfigureAwait(false);
                await RunTestAsync("Health endpoint", TestHealthEndpointAsync).ConfigureAwait(false);

                // Authentication Tests
                PrintSubheader("Authentication");
                await RunTestAsync("Login with valid credentials", TestLoginSuccessAsync).ConfigureAwait(false);
                await RunTestAsync("Login with invalid credentials", TestLoginInvalidCredentialsAsync).ConfigureAwait(false);
                await RunTestAsync("Validate token", TestValidateTokenAsync).ConfigureAwait(false);
                await RunTestAsync("Validate invalid token", TestValidateInvalidTokenAsync).ConfigureAwait(false);

                // Index Management Tests
                PrintSubheader("Index Management");
                await RunTestAsync("List indices (initial)", TestListIndicesInitialAsync).ConfigureAwait(false);
                await RunTestAsync("Create index", TestCreateIndexAsync).ConfigureAwait(false);
                await RunTestAsync("Create duplicate index fails", TestCreateDuplicateIndexAsync).ConfigureAwait(false);
                await RunTestAsync("Get index", TestGetIndexAsync).ConfigureAwait(false);
                await RunTestAsync("Get index not found", TestGetIndexNotFoundAsync).ConfigureAwait(false);
                await RunTestAsync("List indices (after create)", TestListIndicesAfterCreateAsync).ConfigureAwait(false);
                await RunTestAsync("Create index with labels and tags", TestCreateIndexWithLabelsAndTagsAsync).ConfigureAwait(false);
                await RunTestAsync("Get index with labels and tags", TestGetIndexWithLabelsAndTagsAsync).ConfigureAwait(false);

                // Document Management Tests
                PrintSubheader("Document Management");
                await RunTestAsync("List documents (empty)", TestListDocumentsEmptyAsync).ConfigureAwait(false);
                await RunTestAsync("Add document", TestAddDocumentAsync).ConfigureAwait(false);
                await RunTestAsync("Add document with ID", TestAddDocumentWithIdAsync).ConfigureAwait(false);
                await RunTestAsync("Add multiple documents", TestAddMultipleDocumentsAsync).ConfigureAwait(false);
                await RunTestAsync("List documents (after add)", TestListDocumentsAfterAddAsync).ConfigureAwait(false);
                await RunTestAsync("Get document", TestGetDocumentAsync).ConfigureAwait(false);
                await RunTestAsync("Get document not found", TestGetDocumentNotFoundAsync).ConfigureAwait(false);
                await RunTestAsync("Add document with labels and tags", TestAddDocumentWithLabelsAndTagsAsync).ConfigureAwait(false);
                await RunTestAsync("Get document with labels and tags", TestGetDocumentWithLabelsAndTagsAsync).ConfigureAwait(false);

                // Search Tests
                PrintSubheader("Search");
                await RunTestAsync("Basic search", TestSearchBasicAsync).ConfigureAwait(false);
                await RunTestAsync("Search with results", TestSearchWithResultsAsync).ConfigureAwait(false);
                await RunTestAsync("Search multiple terms", TestSearchMultipleTermsAsync).ConfigureAwait(false);
                await RunTestAsync("Search with max results", TestSearchMaxResultsAsync).ConfigureAwait(false);
                await RunTestAsync("Search with no results", TestSearchNoResultsAsync).ConfigureAwait(false);
                await RunTestAsync("Search documents helper", TestSearchDocumentsHelperAsync).ConfigureAwait(false);
                await RunTestAsync("Search with label filter", TestSearchWithLabelFilterAsync).ConfigureAwait(false);
                await RunTestAsync("Search with tag filter", TestSearchWithTagFilterAsync).ConfigureAwait(false);
                await RunTestAsync("Search with labels and tags", TestSearchWithLabelsAndTagsAsync).ConfigureAwait(false);

                // Cleanup Tests
                PrintSubheader("Cleanup");
                await RunTestAsync("Delete document", TestDeleteDocumentAsync).ConfigureAwait(false);
                await RunTestAsync("Delete document not found", TestDeleteDocumentNotFoundAsync).ConfigureAwait(false);
                await RunTestAsync("Verify document deleted", TestVerifyDocumentDeletedAsync).ConfigureAwait(false);
                await RunTestAsync("Delete index", TestDeleteIndexAsync).ConfigureAwait(false);
                await RunTestAsync("Delete index not found", TestDeleteIndexNotFoundAsync).ConfigureAwait(false);
                await RunTestAsync("Verify index deleted", TestVerifyIndexDeletedAsync).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n  FATAL ERROR: {ex.GetType().Name}: {ex.Message}");
                _Failed++;
            }
            finally
            {
                _Client.Dispose();
            }

            // Summary
            totalStopwatch.Stop();
            double totalSeconds = totalStopwatch.Elapsed.TotalSeconds;
            PrintHeader("Test Summary");
            Console.WriteLine($"  Total Tests: {_Passed + _Failed}");
            Console.WriteLine($"  Passed: {_Passed}");
            Console.WriteLine($"  Failed: {_Failed}");
            Console.WriteLine($"  Duration: {totalSeconds:F2}s");
            Console.WriteLine($"  Result: {(_Failed == 0 ? "SUCCESS" : "FAILURE")}");
            Console.WriteLine();

            return _Failed == 0 ? 0 : 1;
        }
    }

    /// <summary>
    /// Program entry point.
    /// </summary>
    internal class Program
    {
        /// <summary>
        /// Main entry point.
        /// </summary>
        /// <param name="args">Command line arguments: endpoint, access_key.</param>
        /// <returns>Exit code (0 = success, 1 = failure).</returns>
        public static async Task<int> Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("Verbex SDK Test Harness - C#");
                Console.WriteLine();
                Console.WriteLine("Usage: dotnet run -- <endpoint> <access_key>");
                Console.WriteLine();
                Console.WriteLine("Arguments:");
                Console.WriteLine("  endpoint    The Verbex server endpoint (e.g., http://localhost:8080)");
                Console.WriteLine("  access_key  The bearer token for authentication");
                Console.WriteLine();
                Console.WriteLine("Example:");
                Console.WriteLine("  dotnet run -- http://localhost:8080 verbexadmin");
                return 1;
            }

            string endpoint = args[0];
            string accessKey = args[1];

            TestHarness harness = new TestHarness(endpoint, accessKey);
            return await harness.RunAsync().ConfigureAwait(false);
        }
    }
}
