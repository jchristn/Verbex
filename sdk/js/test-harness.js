#!/usr/bin/env node
/**
 * Verbex SDK Test Harness for JavaScript
 *
 * A comprehensive test suite that validates all Verbex API endpoints.
 * Runs as a command-line program with consistent output formatting.
 *
 * Usage:
 *     node test-harness.js <endpoint> <access_key>
 *
 * Example:
 *     node test-harness.js http://localhost:8080 verbexadmin
 */

const { VerbexClient, VerbexError } = require('./verbex-sdk.js');
const crypto = require('crypto');

/**
 * Test result class.
 */
class TestResult {
    constructor(name, passed, message = '', durationMs = 0) {
        this.name = name;
        this.passed = passed;
        this.message = message;
        this.durationMs = durationMs;
    }
}

/**
 * Test harness for Verbex SDK.
 */
class TestHarness {
    constructor(endpoint, accessKey) {
        this._endpoint = endpoint;
        this._accessKey = accessKey;
        this._client = null;
        this._testIndexId = `test-index-${crypto.randomBytes(4).toString('hex')}`;
        this._testDocuments = [];
        this._results = [];
        this._passed = 0;
        this._failed = 0;
    }

    _printHeader(text) {
        console.log();
        console.log('='.repeat(60));
        console.log(`  ${text}`);
        console.log('='.repeat(60));
    }

    _printSubheader(text) {
        console.log();
        console.log(`--- ${text} ---`);
    }

    _printResult(result) {
        const status = result.passed ? 'PASS' : 'FAIL';
        console.log(`  [${status}] ${result.name} (${result.durationMs.toFixed(2)}ms)`);
        if (result.message && !result.passed) {
            console.log(`         Error: ${result.message}`);
        }
    }

    async _runTest(name, testFn) {
        const startTime = Date.now();
        let result;
        try {
            await testFn();
            const durationMs = Date.now() - startTime;
            result = new TestResult(name, true, '', durationMs);
            this._passed++;
        } catch (error) {
            const durationMs = Date.now() - startTime;
            const message = error instanceof Error ? error.message : String(error);
            result = new TestResult(name, false, message, durationMs);
            this._failed++;
        }
        this._results.push(result);
        this._printResult(result);
        return result;
    }

    _assert(condition, message) {
        if (!condition) {
            throw new Error(message);
        }
    }

    _assertNotNull(value, fieldName) {
        this._assert(value !== null && value !== undefined, `${fieldName} should not be null`);
    }

    _assertEquals(actual, expected, fieldName) {
        this._assert(actual === expected, `${fieldName} expected '${expected}', got '${actual}'`);
    }

    _assertTrue(value, fieldName) {
        this._assert(value === true, `${fieldName} should be True`);
    }

    _assertFalse(value, fieldName) {
        this._assert(value === false, `${fieldName} should be False`);
    }

    _assertGreaterThan(actual, expected, fieldName) {
        this._assert(actual > expected, `${fieldName} expected > ${expected}, got ${actual}`);
    }

    // ==================== Health Tests ====================

    async testRootHealthCheck() {
        const response = await this._client.rootHealthCheck();
        this._assertTrue(response.success, 'response.success');
        this._assertEquals(response.statusCode, 200, 'response.statusCode');
        this._assertNotNull(response.data, 'response.data');
        this._assertEquals(response.data.status, 'Healthy', 'data.status');
        this._assertNotNull(response.data.version, 'data.version');
        this._assertNotNull(response.data.timestamp, 'data.timestamp');
    }

    async testHealthEndpoint() {
        const response = await this._client.healthCheck();
        this._assertTrue(response.success, 'response.success');
        this._assertEquals(response.statusCode, 200, 'response.statusCode');
        this._assertNotNull(response.data, 'response.data');
        this._assertEquals(response.data.status, 'Healthy', 'data.status');
        this._assertNotNull(response.data.version, 'data.version');
        this._assertNotNull(response.data.timestamp, 'data.timestamp');
    }

