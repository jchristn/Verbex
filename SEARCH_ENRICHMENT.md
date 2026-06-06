# Verbex Search Result Enrichment Plan

This plan describes a fully additive search API enrichment for Verbex. It is written so a developer can annotate each item with progress, notes, and completion without changing the structure.

## Goals

- [x] Keep the existing REST search response valid for current clients.
- [x] Add optional enriched search result fields for UI clients such as AssistantHub.
- [x] Expose matched terms and per-term details without adding database work.
- [x] Expose whole-document term count statistics only when requested.
- [x] Avoid N+1 database calls.
- [x] Update all database providers consistently.
- [x] Update REST OpenAPI metadata, SDKs, Postman, docs, tests, and dashboard-facing examples.
- [ ] Treat the work as a whole-product change across server, core library, REST, MCP, CLI, dashboard, SDKs, generated/API docs, markdown docs, Postman, release notes, package metadata, and validation scripts.

## Non-Goals

- [ ] Do not remove, rename, or move current response fields.
- [ ] Do not replace `SearchResult` as the default serialized object unless strict backward compatibility is proven.
- [ ] Do not return full document text or full term lists by default.
- [ ] Do not make whole-document term statistics part of the default response.
- [ ] Do not add provider behavior that only works in PostgreSQL.

## Current Code Findings

- [ ] Confirmed: `src/Verbex.Server/API/REST/RestServiceHandler.cs` `SearchIndexRoute` deserializes `SearchRequest`, calls `index.SearchAsync(...)`, and returns `searchResults.Results` directly.
- [ ] Confirmed: `src/Verbex/InvertedIndex.cs` `SearchAsync` already fetches query terms, matches, per-query-term frequencies, and document metadata in batches.
- [ ] Confirmed: `src/Verbex/SearchResult.cs` already exposes `DocumentId`, `Document`, `Score`, `MatchedTermCount`, `TermScores`, `TermFrequencies`, and `TotalTermMatches`.
- [ ] Confirmed: `src/Verbex/SearchResult.cs` has `GetMatchedTerms()`, but it is a method and is not serialized as a JSON property.
- [ ] Confirmed: `src/Verbex/DocumentMetadata.cs` exposes document metadata including `DocumentPath`, `OriginalFileName`, `DocumentLength`, `IndexedDate`, `LastModified`, `ContentSha256`, `Terms`, `IsDeleted`, `CustomMetadata`, `Tags`, `Labels`, and `IndexingRuntimeMs`.
- [ ] Confirmed: `src/Verbex/Database/Interfaces/IDocumentTermMethods.cs` has `GetByDocumentsAndTermsAsync(...)` and `GetByDocumentsAsync(...)`.
- [ ] Gap: there is no cheap aggregate API for document term statistics such as unique term count and total term occurrences.
- [ ] Gap: OpenAPI search-result schema currently documents fields such as `MatchedTerms` that are not directly serialized in the current response.

## Whole-Product Surface Inventory

Every item in this section must be reviewed before the plan can be considered complete.

Core and server:

- [ ] `src/Verbex/SearchResult.cs`
- [ ] `src/Verbex/SearchResults.cs`
- [ ] `src/Verbex/DocumentMetadata.cs`
- [ ] `src/Verbex/InvertedIndex.cs`
- [ ] `src/Verbex/Database/Interfaces/IDocumentTermMethods.cs`
- [ ] `src/Verbex/Database/Postgresql/Implementations/DocumentTermMethods.cs`
- [ ] `src/Verbex/Database/Mysql/Implementations/DocumentTermMethods.cs`
- [ ] `src/Verbex/Database/Sqlite/Implementations/DocumentTermMethods.cs`
- [ ] `src/Verbex/Database/SqlServer/Implementations/DocumentTermMethods.cs`
- [ ] `src/Verbex.Server/Classes/SearchRequest.cs`
- [ ] `src/Verbex.Server/API/REST/RestServiceHandler.cs`
- [ ] `src/Verbex.Mcp/Program.cs`
- [ ] `src/VerbexCli/Commands/SearchCommands.cs`
- [ ] `src/VerbexCli/Infrastructure/IndexManager.cs`
- [ ] Verbex dashboard search pages and API calls under `dashboard/`

API and generated metadata:

- [ ] Generated REST OpenAPI/swagger metadata emitted by the server.
- [ ] REST route examples and schemas.
- [ ] MCP tool schemas and descriptions.
- [ ] CLI command help text and examples.
- [ ] Request history/API explorer display if it formats search request or response bodies.

SDKs and client artifacts:

- [ ] `sdk/README.md`
- [ ] `sdk/csharp/README.md`
- [ ] `sdk/csharp/Verbex.Sdk/SearchRequest.cs`
- [ ] `sdk/csharp/Verbex.Sdk/SearchResult.cs`
- [ ] `sdk/csharp/Verbex.Sdk/SearchData.cs`
- [ ] `sdk/csharp/Verbex.Sdk/VerbexClient.cs`
- [ ] `sdk/csharp/Verbex.Sdk/Verbex.Sdk.csproj`
- [ ] `sdk/csharp/Verbex.Sdk.TestHarness/`
- [ ] `sdk/js/verbex-sdk.js`
- [ ] `sdk/js/README.md`
- [ ] `sdk/js/package.json`
- [ ] `sdk/js/test-harness.js`
- [ ] `sdk/python/verbex_sdk.py`
- [ ] `sdk/python/README.md`

