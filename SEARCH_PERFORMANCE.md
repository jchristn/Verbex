# Search Performance Analysis & Optimization Plan

**Issue**: Search API taking 30+ seconds on 250K document index (should be <500ms)
**Database**: MySQL
**Date**: 2026-02-02

---

## Executive Summary

A search request executes **5-6 sequential database queries**. With 250K documents and potentially millions of rows in `document_terms`, missing indexes or poor query plans cause catastrophic performance degradation.

**Target**: Sub-500ms search response time
**Current**: 29,000-55,000ms

---

## Complete Search Execution Path

```
HTTP POST /v1.0/indices/{id}/search
    │
    ▼
┌─────────────────────────────────────────────────────────────────┐
│ 1. REQUEST PARSING (RestServiceHandler.cs:2315-2387)            │
│    - Deserialize JSON body                                       │
│    - Extract Query, MaxResults, Labels, Tags                     │
│    - Estimated time: <1ms                                        │
└─────────────────────────────────────────────────────────────────┘
    │
    ▼
┌─────────────────────────────────────────────────────────────────┐
│ 2. TOKENIZATION (InvertedIndex.cs:907)                          │
│    - TokenizeAndProcess(query)                                   │
│    - Apply stopwords, lemmatization, length filters             │
│    - "san jose" → ["san", "jose"]                               │
│    - Estimated time: <5ms                                        │
└─────────────────────────────────────────────────────────────────┘
    │
    ▼
┌─────────────────────────────────────────────────────────────────┐
│ 3. TERM LOOKUP - DB QUERY #1 (InvertedIndex.cs:939)             │
│    Terms.GetMultipleAsync()                                      │
│                                                                  │
│    SELECT id, term, document_frequency, total_frequency          │
│    FROM default_terms                                            │
│    WHERE term IN ('san', 'jose');                                │
│                                                                  │
│    Index needed: idx_default_terms_term (term)                   │
│    Estimated time: 5-10ms (with index)                           │
└─────────────────────────────────────────────────────────────────┘
    │
    ▼
┌─────────────────────────────────────────────────────────────────┐
│ 4. MAIN SEARCH - DB QUERY #2 (DocumentTermMethods.cs:108-170)   │
│    DocumentTerms.SearchAsync()                                   │
│                                                                  │
│    SELECT dt.document_id,                                        │
│           SUM(dt.term_frequency) as total_frequency,             │
│           COUNT(DISTINCT dt.term_id) as term_count               │
│    FROM default_document_terms dt                                │
│    WHERE dt.term_id IN ('term_id_1', 'term_id_2')               │
│      AND dt.document_id IN (                                     │
│        SELECT document_id FROM default_tags                      │
│        WHERE `key` = 'UserMasterGUID' AND value = 'joel'        │
│      )                                                           │
│    GROUP BY dt.document_id                                       │
│    ORDER BY total_frequency DESC                                 │
│    LIMIT 25;                                                     │
│                                                                  │
│    Indexes needed:                                               │
│      - idx_default_docterms_term (term_id)                       │
│      - idx_default_tags_key_value (`key`, value)                 │
│                                                                  │
│    *** THIS IS THE PRIMARY BOTTLENECK ***                        │
│    Estimated time: 50-200ms (with indexes), 10-30 SECONDS (w/o)  │
└─────────────────────────────────────────────────────────────────┘
    │
    ▼
┌─────────────────────────────────────────────────────────────────┐
│ 5. TERM FREQUENCIES - DB QUERY #3 (InvertedIndex.cs:973)        │
│    DocumentTerms.GetByDocumentsAndTermsAsync()                   │
│                                                                  │
│    SELECT dt.*, t.term                                           │
│    FROM default_document_terms dt                                │
│    JOIN default_terms t ON dt.term_id = t.id                     │
│    WHERE dt.document_id IN ('doc1', 'doc2', ...)                │
│      AND dt.term_id IN ('term1', 'term2');                      │
│                                                                  │
│    Index needed: idx_default_docterms_doc_term (document_id, term_id) │
│    Estimated time: 20-50ms                                       │
└─────────────────────────────────────────────────────────────────┘
    │
    ▼
┌─────────────────────────────────────────────────────────────────┐
│ 6. DOCUMENT METADATA - DB QUERY #4 (InvertedIndex.cs:996)       │
│    Documents.GetByIdsAsync()                                     │
│                                                                  │
│    SELECT * FROM default_documents                               │
│    WHERE id IN ('doc1', 'doc2', ...);                           │
│                                                                  │
│    Index: PRIMARY KEY (always used)                              │
│    Estimated time: 10-20ms                                       │
└─────────────────────────────────────────────────────────────────┘
    │
    ▼
┌─────────────────────────────────────────────────────────────────┐
│ 7. DOCUMENT COUNT - DB QUERY #5 (InvertedIndex.cs:1000)         │
│    GetDocumentCountAsync() - CACHED after first call             │
│                                                                  │
│    SELECT COUNT(*) FROM default_documents;                       │
│                                                                  │
│    First request: 50-100ms                                       │
│    Subsequent: <1ms (cached)                                     │
└─────────────────────────────────────────────────────────────────┘
    │
    ▼
┌─────────────────────────────────────────────────────────────────┐
│ 8. SCORING (InvertedIndex.cs:1002-1015)                         │
│    - For each matched document:                                  │
│      - Calculate TF-IDF score                                    │
│      - TF = log(1 + term_frequency)                             │
│      - IDF = log((total_docs + 1) / (doc_frequency + 1))        │
│    - Sort by score descending                                    │
│    - Take top N results                                          │
│    Estimated time: 5-10ms (in-memory)                            │
└─────────────────────────────────────────────────────────────────┘
    │
    ▼
┌─────────────────────────────────────────────────────────────────┐
│ 9. RESPONSE SERIALIZATION (RestServiceHandler.cs:2369-2380)     │
│    - Serialize SearchResults to JSON                             │
│    Estimated time: 10-30ms                                       │
└─────────────────────────────────────────────────────────────────┘
```