    // ==================== Authentication Tests ====================

    async testLoginSuccess() {
        const response = await this._client.login('admin', 'password');
        this._assertTrue(response.success, 'response.success');
        this._assertEquals(response.statusCode, 200, 'response.statusCode');
        this._assertNotNull(response.data, 'response.data');
        this._assertNotNull(response.data.token, 'data.token');
        this._assertEquals(response.data.username, 'admin', 'data.username');
    }

    async testLoginInvalidCredentials() {
        try {
            await this._client.login('invalid', 'invalid');
            this._assert(false, 'Should have thrown VerbexError');
        } catch (error) {
            if (!(error instanceof VerbexError)) throw error;
            this._assertEquals(error.statusCode, 401, 'error.statusCode');
            this._assertNotNull(error.message, 'error.message');
        }
    }

    async testValidateToken() {
        const response = await this._client.validateToken();
        this._assertTrue(response.success, 'response.success');
        this._assertEquals(response.statusCode, 200, 'response.statusCode');
        this._assertNotNull(response.data, 'response.data');
        this._assertTrue(response.data.valid, 'data.valid');
    }

    async testValidateInvalidToken() {
        const invalidClient = new VerbexClient(this._endpoint, 'invalid-token');
        try {
            await invalidClient.validateToken();
            this._assert(false, 'Should have thrown VerbexError');
        } catch (error) {
            if (!(error instanceof VerbexError)) throw error;
            this._assertEquals(error.statusCode, 401, 'error.statusCode');
        }
    }

    // ==================== Index Management Tests ====================

    async testListIndicesInitial() {
        const response = await this._client.listIndices();
        this._assertTrue(response.success, 'response.success');
        this._assertEquals(response.statusCode, 200, 'response.statusCode');
        this._assertNotNull(response.data, 'response.data');
        this._assertNotNull(response.data.indices, 'data.indices');
        this._assertNotNull(response.data.count, 'data.count');
    }

    async testCreateIndex() {
        const response = await this._client.createIndex({
            id: this._testIndexId,
            name: 'Test Index',
            description: 'A test index for SDK validation',
            inMemory: true,
            storageMode: 'MemoryOnly'
        });
        this._assertTrue(response.success, 'response.success');
        this._assertEquals(response.statusCode, 201, 'response.statusCode');
        this._assertNotNull(response.data, 'response.data');
        this._assertNotNull(response.data.message, 'data.message');
        this._assertNotNull(response.data.index, 'data.index');
        this._assertEquals(response.data.index.id, this._testIndexId, 'index.id');
        this._assertEquals(response.data.index.name, 'Test Index', 'index.name');
    }

    async testCreateDuplicateIndex() {
        try {
            await this._client.createIndex({ id: this._testIndexId, name: 'Duplicate' });
            this._assert(false, 'Should have thrown VerbexError for duplicate');
        } catch (error) {
            if (!(error instanceof VerbexError)) throw error;
            this._assertEquals(error.statusCode, 409, 'error.statusCode');
        }
    }

    async testGetIndex() {
        const response = await this._client.getIndex(this._testIndexId);
        this._assertTrue(response.success, 'response.success');
        this._assertEquals(response.statusCode, 200, 'response.statusCode');
        this._assertNotNull(response.data, 'response.data');
        this._assertEquals(response.data.id, this._testIndexId, 'data.id');
        this._assertEquals(response.data.name, 'Test Index', 'data.name');
        this._assertNotNull(response.data.createdUtc, 'data.createdUtc');
    }

    async testGetIndexNotFound() {
        try {
            await this._client.getIndex('non-existent-index-12345');
            this._assert(false, 'Should have thrown VerbexError for not found');
        } catch (error) {
            if (!(error instanceof VerbexError)) throw error;
            this._assertEquals(error.statusCode, 404, 'error.statusCode');
        }
    }

