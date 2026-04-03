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
