namespace VerbexCli.Commands
{
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Verbex.Database;
    using Verbex.Database.Sqlite;
    using Verbex.Models;
    using VerbexCli.Infrastructure;

    /// <summary>
    /// Commands for managing multi-tenant administration
    /// </summary>
    public static class AdminCommands
    {
        private static DatabaseDriverBase? _Database;
        private static readonly object _Lock = new object();

        /// <summary>
        /// Creates the admin command group
        /// </summary>
        /// <returns>Admin command</returns>
        public static Command CreateAdminCommand()
        {
            Command adminCommand = new Command("admin", "Multi-tenant administration commands");

            // Add subcommand groups
            adminCommand.AddCommand(CreateTenantCommand());
            adminCommand.AddCommand(CreateUserCommand());
            adminCommand.AddCommand(CreateCredentialCommand());

            return adminCommand;
        }

        private static DatabaseDriverBase GetDatabase()
        {
            if (_Database == null)
            {
                lock (_Lock)
                {
                    if (_Database == null)
                    {
                        string configDir = GlobalConfig.GetConfigDirectory() ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                        string dbPath = Path.Combine(configDir, "admin.db");
                        DatabaseSettings settings = new DatabaseSettings
                        {
                            Type = DatabaseTypeEnum.Sqlite,
                            Filename = dbPath
                        };
                        _Database = new SqliteDatabaseDriver(settings);
                        _Database.InitializeAsync().GetAwaiter().GetResult();
                    }
                }
            }
            return _Database;
        }

        // ==================== Tenant Commands ====================

        private static Command CreateTenantCommand()
        {
            Command tenantCommand = new Command("tenant", "Manage tenants");

            tenantCommand.AddCommand(CreateTenantListCommand());
            tenantCommand.AddCommand(CreateTenantCreateCommand());
            tenantCommand.AddCommand(CreateTenantInfoCommand());
            tenantCommand.AddCommand(CreateTenantDeleteCommand());

            return tenantCommand;
        }

        private static Command CreateTenantListCommand()
        {
            Command listCommand = new Command("ls", "List all tenants");

            listCommand.SetHandler(async () =>
            {
                await HandleTenantListAsync().ConfigureAwait(false);
            });

            return listCommand;
        }

        private static Command CreateTenantCreateCommand()
        {
            Command createCommand = new Command("create", "Create a new tenant");

            Argument<string> nameArgument = new Argument<string>("name", "Name of the tenant");

            Option<string> descriptionOption = new Option<string>(
                aliases: new[] { "--description", "-d" },
                description: "Tenant description")
            {
                IsRequired = false
            };

            createCommand.AddArgument(nameArgument);
            createCommand.AddOption(descriptionOption);

            createCommand.SetHandler(async (string name, string? description) =>
            {
                await HandleTenantCreateAsync(name, description).ConfigureAwait(false);
            }, nameArgument, descriptionOption);

            return createCommand;
        }

        private static Command CreateTenantInfoCommand()
        {
            Command infoCommand = new Command("info", "Show tenant details");

            Argument<string> idArgument = new Argument<string>("id", "Tenant identifier");
            infoCommand.AddArgument(idArgument);

            infoCommand.SetHandler(async (string id) =>
            {
                await HandleTenantInfoAsync(id).ConfigureAwait(false);
            }, idArgument);

            return infoCommand;
        }

        private static Command CreateTenantDeleteCommand()
        {
            Command deleteCommand = new Command("delete", "Delete a tenant");

            Argument<string> idArgument = new Argument<string>("id", "Tenant identifier");

            Option<bool> forceOption = new Option<bool>(
                aliases: new[] { "--force", "-f" },
                description: "Force deletion without confirmation")
            {
                IsRequired = false
            };

            deleteCommand.AddArgument(idArgument);
            deleteCommand.AddOption(forceOption);

            deleteCommand.SetHandler(async (string id, bool force) =>
            {
                await HandleTenantDeleteAsync(id, force).ConfigureAwait(false);
            }, idArgument, forceOption);

            return deleteCommand;
        }

        // ==================== User Commands ====================

        private static Command CreateUserCommand()
        {
            Command userCommand = new Command("user", "Manage users");

            userCommand.AddCommand(CreateUserListCommand());
            userCommand.AddCommand(CreateUserCreateCommand());
            userCommand.AddCommand(CreateUserInfoCommand());
            userCommand.AddCommand(CreateUserDeleteCommand());

            return userCommand;
        }