    async testListIndicesAfterCreate() {
        const indices = await this._client.getIndices();
        const found = indices.some(idx => idx.id === this._testIndexId);
        this._assertTrue(found, 'test index should be in list');
    }

    async testCreateIndexWithLabelsAndTags() {
        const indexId = `test-labeled-${crypto.randomBytes(4).toString('hex')}`;
        const labels = ['test', 'labeled'];
        const tags = { environment: 'testing', owner: 'sdk-harness' };
        const response = await this._client.createIndex({
            id: indexId,
            name: 'Labeled Test Index',
            description: 'An index with labels and tags',
            inMemory: true,
            storageMode: 'MemoryOnly',
            labels: labels,
            tags: tags
        });
        this._assertTrue(response.success, 'response.success');
        this._assertEquals(response.statusCode, 201, 'response.statusCode');
        this._assertNotNull(response.data, 'response.data');
        this._assertNotNull(response.data.index, 'data.index');
        // Clean up
        await this._client.deleteIndex(indexId);
    }

    async testGetIndexWithLabelsAndTags() {
        const indexId = `test-labeled-get-${crypto.randomBytes(4).toString('hex')}`;
        const labels = ['retrieval', 'test'];
        const tags = { purpose: 'verification', version: '1.0' };
        await this._client.createIndex({
            id: indexId,
            name: 'Get Labeled Index',
            inMemory: true,
            labels: labels,
            tags: tags
        });
        const response = await this._client.getIndex(indexId);
        this._assertTrue(response.success, 'response.success');
        this._assertNotNull(response.data, 'response.data');
        this._assertNotNull(response.data.labels, 'data.labels');
        this._assertNotNull(response.data.tags, 'data.tags');
        this._assertEquals(response.data.labels.length, 2, 'labels count');
        this._assertEquals(Object.keys(response.data.tags).length, 2, 'tags count');
        // Clean up
        await this._client.deleteIndex(indexId);
    }

    // ==================== Document Management Tests ====================

    async testListDocumentsEmpty() {
        const response = await this._client.listDocuments(this._testIndexId);
        this._assertTrue(response.success, 'response.success');
        this._assertEquals(response.statusCode, 200, 'response.statusCode');
        this._assertNotNull(response.data, 'response.data');
        this._assertNotNull(response.data.documents, 'data.documents');
        this._assertEquals(response.data.count, 0, 'data.count');
    }

    async testAddDocument() {
        const response = await this._client.addDocument(
            this._testIndexId,
            'The quick brown fox jumps over the lazy dog.'
        );
        this._assertTrue(response.success, 'response.success');
        this._assertEquals(response.statusCode, 201, 'response.statusCode');
        this._assertNotNull(response.data, 'response.data');
        this._assertNotNull(response.data.documentId, 'data.documentId');
        this._assertNotNull(response.data.message, 'data.message');
        this._testDocuments.push(response.data.documentId);
    }

    async testAddDocumentWithId() {
        const docId = crypto.randomUUID();
        const response = await this._client.addDocument(
            this._testIndexId,
            'JavaScript is a versatile programming language used for web development and server-side applications.',
            docId
        );
        this._assertTrue(response.success, 'response.success');
        this._assertEquals(response.statusCode, 201, 'response.statusCode');
        this._assertEquals(response.data.documentId, docId, 'data.documentId');
        this._testDocuments.push(docId);
    }

    async testAddMultipleDocuments() {
        const docs = [
            'Machine learning algorithms can identify patterns in large datasets.',
            'Natural language processing enables computers to understand human language.',
            'Deep learning neural networks have revolutionized image recognition.',
            'Cloud computing provides scalable infrastructure for modern applications.'
        ];
        for (const content of docs) {
            const response = await this._client.addDocument(this._testIndexId, content);
            this._assertTrue(response.success, 'response.success');
            this._testDocuments.push(response.data.documentId);
        }
    }