Markdown documentation:

- [ ] `README.md`
- [ ] `REST_API.md`
- [ ] `SEARCH_PERFORMANCE.md`
- [ ] `SCORING.md`
- [ ] `VBX_CLI.md`
- [ ] `DOCKER.md`
- [ ] `CHANGELOG.md`
- [ ] `CONTRIBUTING.md` if API testing or contribution guidance changes.
- [ ] `CLAUDE.md` only if maintainer workflow guidance needs to mention the new contract.
- [ ] `SEARCH_ENRICHMENT.md` progress annotations.

Collections, examples, packaging, and release:

- [ ] `Verbex.postman_collection.json`
- [ ] Docker images and compose examples if dashboard/server behavior or env vars are affected.
- [ ] Build scripts: `build-all.*`, `build-server.*`, `build-dashboard.*` if release validation needs new steps.
- [ ] Package version/release metadata for server, dashboard, C# SDK, JS SDK, and Python SDK if this ships as a release.
- [ ] Release notes and migration notes.

## Compatibility Contract

- [ ] Default request behavior must remain unchanged.
- [ ] Current top-level response shape must remain unchanged.
- [ ] Current result item fields must remain unchanged.
- [ ] New request fields must be optional and have backward-compatible defaults.
- [ ] New response fields must be optional and additive.
- [ ] Strict clients that ignore unknown properties should continue to work.
- [ ] SDK additions should preserve existing method signatures where possible by adding overloads or optional options objects.

Current default response shape to preserve:

```json
{
  "Data": {
    "Query": "botox",
    "Results": [
      {
        "DocumentId": "doc_123",
        "Document": {},
        "MatchedTermCount": 1,
        "Score": 0.8123,
        "TermScores": {},
        "TermFrequencies": {},
        "TotalTermMatches": 6
      }
    ],
    "TotalCount": 1,
    "MaxResults": 25,
    "SearchTime": 8.42,
    "TimingInfo": {}
  }
}
```

Enriched response shape, only when requested:

```json
{
  "Data": {
    "Query": "botox",
    "Results": [
      {
        "DocumentId": "doc_123",
        "Document": {},
        "MatchedTermCount": 1,
        "Score": 0.8123,
        "TermScores": {
          "botox": 0.7123
        },
        "TermFrequencies": {
          "botox": 6
        },
        "TotalTermMatches": 6,
        "MatchedTerms": ["botox"],
        "TermDetails": [
          {
            "Term": "botox",
            "Score": 0.7123,
            "Frequency": 6
          }
        ],
        "DocumentTermStats": {
          "UniqueTermCount": 1840,
          "TotalTermOccurrences": 78893
        }
      }
    ],
    "TotalCount": 1,
    "MaxResults": 25,
    "SearchTime": 8.42,
    "TimingInfo": {
      "TermLookupMs": 1,
      "TermsFound": 1,
      "MainSearchMs": 2,
      "MatchesFound": 1,
      "TermFrequenciesMs": 1,
      "TermFrequencyRecords": 1,
      "DocumentMetadataMs": 2,
      "DocumentsFetched": 1,
      "DocumentCountMs": 1,
      "TotalDocuments": 100,
      "ResultEnrichmentMs": 0,
      "DocumentTermStatsMs": 1,
      "DocumentTermStatsDocuments": 1
    }
  }
}
```

## Proposed Request Additions

Add optional fields to `src/Verbex.Server/Classes/SearchRequest.cs`:

```json
{
  "Query": "botox",
  "MaxResults": 25,
  "UseAndLogic": false,
  "Labels": ["medical"],
  "Tags": {
    "source": "upload"
  },
  "IncludeMatchedTerms": true,
  "IncludeTermDetails": true,
  "IncludeDocumentTermStats": true
}
```

Recommended defaults:

- [x] `IncludeMatchedTerms`: `false`
- [x] `IncludeTermDetails`: `false`
- [x] `IncludeDocumentTermStats`: `false`

Optional future flags to consider after the core change:

- [ ] `IncludeDocumentMetadata`: keep current behavior first; only add this later if payload trimming is needed.
- [ ] `IncludeFullDocumentTerms`: default false; only return full terms if a strong use case exists.
- [ ] `IncludePositions`: default false; potentially expensive and payload-heavy.

## Proposed Response Additions

Add these optional fields to each result item only when requested:

- [x] `MatchedTerms`: array of strings.
- [x] `TermDetails`: array of objects with `Term`, `Score`, and `Frequency`.
- [x] `DocumentTermStats`: object with `UniqueTermCount` and `TotalTermOccurrences`.

Do not add these by default until compatibility with strict deserializers is intentionally accepted:

- [ ] `MatchedTerms`
- [ ] `TermDetails`
- [ ] `DocumentTermStats`