        private static Command CreateUserListCommand()
        {
            Command listCommand = new Command("ls", "List users in a tenant");

            Argument<string> tenantArgument = new Argument<string>("tenant", "Tenant identifier");
            listCommand.AddArgument(tenantArgument);

            listCommand.SetHandler(async (string tenantId) =>
            {
                await HandleUserListAsync(tenantId).ConfigureAwait(false);
            }, tenantArgument);

            return listCommand;
        }

        private static Command CreateUserCreateCommand()
        {
            Command createCommand = new Command("create", "Create a new user");

            Argument<string> tenantArgument = new Argument<string>("tenant", "Tenant identifier");
            Argument<string> emailArgument = new Argument<string>("email", "User email");
            Argument<string> passwordArgument = new Argument<string>("password", "User password");

            Option<string> firstNameOption = new Option<string>(
                aliases: new[] { "--first-name", "-f" },
                description: "First name")
            {
                IsRequired = false
            };

            Option<string> lastNameOption = new Option<string>(
                aliases: new[] { "--last-name", "-l" },
                description: "Last name")
            {
                IsRequired = false
            };

            Option<bool> adminOption = new Option<bool>(
                aliases: new[] { "--admin", "-a" },
                description: "Make user a tenant admin")
            {
                IsRequired = false
            };

            createCommand.AddArgument(tenantArgument);
            createCommand.AddArgument(emailArgument);
            createCommand.AddArgument(passwordArgument);
            createCommand.AddOption(firstNameOption);
            createCommand.AddOption(lastNameOption);
            createCommand.AddOption(adminOption);

            createCommand.SetHandler(async (string tenantId, string email, string password, string? firstName, string? lastName, bool isAdmin) =>
            {
                await HandleUserCreateAsync(tenantId, email, password, firstName, lastName, isAdmin).ConfigureAwait(false);
            }, tenantArgument, emailArgument, passwordArgument, firstNameOption, lastNameOption, adminOption);

            return createCommand;
        }

        private static Command CreateUserInfoCommand()
        {
            Command infoCommand = new Command("info", "Show user details");

            Argument<string> tenantArgument = new Argument<string>("tenant", "Tenant identifier");
            Argument<string> idArgument = new Argument<string>("id", "User identifier");
            infoCommand.AddArgument(tenantArgument);
            infoCommand.AddArgument(idArgument);

            infoCommand.SetHandler(async (string tenantId, string id) =>
            {
                await HandleUserInfoAsync(tenantId, id).ConfigureAwait(false);
            }, tenantArgument, idArgument);

            return infoCommand;
        }

        private static Command CreateUserDeleteCommand()
        {
            Command deleteCommand = new Command("delete", "Delete a user");

            Argument<string> tenantArgument = new Argument<string>("tenant", "Tenant identifier");
            Argument<string> idArgument = new Argument<string>("id", "User identifier");

            Option<bool> forceOption = new Option<bool>(
                aliases: new[] { "--force", "-f" },
                description: "Force deletion without confirmation")
            {
                IsRequired = false
            };

            deleteCommand.AddArgument(tenantArgument);
            deleteCommand.AddArgument(idArgument);
            deleteCommand.AddOption(forceOption);

            deleteCommand.SetHandler(async (string tenantId, string id, bool force) =>
            {
                await HandleUserDeleteAsync(tenantId, id, force).ConfigureAwait(false);
            }, tenantArgument, idArgument, forceOption);

            return deleteCommand;
        }

        // ==================== Credential Commands ====================

        private static Command CreateCredentialCommand()
        {
            Command credentialCommand = new Command("credential", "Manage API credentials");

            credentialCommand.AddCommand(CreateCredentialListCommand());
            credentialCommand.AddCommand(CreateCredentialCreateCommand());
            credentialCommand.AddCommand(CreateCredentialInfoCommand());
            credentialCommand.AddCommand(CreateCredentialDeleteCommand());

            return credentialCommand;
        }

        private static Command CreateCredentialListCommand()
        {
            Command listCommand = new Command("ls", "List credentials in a tenant");

            Argument<string> tenantArgument = new Argument<string>("tenant", "Tenant identifier");
            listCommand.AddArgument(tenantArgument);

            listCommand.SetHandler(async (string tenantId) =>
            {
                await HandleCredentialListAsync(tenantId).ConfigureAwait(false);
            }, tenantArgument);

            return listCommand;
        }

