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
from verbex_sdk import VerbexClient, VerbexError, ApiResponse


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
        self._test_index_id = f"test-index-{uuid.uuid4().hex[:8]}"
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
        response = self._client.root_health_check()
        self._assert_true(response.success, "response.success")
        self._assert_equals(response.status_code, 200, "response.status_code")
        self._assert_not_none(response.data, "response.data")
        self._assert_equals(response.data.get('status'), 'Healthy', "data.status")
        self._assert_not_none(response.data.get('version'), "data.version")
        self._assert_not_none(response.data.get('timestamp'), "data.timestamp")

    def test_health_endpoint(self):
        """Test /v1.0/health endpoint."""
        response = self._client.health_check()
        self._assert_true(response.success, "response.success")
        self._assert_equals(response.status_code, 200, "response.status_code")
        self._assert_not_none(response.data, "response.data")
        self._assert_equals(response.data.get('status'), 'Healthy', "data.status")
        self._assert_not_none(response.data.get('version'), "data.version")
        self._assert_not_none(response.data.get('timestamp'), "data.timestamp")

    # ==================== Authentication Tests ====================

    def test_login_success(self):
        """Test successful login."""
        response = self._client.login("admin", "password")
        self._assert_true(response.success, "response.success")
        self._assert_equals(response.status_code, 200, "response.status_code")
        self._assert_not_none(response.data, "response.data")
        self._assert_not_none(response.data.get('token'), "data.token")
        self._assert_equals(response.data.get('username'), 'admin', "data.username")

    def test_login_invalid_credentials(self):
        """Test login with invalid credentials."""
        try:
            self._client.login("invalid", "invalid")
            self._assert(False, "Should have thrown VerbexError")
        except VerbexError as e:
            self._assert_equals(e.status_code, 401, "error.status_code")
            self._assert_not_none(e.message, "error.message")

    def test_validate_token(self):
        """Test token validation."""
        response = self._client.validate_token()
        self._assert_true(response.success, "response.success")
        self._assert_equals(response.status_code, 200, "response.status_code")
        self._assert_not_none(response.data, "response.data")
        self._assert_true(response.data.get('valid'), "data.valid")

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
        response = self._client.list_indices()
        self._assert_true(response.success, "response.success")
        self._assert_equals(response.status_code, 200, "response.status_code")
        self._assert_not_none(response.data, "response.data")
        self._assert_not_none(response.data.get('indices'), "data.indices")
        self._assert_not_none(response.data.get('count'), "data.count")

    def test_create_index(self):
        """Test creating an index."""
        response = self._client.create_index(
            id=self._test_index_id,
            name="Test Index",
            description="A test index for SDK validation",
            in_memory=True,
            storage_mode="MemoryOnly"
        )
        self._assert_true(response.success, "response.success")
        self._assert_equals(response.status_code, 201, "response.status_code")
        self._assert_not_none(response.data, "response.data")
        self._assert_not_none(response.data.get('message'), "data.message")
        self._assert_not_none(response.data.get('index'), "data.index")
        index_data = response.data.get('index')
        self._assert_equals(index_data.get('id'), self._test_index_id, "index.id")
        self._assert_equals(index_data.get('name'), "Test Index", "index.name")

    def test_create_duplicate_index(self):
        """Test creating a duplicate index fails."""
        try:
            self._client.create_index(id=self._test_index_id, name="Duplicate")
            self._assert(False, "Should have thrown VerbexError for duplicate")
        except VerbexError as e:
            self._assert_equals(e.status_code, 409, "error.status_code")

    def test_get_index(self):
        """Test getting index details."""
        response = self._client.get_index(self._test_index_id)
        self._assert_true(response.success, "response.success")
        self._assert_equals(response.status_code, 200, "response.status_code")
        self._assert_not_none(response.data, "response.data")
        self._assert_equals(response.data.get('id'), self._test_index_id, "data.id")
        self._assert_equals(response.data.get('name'), "Test Index", "data.name")
        self._assert_not_none(response.data.get('createdUtc'), "data.createdUtc")

    def test_get_index_not_found(self):
        """Test getting a non-existent index."""
        try:
            self._client.get_index("non-existent-index-12345")
            self._assert(False, "Should have thrown VerbexError for not found")
        except VerbexError as e:
            self._assert_equals(e.status_code, 404, "error.status_code")

    def test_list_indices_after_create(self):
        """Test listing indices includes new index."""
        indices = self._client.get_indices()
        found = any(idx.id == self._test_index_id for idx in indices)
        self._assert_true(found, "test index should be in list")

    def test_create_index_with_labels_and_tags(self):
        """Test creating an index with labels and tags."""
        index_id = f"test-labeled-{uuid.uuid4().hex[:8]}"
        labels = ["test", "labeled"]
        tags = {"environment": "testing", "owner": "sdk-harness"}
        response = self._client.create_index(
            id=index_id,
            name="Labeled Test Index",
            description="An index with labels and tags",
            in_memory=True,
            storage_mode="MemoryOnly",
            labels=labels,
            tags=tags
        )
        self._assert_true(response.success, "response.success")
        self._assert_equals(response.status_code, 201, "response.status_code")
        self._assert_not_none(response.data, "response.data")
        self._assert_not_none(response.data.get('index'), "data.index")
        # Clean up
        self._client.delete_index(index_id)

    def test_get_index_with_labels_and_tags(self):
        """Test getting an index with labels and tags."""
        index_id = f"test-labeled-get-{uuid.uuid4().hex[:8]}"
        labels = ["retrieval", "test"]
        tags = {"purpose": "verification", "version": "1.0"}
        self._client.create_index(
            id=index_id,
            name="Get Labeled Index",
            in_memory=True,
            labels=labels,
            tags=tags
        )
        response = self._client.get_index(index_id)
        self._assert_true(response.success, "response.success")
        self._assert_not_none(response.data, "response.data")
        self._assert_not_none(response.data.get('labels'), "data.labels")
        self._assert_not_none(response.data.get('tags'), "data.tags")
        self._assert_equals(len(response.data.get('labels')), 2, "labels count")
        self._assert_equals(len(response.data.get('tags')), 2, "tags count")
        # Clean up
        self._client.delete_index(index_id)

    # ==================== Document Management Tests ====================

    def test_list_documents_empty(self):
        """Test listing documents on empty index."""
        response = self._client.list_documents(self._test_index_id)
        self._assert_true(response.success, "response.success")
        self._assert_equals(response.status_code, 200, "response.status_code")
        self._assert_not_none(response.data, "response.data")
        self._assert_not_none(response.data.get('documents'), "data.documents")
        self._assert_equals(response.data.get('count'), 0, "data.count")

    def test_add_document(self):
        """Test adding a document."""
        response = self._client.add_document(
            self._test_index_id,
            "The quick brown fox jumps over the lazy dog."
        )
        self._assert_true(response.success, "response.success")
        self._assert_equals(response.status_code, 201, "response.status_code")
        self._assert_not_none(response.data, "response.data")
        self._assert_not_none(response.data.get('documentId'), "data.documentId")
        self._assert_not_none(response.data.get('message'), "data.message")
        self._test_documents.append(response.data.get('documentId'))

    def test_add_document_with_id(self):
        """Test adding a document with explicit ID."""
        doc_id = str(uuid.uuid4())
        response = self._client.add_document(
            self._test_index_id,
            "Python is a versatile programming language used for web development, data science, and automation.",
            document_id=doc_id
        )
        self._assert_true(response.success, "response.success")
        self._assert_equals(response.status_code, 201, "response.status_code")
        self._assert_equals(response.data.get('documentId'), doc_id, "data.documentId")
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
            response = self._client.add_document(self._test_index_id, content)
            self._assert_true(response.success, "response.success")
            self._test_documents.append(response.data.get('documentId'))

    def test_list_documents_after_add(self):
        """Test listing documents after adding."""
        response = self._client.list_documents(self._test_index_id)
        self._assert_true(response.success, "response.success")
        self._assert_equals(response.data.get('count'), len(self._test_documents), "data.count")
        docs = response.data.get('documents')
        self._assert_equals(len(docs), len(self._test_documents), "documents length")
        for doc in docs:
            self._assert_not_none(doc.get('id'), "document.id")

    def test_get_document(self):
        """Test getting a specific document."""
        doc_id = self._test_documents[0]
        response = self._client.get_document(self._test_index_id, doc_id)
        self._assert_true(response.success, "response.success")
        self._assert_equals(response.status_code, 200, "response.status_code")
        self._assert_not_none(response.data, "response.data")
        self._assert_equals(response.data.get('id'), doc_id, "data.id")

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
        response = self._client.add_document(
            self._test_index_id,
            "This document has labels and tags for testing metadata support.",
            labels=labels,
            tags=tags
        )
        self._assert_true(response.success, "response.success")
        self._assert_equals(response.status_code, 201, "response.status_code")
        self._assert_not_none(response.data, "response.data")
        self._assert_not_none(response.data.get('documentId'), "data.documentId")
        self._test_documents.append(response.data.get('documentId'))

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
        response = self._client.get_document(self._test_index_id, doc_id)
        self._assert_true(response.success, "response.success")
        self._assert_not_none(response.data, "response.data")
        self._assert_not_none(response.data.get('labels'), "data.labels")
        self._assert_not_none(response.data.get('tags'), "data.tags")
        self._assert_equals(len(response.data.get('labels')), 2, "labels count")
        self._assert_equals(len(response.data.get('tags')), 2, "tags count")
        self._test_documents.append(doc_id)

    # ==================== Search Tests ====================

    def test_search_basic(self):
        """Test basic search functionality."""
        response = self._client.search(self._test_index_id, "fox")
        self._assert_true(response.success, "response.success")
        self._assert_equals(response.status_code, 200, "response.status_code")
        self._assert_not_none(response.data, "response.data")
        self._assert_equals(response.data.get('query'), 'fox', "data.query")
        self._assert_not_none(response.data.get('results'), "data.results")
        self._assert_not_none(response.data.get('totalCount'), "data.totalCount")
        self._assert_not_none(response.data.get('maxResults'), "data.maxResults")

    def test_search_with_results(self):
        """Test search returns expected results."""
        response = self._client.search(self._test_index_id, "learning")
        self._assert_true(response.success, "response.success")
        results = response.data.get('results', [])
        self._assert_greater_than(len(results), 0, "results count")
        for result in results:
            self._assert_not_none(result.get('documentId'), "result.documentId")
            self._assert_not_none(result.get('score'), "result.score")

    def test_search_multiple_terms(self):
        """Test search with multiple terms."""
        response = self._client.search(self._test_index_id, "machine learning")
        self._assert_true(response.success, "response.success")
        self._assert_not_none(response.data.get('results'), "data.results")

    def test_search_max_results(self):
        """Test search with max results limit."""
        response = self._client.search(self._test_index_id, "the", max_results=2)
        self._assert_true(response.success, "response.success")
        self._assert_equals(response.data.get('maxResults'), 2, "data.maxResults")

    def test_search_no_results(self):
        """Test search with no matching results."""
        response = self._client.search(self._test_index_id, "xyznonexistent12345")
        self._assert_true(response.success, "response.success")
        results = response.data.get('results', [])
        self._assert_equals(len(results), 0, "results should be empty")

    def test_search_documents_helper(self):
        """Test search using helper method."""
        search_response = self._client.search_documents(self._test_index_id, "programming")
        self._assert_not_none(search_response, "search_response")
        self._assert_equals(search_response.query, "programming", "query")
        self._assert_not_none(search_response.results, "results")
        self._assert_not_none(search_response.total_count, "total_count")

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
        response = self._client.search(
            self._test_index_id,
            "searchable",
            100,
            labels=["searchtest"],
            tags=None
        )
        self._assert_true(response.success, "response.success")
        self._assert_greater_than(len(response.data.get('results', [])), 0, "should find documents with matching label")

        # Search with non-matching label filter
        no_match_response = self._client.search(
            self._test_index_id,
            "searchable",
            100,
            labels=["nonexistentlabel99"],
            tags=None
        )
        self._assert_true(no_match_response.success, "no_match_response.success")
        self._assert_equals(len(no_match_response.data.get('results', [])), 0, "should find no documents with non-matching label")

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
        response = self._client.search(
            self._test_index_id,
            "taggable",
            100,
            labels=None,
            tags={"searchcategory": "testfilter"}
        )
        self._assert_true(response.success, "response.success")
        self._assert_greater_than(len(response.data.get('results', [])), 0, "should find documents with matching tag")

        # Search with non-matching tag filter
        no_match_response = self._client.search(
            self._test_index_id,
            "taggable",
            100,
            labels=None,
            tags={"searchcategory": "wrongvalue"}
        )
        self._assert_true(no_match_response.success, "no_match_response.success")
        self._assert_equals(len(no_match_response.data.get('results', [])), 0, "should find no documents with non-matching tag")

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
        response = self._client.search(
            self._test_index_id,
            "comprehensive",
            100,
            labels=["combined"],
            tags={"combinedcategory": "both"}
        )
        self._assert_true(response.success, "response.success")
        self._assert_greater_than(len(response.data.get('results', [])), 0, "should find documents matching both label and tag")

    # ==================== Document Deletion Tests ====================

    def test_delete_document(self):
        """Test deleting a document."""
        if len(self._test_documents) == 0:
            raise AssertionError("No test documents to delete")
        doc_id = self._test_documents.pop()
        response = self._client.delete_document(self._test_index_id, doc_id)
        self._assert_true(response.success, "response.success")
        self._assert_equals(response.status_code, 200, "response.status_code")
        self._assert_not_none(response.data, "response.data")
        self._assert_equals(response.data.get('documentId'), doc_id, "data.documentId")
        self._assert_not_none(response.data.get('message'), "data.message")

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
        response = self._client.delete_index(self._test_index_id)
        self._assert_true(response.success, "response.success")
        self._assert_equals(response.status_code, 200, "response.status_code")
        self._assert_not_none(response.data, "response.data")
        self._assert_equals(response.data.get('indexId'), self._test_index_id, "data.indexId")
        self._assert_not_none(response.data.get('message'), "data.message")

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
        print(f"  Test Index: {self._test_index_id}")
        print(f"  Started: {datetime.now().isoformat()}")

        self._client = VerbexClient(self._endpoint, self._access_key)

        try:
            # Health Tests
            self._print_subheader("Health Checks")
            self._run_test("Root health check", self.test_root_health_check)
            self._run_test("Health endpoint", self.test_health_endpoint)

            # Authentication Tests
            self._print_subheader("Authentication")
            self._run_test("Login with valid credentials", self.test_login_success)
            self._run_test("Login with invalid credentials", self.test_login_invalid_credentials)
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

            # Search Tests
            self._print_subheader("Search")
            self._run_test("Basic search", self.test_search_basic)
            self._run_test("Search with results", self.test_search_with_results)
            self._run_test("Search multiple terms", self.test_search_multiple_terms)
            self._run_test("Search with max results", self.test_search_max_results)
            self._run_test("Search with no results", self.test_search_no_results)
            self._run_test("Search documents helper", self.test_search_documents_helper)
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
