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
        private string _TestIndexId;
        private readonly List<string> _TestDocuments;
        private readonly List<TestResult> _Results;
        private VerbexClient? _Client;
        private int _Passed;
        private int _Failed;

        public TestHarness(string endpoint, string accessKey)
        {
            _Endpoint = endpoint;
            _AccessKey = accessKey;
            _TestIndexId = string.Empty;
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
            HealthData health = await _Client!.RootHealthCheckAsync().ConfigureAwait(false);
            AssertEquals(health.Status, "Healthy", "health.Status");
            AssertNotNull(health.Version, "health.Version");
            AssertNotNull(health.Timestamp, "health.Timestamp");
        }

        private async Task TestHealthEndpointAsync()
        {
            HealthData health = await _Client!.HealthCheckAsync().ConfigureAwait(false);
            AssertEquals(health.Status, "Healthy", "health.Status");
            AssertNotNull(health.Version, "health.Version");
            AssertNotNull(health.Timestamp, "health.Timestamp");
        }

        // ==================== Authentication Tests ====================

        private async Task TestLoginWithCredentialsSuccessAsync()
        {
            // Test login with tenant ID, email, and password
            // Using "default" tenant with the seeded default user credentials
            LoginResult result = await _Client!.LoginAsync("default", "default@user.com", "password").ConfigureAwait(false);
            AssertTrue(result.Success, "result.Success");
            AssertEquals(result.AuthenticationResult, AuthenticationResultEnum.Success, "result.AuthenticationResult");
            AssertEquals(result.AuthorizationResult, AuthorizationResultEnum.Authorized, "result.AuthorizationResult");
            AssertNotNull(result.Token, "result.Token");
        }

        private async Task TestLoginWithCredentialsInvalidAsync()
        {
            // Test login with invalid credentials - should not throw, just return failure
            LoginResult result = await _Client!.LoginAsync("default", "invalid@example.com", "wrongpassword").ConfigureAwait(false);
            AssertTrue(!result.Success, "result.Success should be false");
            Assert(result.AuthenticationResult != AuthenticationResultEnum.Success, "AuthenticationResult should not be Success");
            AssertNotNull(result.ErrorMessage, "result.ErrorMessage");
        }

        private async Task TestLoginWithBearerTokenSuccessAsync()
        {
            // Test login with a valid bearer token
            LoginResult result = await _Client!.LoginAsync(_AccessKey).ConfigureAwait(false);
            AssertTrue(result.Success, "result.Success");
            AssertEquals(result.AuthenticationResult, AuthenticationResultEnum.Success, "result.AuthenticationResult");
            AssertEquals(result.AuthorizationResult, AuthorizationResultEnum.Authorized, "result.AuthorizationResult");
            AssertNotNull(result.Token, "result.Token");
            AssertEquals(result.Token, _AccessKey, "result.Token should match input");
        }

        private async Task TestLoginWithBearerTokenInvalidAsync()
        {
            // Test login with an invalid bearer token - should not throw, just return failure
            LoginResult result = await _Client!.LoginAsync("invalid-bearer-token-12345").ConfigureAwait(false);
            AssertTrue(!result.Success, "result.Success should be false");
            Assert(result.AuthenticationResult != AuthenticationResultEnum.Success, "AuthenticationResult should not be Success");
            AssertNotNull(result.ErrorMessage, "result.ErrorMessage");
        }

        private async Task TestValidateTokenAsync()
        {
            ValidationData validation = await _Client!.ValidateTokenAsync().ConfigureAwait(false);
            AssertTrue(validation.Valid, "validation.Valid");
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
            List<IndexInfo> indices = await _Client!.ListIndicesAsync().ConfigureAwait(false);
            AssertNotNull(indices, "indices");
        }

        private async Task TestCreateIndexAsync()
        {
            IndexInfo index = await _Client!.CreateIndexAsync(
                name: "Test Index",
                description: "A test index for SDK validation",
                inMemory: true,
                tenantId: "default"
            ).ConfigureAwait(false);
            AssertNotNull(index, "index");
            AssertNotNull(index.Identifier, "index.Identifier");
            AssertEquals(index.Name, "Test Index", "index.Name");
            _TestIndexId = index.Identifier;
        }

        private async Task TestCreateDuplicateNameIndexAsync()
        {
            // Creating an index with the same name should fail with 409 Conflict
            // The server enforces unique index names within a tenant
            try
            {
                await _Client!.CreateIndexAsync(
                    name: "Test Index",
                    description: "Duplicate name index",
                    inMemory: true,
                    tenantId: "default"
                ).ConfigureAwait(false);
                Assert(false, "Should have thrown VerbexException for duplicate name");
            }
            catch (VerbexException ex)
            {
                AssertEquals(ex.StatusCode, 409, "error.StatusCode");
            }
        }

        private async Task TestGetIndexAsync()
        {
            IndexInfo index = await _Client!.GetIndexAsync(_TestIndexId).ConfigureAwait(false);
            AssertNotNull(index, "index");
            AssertEquals(index.Identifier, _TestIndexId, "index.Identifier");
            AssertEquals(index.Name, "Test Index", "index.Name");
            AssertNotNull(index.CreatedUtc, "index.CreatedUtc");
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
            List<IndexInfo> indices = await _Client!.ListIndicesAsync().ConfigureAwait(false);
            bool found = indices.Exists(idx => idx.Identifier == _TestIndexId);
            AssertTrue(found, "test index should be in list");
        }

        private async Task TestCreateIndexWithLabelsAndTagsAsync()
        {
            List<string> labels = new List<string> { "test", "labeled" };
            Dictionary<string, string> tags = new Dictionary<string, string>
            {
                { "environment", "testing" },
                { "owner", "sdk-harness" }
            };
            IndexInfo index = await _Client!.CreateIndexAsync(
                name: "Labeled Test Index",
                description: "An index with labels and tags",
                inMemory: true,
                labels: labels,
                tags: tags,
                tenantId: "default"
            ).ConfigureAwait(false);
            AssertNotNull(index, "index");
            // Clean up
            await _Client!.DeleteIndexAsync(index.Identifier).ConfigureAwait(false);
        }

        private async Task TestGetIndexWithLabelsAndTagsAsync()
        {
            List<string> labels = new List<string> { "retrieval", "test" };
            Dictionary<string, string> tags = new Dictionary<string, string>
            {
                { "purpose", "verification" },
                { "version", "1.0" }
            };
            IndexInfo createdIndex = await _Client!.CreateIndexAsync(
                name: "Get Labeled Index",
                inMemory: true,
                labels: labels,
                tags: tags,
                tenantId: "default"
            ).ConfigureAwait(false);
            string indexId = createdIndex.Identifier;
            IndexInfo index = await _Client!.GetIndexAsync(indexId).ConfigureAwait(false);
            AssertNotNull(index, "index");
            AssertNotNull(index.Labels, "index.Labels");
            AssertNotNull(index.Tags, "index.Tags");
            AssertEquals(index.Labels!.Count, 2, "labels count");
            AssertEquals(index.Tags!.Count, 2, "tags count");
            // Clean up
            await _Client!.DeleteIndexAsync(indexId).ConfigureAwait(false);
        }

        // ==================== HEAD API Tests ====================

        private async Task TestIndexExistsAsync()
        {
            bool exists = await _Client!.IndexExistsAsync(_TestIndexId).ConfigureAwait(false);
            AssertTrue(exists, "index should exist");
        }

        private async Task TestIndexExistsNotFoundAsync()
        {
            bool exists = await _Client!.IndexExistsAsync("non-existent-index-99999").ConfigureAwait(false);
            AssertTrue(!exists, "index should not exist");
        }

        private async Task TestDocumentExistsAsync()
        {
            if (_TestDocuments.Count == 0)
            {
                throw new Exception("No test documents available");
            }
            string docId = _TestDocuments[0];
            bool exists = await _Client!.DocumentExistsAsync(_TestIndexId, docId).ConfigureAwait(false);
            AssertTrue(exists, "document should exist");
        }

        private async Task TestDocumentExistsNotFoundAsync()
        {
            string fakeId = Guid.NewGuid().ToString();
            bool exists = await _Client!.DocumentExistsAsync(_TestIndexId, fakeId).ConfigureAwait(false);
            AssertTrue(!exists, "document should not exist");
        }

        // ==================== Document Management Tests ====================

        private async Task TestListDocumentsEmptyAsync()
        {
            List<DocumentInfo> documents = await _Client!.ListDocumentsAsync(_TestIndexId).ConfigureAwait(false);
            AssertNotNull(documents, "documents");
            AssertEquals(documents.Count, 0, "documents.Count");
        }

        private async Task TestAddDocumentAsync()
        {
            AddDocumentData result = await _Client!.AddDocumentAsync(
                _TestIndexId,
                "The quick brown fox jumps over the lazy dog."
            ).ConfigureAwait(false);
            AssertNotNull(result, "result");
            AssertNotNull(result.DocumentId, "result.DocumentId");
            AssertNotNull(result.Message, "result.Message");
            _TestDocuments.Add(result.DocumentId!);
        }

        private async Task TestAddDocumentWithIdAsync()
        {
            string docId = Guid.NewGuid().ToString();
            AddDocumentData result = await _Client!.AddDocumentAsync(
                _TestIndexId,
                "C# is a versatile programming language used for enterprise applications and game development.",
                docId
            ).ConfigureAwait(false);
            AssertNotNull(result, "result");
            AssertEquals(result.DocumentId, docId, "result.DocumentId");
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
                AddDocumentData result = await _Client!.AddDocumentAsync(_TestIndexId, content).ConfigureAwait(false);
                AssertNotNull(result, "result");
                _TestDocuments.Add(result.DocumentId!);
            }
        }

        private async Task TestListDocumentsAfterAddAsync()
        {
            List<DocumentInfo> documents = await _Client!.ListDocumentsAsync(_TestIndexId).ConfigureAwait(false);
            AssertNotNull(documents, "documents");
            AssertEquals(documents.Count, _TestDocuments.Count, "documents.Count");
            foreach (DocumentInfo doc in documents)
            {
                AssertNotNull(doc.Id, "document.Id");
            }
        }

        private async Task TestGetDocumentAsync()
        {
            string docId = _TestDocuments[0];
            DocumentInfo document = await _Client!.GetDocumentAsync(_TestIndexId, docId).ConfigureAwait(false);
            AssertNotNull(document, "document");
            AssertEquals(document.Id, docId, "document.Id");
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
            AddDocumentData result = await _Client!.AddDocumentAsync(
                _TestIndexId,
                "This document has labels and tags for testing metadata support.",
                null,
                labels,
                tags
            ).ConfigureAwait(false);
            AssertNotNull(result, "result");
            AssertNotNull(result.DocumentId, "result.DocumentId");
            _TestDocuments.Add(result.DocumentId!);
        }

        private async Task TestGetDocumentWithLabelsAndTagsAsync()
        {
            List<string> labels = new List<string> { "verification", "metadata" };
            Dictionary<string, string> tags = new Dictionary<string, string>
            {
                { "source", "sdk-test" },
                { "priority", "high" }
            };
            AddDocumentData addResult = await _Client!.AddDocumentAsync(
                _TestIndexId,
                "Document for verifying labels and tags retrieval.",
                null,
                labels,
                tags
            ).ConfigureAwait(false);
            AssertNotNull(addResult, "addResult");
            string docId = addResult.DocumentId!;
            DocumentInfo document = await _Client!.GetDocumentAsync(_TestIndexId, docId).ConfigureAwait(false);
            AssertNotNull(document, "document");
            AssertNotNull(document.Labels, "document.Labels");
            AssertNotNull(document.Tags, "document.Tags");
            AssertEquals(document.Labels!.Count, 2, "labels count");
            AssertEquals(document.Tags!.Count, 2, "tags count");
            _TestDocuments.Add(docId);
        }

        // ==================== Search Tests ====================

        private async Task TestSearchBasicAsync()
        {
            SearchData searchResult = await _Client!.SearchAsync(_TestIndexId, "fox").ConfigureAwait(false);
            AssertNotNull(searchResult, "searchResult");
            AssertEquals(searchResult.Query, "fox", "searchResult.Query");
            AssertNotNull(searchResult.Results, "searchResult.Results");
        }

        private async Task TestSearchWithResultsAsync()
        {
            SearchData searchResult = await _Client!.SearchAsync(_TestIndexId, "learning").ConfigureAwait(false);
            AssertNotNull(searchResult, "searchResult");
            List<SearchResult> results = searchResult.Results;
            AssertGreaterThan(results.Count, 0, "results count");
            foreach (SearchResult result in results)
            {
                AssertNotNull(result.DocumentId, "result.DocumentId");
            }
        }

        private async Task TestSearchMultipleTermsAsync()
        {
            SearchData searchResult = await _Client!.SearchAsync(_TestIndexId, "machine learning").ConfigureAwait(false);
            AssertNotNull(searchResult, "searchResult");
            AssertNotNull(searchResult.Results, "searchResult.Results");
        }

        private async Task TestSearchMaxResultsAsync()
        {
            SearchData searchResult = await _Client!.SearchAsync(_TestIndexId, "the", 2).ConfigureAwait(false);
            AssertNotNull(searchResult, "searchResult");
            AssertEquals(searchResult.MaxResults, 2, "searchResult.MaxResults");
        }

        private async Task TestSearchNoResultsAsync()
        {
            SearchData searchResult = await _Client!.SearchAsync(_TestIndexId, "xyznonexistent12345").ConfigureAwait(false);
            AssertNotNull(searchResult, "searchResult");
            AssertEquals(searchResult.Results.Count, 0, "results should be empty");
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
            SearchData searchResult = await _Client!.SearchAsync(
                _TestIndexId,
                "searchable",
                100,
                new List<string> { "searchtest" },
                null
            ).ConfigureAwait(false);
            AssertNotNull(searchResult, "searchResult");
            AssertGreaterThan(searchResult.Results.Count, 0, "should find documents with matching label");

            // Search with non-matching label filter
            SearchData noMatchResult = await _Client!.SearchAsync(
                _TestIndexId,
                "searchable",
                100,
                new List<string> { "nonexistentlabel99" },
                null
            ).ConfigureAwait(false);
            AssertNotNull(noMatchResult, "noMatchResult");
            AssertEquals(noMatchResult.Results.Count, 0, "should find no documents with non-matching label");
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
            SearchData searchResult = await _Client!.SearchAsync(
                _TestIndexId,
                "taggable",
                100,
                null,
                new Dictionary<string, string> { { "searchcategory", "testfilter" } }
            ).ConfigureAwait(false);
            AssertNotNull(searchResult, "searchResult");
            AssertGreaterThan(searchResult.Results.Count, 0, "should find documents with matching tag");

            // Search with non-matching tag filter
            SearchData noMatchResult = await _Client!.SearchAsync(
                _TestIndexId,
                "taggable",
                100,
                null,
                new Dictionary<string, string> { { "searchcategory", "wrongvalue" } }
            ).ConfigureAwait(false);
            AssertNotNull(noMatchResult, "noMatchResult");
            AssertEquals(noMatchResult.Results.Count, 0, "should find no documents with non-matching tag");
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
            SearchData searchResult = await _Client!.SearchAsync(
                _TestIndexId,
                "comprehensive",
                100,
                new List<string> { "combined" },
                new Dictionary<string, string> { { "combinedcategory", "both" } }
            ).ConfigureAwait(false);
            AssertNotNull(searchResult, "searchResult");
            AssertGreaterThan(searchResult.Results.Count, 0, "should find documents matching both label and tag");
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
            await _Client!.DeleteDocumentAsync(_TestIndexId, docId).ConfigureAwait(false);
            // If we get here without exception, the delete succeeded
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
            await _Client!.DeleteIndexAsync(_TestIndexId).ConfigureAwait(false);
            // If we get here without exception, the delete succeeded
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
                await RunTestAsync("Login with credentials (success)", TestLoginWithCredentialsSuccessAsync).ConfigureAwait(false);
                await RunTestAsync("Login with credentials (invalid)", TestLoginWithCredentialsInvalidAsync).ConfigureAwait(false);
                await RunTestAsync("Login with bearer token (success)", TestLoginWithBearerTokenSuccessAsync).ConfigureAwait(false);
                await RunTestAsync("Login with bearer token (invalid)", TestLoginWithBearerTokenInvalidAsync).ConfigureAwait(false);
                await RunTestAsync("Validate token", TestValidateTokenAsync).ConfigureAwait(false);
                await RunTestAsync("Validate invalid token", TestValidateInvalidTokenAsync).ConfigureAwait(false);

                // Index Management Tests
                PrintSubheader("Index Management");
                await RunTestAsync("List indices (initial)", TestListIndicesInitialAsync).ConfigureAwait(false);
                await RunTestAsync("Create index", TestCreateIndexAsync).ConfigureAwait(false);
                await RunTestAsync("Create duplicate name index fails", TestCreateDuplicateNameIndexAsync).ConfigureAwait(false);
                await RunTestAsync("Get index", TestGetIndexAsync).ConfigureAwait(false);
                await RunTestAsync("Get index not found", TestGetIndexNotFoundAsync).ConfigureAwait(false);
                await RunTestAsync("List indices (after create)", TestListIndicesAfterCreateAsync).ConfigureAwait(false);
                await RunTestAsync("Create index with labels and tags", TestCreateIndexWithLabelsAndTagsAsync).ConfigureAwait(false);
                await RunTestAsync("Get index with labels and tags", TestGetIndexWithLabelsAndTagsAsync).ConfigureAwait(false);
                await RunTestAsync("Index exists (HEAD)", TestIndexExistsAsync).ConfigureAwait(false);
                await RunTestAsync("Index exists not found (HEAD)", TestIndexExistsNotFoundAsync).ConfigureAwait(false);

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
                await RunTestAsync("Document exists (HEAD)", TestDocumentExistsAsync).ConfigureAwait(false);
                await RunTestAsync("Document exists not found (HEAD)", TestDocumentExistsNotFoundAsync).ConfigureAwait(false);

                // Search Tests
                PrintSubheader("Search");
                await RunTestAsync("Basic search", TestSearchBasicAsync).ConfigureAwait(false);
                await RunTestAsync("Search with results", TestSearchWithResultsAsync).ConfigureAwait(false);
                await RunTestAsync("Search multiple terms", TestSearchMultipleTermsAsync).ConfigureAwait(false);
                await RunTestAsync("Search with max results", TestSearchMaxResultsAsync).ConfigureAwait(false);
                await RunTestAsync("Search with no results", TestSearchNoResultsAsync).ConfigureAwait(false);
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

            // Failed tests detail
            List<TestResult> failedTests = _Results.FindAll(r => !r.Passed);
            if (failedTests.Count > 0)
            {
                PrintHeader("Failed Tests");
                for (int i = 0; i < failedTests.Count; i++)
                {
                    TestResult failed = failedTests[i];
                    Console.WriteLine($"  {i + 1}. {failed.Name}");
                    Console.WriteLine($"     Error: {failed.Message}");
                    Console.WriteLine($"     Duration: {failed.DurationMs:F2}ms");
                    Console.WriteLine();
                }
            }

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
