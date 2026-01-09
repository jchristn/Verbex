# BIG_CHANGES.md - Database Backend & Multi-Tenancy Implementation Plan

## Overview

This document outlines the implementation plan for two major architectural changes to Verbex:

1. **Database Backend Abstraction**: Move from file-system backed SQLite to a pluggable database backend supporting SQLite, PostgreSQL, MySQL, and SQL Server
2. **Multi-Tenancy Support**: Add tenant isolation, user management, and credential-based authentication

**Reference Implementations**:
- `C:\code\lattice\src\Lattice.Core` (Repositories pattern)
- `C:\code\Omnivec\src\Omnivec.Core` (Database pattern, Multi-tenancy model)
- `C:\code\CommittedCoaches\Chronos\src\Chronos.Core` (Database pattern)

---

## Progress Summary

| Phase | Status | Progress |
|-------|--------|----------|
| Phase 1: Core Infrastructure | Not Started | 0/5 |
| Phase 2: Models | Not Started | 0/6 |
| Phase 3: Interfaces | Not Started | 0/9 |
| Phase 4: SQLite Implementation | Not Started | 0/23 |
| Phase 5: PostgreSQL Implementation | Not Started | 0/23 |
| Phase 6: MySQL Implementation | Not Started | 0/23 |
| Phase 7: SQL Server Implementation | Not Started | 0/23 |
| Phase 8: Integration | Not Started | 0/10 |
| Phase 9: Test Project Updates | Not Started | 0/19 |
| Phase 10: TestConsole Updates | Not Started | 0/5 |
| Phase 11: New Tests & Documentation | Not Started | 0/8 |
| **Total** | **Not Started** | **0/162** |

---

## Phase 1: Core Infrastructure

**Status**: Not Started | **Progress**: 0/5

### Tasks

- [ ] **1.1** Create folder structure `src/Verbex/Database/`
  - [ ] Create `Database/` folder
  - [ ] Create `Database/Interfaces/` folder
  - [ ] Create `Database/Sqlite/` folder
  - [ ] Create `Database/Sqlite/Implementations/` folder
  - [ ] Create `Database/Sqlite/Queries/` folder
  - [ ] Create `Database/Postgresql/` folder (with subfolders)
  - [ ] Create `Database/Mysql/` folder (with subfolders)
  - [ ] Create `Database/SqlServer/` folder (with subfolders)

- [ ] **1.2** Create `DatabaseTypeEnum.cs`
  - File: `src/Verbex/Database/DatabaseTypeEnum.cs`
  - Enum values: `Sqlite`, `Postgresql`, `Mysql`, `SqlServer`
  - Add JSON serialization attributes

- [ ] **1.3** Create `DatabaseSettings.cs`
  - File: `src/Verbex/Database/DatabaseSettings.cs`
  - Properties: Type, Filename, InMemory, Hostname, Port, DatabaseName, Username, Password, RequireEncryption, Schema, MinPoolSize, MaxPoolSize, CommandTimeout, ConnectionTimeout
  - Methods: GetDefaultPort(), Validate()

- [ ] **1.4** Create `DatabaseDriverBase.cs`
  - File: `src/Verbex/Database/DatabaseDriverBase.cs`
  - Abstract class implementing IDisposable, IAsyncDisposable
  - Properties for all 10 interface implementations
  - Abstract methods: ExecuteQueryAsync(), ExecuteQueriesAsync(), InitializeAsync()

- [ ] **1.5** Update `IdGenerator.cs` with new prefixes
  - File: `src/Verbex/Utilities/IdGenerator.cs`
  - Add: GenerateTenantId() → `ten_`
  - Add: GenerateUserId() → `usr_`
  - Add: GenerateCredentialId() → `cred_`
  - Add: GenerateAdministratorId() → `admin_`
  - Add: GenerateIndexId() → `idx_`

---

## Phase 2: Models

**Status**: Not Started | **Progress**: 0/6

### Tasks

- [ ] **2.1** Create `TenantMetadata.cs`
  - File: `src/Verbex/Models/TenantMetadata.cs`
  - Properties: Identifier, TenantId (alias), Name, Description, Active, CreatedUtc, LastUpdateUtc
  - Constructors: default (auto-generate ID), with name

- [ ] **2.2** Create `UserMaster.cs`
  - File: `src/Verbex/Models/UserMaster.cs`
  - Properties: Identifier, TenantId, Email, PasswordSha256, FirstName, LastName, IsAdmin, Active, CreatedUtc, LastUpdateUtc
  - Methods: ComputePasswordHash(), VerifyPassword(), SetPassword()

- [ ] **2.3** Create `Credential.cs`
  - File: `src/Verbex/Models/Credential.cs`
  - Properties: Identifier, TenantId, UserId, BearerToken, Name, Active, CreatedUtc, LastUpdateUtc
  - Methods: RegenerateBearerToken()
  - Private: GenerateBearerToken() (64-char cryptographically secure)

- [ ] **2.4** Create `Administrator.cs`
  - File: `src/Verbex/Models/Administrator.cs`
  - Properties: Identifier, Email, PasswordSha256, FirstName, LastName, Active, CreatedUtc, LastUpdateUtc
  - Methods: ComputePasswordHash(), VerifyPassword(), SetPassword()

- [ ] **2.5** Create `AuthenticationContext.cs`
  - File: `src/Verbex/Models/AuthenticationContext.cs`
  - Properties: TenantId, Email, Password, PasswordSha256, BearerToken, Administrator, Tenant, User, Credential, Result, ErrorMessage
  - Computed: IsAuthenticated, IsGlobalAdmin, IsTenantAdmin, HasAdminPrivileges
  - Methods: CanAccessTenant(), CanManageTenant(), ClearSensitiveData()