---

## Optimization Checklist

### Phase 1: Database Index Verification (CRITICAL)

- [ ] **1.1 Verify all required indexes exist**
  ```sql
  SHOW INDEX FROM default_terms;
  SHOW INDEX FROM default_document_terms;
  SHOW INDEX FROM default_labels;
  SHOW INDEX FROM default_tags;
  SHOW INDEX FROM default_documents;
  ```

- [ ] **1.2 Required indexes for search performance**
  ```sql
  -- Terms table
  CREATE INDEX IF NOT EXISTS idx_default_terms_term
    ON default_terms(term);

  -- Document-terms table (CRITICAL for main search)
  CREATE INDEX IF NOT EXISTS idx_default_docterms_term
    ON default_document_terms(term_id);
  CREATE INDEX IF NOT EXISTS idx_default_docterms_term_doc
    ON default_document_terms(term_id, document_id);
  CREATE INDEX IF NOT EXISTS idx_default_docterms_doc_term
    ON default_document_terms(document_id, term_id);

  -- Labels table
  CREATE INDEX IF NOT EXISTS idx_default_labels_doc
    ON default_labels(document_id);
  CREATE INDEX IF NOT EXISTS idx_default_labels_label
    ON default_labels(label);

  -- Tags table (CRITICAL for tag filtering)
  CREATE INDEX IF NOT EXISTS idx_default_tags_doc
    ON default_tags(document_id);
  CREATE INDEX IF NOT EXISTS idx_default_tags_key_value
    ON default_tags(`key`, value(255));
  ```

- [ ] **1.3 Update table statistics**
  ```sql
  ANALYZE TABLE default_terms;
  ANALYZE TABLE default_document_terms;
  ANALYZE TABLE default_labels;
  ANALYZE TABLE default_tags;
  ANALYZE TABLE default_documents;
  ```

