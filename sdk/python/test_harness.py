#!/usr/bin/env python3
"""
Verbex SDK Test Harness for Python

A comprehensive test suite that validates all Verbex API endpoints.
Runs as a command-line program with consistent output formatting.

Usage:
    python test_harness.py <endpoint> <access_key>

Example:
    python test_harness.py http://localhost:8080 verbexadmin
"""

import sys
import uuid
import time
from datetime import datetime
from typing import Callable, Optional, Any
from verbex_sdk import VerbexClient, VerbexError, LoginResult, AuthenticationResult, AuthorizationResult, EnumerationOptions


class TestResult:
    """Result of a single test."""
    def __init__(self, name: str, passed: bool, message: str = "", duration_ms: float = 0):
        self.name = name
        self.passed = passed
        self.message = message
        self.duration_ms = duration_ms


class TestHarness:
    """Test harness for Verbex SDK."""

    def __init__(self, endpoint: str, access_key: str):
        self._endpoint = endpoint
        self._access_key = access_key
        self._client: Optional[VerbexClient] = None
        self._test_index_id = ""  # Will be set after index creation
        self._test_documents: list = []
        self._results: list = []
        self._passed = 0
        self._failed = 0

    def _print_header(self, text: str):
        """Print a section header."""
        print()
        print("=" * 60)
        print(f"  {text}")
        print("=" * 60)

    def _print_subheader(self, text: str):
        """Print a subsection header."""
        print()
        print(f"--- {text} ---")

    def _print_result(self, result: TestResult):
        """Print a test result."""
        status = "PASS" if result.passed else "FAIL"
        print(f"  [{status}] {result.name} ({result.duration_ms:.2f}ms)")
        if result.message and not result.passed:
            print(f"         Error: {result.message}")

    def _run_test(self, name: str, test_fn: Callable[[], None]) -> TestResult:
        """Run a single test and capture the result."""
        start_time = time.time()
        try:
            test_fn()
            duration_ms = (time.time() - start_time) * 1000
            result = TestResult(name, True, "", duration_ms)
            self._passed += 1
        except AssertionError as e:
            duration_ms = (time.time() - start_time) * 1000
            result = TestResult(name, False, str(e), duration_ms)
            self._failed += 1
        except Exception as e:
            duration_ms = (time.time() - start_time) * 1000
            result = TestResult(name, False, f"{type(e).__name__}: {str(e)}", duration_ms)
            self._failed += 1

        self._results.append(result)
        self._print_result(result)
        return result

    def _assert(self, condition: bool, message: str):
        """Assert a condition with a message."""
        if not condition:
            raise AssertionError(message)

    def _assert_not_none(self, value: Any, field_name: str):
        """Assert that a value is not None."""
        self._assert(value is not None, f"{field_name} should not be None")

    def _assert_equals(self, actual: Any, expected: Any, field_name: str):
        """Assert that two values are equal."""
        self._assert(actual == expected, f"{field_name} expected '{expected}', got '{actual}'")

    def _assert_true(self, value: bool, field_name: str):
        """Assert that a value is True."""
        self._assert(value is True, f"{field_name} should be True")

    def _assert_false(self, value: bool, field_name: str):
        """Assert that a value is False."""
        self._assert(value is False, f"{field_name} should be False")

    def _assert_greater_than(self, actual: Any, expected: Any, field_name: str):
        """Assert that a value is greater than expected."""
        self._assert(actual > expected, f"{field_name} expected > {expected}, got {actual}")

    def _assert_contains(self, haystack: str, needle: str, field_name: str):
        """Assert that a string contains a substring."""
        self._assert(needle in haystack, f"{field_name} should contain '{needle}'")

    # ==================== Health Tests ====================

    def test_root_health_check(self):
        """Test root health check endpoint."""
        health = self._client.root_health_check()
        self._assert_not_none(health, "health")
        self._assert_equals(health.status, 'Healthy', "health.status")
        self._assert_not_none(health.version, "health.version")
        self._assert_not_none(health.timestamp, "health.timestamp")

    def test_health_endpoint(self):
        """Test /v1.0/health endpoint."""
        health = self._client.health_check()
        self._assert_not_none(health, "health")
        self._assert_equals(health.status, 'Healthy', "health.status")
        self._assert_not_none(health.version, "health.version")
        self._assert_not_none(health.timestamp, "health.timestamp")

    # ==================== Authentication Tests ====================

    def test_login_with_credentials_success(self):
        """Test successful login with credentials."""
        # Test login with tenant ID, email, and password
        # Using "default" tenant with the seeded default user credentials
        result = self._client.login_with_credentials("default", "default@user.com", "password")
        self._assert_true(result.success, "result.success")
        self._assert_equals(result.authentication_result, AuthenticationResult.SUCCESS, "result.authentication_result")
        self._assert_equals(result.authorization_result, AuthorizationResult.AUTHORIZED, "result.authorization_result")
        self._assert_not_none(result.token, "result.token")

    def test_login_with_credentials_invalid(self):
        """Test login with invalid credentials."""
        # Should not throw, just return failure
        result = self._client.login_with_credentials("default", "invalid@example.com", "wrongpassword")
        self._assert_false(result.success, "result.success should be False")
        self._assert(result.authentication_result != AuthenticationResult.SUCCESS, "authentication_result should not be Success")
        self._assert_not_none(result.error_message, "result.error_message")

    def test_login_with_token_success(self):
        """Test successful login with bearer token."""
        # Test login with a valid bearer token
        result = self._client.login_with_token(self._access_key)
        self._assert_true(result.success, "result.success")
        self._assert_equals(result.authentication_result, AuthenticationResult.SUCCESS, "result.authentication_result")
        self._assert_equals(result.authorization_result, AuthorizationResult.AUTHORIZED, "result.authorization_result")
        self._assert_not_none(result.token, "result.token")
        self._assert_equals(result.token, self._access_key, "result.token should match input")

    def test_login_with_token_invalid(self):
        """Test login with invalid bearer token."""
        # Should not throw, just return failure
        result = self._client.login_with_token("invalid-bearer-token-12345")
        self._assert_false(result.success, "result.success should be False")
        self._assert(result.authentication_result != AuthenticationResult.SUCCESS, "authentication_result should not be Success")
        self._assert_not_none(result.error_message, "result.error_message")

    def test_validate_token(self):
        """Test token validation."""
        validation = self._client.validate_token()
        self._assert_not_none(validation, "validation")
        self._assert_true(validation.valid, "validation.valid")

    def test_validate_invalid_token(self):
        """Test validation with invalid token."""
        invalid_client = VerbexClient(self._endpoint, "invalid-token")
        try:
            invalid_client.validate_token()
            self._assert(False, "Should have thrown VerbexError")
        except VerbexError as e:
            self._assert_equals(e.status_code, 401, "error.status_code")
        finally:
            invalid_client.close()

    # ==================== Index Management Tests ====================

    def test_list_indices_initial(self):
        """Test listing indices."""
        indices = self._client.list_indices()
        self._assert_not_none(indices, "indices")

    def test_create_index(self):
        """Test creating an index."""
        index = self._client.create_index(
            name="Test Index",
            description="A test index for SDK validation",
            in_memory=True,
            tenant_id="default"
        )
        self._assert_not_none(index, "index")
        self._assert_not_none(index.identifier, "index.identifier")
        self._assert_equals(index.name, "Test Index", "index.name")
        # Store the returned index ID for subsequent tests
        self._test_index_id = index.identifier

    def test_create_duplicate_index(self):
        """Test creating an index with duplicate name fails."""
        # Creating an index with the same name should fail with 409 Conflict
        # The server enforces unique index names within a tenant
        try:
            self._client.create_index(name="Test Index", description="Duplicate name index", in_memory=True, tenant_id="default")
            self._assert(False, "Should have thrown VerbexError for duplicate name")
        except VerbexError as e:
            self._assert_equals(e.status_code, 409, "error.status_code")

    def test_get_index(self):
        """Test getting index details."""
        index = self._client.get_index(self._test_index_id)
        self._assert_not_none(index, "index")
        self._assert_equals(index.identifier, self._test_index_id, "index.identifier")
        self._assert_equals(index.name, "Test Index", "index.name")
        self._assert_not_none(index.created_utc, "index.created_utc")

    def test_get_index_not_found(self):
        """Test getting a non-existent index."""
        try:
            self._client.get_index("non-existent-index-12345")
            self._assert(False, "Should have thrown VerbexError for not found")
        except VerbexError as e:
            self._assert_equals(e.status_code, 404, "error.status_code")

    def test_list_indices_after_create(self):
        """Test listing indices includes new index."""
        indices = self._client.list_indices()
        found = any(idx.identifier == self._test_index_id for idx in indices)
        self._assert_true(found, "test index should be in list")

    def test_create_index_with_labels_and_tags(self):
        """Test creating an index with labels and tags."""
        labels = ["test", "labeled"]
        tags = {"environment": "testing", "owner": "sdk-harness"}
        index = self._client.create_index(
            name="Labeled Test Index",
            description="An index with labels and tags",
            in_memory=True,
            labels=labels,
            tags=tags,
            tenant_id="default"
        )
        self._assert_not_none(index, "index")
        # Clean up using the returned identifier
        index_id = index.identifier
        if index_id:
            self._client.delete_index(index_id)

    def test_get_index_with_labels_and_tags(self):
        """Test getting an index with labels and tags."""
        labels = ["retrieval", "test"]
        tags = {"purpose": "verification", "version": "1.0"}
        created_index = self._client.create_index(
            name="Get Labeled Index",
            in_memory=True,
            labels=labels,
            tags=tags,
            tenant_id="default"
        )
        index_id = created_index.identifier
        self._assert_not_none(index_id, "created index identifier")
        index = self._client.get_index(index_id)
        self._assert_not_none(index, "index")
        self._assert_not_none(index.labels, "index.labels")
        self._assert_not_none(index.tags, "index.tags")
        self._assert_equals(len(index.labels), 2, "labels count")
        self._assert_equals(len(index.tags), 2, "tags count")
        # Clean up
        self._client.delete_index(index_id)

    # ==================== HEAD API Tests ====================

    def test_index_exists(self):
        """Test HEAD API for index existence."""
        exists = self._client.index_exists(self._test_index_id)
        self._assert_true(exists, "index should exist")

    def test_index_exists_not_found(self):
        """Test HEAD API for non-existent index."""
        exists = self._client.index_exists("non-existent-index-99999")
        self._assert_false(exists, "index should not exist")

    def test_document_exists(self):
        """Test HEAD API for document existence."""
        if len(self._test_documents) == 0:
            raise AssertionError("No test documents available")
        doc_id = self._test_documents[0]
        exists = self._client.document_exists(self._test_index_id, doc_id)
        self._assert_true(exists, "document should exist")

    def test_document_exists_not_found(self):
        """Test HEAD API for non-existent document."""
        fake_id = str(uuid.uuid4())
        exists = self._client.document_exists(self._test_index_id, fake_id)
        self._assert_false(exists, "document should not exist")

    # ==================== Document Management Tests ====================

    def test_list_documents_empty(self):
        """Test listing documents on empty index."""
        documents = self._client.list_documents(self._test_index_id)
        self._assert_not_none(documents, "documents")
        self._assert_equals(len(documents), 0, "documents count")

    def test_add_document(self):
        """Test adding a document."""
        result = self._client.add_document(
            self._test_index_id,
            "The quick brown fox jumps over the lazy dog."
        )
        self._assert_not_none(result, "result")
        self._assert_not_none(result.document_id, "result.document_id")
        self._assert_not_none(result.message, "result.message")
        self._test_documents.append(result.document_id)

    def test_add_document_with_id(self):
        """Test adding a document with explicit ID."""
        doc_id = str(uuid.uuid4())
        result = self._client.add_document(
            self._test_index_id,
            "Python is a versatile programming language used for web development, data science, and automation.",
            document_id=doc_id
        )
        self._assert_not_none(result, "result")
        self._assert_equals(result.document_id, doc_id, "result.document_id")
        self._test_documents.append(doc_id)

    def test_add_multiple_documents(self):
        """Test adding multiple documents for search tests."""
        docs = [
            "Machine learning algorithms can identify patterns in large datasets.",
            "Natural language processing enables computers to understand human language.",
            "Deep learning neural networks have revolutionized image recognition.",
            "Cloud computing provides scalable infrastructure for modern applications."
        ]
        for content in docs:
            result = self._client.add_document(self._test_index_id, content)
            self._assert_not_none(result, "result")
            self._test_documents.append(result.document_id)

    def test_list_documents_after_add(self):
        """Test listing documents after adding."""
        documents = self._client.list_documents(self._test_index_id)
        self._assert_not_none(documents, "documents")
        self._assert_equals(len(documents), len(self._test_documents), "documents count")
        for doc in documents:
            self._assert_not_none(doc.id, "document.id")

    def test_get_document(self):
        """Test getting a specific document."""
        doc_id = self._test_documents[0]
        document = self._client.get_document(self._test_index_id, doc_id)
        self._assert_not_none(document, "document")
        self._assert_equals(document.id, doc_id, "document.id")

    def test_get_document_not_found(self):
        """Test getting a non-existent document."""
        fake_id = str(uuid.uuid4())
        try:
            self._client.get_document(self._test_index_id, fake_id)
            self._assert(False, "Should have thrown VerbexError for not found")
        except VerbexError as e:
            self._assert_equals(e.status_code, 404, "error.status_code")

    def test_add_document_with_labels_and_tags(self):
        """Test adding a document with labels and tags."""
        labels = ["important", "reviewed"]
        tags = {"author": "test-harness", "category": "technical"}
        result = self._client.add_document(
            self._test_index_id,
            "This document has labels and tags for testing metadata support.",
            labels=labels,
            tags=tags
        )
        self._assert_not_none(result, "result")
        self._assert_not_none(result.document_id, "result.document_id")
        self._test_documents.append(result.document_id)

    def test_get_document_with_labels_and_tags(self):
        """Test getting a document with labels and tags."""
        doc_id = str(uuid.uuid4())
        labels = ["verification", "metadata"]
        tags = {"source": "sdk-test", "priority": "high"}
        self._client.add_document(
            self._test_index_id,
            "Document for verifying labels and tags retrieval.",
            document_id=doc_id,
            labels=labels,
            tags=tags
        )
        document = self._client.get_document(self._test_index_id, doc_id)
        self._assert_not_none(document, "document")
        self._assert_not_none(document.labels, "document.labels")
        self._assert_not_none(document.tags, "document.tags")
        self._assert_equals(len(document.labels), 2, "labels count")
        self._assert_equals(len(document.tags), 2, "tags count")
        self._test_documents.append(doc_id)

    def test_get_document_returns_indexed_terms(self):
        """Test that document retrieval returns indexed terms."""
        # Add a document with known content containing specific terms
        content = "apple banana cherry date elderberry"
        result = self._client.add_document(self._test_index_id, content)
        self._assert_not_none(result, "result")
        doc_id = result.document_id

        # Retrieve the document and verify terms are populated
        document = self._client.get_document(self._test_index_id, doc_id)
        self._assert_not_none(document, "document")
        self._assert_not_none(document.terms, "document.terms")
        self._assert_greater_than(len(document.terms), 0, "len(document.terms)")

        # Verify expected terms are present (terms are lowercased)
        expected_terms = {"apple", "banana", "cherry", "date", "elderberry"}
        for term in expected_terms:
            self._assert(term in document.terms, f"Document should contain term '{term}'")

        # Verify indexing runtime is populated
        self._assert_not_none(document.indexing_runtime_ms, "document.indexing_runtime_ms")
        self._assert_greater_than(document.indexing_runtime_ms, 0, "document.indexing_runtime_ms")

        self._test_documents.append(doc_id)

    def test_get_documents_batch(self):
        """Test getting multiple documents by IDs in a single request."""
        # Need at least 2 documents
        self._assert(len(self._test_documents) >= 2, "Need at least 2 test documents for batch retrieval test")

        # Request some existing document IDs plus some fake ones
        requested_ids = [
            self._test_documents[0],
            self._test_documents[1],
            "non-existent-doc-id-12345",
            "another-fake-doc-id-67890"
        ]

        result = self._client.get_documents_batch(self._test_index_id, requested_ids)

        self._assert_not_none(result, "result")
        self._assert_not_none(result.documents, "result.documents")
        self._assert_not_none(result.not_found, "result.not_found")
        self._assert_equals(result.count, 2, "result.count")
        self._assert_equals(result.requested_count, 4, "result.requested_count")
        self._assert_equals(len(result.documents), 2, "len(result.documents)")
        self._assert_equals(len(result.not_found), 2, "len(result.not_found)")

        # Verify the found documents have expected IDs
        found_ids = {doc.document_id for doc in result.documents}
        self._assert(self._test_documents[0] in found_ids, "first test document should be found")
        self._assert(self._test_documents[1] in found_ids, "second test document should be found")

        # Verify the not found IDs
        self._assert("non-existent-doc-id-12345" in result.not_found, "fake ID should be in not_found")
        self._assert("another-fake-doc-id-67890" in result.not_found, "another fake ID should be in not_found")

    def test_get_documents_batch_empty(self):
        """Test getting documents with empty list."""
        result = self._client.get_documents_batch(self._test_index_id, [])
        self._assert_not_none(result, "result")
        self._assert_equals(result.count, 0, "result.count")
        self._assert_equals(len(result.documents), 0, "len(result.documents)")

    def test_delete_documents_batch(self):
        """Test deleting multiple documents by IDs in a single request."""
        # Add some documents specifically for batch delete test
        doc_id1 = str(uuid.uuid4())
        doc_id2 = str(uuid.uuid4())
        fake_doc_id = "non-existent-doc-for-batch-delete"

        self._client.add_document(self._test_index_id, "Document for batch delete test 1.", doc_id1)
        self._client.add_document(self._test_index_id, "Document for batch delete test 2.", doc_id2)

        # Request deletion of existing docs plus a fake one
        ids_to_delete = [doc_id1, doc_id2, fake_doc_id]
        result = self._client.delete_documents_batch(self._test_index_id, ids_to_delete)

        self._assert_not_none(result, "result")
        self._assert_not_none(result.deleted, "result.deleted")
        self._assert_not_none(result.not_found, "result.not_found")
        self._assert_equals(result.deleted_count, 2, "result.deleted_count")
        self._assert_equals(result.not_found_count, 1, "result.not_found_count")
        self._assert_equals(result.requested_count, 3, "result.requested_count")
        self._assert_equals(len(result.deleted), 2, "len(result.deleted)")
        self._assert_equals(len(result.not_found), 1, "len(result.not_found)")

        # Verify the deleted IDs
        self._assert(doc_id1 in result.deleted, "doc_id1 should be in deleted")
        self._assert(doc_id2 in result.deleted, "doc_id2 should be in deleted")
        self._assert(fake_doc_id in result.not_found, "fake_doc_id should be in not_found")

        # Verify documents are actually deleted
        doc1_exists = self._client.document_exists(self._test_index_id, doc_id1)
        doc2_exists = self._client.document_exists(self._test_index_id, doc_id2)
        self._assert_false(doc1_exists, "doc_id1 should no longer exist")
        self._assert_false(doc2_exists, "doc_id2 should no longer exist")

    def test_delete_documents_batch_empty(self):
        """Test deleting documents with empty list."""
        result = self._client.delete_documents_batch(self._test_index_id, [])
        self._assert_not_none(result, "result")
        self._assert_equals(result.deleted_count, 0, "result.deleted_count")
        self._assert_equals(result.requested_count, 0, "result.requested_count")

    # ==================== Top Terms Tests ====================

    def test_get_top_terms(self):
        """Test getting top terms from an index."""
        # Get top terms from the index - we've added several documents with various terms
        top_terms = self._client.get_top_terms(self._test_index_id, 10)
        self._assert_not_none(top_terms, "top_terms")
        # We have documents, so we should have some terms
        self._assert_greater_than(len(top_terms), 0, "len(top_terms)")
        # Verify each term has a positive frequency
        for term, count in top_terms.items():
            self._assert_not_none(term, "term")
            self._assert_greater_than(count, 0, f"frequency for '{term}'")

    def test_get_top_terms_default_limit(self):
        """Test getting top terms with default limit."""
        # Test with default limit (10)
        top_terms = self._client.get_top_terms(self._test_index_id)
        self._assert_not_none(top_terms, "top_terms")
        # Should not exceed 10 results
        self._assert(len(top_terms) <= 10, f"len(top_terms) should be <= 10, got {len(top_terms)}")

    def test_get_top_terms_custom_limit(self):
        """Test getting top terms with custom limit."""
        # Test with custom limit
        top_terms = self._client.get_top_terms(self._test_index_id, 3)
        self._assert_not_none(top_terms, "top_terms")
        # Should not exceed 3 results
        self._assert(len(top_terms) <= 3, f"len(top_terms) should be <= 3, got {len(top_terms)}")

    def test_get_top_terms_not_found(self):
        """Test getting top terms from non-existent index."""
        try:
            self._client.get_top_terms("non-existent-index-99999")
            self._assert(False, "Should have thrown VerbexError for not found")
        except VerbexError as e:
            self._assert_equals(e.status_code, 404, "error.status_code")

    # ==================== Search Tests ====================

    def test_search_basic(self):
        """Test basic search functionality."""
        search_result = self._client.search(self._test_index_id, "fox")
        self._assert_not_none(search_result, "search_result")
        self._assert_equals(search_result.query, 'fox', "search_result.query")
        self._assert_not_none(search_result.results, "search_result.results")
        self._assert_not_none(search_result.total_count, "search_result.total_count")
        self._assert_not_none(search_result.max_results, "search_result.max_results")

    def test_search_with_results(self):
        """Test search returns expected results."""
        search_result = self._client.search(self._test_index_id, "learning")
        self._assert_not_none(search_result, "search_result")
        results = search_result.results or []
        self._assert_greater_than(len(results), 0, "results count")
        for result in results:
            self._assert_not_none(result.document_id, "result.document_id")
            self._assert_not_none(result.score, "result.score")

    def test_search_multiple_terms(self):
        """Test search with multiple terms."""
        search_result = self._client.search(self._test_index_id, "machine learning")
        self._assert_not_none(search_result, "search_result")
        self._assert_not_none(search_result.results, "search_result.results")

    def test_search_max_results(self):
        """Test search with max results limit."""
        search_result = self._client.search(self._test_index_id, "the", max_results=2)
        self._assert_not_none(search_result, "search_result")
        self._assert_equals(search_result.max_results, 2, "search_result.max_results")

    def test_search_no_results(self):
        """Test search with no matching results."""
        search_result = self._client.search(self._test_index_id, "xyznonexistent12345")
        self._assert_not_none(search_result, "search_result")
        results = search_result.results or []
        self._assert_equals(len(results), 0, "results should be empty")

    def test_search_with_label_filter(self):
        """Test search with label filter."""
        # First add a document with labels
        doc_id = str(uuid.uuid4())
        labels = ["searchtest", "filterable"]
        self._client.add_document(
            self._test_index_id,
            "This document contains searchable content with labels for filter testing.",
            doc_id,
            labels,
            None
        )
        self._test_documents.append(doc_id)

        # Search with matching label filter
        search_result = self._client.search(
            self._test_index_id,
            "searchable",
            100,
            labels=["searchtest"],
            tags=None
        )
        self._assert_not_none(search_result, "search_result")
        self._assert_greater_than(len(search_result.results), 0, "should find documents with matching label")

        # Search with non-matching label filter
        no_match_result = self._client.search(
            self._test_index_id,
            "searchable",
            100,
            labels=["nonexistentlabel99"],
            tags=None
        )
        self._assert_not_none(no_match_result, "no_match_result")
        self._assert_equals(len(no_match_result.results), 0, "should find no documents with non-matching label")

    def test_search_with_tag_filter(self):
        """Test search with tag filter."""
        # First add a document with tags
        doc_id = str(uuid.uuid4())
        tags = {
            "searchcategory": "testfilter",
            "searchpriority": "high"
        }
        self._client.add_document(
            self._test_index_id,
            "This document contains taggable content for tag filter testing.",
            doc_id,
            None,
            tags
        )
        self._test_documents.append(doc_id)

        # Search with matching tag filter
        search_result = self._client.search(
            self._test_index_id,
            "taggable",
            100,
            labels=None,
            tags={"searchcategory": "testfilter"}
        )
        self._assert_not_none(search_result, "search_result")
        self._assert_greater_than(len(search_result.results), 0, "should find documents with matching tag")

        # Search with non-matching tag filter
        no_match_result = self._client.search(
            self._test_index_id,
            "taggable",
            100,
            labels=None,
            tags={"searchcategory": "wrongvalue"}
        )
        self._assert_not_none(no_match_result, "no_match_result")
        self._assert_equals(len(no_match_result.results), 0, "should find no documents with non-matching tag")

    def test_search_with_labels_and_tags(self):
        """Test search with both label and tag filters."""
        # First add a document with both labels and tags
        doc_id = str(uuid.uuid4())
        labels = ["combined", "fulltest"]
        tags = {"combinedcategory": "both"}
        self._client.add_document(
            self._test_index_id,
            "This document has combined labels and tags for comprehensive filter testing.",
            doc_id,
            labels,
            tags
        )
        self._test_documents.append(doc_id)

        # Search with both label and tag filters
        search_result = self._client.search(
            self._test_index_id,
            "comprehensive",
            100,
            labels=["combined"],
            tags={"combinedcategory": "both"}
        )
        self._assert_not_none(search_result, "search_result")
        self._assert_greater_than(len(search_result.results), 0, "should find documents matching both label and tag")

    # ==================== Filtered Enumeration Tests ====================

    def test_list_documents_with_label_filter(self):
        """Test listing documents filtered by label."""
        # Add 3 documents, 2 with a specific label
        doc1 = self._client.add_document(
            self._test_index_id,
            "First labeled document for enumeration filter testing.",
            labels=["enumfilter"]
        )
        self._test_documents.append(doc1.document_id)

        doc2 = self._client.add_document(
            self._test_index_id,
            "Second labeled document for enumeration filter testing.",
            labels=["enumfilter"]
        )
        self._test_documents.append(doc2.document_id)

        doc3 = self._client.add_document(
            self._test_index_id,
            "Third document without the filter label."
        )
        self._test_documents.append(doc3.document_id)

        # List documents with label filter
        options = EnumerationOptions(labels=["enumfilter"])
        result = self._client.list_documents(self._test_index_id, options)
        self._assert_not_none(result, "result")
        self._assert_equals(len(result.objects), 2, "filtered documents count")

    def test_list_documents_with_tag_filter(self):
        """Test listing documents filtered by tag."""
        # Add 3 documents, 2 with a specific tag
        doc1 = self._client.add_document(
            self._test_index_id,
            "First tagged document for enumeration tag filter testing.",
            tags={"enumtag": "yes"}
        )
        self._test_documents.append(doc1.document_id)

        doc2 = self._client.add_document(
            self._test_index_id,
            "Second tagged document for enumeration tag filter testing.",
            tags={"enumtag": "yes"}
        )
        self._test_documents.append(doc2.document_id)

        doc3 = self._client.add_document(
            self._test_index_id,
            "Third document without the filter tag."
        )
        self._test_documents.append(doc3.document_id)

        # List documents with tag filter
        options = EnumerationOptions(tags={"enumtag": "yes"})
        result = self._client.list_documents(self._test_index_id, options)
        self._assert_not_none(result, "result")
        self._assert_equals(len(result.objects), 2, "filtered documents count")

    # ==================== Wildcard Search Tests ====================

    def test_wildcard_search(self):
        """Test wildcard search returns all documents with score 0."""
        # Search with wildcard query "*"
        search_result = self._client.search(self._test_index_id, "*")
        self._assert_not_none(search_result, "search_result")
        self._assert_not_none(search_result.results, "search_result.results")
        self._assert_greater_than(len(search_result.results), 0, "wildcard results count")

        # All wildcard results should have a score of 0
        for result in search_result.results:
            self._assert_not_none(result.document_id, "result.document_id")
            self._assert_equals(result.score, 0, "wildcard result score should be 0")

    def test_wildcard_search_with_filters(self):
        """Test wildcard search with label filter returns only matching documents."""
        # Add a document with a unique label for wildcard filter test
        doc_id = str(uuid.uuid4())
        labels = ["wildcardfilter"]
        self._client.add_document(
            self._test_index_id,
            "Document specifically for wildcard filter testing.",
            doc_id,
            labels,
            None
        )
        self._test_documents.append(doc_id)

        # Wildcard search with label filter
        search_result = self._client.search(
            self._test_index_id,
            "*",
            100,
            labels=["wildcardfilter"],
            tags=None
        )
        self._assert_not_none(search_result, "search_result")
        self._assert_greater_than(len(search_result.results), 0, "wildcard filtered results count")

        # All results should have the matching document
        found = any(r.document_id == doc_id for r in search_result.results)
        self._assert_true(found, "wildcard filter should return the labeled document")

        # All wildcard results should have a score of 0
        for result in search_result.results:
            self._assert_equals(result.score, 0, "wildcard filtered result score should be 0")

    # ==================== Document Deletion Tests ====================

    def test_delete_document(self):
        """Test deleting a document."""
        if len(self._test_documents) == 0:
            raise AssertionError("No test documents to delete")
        doc_id = self._test_documents.pop()
        self._client.delete_document(self._test_index_id, doc_id)
        # If we get here without exception, the delete succeeded

    def test_delete_document_not_found(self):
        """Test deleting a non-existent document."""
        fake_id = str(uuid.uuid4())
        try:
            self._client.delete_document(self._test_index_id, fake_id)
            self._assert(False, "Should have thrown VerbexError for not found")
        except VerbexError as e:
            self._assert_equals(e.status_code, 404, "error.status_code")

    def test_verify_document_deleted(self):
        """Test that deleted document is no longer retrievable."""
        if len(self._test_documents) == 0:
            return  # Skip if no documents left
        doc_id = self._test_documents.pop()
        self._client.delete_document(self._test_index_id, doc_id)
        try:
            self._client.get_document(self._test_index_id, doc_id)
            self._assert(False, "Should have thrown VerbexError for deleted document")
        except VerbexError as e:
            self._assert_equals(e.status_code, 404, "error.status_code")

    # ==================== Index Deletion Tests ====================

    def test_delete_index(self):
        """Test deleting an index."""
        self._client.delete_index(self._test_index_id)
        # If we get here without exception, the delete succeeded

    def test_delete_index_not_found(self):
        """Test deleting a non-existent index."""
        try:
            self._client.delete_index("non-existent-index-67890")
            self._assert(False, "Should have thrown VerbexError for not found")
        except VerbexError as e:
            self._assert_equals(e.status_code, 404, "error.status_code")

    def test_verify_index_deleted(self):
        """Test that deleted index is no longer retrievable."""
        try:
            self._client.get_index(self._test_index_id)
            self._assert(False, "Should have thrown VerbexError for deleted index")
        except VerbexError as e:
            self._assert_equals(e.status_code, 404, "error.status_code")

    def run(self) -> int:
        """Run all tests and return exit code."""
        start_time = time.time()

        self._print_header("Verbex SDK Test Harness - Python")
        print(f"  Endpoint: {self._endpoint}")
        print(f"  Started: {datetime.now().isoformat()}")

        self._client = VerbexClient(self._endpoint, self._access_key)

        try:
            # Health Tests
            self._print_subheader("Health Checks")
            self._run_test("Root health check", self.test_root_health_check)
            self._run_test("Health endpoint", self.test_health_endpoint)

            # Authentication Tests
            self._print_subheader("Authentication")
            self._run_test("Login with credentials (success)", self.test_login_with_credentials_success)
            self._run_test("Login with credentials (invalid)", self.test_login_with_credentials_invalid)
            self._run_test("Login with bearer token (success)", self.test_login_with_token_success)
            self._run_test("Login with bearer token (invalid)", self.test_login_with_token_invalid)
            self._run_test("Validate token", self.test_validate_token)
            self._run_test("Validate invalid token", self.test_validate_invalid_token)

            # Index Management Tests
            self._print_subheader("Index Management")
            self._run_test("List indices (initial)", self.test_list_indices_initial)
            self._run_test("Create index", self.test_create_index)
            self._run_test("Create duplicate index fails", self.test_create_duplicate_index)
            self._run_test("Get index", self.test_get_index)
            self._run_test("Get index not found", self.test_get_index_not_found)
            self._run_test("List indices (after create)", self.test_list_indices_after_create)
            self._run_test("Create index with labels and tags", self.test_create_index_with_labels_and_tags)
            self._run_test("Get index with labels and tags", self.test_get_index_with_labels_and_tags)
            self._run_test("Index exists (HEAD)", self.test_index_exists)
            self._run_test("Index exists not found (HEAD)", self.test_index_exists_not_found)

            # Document Management Tests
            self._print_subheader("Document Management")
            self._run_test("List documents (empty)", self.test_list_documents_empty)
            self._run_test("Add document", self.test_add_document)
            self._run_test("Add document with ID", self.test_add_document_with_id)
            self._run_test("Add multiple documents", self.test_add_multiple_documents)
            self._run_test("List documents (after add)", self.test_list_documents_after_add)
            self._run_test("Get document", self.test_get_document)
            self._run_test("Get document not found", self.test_get_document_not_found)
            self._run_test("Add document with labels and tags", self.test_add_document_with_labels_and_tags)
            self._run_test("Get document with labels and tags", self.test_get_document_with_labels_and_tags)
            self._run_test("Get document returns indexed terms", self.test_get_document_returns_indexed_terms)
            self._run_test("Get documents batch", self.test_get_documents_batch)
            self._run_test("Get documents batch (empty)", self.test_get_documents_batch_empty)
            self._run_test("Document exists (HEAD)", self.test_document_exists)
            self._run_test("Document exists not found (HEAD)", self.test_document_exists_not_found)
            self._run_test("Delete documents batch", self.test_delete_documents_batch)
            self._run_test("Delete documents batch (empty)", self.test_delete_documents_batch_empty)

            # Top Terms Tests
            self._print_subheader("Top Terms")
            self._run_test("Get top terms", self.test_get_top_terms)
            self._run_test("Get top terms default limit", self.test_get_top_terms_default_limit)
            self._run_test("Get top terms custom limit", self.test_get_top_terms_custom_limit)
            self._run_test("Get top terms not found", self.test_get_top_terms_not_found)

            # Search Tests
            self._print_subheader("Search")
            self._run_test("Basic search", self.test_search_basic)
            self._run_test("Search with results", self.test_search_with_results)
            self._run_test("Search multiple terms", self.test_search_multiple_terms)
            self._run_test("Search with max results", self.test_search_max_results)
            self._run_test("Search with no results", self.test_search_no_results)
            self._run_test("Search with label filter", self.test_search_with_label_filter)
            self._run_test("Search with tag filter", self.test_search_with_tag_filter)
            self._run_test("Search with labels and tags", self.test_search_with_labels_and_tags)

            # Cleanup Tests
            self._print_subheader("Cleanup")
            self._run_test("Delete document", self.test_delete_document)
            self._run_test("Delete document not found", self.test_delete_document_not_found)
            self._run_test("Verify document deleted", self.test_verify_document_deleted)
            self._run_test("Delete index", self.test_delete_index)
            self._run_test("Delete index not found", self.test_delete_index_not_found)
            self._run_test("Verify index deleted", self.test_verify_index_deleted)

        except Exception as e:
            print(f"\n  FATAL ERROR: {type(e).__name__}: {str(e)}")
            self._failed += 1
        finally:
            self._client.close()

        # Summary
        duration = time.time() - start_time
        self._print_header("Test Summary")
        print(f"  Total Tests: {self._passed + self._failed}")
        print(f"  Passed: {self._passed}")
        print(f"  Failed: {self._failed}")
        print(f"  Duration: {duration:.2f}s")
        print(f"  Result: {'SUCCESS' if self._failed == 0 else 'FAILURE'}")

        # Failed tests detail
        failed_tests = [r for r in self._results if not r.passed]
        if failed_tests:
            self._print_header("Failed Tests")
            for i, failed in enumerate(failed_tests, 1):
                print(f"  {i}. {failed.name}")
                print(f"     Error: {failed.message}")
                print(f"     Duration: {failed.duration_ms:.2f}ms")
                print()

        return 0 if self._failed == 0 else 1


def main():
    """Main entry point."""
    if len(sys.argv) != 3:
        print("Verbex SDK Test Harness - Python")
        print()
        print("Usage: python test_harness.py <endpoint> <access_key>")
        print()
        print("Arguments:")
        print("  endpoint    The Verbex server endpoint (e.g., http://localhost:8080)")
        print("  access_key  The bearer token for authentication")
        print()
        print("Example:")
        print("  python test_harness.py http://localhost:8080 verbexadmin")
        return 1

    endpoint = sys.argv[1]
    access_key = sys.argv[2]

    harness = TestHarness(endpoint, access_key)
    return harness.run()


if __name__ == "__main__":
    sys.exit(main())