## Data Model Additions

Add a small model for document term statistics:

- [x] Add `src/Verbex/Models/DocumentTermStats.cs` or equivalent.
- [x] Fields:
  - [x] `DocumentId`
  - [x] `UniqueTermCount`
  - [x] `TotalTermOccurrences`

Suggested C# model:

```csharp
namespace Verbex.Models
{
    public class DocumentTermStats
    {
        public string DocumentId { get; set; } = string.Empty;
        public long UniqueTermCount { get; set; }
        public long TotalTermOccurrences { get; set; }
    }
}
```

Add response DTOs in the server layer:

- [x] Add `SearchResultEnrichmentOptions`.
- [x] Add `SearchResultTermDetail`.
- [x] Add `SearchResultDocumentTermStats`.
- [x] Add `SearchResultEnrichedView` or construct an anonymous object carefully in one helper.

Preferred approach:

- [x] Keep the domain `Verbex.SearchResult` class unchanged for core library compatibility.
- [x] Build enriched response views in the REST server route.
- [x] Avoid adding serialization-specific properties to the core domain class unless the SDK and REST behavior intentionally standardize on them.

## Database Interface Work

Add a batch aggregate method to `src/Verbex/Database/Interfaces/IDocumentTermMethods.cs`:

```csharp
Task<Dictionary<string, DocumentTermStats>> GetStatsByDocumentsAsync(
    string tablePrefix,
    IEnumerable<string> documentIds,
    CancellationToken token = default);
```

Checklist:

- [x] Add the interface method.
- [x] Add XML comments.
- [x] Use `Dictionary<string, DocumentTermStats>` for fast result lookup by document ID.
- [x] Return an empty dictionary when `documentIds` is empty.
- [x] Deduplicate `documentIds` before querying.
- [x] Keep the query bounded by the already-limited result set.

Provider implementations:

- [x] PostgreSQL: `src/Verbex/Database/Postgresql/Implementations/DocumentTermMethods.cs`
- [x] MySQL: `src/Verbex/Database/Mysql/Implementations/DocumentTermMethods.cs`
- [x] SQLite: `src/Verbex/Database/Sqlite/Implementations/DocumentTermMethods.cs`
- [x] SQL Server: `src/Verbex/Database/SqlServer/Implementations/DocumentTermMethods.cs`

Provider SQL intent:

```sql
SELECT
  document_id,
  COUNT(*) AS unique_term_count,
  COALESCE(SUM(term_frequency), 0) AS total_term_occurrences
FROM {tablePrefix}_document_terms
WHERE document_id IN (...)
GROUP BY document_id;
```

Provider-specific notes:

- [ ] PostgreSQL: use the existing parameter style and `DataTable` mapping pattern already used in `DocumentTermMethods`.
- [ ] MySQL: use the provider's current `IN` parameter construction pattern.
- [ ] SQLite: use the provider's current `IN` parameter construction pattern.
- [ ] SQL Server: use the provider's current bracket/parameter conventions and avoid string-built values.
- [ ] All providers: use table prefix validation/quoting exactly as existing methods do.
- [ ] All providers: do not load character positions or term positions.
- [ ] All providers: do not call `GetByDocumentsAsync` and aggregate in memory for this feature.

## Core Search Flow

Primary implementation option:

- [x] Keep `InvertedIndex.SearchAsync(...)` unchanged.
- [x] In `SearchIndexRoute`, after `SearchAsync`, inspect enrichment flags.
- [x] Use already-present `TermScores` and `TermFrequencies` to build `MatchedTerms` and `TermDetails`.
- [x] Only call `index` or driver-level term stats when `IncludeDocumentTermStats` is true.

If a core API is needed for term stats:

- [x] Add `InvertedIndex.GetDocumentTermStatsAsync(IEnumerable<string> documentIds, CancellationToken token = default)`.
- [x] The method should validate/open state the same way other public methods do.
- [x] The method should delegate to `_Driver.DocumentTerms.GetStatsByDocumentsAsync(_TablePrefix, documentIds, token)`.
- [x] The method should return an empty dictionary for empty input.

Do not:

- [ ] Do not recalculate query tokenization in the REST layer.
- [ ] Do not fetch all terms for every document.
- [ ] Do not add one database call per result.
- [ ] Do not change the scoring algorithm for this feature.

## REST Route Work

Update `src/Verbex.Server/API/REST/RestServiceHandler.cs`.

Search route checklist:

- [x] Preserve current request validation.
- [x] Preserve current label/tag filtering behavior.
- [x] Preserve current default `Data.Results = searchResults.Results`.
- [x] Add a helper that checks whether any enrichment flag is true.
- [x] If no enrichment flag is true, return the current response shape.
- [x] If any enrichment flag is true, project each result to an additive result object.
- [x] Include all legacy fields in the projected result.
- [x] Add `MatchedTerms` only when `IncludeMatchedTerms` or `IncludeTermDetails` is true.
- [x] Add `TermDetails` only when `IncludeTermDetails` is true.
- [x] Add `DocumentTermStats` only when `IncludeDocumentTermStats` is true.
- [x] Add enrichment timing to `TimingInfo` only when enrichment runs.
- [x] Keep `SearchTime` as the core search duration unless intentionally documented otherwise.
- [ ] Optionally add a separate `TotalProcessingTime` or `ResultEnrichmentTime` if total route time is useful.