        private static Command CreateCredentialCreateCommand()
        {
            Command createCommand = new Command("create", "Create a new API credential");

            Argument<string> tenantArgument = new Argument<string>("tenant", "Tenant identifier");
            Argument<string> userArgument = new Argument<string>("user", "User identifier");

            Option<string> descriptionOption = new Option<string>(
                aliases: new[] { "--description", "-d" },
                description: "Credential description")
            {
                IsRequired = false
            };

            createCommand.AddArgument(tenantArgument);
            createCommand.AddArgument(userArgument);
            createCommand.AddOption(descriptionOption);

            createCommand.SetHandler(async (string tenantId, string userId, string? description) =>
            {
                await HandleCredentialCreateAsync(tenantId, userId, description).ConfigureAwait(false);
            }, tenantArgument, userArgument, descriptionOption);

            return createCommand;
        }

        private static Command CreateCredentialInfoCommand()
        {
            Command infoCommand = new Command("info", "Show credential details");

            Argument<string> tenantArgument = new Argument<string>("tenant", "Tenant identifier");
            Argument<string> idArgument = new Argument<string>("id", "Credential identifier");
            infoCommand.AddArgument(tenantArgument);
            infoCommand.AddArgument(idArgument);

            infoCommand.SetHandler(async (string tenantId, string id) =>
            {
                await HandleCredentialInfoAsync(tenantId, id).ConfigureAwait(false);
            }, tenantArgument, idArgument);

            return infoCommand;
        }

        private static Command CreateCredentialDeleteCommand()
        {
            Command deleteCommand = new Command("delete", "Delete a credential");

            Argument<string> tenantArgument = new Argument<string>("tenant", "Tenant identifier");
            Argument<string> idArgument = new Argument<string>("id", "Credential identifier");

            Option<bool> forceOption = new Option<bool>(
                aliases: new[] { "--force", "-f" },
                description: "Force deletion without confirmation")
            {
                IsRequired = false
            };

            deleteCommand.AddArgument(tenantArgument);
            deleteCommand.AddArgument(idArgument);
            deleteCommand.AddOption(forceOption);

            deleteCommand.SetHandler(async (string tenantId, string id, bool force) =>
            {
                await HandleCredentialDeleteAsync(tenantId, id, force).ConfigureAwait(false);
            }, tenantArgument, idArgument, forceOption);

            return deleteCommand;
        }

        // ==================== Handler Methods ====================

        // Tenant Handlers

        private static async Task HandleTenantListAsync()
        {
            try
            {
                DatabaseDriverBase db = GetDatabase();
                IEnumerable<TenantMetadata> tenants = await db.Tenants.ReadManyAsync().ConfigureAwait(false);

                object[] data = tenants.Select(t => new
                {
                    Id = t.Identifier,
                    Name = t.Name,
                    Active = t.Active,
                    Created = t.CreatedUtc
                }).ToArray();

                OutputManager.WriteData(data);
            }
            catch (Exception ex)
            {
                OutputManager.WriteError($"Failed to list tenants: {ex.Message}");
                throw;
            }
        }

        private static async Task HandleTenantCreateAsync(string name, string? description)
        {
            try
            {
                DatabaseDriverBase db = GetDatabase();
                TenantMetadata tenant = new TenantMetadata(name, description ?? string.Empty);
                await db.Tenants.CreateAsync(tenant).ConfigureAwait(false);

                OutputManager.WriteSuccess($"Tenant created: {tenant.Identifier}");
                OutputManager.WriteInfo($"Name: {tenant.Name}");
            }
            catch (Exception ex)
            {
                OutputManager.WriteError($"Failed to create tenant: {ex.Message}");
                throw;
            }
        }

        private static async Task HandleTenantInfoAsync(string id)
        {
            try
            {
                DatabaseDriverBase db = GetDatabase();
                TenantMetadata? tenant = await db.Tenants.ReadByIdentifierAsync(id).ConfigureAwait(false);

                if (tenant == null)
                {
                    OutputManager.WriteError($"Tenant not found: {id}");
                    return;
                }

                OutputManager.WriteData(new
                {
                    Id = tenant.Identifier,
                    Name = tenant.Name,
                    Active = tenant.Active,
                    Created = tenant.CreatedUtc
                });
            }
            catch (Exception ex)
            {
                OutputManager.WriteError($"Failed to get tenant info: {ex.Message}");
                throw;
            }
        }