- [ ] **2.6** Create `AuthenticationResultEnum.cs`
  - File: `src/Verbex/Models/AuthenticationResultEnum.cs`
  - Values: Success, NotAuthenticated, MissingCredentials, NotFound, Inactive, InvalidCredentials, TenantNotFound

---

## Phase 3: Interfaces

**Status**: Not Started | **Progress**: 0/9

### Tasks

- [ ] **3.1** Create `ITenantMethods.cs`
  - File: `src/Verbex/Database/Interfaces/ITenantMethods.cs`
  - Methods: CreateAsync, ReadByIdentifierAsync, ReadByNameAsync, ReadManyAsync, UpdateAsync, DeleteByIdentifierAsync, ExistsByIdentifierAsync, ExistsByNameAsync, GetRecordCountAsync

- [ ] **3.2** Create `IUserMethods.cs`
  - File: `src/Verbex/Database/Interfaces/IUserMethods.cs`
  - Methods: CreateAsync, ReadByIdentifierAsync(tenantId, id), ReadByEmailAsync(tenantId, email), ReadManyAsync(tenantId), UpdateAsync, DeleteByIdentifierAsync, DeleteByTenantAsync, ExistsByIdentifierAsync, ExistsByEmailAsync, GetRecordCountAsync

- [ ] **3.3** Create `ICredentialMethods.cs`
  - File: `src/Verbex/Database/Interfaces/ICredentialMethods.cs`
  - Methods: CreateAsync, ReadByIdentifierAsync, ReadByBearerTokenAsync (global), ReadByUserAsync, ReadManyAsync, UpdateAsync, DeleteByIdentifierAsync, DeleteByUserAsync, DeleteByTenantAsync, ExistsByIdentifierAsync, ExistsByBearerTokenAsync, GetRecordCountAsync

- [ ] **3.4** Create `IAdministratorMethods.cs`
  - File: `src/Verbex/Database/Interfaces/IAdministratorMethods.cs`
  - Methods: CreateAsync, ReadByIdentifierAsync, ReadByEmailAsync, ReadManyAsync, UpdateAsync, DeleteByIdentifierAsync, ExistsByIdentifierAsync, ExistsByEmailAsync, GetRecordCountAsync

- [ ] **3.5** Migrate/Update `IDocumentMethods.cs`
  - File: `src/Verbex/Database/Interfaces/IDocumentMethods.cs`
  - Add tenant_id and index_id parameters to all methods
  - Move from existing Repositories/Interfaces location

- [ ] **3.6** Migrate/Update `ITermMethods.cs`
  - File: `src/Verbex/Database/Interfaces/ITermMethods.cs`
  - Add tenant_id and index_id parameters to all methods

- [ ] **3.7** Migrate/Update `IDocumentTermMethods.cs`
  - File: `src/Verbex/Database/Interfaces/IDocumentTermMethods.cs`
  - Update for new schema relationships

- [ ] **3.8** Migrate/Update `ILabelMethods.cs` and `ITagMethods.cs`
  - Files: `src/Verbex/Database/Interfaces/ILabelMethods.cs`, `ITagMethods.cs`
  - Update for index-level scope

- [ ] **3.9** Migrate/Update `IStatisticsMethods.cs`
  - File: `src/Verbex/Database/Interfaces/IStatisticsMethods.cs`
  - Add tenant_id parameter for scoped statistics

---

## Phase 4: SQLite Implementation

**Status**: Not Started | **Progress**: 0/23

### Driver & Infrastructure

- [ ] **4.1** Create `SqliteDatabaseDriver.cs`
  - File: `src/Verbex/Database/Sqlite/SqliteDatabaseDriver.cs`
  - Implement DatabaseDriverBase
  - Connection management with SemaphoreSlim
  - Support for in-memory and file-based modes
  - PRAGMA configuration (WAL, synchronous, cache_size, etc.)
  - Initialize all 10 implementation instances

- [ ] **4.2** Create `Sanitizer.cs`
  - File: `src/Verbex/Database/Sqlite/Sanitizer.cs`
  - SQL injection prevention for SQLite

### Query Builders

- [ ] **4.3** Create `SetupQueries.cs`
  - File: `src/Verbex/Database/Sqlite/Queries/SetupQueries.cs`
  - Schema v3.0 with all tables (tenants, administrators, users, credentials, indexes, documents, terms, document_terms, labels, tags)
  - All indexes
  - Migration statements

- [ ] **4.4** Create `TenantQueries.cs`
  - File: `src/Verbex/Database/Sqlite/Queries/TenantQueries.cs`

- [ ] **4.5** Create `UserQueries.cs`
  - File: `src/Verbex/Database/Sqlite/Queries/UserQueries.cs`

- [ ] **4.6** Create `CredentialQueries.cs`
  - File: `src/Verbex/Database/Sqlite/Queries/CredentialQueries.cs`

- [ ] **4.7** Create `AdministratorQueries.cs`
  - File: `src/Verbex/Database/Sqlite/Queries/AdministratorQueries.cs`

- [ ] **4.8** Migrate `DocumentQueries.cs`
  - File: `src/Verbex/Database/Sqlite/Queries/DocumentQueries.cs`
  - Update for tenant_id, index_id

- [ ] **4.9** Migrate `TermQueries.cs`
  - File: `src/Verbex/Database/Sqlite/Queries/TermQueries.cs`
  - Update for tenant_id, index_id