Legacy fields that must be carried forward in enriched projection:

- [ ] `DocumentId`
- [ ] `Document`
- [ ] `MatchedTermCount`
- [ ] `Score`
- [ ] `TermScores`
- [ ] `TermFrequencies`
- [ ] `TotalTermMatches`

Term details construction:

```csharp
List<object> termDetails = result.TermScores
    .Select(kvp => new
    {
        Term = kvp.Key,
        Score = kvp.Value,
        Frequency = result.TermFrequencies.TryGetValue(kvp.Key, out int frequency) ? frequency : 0
    })
    .OrderByDescending(x => x.Score)
    .ThenBy(x => x.Term)
    .ToList<object>();
```

Matched terms construction:

```csharp
List<string> matchedTerms = result.TermScores.Keys
    .OrderBy(term => term)
    .ToList();
```

Document term stats lookup:

```csharp
Dictionary<string, DocumentTermStats> statsByDocumentId = includeStats
    ? await index.GetDocumentTermStatsAsync(searchResults.Results.Select(r => r.DocumentId), token).ConfigureAwait(false)
    : new Dictionary<string, DocumentTermStats>();
```

Wildcard search behavior:

- [x] For `Query = "*"`, `MatchedTerms` should be an empty array when requested.
- [x] For `Query = "*"`, `TermDetails` should be an empty array when requested.
- [x] For `Query = "*"`, `DocumentTermStats` should be populated when requested.

## Timing and Observability

Extend timing information additively:

- [x] `ResultEnrichmentMs`: elapsed time for in-memory result projection.
- [x] `DocumentTermStatsMs`: elapsed time for the optional grouped stats query.
- [x] `DocumentTermStatsDocuments`: number of documents requested for stats.

Rules:

- [x] Existing timing fields remain unchanged.
- [x] New timing fields are nullable or omitted when not applicable.
- [ ] Logs should include whether enrichment was requested and whether stats were requested.
- [ ] Do not log query contents at higher verbosity than current search logging policy.

## OpenAPI Work

Update `CreateSearchRequestSchema()`:

- [x] Add `IncludeMatchedTerms`.
- [x] Add `IncludeTermDetails`.
- [x] Add `IncludeDocumentTermStats`.
- [x] Mark all enrichment flags optional.
- [x] Document defaults as `false`.
- [x] Document that flags are additive and do not change legacy fields.

Update `CreateSearchResultsSchema()`:

- [x] Include `MaxResults`.
- [x] Include `TimingInfo`.
- [x] Ensure current legacy fields match actual serialized fields.
- [x] Add optional enriched fields with descriptions.

Update `CreateSearchResultItemSchema()`:

- [x] Keep `DocumentId`.
- [x] Add or correct `Document`.
- [x] Keep `MatchedTermCount`.
- [x] Keep `Score`.
- [x] Keep `TermScores`.
- [x] Keep `TermFrequencies`.
- [x] Keep `TotalTermMatches`.
- [x] Mark `MatchedTerms` as optional and only present when requested.
- [x] Mark `TermDetails` as optional and only present when requested.
- [x] Mark `DocumentTermStats` as optional and only present when requested.
- [x] Remove or correct stale fields such as top-level `DocumentName` if they are not actually returned.

Add schemas:

- [x] `CreateSearchTermDetailSchema()`
- [x] `CreateDocumentTermStatsSchema()`
- [x] Optional: `CreateSearchTimingInfoSchema()`

## REST Documentation Work

Update `REST_API.md`:

- [x] Document the compatibility contract.
- [x] Document the new request flags.
- [x] Document default non-enriched behavior.
- [x] Document enriched response examples.
- [x] Document wildcard behavior.
- [x] Document performance guidance.
- [x] Document that document term stats add one grouped database query.

Add examples:

- [ ] Default search.
- [ ] Search with matched terms and term details.
- [ ] Search with document term stats.
- [ ] Wildcard search with document term stats.
- [ ] Search with labels and tags plus enrichment.

## Markdown Documentation Work

Update every markdown document whose search/API/product guidance is affected.

Root documentation checklist:

- [x] `README.md`: update search overview and examples if the README describes search result contents.
- [x] `REST_API.md`: document request flags, response fields, examples, and compatibility behavior.
- [x] `SEARCH_PERFORMANCE.md`: describe zero-extra-query enrichment and optional one-query document term stats.
- [x] `SCORING.md`: mention `TermDetails` if score contribution per term becomes a documented API feature.
- [x] `VBX_CLI.md`: document CLI search behavior if CLI adds flags or displays enriched fields.
- [ ] `DOCKER.md`: update only if server/dashboard env vars or image validation steps change.
- [x] `CHANGELOG.md`: add a release note under the target version.
- [ ] `CONTRIBUTING.md`: update only if provider-test expectations or OpenAPI/doc update rules change.
- [ ] `CLAUDE.md`: update only if maintainer workflow guidance should require API-surface sync for search changes.