### Phase 2: Query Plan Analysis

- [ ] **2.1 Capture the actual query being executed**
  - Add logging to `DocumentTermMethods.SearchAsync()` to print the generated SQL
  - Or enable MySQL general query log temporarily

- [ ] **2.2 Run EXPLAIN on the main search query**
  ```sql
  EXPLAIN SELECT dt.document_id,
         SUM(dt.term_frequency) as total_frequency,
         COUNT(DISTINCT dt.term_id) as term_count
  FROM default_document_terms dt
  WHERE dt.term_id IN ('TERM_ID_1', 'TERM_ID_2')
    AND dt.document_id IN (
      SELECT document_id FROM default_tags
      WHERE `key` = 'UserMasterGUID' AND value = 'joel'
    )
  GROUP BY dt.document_id
  ORDER BY total_frequency DESC
  LIMIT 25;
  ```

  **Expected**: Should show `Using index` for the WHERE clause
  **Problem**: If shows `Using filesort` or `Using temporary` on large rowsets

- [ ] **2.3 Run EXPLAIN on tag subquery separately**
  ```sql
  EXPLAIN SELECT document_id FROM default_tags
  WHERE `key` = 'UserMasterGUID' AND value = 'joel';
  ```

  **Expected**: Should use `idx_default_tags_key_value`
  **Record row count returned**: ___________

- [ ] **2.4 Check table sizes**
  ```sql
  SELECT table_name, table_rows, data_length, index_length
  FROM information_schema.tables
  WHERE table_schema = DATABASE()
    AND table_name LIKE 'default_%';
  ```

### Phase 3: Profiling Individual Query Steps

- [ ] **3.1 Time the term lookup query**
  ```sql
  SET profiling = 1;
  SELECT id, term, document_frequency, total_frequency
  FROM default_terms
  WHERE term IN ('san', 'jose');
  SHOW PROFILES;
  ```
  **Record time**: ___________ms (should be <10ms)

- [ ] **3.2 Time the tag filter subquery alone**
  ```sql
  SELECT document_id FROM default_tags
  WHERE `key` = 'UserMasterGUID' AND value = 'joel';
  ```
  **Record time**: ___________ms
  **Record row count**: ___________

- [ ] **3.3 Time the main search WITHOUT tag filter**
  ```sql
  SELECT dt.document_id,
         SUM(dt.term_frequency) as total_frequency,
         COUNT(DISTINCT dt.term_id) as term_count
  FROM default_document_terms dt
  WHERE dt.term_id IN ('TERM_ID_1', 'TERM_ID_2')
  GROUP BY dt.document_id
  ORDER BY total_frequency DESC
  LIMIT 25;
  ```
  **Record time**: ___________ms

- [ ] **3.4 Time the main search WITH tag filter**
  ```sql
  SELECT dt.document_id,
         SUM(dt.term_frequency) as total_frequency,
         COUNT(DISTINCT dt.term_id) as term_count
  FROM default_document_terms dt
  WHERE dt.term_id IN ('TERM_ID_1', 'TERM_ID_2')
    AND dt.document_id IN (
      SELECT document_id FROM default_tags
      WHERE `key` = 'UserMasterGUID' AND value = 'joel'
    )
  GROUP BY dt.document_id
  ORDER BY total_frequency DESC
  LIMIT 25;
  ```
  **Record time**: ___________ms

### Phase 4: Alternative Query Strategies

If Phase 3 shows the tag subquery is fast but combined query is slow:

- [ ] **4.1 Try JOIN instead of IN subquery**
  ```sql
  SELECT dt.document_id,
         SUM(dt.term_frequency) as total_frequency,
         COUNT(DISTINCT dt.term_id) as term_count
  FROM default_document_terms dt
  INNER JOIN default_tags tg
    ON dt.document_id = tg.document_id
    AND tg.`key` = 'UserMasterGUID'
    AND tg.value = 'joel'
  WHERE dt.term_id IN ('TERM_ID_1', 'TERM_ID_2')
  GROUP BY dt.document_id
  ORDER BY total_frequency DESC
  LIMIT 25;
  ```
  **Record time**: ___________ms