- [ ] **4.10** Migrate `DocumentTermQueries.cs`
  - File: `src/Verbex/Database/Sqlite/Queries/DocumentTermQueries.cs`

- [ ] **4.11** Migrate `LabelQueries.cs`
  - File: `src/Verbex/Database/Sqlite/Queries/LabelQueries.cs`

- [ ] **4.12** Migrate `TagQueries.cs`
  - File: `src/Verbex/Database/Sqlite/Queries/TagQueries.cs`

- [ ] **4.13** Migrate `StatisticsQueries.cs`
  - File: `src/Verbex/Database/Sqlite/Queries/StatisticsQueries.cs`

### Implementation Classes

- [ ] **4.14** Create `TenantMethods.cs`
  - File: `src/Verbex/Database/Sqlite/Implementations/TenantMethods.cs`

- [ ] **4.15** Create `UserMethods.cs`
  - File: `src/Verbex/Database/Sqlite/Implementations/UserMethods.cs`

- [ ] **4.16** Create `CredentialMethods.cs`
  - File: `src/Verbex/Database/Sqlite/Implementations/CredentialMethods.cs`

- [ ] **4.17** Create `AdministratorMethods.cs`
  - File: `src/Verbex/Database/Sqlite/Implementations/AdministratorMethods.cs`

- [ ] **4.18** Migrate `DocumentMethods.cs`
  - File: `src/Verbex/Database/Sqlite/Implementations/DocumentMethods.cs`

- [ ] **4.19** Migrate `TermMethods.cs`
  - File: `src/Verbex/Database/Sqlite/Implementations/TermMethods.cs`

- [ ] **4.20** Migrate `DocumentTermMethods.cs`
  - File: `src/Verbex/Database/Sqlite/Implementations/DocumentTermMethods.cs`

- [ ] **4.21** Migrate `LabelMethods.cs`
  - File: `src/Verbex/Database/Sqlite/Implementations/LabelMethods.cs`

- [ ] **4.22** Migrate `TagMethods.cs`
  - File: `src/Verbex/Database/Sqlite/Implementations/TagMethods.cs`

- [ ] **4.23** Migrate `StatisticsMethods.cs`
  - File: `src/Verbex/Database/Sqlite/Implementations/StatisticsMethods.cs`

---

## Phase 5: PostgreSQL Implementation

**Status**: Not Started | **Progress**: 0/23

### Driver & Infrastructure

- [ ] **5.1** Add Npgsql NuGet package
  - Package: `Npgsql` version 8.0.0+

- [ ] **5.2** Create `PostgresqlDatabaseDriver.cs`
  - File: `src/Verbex/Database/Postgresql/PostgresqlDatabaseDriver.cs`
  - NpgsqlConnection management
  - Connection string builder
  - Schema support

- [ ] **5.3** Create `Sanitizer.cs`
  - File: `src/Verbex/Database/Postgresql/Sanitizer.cs`

### Query Builders

- [ ] **5.4** Create `SetupQueries.cs`
  - File: `src/Verbex/Database/Postgresql/Queries/SetupQueries.cs`
  - PostgreSQL DDL (SERIAL, BOOLEAN, TIMESTAMPTZ, JSONB)

- [ ] **5.5** Create `TenantQueries.cs`
- [ ] **5.6** Create `UserQueries.cs`
- [ ] **5.7** Create `CredentialQueries.cs`
- [ ] **5.8** Create `AdministratorQueries.cs`
- [ ] **5.9** Create `DocumentQueries.cs` (PostgreSQL syntax)
- [ ] **5.10** Create `TermQueries.cs`
- [ ] **5.11** Create `DocumentTermQueries.cs`
- [ ] **5.12** Create `LabelQueries.cs`
- [ ] **5.13** Create `TagQueries.cs`
- [ ] **5.14** Create `StatisticsQueries.cs`

### Implementation Classes

- [ ] **5.15** Create `TenantMethods.cs`
- [ ] **5.16** Create `UserMethods.cs`
- [ ] **5.17** Create `CredentialMethods.cs`
- [ ] **5.18** Create `AdministratorMethods.cs`
- [ ] **5.19** Create `DocumentMethods.cs`
- [ ] **5.20** Create `TermMethods.cs`
- [ ] **5.21** Create `DocumentTermMethods.cs`
- [ ] **5.22** Create `LabelMethods.cs`
- [ ] **5.23** Create `TagMethods.cs` and `StatisticsMethods.cs`

---

## Phase 6: MySQL Implementation

**Status**: Not Started | **Progress**: 0/23

### Driver & Infrastructure

- [ ] **6.1** Add MySqlConnector NuGet package
  - Package: `MySqlConnector` version 2.3.0+

- [ ] **6.2** Create `MysqlDatabaseDriver.cs`
  - File: `src/Verbex/Database/Mysql/MysqlDatabaseDriver.cs`
  - MySqlConnection management
  - Connection string builder

- [ ] **6.3** Create `Sanitizer.cs`
  - File: `src/Verbex/Database/Mysql/Sanitizer.cs`

### Query Builders

- [ ] **6.4** Create `SetupQueries.cs`
  - File: `src/Verbex/Database/Mysql/Queries/SetupQueries.cs`
  - MySQL DDL (AUTO_INCREMENT, TINYINT, DATETIME, backtick quoting)

- [ ] **6.5** Create `TenantQueries.cs`
- [ ] **6.6** Create `UserQueries.cs`
- [ ] **6.7** Create `CredentialQueries.cs`
- [ ] **6.8** Create `AdministratorQueries.cs`
- [ ] **6.9** Create `DocumentQueries.cs` (MySQL syntax - no RETURNING)
- [ ] **6.10** Create `TermQueries.cs`
- [ ] **6.11** Create `DocumentTermQueries.cs`
- [ ] **6.12** Create `LabelQueries.cs`
- [ ] **6.13** Create `TagQueries.cs`
- [ ] **6.14** Create `StatisticsQueries.cs`