SDK documentation checklist:

- [ ] `sdk/README.md`: update cross-SDK search behavior.
- [ ] `sdk/csharp/README.md`: add C# enriched search example.
- [ ] `sdk/js/README.md`: add JavaScript enriched search example.
- [ ] `sdk/python/README.md`: add Python enriched search example.

Documentation quality checklist:

- [ ] Every documented field must exist in either default or enriched response output.
- [ ] Every optional field must state the request flag that enables it.
- [ ] Every example must preserve the current top-level response envelope.
- [ ] Performance-sensitive fields must include guidance about when to request them.
- [ ] Backward compatibility must be called out directly.

## MCP Work

Inspect `src/Verbex.Mcp/Program.cs`.

Checklist:

- [x] Add optional enrichment parameters to the MCP search tool input schema.
- [x] Preserve current MCP defaults.
- [x] Ensure MCP search output includes enriched fields only when requested, or document if MCP always passes through server/core fields.
- [x] Update any examples shown in MCP tool descriptions.
- [ ] Validate search with and without enrichment through MCP.

## CLI Work

Inspect `src/VerbexCli/Commands/SearchCommands.cs` and `src/VerbexCli/Infrastructure/IndexManager.cs`.

Checklist:

- [x] Decide whether CLI search should support enrichment flags.
- [x] Preserve current CLI search output by default.
- [x] Add optional CLI flags only if the CLI will display enriched fields.
- [x] Suggested flags if implemented:
  - [x] `--matched-terms`
  - [x] `--term-details`
  - [x] `--term-stats`
- [x] Ensure CLI output is useful in both human-readable and JSON modes if JSON mode exists.
- [x] Update `VBX_CLI.md`.
- [ ] Update CLI tests or manual validation notes.

## SDK Work

General SDK rules:

- [x] Keep existing search method signatures working.
- [x] Add optional parameters or an options object for enrichment.
- [x] Add response properties as optional/additive fields.
- [x] Preserve existing JSON casing conventions in each SDK.
- [ ] Add tests or test harness coverage for default and enriched search.

C# SDK:

- [x] Update `sdk/csharp/Verbex.Sdk/SearchRequest.cs`.
- [x] Add `IncludeMatchedTerms`.
- [x] Add `IncludeTermDetails`.
- [x] Add `IncludeDocumentTermStats`.
- [x] Update `sdk/csharp/Verbex.Sdk/SearchResult.cs`.
- [x] Add optional `MatchedTerms`.
- [x] Add optional `TermDetails`.
- [x] Add optional `DocumentTermStats`.
- [x] Consider adding `Document` if current SDK result model cannot access returned document metadata.
- [x] Update `VerbexClient.SearchAsync(...)` with optional overload or options object.
- [x] Update XML docs.
- [ ] Update C# SDK test harness.

JavaScript SDK:

- [x] Update `sdk/js/verbex-sdk.js` `search(...)`.
- [x] Prefer an optional `options` object to avoid too many positional parameters.
- [x] Keep current positional arguments supported.
- [x] Parse `matchedTerms`, `termDetails`, and `documentTermStats`.
- [x] Update `sdk/js/README.md`.
- [ ] Update JS test harness.

Python SDK:

- [x] Update `sdk/python/verbex_sdk.py` `search(...)`.
- [x] Add optional keyword args: `include_matched_terms`, `include_term_details`, `include_document_term_stats`.
- [x] Add dataclass fields for `matched_terms`, `term_details`, and `document_term_stats`.
- [x] Ensure response parsing handles both snake_case and camel/Pascal converted data consistently with current SDK behavior.
- [x] Update Python docs/examples.

SDK packaging and release metadata:

- [ ] Confirm whether the C# SDK package version changes in `sdk/csharp/Verbex.Sdk/Verbex.Sdk.csproj`.
- [ ] Confirm whether the JS SDK package version changes in `sdk/js/package.json`.
- [ ] Confirm whether the Python SDK has package metadata that needs a version bump.
- [ ] Confirm generated XML docs are updated or intentionally regenerated.
- [ ] Confirm package readmes include enriched search examples before publishing.
- [ ] Confirm test harnesses validate default and enriched search before release packaging.

## Postman Work

Update `Verbex.postman_collection.json`:

- [x] Add default search request example with no enrichment flags.
- [x] Add enriched search request example with all enrichment flags true.
- [x] Add wildcard enriched search example.
- [x] Add labels/tags enriched search example.
- [x] Document expected optional fields in example response bodies if response examples are maintained.
- [x] Ensure the collection uses the same route, headers, auth variables, and base URL conventions as existing requests.
- [x] Add or update collection variables only if required.
- [x] If examples are grouped by feature, place enriched examples under the existing Search section.
- [ ] Validate imported collection in Postman after editing JSON.

## API Explorer and Request History Work

Review dashboard/server features that display API request or response bodies.