- [ ] **4.2 Try reversing the query order (filter first, then search)**
  ```sql
  WITH filtered_docs AS (
    SELECT DISTINCT document_id
    FROM default_tags
    WHERE `key` = 'UserMasterGUID' AND value = 'joel'
  )
  SELECT dt.document_id,
         SUM(dt.term_frequency) as total_frequency,
         COUNT(DISTINCT dt.term_id) as term_count
  FROM default_document_terms dt
  INNER JOIN filtered_docs fd ON dt.document_id = fd.document_id
  WHERE dt.term_id IN ('TERM_ID_1', 'TERM_ID_2')
  GROUP BY dt.document_id
  ORDER BY total_frequency DESC
  LIMIT 25;
  ```
  **Record time**: ___________ms

- [ ] **4.3 Try with FORCE INDEX hint**
  ```sql
  SELECT dt.document_id,
         SUM(dt.term_frequency) as total_frequency,
         COUNT(DISTINCT dt.term_id) as term_count
  FROM default_document_terms dt FORCE INDEX (idx_default_docterms_term)
  WHERE dt.term_id IN ('TERM_ID_1', 'TERM_ID_2')
    AND dt.document_id IN (
      SELECT document_id FROM default_tags
      WHERE `key` = 'UserMasterGUID' AND value = 'joel'
    )
  GROUP BY dt.document_id
  ORDER BY total_frequency DESC
  LIMIT 25;
  ```
  **Record time**: ___________ms

### Phase 5: Data Distribution Analysis

- [ ] **5.1 Check term frequency distribution**
  ```sql
  -- How common are the search terms?
  SELECT term, document_frequency, total_frequency
  FROM default_terms
  WHERE term IN ('san', 'jose');
  ```
  **Record**:
  - 'san' appears in _______ documents
  - 'jose' appears in _______ documents

- [ ] **5.2 Check tag filter selectivity**
  ```sql
  -- How many documents have this tag?
  SELECT COUNT(DISTINCT document_id)
  FROM default_tags
  WHERE `key` = 'UserMasterGUID' AND value = 'joel';
  ```
  **Record**: _______ documents match this tag

- [ ] **5.3 Check document_terms table size**
  ```sql
  SELECT COUNT(*) FROM default_document_terms;
  ```
  **Record**: _______ rows

- [ ] **5.4 Check average terms per document**
  ```sql
  SELECT AVG(term_count) FROM default_documents;
  ```
  **Record**: _______ terms per document

### Phase 6: Code-Level Optimizations

If database optimizations don't help sufficiently:

- [ ] **6.1 Add query timing instrumentation**
  - File: `src/Verbex/InvertedIndex.cs`
  - Add `Stopwatch` around each database call in `SearchAsync()`
  - Log individual query times to identify the slowest step

- [ ] **6.2 Consider pre-filtering approach**
  - If tag filter returns small result set (<1000 docs)
  - First query tags to get document IDs
  - Then search only within those document IDs
  - May require code change in `DocumentTermMethods.SearchAsync()`

- [ ] **6.3 Consider caching filtered document sets**
  - If same tag filter is used repeatedly
  - Cache the document IDs for that filter
  - Invalidate on tag changes

- [ ] **6.4 Consider search result caching**
  - Cache full search results for repeated queries
  - Key: hash of (query + filters + limit)
  - TTL: 60 seconds or until index modified

### Phase 7: Infrastructure Checks

- [ ] **7.1 Check MySQL buffer pool size**
  ```sql
  SHOW VARIABLES LIKE 'innodb_buffer_pool_size';
  ```
  **Record**: _______ bytes
  **Recommendation**: Should be at least 1GB for 250K doc index