    async testListDocumentsAfterAdd() {
        const response = await this._client.listDocuments(this._testIndexId);
        this._assertTrue(response.success, 'response.success');
        this._assertEquals(response.data.count, this._testDocuments.length, 'data.count');
        const docs = response.data.documents;
        this._assertEquals(docs.length, this._testDocuments.length, 'documents length');
        for (const doc of docs) {
            this._assertNotNull(doc.id, 'document.id');
        }
    }

    async testGetDocument() {
        const docId = this._testDocuments[0];
        const response = await this._client.getDocument(this._testIndexId, docId);
        this._assertTrue(response.success, 'response.success');
        this._assertEquals(response.statusCode, 200, 'response.statusCode');
        this._assertNotNull(response.data, 'response.data');
        this._assertEquals(response.data.id, docId, 'data.id');
    }

    async testGetDocumentNotFound() {
        const fakeId = crypto.randomUUID();
        try {
            await this._client.getDocument(this._testIndexId, fakeId);
            this._assert(false, 'Should have thrown VerbexError for not found');
        } catch (error) {
            if (!(error instanceof VerbexError)) throw error;
            this._assertEquals(error.statusCode, 404, 'error.statusCode');
        }
    }

    async testAddDocumentWithLabelsAndTags() {
        const labels = ['important', 'reviewed'];
        const tags = { author: 'test-harness', category: 'technical' };
        const response = await this._client.addDocument(
            this._testIndexId,
            'This document has labels and tags for testing metadata support.',
            null,
            labels,
            tags
        );
        this._assertTrue(response.success, 'response.success');
        this._assertEquals(response.statusCode, 201, 'response.statusCode');
        this._assertNotNull(response.data, 'response.data');
        this._assertNotNull(response.data.documentId, 'data.documentId');
        this._testDocuments.push(response.data.documentId);
    }

    async testGetDocumentWithLabelsAndTags() {
        const docId = crypto.randomUUID();
        const labels = ['verification', 'metadata'];
        const tags = { source: 'sdk-test', priority: 'high' };
        await this._client.addDocument(
            this._testIndexId,
            'Document for verifying labels and tags retrieval.',
            docId,
            labels,
            tags
        );
        const response = await this._client.getDocument(this._testIndexId, docId);
        this._assertTrue(response.success, 'response.success');
        this._assertNotNull(response.data, 'response.data');
        this._assertNotNull(response.data.labels, 'data.labels');
        this._assertNotNull(response.data.tags, 'data.tags');
        this._assertEquals(response.data.labels.length, 2, 'labels count');
        this._assertEquals(Object.keys(response.data.tags).length, 2, 'tags count');
        this._testDocuments.push(docId);
    }

    // ==================== Search Tests ====================

    async testSearchBasic() {
        const response = await this._client.search(this._testIndexId, 'fox');
        this._assertTrue(response.success, 'response.success');
        this._assertEquals(response.statusCode, 200, 'response.statusCode');
        this._assertNotNull(response.data, 'response.data');
        this._assertEquals(response.data.query, 'fox', 'data.query');
        this._assertNotNull(response.data.results, 'data.results');
        this._assertNotNull(response.data.totalCount, 'data.totalCount');
        this._assertNotNull(response.data.maxResults, 'data.maxResults');
    }

    async testSearchWithResults() {
        const response = await this._client.search(this._testIndexId, 'learning');
        this._assertTrue(response.success, 'response.success');
        const results = response.data.results || [];
        this._assertGreaterThan(results.length, 0, 'results count');
        for (const result of results) {
            this._assertNotNull(result.documentId, 'result.documentId');
            this._assertNotNull(result.score, 'result.score');
        }
    }

    async testSearchMultipleTerms() {
        const response = await this._client.search(this._testIndexId, 'machine learning');
        this._assertTrue(response.success, 'response.success');
        this._assertNotNull(response.data.results, 'data.results');
    }