### Implementation Classes

- [ ] **6.15** Create `TenantMethods.cs`
- [ ] **6.16** Create `UserMethods.cs`
- [ ] **6.17** Create `CredentialMethods.cs`
- [ ] **6.18** Create `AdministratorMethods.cs`
- [ ] **6.19** Create `DocumentMethods.cs`
- [ ] **6.20** Create `TermMethods.cs`
- [ ] **6.21** Create `DocumentTermMethods.cs`
- [ ] **6.22** Create `LabelMethods.cs`
- [ ] **6.23** Create `TagMethods.cs` and `StatisticsMethods.cs`

---

## Phase 7: SQL Server Implementation

**Status**: Not Started | **Progress**: 0/23

### Driver & Infrastructure

- [ ] **7.1** Add Microsoft.Data.SqlClient NuGet package
  - Package: `Microsoft.Data.SqlClient` version 5.2.0+

- [ ] **7.2** Create `SqlServerDatabaseDriver.cs`
  - File: `src/Verbex/Database/SqlServer/SqlServerDatabaseDriver.cs`
  - SqlConnection management
  - Connection string builder

- [ ] **7.3** Create `Sanitizer.cs`
  - File: `src/Verbex/Database/SqlServer/Sanitizer.cs`

### Query Builders

- [ ] **7.4** Create `SetupQueries.cs`
  - File: `src/Verbex/Database/SqlServer/Queries/SetupQueries.cs`
  - SQL Server DDL (IDENTITY, BIT, DATETIME2, bracket quoting, OFFSET FETCH)

- [ ] **7.5** Create `TenantQueries.cs`
- [ ] **7.6** Create `UserQueries.cs`
- [ ] **7.7** Create `CredentialQueries.cs`
- [ ] **7.8** Create `AdministratorQueries.cs`
- [ ] **7.9** Create `DocumentQueries.cs` (SQL Server syntax - OUTPUT clause)
- [ ] **7.10** Create `TermQueries.cs`
- [ ] **7.11** Create `DocumentTermQueries.cs`
- [ ] **7.12** Create `LabelQueries.cs`
- [ ] **7.13** Create `TagQueries.cs`
- [ ] **7.14** Create `StatisticsQueries.cs`

### Implementation Classes

- [ ] **7.15** Create `TenantMethods.cs`
- [ ] **7.16** Create `UserMethods.cs`
- [ ] **7.17** Create `CredentialMethods.cs`
- [ ] **7.18** Create `AdministratorMethods.cs`
- [ ] **7.19** Create `DocumentMethods.cs`
- [ ] **7.20** Create `TermMethods.cs`
- [ ] **7.21** Create `DocumentTermMethods.cs`
- [ ] **7.22** Create `LabelMethods.cs`
- [ ] **7.23** Create `TagMethods.cs` and `StatisticsMethods.cs`

---

## Phase 8: Integration

**Status**: Not Started | **Progress**: 0/10

### Tasks

- [ ] **8.1** Create database driver factory
  - File: `src/Verbex/Database/DatabaseDriverFactory.cs`
  - Switch on DatabaseTypeEnum to instantiate correct driver

- [ ] **8.2** Update `VerbexConfiguration.cs`
  - Add DatabaseSettings property
  - Deprecate StorageMode, StorageDirectory, DatabaseFilename
  - Add migration path for existing configurations

- [ ] **8.3** Update `InvertedIndex.cs`
  - Replace IIndexRepository with DatabaseDriverBase
  - Update constructor to use factory
  - Add tenant_id parameter to all public methods
  - Update internal calls to use new driver

- [ ] **8.4** Remove deprecated files
  - Delete: `IIndexRepository.cs`
  - Delete: `SqliteIndexRepository.cs`
  - Delete: `MemoryIndexRepository.cs`
  - Delete: `DiskIndexRepository.cs`
  - Delete: Old `Repositories/` folder structure

- [ ] **8.5** Update `Verbex.Server` for authentication
  - Add authentication middleware
  - Implement bearer token validation
  - Add tenant context to requests
  - Update all endpoints for tenant isolation

- [ ] **8.6** Add tenant/user/credential management endpoints
  - POST/GET/PUT/DELETE `/api/v1/tenants`
  - POST/GET/PUT/DELETE `/api/v1/users`
  - POST/GET/PUT/DELETE `/api/v1/credentials`
  - POST/GET/PUT/DELETE `/api/v1/administrators`

- [ ] **8.7** Update `VerbexCli` for multi-tenancy
  - Add tenant management commands
  - Add user management commands
  - Add credential management commands
  - Add --tenant-id flag to existing commands

- [ ] **8.8** Update C# SDK (`sdk/csharp/`)
  - Update client for authentication headers
  - Add tenant/user/credential management methods

- [ ] **8.9** Update JavaScript SDK (`sdk/js/`)
  - Update client for authentication headers
  - Add tenant/user/credential management methods

- [ ] **8.10** Update Python SDK (`sdk/python/`)
  - Update client for authentication headers
  - Add tenant/user/credential management methods

---

## Phase 9: Test Project Updates

**Status**: Not Started | **Progress**: 0/19

Update existing test files in `src/Test/` for new architecture.

### Test Infrastructure

