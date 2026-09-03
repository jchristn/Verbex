namespace Verbex.Telemetry
{
    using System;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;

    /// <summary>
    /// Application-layer telemetry for the Verbex core library, emitted through the .NET base class
    /// library only. The library takes no dependency on OpenTelemetry or any telemetry host: it
    /// creates a <see cref="System.Diagnostics.Metrics.Meter"/> and a
    /// <see cref="System.Diagnostics.ActivitySource"/> named by the stable contract strings
    /// <see cref="MeterName"/> and <see cref="ActivitySourceName"/>, and every measurement is a cheap
    /// no-op until a host (for example the Verbex server or the Verbex MCP server) subscribes to those
    /// names and builds an export pipeline.
    /// <para>
    /// To collect these instruments, subscribe a meter listener to <see cref="MeterName"/> and an
    /// activity listener to <see cref="ActivitySourceName"/>. Instrument names follow the
    /// OpenTelemetry dotted-lowercase convention so they render in stock dashboards; units use UCUM
    /// (<c>s</c> for seconds, <c>{document}</c>/<c>{term}</c>/<c>{search}</c> for counts).
    /// </para>
    /// <para>
    /// Thread safety: all members are thread safe. The instruments are created once and shared; the
    /// record helpers may be called concurrently from any thread.
    /// </para>
    /// </summary>
    public static class VerbexTelemetry
    {
        #region Public-Members

        /// <summary>
        /// The meter name a host subscribes to in order to collect Verbex core-library metrics.
        /// Treated as a stable public contract; do not change it casually.
        /// </summary>
        public const string MeterName = "Verbex.Core";

        /// <summary>
        /// The activity-source name a host subscribes to in order to collect Verbex core-library
        /// spans. Treated as a stable public contract; do not change it casually.
        /// </summary>
        public const string ActivitySourceName = "Verbex.Core";

        /// <summary>
        /// Metric label key carrying the logical index name. Bounded (low) cardinality: the number of
        /// indices in a deployment, not per-document identifiers.
        /// </summary>
        public const string TagIndex = "verbex.index";

        /// <summary>
        /// Metric label key carrying the operation outcome (<c>ok</c> or <c>error</c>).
        /// </summary>
        public const string TagOutcome = "outcome";

        /// <summary>
        /// Metric label key carrying the search mode (<c>and</c>, <c>or</c>, or <c>wildcard</c>).
        /// </summary>
        public const string TagMode = "verbex.search.mode";

        /// <summary>
        /// Metric label key carrying the batch operation kind (<c>add</c> or <c>remove</c>).
        /// </summary>
        public const string TagOperation = "verbex.operation";

        /// <summary>
        /// The Verbex core-library meter. Never null.
        /// </summary>
        public static Meter Meter
        {
            get
            {
                return _Meter;
            }
        }

        /// <summary>
        /// The Verbex core-library activity source. Never null. Spans started from it become children
        /// of any ambient <see cref="Activity"/> (for example an HTTP server span), giving correct
        /// trace nesting when a host samples both sources.
        /// </summary>
        public static ActivitySource ActivitySource
        {
            get
            {
                return _ActivitySource;
            }
        }

        #endregion

        #region Private-Members

        private static readonly Meter _Meter = new Meter(MeterName);
        private static readonly ActivitySource _ActivitySource = new ActivitySource(ActivitySourceName);

        private static readonly Counter<long> _DocumentsIndexed =
            _Meter.CreateCounter<long>("verbex.documents.indexed", "{document}", "Documents successfully indexed.");

        private static readonly Counter<long> _DocumentsRemoved =
            _Meter.CreateCounter<long>("verbex.documents.removed", "{document}", "Documents removed from an index.");

        private static readonly Counter<long> _TermsIndexed =
            _Meter.CreateCounter<long>("verbex.terms.indexed", "{term}", "Term occurrences written during indexing.");

        private static readonly Histogram<double> _IndexDocumentDuration =
            _Meter.CreateHistogram<double>("verbex.index.document.duration", "s", "Duration to index a single document.");

        private static readonly Histogram<double> _BatchDuration =
            _Meter.CreateHistogram<double>("verbex.index.batch.duration", "s", "Duration of a batch add or remove operation.");

        private static readonly Counter<long> _SearchRequests =
            _Meter.CreateCounter<long>("verbex.search.requests", "{search}", "Search requests executed.");

        private static readonly Histogram<double> _SearchDuration =
            _Meter.CreateHistogram<double>("verbex.search.duration", "s", "Duration of a search request.");

        private static readonly Histogram<long> _SearchResults =
            _Meter.CreateHistogram<long>("verbex.search.results", "{document}", "Number of documents returned by a search.");

        #endregion

        #region Public-Methods

        /// <summary>
        /// Start a core-library span. Returns null when no listener is sampling, in which case callers
        /// use the null-conditional operator and pay no cost.
        /// </summary>
        /// <param name="name">The span name. Must be non-null and non-empty.</param>
        /// <param name="kind">The activity kind. Defaults to <see cref="ActivityKind.Internal"/>.</param>
        /// <returns>The started activity, or null when nothing is sampling.</returns>
        public static Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal)
        {
            if (String.IsNullOrEmpty(name)) return null;
            return _ActivitySource.StartActivity(name, kind);
        }

        /// <summary>
        /// Record the indexing of one document: increments the indexed-document counter, records the
        /// duration, and records the number of term occurrences written.
        /// </summary>
        /// <param name="indexName">The logical index name.</param>
        /// <param name="seconds">The indexing duration in seconds.</param>
        /// <param name="termOccurrences">The number of term occurrences written for the document.</param>
        /// <param name="success">Whether indexing succeeded.</param>
        public static void RecordDocumentIndexed(string? indexName, double seconds, long termOccurrences, bool success)
        {
            TagList tags = new TagList
            {
                { TagIndex, indexName ?? String.Empty },
                { TagOutcome, success ? "ok" : "error" }
            };

            _IndexDocumentDuration.Record(seconds, tags);
            if (success)
            {
                _DocumentsIndexed.Add(1, tags);
                if (termOccurrences > 0) _TermsIndexed.Add(termOccurrences, tags);
            }
        }

        /// <summary>
        /// Record the removal of one document.
        /// </summary>
        /// <param name="indexName">The logical index name.</param>
        /// <param name="removed">Whether a document was actually removed.</param>
        public static void RecordDocumentRemoved(string? indexName, bool removed)
        {
            if (!removed) return;
            TagList tags = new TagList { { TagIndex, indexName ?? String.Empty } };
            _DocumentsRemoved.Add(1, tags);
        }

        /// <summary>
        /// Record the execution of a search request.
        /// </summary>
        /// <param name="indexName">The logical index name.</param>
        /// <param name="mode">The search mode (<c>and</c>, <c>or</c>, or <c>wildcard</c>).</param>
        /// <param name="seconds">The search duration in seconds.</param>
        /// <param name="resultCount">The number of documents returned.</param>
        /// <param name="success">Whether the search succeeded.</param>
        public static void RecordSearch(string? indexName, string mode, double seconds, long resultCount, bool success)
        {
            TagList tags = new TagList
            {
                { TagIndex, indexName ?? String.Empty },
                { TagMode, mode },
                { TagOutcome, success ? "ok" : "error" }
            };

            _SearchRequests.Add(1, tags);
            _SearchDuration.Record(seconds, tags);
            if (success) _SearchResults.Record(resultCount, tags);
        }

        /// <summary>
        /// Record a batch add or remove operation.
        /// </summary>
        /// <param name="indexName">The logical index name.</param>
        /// <param name="operation">The operation kind (<c>add</c> or <c>remove</c>).</param>
        /// <param name="seconds">The batch duration in seconds.</param>
        /// <param name="success">Whether the batch succeeded.</param>
        public static void RecordBatch(string? indexName, string operation, double seconds, bool success)
        {
            TagList tags = new TagList
            {
                { TagIndex, indexName ?? String.Empty },
                { TagOperation, operation },
                { TagOutcome, success ? "ok" : "error" }
            };

            _BatchDuration.Record(seconds, tags);
        }

        #endregion
    }
}
