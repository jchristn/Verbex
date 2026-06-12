namespace Test
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Verbex;
    using Verbex.Database;
    using Verbex.Models;
    using Verbex.Server.Services;

    /// <summary>
    /// Tests for multi-tenancy database operations.
    /// </summary>
    public static class DatabaseDriverTests
    {
        private static string _TestDbPath = string.Empty;

        /// <summary>
        /// Runs all database driver tests.
        /// </summary>
        /// <param name="runner">Test runner to execute tests.</param>
        /// <returns>Task representing the asynchronous operation.</returns>
        public static async Task RunAllAsync(TestRunner runner)
        {
            // Set up test database path for SQLite file mode
            if (TestContext.DatabaseSettings?.Type == DatabaseTypeEnum.Sqlite &&
                !TestContext.DatabaseSettings.InMemory)
            {
                // Use a temporary file if testing with SQLite file mode
                _TestDbPath = Path.Combine(Path.GetTempPath(), $"verbex_test_{Guid.NewGuid()}.db");
            }

            try
            {
                // Tenant tests
                await runner.RunTestAsync("Tenant Create Test", TestTenantCreateAsync).ConfigureAwait(false);
                await runner.RunTestAsync("Tenant Read Test", TestTenantReadAsync).ConfigureAwait(false);
                await runner.RunTestAsync("Tenant List Test", TestTenantListAsync).ConfigureAwait(false);
                await runner.RunTestAsync("Tenant Delete Test", TestTenantDeleteAsync).ConfigureAwait(false);

                // User tests
                await runner.RunTestAsync("User Create Test", TestUserCreateAsync).ConfigureAwait(false);
                await runner.RunTestAsync("User Read By Email Test", TestUserReadByEmailAsync).ConfigureAwait(false);
                await runner.RunTestAsync("User List Test", TestUserListAsync).ConfigureAwait(false);
                await runner.RunTestAsync("User Password Hashing Test", TestUserPasswordHashingAsync).ConfigureAwait(false);
                await runner.RunTestAsync("User Delete Test", TestUserDeleteAsync).ConfigureAwait(false);

                // Credential tests
                await runner.RunTestAsync("Credential Create Test", TestCredentialCreateAsync).ConfigureAwait(false);
                await runner.RunTestAsync("Credential Read By Token Test", TestCredentialReadByTokenAsync).ConfigureAwait(false);
                await runner.RunTestAsync("Credential List Test", TestCredentialListAsync).ConfigureAwait(false);
                await runner.RunTestAsync("Credential Delete Test", TestCredentialDeleteAsync).ConfigureAwait(false);

                // Administrator tests
                await runner.RunTestAsync("Administrator Create Test", TestAdministratorCreateAsync).ConfigureAwait(false);
                await runner.RunTestAsync("Administrator Read By Email Test", TestAdministratorReadByEmailAsync).ConfigureAwait(false);

                // Cross-tenant isolation tests
                await runner.RunTestAsync("Tenant Isolation Test", TestTenantIsolationAsync).ConfigureAwait(false);
                await runner.RunTestAsync("Index Manager Delete Drops Tables Test", TestIndexManagerDeleteDropsTablesAsync).ConfigureAwait(false);
                // This regression targets async-flow server drivers; SQLite uses a thread-affine write lock.
                if (TestContext.DatabaseSettings?.Type != DatabaseTypeEnum.Sqlite)
                {
                    await runner.RunTestAsync("Scoped Transaction Rollback Test", TestScopedTransactionRollbackAsync).ConfigureAwait(false);
                }
            }
            finally
            {
                // Clean up test database
                CleanupTestDatabase();
            }
        }

        private static async Task<DatabaseDriverBase> CreateTestDriverAsync()
        {
            DatabaseSettings settings = GetTestDatabaseSettings();
            DatabaseDriverBase driver = DatabaseDriverFactory.Create(settings);
            await driver.InitializeAsync().ConfigureAwait(false);
            return driver;
        }

        private static DatabaseSettings GetTestDatabaseSettings()
        {
            if (TestContext.DatabaseSettings == null)
            {
                throw new InvalidOperationException("Database settings not initialized. Call TestContext.Initialize() first.");
            }

            DatabaseSettings settings = TestContext.DatabaseSettings.Clone();

            // For SQLite file mode, use the test-specific path
            if (settings.Type == DatabaseTypeEnum.Sqlite && !settings.InMemory)
            {
                if (!string.IsNullOrEmpty(_TestDbPath))
                {
                    settings.Filename = _TestDbPath;
                }
            }

            return settings;
        }

        private static void CleanupTestDatabase()
        {
            if (!TestContext.ShouldCleanup)
            {
                if (!string.IsNullOrEmpty(_TestDbPath))
                {
                    Console.WriteLine($"  (Test database preserved at: {_TestDbPath})");
                }
                return;
            }

            // Only clean up SQLite file databases
            if (TestContext.DatabaseSettings?.Type == DatabaseTypeEnum.Sqlite &&
                !TestContext.DatabaseSettings.InMemory)
            {
                TestContext.CleanupTestDatabaseFile(_TestDbPath);
            }
        }

        // Tenant Tests

        private static async Task TestTenantCreateAsync()
        {
            using DatabaseDriverBase driver = await CreateTestDriverAsync().ConfigureAwait(false);

            TenantMetadata tenant = new TenantMetadata("Test Tenant", "A test tenant for unit testing");
            await driver.Tenants.CreateAsync(tenant).ConfigureAwait(false);

            TestAssert.IsNotNull(tenant.Identifier);
            TestAssert.IsTrue(tenant.Identifier.StartsWith("ten_"));
            TestAssert.AreEqual("Test Tenant", tenant.Name);
            TestAssert.IsTrue(tenant.Active);
        }

        private static async Task TestTenantReadAsync()
        {
            using DatabaseDriverBase driver = await CreateTestDriverAsync().ConfigureAwait(false);

            TenantMetadata tenant = new TenantMetadata("Read Test Tenant");
            await driver.Tenants.CreateAsync(tenant).ConfigureAwait(false);

            TenantMetadata? retrieved = await driver.Tenants.ReadByIdentifierAsync(tenant.Identifier).ConfigureAwait(false);

            TestAssert.IsNotNull(retrieved);
            TestAssert.AreEqual(tenant.Identifier, retrieved!.Identifier);
            TestAssert.AreEqual("Read Test Tenant", retrieved.Name);
        }

        private static async Task TestTenantListAsync()
        {
            using DatabaseDriverBase driver = await CreateTestDriverAsync().ConfigureAwait(false);

            // Create multiple tenants with unique names
            string suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
            await driver.Tenants.CreateAsync(new TenantMetadata($"List Tenant A {suffix}")).ConfigureAwait(false);
            await driver.Tenants.CreateAsync(new TenantMetadata($"List Tenant B {suffix}")).ConfigureAwait(false);
            await driver.Tenants.CreateAsync(new TenantMetadata($"List Tenant C {suffix}")).ConfigureAwait(false);

            IEnumerable<TenantMetadata> tenants = await driver.Tenants.ReadManyAsync().ConfigureAwait(false);
            List<TenantMetadata> tenantList = tenants.ToList();

            TestAssert.IsTrue(tenantList.Count >= 3);
        }

        private static async Task TestTenantDeleteAsync()
        {
            using DatabaseDriverBase driver = await CreateTestDriverAsync().ConfigureAwait(false);

            TenantMetadata tenant = new TenantMetadata("Delete Test Tenant");
            await driver.Tenants.CreateAsync(tenant).ConfigureAwait(false);

            bool deleted = await driver.Tenants.DeleteByIdentifierAsync(tenant.Identifier).ConfigureAwait(false);
            TestAssert.IsTrue(deleted);

            TenantMetadata? retrieved = await driver.Tenants.ReadByIdentifierAsync(tenant.Identifier).ConfigureAwait(false);
            TestAssert.IsNull(retrieved);
        }

        // User Tests

        private static async Task TestUserCreateAsync()
        {
            using DatabaseDriverBase driver = await CreateTestDriverAsync().ConfigureAwait(false);

            TenantMetadata tenant = new TenantMetadata("User Test Tenant");
            await driver.Tenants.CreateAsync(tenant).ConfigureAwait(false);

            UserMaster user = new UserMaster(tenant.Identifier, "test@example.com");
            user.SetPassword("testpassword123");
            user.FirstName = "Test";
            user.LastName = "User";
            await driver.Users.CreateAsync(user).ConfigureAwait(false);

            TestAssert.IsNotNull(user.Identifier);
            TestAssert.IsTrue(user.Identifier.StartsWith("usr_"));
            TestAssert.AreEqual("test@example.com", user.Email);
            TestAssert.IsTrue(user.Active);
        }

        private static async Task TestUserReadByEmailAsync()
        {
            using DatabaseDriverBase driver = await CreateTestDriverAsync().ConfigureAwait(false);

            TenantMetadata tenant = new TenantMetadata("Email Test Tenant");
            await driver.Tenants.CreateAsync(tenant).ConfigureAwait(false);

            UserMaster user = new UserMaster(tenant.Identifier, "lookup@example.com");
            user.SetPassword("password");
            await driver.Users.CreateAsync(user).ConfigureAwait(false);

            UserMaster? retrieved = await driver.Users.ReadByEmailAsync(tenant.Identifier, "lookup@example.com").ConfigureAwait(false);

            TestAssert.IsNotNull(retrieved);
            TestAssert.AreEqual(user.Identifier, retrieved!.Identifier);
            TestAssert.AreEqual("lookup@example.com", retrieved.Email);
        }

        private static async Task TestUserListAsync()
        {
            using DatabaseDriverBase driver = await CreateTestDriverAsync().ConfigureAwait(false);

            TenantMetadata tenant = new TenantMetadata("List Users Tenant");
            await driver.Tenants.CreateAsync(tenant).ConfigureAwait(false);

            UserMaster user1 = new UserMaster(tenant.Identifier, "user1@example.com");
            user1.SetPassword("pass1");
            await driver.Users.CreateAsync(user1).ConfigureAwait(false);

            UserMaster user2 = new UserMaster(tenant.Identifier, "user2@example.com");
            user2.SetPassword("pass2");
            await driver.Users.CreateAsync(user2).ConfigureAwait(false);

            IEnumerable<UserMaster> users = await driver.Users.ReadManyAsync(tenant.Identifier).ConfigureAwait(false);
            List<UserMaster> userList = users.ToList();

            TestAssert.AreEqual(2, userList.Count);
        }

        private static async Task TestUserPasswordHashingAsync()
        {
            using DatabaseDriverBase driver = await CreateTestDriverAsync().ConfigureAwait(false);

            TenantMetadata tenant = new TenantMetadata("Password Test Tenant");
            await driver.Tenants.CreateAsync(tenant).ConfigureAwait(false);

            UserMaster user = new UserMaster(tenant.Identifier, "password@example.com");
            user.SetPassword("mysecretpassword");
            await driver.Users.CreateAsync(user).ConfigureAwait(false);

            // Password should be hashed, not plain text
            TestAssert.IsNotNull(user.PasswordSha256);
            TestAssert.AreNotEqual("mysecretpassword", user.PasswordSha256);
            TestAssert.AreEqual(64, user.PasswordSha256.Length); // SHA-256 hex string is 64 chars
        }

        private static async Task TestUserDeleteAsync()
        {
            using DatabaseDriverBase driver = await CreateTestDriverAsync().ConfigureAwait(false);

            TenantMetadata tenant = new TenantMetadata("Delete User Tenant");
            await driver.Tenants.CreateAsync(tenant).ConfigureAwait(false);

            UserMaster user = new UserMaster(tenant.Identifier, "delete@example.com");
            user.SetPassword("password");
            await driver.Users.CreateAsync(user).ConfigureAwait(false);

            bool deleted = await driver.Users.DeleteByIdentifierAsync(tenant.Identifier, user.Identifier).ConfigureAwait(false);
            TestAssert.IsTrue(deleted);

            UserMaster? retrieved = await driver.Users.ReadByIdentifierAsync(tenant.Identifier, user.Identifier).ConfigureAwait(false);
            TestAssert.IsNull(retrieved);
        }

        // Credential Tests

        private static async Task TestCredentialCreateAsync()
        {
            using DatabaseDriverBase driver = await CreateTestDriverAsync().ConfigureAwait(false);

            TenantMetadata tenant = new TenantMetadata("Credential Test Tenant");
            await driver.Tenants.CreateAsync(tenant).ConfigureAwait(false);

            UserMaster user = new UserMaster(tenant.Identifier, "creduser@example.com");
            user.SetPassword("password");
            await driver.Users.CreateAsync(user).ConfigureAwait(false);

            Credential credential = new Credential(tenant.Identifier, user.Identifier);
            credential.Name = "Test API Key";
            await driver.Credentials.CreateAsync(credential).ConfigureAwait(false);

            TestAssert.IsNotNull(credential.Identifier);
            TestAssert.IsTrue(credential.Identifier.StartsWith("cred_"));
            TestAssert.IsNotNull(credential.BearerToken);
            TestAssert.IsTrue(credential.Active);
        }

        private static async Task TestCredentialReadByTokenAsync()
        {
            using DatabaseDriverBase driver = await CreateTestDriverAsync().ConfigureAwait(false);

            TenantMetadata tenant = new TenantMetadata("Token Test Tenant");
            await driver.Tenants.CreateAsync(tenant).ConfigureAwait(false);

            UserMaster user = new UserMaster(tenant.Identifier, "tokenuser@example.com");
            user.SetPassword("password");
            await driver.Users.CreateAsync(user).ConfigureAwait(false);

            Credential credential = new Credential(tenant.Identifier, user.Identifier);
            await driver.Credentials.CreateAsync(credential).ConfigureAwait(false);

            Credential? retrieved = await driver.Credentials.ReadByBearerTokenAsync(credential.BearerToken).ConfigureAwait(false);

            TestAssert.IsNotNull(retrieved);
            TestAssert.AreEqual(credential.Identifier, retrieved!.Identifier);
            TestAssert.AreEqual(credential.BearerToken, retrieved.BearerToken);
        }

        private static async Task TestCredentialListAsync()
        {
            using DatabaseDriverBase driver = await CreateTestDriverAsync().ConfigureAwait(false);

            TenantMetadata tenant = new TenantMetadata("List Credentials Tenant");
            await driver.Tenants.CreateAsync(tenant).ConfigureAwait(false);

            UserMaster user = new UserMaster(tenant.Identifier, "listcreduser@example.com");
            user.SetPassword("password");
            await driver.Users.CreateAsync(user).ConfigureAwait(false);

            await driver.Credentials.CreateAsync(new Credential(tenant.Identifier, user.Identifier, "Key 1")).ConfigureAwait(false);
            await driver.Credentials.CreateAsync(new Credential(tenant.Identifier, user.Identifier, "Key 2")).ConfigureAwait(false);

            IEnumerable<Credential> credentials = await driver.Credentials.ReadManyAsync(tenant.Identifier).ConfigureAwait(false);
            List<Credential> credentialList = credentials.ToList();

            TestAssert.AreEqual(2, credentialList.Count);
        }

        private static async Task TestCredentialDeleteAsync()
        {
            using DatabaseDriverBase driver = await CreateTestDriverAsync().ConfigureAwait(false);

            TenantMetadata tenant = new TenantMetadata("Delete Credential Tenant");
            await driver.Tenants.CreateAsync(tenant).ConfigureAwait(false);

            UserMaster user = new UserMaster(tenant.Identifier, "delcreduser@example.com");
            user.SetPassword("password");
            await driver.Users.CreateAsync(user).ConfigureAwait(false);

            Credential credential = new Credential(tenant.Identifier, user.Identifier);
            await driver.Credentials.CreateAsync(credential).ConfigureAwait(false);

            bool deleted = await driver.Credentials.DeleteByIdentifierAsync(tenant.Identifier, credential.Identifier).ConfigureAwait(false);
            TestAssert.IsTrue(deleted);

            Credential? retrieved = await driver.Credentials.ReadByIdentifierAsync(tenant.Identifier, credential.Identifier).ConfigureAwait(false);
            TestAssert.IsNull(retrieved);
        }

        // Administrator Tests

        private static async Task TestAdministratorCreateAsync()
        {
            using DatabaseDriverBase driver = await CreateTestDriverAsync().ConfigureAwait(false);

            string uniqueEmail = $"admin_{Guid.NewGuid():N}@example.com";
            Administrator admin = new Administrator(uniqueEmail);
            admin.SetPassword("adminpassword");
            admin.FirstName = "System";
            admin.LastName = "Admin";
            await driver.Administrators.CreateAsync(admin).ConfigureAwait(false);

            TestAssert.IsNotNull(admin.Identifier);
            TestAssert.IsTrue(admin.Identifier.StartsWith("adm_"));
            TestAssert.AreEqual(uniqueEmail, admin.Email);
            TestAssert.IsTrue(admin.Active);
        }

        private static async Task TestAdministratorReadByEmailAsync()
        {
            using DatabaseDriverBase driver = await CreateTestDriverAsync().ConfigureAwait(false);

            string uniqueEmail = $"lookup.admin_{Guid.NewGuid():N}@example.com";
            Administrator admin = new Administrator(uniqueEmail);
            admin.SetPassword("password");
            await driver.Administrators.CreateAsync(admin).ConfigureAwait(false);

            Administrator? retrieved = await driver.Administrators.ReadByEmailAsync(uniqueEmail).ConfigureAwait(false);

            TestAssert.IsNotNull(retrieved);
            TestAssert.AreEqual(admin.Identifier, retrieved!.Identifier);
            TestAssert.AreEqual(uniqueEmail, retrieved.Email);
        }

        // Tenant Isolation Tests

        private static async Task TestTenantIsolationAsync()
        {
            using DatabaseDriverBase driver = await CreateTestDriverAsync().ConfigureAwait(false);

            // Create two tenants with unique names
            string suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
            TenantMetadata tenantA = new TenantMetadata($"Isolation Tenant A {suffix}");
            await driver.Tenants.CreateAsync(tenantA).ConfigureAwait(false);

            TenantMetadata tenantB = new TenantMetadata($"Isolation Tenant B {suffix}");
            await driver.Tenants.CreateAsync(tenantB).ConfigureAwait(false);

            // Create users in each tenant with same email
            UserMaster userA = new UserMaster(tenantA.Identifier, "same@example.com");
            userA.SetPassword("passwordA");
            await driver.Users.CreateAsync(userA).ConfigureAwait(false);

            UserMaster userB = new UserMaster(tenantB.Identifier, "same@example.com");
            userB.SetPassword("passwordB");
            await driver.Users.CreateAsync(userB).ConfigureAwait(false);

            // Verify each tenant only sees their own user
            UserMaster? retrievedA = await driver.Users.ReadByEmailAsync(tenantA.Identifier, "same@example.com").ConfigureAwait(false);
            UserMaster? retrievedB = await driver.Users.ReadByEmailAsync(tenantB.Identifier, "same@example.com").ConfigureAwait(false);

            TestAssert.IsNotNull(retrievedA);
            TestAssert.IsNotNull(retrievedB);
            TestAssert.AreNotEqual(retrievedA!.Identifier, retrievedB!.Identifier);
            TestAssert.AreEqual(tenantA.Identifier, retrievedA.TenantId);
            TestAssert.AreEqual(tenantB.Identifier, retrievedB.TenantId);

            // Verify listing users is tenant-scoped
            IEnumerable<UserMaster> usersA = await driver.Users.ReadManyAsync(tenantA.Identifier).ConfigureAwait(false);
            IEnumerable<UserMaster> usersB = await driver.Users.ReadManyAsync(tenantB.Identifier).ConfigureAwait(false);

            TestAssert.AreEqual(1, usersA.Count());
            TestAssert.AreEqual(1, usersB.Count());
        }

        private static async Task TestScopedTransactionRollbackAsync()
        {
            using DatabaseDriverBase driver = await CreateTestDriverAsync().ConfigureAwait(false);

            string tenantName = $"Rollback Tenant {Guid.NewGuid():N}";
            string? tenantId = null;

            await TestAssert.ThrowsAsync<InvalidOperationException>(
                async () => await driver.ExecuteInTransactionAsync(async token =>
                {
                    TenantMetadata tenant = new TenantMetadata(tenantName);
                    await driver.Tenants.CreateAsync(tenant, token).ConfigureAwait(false);
                    tenantId = tenant.Identifier;

                    await Task.Yield();
                    throw new InvalidOperationException("force rollback");
                }).ConfigureAwait(false),
                "Scoped transaction should surface the original failure").ConfigureAwait(false);

            TestAssert.IsNotNull(tenantId, "Tenant should have been assigned an identifier inside the transaction");
            TenantMetadata? byId = await driver.Tenants.ReadByIdentifierAsync(tenantId!).ConfigureAwait(false);
            TenantMetadata? byName = await driver.Tenants.ReadByNameAsync(tenantName).ConfigureAwait(false);

            TestAssert.IsNull(byId, "Scoped transaction should roll back tenant creation by id");
            TestAssert.IsNull(byName, "Scoped transaction should roll back tenant creation by name");
        }

        private static async Task TestIndexManagerDeleteDropsTablesAsync()
        {
            using DatabaseDriverBase driver = await CreateTestDriverAsync().ConfigureAwait(false);
            IndexManager manager = new IndexManager(driver);

            string suffix = Guid.NewGuid().ToString("N");
            string indexId = $"idx_mgr_del_{suffix}";
            string tablePrefix = TablePrefixValidator.FromIndexId(indexId);
            TenantMetadata tenant = new TenantMetadata($"Index Manager Delete Tenant {suffix}");

            try
            {
                await driver.Tenants.CreateAsync(tenant).ConfigureAwait(false);

                IndexMetadata metadata = new IndexMetadata(
                    tenant.Identifier,
                    $"Index Manager Delete {suffix}",
                    "Temporary index for delete table cleanup regression")
                {
                    Identifier = indexId,
                    InMemory = false
                };

                await manager.CreateIndexAsync(metadata).ConfigureAwait(false);
                long tableCountAfterCreate = await CountIndexTablesAsync(driver, tablePrefix).ConfigureAwait(false);
                TestAssert.AreEqual(5L, tableCountAfterCreate, "Index creation should create all prefixed tables");

                InvertedIndex? index = manager.GetIndex(indexId);
                TestAssert.IsNotNull(index, "Index manager should return the created runtime index");

                string documentId = $"doc_{suffix}";
                await index!.AddDocumentAsync(documentId, documentId, "index manager delete validation document").ConfigureAwait(false);
                await index.AddLabelsBatchAsync(documentId, new List<string> { "delete-validation" }).ConfigureAwait(false);
                await index.AddTagsBatchAsync(documentId, new Dictionary<string, string> { ["mode"] = "delete-validation" }).ConfigureAwait(false);

                bool deleted = await manager.DeleteIndexAsync(tenant.Identifier, indexId).ConfigureAwait(false);
                TestAssert.IsTrue(deleted, "Index delete should succeed");

                IndexMetadata? deletedMetadata = await driver.Indexes.ReadByIdentifierAsync(tenant.Identifier, indexId).ConfigureAwait(false);
                TestAssert.IsNull(deletedMetadata, "Index metadata should be deleted");

                long tableCountAfterDelete = await CountIndexTablesAsync(driver, tablePrefix).ConfigureAwait(false);
                TestAssert.AreEqual(0L, tableCountAfterDelete, "Index delete should drop all prefixed tables");
            }
            finally
            {
                await manager.DisposeAllAsync().ConfigureAwait(false);

                try
                {
                    await driver.Indexes.DeleteByIdentifierAsync(tenant.Identifier, indexId).ConfigureAwait(false);
                }
                catch
                {
                }

                try
                {
                    await driver.DropIndexTablesAsync(tablePrefix).ConfigureAwait(false);
                }
                catch
                {
                }

                try
                {
                    await driver.Tenants.DeleteByIdentifierAsync(tenant.Identifier).ConfigureAwait(false);
                }
                catch
                {
                }
            }
        }

        private static async Task<long> CountIndexTablesAsync(DatabaseDriverBase driver, string tablePrefix)
        {
            string postgresqlSchema = string.IsNullOrWhiteSpace(driver.Settings.Schema) ? "public" : driver.Settings.Schema;
            string sqlServerSchema = string.IsNullOrWhiteSpace(driver.Settings.Schema) ? "dbo" : driver.Settings.Schema;
            string tableNames = string.Join(",",
                new[]
                {
                    $"{tablePrefix}_documents",
                    $"{tablePrefix}_terms",
                    $"{tablePrefix}_document_terms",
                    $"{tablePrefix}_labels",
                    $"{tablePrefix}_tags"
                }.Select(name => $"'{EscapeSql(name)}'"));

            string query = driver.Settings.Type switch
            {
                DatabaseTypeEnum.Sqlite =>
                    $"SELECT COUNT(*) AS table_count FROM sqlite_master WHERE type = 'table' AND name IN ({tableNames});",
                DatabaseTypeEnum.Postgresql =>
                    $"SELECT COUNT(*) AS table_count FROM information_schema.tables WHERE table_schema = '{EscapeSql(postgresqlSchema)}' AND table_name IN ({tableNames});",
                DatabaseTypeEnum.Mysql =>
                    $"SELECT COUNT(*) AS table_count FROM information_schema.tables WHERE table_schema = '{EscapeSql(driver.Settings.DatabaseName)}' AND table_name IN ({tableNames});",
                DatabaseTypeEnum.SqlServer =>
                    $"SELECT COUNT(*) AS table_count FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = '{EscapeSql(sqlServerSchema)}' AND TABLE_NAME IN ({tableNames});",
                _ => throw new NotSupportedException($"Unsupported database type {driver.Settings.Type}")
            };

            DataTable table = await driver.ExecuteQueryAsync(query).ConfigureAwait(false);
            return Convert.ToInt64(table.Rows[0][0]);
        }

        private static string EscapeSql(string value)
        {
            return (value ?? string.Empty).Replace("'", "''");
        }
    }
}
