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

const { VerbexClient, VerbexError, LoginResult, AuthenticationResult, AuthorizationResult, EnumerationOptions } = require('./verbex-sdk.js');
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
        this._testIndexId = '';  // Will be set after index creation
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
        const health = await this._client.rootHealthCheck();
        this._assertNotNull(health, 'health');
        this._assertEquals(health.status, 'Healthy', 'health.status');
        this._assertNotNull(health.version, 'health.version');
        this._assertNotNull(health.timestamp, 'health.timestamp');
    }

    async testHealthEndpoint() {
        const health = await this._client.healthCheck();
        this._assertNotNull(health, 'health');
        this._assertEquals(health.status, 'Healthy', 'health.status');
        this._assertNotNull(health.version, 'health.version');
        this._assertNotNull(health.timestamp, 'health.timestamp');
    }

    // ==================== Authentication Tests ====================

    async testLoginWithCredentialsSuccess() {
        // Test login with tenant ID, email, and password
        // Using "default" tenant with the seeded default user credentials
        const result = await this._client.loginWithCredentials('default', 'default@user.com', 'password');
        this._assertTrue(result.success, 'result.success');
        this._assertEquals(result.authenticationResult, AuthenticationResult.Success, 'result.authenticationResult');
        this._assertEquals(result.authorizationResult, AuthorizationResult.Authorized, 'result.authorizationResult');
        this._assertNotNull(result.token, 'result.token');
    }

    async testLoginWithCredentialsInvalid() {
        // Test login with invalid credentials - should not throw, just return failure
        const result = await this._client.loginWithCredentials('default', 'invalid@example.com', 'wrongpassword');
        this._assertFalse(result.success, 'result.success should be false');
        this._assert(result.authenticationResult !== AuthenticationResult.Success, 'authenticationResult should not be Success');
        this._assertNotNull(result.errorMessage, 'result.errorMessage');
    }

    async testLoginWithTokenSuccess() {
        // Test login with a valid bearer token
        const result = await this._client.loginWithToken(this._accessKey);
        this._assertTrue(result.success, 'result.success');
        this._assertEquals(result.authenticationResult, AuthenticationResult.Success, 'result.authenticationResult');
        this._assertEquals(result.authorizationResult, AuthorizationResult.Authorized, 'result.authorizationResult');
        this._assertNotNull(result.token, 'result.token');
        this._assertEquals(result.token, this._accessKey, 'result.token should match input');
    }

    async testLoginWithTokenInvalid() {
        // Test login with an invalid bearer token - should not throw, just return failure
        const result = await this._client.loginWithToken('invalid-bearer-token-12345');
        this._assertFalse(result.success, 'result.success should be false');
        this._assert(result.authenticationResult !== AuthenticationResult.Success, 'authenticationResult should not be Success');
        this._assertNotNull(result.errorMessage, 'result.errorMessage');
    }

    async testValidateToken() {
        const validation = await this._client.validateToken();
        this._assertNotNull(validation, 'validation');
        this._assertTrue(validation.valid, 'validation.valid');
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
        const indices = await this._client.listIndices();
        this._assertNotNull(indices, 'indices');
    }

    async testCreateIndex() {
        const index = await this._client.createIndex({
            name: 'Test Index',
            description: 'A test index for SDK validation',
            inMemory: true,
            tenantId: 'default'
        });
        this._assertNotNull(index, 'index');
        this._assertNotNull(index.identifier, 'index.identifier');
        this._assertEquals(index.name, 'Test Index', 'index.name');
        // Store the returned index ID for subsequent tests
        this._testIndexId = index.identifier;
    }

    async testCreateDuplicateIndex() {
        // Creating an index with the same name should fail with 409 Conflict
        // The server enforces unique index names within a tenant
        try {
            await this._client.createIndex({ name: 'Test Index', description: 'Duplicate name index', inMemory: true, tenantId: 'default' });
            this._assert(false, 'Should have thrown VerbexError for duplicate name');
        } catch (error) {
            if (!(error instanceof VerbexError)) throw error;
            this._assertEquals(error.statusCode, 409, 'error.statusCode');
        }
    }

    async testGetIndex() {
        const index = await this._client.getIndex(this._testIndexId);
        this._assertNotNull(index, 'index');
        this._assertEquals(index.identifier, this._testIndexId, 'index.identifier');
        this._assertEquals(index.name, 'Test Index', 'index.name');
        this._assertNotNull(index.createdUtc, 'index.createdUtc');
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
        const indices = await this._client.listIndices();
        const found = indices.objects.some(idx => idx.identifier === this._testIndexId);
        this._assertTrue(found, 'test index should be in list');
    }

    async testCreateIndexWithLabelsAndTags() {
        const labels = ['test', 'labeled'];
        const tags = { environment: 'testing', owner: 'sdk-harness' };
        const index = await this._client.createIndex({
            name: 'Labeled Test Index',
            description: 'An index with labels and tags',
            inMemory: true,
            labels: labels,
            tags: tags,
            tenantId: 'default'
        });
        this._assertNotNull(index, 'index');
        // Clean up using the returned identifier
        const indexId = index.identifier;
        if (indexId) {
            await this._client.deleteIndex(indexId);
        }
    }

    async testGetIndexWithLabelsAndTags() {
        const labels = ['retrieval', 'test'];
        const tags = { purpose: 'verification', version: '1.0' };
        const createdIndex = await this._client.createIndex({
            name: 'Get Labeled Index',
            inMemory: true,
            labels: labels,
            tags: tags,
            tenantId: 'default'
        });
        const indexId = createdIndex.identifier;
        this._assertNotNull(indexId, 'created index identifier');
        const index = await this._client.getIndex(indexId);
        this._assertNotNull(index, 'index');
        this._assertNotNull(index.labels, 'index.labels');
        this._assertNotNull(index.tags, 'index.tags');
        this._assertEquals(index.labels.length, 2, 'labels count');
        this._assertEquals(Object.keys(index.tags).length, 2, 'tags count');
        // Clean up
        await this._client.deleteIndex(indexId);
    }

    // ==================== HEAD API Tests ====================

    async testIndexExists() {
        const exists = await this._client.indexExists(this._testIndexId);
        this._assertTrue(exists, 'index should exist');
    }

    async testIndexExistsNotFound() {
        const exists = await this._client.indexExists('non-existent-index-99999');
        this._assertFalse(exists, 'index should not exist');
    }

    async testDocumentExists() {
        if (this._testDocuments.length === 0) {
            throw new Error('No test documents available');
        }
        const docId = this._testDocuments[0];
        const exists = await this._client.documentExists(this._testIndexId, docId);
        this._assertTrue(exists, 'document should exist');
    }

    async testDocumentExistsNotFound() {
        const fakeId = crypto.randomUUID();
        const exists = await this._client.documentExists(this._testIndexId, fakeId);
        this._assertFalse(exists, 'document should not exist');
    }

    // ==================== Document Management Tests ====================

    async testListDocumentsEmpty() {
        const documents = await this._client.listDocuments(this._testIndexId);
        this._assertNotNull(documents, 'documents');
        this._assertEquals(documents.objects.length, 0, 'documents.length');
    }

    async testAddDocument() {
        const result = await this._client.addDocument(
            this._testIndexId,
            'The quick brown fox jumps over the lazy dog.'
        );
        this._assertNotNull(result, 'result');
        this._assertNotNull(result.documentId, 'result.documentId');
        this._assertNotNull(result.message, 'result.message');
        this._testDocuments.push(result.documentId);
    }

    async testAddDocumentWithId() {
        const docId = crypto.randomUUID();
        const result = await this._client.addDocument(
            this._testIndexId,
            'JavaScript is a versatile programming language used for web development and server-side applications.',
            docId
        );
        this._assertNotNull(result, 'result');
        this._assertEquals(result.documentId, docId, 'result.documentId');
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
            const result = await this._client.addDocument(this._testIndexId, content);
            this._assertNotNull(result, 'result');
            this._testDocuments.push(result.documentId);
        }
    }

    async testListDocumentsAfterAdd() {
        const documents = await this._client.listDocuments(this._testIndexId);
        this._assertNotNull(documents, 'documents');
        this._assertEquals(documents.objects.length, this._testDocuments.length, 'documents.length');
        for (const doc of documents.objects) {
            this._assertNotNull(doc.id, 'document.id');
        }
    }

    async testGetDocument() {
        const docId = this._testDocuments[0];
        const document = await this._client.getDocument(this._testIndexId, docId);
        this._assertNotNull(document, 'document');
        this._assertEquals(document.id, docId, 'document.id');
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
        const result = await this._client.addDocument(
            this._testIndexId,
            'This document has labels and tags for testing metadata support.',
            null,
            labels,
            tags
        );
        this._assertNotNull(result, 'result');
        this._assertNotNull(result.documentId, 'result.documentId');
        this._testDocuments.push(result.documentId);
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
        const document = await this._client.getDocument(this._testIndexId, docId);
        this._assertNotNull(document, 'document');
        this._assertNotNull(document.labels, 'document.labels');
        this._assertNotNull(document.tags, 'document.tags');
        this._assertEquals(document.labels.length, 2, 'labels count');
        this._assertEquals(Object.keys(document.tags).length, 2, 'tags count');
        this._testDocuments.push(docId);
    }

    async testGetDocumentReturnsIndexedTerms() {
        // Add a document with known content containing specific terms
        const content = 'apple banana cherry date elderberry';
        const result = await this._client.addDocument(this._testIndexId, content);
        this._assertNotNull(result, 'result');
        const docId = result.documentId;

        // Retrieve the document and verify terms are populated
        const document = await this._client.getDocument(this._testIndexId, docId);
        this._assertNotNull(document, 'document');
        this._assertNotNull(document.terms, 'document.terms');
        this._assertGreaterThan(document.terms.length, 0, 'document.terms.length');

        // Verify expected terms are present (terms are lowercased)
        const expectedTerms = ['apple', 'banana', 'cherry', 'date', 'elderberry'];
        for (const term of expectedTerms) {
            this._assert(document.terms.includes(term), `Document should contain term '${term}'`);
        }

        // Verify indexing runtime is populated
        this._assertNotNull(document.indexingRuntimeMs, 'document.indexingRuntimeMs');
        this._assertGreaterThan(document.indexingRuntimeMs, 0, 'document.indexingRuntimeMs');

        this._testDocuments.push(docId);
    }

    async testGetDocumentsBatch() {
        // Need at least 2 documents
        this._assert(this._testDocuments.length >= 2, 'Need at least 2 test documents for batch retrieval test');

        // Request some existing document IDs plus some fake ones
        const requestedIds = [
            this._testDocuments[0],
            this._testDocuments[1],
            'non-existent-doc-id-12345',
            'another-fake-doc-id-67890'
        ];

        const result = await this._client.getDocumentsBatch(this._testIndexId, requestedIds);

        this._assertNotNull(result, 'result');
        this._assertNotNull(result.documents, 'result.documents');
        this._assertNotNull(result.notFound, 'result.notFound');
        this._assertEquals(result.count, 2, 'result.count');
        this._assertEquals(result.requestedCount, 4, 'result.requestedCount');
        this._assertEquals(result.documents.length, 2, 'result.documents.length');
        this._assertEquals(result.notFound.length, 2, 'result.notFound.length');

        // Verify the found documents have expected IDs
        const foundIds = new Set(result.documents.map(d => d.documentId));
        this._assert(foundIds.has(this._testDocuments[0]), 'first test document should be found');
        this._assert(foundIds.has(this._testDocuments[1]), 'second test document should be found');

        // Verify the not found IDs
        this._assert(result.notFound.includes('non-existent-doc-id-12345'), 'fake ID should be in notFound');
        this._assert(result.notFound.includes('another-fake-doc-id-67890'), 'another fake ID should be in notFound');
    }

    async testGetDocumentsBatchEmpty() {
        // Test with empty array
        const result = await this._client.getDocumentsBatch(this._testIndexId, []);
        this._assertNotNull(result, 'result');
        this._assertEquals(result.count, 0, 'result.count');
        this._assertEquals(result.documents.length, 0, 'result.documents.length');
    }

    async testDeleteDocumentsBatch() {
        // Add some documents specifically for batch delete test
        const docId1 = crypto.randomUUID();
        const docId2 = crypto.randomUUID();
        const fakeDocId = 'non-existent-doc-for-batch-delete';

        await this._client.addDocument(this._testIndexId, 'Document for batch delete test 1.', docId1);
        await this._client.addDocument(this._testIndexId, 'Document for batch delete test 2.', docId2);

        // Request deletion of existing docs plus a fake one
        const idsToDelete = [docId1, docId2, fakeDocId];
        const result = await this._client.deleteDocumentsBatch(this._testIndexId, idsToDelete);

        this._assertNotNull(result, 'result');
        this._assertNotNull(result.deleted, 'result.deleted');
        this._assertNotNull(result.notFound, 'result.notFound');
        this._assertEquals(result.deletedCount, 2, 'result.deletedCount');
        this._assertEquals(result.notFoundCount, 1, 'result.notFoundCount');
        this._assertEquals(result.requestedCount, 3, 'result.requestedCount');
        this._assertEquals(result.deleted.length, 2, 'result.deleted.length');
        this._assertEquals(result.notFound.length, 1, 'result.notFound.length');

        // Verify the deleted IDs
        this._assert(result.deleted.includes(docId1), 'docId1 should be in deleted');
        this._assert(result.deleted.includes(docId2), 'docId2 should be in deleted');
        this._assert(result.notFound.includes(fakeDocId), 'fakeDocId should be in notFound');

        // Verify documents are actually deleted
        const doc1Exists = await this._client.documentExists(this._testIndexId, docId1);
        const doc2Exists = await this._client.documentExists(this._testIndexId, docId2);
        this._assertFalse(doc1Exists, 'docId1 should no longer exist');
        this._assertFalse(doc2Exists, 'docId2 should no longer exist');
    }

    async testDeleteDocumentsBatchEmpty() {
        // Test with empty array
        const result = await this._client.deleteDocumentsBatch(this._testIndexId, []);
        this._assertNotNull(result, 'result');
        this._assertEquals(result.deletedCount, 0, 'result.deletedCount');
        this._assertEquals(result.requestedCount, 0, 'result.requestedCount');
    }

    // ==================== Search Tests ====================

    async testSearchBasic() {
        const searchResult = await this._client.search(this._testIndexId, 'fox');
        this._assertNotNull(searchResult, 'searchResult');
        this._assertEquals(searchResult.query, 'fox', 'searchResult.query');
        this._assertNotNull(searchResult.results, 'searchResult.results');
        this._assertNotNull(searchResult.totalCount, 'searchResult.totalCount');
        this._assertNotNull(searchResult.maxResults, 'searchResult.maxResults');
    }

    async testSearchWithResults() {
        const searchResult = await this._client.search(this._testIndexId, 'learning');
        this._assertNotNull(searchResult, 'searchResult');
        const results = searchResult.results || [];
        this._assertGreaterThan(results.length, 0, 'results count');
        for (const result of results) {
            this._assertNotNull(result.documentId, 'result.documentId');
            this._assertNotNull(result.score, 'result.score');
        }
    }

    async testSearchMultipleTerms() {
        const searchResult = await this._client.search(this._testIndexId, 'machine learning');
        this._assertNotNull(searchResult, 'searchResult');
        this._assertNotNull(searchResult.results, 'searchResult.results');
    }

    async testSearchMaxResults() {
        const searchResult = await this._client.search(this._testIndexId, 'the', 2);
        this._assertNotNull(searchResult, 'searchResult');
        this._assertEquals(searchResult.maxResults, 2, 'searchResult.maxResults');
    }

    async testSearchNoResults() {
        const searchResult = await this._client.search(this._testIndexId, 'xyznonexistent12345');
        this._assertNotNull(searchResult, 'searchResult');
        const results = searchResult.results || [];
        this._assertEquals(results.length, 0, 'results should be empty');
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
        const searchResult = await this._client.search(
            this._testIndexId,
            'searchable',
            100,
            ['searchtest'],
            null
        );
        this._assertNotNull(searchResult, 'searchResult');
        this._assertGreaterThan(searchResult.results.length, 0, 'should find documents with matching label');

        // Search with non-matching label filter
        const noMatchResult = await this._client.search(
            this._testIndexId,
            'searchable',
            100,
            ['nonexistentlabel99'],
            null
        );
        this._assertNotNull(noMatchResult, 'noMatchResult');
        this._assertEquals(noMatchResult.results.length, 0, 'should find no documents with non-matching label');
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
        const searchResult = await this._client.search(
            this._testIndexId,
            'taggable',
            100,
            null,
            { searchcategory: 'testfilter' }
        );
        this._assertNotNull(searchResult, 'searchResult');
        this._assertGreaterThan(searchResult.results.length, 0, 'should find documents with matching tag');

        // Search with non-matching tag filter
        const noMatchResult = await this._client.search(
            this._testIndexId,
            'taggable',
            100,
            null,
            { searchcategory: 'wrongvalue' }
        );
        this._assertNotNull(noMatchResult, 'noMatchResult');
        this._assertEquals(noMatchResult.results.length, 0, 'should find no documents with non-matching tag');
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
        const searchResult = await this._client.search(
            this._testIndexId,
            'comprehensive',
            100,
            ['combined'],
            { combinedcategory: 'both' }
        );
        this._assertNotNull(searchResult, 'searchResult');
        this._assertGreaterThan(searchResult.results.length, 0, 'should find documents matching both label and tag');
    }

    // ==================== Filtered Enumeration Tests ====================

    async testListDocumentsWithLabelFilter() {
        // Add 3 documents, 2 with a specific label
        const doc1 = await this._client.addDocument(
            this._testIndexId,
            'First labeled document for enumeration filter testing.',
            null,
            ['enumfilter'],
            null
        );
        this._testDocuments.push(doc1.documentId);

        const doc2 = await this._client.addDocument(
            this._testIndexId,
            'Second labeled document for enumeration filter testing.',
            null,
            ['enumfilter'],
            null
        );
        this._testDocuments.push(doc2.documentId);

        const doc3 = await this._client.addDocument(
            this._testIndexId,
            'Third document without the filter label.'
        );
        this._testDocuments.push(doc3.documentId);

        // List documents with label filter
        const options = new EnumerationOptions({ labels: ['enumfilter'] });
        const result = await this._client.listDocuments(this._testIndexId, options);
        this._assertNotNull(result, 'result');
        this._assertEquals(result.objects.length, 2, 'filtered documents count');
    }

    async testListDocumentsWithTagFilter() {
        // Add 3 documents, 2 with a specific tag
        const doc1 = await this._client.addDocument(
            this._testIndexId,
            'First tagged document for enumeration tag filter testing.',
            null,
            null,
            { enumtag: 'yes' }
        );
        this._testDocuments.push(doc1.documentId);

        const doc2 = await this._client.addDocument(
            this._testIndexId,
            'Second tagged document for enumeration tag filter testing.',
            null,
            null,
            { enumtag: 'yes' }
        );
        this._testDocuments.push(doc2.documentId);

        const doc3 = await this._client.addDocument(
            this._testIndexId,
            'Third document without the filter tag.'
        );
        this._testDocuments.push(doc3.documentId);

        // List documents with tag filter
        const options = new EnumerationOptions({ tags: { enumtag: 'yes' } });
        const result = await this._client.listDocuments(this._testIndexId, options);
        this._assertNotNull(result, 'result');
        this._assertEquals(result.objects.length, 2, 'filtered documents count');
    }

    // ==================== Wildcard Search Tests ====================

    async testWildcardSearch() {
        // Search with wildcard query "*"
        const searchResult = await this._client.search(this._testIndexId, '*');
        this._assertNotNull(searchResult, 'searchResult');
        this._assertNotNull(searchResult.results, 'searchResult.results');
        this._assertGreaterThan(searchResult.results.length, 0, 'wildcard results count');

        // All wildcard results should have a score of 0
        for (const result of searchResult.results) {
            this._assertNotNull(result.documentId, 'result.documentId');
            this._assertEquals(result.score, 0, 'wildcard result score should be 0');
        }
    }

    async testWildcardSearchWithFilters() {
        // Add a document with a unique label for wildcard filter test
        const docId = crypto.randomUUID();
        const labels = ['wildcardfilter'];
        await this._client.addDocument(
            this._testIndexId,
            'Document specifically for wildcard filter testing.',
            docId,
            labels,
            null
        );
        this._testDocuments.push(docId);

        // Wildcard search with label filter
        const searchResult = await this._client.search(
            this._testIndexId,
            '*',
            100,
            ['wildcardfilter'],
            null
        );
        this._assertNotNull(searchResult, 'searchResult');
        this._assertGreaterThan(searchResult.results.length, 0, 'wildcard filtered results count');

        // All results should have the matching document
        const found = searchResult.results.some(r => r.documentId === docId);
        this._assertTrue(found, 'wildcard filter should return the labeled document');

        // All wildcard results should have a score of 0
        for (const result of searchResult.results) {
            this._assertEquals(result.score, 0, 'wildcard filtered result score should be 0');
        }
    }

    // ==================== Document Deletion Tests ====================

    async testDeleteDocument() {
        if (this._testDocuments.length === 0) {
            throw new Error('No test documents to delete');
        }
        const docId = this._testDocuments.pop();
        await this._client.deleteDocument(this._testIndexId, docId);
        // If we get here without exception, the delete succeeded
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
        await this._client.deleteIndex(this._testIndexId);
        // If we get here without exception, the delete succeeded
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
        console.log(`  Started: ${new Date().toISOString()}`);

        this._client = new VerbexClient(this._endpoint, this._accessKey);

        try {
            // Health Tests
            this._printSubheader('Health Checks');
            await this._runTest('Root health check', () => this.testRootHealthCheck());
            await this._runTest('Health endpoint', () => this.testHealthEndpoint());

            // Authentication Tests
            this._printSubheader('Authentication');
            await this._runTest('Login with credentials (success)', () => this.testLoginWithCredentialsSuccess());
            await this._runTest('Login with credentials (invalid)', () => this.testLoginWithCredentialsInvalid());
            await this._runTest('Login with bearer token (success)', () => this.testLoginWithTokenSuccess());
            await this._runTest('Login with bearer token (invalid)', () => this.testLoginWithTokenInvalid());
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
            await this._runTest('Index exists (HEAD)', () => this.testIndexExists());
            await this._runTest('Index exists not found (HEAD)', () => this.testIndexExistsNotFound());

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
            await this._runTest('Get document returns indexed terms', () => this.testGetDocumentReturnsIndexedTerms());
            await this._runTest('Get documents batch', () => this.testGetDocumentsBatch());
            await this._runTest('Get documents batch (empty)', () => this.testGetDocumentsBatchEmpty());
            await this._runTest('Delete documents batch', () => this.testDeleteDocumentsBatch());
            await this._runTest('Delete documents batch (empty)', () => this.testDeleteDocumentsBatchEmpty());
            await this._runTest('Document exists (HEAD)', () => this.testDocumentExists());
            await this._runTest('Document exists not found (HEAD)', () => this.testDocumentExistsNotFound());

            // Search Tests
            this._printSubheader('Search');
            await this._runTest('Basic search', () => this.testSearchBasic());
            await this._runTest('Search with results', () => this.testSearchWithResults());
            await this._runTest('Search multiple terms', () => this.testSearchMultipleTerms());
            await this._runTest('Search with max results', () => this.testSearchMaxResults());
            await this._runTest('Search with no results', () => this.testSearchNoResults());
            await this._runTest('Search with label filter', () => this.testSearchWithLabelFilter());
            await this._runTest('Search with tag filter', () => this.testSearchWithTagFilter());
            await this._runTest('Search with labels and tags', () => this.testSearchWithLabelsAndTags());

            // Filtered Enumeration Tests
            this._printSubheader('Filtered Enumeration');
            await this._runTest('List documents with label filter', () => this.testListDocumentsWithLabelFilter());
            await this._runTest('List documents with tag filter', () => this.testListDocumentsWithTagFilter());

            // Wildcard Search Tests
            this._printSubheader('Wildcard Search');
            await this._runTest('Wildcard search', () => this.testWildcardSearch());
            await this._runTest('Wildcard search with filters', () => this.testWildcardSearchWithFilters());

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

        // Failed tests detail
        const failedTests = this._results.filter(r => !r.passed);
        if (failedTests.length > 0) {
            this._printHeader('Failed Tests');
            failedTests.forEach((failed, index) => {
                console.log(`  ${index + 1}. ${failed.name}`);
                console.log(`     Error: ${failed.message}`);
                console.log(`     Duration: ${failed.durationMs.toFixed(2)}ms`);
                console.log();
            });
        }

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