- [ ] Confirm API Explorer shows the new search request flags.
- [ ] Confirm API Explorer examples include both default and enriched requests.
- [ ] Confirm request-history views can display enriched search bodies without layout or serialization issues.
- [ ] Confirm generated OpenAPI/swagger UI shows optional fields and descriptions accurately.

## Dashboard Work

Dashboard changes should consume the enriched API only where needed.

- [x] Identify Verbex dashboard search request path.
- [x] Add UI toggle or default request behavior for enrichment if the UI displays matched terms or term stats.
- [x] For result tables needing matched terms, send `IncludeMatchedTerms = true`.
- [ ] For result detail modals needing per-term score/frequency, send `IncludeTermDetails = true`.
- [ ] For result tables needing unique/total term counts, send `IncludeDocumentTermStats = true`.
- [x] Avoid requesting `IncludeDocumentTermStats` on every search unless the UI visibly needs it.
- [x] Handle missing enriched fields gracefully for older servers.

AssistantHub integration guidance:

- [ ] AssistantHub should request `IncludeMatchedTerms = true` and `IncludeTermDetails = true` for `ARTIFACTS > Indices > Search`.
- [ ] AssistantHub should request `IncludeDocumentTermStats = true` only if it needs `UniqueTermCount` or `TotalTermOccurrences`.
- [ ] AssistantHub should continue deriving fallback matched terms from `TermScores` or `TermFrequencies` when talking to older Verbex versions.

Verbex dashboard checklist:

- [x] Update dashboard search API call to request only the enrichment that is displayed.
- [x] Update result tables if matched terms or document term stats are displayed.
- [x] Update result detail modals if per-term details are displayed.
- [x] Add empty states for older servers or non-enriched responses.
- [x] Avoid showing fields that the API does not return by default.
- [ ] Verify desktop and mobile search layouts after adding fields.

## Performance Requirements

- [x] Default search performs the same number of database calls as before.
- [x] Matched terms enrichment performs no additional database calls.
- [x] Term details enrichment performs no additional database calls.
- [x] Document term stats enrichment performs exactly one additional grouped database call.
- [x] No enrichment path performs one database call per result.
- [x] No enrichment path loads full term position arrays unless explicitly requested by a future option.
- [x] Stats query must be limited to result document IDs after `MaxResults` has been applied.
- [x] Stats query must return aggregate rows only.
- [x] Ensure query plans use the existing document-term table indexes.

Performance validation:

- [ ] Measure default search before and after the change.
- [ ] Measure enriched search with matched terms only.
- [ ] Measure enriched search with term details only.
- [ ] Measure enriched search with document term stats.
- [ ] Measure wildcard search with document term stats.
- [ ] Test with result sizes 1, 25, 100, and the configured max.
- [ ] Confirm no N+1 query pattern in logs/traces.

## Database Index Review

Review document-term table definitions for all providers:

- [ ] Confirm there is an index on `document_id`.
- [ ] Confirm there is an index that supports `document_id IN (...) GROUP BY document_id`.
- [ ] Add provider-specific migration/table-creation updates if needed.
- [ ] Avoid adding duplicate indexes if equivalent indexes already exist.

Suggested index if missing:

```sql
CREATE INDEX idx_{prefix}_document_terms_document_id
ON {prefix}_document_terms (document_id);
```

Provider checklist:

- [ ] PostgreSQL table creation reviewed.
- [ ] MySQL table creation reviewed.
- [ ] SQLite table creation reviewed.
- [ ] SQL Server table creation reviewed.

## Tests

Core tests:

- [ ] Add tests for default search shape remaining unchanged.
- [ ] Add tests for `IncludeMatchedTerms`.
- [ ] Add tests for `IncludeTermDetails`.
- [ ] Add tests for `IncludeDocumentTermStats`.
- [ ] Add tests for all flags together.
- [ ] Add tests for wildcard search with enrichment flags.
- [ ] Add tests for label-filtered enriched search.
- [ ] Add tests for tag-filtered enriched search.
- [ ] Add tests for no results.
- [ ] Add tests for query terms not in the index.

Provider tests:

- [ ] PostgreSQL `GetStatsByDocumentsAsync`.
- [ ] MySQL `GetStatsByDocumentsAsync`.
- [ ] SQLite `GetStatsByDocumentsAsync`.
- [ ] SQL Server `GetStatsByDocumentsAsync`.
- [ ] Empty input returns empty dictionary.
- [ ] Duplicate document IDs do not duplicate output.
- [ ] Unknown document IDs are omitted or return no stats, as documented.
- [ ] Term frequency sums match indexed content.

REST tests:

- [ ] Default REST search response contains no enriched fields.
- [ ] Enriched REST search response contains requested fields.
- [ ] Requesting only matched terms does not include stats.
- [ ] Requesting only stats does not include term details unless explicitly requested.
- [ ] OpenAPI schema includes optional enrichment fields.

SDK tests:

- [ ] C# SDK default search works.
- [ ] C# SDK enriched search works.
- [ ] JS SDK default search works.
- [ ] JS SDK enriched search works.
- [ ] Python SDK default search works.
- [ ] Python SDK enriched search works.

Compatibility tests:

- [ ] Existing test harnesses pass without setting enrichment flags.
- [ ] Existing clients can deserialize default search response.
- [ ] A strict default-shape test confirms no new fields are emitted by default.
- [ ] Enriched response remains additive and contains all legacy fields.

## Documentation Examples

Default request:

```json
{
  "Query": "botox",
  "MaxResults": 25
}
```

Enriched matched-terms request:

```json
{
  "Query": "botox",
  "MaxResults": 25,
  "IncludeMatchedTerms": true,
  "IncludeTermDetails": true
}
```

Term stats request:

```json
{
  "Query": "botox",
  "MaxResults": 25,
  "IncludeDocumentTermStats": true
}
```

Full enrichment request:

```json
{
  "Query": "botox",
  "MaxResults": 25,
  "UseAndLogic": false,
  "Labels": ["medical"],
  "Tags": {
    "source": "upload"
  },
  "IncludeMatchedTerms": true,
  "IncludeTermDetails": true,
  "IncludeDocumentTermStats": true
}
```

Wildcard stats request:

```json
{
  "Query": "*",
  "MaxResults": 25,
  "IncludeDocumentTermStats": true
}
```

## Release and Distribution Work

Complete this section if the feature is shipped in a tagged release or published artifact.

- [ ] Confirm target release version.
- [ ] Update server package/project metadata if versioned in source.
- [ ] Update dashboard package metadata if versioned in source.
- [ ] Update C# SDK package metadata.
- [ ] Update JS SDK package metadata.
- [ ] Update Python SDK package metadata if present.
- [ ] Update Docker image tags or release notes if images are published.
- [ ] Update `CHANGELOG.md` with compatibility, API, SDK, and performance notes.
- [ ] Confirm release notes mention that enrichment flags are opt-in and additive.
- [ ] Confirm no migration is required unless database index additions are introduced.
- [ ] If database index additions are introduced, document migration/backfill behavior.

## Validation Matrix

Build and test:

- [x] Build core library.
- [x] Build server.
- [x] Build MCP project.
- [x] Build CLI.
- [x] Build dashboard.
- [x] Build C# SDK.
- [ ] Run C# SDK test harness.
- [ ] Run JS SDK test harness.
- [ ] Run Python SDK tests or smoke script.

Runtime smoke tests:

- [ ] REST default search against a running server.
- [ ] REST enriched matched terms search.
- [ ] REST enriched term details search.
- [ ] REST enriched document term stats search.
- [ ] REST wildcard stats search.
- [ ] MCP default search.
- [ ] MCP enriched search.
- [ ] CLI default search.
- [ ] CLI enriched search if CLI flags are implemented.
- [ ] Dashboard search default/enriched path.
- [ ] Postman collection import and run.

Provider validation:

- [ ] PostgreSQL.
- [ ] MySQL.
- [x] SQLite.
- [ ] SQL Server.

## Implementation Sequence

Phase 1: Contract and server request model

- [x] Add enrichment flags to `SearchRequest`.
- [x] Add validation if any flag combination needs bounds.
- [x] Update OpenAPI request schema.
- [x] Add REST docs for request flags.

Phase 2: Result projection

- [x] Add helper to determine if enrichment is requested.
- [x] Add helper to project a legacy result into an enriched result object.
- [x] Preserve default route behavior when no flags are set.
- [x] Add matched terms projection.
- [x] Add term details projection.
- [ ] Add route-level tests.

Phase 3: Term stats provider method

- [x] Add `DocumentTermStats` model.
- [x] Add `GetStatsByDocumentsAsync` to `IDocumentTermMethods`.
- [x] Implement PostgreSQL provider method.
- [x] Implement MySQL provider method.
- [x] Implement SQLite provider method.
- [x] Implement SQL Server provider method.
- [ ] Add provider tests.

Phase 4: Core access method

- [x] Add `InvertedIndex.GetDocumentTermStatsAsync(...)` if route should not access driver internals.
- [x] Validate state with `ThrowIfDisposed()` and `ThrowIfNotOpen()`.
- [x] Delegate to provider method.
- [x] Add core tests.

Phase 5: REST stats enrichment

- [x] In search route, collect result document IDs.
- [x] Call stats method only when `IncludeDocumentTermStats` is true.
- [x] Attach `DocumentTermStats` to each result when present.
- [x] Attach zero-value stats only if that behavior is explicitly documented; otherwise omit missing stats.
- [x] Add timing fields.
- [ ] Add REST tests.

Phase 6: OpenAPI, generated metadata, and API Explorer

- [x] Update response schemas.
- [x] Update timing schema.
- [x] Update generated OpenAPI/swagger metadata.
- [ ] Confirm API Explorer displays the new request flags.
- [ ] Confirm request history can display enriched request and response bodies.

Phase 7: Markdown documentation

- [x] Update `REST_API.md`.
- [x] Update `README.md` search examples.
- [x] Update `SEARCH_PERFORMANCE.md` with the optional stats query behavior.
- [x] Update `SCORING.md` if term details become part of documented scoring behavior.
- [x] Update `VBX_CLI.md` if CLI behavior changes.
- [ ] Update `DOCKER.md` if deployment or validation changes.
- [x] Update SDK readmes.
- [x] Update `CHANGELOG.md`.
- [x] Annotate `SEARCH_ENRICHMENT.md` progress.