    async testSearchMaxResults() {
        const response = await this._client.search(this._testIndexId, 'the', 2);
        this._assertTrue(response.success, 'response.success');
        this._assertEquals(response.data.maxResults, 2, 'data.maxResults');
    }

    async testSearchNoResults() {
        const response = await this._client.search(this._testIndexId, 'xyznonexistent12345');
        this._assertTrue(response.success, 'response.success');
        const results = response.data.results || [];
        this._assertEquals(results.length, 0, 'results should be empty');
    }

    async testSearchDocumentsHelper() {
        const searchResponse = await this._client.searchDocuments(this._testIndexId, 'programming');
        this._assertNotNull(searchResponse, 'searchResponse');
        this._assertEquals(searchResponse.query, 'programming', 'query');
        this._assertNotNull(searchResponse.results, 'results');
        this._assertNotNull(searchResponse.totalCount, 'totalCount');
    }

    async testSearchWithLabelFilter() {
        // First add a document with labels
        const docId = crypto.randomUUID();
        const labels = ['searchtest', 'filterable'];
        await this._client.addDocument(
            this._testIndexId,
            'This document contains searchable content with labels for filter testing.',
            docId,
            labels,
            null
        );
        this._testDocuments.push(docId);

        // Search with matching label filter
        const response = await this._client.search(
            this._testIndexId,
            'searchable',
            100,
            ['searchtest'],
            null
        );
        this._assertTrue(response.success, 'response.success');
        this._assertGreaterThan(response.data.results.length, 0, 'should find documents with matching label');

        // Search with non-matching label filter
        const noMatchResponse = await this._client.search(
            this._testIndexId,
            'searchable',
            100,
            ['nonexistentlabel99'],
            null
        );
        this._assertTrue(noMatchResponse.success, 'noMatchResponse.success');
        this._assertEquals(noMatchResponse.data.results.length, 0, 'should find no documents with non-matching label');
    }

    async testSearchWithTagFilter() {
        // First add a document with tags
        const docId = crypto.randomUUID();
        const tags = {
            searchcategory: 'testfilter',
            searchpriority: 'high'
        };
        await this._client.addDocument(
            this._testIndexId,
            'This document contains taggable content for tag filter testing.',
            docId,
            null,
            tags
        );
        this._testDocuments.push(docId);

        // Search with matching tag filter
        const response = await this._client.search(
            this._testIndexId,
            'taggable',
            100,
            null,
            { searchcategory: 'testfilter' }
        );
        this._assertTrue(response.success, 'response.success');
        this._assertGreaterThan(response.data.results.length, 0, 'should find documents with matching tag');

        // Search with non-matching tag filter
        const noMatchResponse = await this._client.search(
            this._testIndexId,
            'taggable',
            100,
            null,
            { searchcategory: 'wrongvalue' }
        );
        this._assertTrue(noMatchResponse.success, 'noMatchResponse.success');
        this._assertEquals(noMatchResponse.data.results.length, 0, 'should find no documents with non-matching tag');
    }

    async testSearchWithLabelsAndTags() {
        // First add a document with both labels and tags
        const docId = crypto.randomUUID();
        const labels = ['combined', 'fulltest'];
        const tags = { combinedcategory: 'both' };
        await this._client.addDocument(
            this._testIndexId,
            'This document has combined labels and tags for comprehensive filter testing.',
            docId,
            labels,
            tags
        );
        this._testDocuments.push(docId);

        // Search with both label and tag filters
        const response = await this._client.search(
            this._testIndexId,
            'comprehensive',
            100,
            ['combined'],
            { combinedcategory: 'both' }
        );
        this._assertTrue(response.success, 'response.success');
        this._assertGreaterThan(response.data.results.length, 0, 'should find documents matching both label and tag');
    }

    // ==================== Document Deletion Tests ====================