- [ ] **9.1** Update `TestContext.cs`
  - File: `src/Test/TestContext.cs`
  - Add tenant context support
  - Update for DatabaseDriverBase instead of IIndexRepository
  - Add default tenant/user creation for tests

- [ ] **9.2** Update `TestHelpers.cs`
  - File: `src/Test/TestHelpers.cs`
  - Add helpers for multi-tenant test setup
  - Add helpers for authentication context

- [ ] **9.3** Update `TestRunner.cs`
  - File: `src/Test/TestRunner.cs`
  - Add test categories for multi-tenancy
  - Add database type selection for cross-DB testing

- [ ] **9.4** Update `Program.cs`
  - File: `src/Test/Program.cs`
  - Add command-line options for database type
  - Add tenant isolation test suite

### Existing Test File Updates

- [ ] **9.5** Update `InvertedIndexBasicTests.cs`
  - File: `src/Test/InvertedIndexBasicTests.cs`
  - Add tenant_id to all test operations
  - Update for new InvertedIndex constructor

- [ ] **9.6** Update `InvertedIndexStatisticsTests.cs`
  - File: `src/Test/InvertedIndexStatisticsTests.cs`
  - Add tenant_id to statistics queries
  - Test tenant-scoped statistics

- [ ] **9.7** Update `InvertedIndexDisposableTests.cs`
  - File: `src/Test/InvertedIndexDisposableTests.cs`
  - Update for DatabaseDriverBase disposal
  - Test connection cleanup per database type

- [ ] **9.8** Update `ConfigurationTests.cs`
  - File: `src/Test/ConfigurationTests.cs`
  - Replace StorageMode tests with DatabaseTypeEnum tests
  - Add DatabaseSettings validation tests
  - Test all four database configurations

- [ ] **9.9** Update `StorageModeTests.cs`
  - File: `src/Test/StorageModeTests.cs`
  - Rename to `DatabaseDriverTests.cs`
  - Test SQLite in-memory vs file modes
  - Test driver factory instantiation

- [ ] **9.10** Update `TextProcessingTests.cs`
  - File: `src/Test/TextProcessingTests.cs`
  - No major changes expected (tokenizer/lemmatizer unchanged)
  - Verify compatibility with new architecture

- [ ] **9.11** Update `SearchFilterTests.cs`
  - File: `src/Test/SearchFilterTests.cs`
  - Add tenant_id to search operations
  - Test cross-tenant search isolation

- [ ] **9.12** Update `DocumentMetadataRetrievalTests.cs`
  - File: `src/Test/DocumentMetadataRetrievalTests.cs`
  - Add tenant_id to document retrieval
  - Test TenantId and IndexId properties on documents

- [ ] **9.13** Update `LabelsAndTagsTests.cs`
  - File: `src/Test/LabelsAndTagsTests.cs`
  - Add tenant_id to label/tag operations
  - Test index-level labels/tags

- [ ] **9.14** Update `LabelsAndTagsUpdateTests.cs`
  - File: `src/Test/LabelsAndTagsUpdateTests.cs`
  - Add tenant_id to update operations

- [ ] **9.15** Update `MetadataFilterTests.cs`
  - File: `src/Test/MetadataFilterTests.cs`
  - Add tenant_id to filter operations
  - Test tenant-scoped filtering

- [ ] **9.16** Update `DocumentData.cs`
  - File: `src/Test/DocumentData.cs`
  - Add TenantId to test document data

### New Test Files for Multi-Tenancy

- [ ] **9.17** Create `TenantTests.cs`
  - File: `src/Test/TenantTests.cs`
  - Test tenant CRUD operations
  - Test tenant isolation
  - Test cascade delete behavior

- [ ] **9.18** Create `UserTests.cs`
  - File: `src/Test/UserTests.cs`
  - Test user CRUD within tenant
  - Test email uniqueness per tenant
  - Test password hashing/verification

- [ ] **9.19** Create `CredentialTests.cs`
  - File: `src/Test/CredentialTests.cs`
  - Test credential CRUD
  - Test bearer token generation
  - Test global bearer token lookup
  - Test credential cascade delete

---

## Phase 10: TestConsole Updates

**Status**: Not Started | **Progress**: 0/5

Update interactive test console in `src/TestConsole/` for multi-tenancy.

### Tasks

- [ ] **10.1** Update `IndexConfiguration.cs`
  - File: `src/TestConsole/IndexConfiguration.cs`
  - Replace StorageMode with DatabaseTypeEnum
  - Add DatabaseSettings support
  - Add TenantId configuration

- [ ] **10.2** Update `SavedIndexConfiguration.cs`
  - File: `src/TestConsole/SavedIndexConfiguration.cs`
  - Add tenant context to saved configurations
  - Support database connection string storage

- [ ] **10.3** Update `IndexManager.cs`
  - File: `src/TestConsole/IndexManager.cs`
  - Update for DatabaseDriverBase
  - Add tenant-aware index management
  - Support multiple database types

- [ ] **10.4** Update `CommandProcessor.cs`
  - File: `src/TestConsole/CommandProcessor.cs`
  - Add tenant management commands (`tenant create`, `tenant list`, `tenant use`)
  - Add user management commands (`user create`, `user list`)
  - Add credential management commands (`credential create`, `credential list`)
  - Add `--database` flag for database type selection
  - Update existing commands for tenant context

- [ ] **10.5** Update `Program.cs`
  - File: `src/TestConsole/Program.cs`
  - Add startup configuration for database type
  - Add default tenant initialization
  - Update help text for new commands

---

## Phase 11: New Tests & Documentation

**Status**: Not Started | **Progress**: 0/8

### Database Driver Tests

