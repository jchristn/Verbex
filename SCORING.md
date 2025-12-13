# Verbex Search and Scoring

This document provides an exhaustive explanation of how search and relevance scoring work in Verbex.

## Table of Contents

- [Overview](#overview)
- [Search Pipeline](#search-pipeline)
- [Tokenization](#tokenization)
- [Token Processing](#token-processing)
- [Index Structure](#index-structure)
- [TF-IDF Scoring Algorithm](#tf-idf-scoring-algorithm)
- [Score Normalization](#score-normalization)
- [Search Modes](#search-modes)
- [Metadata Filtering](#metadata-filtering)
- [Understanding Score Values](#understanding-score-values)
- [Practical Examples](#practical-examples)
- [Tuning Search Results](#tuning-search-results)

## Overview

Verbex uses a **TF-IDF (Term Frequency-Inverse Document Frequency)** scoring algorithm with **max-normalization** to rank search results. This is a well-established information retrieval technique that balances:

1. **How often a term appears in a document** (term frequency)
2. **How rare a term is across all documents** (inverse document frequency)

The final scores are normalized to a 0.0-1.0 range, where 1.0 represents the most relevant document in the result set.

## Search Pipeline

When you execute a search query, Verbex processes it through the following pipeline:

```
Query String
    │
    ▼
┌─────────────────────────┐
│   1. Tokenization       │  Split query into individual terms
└─────────────────────────┘
    │
    ▼
┌─────────────────────────┐
│   2. Token Processing   │  Lowercase, filter, lemmatize
└─────────────────────────┘
    │
    ▼
┌─────────────────────────┐
│   3. Deduplication      │  Remove duplicate terms
└─────────────────────────┘
    │
    ▼
┌─────────────────────────┐
│   4. Document Retrieval │  Find matching documents via SQL
│      with Filtering     │  JOINs (includes label/tag filters)
└─────────────────────────┘
    │
    ▼
┌─────────────────────────┐
│   5. TF-IDF Scoring     │  Calculate relevance scores
└─────────────────────────┘
    │
    ▼
┌─────────────────────────┐
│   6. Ranking & Limit    │  Sort by score, apply maxResults
└─────────────────────────┘
    │
    ▼
┌─────────────────────────┐
│   7. Normalization      │  Scale scores to 0.0-1.0
└─────────────────────────┘
    │
    ▼
Search Results
```

**Note:** Metadata filtering (labels and tags) is integrated into the document retrieval step using SQL JOINs, not applied as a separate post-processing step. This approach ensures optimal performance by filtering at the database level.

## Tokenization

The first step splits the query string into individual tokens (words).

### Default Tokenizer

The `DefaultTokenizer` splits text on these separator characters:

```
Space ( ), Tab (\t), Newline (\n), Carriage Return (\r),
Period (.), Comma (,), Semicolon (;), Colon (:),
Exclamation (!), Question (?), Parentheses (()),
Brackets ([]), Braces ({}), Quotes (" ')
```

### Example

```
Input:  "Hello, world! How are you?"
Output: ["hello", "world", "how", "are", "you"]
```

Note: Tokenization converts all text to lowercase.

## Token Processing

After tokenization, each token is processed through several optional filters:

### 1. Case Normalization

All tokens are converted to lowercase:
```
"Machine" → "machine"
"LEARNING" → "learning"
```

### 2. Token Length Filtering

Tokens can be filtered by length (configurable via `MinTokenLength` and `MaxTokenLength`):

```csharp
// Example configuration
MinTokenLength = 2   // Tokens shorter than 2 chars are removed
MaxTokenLength = 50  // Tokens longer than 50 chars are removed
```

### 3. Stop Word Removal

Common words that add little search value can be removed (when `StopWordRemover` is configured):

```
Removed: "the", "a", "an", "and", "or", "but", "in", "on", "at", "to", "for", ...
Kept:    "machine", "learning", "algorithm", "data", ...
```

### 4. Lemmatization

Words can be reduced to their base form (when `Lemmatizer` is configured):

```
"running" → "run"
"machines" → "machine"
"better" → "good"
```

### Processing Example

```
Original Query: "The running machines are working!"

After Tokenization:    ["the", "running", "machines", "are", "working"]
After Stop Words:      ["running", "machines", "working"]
After Lemmatization:   ["run", "machine", "work"]
After Deduplication:   ["run", "machine", "work"]
```

## Index Structure

Verbex maintains an inverted index with the following structure:

### Posting List

For each unique term in the index, there is a **PostingList** containing:

- **Term**: The indexed term (lowercase)
- **Document Frequency**: Number of documents containing this term
- **Postings**: List of document occurrences

### Posting (Document-Term Mapping)

Each posting represents a term's occurrence in a specific document:

- **Document ID**: K-sortable unique ID identifying the document
- **Term Frequency**: How many times the term appears in this document
- **Character Positions**: JSON array of absolute byte offsets where the term appears
- **Term Positions**: JSON array of word indices (0-based) where the term appears

### Example Index Structure

```
Index:
├── "machine"
│   └── PostingList (DocumentFrequency: 3)
│       ├── Posting: doc_01JFXA... (TermFrequency: 5)
│       │   ├── CharacterPositions: [0, 45, 120, 305, 512]
│       │   └── TermPositions: [0, 8, 22, 55, 93]
│       ├── Posting: doc_01JFXB... (TermFrequency: 2)
│       │   ├── CharacterPositions: [15, 340]
│       │   └── TermPositions: [3, 67]
│       └── Posting: doc_01JFXC... (TermFrequency: 1)
│           ├── CharacterPositions: [88]
│           └── TermPositions: [23]
│
├── "learning"
│   └── PostingList (DocumentFrequency: 2)
│       ├── Posting: doc_01JFXA... (TermFrequency: 3)
│       │   ├── CharacterPositions: [8, 180, 515]
│       │   └── TermPositions: [1, 33, 94]
│       └── Posting: doc_01JFXB... (TermFrequency: 1)
│           ├── CharacterPositions: [20]
│           └── TermPositions: [4]
│
└── "algorithm"
    └── PostingList (DocumentFrequency: 1)
        └── Posting: doc_01JFXA... (TermFrequency: 2)
            ├── CharacterPositions: [60, 400]
            └── TermPositions: [11, 73]
```

**Position Types:**
- **CharacterPositions**: Byte offsets from the start of the document content (useful for highlighting exact text spans)
- **TermPositions**: Word indices based on tokenization (useful for phrase proximity calculations)

## TF-IDF Scoring Algorithm

Verbex calculates relevance scores using TF-IDF with logarithmic scaling and smoothing.

### Term Frequency (TF)

Term frequency measures how often a term appears in a document. Verbex uses **logarithmic TF** to prevent high-frequency terms from dominating:

```
TF = log(1 + termFrequency)
```

| Term Frequency | TF Score |
|---------------|----------|
| 1             | 0.693    |
| 2             | 1.099    |
| 5             | 1.792    |
| 10            | 2.398    |
| 100           | 4.615    |

The logarithm ensures that a term appearing 100 times isn't 100x more important than appearing once.

### Inverse Document Frequency (IDF)

IDF measures how rare a term is across all documents. Rare terms are more discriminating and receive higher weights:

```
IDF = log((totalDocuments + 1) / (documentsWithTerm + 1))
```

The "+1" smoothing prevents:
- Division by zero when a term appears in no documents
- Zero IDF when a term appears in all documents

| Total Docs | Docs with Term | IDF Score |
|------------|----------------|-----------|
| 1000       | 1              | 6.908     |
| 1000       | 10             | 4.505     |
| 1000       | 100            | 2.303     |
| 1000       | 500            | 0.693     |
| 1000       | 1000           | 0.000     |

### Combined TF-IDF Score

For each term-document pair:

```
TF-IDF = TF × IDF = log(1 + termFrequency) × log((totalDocs + 1) / (docsWithTerm + 1))
```

### Multi-Term Queries

For queries with multiple terms, scores are accumulated:

```
DocumentScore = Σ TF-IDF(term, document) for all query terms
```

Each matching term contributes its TF-IDF score to the document's total.

### Scoring Example

Consider an index with 1000 documents:

**Query:** "machine learning"

**Document A:**
- "machine": appears 5 times, in 100 documents total
  - TF = log(1 + 5) = 1.792
  - IDF = log(1001 / 101) = 2.294
  - TF-IDF = 1.792 × 2.294 = 4.111

- "learning": appears 3 times, in 50 documents total
  - TF = log(1 + 3) = 1.386
  - IDF = log(1001 / 51) = 2.977
  - TF-IDF = 1.386 × 2.977 = 4.127

- **Total Score = 4.111 + 4.127 = 8.238**

**Document B:**
- "machine": appears 1 time, in 100 documents total
  - TF = log(1 + 1) = 0.693
  - IDF = log(1001 / 101) = 2.294
  - TF-IDF = 0.693 × 2.294 = 1.590

- "learning": not present
  - TF-IDF = 0

- **Total Score = 1.590 + 0 = 1.590**

## Score Normalization

After calculating raw TF-IDF scores, Verbex normalizes all scores to a 0.0-1.0 range using **max-normalization**:

```
NormalizedScore = RawScore / MaxScore
```

Where `MaxScore` is the highest raw score among all results.

### Normalization Properties

1. **The best match always scores 1.0** - The document with the highest relevance
2. **Other scores are relative** - A score of 0.5 means half as relevant as the best match
3. **Scores are comparable within a query** - But not across different queries

### Example

| Document | Raw Score | Normalized Score |
|----------|-----------|------------------|
| Doc-A    | 8.238     | 1.000           |
| Doc-B    | 1.590     | 0.193           |
| Doc-C    | 4.500     | 0.546           |

### Individual Term Score Normalization

Verbex also normalizes individual term scores proportionally:

```
NormalizedTermScore = RawTermScore / MaxDocumentScore
```

This allows you to see each term's contribution to the overall relevance.

## Search Modes

### OR Logic (Default)

Returns documents containing **any** of the query terms:

```csharp
// Search: "machine learning"
// Returns: documents with "machine" OR "learning" OR both
var results = await index.SearchAsync("machine learning");
```

Documents are ranked by their total TF-IDF score across all matching terms.

### AND Logic

Returns only documents containing **all** query terms:

```csharp
// Search: "machine learning"
// Returns: only documents with BOTH "machine" AND "learning"
var results = await index.SearchAsync("machine learning", useAndLogic: true);
```

The AND filter is applied after scoring, so documents still have meaningful relevance scores based on term frequencies.

## Metadata Filtering

Results can be filtered by document labels and tags:

```csharp
// Filter by labels (AND logic - document must have ALL specified labels)
List<string> labels = new List<string> { "important", "reviewed" };

// Filter by tags (AND logic - document must have ALL specified key-value pairs)
Dictionary<string, string> tags = new Dictionary<string, string>
{
    { "category", "technology" },
    { "year", "2024" }
};

var results = await index.SearchAsync("machine learning", labels: labels, tags: tags);
```

**Important:** Metadata filtering is integrated into the document retrieval step using SQL JOINs, not applied as a post-processing step. This ensures optimal performance by filtering at the database level before documents are retrieved and scored.

## Understanding Score Values

### Why All Scores Might Be 1.0

If all your search results show a score of 1.0, it's likely because:

1. **Single Result**: Only one document matches, so it's automatically the best match
2. **Identical Relevance**: All matching documents have the same TF-IDF scores
3. **Single Occurrence**: Each query term appears exactly once in each matching document

### Getting Varied Scores

To see differentiated scores:

1. **Add more documents** with varying content
2. **Use terms with different frequencies** across documents
3. **Search for multiple terms** to create score variation

### Score Interpretation Guide

| Score Range | Interpretation |
|-------------|----------------|
| 0.9 - 1.0   | Highly relevant - strong term matches |
| 0.7 - 0.9   | Very relevant - good term coverage |
| 0.5 - 0.7   | Moderately relevant - partial matches |
| 0.3 - 0.5   | Somewhat relevant - limited matches |
| 0.0 - 0.3   | Marginally relevant - few term matches |

## Practical Examples

### Example 1: Basic Differentiation

**Documents:**
```
Doc-1: "The quick brown fox jumps over the lazy dog"
Doc-2: "A quick fox runs fast"
Doc-3: "The lazy dog sleeps all day"
```

**Query:** "quick fox"

**Results:**
| Document | Matched Terms | Approx Score |
|----------|---------------|--------------|
| Doc-1    | quick, fox    | 1.00         |
| Doc-2    | quick, fox    | ~0.90        |
| Doc-3    | (none)        | N/A          |

Doc-1 and Doc-2 both match both terms, but Doc-1 may score slightly higher due to document length normalization effects.

### Example 2: Term Frequency Impact

**Documents:**
```
Doc-1: "machine machine machine learning learning"  (machine: 3x, learning: 2x)
Doc-2: "machine learning"                           (machine: 1x, learning: 1x)
Doc-3: "deep learning neural networks"              (learning: 1x)
```

**Query:** "machine learning"

**Results:**
| Document | machine TF | learning TF | Approx Score |
|----------|------------|-------------|--------------|
| Doc-1    | log(4)=1.39| log(3)=1.10 | 1.00         |
| Doc-2    | log(2)=0.69| log(2)=0.69 | ~0.55        |
| Doc-3    | 0          | log(2)=0.69 | ~0.28        |

### Example 3: Rare Terms Boost

**Documents (1000 total in index):**
```
Doc-1: "quantum computing algorithms"    (quantum: rare, computing: common)
Doc-2: "cloud computing services"        (computing: common)
```

**Query:** "quantum computing"

**Results:**
- Doc-1 scores much higher because "quantum" is rare (high IDF)
- Doc-2 only matches "computing" which has low IDF due to commonality

## Tuning Search Results

### Configuration Options

```csharp
var config = new VerbexConfiguration
{
    // Storage
    StorageMode = StorageMode.OnDisk,     // InMemory or OnDisk
    StorageDirectory = "/path/to/index",   // Directory for OnDisk mode

    // Text Processing
    Lemmatizer = new BasicLemmatizer(),    // Enable lemmatization
    StopWordRemover = new BasicStopWordRemover(), // Enable stop word removal
    MinTokenLength = 2,                    // Ignore very short tokens (0 = disabled)
    MaxTokenLength = 50,                   // Ignore very long tokens (0 = disabled)

    // Search Tuning
    DefaultMaxSearchResults = 100,         // Default result limit
    PhraseSearchBonus = 2.0,               // Multiplier for phrase matches
    SigmoidNormalizationDivisor = 10.0     // Score normalization factor
};
```

### Improving Search Quality

1. **Enable Lemmatization** - Matches "running" with "run", "runs", etc.
2. **Enable Stop Word Removal** - Focuses on meaningful terms
3. **Adjust Token Length** - Filter out noise from very short/long tokens
4. **Use AND Logic** - When precision matters more than recall
5. **Add Metadata** - Enable filtering by categories, dates, etc.

### Performance Considerations

- **SQLite WAL Mode**: Write-ahead logging for concurrent reads during writes
- **Thread Safety**: `ReaderWriterLockSlim` optimizes read-heavy workloads
- **Index Coverage**: Database indices on frequently queried columns
- **Metadata JOINs**: Label/tag filtering integrated into document retrieval queries

---

## Summary

Verbex's search scoring:

1. **Tokenizes** the query into individual terms
2. **Processes** tokens (lowercase, filter, lemmatize)
3. **Looks up** posting lists for each term
4. **Calculates** TF-IDF scores: `log(1 + TF) × log((N+1)/(df+1))`
5. **Accumulates** scores across all query terms
6. **Normalizes** to 0.0-1.0 range (best match = 1.0)
7. **Returns** ranked results

The algorithm rewards:
- Documents with higher term frequencies (logarithmically scaled)
- Matches on rare/discriminating terms
- Coverage of multiple query terms

For questions or issues, see the [main documentation](README.md).