        private static async Task HandleTenantDeleteAsync(string id, bool force)
        {
            try
            {
                if (!force)
                {
                    OutputManager.WriteLine($"Are you sure you want to delete tenant '{id}'? This will delete all associated data. (y/N)");
                    string? response = Console.ReadLine();
                    if (response?.ToLowerInvariant() != "y" && response?.ToLowerInvariant() != "yes")
                    {
                        OutputManager.WriteLine("Operation cancelled");
                        return;
                    }
                }

                DatabaseDriverBase db = GetDatabase();
                bool deleted = await db.Tenants.DeleteByIdentifierAsync(id).ConfigureAwait(false);

                if (deleted)
                {
                    OutputManager.WriteSuccess($"Tenant deleted: {id}");
                }
                else
                {
                    OutputManager.WriteError($"Tenant not found: {id}");
                }
            }
            catch (Exception ex)
            {
                OutputManager.WriteError($"Failed to delete tenant: {ex.Message}");
                throw;
            }
        }

        // User Handlers

        private static async Task HandleUserListAsync(string tenantId)
        {
            try
            {
                DatabaseDriverBase db = GetDatabase();
                IEnumerable<UserMaster> users = await db.Users.ReadManyAsync(tenantId).ConfigureAwait(false);

                object[] data = users.Select(u => new
                {
                    Id = u.Identifier,
                    Email = u.Email,
                    Name = $"{u.FirstName} {u.LastName}".Trim(),
                    Admin = u.IsAdmin,
                    Active = u.Active,
                    Created = u.CreatedUtc
                }).ToArray();

                OutputManager.WriteData(data);
            }
            catch (Exception ex)
            {
                OutputManager.WriteError($"Failed to list users: {ex.Message}");
                throw;
            }
        }

        private static async Task HandleUserCreateAsync(string tenantId, string email, string password, string? firstName, string? lastName, bool isAdmin)
        {
            try
            {
                DatabaseDriverBase db = GetDatabase();

                // Verify tenant exists
                TenantMetadata? tenant = await db.Tenants.ReadByIdentifierAsync(tenantId).ConfigureAwait(false);
                if (tenant == null)
                {
                    OutputManager.WriteError($"Tenant not found: {tenantId}");
                    return;
                }

                UserMaster user = new UserMaster(tenantId, email);
                user.SetPassword(password);
                user.FirstName = firstName ?? string.Empty;
                user.LastName = lastName ?? string.Empty;
                user.IsAdmin = isAdmin;

                await db.Users.CreateAsync(user).ConfigureAwait(false);

                OutputManager.WriteSuccess($"User created: {user.Identifier}");
                OutputManager.WriteInfo($"Email: {user.Email}");
            }
            catch (Exception ex)
            {
                OutputManager.WriteError($"Failed to create user: {ex.Message}");
                throw;
            }
        }

        private static async Task HandleUserInfoAsync(string tenantId, string id)
        {
            try
            {
                DatabaseDriverBase db = GetDatabase();
                UserMaster? user = await db.Users.ReadByIdentifierAsync(tenantId, id).ConfigureAwait(false);

                if (user == null)
                {
                    OutputManager.WriteError($"User not found: {id}");
                    return;
                }

                OutputManager.WriteData(new
                {
                    Id = user.Identifier,
                    TenantId = user.TenantId,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Admin = user.IsAdmin,
                    Active = user.Active,
                    Created = user.CreatedUtc
                });
            }
            catch (Exception ex)
            {
                OutputManager.WriteError($"Failed to get user info: {ex.Message}");
                throw;
            }
        }

        private static async Task HandleUserDeleteAsync(string tenantId, string id, bool force)
        {
            try
            {
                if (!force)
                {
                    OutputManager.WriteLine($"Are you sure you want to delete user '{id}'? (y/N)");
                    string? response = Console.ReadLine();
                    if (response?.ToLowerInvariant() != "y" && response?.ToLowerInvariant() != "yes")
                    {
                        OutputManager.WriteLine("Operation cancelled");
                        return;
                    }
                }

                DatabaseDriverBase db = GetDatabase();
                bool deleted = await db.Users.DeleteByIdentifierAsync(tenantId, id).ConfigureAwait(false);

                if (deleted)
                {
                    OutputManager.WriteSuccess($"User deleted: {id}");
                }
                else
                {
                    OutputManager.WriteError($"User not found: {id}");
                }
            }
            catch (Exception ex)
            {
                OutputManager.WriteError($"Failed to delete user: {ex.Message}");
                throw;
            }
        }

