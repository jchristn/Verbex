namespace Test
{
    using SyslogLogging;
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Verbex.Database;
    using Verbex.Server.Classes;
    using Verbex.Server.Services;

    /// <summary>
    /// Tests for request history summary bucketing across supported database drivers.
    /// </summary>
    public static class RequestHistoryServiceTests
    {
        private static string _TestDbPath = string.Empty;

        /// <summary>
        /// Runs all request history tests.
        /// </summary>
        /// <param name="runner">Test runner.</param>
        /// <returns>Task.</returns>
        public static async Task RunAllAsync(TestRunner runner)
        {
            if (TestContext.DatabaseSettings?.Type == DatabaseTypeEnum.Sqlite &&
                !TestContext.DatabaseSettings.InMemory)
            {
                _TestDbPath = Path.Combine(Path.GetTempPath(), $"verbex_request_history_{Guid.NewGuid():N}.db");
            }

            try
            {
                await runner.RunTestAsync("Request History Last Hour Summary Test", TestLastHourSummaryAsync).ConfigureAwait(false);
                await runner.RunTestAsync("Request History Last Day Summary Test", TestLastDaySummaryAsync).ConfigureAwait(false);
                await runner.RunTestAsync("Request History Last Week Summary Test", TestLastWeekSummaryAsync).ConfigureAwait(false);
                await runner.RunTestAsync("Request History Last Month Summary Test", TestLastMonthSummaryAsync).ConfigureAwait(false);
                await runner.RunTestAsync("Request History Success Filter Test", TestSuccessFilterAsync).ConfigureAwait(false);
                await runner.RunTestAsync("Request History Bulk Delete Route Contract Test", TestBulkDeleteRouteContractAsync).ConfigureAwait(false);
                await runner.RunTestAsync("Request History Bulk Delete Asset Contract Test", TestBulkDeleteAssetContractAsync).ConfigureAwait(false);
            }
            finally
            {
                CleanupTestDatabase();
            }
        }

        private static async Task TestLastHourSummaryAsync()
        {
            await AssertSummaryWindowAsync(
                fromUtc: new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc),
                bucketMinutes: 1,
                expectedBucketCount: 60).ConfigureAwait(false);
        }

        private static async Task TestLastDaySummaryAsync()
        {
            await AssertSummaryWindowAsync(
                fromUtc: new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                bucketMinutes: 15,
                expectedBucketCount: 96).ConfigureAwait(false);
        }

        private static async Task TestLastWeekSummaryAsync()
        {
            await AssertSummaryWindowAsync(
                fromUtc: new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc),
                bucketMinutes: 120,
                expectedBucketCount: 84).ConfigureAwait(false);
        }

        private static async Task TestLastMonthSummaryAsync()
        {
            await AssertSummaryWindowAsync(
                fromUtc: new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                bucketMinutes: 720,
                expectedBucketCount: 60).ConfigureAwait(false);
        }

        private static async Task TestSuccessFilterAsync()
        {
            (DatabaseDriverBase driver, RequestHistoryService service) = await CreateTestServiceAsync().ConfigureAwait(false);
            try
            {
                string tenantId = $"reqhist_{Guid.NewGuid():N}";
                DateTime fromUtc = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
                DateTime toUtc = fromUtc.AddHours(1);

                await RecordAsync(service, tenantId, fromUtc.AddMinutes(10), true, 20).ConfigureAwait(false);
                await RecordAsync(service, tenantId, fromUtc.AddMinutes(20), false, 40).ConfigureAwait(false);

                RequestHistoryQuery query = new RequestHistoryQuery
                {
                    TenantId = tenantId,
                    Success = false,
                    FromUtc = fromUtc,
                    ToUtc = toUtc,
                    BucketMinutes = 60
                };

                var (entries, totalCount) = await service.SearchAsync(query).ConfigureAwait(false);
                RequestHistorySummary summary = await service.GetSummaryAsync(query).ConfigureAwait(false);

                TestAssert.AreEqual(1L, totalCount);
                TestAssert.CollectionCount(entries, 1);
                TestAssert.AreEqual(false, entries[0].Success);
                TestAssert.AreEqual(1, summary.TotalCount);
                TestAssert.AreEqual(0, summary.SuccessCount);
                TestAssert.AreEqual(1, summary.FailureCount);
                TestAssert.AreEqual(40d, summary.AverageDurationMs);
            }
            finally
            {
                service.Dispose();
                driver.Dispose();
            }
        }

        private static Task TestBulkDeleteRouteContractAsync()
        {
            string root = GetRepositoryRoot();
            string source = File.ReadAllText(Path.Combine(root, "src", "Verbex.Server", "API", "REST", "RestServiceHandler.cs"));
            string deletedBulkRoute = "/v1.0/requesthistory" + "/bulk";

            TestAssert.IsTrue(
                source.Contains("HttpMethod.POST, \"/v1.0/requesthistory/delete\"", StringComparison.Ordinal),
                "request history bulk delete route should use POST /v1.0/requesthistory/delete");
            TestAssert.IsFalse(
                source.Contains($"HttpMethod.DELETE, \"{deletedBulkRoute}\"", StringComparison.Ordinal),
                "request history bulk delete route should not expose the previous DELETE bulk shape");
            TestAssert.IsTrue(
                source.Contains("ParseRequestHistoryBulkDeleteRequest(body)", StringComparison.Ordinal),
                "request history bulk delete should parse filters from request body");
            TestAssert.IsTrue(
                source.Contains("CreateRequestHistoryBulkDeleteRequestSchema()", StringComparison.Ordinal),
                "request history bulk delete should expose an OpenAPI request schema");

            return Task.CompletedTask;
        }

        private static Task TestBulkDeleteAssetContractAsync()
        {
            string root = GetRepositoryRoot();
            string dashboardApiPath = Path.Combine(root, "dashboard", "src", "utils", "api.js");
            string restApiPath = Path.Combine(root, "REST_API.md");
            string serverRestApiPath = Path.Combine(root, "src", "Verbex.Server", "REST_API.md");
            string postmanPath = Path.Combine(root, "Verbex.postman_collection.json");
            string dashboardApi = File.ReadAllText(dashboardApiPath);
            string restApi = File.ReadAllText(restApiPath);
            string serverRestApi = File.ReadAllText(serverRestApiPath);
            string postman = File.ReadAllText(postmanPath);
            string deletedBulkRoute = "/v1.0/requesthistory" + "/bulk";

            foreach (string assetPath in new[]
            {
                dashboardApiPath,
                restApiPath,
                serverRestApiPath,
                postmanPath,
                Path.Combine(root, "dashboard", "README.md"),
                Path.Combine(root, "sdk", "README.md"),
                Path.Combine(root, "sdk", "csharp", "README.md"),
                Path.Combine(root, "sdk", "js", "README.md"),
                Path.Combine(root, "sdk", "python", "README.md")
            })
            {
                string asset = File.ReadAllText(assetPath);
                TestAssert.IsFalse(
                    asset.Contains(deletedBulkRoute, StringComparison.Ordinal),
                    $"{assetPath} should not reference the old request history bulk delete route");
            }

            foreach (string assetPath in Directory.EnumerateFiles(Path.Combine(root, "dashboard", "dist"), "*.*", SearchOption.AllDirectories))
            {
                string extension = Path.GetExtension(assetPath);
                if (extension != ".html" && extension != ".js" && extension != ".css")
                {
                    continue;
                }

                string asset = File.ReadAllText(assetPath);
                TestAssert.IsFalse(
                    asset.Contains(deletedBulkRoute, StringComparison.Ordinal),
                    $"{assetPath} should not reference the old request history bulk delete route");
            }

            TestAssert.IsTrue(
                dashboardApi.Contains("this.post('/v1.0/requesthistory/delete'", StringComparison.Ordinal),
                "dashboard should call request history bulk delete with POST body");
            TestAssert.IsTrue(
                restApi.Contains("POST `/v1.0/requesthistory/delete`", StringComparison.Ordinal),
                "REST_API.md should document POST request history bulk delete");
            TestAssert.IsTrue(
                serverRestApi.Contains("### POST /v1.0/requesthistory/delete", StringComparison.Ordinal),
                "server REST_API.md should document POST request history bulk delete");
            TestAssert.IsTrue(
                postman.Contains("\"raw\": \"{{protocol}}://{{hostname}}:{{port}}/v1.0/requesthistory/delete\"", StringComparison.Ordinal),
                "Postman should include POST request history bulk delete");

            return Task.CompletedTask;
        }

        private static async Task AssertSummaryWindowAsync(DateTime fromUtc, int bucketMinutes, int expectedBucketCount)
        {
            (DatabaseDriverBase driver, RequestHistoryService service) = await CreateTestServiceAsync().ConfigureAwait(false);
            try
            {
                string tenantId = $"reqhist_{Guid.NewGuid():N}";
                DateTime lastBucketStartUtc = fromUtc.AddMinutes((expectedBucketCount - 1) * bucketMinutes);
                DateTime toUtc = lastBucketStartUtc.AddMinutes(bucketMinutes).AddSeconds(-1);

                await RecordAsync(service, tenantId, fromUtc.AddSeconds(30), true, 12).ConfigureAwait(false);
                await RecordAsync(service, tenantId, lastBucketStartUtc.AddSeconds(30), false, 36).ConfigureAwait(false);
                await RecordAsync(service, tenantId, fromUtc.AddMinutes(-bucketMinutes), true, 24).ConfigureAwait(false);

                RequestHistorySummary summary = await service.GetSummaryAsync(new RequestHistoryQuery
                {
                    TenantId = tenantId,
                    FromUtc = fromUtc,
                    ToUtc = toUtc,
                    BucketMinutes = bucketMinutes
                }).ConfigureAwait(false);

                TestAssert.CollectionCount(summary.Buckets, expectedBucketCount);
                TestAssert.AreEqual(fromUtc, summary.Buckets[0].BucketStartUtc);
                TestAssert.AreEqual(lastBucketStartUtc, summary.Buckets[summary.Buckets.Count - 1].BucketStartUtc);
                TestAssert.AreEqual(2, summary.TotalCount);
                TestAssert.AreEqual(1, summary.SuccessCount);
                TestAssert.AreEqual(1, summary.FailureCount);
                TestAssert.AreEqual(24d, summary.AverageDurationMs);
                TestAssert.AreEqual(1, summary.Buckets[0].TotalCount);
                TestAssert.AreEqual(1, summary.Buckets[summary.Buckets.Count - 1].TotalCount);
                TestAssert.AreEqual(0, summary.Buckets[summary.Buckets.Count / 2].TotalCount);
            }
            finally
            {
                service.Dispose();
                driver.Dispose();
            }
        }

        private static async Task RecordAsync(
            RequestHistoryService service,
            string tenantId,
            DateTime createdUtc,
            bool success,
            double durationMs)
        {
            RequestHistoryEntry entry = new RequestHistoryEntry
            {
                TenantId = tenantId,
                PrincipalType = "user",
                PrincipalName = "request-history-test",
                RequestType = RequestTypeEnum.IndexManagement.ToString(),
                HttpMethod = "GET",
                RouteTemplate = "/v1.0/requesthistory/summary",
                RequestUrl = "/v1.0/requesthistory/summary",
                StatusCode = success ? 200 : 500,
                Success = success,
                DurationMs = durationMs,
                CreatedUtc = createdUtc
            };

            await service.RecordAsync(entry, new RequestHistoryDetail()).ConfigureAwait(false);
        }

        private static async Task<(DatabaseDriverBase Driver, RequestHistoryService Service)> CreateTestServiceAsync()
        {
            DatabaseDriverBase driver = await CreateTestDriverAsync().ConfigureAwait(false);
            RequestHistoryService service = new RequestHistoryService(
                new RequestHistorySettings
                {
                    Enabled = true,
                    CleanupIntervalMinutes = 1440,
                    RetentionDays = 365
                },
                driver,
                new LoggingModule());

            await service.InitializeAsync().ConfigureAwait(false);
            return (driver, service);
        }

        private static async Task<DatabaseDriverBase> CreateTestDriverAsync()
        {
            DatabaseSettings settings = GetTestDatabaseSettings();
            DatabaseDriverBase driver = DatabaseDriverFactory.Create(settings);
            await driver.InitializeAsync().ConfigureAwait(false);
            return driver;
        }

        private static DatabaseSettings GetTestDatabaseSettings()
        {
            if (TestContext.DatabaseSettings == null)
            {
                throw new InvalidOperationException("Database settings not initialized. Call TestContext.Initialize() first.");
            }

            DatabaseSettings settings = TestContext.DatabaseSettings.Clone();

            if (settings.Type == DatabaseTypeEnum.Sqlite && !settings.InMemory && !string.IsNullOrEmpty(_TestDbPath))
            {
                settings.Filename = _TestDbPath;
            }

            return settings;
        }

        private static string GetRepositoryRoot()
        {
            DirectoryInfo? current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current != null)
            {
                if (File.Exists(Path.Combine(current.FullName, "REST_API.md")) &&
                    Directory.Exists(Path.Combine(current.FullName, "src", "Verbex.Server")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            throw new DirectoryNotFoundException("Unable to locate repository root.");
        }

        private static void CleanupTestDatabase()
        {
            if (TestContext.DatabaseSettings?.Type == DatabaseTypeEnum.Sqlite &&
                !TestContext.DatabaseSettings.InMemory)
            {
                TestContext.CleanupTestDatabaseFile(_TestDbPath);
            }
        }
    }
}