- [ ] **7.2 Check if tables fit in buffer pool**
  ```sql
  SELECT SUM(data_length + index_length) / 1024 / 1024 AS total_mb
  FROM information_schema.tables
  WHERE table_schema = DATABASE()
    AND table_name LIKE 'default_%';
  ```
  **Record**: _______ MB

- [ ] **7.3 Check for table fragmentation**
  ```sql
  SELECT table_name, data_free
  FROM information_schema.tables
  WHERE table_schema = DATABASE()
    AND table_name LIKE 'default_%'
    AND data_free > 0;
  ```
  If high fragmentation, run: `OPTIMIZE TABLE default_document_terms;`

- [ ] **7.4 Check MySQL slow query log**
  ```sql
  SHOW VARIABLES LIKE 'slow_query_log%';
  SHOW VARIABLES LIKE 'long_query_time';
  ```

---

## Expected Results After Optimization

## Search Result Enrichment Cost

The REST enrichment flags are designed to preserve default search performance:

| Request Flags | Extra Database Work | Notes |
|---------------|---------------------|-------|
| none | none | Default response shape and database call count are unchanged. |
| `IncludeMatchedTerms` | none | Uses `TermScores` keys already populated during scoring. |
| `IncludeTermDetails` | none | Uses `TermScores` and `TermFrequencies` already populated during scoring. |
| `IncludeDocumentTermStats` | one grouped query | Aggregates only result document IDs after `MaxResults` is applied. |

The document stats query is:

```sql
SELECT document_id,
       COUNT(*) AS unique_term_count,
       COALESCE(SUM(term_frequency), 0) AS total_term_occurrences
FROM {prefix}_document_terms
WHERE document_id IN (...)
GROUP BY document_id;
```

It uses the existing document-term document ID indexes and does not load position arrays or full term lists.

| Query Step | Before | After |
|------------|--------|-------|
| Term lookup | 50-100ms | 5-10ms |
| Main search (no filter) | 10-30s | 50-200ms |
| Main search (with tag) | 10-30s | 100-300ms |
| Term frequencies | 100-500ms | 20-50ms |
| Document metadata | 20-50ms | 10-20ms |
| Document count | cached | <1ms |
| **Total** | **29,000ms** | **<500ms** |

---

## Progress Tracking

| Date | Phase | Task | Status | Notes |
|------|-------|------|--------|-------|
| | 1.1 | Verify indexes exist | | |
| | 1.2 | Create missing indexes | | |
| | 1.3 | Update statistics | | |
| | 2.2 | EXPLAIN main query | | |
| | 3.2 | Time tag subquery | | |
| | 3.4 | Time combined query | | |
| | | | | |

---

## Findings Log

Record your findings here as you work through the checklist:

### Index Verification Results
```
(paste SHOW INDEX results here)
```

### EXPLAIN Output
```
(paste EXPLAIN results here)
```

### Query Timing Results
```
(paste timing results here)
```

### Root Cause Identified
```
MySQL's query optimizer chooses wrong execution order for IN subqueries.
- Tag filter returns only 104 documents (0.04% of 287K)
- But MySQL scans 6.1M rows in document_terms FIRST, then filters by tag
- Should filter by tag first (104 docs), then search within those
- IN subqueries don't provide cardinality hints to the optimizer
```

### Solution Applied
```
Changed query structure from:
  SELECT ... FROM document_terms WHERE term_id IN (...)
  AND document_id IN (SELECT document_id FROM tags WHERE ...)

To:
  SELECT ... FROM (SELECT document_id FROM tags WHERE ...) AS filtered
  STRAIGHT_JOIN document_terms dt ON dt.document_id = filtered.document_id
  WHERE dt.term_id IN (...)

Key changes:
1. Execute tag/label filter as derived table FIRST
2. Use STRAIGHT_JOIN (MySQL) / OPTION (FORCE ORDER) (SQL Server) to force execution order
3. MySQL must materialize the derived table, then use it to drive the join
4. Result: Only ~104 * avg_terms_per_doc rows scanned instead of 6.1M
```
