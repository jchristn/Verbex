namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Verbex.Server.Classes;

    /// <summary>
    /// Tests for EnumerationQuery and EnumerationResult classes.
    /// </summary>
    public static class EnumerationTests
    {
        /// <summary>
        /// Runs all enumeration tests.
        /// </summary>
        /// <param name="runner">Test runner to execute tests.</param>
        /// <returns>Task representing the asynchronous operation.</returns>
        public static async Task RunAllAsync(ITestCollector runner)
        {
            await Task.CompletedTask.ConfigureAwait(false);

            runner.RunTest("EnumerationQuery_DefaultValues", TestEnumerationQueryDefaults);
            runner.RunTest("EnumerationQuery_MaxResultsValidation", TestEnumerationQueryMaxResultsValidation);
            runner.RunTest("EnumerationQuery_SkipValidation", TestEnumerationQuerySkipValidation);
            runner.RunTest("EnumerationQuery_ParseFromStrings", TestEnumerationQueryParse);
            runner.RunTest("EnumerationQuery_ParseOrdering", TestEnumerationQueryParseOrdering);
            runner.RunTest("EnumerationResult_Pagination", TestEnumerationResultPagination);
            runner.RunTest("EnumerationResult_ContinuationToken", TestEnumerationResultContinuationToken);
            runner.RunTest("EnumerationResult_EndOfResults", TestEnumerationResultEndOfResults);
            runner.RunTest("EnumerationResult_EmptyPageTerminates", TestEnumerationResultEmptyPageTerminates);
            runner.RunTest("EnumerationResult_Empty", TestEnumerationResultEmpty);
            runner.RunTest("EnumerationQuery_ParseLabels", TestEnumerationQueryParseLabels);
            runner.RunTest("EnumerationQuery_ParseTags", TestEnumerationQueryParseTags);
            runner.RunTest("EnumerationQuery_LabelsAndTagsDefaults", TestEnumerationQueryLabelsAndTagsDefaults);
        }

        private static void TestEnumerationQueryDefaults()
        {
            EnumerationQuery query = new EnumerationQuery();

            TestAssert.AreEqual(100, query.MaxResults, "Default MaxResults should be 100");
            TestAssert.AreEqual(0, query.Skip, "Default Skip should be 0");
            TestAssert.IsNull(query.ContinuationToken, "Default ContinuationToken should be null");
            TestAssert.AreEqual(EnumerationOrderEnum.CreatedDescending, query.Ordering, "Default Ordering should be CreatedDescending");
        }

        private static void TestEnumerationQueryMaxResultsValidation()
        {
            EnumerationQuery query = new EnumerationQuery();

            // Valid values
            query.MaxResults = 1;
            TestAssert.AreEqual(1, query.MaxResults);

            query.MaxResults = 500;
            TestAssert.AreEqual(500, query.MaxResults);

            query.MaxResults = 1000;
            TestAssert.AreEqual(1000, query.MaxResults);

            // Invalid: 0
            bool caught = false;
            try
            {
                query.MaxResults = 0;
            }
            catch (ArgumentOutOfRangeException)
            {
                caught = true;
            }
            TestAssert.IsTrue(caught, "MaxResults=0 should throw ArgumentOutOfRangeException");

            // Invalid: negative
            caught = false;
            try
            {
                query.MaxResults = -1;
            }
            catch (ArgumentOutOfRangeException)
            {
                caught = true;
            }
            TestAssert.IsTrue(caught, "MaxResults=-1 should throw ArgumentOutOfRangeException");

            // Invalid: > 1000
            caught = false;
            try
            {
                query.MaxResults = 1001;
            }
            catch (ArgumentOutOfRangeException)
            {
                caught = true;
            }
            TestAssert.IsTrue(caught, "MaxResults=1001 should throw ArgumentOutOfRangeException");
        }

        private static void TestEnumerationQuerySkipValidation()
        {
            EnumerationQuery query = new EnumerationQuery();

            // Valid values
            query.Skip = 0;
            TestAssert.AreEqual(0, query.Skip);

            query.Skip = 100;
            TestAssert.AreEqual(100, query.Skip);

            query.Skip = 10000;
            TestAssert.AreEqual(10000, query.Skip);

            // Invalid: negative
            bool caught = false;
            try
            {
                query.Skip = -1;
            }
            catch (ArgumentOutOfRangeException)
            {
                caught = true;
            }
            TestAssert.IsTrue(caught, "Skip=-1 should throw ArgumentOutOfRangeException");
        }

        private static void TestEnumerationQueryParse()
        {
            // Test with all parameters
            EnumerationQuery query = EnumerationQuery.Parse("50", "10", "abc123", "CreatedAscending");
            TestAssert.AreEqual(50, query.MaxResults);
            TestAssert.AreEqual(10, query.Skip);
            TestAssert.AreEqual("abc123", query.ContinuationToken);
            TestAssert.AreEqual(EnumerationOrderEnum.CreatedAscending, query.Ordering);

            // Test with null/empty parameters
            query = EnumerationQuery.Parse(null, null, null, null);
            TestAssert.AreEqual(100, query.MaxResults);
            TestAssert.AreEqual(0, query.Skip);
            TestAssert.IsNull(query.ContinuationToken);
            TestAssert.AreEqual(EnumerationOrderEnum.CreatedDescending, query.Ordering);

            // Test with empty strings
            query = EnumerationQuery.Parse("", "", "", "");
            TestAssert.AreEqual(100, query.MaxResults);
            TestAssert.AreEqual(0, query.Skip);
            TestAssert.IsNull(query.ContinuationToken);
            TestAssert.AreEqual(EnumerationOrderEnum.CreatedDescending, query.Ordering);

            // Test with invalid number strings (should keep defaults)
            query = EnumerationQuery.Parse("invalid", "notanumber", null, null);
            TestAssert.AreEqual(100, query.MaxResults);
            TestAssert.AreEqual(0, query.Skip);
        }

        private static void TestEnumerationQueryParseOrdering()
        {
            // Test various ordering string formats
            EnumerationQuery query = EnumerationQuery.Parse(null, null, null, "CreatedAscending");
            TestAssert.AreEqual(EnumerationOrderEnum.CreatedAscending, query.Ordering);

            query = EnumerationQuery.Parse(null, null, null, "CreatedDescending");
            TestAssert.AreEqual(EnumerationOrderEnum.CreatedDescending, query.Ordering);

            query = EnumerationQuery.Parse(null, null, null, "asc");
            TestAssert.AreEqual(EnumerationOrderEnum.CreatedAscending, query.Ordering);

            query = EnumerationQuery.Parse(null, null, null, "desc");
            TestAssert.AreEqual(EnumerationOrderEnum.CreatedDescending, query.Ordering);

            query = EnumerationQuery.Parse(null, null, null, "ascending");
            TestAssert.AreEqual(EnumerationOrderEnum.CreatedAscending, query.Ordering);

            query = EnumerationQuery.Parse(null, null, null, "descending");
            TestAssert.AreEqual(EnumerationOrderEnum.CreatedDescending, query.Ordering);

            // Case insensitive
            query = EnumerationQuery.Parse(null, null, null, "CREATEDDESCENDING");
            TestAssert.AreEqual(EnumerationOrderEnum.CreatedDescending, query.Ordering);
        }

        private static void TestEnumerationResultPagination()
        {
            EnumerationQuery query = new EnumerationQuery(10, 5);
            List<string> objects = new List<string> { "a", "b", "c", "d", "e" };
            long totalRecords = 25;

            EnumerationResult<string> result = new EnumerationResult<string>(query, objects, totalRecords);

            TestAssert.AreEqual(true, result.Success);
            TestAssert.AreEqual(10, result.MaxResults);
            TestAssert.AreEqual(5, result.Skip);
            TestAssert.AreEqual(25L, result.TotalRecords);
            TestAssert.AreEqual(15L, result.RecordsRemaining); // 25 - (5 + 5) = 15
            TestAssert.AreEqual(5, result.Objects.Count);
            TestAssert.IsFalse(result.EndOfResults);
            TestAssert.IsNotNull(result.ContinuationToken);
        }

        private static void TestEnumerationResultContinuationToken()
        {
            EnumerationQuery query = new EnumerationQuery(10, 0);
            List<string> objects = new List<string> { "a", "b", "c" };
            long totalRecords = 20;

            EnumerationResult<string> result = new EnumerationResult<string>(query, objects, totalRecords);

            // Should have continuation token when not at end
            TestAssert.IsNotNull(result.ContinuationToken, "ContinuationToken should be set when more records exist");
            TestAssert.IsFalse(result.EndOfResults);

            // Parse the continuation token
            bool parsed = EnumerationResult<string>.TryParseContinuationToken(result.ContinuationToken, out int skip);
            TestAssert.IsTrue(parsed, "Continuation token should be parseable");
            TestAssert.AreEqual(3, skip, "Continuation token should encode skip=3 (0 + 3 items)");
        }

        private static void TestEnumerationResultEndOfResults()
        {
            EnumerationQuery query = new EnumerationQuery(10, 15);
            List<string> objects = new List<string> { "a", "b", "c", "d", "e" };
            long totalRecords = 20;

            EnumerationResult<string> result = new EnumerationResult<string>(query, objects, totalRecords);

            // 15 skip + 5 objects = 20, which equals total records
            TestAssert.AreEqual(0L, result.RecordsRemaining);
            TestAssert.IsTrue(result.EndOfResults, "EndOfResults should be true when no more records");
            TestAssert.IsNull(result.ContinuationToken, "ContinuationToken should be null at end of results");
        }

        private static void TestEnumerationResultEmptyPageTerminates()
        {
            EnumerationQuery query = new EnumerationQuery(10, 10);
            List<string> objects = new List<string>();
            long totalRecords = 25;

            EnumerationResult<string> result = new EnumerationResult<string>(query, objects, totalRecords);

            TestAssert.AreEqual(15L, result.RecordsRemaining);
            TestAssert.IsTrue(result.EndOfResults, "Empty pages should terminate even when TotalRecords is stale");
            TestAssert.IsNull(result.ContinuationToken, "Empty pages should not emit a repeat-skip continuation token");
        }

        private static void TestEnumerationResultEmpty()
        {
            EnumerationQuery query = new EnumerationQuery(10, 0);
            EnumerationResult<string> result = EnumerationResult<string>.Empty(query);

            TestAssert.AreEqual(true, result.Success);
            TestAssert.AreEqual(10, result.MaxResults);
            TestAssert.AreEqual(0, result.Skip);
            TestAssert.AreEqual(0L, result.TotalRecords);
            TestAssert.AreEqual(0L, result.RecordsRemaining);
            TestAssert.AreEqual(0, result.Objects.Count);
            TestAssert.IsTrue(result.EndOfResults);
            TestAssert.IsNull(result.ContinuationToken);
        }
        private static void TestEnumerationQueryParseLabels()
        {
            // Parse comma-separated labels
            EnumerationQuery query = EnumerationQuery.Parse(null, null, null, null, "important, reviewed, active");
            TestAssert.IsNotNull(query.Labels, "Labels should not be null");
            TestAssert.AreEqual(3, query.Labels!.Count, "Should have 3 labels");
            TestAssert.IsTrue(query.Labels.Contains("important"), "Should contain 'important'");
            TestAssert.IsTrue(query.Labels.Contains("reviewed"), "Should contain 'reviewed'");
            TestAssert.IsTrue(query.Labels.Contains("active"), "Should contain 'active'");

            // Null labels
            query = EnumerationQuery.Parse(null, null, null, null, null);
            TestAssert.IsNull(query.Labels, "Labels should be null when not provided");

            // Empty labels
            query = EnumerationQuery.Parse(null, null, null, null, "");
            TestAssert.IsNull(query.Labels, "Labels should be null when empty string provided");

            // Labels with extra commas/spaces
            query = EnumerationQuery.Parse(null, null, null, null, " , foo , , bar , ");
            TestAssert.IsNotNull(query.Labels, "Labels should not be null");
            TestAssert.AreEqual(2, query.Labels!.Count, "Should have 2 labels after filtering empty entries");
        }

        private static void TestEnumerationQueryParseTags()
        {
            // Parse tags dictionary
            Dictionary<string, string> tags = new Dictionary<string, string>
            {
                { "category", "tech" },
                { "status", "published" }
            };
            EnumerationQuery query = EnumerationQuery.Parse(null, null, null, null, null, tags);
            TestAssert.IsNotNull(query.Tags, "Tags should not be null");
            TestAssert.AreEqual(2, query.Tags!.Count, "Should have 2 tags");
            TestAssert.AreEqual("tech", query.Tags["category"]);
            TestAssert.AreEqual("published", query.Tags["status"]);

            // Null tags
            query = EnumerationQuery.Parse(null, null, null, null, null, null);
            TestAssert.IsNull(query.Tags, "Tags should be null when not provided");

            // Empty tags
            query = EnumerationQuery.Parse(null, null, null, null, null, new Dictionary<string, string>());
            TestAssert.IsNull(query.Tags, "Tags should be null when empty dictionary provided");
        }

        private static void TestEnumerationQueryLabelsAndTagsDefaults()
        {
            EnumerationQuery query = new EnumerationQuery();
            TestAssert.IsNull(query.Labels, "Default Labels should be null");
            TestAssert.IsNull(query.Tags, "Default Tags should be null");
        }
    }
}