- [ ] **11.1** Create unit tests for SQLite implementation
  - Test all 10 method interfaces
  - Test driver initialization
  - Test in-memory and file modes

- [ ] **11.2** Create unit tests for PostgreSQL implementation
  - Test all 10 method interfaces
  - Test connection management
  - Requires PostgreSQL test instance

- [ ] **11.3** Create unit tests for MySQL implementation
  - Test all 10 method interfaces
  - Test connection management
  - Requires MySQL test instance

- [ ] **11.4** Create unit tests for SQL Server implementation
  - Test all 10 method interfaces
  - Test connection management
  - Requires SQL Server test instance

### Integration & Performance

- [ ] **11.5** Create integration tests
  - Cross-database compatibility tests
  - Multi-tenant isolation tests
  - Authentication flow tests
  - Data migration tests

- [ ] **11.6** Create performance benchmarks
  - Compare SQLite vs server databases
  - Document performance characteristics
  - Indexing throughput per database

### Documentation

- [ ] **11.7** Update documentation
  - Update README.md with multi-tenancy overview
  - Update REST_API.md with authentication headers
  - Update VBX_CLI.md with new commands
  - Create MULTI_TENANCY.md guide
  - Create DATABASE_CONFIGURATION.md guide

- [ ] **11.8** Create migration guide
  - Schema v2 to v3 migration script
  - Default tenant creation for existing data
  - Step-by-step upgrade instructions
  - Rollback procedures

---

## Appendix A: Target Folder Structure

```
src/Verbex/
├── Database/
│   ├── DatabaseDriverBase.cs
│   ├── DatabaseDriverFactory.cs
│   ├── DatabaseTypeEnum.cs
│   ├── DatabaseSettings.cs
│   │
│   ├── Interfaces/
│   │   ├── IAdministratorMethods.cs
│   │   ├── ICredentialMethods.cs
│   │   ├── IDocumentMethods.cs
│   │   ├── IDocumentTermMethods.cs
│   │   ├── ILabelMethods.cs
│   │   ├── IStatisticsMethods.cs
│   │   ├── ITagMethods.cs
│   │   ├── ITenantMethods.cs
│   │   ├── ITermMethods.cs
│   │   └── IUserMethods.cs
│   │
│   ├── Sqlite/
│   │   ├── SqliteDatabaseDriver.cs
│   │   ├── Sanitizer.cs
│   │   ├── Implementations/
│   │   │   ├── AdministratorMethods.cs
│   │   │   ├── CredentialMethods.cs
│   │   │   ├── DocumentMethods.cs
│   │   │   ├── DocumentTermMethods.cs
│   │   │   ├── LabelMethods.cs
│   │   │   ├── StatisticsMethods.cs
│   │   │   ├── TagMethods.cs
│   │   │   ├── TenantMethods.cs
│   │   │   ├── TermMethods.cs
│   │   │   └── UserMethods.cs
│   │   └── Queries/
│   │       ├── AdministratorQueries.cs
│   │       ├── CredentialQueries.cs
│   │       ├── DocumentQueries.cs
│   │       ├── DocumentTermQueries.cs
│   │       ├── LabelQueries.cs
│   │       ├── SetupQueries.cs
│   │       ├── StatisticsQueries.cs
│   │       ├── TagQueries.cs
│   │       ├── TenantQueries.cs
│   │       ├── TermQueries.cs
│   │       └── UserQueries.cs
│   │
│   ├── Postgresql/
│   │   └── (same structure as Sqlite/)
│   │
│   ├── Mysql/
│   │   └── (same structure as Sqlite/)
│   │
│   └── SqlServer/
│       └── (same structure as Sqlite/)
│
├── Models/
│   ├── Administrator.cs
│   ├── AuthenticationContext.cs
│   ├── AuthenticationResultEnum.cs
│   ├── Credential.cs
│   ├── TenantMetadata.cs
│   └── UserMaster.cs
│
└── (existing files updated)
```

---

## Appendix B: SQL Dialect Reference