        // Credential Handlers

        private static async Task HandleCredentialListAsync(string tenantId)
        {
            try
            {
                DatabaseDriverBase db = GetDatabase();
                IEnumerable<Credential> credentials = await db.Credentials.ReadManyAsync(tenantId).ConfigureAwait(false);

                object[] data = credentials.Select(c => new
                {
                    Id = c.Identifier,
                    Description = c.Name,
                    Token = MaskToken(c.BearerToken),
                    Active = c.Active,
                    Created = c.CreatedUtc
                }).ToArray();

                OutputManager.WriteData(data);
            }
            catch (Exception ex)
            {
                OutputManager.WriteError($"Failed to list credentials: {ex.Message}");
                throw;
            }
        }

        private static async Task HandleCredentialCreateAsync(string tenantId, string userId, string? description)
        {
            try
            {
                DatabaseDriverBase db = GetDatabase();

                // Verify tenant exists
                TenantMetadata? tenant = await db.Tenants.ReadByIdentifierAsync(tenantId).ConfigureAwait(false);
                if (tenant == null)
                {
                    OutputManager.WriteError($"Tenant not found: {tenantId}");
                    return;
                }

                // Verify user exists
                UserMaster? user = await db.Users.ReadByIdentifierAsync(tenantId, userId).ConfigureAwait(false);
                if (user == null)
                {
                    OutputManager.WriteError($"User not found: {userId}");
                    return;
                }

                Credential credential = new Credential(tenantId, userId);
                credential.Name = description ?? string.Empty;

                await db.Credentials.CreateAsync(credential).ConfigureAwait(false);

                OutputManager.WriteSuccess($"Credential created: {credential.Identifier}");
                OutputManager.WriteWarning("IMPORTANT: Copy this token now. You will not be able to see it again!");
                OutputManager.WriteLine($"Bearer Token: {credential.BearerToken}");
            }
            catch (Exception ex)
            {
                OutputManager.WriteError($"Failed to create credential: {ex.Message}");
                throw;
            }
        }

        private static async Task HandleCredentialInfoAsync(string tenantId, string id)
        {
            try
            {
                DatabaseDriverBase db = GetDatabase();
                Credential? credential = await db.Credentials.ReadByIdentifierAsync(tenantId, id).ConfigureAwait(false);

                if (credential == null)
                {
                    OutputManager.WriteError($"Credential not found: {id}");
                    return;
                }

                OutputManager.WriteData(new
                {
                    Id = credential.Identifier,
                    TenantId = credential.TenantId,
                    Description = credential.Name,
                    Token = MaskToken(credential.BearerToken),
                    Active = credential.Active,
                    Created = credential.CreatedUtc
                });
            }
            catch (Exception ex)
            {
                OutputManager.WriteError($"Failed to get credential info: {ex.Message}");
                throw;
            }
        }

        private static async Task HandleCredentialDeleteAsync(string tenantId, string id, bool force)
        {
            try
            {
                if (!force)
                {
                    OutputManager.WriteLine($"Are you sure you want to delete credential '{id}'? Applications using this key will stop working. (y/N)");
                    string? response = Console.ReadLine();
                    if (response?.ToLowerInvariant() != "y" && response?.ToLowerInvariant() != "yes")
                    {
                        OutputManager.WriteLine("Operation cancelled");
                        return;
                    }
                }

                DatabaseDriverBase db = GetDatabase();
                bool deleted = await db.Credentials.DeleteByIdentifierAsync(tenantId, id).ConfigureAwait(false);

                if (deleted)
                {
                    OutputManager.WriteSuccess($"Credential deleted: {id}");
                }
                else
                {
                    OutputManager.WriteError($"Credential not found: {id}");
                }
            }
            catch (Exception ex)
            {
                OutputManager.WriteError($"Failed to delete credential: {ex.Message}");
                throw;
            }
        }

        private static string MaskToken(string? token)
        {
            if (string.IsNullOrEmpty(token)) return "N/A";
            if (token.Length <= 8) return "********";
            return token.Substring(0, 4) + "..." + token.Substring(token.Length - 4);
        }
    }
}