    async testDeleteDocument() {
        if (this._testDocuments.length === 0) {
            throw new Error('No test documents to delete');
        }
        const docId = this._testDocuments.pop();
        const response = await this._client.deleteDocument(this._testIndexId, docId);
        this._assertTrue(response.success, 'response.success');
        this._assertEquals(response.statusCode, 200, 'response.statusCode');
        this._assertNotNull(response.data, 'response.data');
        this._assertEquals(response.data.documentId, docId, 'data.documentId');
        this._assertNotNull(response.data.message, 'data.message');
    }

    async testDeleteDocumentNotFound() {
        const fakeId = crypto.randomUUID();
        try {
            await this._client.deleteDocument(this._testIndexId, fakeId);
            this._assert(false, 'Should have thrown VerbexError for not found');
        } catch (error) {
            if (!(error instanceof VerbexError)) throw error;
            this._assertEquals(error.statusCode, 404, 'error.statusCode');
        }
    }

    async testVerifyDocumentDeleted() {
        if (this._testDocuments.length === 0) {
            return; // Skip if no documents left
        }
        const docId = this._testDocuments.pop();
        await this._client.deleteDocument(this._testIndexId, docId);
        try {
            await this._client.getDocument(this._testIndexId, docId);
            this._assert(false, 'Should have thrown VerbexError for deleted document');
        } catch (error) {
            if (!(error instanceof VerbexError)) throw error;
            this._assertEquals(error.statusCode, 404, 'error.statusCode');
        }
    }

    // ==================== Index Deletion Tests ====================

    async testDeleteIndex() {
        const response = await this._client.deleteIndex(this._testIndexId);
        this._assertTrue(response.success, 'response.success');
        this._assertEquals(response.statusCode, 200, 'response.statusCode');
        this._assertNotNull(response.data, 'response.data');
        this._assertEquals(response.data.indexId, this._testIndexId, 'data.indexId');
        this._assertNotNull(response.data.message, 'data.message');
    }

    async testDeleteIndexNotFound() {
        try {
            await this._client.deleteIndex('non-existent-index-67890');
            this._assert(false, 'Should have thrown VerbexError for not found');
        } catch (error) {
            if (!(error instanceof VerbexError)) throw error;
            this._assertEquals(error.statusCode, 404, 'error.statusCode');
        }
    }

    async testVerifyIndexDeleted() {
        try {
            await this._client.getIndex(this._testIndexId);
            this._assert(false, 'Should have thrown VerbexError for deleted index');
        } catch (error) {
            if (!(error instanceof VerbexError)) throw error;
            this._assertEquals(error.statusCode, 404, 'error.statusCode');
        }
    }