| Feature | SQLite | PostgreSQL | MySQL | SQL Server |
|---------|--------|------------|-------|------------|
| Boolean | INTEGER 0/1 | TRUE/FALSE | TINYINT 0/1 | BIT 0/1 |
| RETURNING | Yes | Yes | No (SELECT after) | OUTPUT clause |
| Table quotes | none or `'` | `"` or none | `` ` `` | `[]` |
| JSON | TEXT | JSONB | JSON | NVARCHAR(MAX) |
| Timestamp | TEXT | TIMESTAMPTZ | DATETIME | DATETIME2 |
| Upsert | INSERT OR REPLACE | ON CONFLICT | ON DUPLICATE KEY | MERGE |
| Concat | `\|\|` | `\|\|` | CONCAT() | `+` |
| Limit | LIMIT x OFFSET y | LIMIT x OFFSET y | LIMIT y, x | OFFSET x FETCH NEXT y |

---

## Appendix C: ID Prefixes

| Entity | Prefix | Example |
|--------|--------|---------|
| Tenant | `ten_` | `ten_01ar5xxlajk1sxr6hzf29ksz4o` |
| User | `usr_` | `usr_01ar5xxlajk1sxr6hzf29ksz4o` |
| Credential | `cred_` | `cred_01ar5xxlajk1sxr6hzf29ksz4o` |
| Administrator | `admin_` | `admin_01ar5xxlajk1sxr6hzf29ksz4o` |
| Index | `idx_` | `idx_01ar5xxlajk1sxr6hzf29ksz4o` |
| Document | `doc_` | `doc_01ar5xxlajk1sxr6hzf29ksz4o` |
| Term | `term_` | `term_01ar5xxlajk1sxr6hzf29ksz4o` |
| DocumentTerm | `docterm_` | `docterm_01ar5xxlajk1sxr6hzf29ksz4o` |
| Label | `label_` | `label_01ar5xxlajk1sxr6hzf29ksz4o` |
| Tag | `tag_` | `tag_01ar5xxlajk1sxr6hzf29ksz4o` |
| Bearer Token | (none) | 64-char alphanumeric |

---

## Appendix D: HTTP Headers for Authentication

| Header | Purpose | Example |
|--------|---------|---------|
| `Authorization` | Bearer token | `Bearer ABCdef123...` |
| `x-tenant-id` | Tenant identifier | `ten_01ar5xxlajk...` |
| `x-email` | User email | `user@example.com` |
| `x-password` | User password | (plain text, cleared after auth) |
| `x-admin-email` | Admin email | `admin@example.com` |
| `x-admin-password` | Admin password | (plain text, cleared after auth) |

---

## Appendix E: Model Specifications

### TenantMetadata
```csharp
public class TenantMetadata
{
    public string Identifier { get; set; }      // ten_{k-sortable-id}
    public string TenantId { get; set; }        // Alias for Identifier
    public string Name { get; set; }            // Required, unique
    public string Description { get; set; }     // Optional
    public bool Active { get; set; }            // Default: true
    public DateTime CreatedUtc { get; set; }
    public DateTime LastUpdateUtc { get; set; }
}
```

### UserMaster
```csharp
public class UserMaster
{
    public string Identifier { get; set; }      // usr_{k-sortable-id}
    public string TenantId { get; set; }        // FK to tenants
    public string Email { get; set; }           // Unique within tenant
    public string PasswordSha256 { get; set; }  // SHA256 hash
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public bool IsAdmin { get; set; }           // Tenant admin flag
    public bool Active { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime LastUpdateUtc { get; set; }
}
```

### Credential
```csharp
public class Credential
{
    public string Identifier { get; set; }      // cred_{k-sortable-id}
    public string TenantId { get; set; }        // FK to tenants
    public string UserId { get; set; }          // FK to users
    public string BearerToken { get; set; }     // 64-char, globally unique
    public string Name { get; set; }            // Optional description
    public bool Active { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime LastUpdateUtc { get; set; }
}
```

### Administrator
```csharp
public class Administrator
{
    public string Identifier { get; set; }      // admin_{k-sortable-id}
    public string Email { get; set; }           // Globally unique
    public string PasswordSha256 { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public bool Active { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime LastUpdateUtc { get; set; }
}
```

---

## Appendix F: Database Schema (SQLite)

```sql
-- Schema Version 3.0 (Multi-tenant)

CREATE TABLE IF NOT EXISTS tenants (
    identifier TEXT PRIMARY KEY,
    name TEXT NOT NULL UNIQUE,
    description TEXT,
    active INTEGER DEFAULT 1,
    created_utc TEXT NOT NULL,
    last_update_utc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS administrators (
    identifier TEXT PRIMARY KEY,
    email TEXT NOT NULL UNIQUE,
    password_sha256 TEXT NOT NULL,
    first_name TEXT,
    last_name TEXT,
    active INTEGER DEFAULT 1,
    created_utc TEXT NOT NULL,
    last_update_utc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS users (
    identifier TEXT PRIMARY KEY,
    tenant_id TEXT NOT NULL,
    email TEXT NOT NULL,
    password_sha256 TEXT NOT NULL,
    first_name TEXT,
    last_name TEXT,
    is_admin INTEGER DEFAULT 0,
    active INTEGER DEFAULT 1,
    created_utc TEXT NOT NULL,
    last_update_utc TEXT NOT NULL,
    FOREIGN KEY (tenant_id) REFERENCES tenants(identifier) ON DELETE CASCADE,
    UNIQUE(tenant_id, email)
);

CREATE TABLE IF NOT EXISTS credentials (
    identifier TEXT PRIMARY KEY,
    tenant_id TEXT NOT NULL,
    user_id TEXT NOT NULL,
    bearer_token TEXT NOT NULL UNIQUE,
    name TEXT,
    active INTEGER DEFAULT 1,
    created_utc TEXT NOT NULL,
    last_update_utc TEXT NOT NULL,
    FOREIGN KEY (tenant_id) REFERENCES tenants(identifier) ON DELETE CASCADE,
    FOREIGN KEY (user_id) REFERENCES users(identifier) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS indexes (
    identifier TEXT PRIMARY KEY,
    tenant_id TEXT NOT NULL,
    name TEXT NOT NULL,
    description TEXT,
    created_utc TEXT NOT NULL,
    last_update_utc TEXT NOT NULL,
    FOREIGN KEY (tenant_id) REFERENCES tenants(identifier) ON DELETE CASCADE,
    UNIQUE(tenant_id, name)
);

CREATE TABLE IF NOT EXISTS documents (
    id TEXT PRIMARY KEY,
    tenant_id TEXT NOT NULL,
    index_id TEXT NOT NULL,
    name TEXT NOT NULL,
    content_sha256 TEXT,
    document_length INTEGER,
    term_count INTEGER,
    indexed_utc TEXT,
    last_update_utc TEXT,
    created_utc TEXT NOT NULL,
    FOREIGN KEY (tenant_id) REFERENCES tenants(identifier) ON DELETE CASCADE,
    FOREIGN KEY (index_id) REFERENCES indexes(identifier) ON DELETE CASCADE,
    UNIQUE(index_id, name)
);

CREATE TABLE IF NOT EXISTS terms (
    id TEXT PRIMARY KEY,
    tenant_id TEXT NOT NULL,
    index_id TEXT NOT NULL,
    term TEXT NOT NULL,
    document_frequency INTEGER DEFAULT 0,
    total_frequency INTEGER DEFAULT 0,
    last_update_utc TEXT,
    created_utc TEXT NOT NULL,
    FOREIGN KEY (tenant_id) REFERENCES tenants(identifier) ON DELETE CASCADE,
    FOREIGN KEY (index_id) REFERENCES indexes(identifier) ON DELETE CASCADE,
    UNIQUE(index_id, term)
);

CREATE TABLE IF NOT EXISTS document_terms (
    id TEXT PRIMARY KEY,
    document_id TEXT NOT NULL,
    term_id TEXT NOT NULL,
    term_frequency INTEGER DEFAULT 0,
    character_positions TEXT,
    term_positions TEXT,
    last_update_utc TEXT,
    created_utc TEXT NOT NULL,
    FOREIGN KEY (document_id) REFERENCES documents(id) ON DELETE CASCADE,
    FOREIGN KEY (term_id) REFERENCES terms(id) ON DELETE CASCADE,
    UNIQUE(document_id, term_id)
);

CREATE TABLE IF NOT EXISTS labels (
    id TEXT PRIMARY KEY,
    document_id TEXT,
    index_id TEXT,
    label TEXT NOT NULL,
    last_update_utc TEXT,
    created_utc TEXT NOT NULL,
    FOREIGN KEY (document_id) REFERENCES documents(id) ON DELETE CASCADE,
    FOREIGN KEY (index_id) REFERENCES indexes(identifier) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS tags (
    id TEXT PRIMARY KEY,
    document_id TEXT,
    index_id TEXT,
    key TEXT NOT NULL,
    value TEXT,
    last_update_utc TEXT,
    created_utc TEXT NOT NULL,
    FOREIGN KEY (document_id) REFERENCES documents(id) ON DELETE CASCADE,
    FOREIGN KEY (index_id) REFERENCES indexes(identifier) ON DELETE CASCADE
);

-- Indexes (add after table creation)
CREATE INDEX IF NOT EXISTS idx_tenants_name ON tenants(name);
CREATE INDEX IF NOT EXISTS idx_tenants_active ON tenants(active);
CREATE INDEX IF NOT EXISTS idx_administrators_email ON administrators(email);
CREATE INDEX IF NOT EXISTS idx_users_tenant_id ON users(tenant_id);
CREATE INDEX IF NOT EXISTS idx_users_email ON users(tenant_id, email);
CREATE INDEX IF NOT EXISTS idx_credentials_tenant_id ON credentials(tenant_id);
CREATE INDEX IF NOT EXISTS idx_credentials_user_id ON credentials(user_id);
CREATE INDEX IF NOT EXISTS idx_credentials_bearer_token ON credentials(bearer_token);
CREATE INDEX IF NOT EXISTS idx_indexes_tenant_id ON indexes(tenant_id);
CREATE INDEX IF NOT EXISTS idx_indexes_name ON indexes(tenant_id, name);
CREATE INDEX IF NOT EXISTS idx_documents_tenant_id ON documents(tenant_id);
CREATE INDEX IF NOT EXISTS idx_documents_index_id ON documents(index_id);
CREATE INDEX IF NOT EXISTS idx_documents_name ON documents(index_id, name);
CREATE INDEX IF NOT EXISTS idx_documents_content_sha256 ON documents(content_sha256);
CREATE INDEX IF NOT EXISTS idx_terms_tenant_id ON terms(tenant_id);
CREATE INDEX IF NOT EXISTS idx_terms_index_id ON terms(index_id);
CREATE INDEX IF NOT EXISTS idx_terms_term ON terms(index_id, term);
CREATE INDEX IF NOT EXISTS idx_terms_document_frequency ON terms(document_frequency DESC);
CREATE INDEX IF NOT EXISTS idx_document_terms_document_id ON document_terms(document_id);
CREATE INDEX IF NOT EXISTS idx_document_terms_term_id ON document_terms(term_id);
CREATE INDEX IF NOT EXISTS idx_document_terms_frequency ON document_terms(term_frequency DESC);
CREATE INDEX IF NOT EXISTS idx_labels_document_id ON labels(document_id);
CREATE INDEX IF NOT EXISTS idx_labels_index_id ON labels(index_id);
CREATE INDEX IF NOT EXISTS idx_labels_label ON labels(label);
CREATE INDEX IF NOT EXISTS idx_tags_document_id ON tags(document_id);
CREATE INDEX IF NOT EXISTS idx_tags_index_id ON tags(index_id);
CREATE INDEX IF NOT EXISTS idx_tags_key ON tags(key);
```

---

## Appendix G: Breaking Changes

1. **StorageMode enum removed** - Replaced by DatabaseTypeEnum
2. **IIndexRepository removed** - Replaced by DatabaseDriverBase
3. **SqliteIndexRepository removed** - Replaced by SqliteDatabaseDriver
4. **MemoryIndexRepository removed** - SQLite in-memory via DatabaseSettings.InMemory
5. **DiskIndexRepository removed** - SQLite file via DatabaseSettings.Filename
6. **VerbexConfiguration changes** - DatabaseSettings integration
7. **All existing data queries** - Add tenant_id parameter
8. **Document/Term models** - Add TenantId and IndexId properties

---

*Document created: 2026-01-08*
*Last updated: 2026-01-08*
*Revision: Added Phases 9-11 for test project coverage*