Phase 8: MCP

- [x] Update MCP search tool parameters.
- [x] Preserve defaults.
- [x] Return enriched fields when requested.
- [ ] Add MCP validation/manual test notes.

Phase 9: CLI

- [x] Decide whether CLI search should expose enrichment flags.
- [x] Preserve current CLI output by default.
- [x] Implement CLI flags if enriched fields are displayed.
- [x] Update CLI docs and validation.

Phase 10: SDKs

- [x] Update C# SDK request and result models.
- [x] Update C# SDK client overload/options.
- [x] Update JS SDK request and result models.
- [x] Update Python SDK request and result models.
- [ ] Update SDK package metadata if this ships in a release.
- [ ] Update SDK test harnesses.

Phase 11: Dashboard and UI clients

- [x] Update Verbex dashboard search requests where enriched fields are displayed.
- [x] Update Verbex dashboard result table/detail rendering as needed.
- [x] Confirm dashboard fallbacks for older/non-enriched responses.
- [x] Confirm AssistantHub integration guidance remains accurate.

Phase 12: Postman

- [x] Add enriched request examples.
- [x] Add wildcard stats example.
- [x] Add labels/tags enriched example.
- [ ] Validate collection import and request execution.

Phase 13: Release and distribution

- [ ] Confirm target release version.
- [ ] Update package/image metadata where applicable.
- [ ] Update release notes.
- [ ] Document migration behavior if database indexes are added.

Phase 14: Final validation

- [x] Build Verbex solution.
- [x] Build dashboard.
- [x] Build MCP project.
- [x] Build CLI.
- [x] Build SDKs.
- [x] Run unit tests. Executed SQLite RAM/on-disk suite; all tests passed.
- [ ] Run provider tests where infrastructure is available.
- [ ] Run REST smoke tests.
- [ ] Run MCP smoke tests.
- [ ] Run CLI smoke tests.
- [ ] Run SDK test harnesses.
- [ ] Run dashboard smoke tests.
- [ ] Validate Postman collection.
- [x] Confirm default response is unchanged.
- [x] Confirm enriched response is additive.
- [ ] Confirm AssistantHub can use enriched fields without fallback code for new Verbex versions.

## Acceptance Criteria

- [x] A default search request produces the same response fields as before.
- [x] A search with `IncludeMatchedTerms = true` returns `MatchedTerms` for each result.
- [x] A search with `IncludeTermDetails = true` returns `TermDetails` for each result.
- [x] A search with `IncludeDocumentTermStats = true` returns `DocumentTermStats` for each result with stats available.
- [x] `MatchedTerms` and `TermDetails` do not add database calls.
- [x] `DocumentTermStats` adds one grouped database call at most.
- [x] Wildcard search supports `DocumentTermStats`.
- [x] OpenAPI accurately reflects legacy and optional enriched fields.
- [x] REST docs, markdown docs, MCP docs, CLI docs, SDK docs, and Postman are updated.
- [ ] SDK package metadata and examples are updated where applicable.
- [ ] Verbex dashboard and API Explorer are updated or explicitly marked not impacted.
- [ ] Release notes and distribution metadata are updated where applicable.
- [x] All supported database providers implement the stats method.
- [ ] Tests and smoke checks prove backward compatibility and additive enrichment across REST, MCP, CLI, SDKs, dashboard, and Postman.

## Developer Notes

- [ ] Prefer server-side projection over modifying the core domain `SearchResult` serialization.
- [ ] Keep enrichment field names PascalCase in REST responses to match current server output.
- [ ] Let SDKs map to their idiomatic casing.
- [ ] AssistantHub should request only the enrichment it displays.
- [ ] If total terms are displayed in a table, use `DocumentTermStats.UniqueTermCount`.
- [ ] If total term occurrences are displayed, use `DocumentTermStats.TotalTermOccurrences`.
- [ ] If matched terms are displayed, use `MatchedTerms`, with fallback to `TermScores` keys for older servers.
- [ ] If term details are displayed, use `TermDetails`, with fallback to `TermScores` plus `TermFrequencies` for older servers.

## Progress Log

Use this section for dated implementation notes.

- [x] 2026-06-06: Implemented opt-in search result enrichment through core, REST, all database providers, MCP, CLI, dashboard, C# SDK, JavaScript SDK, Python SDK, OpenAPI schemas, REST/SDK/CLI/MCP docs, Postman examples, and changelog. Added focused core tests for document term stats and wildcard stats. Built the .NET solution and dashboard successfully; runtime REST/MCP/CLI/SDK/Postman smoke tests remain open.
- [x] 2026-06-06: Verification completed for `dotnet build src\Verbex.sln --no-restore`, dashboard production build, JavaScript syntax check, Python bytecode compile, Postman JSON parsing, and `git diff --check`. SQLite RAM/on-disk tests run; after correcting enumeration pagination semantics, all 263 tests passed.