    async run() {
        const startTime = Date.now();

        this._printHeader('Verbex SDK Test Harness - JavaScript');
        console.log(`  Endpoint: ${this._endpoint}`);
        console.log(`  Test Index: ${this._testIndexId}`);
        console.log(`  Started: ${new Date().toISOString()}`);

        this._client = new VerbexClient(this._endpoint, this._accessKey);

        try {
            // Health Tests
            this._printSubheader('Health Checks');
            await this._runTest('Root health check', () => this.testRootHealthCheck());
            await this._runTest('Health endpoint', () => this.testHealthEndpoint());

            // Authentication Tests
            this._printSubheader('Authentication');
            await this._runTest('Login with valid credentials', () => this.testLoginSuccess());
            await this._runTest('Login with invalid credentials', () => this.testLoginInvalidCredentials());
            await this._runTest('Validate token', () => this.testValidateToken());
            await this._runTest('Validate invalid token', () => this.testValidateInvalidToken());

            // Index Management Tests
            this._printSubheader('Index Management');
            await this._runTest('List indices (initial)', () => this.testListIndicesInitial());
            await this._runTest('Create index', () => this.testCreateIndex());
            await this._runTest('Create duplicate index fails', () => this.testCreateDuplicateIndex());
            await this._runTest('Get index', () => this.testGetIndex());
            await this._runTest('Get index not found', () => this.testGetIndexNotFound());
            await this._runTest('List indices (after create)', () => this.testListIndicesAfterCreate());
            await this._runTest('Create index with labels and tags', () => this.testCreateIndexWithLabelsAndTags());
            await this._runTest('Get index with labels and tags', () => this.testGetIndexWithLabelsAndTags());

            // Document Management Tests
            this._printSubheader('Document Management');
            await this._runTest('List documents (empty)', () => this.testListDocumentsEmpty());
            await this._runTest('Add document', () => this.testAddDocument());
            await this._runTest('Add document with ID', () => this.testAddDocumentWithId());
            await this._runTest('Add multiple documents', () => this.testAddMultipleDocuments());
            await this._runTest('List documents (after add)', () => this.testListDocumentsAfterAdd());
            await this._runTest('Get document', () => this.testGetDocument());
            await this._runTest('Get document not found', () => this.testGetDocumentNotFound());
            await this._runTest('Add document with labels and tags', () => this.testAddDocumentWithLabelsAndTags());
            await this._runTest('Get document with labels and tags', () => this.testGetDocumentWithLabelsAndTags());

            // Search Tests
            this._printSubheader('Search');
            await this._runTest('Basic search', () => this.testSearchBasic());
            await this._runTest('Search with results', () => this.testSearchWithResults());
            await this._runTest('Search multiple terms', () => this.testSearchMultipleTerms());
            await this._runTest('Search with max results', () => this.testSearchMaxResults());
            await this._runTest('Search with no results', () => this.testSearchNoResults());
            await this._runTest('Search documents helper', () => this.testSearchDocumentsHelper());
            await this._runTest('Search with label filter', () => this.testSearchWithLabelFilter());
            await this._runTest('Search with tag filter', () => this.testSearchWithTagFilter());
            await this._runTest('Search with labels and tags', () => this.testSearchWithLabelsAndTags());

            // Cleanup Tests
            this._printSubheader('Cleanup');
            await this._runTest('Delete document', () => this.testDeleteDocument());
            await this._runTest('Delete document not found', () => this.testDeleteDocumentNotFound());
            await this._runTest('Verify document deleted', () => this.testVerifyDocumentDeleted());
            await this._runTest('Delete index', () => this.testDeleteIndex());
            await this._runTest('Delete index not found', () => this.testDeleteIndexNotFound());
            await this._runTest('Verify index deleted', () => this.testVerifyIndexDeleted());

        } catch (error) {
            console.log(`\n  FATAL ERROR: ${error.constructor.name}: ${error.message}`);
            this._failed++;
        }

        // Summary
        const duration = (Date.now() - startTime) / 1000;
        this._printHeader('Test Summary');
        console.log(`  Total Tests: ${this._passed + this._failed}`);
        console.log(`  Passed: ${this._passed}`);
        console.log(`  Failed: ${this._failed}`);
        console.log(`  Duration: ${duration.toFixed(2)}s`);
        console.log(`  Result: ${this._failed === 0 ? 'SUCCESS' : 'FAILURE'}`);
        console.log();

        return this._failed === 0 ? 0 : 1;
    }
}

/**
 * Main entry point.
 */
async function main() {
    const args = process.argv.slice(2);

    if (args.length !== 2) {
        console.log('Verbex SDK Test Harness - JavaScript');
        console.log();
        console.log('Usage: node test-harness.js <endpoint> <access_key>');
        console.log();
        console.log('Arguments:');
        console.log('  endpoint    The Verbex server endpoint (e.g., http://localhost:8080)');
        console.log('  access_key  The bearer token for authentication');
        console.log();
        console.log('Example:');
        console.log('  node test-harness.js http://localhost:8080 verbexadmin');
        process.exit(1);
    }

    const [endpoint, accessKey] = args;
    const harness = new TestHarness(endpoint, accessKey);
    const exitCode = await harness.run();
    process.exit(exitCode);
}

main().catch(error => {
    console.error('Fatal error:', error);
    process.exit(1);
});
