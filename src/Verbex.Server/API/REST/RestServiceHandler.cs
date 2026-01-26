namespace Verbex.Server.API.REST
{
    using SyslogLogging;
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Reflection.PortableExecutable;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using Verbex.Database;
    using Verbex.Database.Interfaces;
    using Verbex.Models;
    using Verbex.Server.Classes;
    using Verbex.Server.Services;
    using Verbex.Utilities;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>
    /// REST service handler.
    /// </summary>
    public class RestServiceHandler
    {
        #region Public-Members

        #endregion

        #region Private-Members

        private static readonly JsonSerializerOptions _JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private Settings? _Settings = null;
        private AuthenticationService? _Auth = null;
        private IndexManager? _IndexManager = null;
        private DatabaseDriverBase? _Database = null;
        private LoggingModule? _Logging = null;
        private Webserver? _Webserver = null;
        private readonly string _Header = "[RestServiceHandler] ";

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settings">Settings.</param>
        /// <param name="auth">Authentication service.</param>
        /// <param name="indexManager">Index manager.</param>
        /// <param name="database">Database driver for multi-tenant operations.</param>
        /// <param name="logging">Logging module.</param>
        /// <exception cref="ArgumentNullException">Thrown when required parameter is null.</exception>
        public RestServiceHandler(Settings settings, AuthenticationService auth, IndexManager indexManager, DatabaseDriverBase database, LoggingModule logging)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Auth = auth ?? throw new ArgumentNullException(nameof(auth));
            _IndexManager = indexManager ?? throw new ArgumentNullException(nameof(indexManager));
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));

            InitializeWebserver();
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Start the webserver.
        /// </summary>
        public void Start()
        {
            _Webserver?.Start();
            string protocol = _Settings!.Rest.Ssl ? "https" : "http";
            _Logging?.Info(_Header + "started on " + protocol + "://" + _Settings.Rest.Hostname + ":" + _Settings.Rest.Port);
        }

        /// <summary>
        /// Stop the webserver.
        /// </summary>
        public void Stop()
        {
            _Webserver?.Stop();
            _Logging?.Info(_Header + "stopped");
        }

        #endregion

        #region Private-Methods

        /// <summary>
        /// Initialize webserver.
        /// </summary>
        private void InitializeWebserver()
        {
            WebserverSettings webserverSettings = new WebserverSettings
            {
                Hostname = _Settings!.Rest.Hostname,
                Port = _Settings.Rest.Port
            };

            // Configure SSL settings
            webserverSettings.Ssl.Enable = _Settings.Rest.Ssl;
            if (_Settings.Rest.Ssl)
            {
                if (!String.IsNullOrEmpty(_Settings.Rest.SslCertificateFile))
                {
                    webserverSettings.Ssl.PfxCertificateFile = _Settings.Rest.SslCertificateFile;
                }
                if (!String.IsNullOrEmpty(_Settings.Rest.SslCertificatePassword))
                {
                    webserverSettings.Ssl.PfxCertificatePassword = _Settings.Rest.SslCertificatePassword;
                }
            }

            _Webserver = new Webserver(webserverSettings, DefaultRoute);

            if (_Settings.Rest.EnableOpenApi)
            {
                ConfigureOpenApi();
            }

            InitializeRoutes();
        }

        /// <summary>
        /// Configure OpenAPI/Swagger documentation.
        /// </summary>
        private void ConfigureOpenApi()
        {
            _Webserver!.UseOpenApi(settings =>
            {
                settings.Info.Title = "Verbex API";
                settings.Info.Version = "1.0.0";
                settings.Info.Description = "REST API for Verbex inverted index service. Provides endpoints for managing indices, documents, and performing full-text search operations.";

                settings.Tags.Add(new OpenApiTag { Name = "Health", Description = "Health check endpoints" });
                settings.Tags.Add(new OpenApiTag { Name = "Authentication", Description = "Authentication and token management" });
                settings.Tags.Add(new OpenApiTag { Name = "Indices", Description = "Index management operations" });
                settings.Tags.Add(new OpenApiTag { Name = "Documents", Description = "Document management within indices" });
                settings.Tags.Add(new OpenApiTag { Name = "Search", Description = "Full-text search operations" });

                settings.SecuritySchemes.Add("BearerAuth", new OpenApiSecurityScheme
                {
                    Type = "http",
                    Scheme = "bearer",
                    BearerFormat = "Token",
                    Description = "Bearer token authentication. Use the /v1.0/auth/login endpoint to obtain a token."
                });

                settings.EnableSwaggerUi = _Settings!.Rest.EnableSwaggerUi;
                settings.DocumentPath = "/openapi.json";
                settings.SwaggerUiPath = "/swagger";
            });
        }

        /// <summary>
        /// Initialize routes.
        /// </summary>
        private void InitializeRoutes()
        {
            #region General-Routes

            _Webserver!.Routes.Preflight = PreflightRoute;
            _Webserver.Routes.PostRouting = PostRoutingRoute;

            _Webserver.Routes.PreAuthentication.Static.Add(
                HttpMethod.GET, "/", GetHealthRoute,
                metadata => metadata
                    .WithTag("Health")
                    .WithDescription("Root health check endpoint. Returns service status, version, and timestamp."),
                ExceptionRoute);

            _Webserver.Routes.PreAuthentication.Static.Add(
                HttpMethod.HEAD, "/", HeadHealthRoute,
                metadata => metadata
                    .WithTag("Health")
                    .WithDescription("Root health check endpoint. Returns 200/OK."),
                ExceptionRoute);

            _Webserver.Routes.PreAuthentication.Static.Add(
                HttpMethod.GET, "/v1.0/health", GetHealthRoute,
                metadata => metadata
                    .WithTag("Health")
                    .WithDescription("Versioned health check endpoint. Returns service status, version, and timestamp.")
                    .WithResponse(200, OpenApiResponseMetadata.Json("Service is healthy", CreateResponseSchema())),
                ExceptionRoute);

            #endregion

            #region Authentication-Routes

            _Webserver.Routes.PreAuthentication.Static.Add(
                HttpMethod.POST, "/v1.0/auth/login", PostAuthLoginRoute,
                metadata => metadata
                    .WithTag("Authentication")
                    .WithDescription("Authenticate with username and password to obtain a bearer token.")
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json(
                        CreateLoginRequestSchema(),
                        "Login credentials",
                        required: true))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Login successful", CreateLoginResponseSchema()))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest(CreateErrorSchema()))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
                ExceptionRoute);

            _Webserver.Routes.PreAuthentication.Static.Add(
                HttpMethod.GET, "/v1.0/auth/validate", GetAuthValidateRoute,
                metadata => metadata
                    .WithTag("Authentication")
                    .WithDescription("Validate a bearer token. Returns whether the token is valid.")
                    .WithResponse(200, OpenApiResponseMetadata.Json("Token validation result", CreateValidateResponseSchema()))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
                ExceptionRoute);

            #endregion

            #region Index-Management-Routes

            _Webserver.Routes.PostAuthentication.Static.Add(
                HttpMethod.GET, "/v1.0/indices", GetIndicesRoute,
                metadata => metadata
                    .WithTag("Indices")
                    .WithDescription("List all available indices with their configuration and metadata.")
                    .WithResponse(200, OpenApiResponseMetadata.Json("List of indices", CreateIndicesListSchema()))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
                ExceptionRoute);

            _Webserver.Routes.PostAuthentication.Static.Add(
                HttpMethod.POST, "/v1.0/indices", PostIndicesRoute,
                metadata => metadata
                    .WithTag("Indices")
                    .WithDescription("Create a new index with the specified configuration. The index ID must be unique.")
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json(
                        CreateIndexRequestSchema(),
                        "Index configuration",
                        required: true))
                    .WithResponse(201, OpenApiResponseMetadata.Created(CreateIndexCreatedSchema()))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest(CreateErrorSchema()))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(409, OpenApiResponseMetadata.Create("Index with this ID already exists")),
                ExceptionRoute);

            _Webserver.Routes.PostAuthentication.Parameter.Add(
                HttpMethod.GET, "/v1.0/indices/{id}", GetIndexRoute,
                metadata => metadata
                    .WithTag("Indices")
                    .WithDescription("Get detailed information about a specific index including configuration and statistics.")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "The unique identifier of the index"))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Index details with statistics", CreateIndexDetailsSchema()))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()),
                ExceptionRoute);

            _Webserver.Routes.PostAuthentication.Parameter.Add(
                HttpMethod.HEAD, "/v1.0/indices/{id}", HeadIndexRoute,
                metadata => metadata
                    .WithTag("Indices")
                    .WithDescription("Check if an index exists. Returns 200 if found, 404 if not found.")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "The unique identifier of the index"))
                    .WithResponse(200, OpenApiResponseMetadata.Create("Index exists"))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()),
                ExceptionRoute);

            _Webserver.Routes.PostAuthentication.Parameter.Add(
                HttpMethod.DELETE, "/v1.0/indices/{id}", DeleteIndexRoute,
                metadata => metadata
                    .WithTag("Indices")
                    .WithDescription("Delete an index and all its documents permanently.")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "The unique identifier of the index to delete"))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Index deleted successfully", CreateMessageSchema()))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()),
                ExceptionRoute);

            _Webserver.Routes.PostAuthentication.Parameter.Add(
                HttpMethod.PUT, "/v1.0/indices/{id}/labels", PutIndexLabelsRoute,
                metadata => metadata
                    .WithTag("Indices")
                    .WithDescription("Replace all labels on an index. This is a full replacement, not an additive operation.")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "The unique identifier of the index"))
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json(
                        CreateUpdateLabelsRequestSchema(),
                        "New labels for the index",
                        required: true))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Labels updated successfully", CreateIndexUpdateSchema()))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest(CreateErrorSchema()))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()),
                ExceptionRoute);

            _Webserver.Routes.PostAuthentication.Parameter.Add(
                HttpMethod.PUT, "/v1.0/indices/{id}/tags", PutIndexTagsRoute,
                metadata => metadata
                    .WithTag("Indices")
                    .WithDescription("Replace all tags on an index. This is a full replacement, not an additive operation.")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "The unique identifier of the index"))
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json(
                        CreateUpdateTagsRequestSchema(),
                        "New tags for the index",
                        required: true))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Tags updated successfully", CreateIndexUpdateSchema()))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest(CreateErrorSchema()))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()),
                ExceptionRoute);

            _Webserver.Routes.PostAuthentication.Parameter.Add(
                HttpMethod.PUT, "/v1.0/indices/{id}/customMetadata", PutIndexCustomMetadataRoute,
                metadata => metadata
                    .WithTag("Indices")
                    .WithDescription("Replace custom metadata on an index. Can be any JSON-serializable value.")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "The unique identifier of the index"))
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json(
                        CreateUpdateCustomMetadataRequestSchema(),
                        "New custom metadata for the index",
                        required: true))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Custom metadata updated successfully", CreateIndexUpdateSchema()))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest(CreateErrorSchema()))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()),
                ExceptionRoute);

            _Webserver.Routes.PostAuthentication.Parameter.Add(
                HttpMethod.POST, "/v1.0/indices/{id}/search", PostIndexSearchRoute,
                metadata => metadata
                    .WithTag("Search")
                    .WithDescription("Perform a full-text search within an index. Supports AND/OR logic and label/tag filtering.")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "The unique identifier of the index to search"))
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json(
                        CreateSearchRequestSchema(),
                        "Search query and options",
                        required: true))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Search results", CreateSearchResultsSchema()))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest(CreateErrorSchema()))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()),
                ExceptionRoute);

            #endregion

            #region Document-Routes

            _Webserver.Routes.PostAuthentication.Parameter.Add(
                HttpMethod.GET, "/v1.0/indices/{id}/documents", GetIndexDocumentsRoute,
                metadata => metadata
                    .WithTag("Documents")
                    .WithDescription("List all documents in an index. Limited to 1000 documents maximum.")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "The unique identifier of the index"))
                    .WithResponse(200, OpenApiResponseMetadata.Json("List of documents", CreateDocumentsListSchema()))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()),
                ExceptionRoute);

            _Webserver.Routes.PostAuthentication.Parameter.Add(
                HttpMethod.POST, "/v1.0/indices/{id}/documents", PostIndexDocumentsRoute,
                metadata => metadata
                    .WithTag("Documents")
                    .WithDescription("Add a new document to an index. If no ID is provided, one will be auto-generated.")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "The unique identifier of the index"))
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json(
                        CreateAddDocumentRequestSchema(),
                        "Document to add",
                        required: true))
                    .WithResponse(201, OpenApiResponseMetadata.Created(CreateDocumentCreatedSchema()))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest(CreateErrorSchema()))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()),
                ExceptionRoute);

            _Webserver.Routes.PostAuthentication.Parameter.Add(
                HttpMethod.GET, "/v1.0/indices/{id}/documents/{docId}", GetIndexDocumentRoute,
                metadata => metadata
                    .WithTag("Documents")
                    .WithDescription("Get a specific document by ID including its metadata (labels and tags).")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "The unique identifier of the index"))
                    .WithParameter(OpenApiParameterMetadata.Path("docId", "The unique identifier of the document"))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Document details", CreateDocumentDetailsSchema()))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()),
                ExceptionRoute);

            _Webserver.Routes.PostAuthentication.Parameter.Add(
                HttpMethod.HEAD, "/v1.0/indices/{id}/documents/{docId}", HeadIndexDocumentRoute,
                metadata => metadata
                    .WithTag("Documents")
                    .WithDescription("Check if a document exists in an index. Returns 200 if found, 404 if not found.")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "The unique identifier of the index"))
                    .WithParameter(OpenApiParameterMetadata.Path("docId", "The unique identifier of the document"))
                    .WithResponse(200, OpenApiResponseMetadata.Create("Document exists"))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()),
                ExceptionRoute);

            _Webserver.Routes.PostAuthentication.Parameter.Add(
                HttpMethod.DELETE, "/v1.0/indices/{id}/documents/{docId}", DeleteIndexDocumentRoute,
                metadata => metadata
                    .WithTag("Documents")
                    .WithDescription("Delete a document from an index permanently.")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "The unique identifier of the index"))
                    .WithParameter(OpenApiParameterMetadata.Path("docId", "The unique identifier of the document to delete"))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Document deleted successfully", CreateMessageSchema()))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()),
                ExceptionRoute);

            _Webserver.Routes.PostAuthentication.Parameter.Add(
                HttpMethod.PUT, "/v1.0/indices/{id}/documents/{docId}/labels", PutDocumentLabelsRoute,
                metadata => metadata
                    .WithTag("Documents")
                    .WithDescription("Replace all labels on a document. This is a full replacement, not an additive operation.")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "The unique identifier of the index"))
                    .WithParameter(OpenApiParameterMetadata.Path("docId", "The unique identifier of the document"))
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json(
                        CreateUpdateLabelsRequestSchema(),
                        "New labels for the document",
                        required: true))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Labels updated successfully", CreateDocumentUpdateSchema()))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest(CreateErrorSchema()))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()),
                ExceptionRoute);

            _Webserver.Routes.PostAuthentication.Parameter.Add(
                HttpMethod.PUT, "/v1.0/indices/{id}/documents/{docId}/tags", PutDocumentTagsRoute,
                metadata => metadata
                    .WithTag("Documents")
                    .WithDescription("Replace all tags on a document. This is a full replacement, not an additive operation.")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "The unique identifier of the index"))
                    .WithParameter(OpenApiParameterMetadata.Path("docId", "The unique identifier of the document"))
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json(
                        CreateUpdateTagsRequestSchema(),
                        "New tags for the document",
                        required: true))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Tags updated successfully", CreateDocumentUpdateSchema()))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest(CreateErrorSchema()))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()),
                ExceptionRoute);

            _Webserver.Routes.PostAuthentication.Parameter.Add(
                HttpMethod.PUT, "/v1.0/indices/{id}/documents/{docId}/customMetadata", PutDocumentCustomMetadataRoute,
                metadata => metadata
                    .WithTag("Documents")
                    .WithDescription("Replace custom metadata on a document. Can be any JSON-serializable value.")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "The unique identifier of the index"))
                    .WithParameter(OpenApiParameterMetadata.Path("docId", "The unique identifier of the document"))
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json(
                        CreateUpdateCustomMetadataRequestSchema(),
                        "New custom metadata for the document",
                        required: true))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Custom metadata updated successfully", CreateDocumentUpdateSchema()))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest(CreateErrorSchema()))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()),
                ExceptionRoute);

            #endregion

            #region Admin-Routes

            #region Tenant-Routes

            _Webserver.Routes.PostAuthentication.Static.Add(
                HttpMethod.GET, "/v1.0/tenants", GetTenantsRoute,
                metadata => metadata
                    .WithTag("Tenants")
                    .WithDescription("List all tenants. Requires global admin access."),
                ExceptionRoute);

            _Webserver.Routes.PostAuthentication.Static.Add(
                HttpMethod.POST, "/v1.0/tenants", PostTenantsRoute,
                metadata => metadata
                    .WithTag("Tenants")
                    .WithDescription("Create a new tenant. Requires global admin access."),
                ExceptionRoute);

            _Webserver.Routes.PostAuthentication.Parameter.Add(
                HttpMethod.GET, "/v1.0/tenants/{id}", GetTenantRoute,
                metadata => metadata
                    .WithTag("Tenants")
                    .WithDescription("Get a specific tenant by ID."),
                ExceptionRoute);

            _Webserver.Routes.PostAuthentication.Parameter.Add(
                HttpMethod.DELETE, "/v1.0/tenants/{id}", DeleteTenantRoute,
                metadata => metadata
                    .WithTag("Tenants")
                    .WithDescription("Delete a tenant and all its data. Requires global admin access."),
                ExceptionRoute);

            _Webserver.Routes.PostAuthentication.Parameter.Add(
                HttpMethod.PUT, "/v1.0/tenants/{id}", PutTenantRoute,
                metadata => metadata
                    .WithTag("Tenants")
                    .WithDescription("Update a tenant. Requires global admin access."),
                ExceptionRoute);

            #endregion

            #region User-Routes

            _Webserver.Routes.PostAuthentication.Parameter.Add(
                HttpMethod.GET, "/v1.0/tenants/{id}/users", GetTenantUsersRoute,
                metadata => metadata
                    .WithTag("Users")
                    .WithDescription("List all users for a tenant."),
                ExceptionRoute);

            _Webserver.Routes.PostAuthentication.Parameter.Add(
                HttpMethod.POST, "/v1.0/tenants/{id}/users", PostTenantUsersRoute,
                metadata => metadata
                    .WithTag("Users")
                    .WithDescription("Create a new user for a tenant."),
                ExceptionRoute);

            _Webserver.Routes.PostAuthentication.Parameter.Add(
                HttpMethod.GET, "/v1.0/tenants/{id}/users/{userId}", GetTenantUserRoute,
                metadata => metadata
                    .WithTag("Users")
                    .WithDescription("Get a specific user by ID."),
                ExceptionRoute);

            _Webserver.Routes.PostAuthentication.Parameter.Add(
                HttpMethod.DELETE, "/v1.0/tenants/{id}/users/{userId}", DeleteTenantUserRoute,
                metadata => metadata
                    .WithTag("Users")
                    .WithDescription("Delete a user."),
                ExceptionRoute);

            _Webserver.Routes.PostAuthentication.Parameter.Add(
                HttpMethod.PUT, "/v1.0/tenants/{id}/users/{userId}", PutTenantUserRoute,
                metadata => metadata
                    .WithTag("Users")
                    .WithDescription("Update a user."),
                ExceptionRoute);

            #endregion

            #region Credential-Routes

            _Webserver.Routes.PostAuthentication.Parameter.Add(
                HttpMethod.GET, "/v1.0/tenants/{id}/credentials", GetTenantCredentialsRoute,
                metadata => metadata
                    .WithTag("Credentials")
                    .WithDescription("List all API credentials for a tenant."),
                ExceptionRoute);

            _Webserver.Routes.PostAuthentication.Parameter.Add(
                HttpMethod.POST, "/v1.0/tenants/{id}/credentials", PostTenantCredentialsRoute,
                metadata => metadata
                    .WithTag("Credentials")
                    .WithDescription("Create a new API credential for a tenant."),
                ExceptionRoute);

            _Webserver.Routes.PostAuthentication.Parameter.Add(
                HttpMethod.DELETE, "/v1.0/tenants/{id}/credentials/{credId}", DeleteTenantCredentialRoute,
                metadata => metadata
                    .WithTag("Credentials")
                    .WithDescription("Revoke an API credential."),
                ExceptionRoute);

            _Webserver.Routes.PostAuthentication.Parameter.Add(
                HttpMethod.PUT, "/v1.0/tenants/{id}/credentials/{credId}", PutTenantCredentialRoute,
                metadata => metadata
                    .WithTag("Credentials")
                    .WithDescription("Update an API credential (activate/deactivate)."),
                ExceptionRoute);

            #endregion

            #region Tags-and-Labels-Routes

            _Webserver.Routes.PostAuthentication.Parameter.Add(
                HttpMethod.PUT, "/v1.0/tenants/{id}/labels", PutTenantLabelsRoute,
                metadata => metadata
                    .WithTag("Tenants")
                    .WithDescription("Replace all labels on a tenant. This is a full replacement, not an additive operation."),
                ExceptionRoute);

            _Webserver.Routes.PostAuthentication.Parameter.Add(
                HttpMethod.PUT, "/v1.0/tenants/{id}/tags", PutTenantTagsRoute,
                metadata => metadata
                    .WithTag("Tenants")
                    .WithDescription("Replace all tags on a tenant. This is a full replacement, not an additive operation."),
                ExceptionRoute);

            // User labels and tags routes
            _Webserver.Routes.PostAuthentication.Parameter.Add(
                HttpMethod.PUT, "/v1.0/tenants/{id}/users/{userId}/labels", PutUserLabelsRoute,
                metadata => metadata
                    .WithTag("Users")
                    .WithDescription("Replace all labels on a user. This is a full replacement, not an additive operation."),
                ExceptionRoute);

            _Webserver.Routes.PostAuthentication.Parameter.Add(
                HttpMethod.PUT, "/v1.0/tenants/{id}/users/{userId}/tags", PutUserTagsRoute,
                metadata => metadata
                    .WithTag("Users")
                    .WithDescription("Replace all tags on a user. This is a full replacement, not an additive operation."),
                ExceptionRoute);

            _Webserver.Routes.PostAuthentication.Parameter.Add(
                HttpMethod.PUT, "/v1.0/tenants/{id}/credentials/{credId}/labels", PutCredentialLabelsRoute,
                metadata => metadata
                    .WithTag("Credentials")
                    .WithDescription("Replace all labels on a credential. This is a full replacement, not an additive operation."),
                ExceptionRoute);

            _Webserver.Routes.PostAuthentication.Parameter.Add(
                HttpMethod.PUT, "/v1.0/tenants/{id}/credentials/{credId}/tags", PutCredentialTagsRoute,
                metadata => metadata
                    .WithTag("Credentials")
                    .WithDescription("Replace all tags on a credential. This is a full replacement, not an additive operation."),
                ExceptionRoute);

            #endregion

            #endregion
        }

        /// <summary>
        /// Preflight route.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        private async Task PreflightRoute(HttpContextBase ctx)
        {
            NameValueCollection responseHeaders = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);

            string[] requestedHeaders = null;
            string headers = "";

            if (ctx.Request.Headers != null)
            {
                for (int i = 0; i < ctx.Request.Headers.Count; i++)
                {
                    string key = ctx.Request.Headers.GetKey(i);
                    string value = ctx.Request.Headers.Get(i);
                    if (String.IsNullOrEmpty(key)) continue;
                    if (String.IsNullOrEmpty(value)) continue;
                    if (String.Compare(key.ToLower(), "access-control-request-headers") == 0)
                    {
                        requestedHeaders = value.Split(',');
                        break;
                    }
                }
            }

            if (requestedHeaders != null)
            {
                foreach (string curr in requestedHeaders)
                {
                    headers += ", " + curr;
                }
            }

            responseHeaders.Add("Access-Control-Allow-Methods", "OPTIONS, HEAD, GET, PUT, POST, DELETE");
            responseHeaders.Add("Access-Control-Allow-Headers", "*, Content-Type, X-Requested-With, " + headers);
            responseHeaders.Add("Access-Control-Expose-Headers", "Content-Type, X-Requested-With, " + headers);
            responseHeaders.Add("Access-Control-Allow-Origin", "*");
            responseHeaders.Add("Accept", "*/*");
            responseHeaders.Add("Accept-Language", "en-US, en");
            responseHeaders.Add("Accept-Charset", "ISO-8859-1, utf-8");
            responseHeaders.Add("Connection", "keep-alive");

            ctx.Response.StatusCode = 200;
            ctx.Response.Headers = responseHeaders;
            await ctx.Response.Send().ConfigureAwait(false);
            return;
        }

        /// <summary>
        /// Post-routing route.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        private async Task PostRoutingRoute(HttpContextBase ctx)
        {
            ctx.Response.Timestamp.End = DateTime.UtcNow;

            _Logging.Debug(
                _Header
                + ctx.Request.Method + " " + ctx.Request.Url.RawWithQuery + " "
                + ctx.Response.StatusCode + " "
                + "(" + ctx.Response.Timestamp.TotalMs.Value.ToString("F2") + "ms)");
        }

        /// <summary>
        /// Default route.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        private async Task DefaultRoute(HttpContextBase ctx)
        {
            ResponseContext response = new ResponseContext(false, 404, "Not found");
            await SendResponse(ctx, response);
        }

        /// <summary>
        /// Exception route.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <param name="e">Exception.</param>
        /// <returns>Task.</returns>
        private async Task ExceptionRoute(HttpContextBase ctx, Exception e)
        {
            _Logging?.Error(_Header + "Exception: " + e.Message);
            ResponseContext response = new ResponseContext(false, 500, e.Message);
            await SendResponse(ctx, response);
        }

        /// <summary>
        /// Health check route.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        private async Task GetHealthRoute(HttpContextBase ctx)
        {
            await WrappedRequestHandler(ctx, RequestTypeEnum.HealthCheck, (reqCtx) =>
            {
                return Task.FromResult(new ResponseContext
                {
                    Success = true,
                    StatusCode = 200,
                    Data = new
                    {
                        Status = "Healthy",
                        Version = "1.0.0",
                        Timestamp = DateTime.UtcNow
                    }
                });
            });
        }

        /// <summary>
        /// Health check route.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        private async Task HeadHealthRoute(HttpContextBase ctx)
        {
            await WrappedRequestHandler(ctx, RequestTypeEnum.HealthCheck, (reqCtx) =>
            {
                return Task.FromResult(new ResponseContext
                {
                    Success = true,
                    StatusCode = 200,
                    Data = null
                });
            });
        }

        /// <summary>
        /// Login route.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        private async Task PostAuthLoginRoute(HttpContextBase ctx)
        {
            await WrappedRequestHandler(ctx, RequestTypeEnum.Authentication, async (reqCtx) =>
            {
                string body = await GetRequestBody(ctx);
                if (String.IsNullOrEmpty(body))
                {
                    return new ResponseContext(false, 400, "Request body is required");
                }

                LoginRequest? loginRequest = JsonSerializer.Deserialize<LoginRequest>(body, _JsonOptions);
                if (loginRequest == null)
                {
                    return new ResponseContext(false, 400, "Invalid JSON in request body");
                }

                if (!loginRequest.Validate(out string errorMessage))
                {
                    return new ResponseContext(false, 400, errorMessage);
                }

                // Try global admin authentication first
                Administrator? admin = await _Auth!.AuthenticateAdminAsync(loginRequest.Username, loginRequest.Password).ConfigureAwait(false);
                if (admin != null)
                {
                    // Return global admin token
                    return new ResponseContext
                    {
                        Success = true,
                        StatusCode = 200,
                        Data = new
                        {
                            Token = _Settings!.AdminBearerToken,
                            Email = admin.Email,
                            IsGlobalAdmin = true,
                            IsAdmin = true
                        }
                    };
                }

                // Try tenant user authentication if tenant ID is provided
                if (!String.IsNullOrEmpty(loginRequest.TenantId))
                {
                    UserMaster? user = await _Auth.AuthenticateUserAsync(loginRequest.TenantId, loginRequest.Username, loginRequest.Password).ConfigureAwait(false);
                    if (user != null)
                    {
                        // Find or create a credential for this user
                        List<Credential> existingCreds = await _Database!.Credentials.ReadManyAsync(loginRequest.TenantId).ConfigureAwait(false);
                        Credential? userCred = existingCreds.FirstOrDefault(c => c.UserId == user.Identifier && c.Active);

                        if (userCred == null)
                        {
                            // Create a new credential for this user
                            userCred = new Credential(loginRequest.TenantId, user.Identifier);
                            userCred.Name = $"Login credential for {user.Email}";
                            await _Database.Credentials.CreateAsync(userCred).ConfigureAwait(false);
                        }

                        return new ResponseContext
                        {
                            Success = true,
                            StatusCode = 200,
                            Data = new
                            {
                                Token = userCred.BearerToken,
                                Email = user.Email,
                                FirstName = user.FirstName,
                                LastName = user.LastName,
                                TenantId = loginRequest.TenantId,
                                IsGlobalAdmin = false,
                                IsAdmin = user.IsAdmin
                            }
                        };
                    }
                }

                return new ResponseContext(false, 401, "Invalid credentials");
            });
        }

        /// <summary>
        /// Validate token route.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        private async Task GetAuthValidateRoute(HttpContextBase ctx)
        {
            await WrappedRequestHandler(ctx, RequestTypeEnum.Authentication, async (reqCtx) =>
            {
                string? token = GetAuthToken(ctx);
                if (String.IsNullOrEmpty(token))
                {
                    return new ResponseContext
                    {
                        Success = false,
                        StatusCode = 401,
                        Data = new { Valid = false }
                    };
                }

                AuthContext? authContext = await _Auth!.AuthenticateBearerAsync(token).ConfigureAwait(false);
                if (authContext == null || !authContext.IsAuthenticated)
                {
                    return new ResponseContext
                    {
                        Success = false,
                        StatusCode = 401,
                        Data = new { Valid = false }
                    };
                }

                return new ResponseContext
                {
                    Success = true,
                    StatusCode = 200,
                    Data = new
                    {
                        Valid = true,
                        IsGlobalAdmin = authContext.IsGlobalAdmin,
                        IsTenantAdmin = authContext.IsTenantAdmin,
                        TenantId = authContext.TenantId,
                        UserId = authContext.UserId,
                        CredentialId = authContext.CredentialId,
                        Email = authContext.Email
                    }
                };
            });
        }

        /// <summary>
        /// Get all indices route.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        private async Task GetIndicesRoute(HttpContextBase ctx)
        {
            await WrappedRequestHandler(ctx, RequestTypeEnum.IndexManagement, async (reqCtx) =>
            {
                // Get tenant from auth context
                AuthContext? auth = await _Auth!.AuthenticateBearerAsync(GetAuthToken(ctx) ?? "").ConfigureAwait(false);
                string tenantId = !String.IsNullOrEmpty(auth?.TenantId) ? auth.TenantId : "default";

                List<IndexMetadata> indices = _IndexManager!.GetAllMetadata(tenantId);
                var response = indices.Select(m => new
                {
                    Identifier = m.Identifier,
                    TenantId = m.TenantId,
                    Name = m.Name,
                    Description = m.Description,
                    Enabled = m.Enabled,
                    InMemory = m.InMemory,
                    CreatedUtc = m.CreatedUtc,
                    Labels = m.Labels,
                    Tags = m.Tags
                }).ToList();

                return new ResponseContext
                {
                    Success = true,
                    StatusCode = 200,
                    Data = new { Indices = response, Count = response.Count }
                };
            });
        }

        /// <summary>
        /// Create index route.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        private async Task PostIndicesRoute(HttpContextBase ctx)
        {
            await WrappedRequestHandler(ctx, RequestTypeEnum.IndexManagement, async (reqCtx) =>
            {
                // Get tenant from auth context
                AuthContext? auth = await _Auth!.AuthenticateBearerAsync(GetAuthToken(ctx) ?? "").ConfigureAwait(false);

                string body = await GetRequestBody(ctx);
                if (String.IsNullOrEmpty(body))
                {
                    return new ResponseContext(false, 400, "Request body is required");
                }

                CreateIndexRequest? createRequest = JsonSerializer.Deserialize<CreateIndexRequest>(body, _JsonOptions);
                if (createRequest == null)
                {
                    return new ResponseContext(false, 400, "Invalid JSON in request body");
                }

                if (!createRequest.Validate(out string errorMessage))
                {
                    return new ResponseContext(false, 400, errorMessage);
                }

                // Determine tenant ID: for global admins use request, for tenant users use auth context
                string tenantId;
                if (auth != null && auth.IsGlobalAdmin)
                {
                    // Global admins must specify tenant ID in request
                    if (String.IsNullOrEmpty(createRequest.TenantId))
                    {
                        return new ResponseContext(false, 400, "Tenant ID is required for global admin");
                    }
                    tenantId = createRequest.TenantId;
                }
                else
                {
                    // Tenant users use their auth context tenant ID
                    tenantId = auth?.TenantId ?? "";
                    if (String.IsNullOrEmpty(tenantId))
                    {
                        return new ResponseContext(false, 400, "Unable to determine tenant ID");
                    }
                }

                if (_IndexManager!.IndexExistsByName(tenantId, createRequest.Name))
                {
                    return new ResponseContext(false, 409, "Index with this name already exists in the tenant");
                }

                IndexMetadata metadata = createRequest.ToIndexMetadata(tenantId);

                try
                {
                    IndexMetadata created = await _IndexManager!.CreateIndexAsync(metadata).ConfigureAwait(false);
                    return new ResponseContext
                    {
                        Success = true,
                        StatusCode = 201,
                        Data = new {
                            Message = "Index created successfully",
                            Index = new {
                                Identifier = created.Identifier,
                                TenantId = created.TenantId,
                                Name = created.Name,
                                Description = created.Description,
                                InMemory = created.InMemory,
                                CreatedUtc = created.CreatedUtc,
                                Labels = created.Labels,
                                Tags = created.Tags,
                                CustomMetadata = created.CustomMetadata
                            }
                        }
                    };
                }
                catch (InvalidOperationException ex)
                {
                    return new ResponseContext(false, 409, ex.Message);
                }
            });
        }

        /// <summary>
        /// Get specific index route.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        private async Task GetIndexRoute(HttpContextBase ctx)
        {
            await WrappedRequestHandler(ctx, RequestTypeEnum.IndexManagement, async (reqCtx) =>
            {
                string? indexId = ctx.Request.Url.Parameters["id"];
                if (String.IsNullOrEmpty(indexId))
                {
                    return new ResponseContext(false, 400, "Index ID is required");
                }

                var statistics = await _IndexManager!.GetIndexStatisticsAsync(indexId);
                if (statistics == null)
                {
                    return new ResponseContext(false, 404, "Index not found");
                }

                return new ResponseContext
                {
                    Success = true,
                    StatusCode = 200,
                    Data = statistics
                };
            });
        }

        /// <summary>
        /// Check if index exists route (HEAD).
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        private async Task HeadIndexRoute(HttpContextBase ctx)
        {
            await WrappedRequestHandler(ctx, RequestTypeEnum.IndexManagement, (reqCtx) =>
            {
                string? indexId = ctx.Request.Url.Parameters["id"];
                if (String.IsNullOrEmpty(indexId))
                {
                    return Task.FromResult(new ResponseContext(false, 400, "Index ID is required"));
                }

                if (!_IndexManager!.IndexExists(indexId))
                {
                    return Task.FromResult(new ResponseContext(false, 404, "Index not found"));
                }

                return Task.FromResult(new ResponseContext
                {
                    Success = true,
                    StatusCode = 200,
                    Data = null
                });
            });
        }

        /// <summary>
        /// Delete index route.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        private async Task DeleteIndexRoute(HttpContextBase ctx)
        {
            await WrappedRequestHandler(ctx, RequestTypeEnum.IndexManagement, async (reqCtx) =>
            {
                // Get tenant from auth context
                AuthContext? auth = await _Auth!.AuthenticateBearerAsync(GetAuthToken(ctx) ?? "").ConfigureAwait(false);
                string tenantId = !String.IsNullOrEmpty(auth?.TenantId) ? auth.TenantId : "default";

                string? indexId = ctx.Request.Url.Parameters["id"];
                if (String.IsNullOrEmpty(indexId))
                {
                    return new ResponseContext(false, 400, "Index ID is required");
                }

                if (!_IndexManager!.IndexExists(indexId))
                {
                    return new ResponseContext(false, 404, "Index not found");
                }

                bool deleted = await _IndexManager!.DeleteIndexAsync(tenantId, indexId).ConfigureAwait(false);
                if (deleted)
                {
                    return new ResponseContext
                    {
                        Success = true,
                        StatusCode = 200,
                        Data = new { Message = "Index deleted successfully", IndexId = indexId }
                    };
                }
                else
                {
                    return new ResponseContext(false, 500, "Failed to delete index");
                }
            });
        }

        /// <summary>
        /// Update index labels route.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        private async Task PutIndexLabelsRoute(HttpContextBase ctx)
        {
            await WrappedRequestHandler(ctx, RequestTypeEnum.IndexManagement, async (reqCtx) =>
            {
                // Get tenant from auth context
                AuthContext? auth = await _Auth!.AuthenticateBearerAsync(GetAuthToken(ctx) ?? "").ConfigureAwait(false);
                string tenantId = !String.IsNullOrEmpty(auth?.TenantId) ? auth.TenantId : "default";

                string? indexId = ctx.Request.Url.Parameters["id"];
                if (String.IsNullOrEmpty(indexId))
                {
                    return new ResponseContext(false, 400, "Index ID is required");
                }

                if (!_IndexManager!.IndexExists(indexId))
                {
                    return new ResponseContext(false, 404, "Index not found");
                }

                string body = await GetRequestBody(ctx).ConfigureAwait(false);
                if (String.IsNullOrEmpty(body))
                {
                    return new ResponseContext(false, 400, "Request body is required");
                }

                UpdateLabelsRequest? request = JsonSerializer.Deserialize<UpdateLabelsRequest>(body, _JsonOptions);
                if (request == null)
                {
                    return new ResponseContext(false, 400, "Invalid JSON in request body");
                }

                IndexMetadata? updated = await _IndexManager.UpdateIndexLabelsAsync(tenantId, indexId, request.Labels).ConfigureAwait(false);
                if (updated != null)
                {
                    return new ResponseContext
                    {
                        Success = true,
                        StatusCode = 200,
                        Data = new
                        {
                            Message = "Labels updated successfully",
                            Index = new
                            {
                                Identifier = updated.Identifier,
                                Name = updated.Name,
                                Labels = updated.Labels,
                                Tags = updated.Tags
                            }
                        }
                    };
                }
                else
                {
                    return new ResponseContext(false, 500, "Failed to update labels");
                }
            });
        }

        /// <summary>
        /// Update index tags route.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        private async Task PutIndexTagsRoute(HttpContextBase ctx)
        {
            await WrappedRequestHandler(ctx, RequestTypeEnum.IndexManagement, async (reqCtx) =>
            {
                // Get tenant from auth context
                AuthContext? auth = await _Auth!.AuthenticateBearerAsync(GetAuthToken(ctx) ?? "").ConfigureAwait(false);
                string tenantId = !String.IsNullOrEmpty(auth?.TenantId) ? auth.TenantId : "default";

                string? indexId = ctx.Request.Url.Parameters["id"];
                if (String.IsNullOrEmpty(indexId))
                {
                    return new ResponseContext(false, 400, "Index ID is required");
                }

                if (!_IndexManager!.IndexExists(indexId))
                {
                    return new ResponseContext(false, 404, "Index not found");
                }

                string body = await GetRequestBody(ctx).ConfigureAwait(false);
                if (String.IsNullOrEmpty(body))
                {
                    return new ResponseContext(false, 400, "Request body is required");
                }

                UpdateTagsRequest? request = JsonSerializer.Deserialize<UpdateTagsRequest>(body, _JsonOptions);
                if (request == null)
                {
                    return new ResponseContext(false, 400, "Invalid JSON in request body");
                }

                IndexMetadata? updated = await _IndexManager.UpdateIndexTagsAsync(tenantId, indexId, request.Tags).ConfigureAwait(false);
                if (updated != null)
                {
                    return new ResponseContext
                    {
                        Success = true,
                        StatusCode = 200,
                        Data = new
                        {
                            Message = "Tags updated successfully",
                            Index = new
                            {
                                Identifier = updated.Identifier,
                                Name = updated.Name,
                                Labels = updated.Labels,
                                Tags = updated.Tags
                            }
                        }
                    };
                }
                else
                {
                    return new ResponseContext(false, 500, "Failed to update tags");
                }
            });
        }

        /// <summary>
        /// Update index custom metadata route.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        private async Task PutIndexCustomMetadataRoute(HttpContextBase ctx)
        {
            await WrappedRequestHandler(ctx, RequestTypeEnum.IndexManagement, async (reqCtx) =>
            {
                // Get tenant from auth context
                AuthContext? auth = await _Auth!.AuthenticateBearerAsync(GetAuthToken(ctx) ?? "").ConfigureAwait(false);
                string tenantId = !String.IsNullOrEmpty(auth?.TenantId) ? auth.TenantId : "default";

                string? indexId = ctx.Request.Url.Parameters["id"];
                if (String.IsNullOrEmpty(indexId))
                {
                    return new ResponseContext(false, 400, "Index ID is required");
                }

                if (!_IndexManager!.IndexExists(indexId))
                {
                    return new ResponseContext(false, 404, "Index not found");
                }

                string body = await GetRequestBody(ctx).ConfigureAwait(false);
                if (String.IsNullOrEmpty(body))
                {
                    return new ResponseContext(false, 400, "Request body is required");
                }

                UpdateCustomMetadataRequest? request = JsonSerializer.Deserialize<UpdateCustomMetadataRequest>(body, _JsonOptions);
                if (request == null)
                {
                    return new ResponseContext(false, 400, "Invalid JSON in request body");
                }

                IndexMetadata? updated = await _IndexManager.UpdateIndexCustomMetadataAsync(tenantId, indexId, request.CustomMetadata).ConfigureAwait(false);
                if (updated != null)
                {
                    return new ResponseContext
                    {
                        Success = true,
                        StatusCode = 200,
                        Data = new
                        {
                            Message = "Custom metadata updated successfully",
                            Index = new
                            {
                                Identifier = updated.Identifier,
                                Name = updated.Name,
                                Labels = updated.Labels,
                                Tags = updated.Tags,
                                CustomMetadata = updated.CustomMetadata
                            }
                        }
                    };
                }
                else
                {
                    return new ResponseContext(false, 500, "Failed to update custom metadata");
                }
            });
        }

        /// <summary>
        /// Update document labels route.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        private async Task PutDocumentLabelsRoute(HttpContextBase ctx)
        {
            await WrappedRequestHandler(ctx, RequestTypeEnum.Document, async (reqCtx) =>
            {
                string? indexId = ctx.Request.Url.Parameters["id"];
                string? docId = ctx.Request.Url.Parameters["docId"];

                if (String.IsNullOrEmpty(indexId))
                {
                    return new ResponseContext(false, 400, "Index ID is required");
                }

                if (String.IsNullOrEmpty(docId))
                {
                    return new ResponseContext(false, 400, "Document ID is required");
                }

                InvertedIndex? index = _IndexManager!.GetIndex(indexId);
                if (index == null)
                {
                    return new ResponseContext(false, 404, "Index not found");
                }

                string body = await GetRequestBody(ctx).ConfigureAwait(false);
                if (String.IsNullOrEmpty(body))
                {
                    return new ResponseContext(false, 400, "Request body is required");
                }

                UpdateLabelsRequest? request = JsonSerializer.Deserialize<UpdateLabelsRequest>(body, _JsonOptions);
                if (request == null)
                {
                    return new ResponseContext(false, 400, "Invalid JSON in request body");
                }

                // Replace all labels with the new list (batch operation)
                await index.ReplaceLabelsAsync(docId, request.Labels ?? new List<string>()).ConfigureAwait(false);
                bool updated = true;
                if (updated)
                {
                    DocumentMetadata? document = await index.GetDocumentAsync(docId).ConfigureAwait(false);
                    return new ResponseContext
                    {
                        Success = true,
                        StatusCode = 200,
                        Data = new
                        {
                            Message = "Labels updated successfully",
                            Document = document
                        }
                    };
                }
                else
                {
                    return new ResponseContext(false, 404, "Document not found");
                }
            });
        }

        /// <summary>
        /// Update document tags route.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        private async Task PutDocumentTagsRoute(HttpContextBase ctx)
        {
            await WrappedRequestHandler(ctx, RequestTypeEnum.Document, async (reqCtx) =>
            {
                string? indexId = ctx.Request.Url.Parameters["id"];
                string? docId = ctx.Request.Url.Parameters["docId"];

                if (String.IsNullOrEmpty(indexId))
                {
                    return new ResponseContext(false, 400, "Index ID is required");
                }

                if (String.IsNullOrEmpty(docId))
                {
                    return new ResponseContext(false, 400, "Document ID is required");
                }

                InvertedIndex? index = _IndexManager!.GetIndex(indexId);
                if (index == null)
                {
                    return new ResponseContext(false, 404, "Index not found");
                }

                string body = await GetRequestBody(ctx).ConfigureAwait(false);
                if (String.IsNullOrEmpty(body))
                {
                    return new ResponseContext(false, 400, "Request body is required");
                }

                UpdateTagsRequest? request = JsonSerializer.Deserialize<UpdateTagsRequest>(body, _JsonOptions);
                if (request == null)
                {
                    return new ResponseContext(false, 400, "Invalid JSON in request body");
                }

                // Replace all tags with the new dictionary (batch operation)
                await index.ReplaceTagsAsync(docId, request.Tags ?? new Dictionary<string, string>()).ConfigureAwait(false);
                bool updated = true;
                if (updated)
                {
                    DocumentMetadata? document = await index.GetDocumentAsync(docId).ConfigureAwait(false);
                    return new ResponseContext
                    {
                        Success = true,
                        StatusCode = 200,
                        Data = new
                        {
                            Message = "Tags updated successfully",
                            Document = document
                        }
                    };
                }
                else
                {
                    return new ResponseContext(false, 404, "Document not found");
                }
            });
        }

        /// <summary>
        /// Update document custom metadata route.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        private async Task PutDocumentCustomMetadataRoute(HttpContextBase ctx)
        {
            await WrappedRequestHandler(ctx, RequestTypeEnum.Document, async (reqCtx) =>
            {
                string? indexId = ctx.Request.Url.Parameters["id"];
                string? docId = ctx.Request.Url.Parameters["docId"];

                if (String.IsNullOrEmpty(indexId))
                {
                    return new ResponseContext(false, 400, "Index ID is required");
                }

                if (String.IsNullOrEmpty(docId))
                {
                    return new ResponseContext(false, 400, "Document ID is required");
                }

                InvertedIndex? index = _IndexManager!.GetIndex(indexId);
                if (index == null)
                {
                    return new ResponseContext(false, 404, "Index not found");
                }

                string body = await GetRequestBody(ctx).ConfigureAwait(false);
                if (String.IsNullOrEmpty(body))
                {
                    return new ResponseContext(false, 400, "Request body is required");
                }

                UpdateCustomMetadataRequest? request = JsonSerializer.Deserialize<UpdateCustomMetadataRequest>(body, _JsonOptions);
                if (request == null)
                {
                    return new ResponseContext(false, 400, "Invalid JSON in request body");
                }

                try
                {
                    await index.SetCustomMetadataAsync(docId, request.CustomMetadata).ConfigureAwait(false);
                    DocumentMetadata? document = await index.GetDocumentAsync(docId).ConfigureAwait(false);
                    return new ResponseContext
                    {
                        Success = true,
                        StatusCode = 200,
                        Data = new
                        {
                            Message = "Custom metadata updated successfully",
                            Document = document
                        }
                    };
                }
                catch (Exception ex)
                {
                    return new ResponseContext(false, 500, $"Failed to update custom metadata: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Get documents for specific index route.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        private async Task GetIndexDocumentsRoute(HttpContextBase ctx)
        {
            await WrappedRequestHandler(ctx, RequestTypeEnum.Document, async (reqCtx) =>
            {
                string? indexId = ctx.Request.Url.Parameters["id"];
                if (String.IsNullOrEmpty(indexId))
                {
                    return new ResponseContext(false, 400, "Index ID is required");
                }

                var index = _IndexManager!.GetIndex(indexId);
                if (index == null)
                {
                    return new ResponseContext(false, 404, "Index not found");
                }

                try
                {
                    List<DocumentMetadata> documentList = await index.GetDocumentsAsync(limit: 1000).ConfigureAwait(false);
                    return new ResponseContext
                    {
                        Success = true,
                        StatusCode = 200,
                        Data = new { Documents = documentList, Count = documentList.Count }
                    };
                }
                catch (Exception ex)
                {
                    return new ResponseContext(false, 500, $"Error retrieving documents: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Add document to specific index route.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        private async Task PostIndexDocumentsRoute(HttpContextBase ctx)
        {
            await WrappedRequestHandler(ctx, RequestTypeEnum.Document, async (reqCtx) =>
            {
                string? indexId = ctx.Request.Url.Parameters["id"];
                if (String.IsNullOrEmpty(indexId))
                {
                    return new ResponseContext(false, 400, "Index ID is required");
                }

                var index = _IndexManager!.GetIndex(indexId);
                if (index == null)
                {
                    return new ResponseContext(false, 404, "Index not found");
                }

                string body = await GetRequestBody(ctx);
                if (String.IsNullOrEmpty(body))
                {
                    return new ResponseContext(false, 400, "Request body is required");
                }

                AddDocumentRequest? documentRequest = JsonSerializer.Deserialize<AddDocumentRequest>(body, _JsonOptions);
                if (documentRequest == null)
                {
                    return new ResponseContext(false, 400, "Invalid JSON in request body");
                }

                if (!documentRequest.Validate(out string errorMessage))
                {
                    return new ResponseContext(false, 400, errorMessage);
                }

                try
                {
                    // Add the document - use the correct overload based on whether ID is provided
                    string documentId;
                    if (!String.IsNullOrEmpty(documentRequest.Id))
                    {
                        // Use provided ID
                        documentId = documentRequest.Id;
                        await index.AddDocumentAsync(documentId, documentId, documentRequest.Content).ConfigureAwait(false);
                    }
                    else
                    {
                        // Let the index generate an ID
                        documentId = await index.AddDocumentAsync(
                            IdGenerator.GenerateDocumentId(),
                            documentRequest.Content).ConfigureAwait(false);
                    }

                    // Add labels if provided (batch operation)
                    if (documentRequest.Labels != null && documentRequest.Labels.Count > 0)
                    {
                        await index.AddLabelsBatchAsync(documentId, documentRequest.Labels).ConfigureAwait(false);
                    }

                    // Add tags if provided (batch operation)
                    if (documentRequest.Tags != null && documentRequest.Tags.Count > 0)
                    {
                        await index.AddTagsBatchAsync(documentId, documentRequest.Tags).ConfigureAwait(false);
                    }

                    // Set custom metadata if provided
                    if (documentRequest.CustomMetadata != null)
                    {
                        await index.SetCustomMetadataAsync(documentId, documentRequest.CustomMetadata).ConfigureAwait(false);
                    }

                    return new ResponseContext
                    {
                        Success = true,
                        StatusCode = 201,
                        Data = new { DocumentId = documentId, Message = "Document added successfully" }
                    };
                }
                catch (Exception ex)
                {
                    return new ResponseContext(false, 500, $"Error adding document: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Get specific document from index route.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        private async Task GetIndexDocumentRoute(HttpContextBase ctx)
        {
            await WrappedRequestHandler(ctx, RequestTypeEnum.Document, async (reqCtx) =>
            {
                string? indexId = ctx.Request.Url.Parameters["id"];
                string? docId = ctx.Request.Url.Parameters["docId"];

                if (String.IsNullOrEmpty(indexId))
                {
                    return new ResponseContext(false, 400, "Index ID is required");
                }

                if (String.IsNullOrEmpty(docId))
                {
                    return new ResponseContext(false, 400, "Document ID is required");
                }

                var index = _IndexManager!.GetIndex(indexId);
                if (index == null)
                {
                    return new ResponseContext(false, 404, "Index not found");
                }

                try
                {
                    // Use GetDocumentWithMetadataAsync for single query with JOINs
                    var document = await index.GetDocumentWithMetadataAsync(docId);
                    if (document == null)
                    {
                        return new ResponseContext(false, 404, "Document not found");
                    }

                    return new ResponseContext
                    {
                        Success = true,
                        StatusCode = 200,
                        Data = document
                    };
                }
                catch (Exception ex)
                {
                    return new ResponseContext(false, 500, $"Error retrieving document: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Check if document exists in index route (HEAD).
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        private async Task HeadIndexDocumentRoute(HttpContextBase ctx)
        {
            await WrappedRequestHandler(ctx, RequestTypeEnum.Document, async (reqCtx) =>
            {
                string? indexId = ctx.Request.Url.Parameters["id"];
                string? docId = ctx.Request.Url.Parameters["docId"];

                if (String.IsNullOrEmpty(indexId))
                {
                    return new ResponseContext(false, 400, "Index ID is required");
                }

                if (String.IsNullOrEmpty(docId))
                {
                    return new ResponseContext(false, 400, "Document ID is required");
                }

                var index = _IndexManager!.GetIndex(indexId);
                if (index == null)
                {
                    return new ResponseContext(false, 404, "Index not found");
                }

                bool exists = await index.DocumentExistsAsync(docId).ConfigureAwait(false);
                if (!exists)
                {
                    return new ResponseContext(false, 404, "Document not found");
                }

                return new ResponseContext
                {
                    Success = true,
                    StatusCode = 200,
                    Data = null
                };
            });
        }

        /// <summary>
        /// Delete document from index route.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        private async Task DeleteIndexDocumentRoute(HttpContextBase ctx)
        {
            await WrappedRequestHandler(ctx, RequestTypeEnum.Document, async (reqCtx) =>
            {
                string? indexId = ctx.Request.Url.Parameters["id"];
                string? docId = ctx.Request.Url.Parameters["docId"];

                if (String.IsNullOrEmpty(indexId))
                {
                    return new ResponseContext(false, 400, "Index ID is required");
                }

                if (String.IsNullOrEmpty(docId))
                {
                    return new ResponseContext(false, 400, "Document ID is required");
                }

                var index = _IndexManager!.GetIndex(indexId);
                if (index == null)
                {
                    return new ResponseContext(false, 404, "Index not found");
                }

                try
                {
                    bool removed = await index.RemoveDocumentAsync(docId);
                    if (removed)
                    {
                        return new ResponseContext
                        {
                            Success = true,
                            StatusCode = 200,
                            Data = new { DocumentId = docId, Message = "Document deleted successfully" }
                        };
                    }
                    else
                    {
                        return new ResponseContext(false, 404, "Document not found");
                    }
                }
                catch (Exception ex)
                {
                    return new ResponseContext(false, 500, $"Error deleting document: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Search documents in specific index route.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        private async Task PostIndexSearchRoute(HttpContextBase ctx)
        {
            await WrappedRequestHandler(ctx, RequestTypeEnum.Search, async (reqCtx) =>
            {
                string? indexId = ctx.Request.Url.Parameters["id"];
                if (String.IsNullOrEmpty(indexId))
                {
                    return new ResponseContext(false, 400, "Index ID is required");
                }

                var index = _IndexManager!.GetIndex(indexId);
                if (index == null)
                {
                    return new ResponseContext(false, 404, "Index not found");
                }

                string body = await GetRequestBody(ctx);
                if (String.IsNullOrEmpty(body))
                {
                    return new ResponseContext(false, 400, "Request body is required");
                }

                SearchRequest? searchRequest = JsonSerializer.Deserialize<SearchRequest>(body, _JsonOptions);
                if (searchRequest == null)
                {
                    return new ResponseContext(false, 400, "Invalid JSON in request body");
                }

                if (!searchRequest.Validate(out string errorMessage))
                {
                    return new ResponseContext(false, 400, errorMessage);
                }

                try
                {
                    // Convert tags from Dictionary<string, object> to Dictionary<string, string>
                    Dictionary<string, string>? tagFilters = null;
                    if (searchRequest.Tags != null && searchRequest.Tags.Count > 0)
                    {
                        tagFilters = new Dictionary<string, string>();
                        foreach (KeyValuePair<string, object> kvp in searchRequest.Tags)
                        {
                            tagFilters[kvp.Key] = kvp.Value?.ToString() ?? "";
                        }
                    }

                    // Perform search with label/tag filtering at the SQL level
                    SearchResults searchResults = await index.SearchAsync(
                        searchRequest.Query,
                        searchRequest.MaxResults,
                        searchRequest.UseAndLogic,
                        searchRequest.Labels,
                        tagFilters).ConfigureAwait(false);

                    return new ResponseContext
                    {
                        Success = true,
                        StatusCode = 200,
                        Data = new {
                            Query = searchRequest.Query,
                            Results = searchResults.Results,
                            TotalCount = searchResults.TotalCount,
                            MaxResults = searchRequest.MaxResults,
                            SearchTime = searchResults.SearchTime.TotalMilliseconds
                        }
                    };
                }
                catch (Exception ex)
                {
                    return new ResponseContext(false, 500, $"Error performing search: {ex.Message}");
                }
            });
        }

        // ==================== Admin Route Handlers ====================

        /// <summary>
        /// Get all tenants route.
        /// </summary>
        private async Task GetTenantsRoute(HttpContextBase ctx)
        {
            await WrappedRequestHandler(ctx, RequestTypeEnum.IndexManagement, async (reqCtx) =>
            {
                AuthContext? auth = await _Auth!.AuthenticateBearerAsync(GetAuthToken(ctx) ?? "").ConfigureAwait(false);
                if (auth == null || !auth.HasAdminAccess)
                {
                    return new ResponseContext(false, 403, "Admin access required");
                }

                List<TenantMetadata> tenants;
                if (auth.IsGlobalAdmin)
                {
                    // Global admins can see all tenants
                    tenants = await _Database!.Tenants.ReadManyAsync().ConfigureAwait(false);
                }
                else
                {
                    // Tenant admins can only see their own tenant
                    TenantMetadata? tenant = await _Database!.Tenants.ReadByIdentifierAsync(auth.TenantId).ConfigureAwait(false);
                    tenants = tenant != null ? new List<TenantMetadata> { tenant } : new List<TenantMetadata>();
                }

                return new ResponseContext
                {
                    Success = true,
                    StatusCode = 200,
                    Data = new { Tenants = tenants, Count = tenants.Count }
                };
            });
        }

        /// <summary>
        /// Create tenant route.
        /// </summary>
        private async Task PostTenantsRoute(HttpContextBase ctx)
        {
            await WrappedRequestHandler(ctx, RequestTypeEnum.IndexManagement, async (reqCtx) =>
            {
                AuthContext? auth = await _Auth!.AuthenticateBearerAsync(GetAuthToken(ctx) ?? "").ConfigureAwait(false);
                if (auth == null || !auth.IsGlobalAdmin)
                {
                    return new ResponseContext(false, 403, "Global admin access required");
                }

                string body = await GetRequestBody(ctx);
                if (String.IsNullOrEmpty(body))
                {
                    return new ResponseContext(false, 400, "Request body is required");
                }

                CreateTenantRequest? request = JsonSerializer.Deserialize<CreateTenantRequest>(body, _JsonOptions);
                string error = "Invalid request";
                if (request == null || !request.Validate(out error))
                {
                    return new ResponseContext(false, 400, error);
                }

                TenantMetadata tenant = new TenantMetadata(request.Name, request.Description ?? "");

                await _Database!.Tenants.CreateAsync(tenant).ConfigureAwait(false);

                return new ResponseContext
                {
                    Success = true,
                    StatusCode = 201,
                    Data = new { Message = "Tenant created successfully", Tenant = tenant }
                };
            });
        }

        /// <summary>
        /// Get tenant route.
        /// </summary>
        private async Task GetTenantRoute(HttpContextBase ctx)
        {
            await WrappedRequestHandler(ctx, RequestTypeEnum.IndexManagement, async (reqCtx) =>
            {
                string? tenantId = ctx.Request.Url.Parameters["id"];
                if (String.IsNullOrEmpty(tenantId))
                {
                    return new ResponseContext(false, 400, "Tenant ID is required");
                }

                AuthContext? auth = await _Auth!.AuthenticateBearerAsync(GetAuthToken(ctx) ?? "").ConfigureAwait(false);
                // Require admin access and correct tenant for non-global admins
                if (auth == null || !auth.HasAdminAccess || (!auth.IsGlobalAdmin && auth.TenantId != tenantId))
                {
                    return new ResponseContext(false, 403, "Admin access required");
                }

                TenantMetadata? tenant = await _Database!.Tenants.ReadByIdentifierAsync(tenantId).ConfigureAwait(false);
                if (tenant == null)
                {
                    return new ResponseContext(false, 404, "Tenant not found");
                }

                TenantStatistics stats = await _Database.Statistics.GetTenantStatisticsAsync(tenantId).ConfigureAwait(false);

                return new ResponseContext
                {
                    Success = true,
                    StatusCode = 200,
                    Data = new { Tenant = tenant, Statistics = stats }
                };
            });
        }

        /// <summary>
        /// Delete tenant route.
        /// </summary>
        private async Task DeleteTenantRoute(HttpContextBase ctx)
        {
            await WrappedRequestHandler(ctx, RequestTypeEnum.IndexManagement, async (reqCtx) =>
            {
                AuthContext? auth = await _Auth!.AuthenticateBearerAsync(GetAuthToken(ctx) ?? "").ConfigureAwait(false);
                if (auth == null || !auth.IsGlobalAdmin)
                {
                    return new ResponseContext(false, 403, "Global admin access required");
                }

                string? tenantId = ctx.Request.Url.Parameters["id"];
                if (String.IsNullOrEmpty(tenantId))
                {
                    return new ResponseContext(false, 400, "Tenant ID is required");
                }

                bool deleted = await _Database!.Tenants.DeleteByIdentifierAsync(tenantId).ConfigureAwait(false);
                if (!deleted)
                {
                    return new ResponseContext(false, 404, "Tenant not found");
                }

                return new ResponseContext
                {
                    Success = true,
                    StatusCode = 200,
                    Data = new { Message = "Tenant deleted successfully", TenantId = tenantId }
                };
            });
        }

        /// <summary>
        /// Update tenant route.
        /// </summary>
        private async Task PutTenantRoute(HttpContextBase ctx)
        {
            await WrappedRequestHandler(ctx, RequestTypeEnum.IndexManagement, async (reqCtx) =>
            {
                AuthContext? auth = await _Auth!.AuthenticateBearerAsync(GetAuthToken(ctx) ?? "").ConfigureAwait(false);
                if (auth == null || !auth.IsGlobalAdmin)
                {
                    return new ResponseContext(false, 403, "Global admin access required");
                }

                string? tenantId = ctx.Request.Url.Parameters["id"];
                if (String.IsNullOrEmpty(tenantId))
                {
                    return new ResponseContext(false, 400, "Tenant ID is required");
                }

                TenantMetadata? tenant = await _Database!.Tenants.ReadByIdentifierAsync(tenantId).ConfigureAwait(false);
                if (tenant == null)
                {
                    return new ResponseContext(false, 404, "Tenant not found");
                }

                string body = await GetRequestBody(ctx);
                if (String.IsNullOrEmpty(body))
                {
                    return new ResponseContext(false, 400, "Request body is required");
                }

                UpdateTenantRequest? request = JsonSerializer.Deserialize<UpdateTenantRequest>(body, _JsonOptions);
                if (request == null)
                {
                    return new ResponseContext(false, 400, "Invalid request");
                }

                // Apply updates
                if (request.Name != null) tenant.Name = request.Name;
                if (request.Description != null) tenant.Description = request.Description;
                if (request.Active.HasValue) tenant.Active = request.Active.Value;
                tenant.LastUpdateUtc = DateTime.UtcNow;

                await _Database.Tenants.UpdateAsync(tenant).ConfigureAwait(false);

                return new ResponseContext
                {
                    Success = true,
                    StatusCode = 200,
                    Data = new { Message = "Tenant updated successfully", Tenant = tenant }
                };
            });
        }

        /// <summary>
        /// Get tenant users route.
        /// </summary>
        private async Task GetTenantUsersRoute(HttpContextBase ctx)
        {
            await WrappedRequestHandler(ctx, RequestTypeEnum.IndexManagement, async (reqCtx) =>
            {
                string? tenantId = ctx.Request.Url.Parameters["id"];
                if (String.IsNullOrEmpty(tenantId))
                {
                    return new ResponseContext(false, 400, "Tenant ID is required");
                }

                AuthContext? auth = await _Auth!.AuthenticateBearerAsync(GetAuthToken(ctx) ?? "").ConfigureAwait(false);
                // Require admin access and correct tenant for non-global admins
                if (auth == null || !auth.HasAdminAccess || (!auth.IsGlobalAdmin && auth.TenantId != tenantId))
                {
                    return new ResponseContext(false, 403, "Admin access required");
                }

                List<UserMaster> users = await _Database!.Users.ReadManyAsync(tenantId).ConfigureAwait(false);
                return new ResponseContext
                {
                    Success = true,
                    StatusCode = 200,
                    Data = new { Users = users.Select(u => new { u.Identifier, u.TenantId, u.Email, u.FirstName, u.LastName, u.IsAdmin, u.Active, u.CreatedUtc }), Count = users.Count }
                };
            });
        }

        /// <summary>
        /// Create tenant user route.
        /// </summary>
        private async Task PostTenantUsersRoute(HttpContextBase ctx)
        {
            await WrappedRequestHandler(ctx, RequestTypeEnum.IndexManagement, async (reqCtx) =>
            {
                string? tenantId = ctx.Request.Url.Parameters["id"];
                if (String.IsNullOrEmpty(tenantId))
                {
                    return new ResponseContext(false, 400, "Tenant ID is required");
                }

                AuthContext? auth = await _Auth!.AuthenticateBearerAsync(GetAuthToken(ctx) ?? "").ConfigureAwait(false);
                // Require admin access and correct tenant for non-global admins
                if (auth == null || !auth.HasAdminAccess || (!auth.IsGlobalAdmin && auth.TenantId != tenantId))
                {
                    return new ResponseContext(false, 403, "Admin access required");
                }

                string body = await GetRequestBody(ctx);
                if (String.IsNullOrEmpty(body))
                {
                    return new ResponseContext(false, 400, "Request body is required");
                }

                CreateUserRequest? request = JsonSerializer.Deserialize<CreateUserRequest>(body, _JsonOptions);
                string error = "Invalid request";
                if (request == null || !request.Validate(out error))
                {
                    return new ResponseContext(false, 400, error);
                }

                // Check if user already exists
                UserMaster? existing = await _Database!.Users.ReadByEmailAsync(tenantId, request.Email).ConfigureAwait(false);
                if (existing != null)
                {
                    return new ResponseContext(false, 409, "User with this email already exists");
                }

                UserMaster user = new UserMaster(tenantId, request.Email);
                user.SetPassword(request.Password);
                user.FirstName = request.FirstName;
                user.LastName = request.LastName;
                user.IsAdmin = request.IsAdmin;
                user.Active = true;

                await _Database.Users.CreateAsync(user).ConfigureAwait(false);

                return new ResponseContext
                {
                    Success = true,
                    StatusCode = 201,
                    Data = new { Message = "User created successfully", User = new { user.Identifier, user.Email, user.FirstName, user.LastName, user.IsAdmin } }
                };
            });
        }

        /// <summary>
        /// Get tenant user route.
        /// </summary>
        private async Task GetTenantUserRoute(HttpContextBase ctx)
        {
            await WrappedRequestHandler(ctx, RequestTypeEnum.IndexManagement, async (reqCtx) =>
            {
                string? tenantId = ctx.Request.Url.Parameters["id"];
                string? userId = ctx.Request.Url.Parameters["userId"];

                if (String.IsNullOrEmpty(tenantId) || String.IsNullOrEmpty(userId))
                {
                    return new ResponseContext(false, 400, "Tenant ID and User ID are required");
                }

                AuthContext? auth = await _Auth!.AuthenticateBearerAsync(GetAuthToken(ctx) ?? "").ConfigureAwait(false);
                // Require admin access and correct tenant for non-global admins
                if (auth == null || !auth.HasAdminAccess || (!auth.IsGlobalAdmin && auth.TenantId != tenantId))
                {
                    return new ResponseContext(false, 403, "Admin access required");
                }

                UserMaster? user = await _Database!.Users.ReadByIdentifierAsync(tenantId, userId).ConfigureAwait(false);
                if (user == null)
                {
                    return new ResponseContext(false, 404, "User not found");
                }

                return new ResponseContext
                {
                    Success = true,
                    StatusCode = 200,
                    Data = new { user.Identifier, user.TenantId, user.Email, user.FirstName, user.LastName, user.IsAdmin, user.Active, user.CreatedUtc }
                };
            });
        }

        /// <summary>
        /// Delete tenant user route.
        /// </summary>
        private async Task DeleteTenantUserRoute(HttpContextBase ctx)
        {
            await WrappedRequestHandler(ctx, RequestTypeEnum.IndexManagement, async (reqCtx) =>
            {
                string? tenantId = ctx.Request.Url.Parameters["id"];
                string? userId = ctx.Request.Url.Parameters["userId"];

                if (String.IsNullOrEmpty(tenantId) || String.IsNullOrEmpty(userId))
                {
                    return new ResponseContext(false, 400, "Tenant ID and User ID are required");
                }

                AuthContext? auth = await _Auth!.AuthenticateBearerAsync(GetAuthToken(ctx) ?? "").ConfigureAwait(false);
                // Require admin access and correct tenant for non-global admins
                if (auth == null || !auth.HasAdminAccess || (!auth.IsGlobalAdmin && auth.TenantId != tenantId))
                {
                    return new ResponseContext(false, 403, "Admin access required");
                }

                bool deleted = await _Database!.Users.DeleteByIdentifierAsync(tenantId, userId).ConfigureAwait(false);
                if (!deleted)
                {
                    return new ResponseContext(false, 404, "User not found");
                }

                return new ResponseContext
                {
                    Success = true,
                    StatusCode = 200,
                    Data = new { Message = "User deleted successfully", UserId = userId }
                };
            });
        }

        /// <summary>
        /// Update tenant user route.
        /// </summary>
        private async Task PutTenantUserRoute(HttpContextBase ctx)
        {
            await WrappedRequestHandler(ctx, RequestTypeEnum.IndexManagement, async (reqCtx) =>
            {
                string? tenantId = ctx.Request.Url.Parameters["id"];
                string? userId = ctx.Request.Url.Parameters["userId"];

                if (String.IsNullOrEmpty(tenantId) || String.IsNullOrEmpty(userId))
                {
                    return new ResponseContext(false, 400, "Tenant ID and User ID are required");
                }

                AuthContext? auth = await _Auth!.AuthenticateBearerAsync(GetAuthToken(ctx) ?? "").ConfigureAwait(false);
                // Require admin access and correct tenant for non-global admins
                if (auth == null || !auth.HasAdminAccess || (!auth.IsGlobalAdmin && auth.TenantId != tenantId))
                {
                    return new ResponseContext(false, 403, "Admin access required");
                }

                UserMaster? user = await _Database!.Users.ReadByIdentifierAsync(tenantId, userId).ConfigureAwait(false);
                if (user == null)
                {
                    return new ResponseContext(false, 404, "User not found");
                }

                string body = await GetRequestBody(ctx);
                if (String.IsNullOrEmpty(body))
                {
                    return new ResponseContext(false, 400, "Request body is required");
                }

                UpdateUserRequest? request = JsonSerializer.Deserialize<UpdateUserRequest>(body, _JsonOptions);
                if (request == null)
                {
                    return new ResponseContext(false, 400, "Invalid request");
                }

                // Apply updates
                if (request.Email != null) user.Email = request.Email;
                if (request.Password != null) user.SetPassword(request.Password);
                if (request.FirstName != null) user.FirstName = request.FirstName;
                if (request.LastName != null) user.LastName = request.LastName;
                if (request.IsAdmin.HasValue) user.IsAdmin = request.IsAdmin.Value;
                if (request.Active.HasValue) user.Active = request.Active.Value;
                user.LastUpdateUtc = DateTime.UtcNow;

                await _Database.Users.UpdateAsync(user).ConfigureAwait(false);

                return new ResponseContext
                {
                    Success = true,
                    StatusCode = 200,
                    Data = new { Message = "User updated successfully", User = new { user.Identifier, user.TenantId, user.Email, user.FirstName, user.LastName, user.IsAdmin, user.Active, user.CreatedUtc } }
                };
            });
        }

        /// <summary>
        /// Get tenant credentials route.
        /// </summary>
        private async Task GetTenantCredentialsRoute(HttpContextBase ctx)
        {
            await WrappedRequestHandler(ctx, RequestTypeEnum.IndexManagement, async (reqCtx) =>
            {
                string? tenantId = ctx.Request.Url.Parameters["id"];
                if (String.IsNullOrEmpty(tenantId))
                {
                    return new ResponseContext(false, 400, "Tenant ID is required");
                }

                AuthContext? auth = await _Auth!.AuthenticateBearerAsync(GetAuthToken(ctx) ?? "").ConfigureAwait(false);
                // Require admin access and correct tenant for non-global admins
                if (auth == null || !auth.HasAdminAccess || (!auth.IsGlobalAdmin && auth.TenantId != tenantId))
                {
                    return new ResponseContext(false, 403, "Admin access required");
                }

                List<Credential> credentials = await _Database!.Credentials.ReadManyAsync(tenantId).ConfigureAwait(false);
                return new ResponseContext
                {
                    Success = true,
                    StatusCode = 200,
                    Data = new { Credentials = credentials.Select(c => new { c.Identifier, c.TenantId, c.UserId, c.Name, c.Active, c.CreatedUtc, TokenPreview = c.BearerToken.Substring(0, Math.Min(8, c.BearerToken.Length)) + "..." }), Count = credentials.Count }
                };
            });
        }

        /// <summary>
        /// Create tenant credential route.
        /// </summary>
        private async Task PostTenantCredentialsRoute(HttpContextBase ctx)
        {
            await WrappedRequestHandler(ctx, RequestTypeEnum.IndexManagement, async (reqCtx) =>
            {
                string? tenantId = ctx.Request.Url.Parameters["id"];
                if (String.IsNullOrEmpty(tenantId))
                {
                    return new ResponseContext(false, 400, "Tenant ID is required");
                }

                AuthContext? auth = await _Auth!.AuthenticateBearerAsync(GetAuthToken(ctx) ?? "").ConfigureAwait(false);
                // Require admin access and correct tenant for non-global admins
                if (auth == null || !auth.HasAdminAccess || (!auth.IsGlobalAdmin && auth.TenantId != tenantId))
                {
                    return new ResponseContext(false, 403, "Admin access required");
                }

                string body = await GetRequestBody(ctx);
                CreateCredentialRequest? request = null;
                if (!String.IsNullOrEmpty(body))
                {
                    request = JsonSerializer.Deserialize<CreateCredentialRequest>(body, _JsonOptions);
                }

                // Determine the user ID to associate with the credential
                string userId;
                if (!String.IsNullOrEmpty(auth.UserId) && auth.TenantId == tenantId)
                {
                    // Use the authenticated user's ID if they belong to this tenant
                    userId = auth.UserId;
                }
                else
                {
                    // For global admin or cross-tenant operations, find the first admin user for the tenant
                    List<UserMaster> tenantUsers = new List<UserMaster>();
                    await foreach (UserMaster user in _Database!.Users.ReadAllAsync(tenantId).ConfigureAwait(false))
                    {
                        tenantUsers.Add(user);
                    }
                    UserMaster? adminUser = tenantUsers.FirstOrDefault(u => u.IsAdmin && u.Active);
                    if (adminUser == null)
                    {
                        adminUser = tenantUsers.FirstOrDefault(u => u.Active);
                    }
                    if (adminUser == null)
                    {
                        return new ResponseContext(false, 400, "No active users found for this tenant. Create a user first before creating credentials.");
                    }
                    userId = adminUser.Identifier;
                }

                Credential credential = new Credential(tenantId, userId);
                if (request?.Description != null)
                {
                    credential.Name = request.Description;
                }

                await _Database!.Credentials.CreateAsync(credential).ConfigureAwait(false);

                return new ResponseContext
                {
                    Success = true,
                    StatusCode = 201,
                    Data = new { Message = "Credential created successfully", Credential = new { credential.Identifier, credential.BearerToken, credential.Name } }
                };
            });
        }

        /// <summary>
        /// Delete tenant credential route.
        /// </summary>
        private async Task DeleteTenantCredentialRoute(HttpContextBase ctx)
        {
            await WrappedRequestHandler(ctx, RequestTypeEnum.IndexManagement, async (reqCtx) =>
            {
                string? tenantId = ctx.Request.Url.Parameters["id"];
                string? credId = ctx.Request.Url.Parameters["credId"];

                if (String.IsNullOrEmpty(tenantId) || String.IsNullOrEmpty(credId))
                {
                    return new ResponseContext(false, 400, "Tenant ID and Credential ID are required");
                }

                AuthContext? auth = await _Auth!.AuthenticateBearerAsync(GetAuthToken(ctx) ?? "").ConfigureAwait(false);
                // Require admin access and correct tenant for non-global admins
                if (auth == null || !auth.HasAdminAccess || (!auth.IsGlobalAdmin && auth.TenantId != tenantId))
                {
                    return new ResponseContext(false, 403, "Admin access required");
                }

                bool deleted = await _Database!.Credentials.DeleteByIdentifierAsync(tenantId, credId).ConfigureAwait(false);
                if (!deleted)
                {
                    return new ResponseContext(false, 404, "Credential not found");
                }

                return new ResponseContext
                {
                    Success = true,
                    StatusCode = 200,
                    Data = new { Message = "Credential revoked successfully", CredentialId = credId }
                };
            });
        }

        /// <summary>
        /// Update tenant credential route.
        /// </summary>
        private async Task PutTenantCredentialRoute(HttpContextBase ctx)
        {
            await WrappedRequestHandler(ctx, RequestTypeEnum.IndexManagement, async (reqCtx) =>
            {
                string? tenantId = ctx.Request.Url.Parameters["id"];
                string? credId = ctx.Request.Url.Parameters["credId"];

                if (String.IsNullOrEmpty(tenantId) || String.IsNullOrEmpty(credId))
                {
                    return new ResponseContext(false, 400, "Tenant ID and Credential ID are required");
                }

                AuthContext? auth = await _Auth!.AuthenticateBearerAsync(GetAuthToken(ctx) ?? "").ConfigureAwait(false);
                if (auth == null || !auth.HasAdminAccess || (!auth.IsGlobalAdmin && auth.TenantId != tenantId))
                {
                    return new ResponseContext(false, 403, "Admin access required");
                }

                Credential? credential = await _Database!.Credentials.ReadByIdentifierAsync(tenantId, credId).ConfigureAwait(false);
                if (credential == null)
                {
                    return new ResponseContext(false, 404, "Credential not found");
                }

                string body = await GetRequestBody(ctx);
                if (String.IsNullOrEmpty(body))
                {
                    return new ResponseContext(false, 400, "Request body is required");
                }

                UpdateCredentialRequest? request = JsonSerializer.Deserialize<UpdateCredentialRequest>(body, _JsonOptions);
                if (request == null)
                {
                    return new ResponseContext(false, 400, "Invalid request");
                }

                if (request.Name != null)
                {
                    credential.Name = request.Name;
                }
                if (request.Active.HasValue)
                {
                    credential.Active = request.Active.Value;
                }

                await _Database.Credentials.UpdateAsync(credential).ConfigureAwait(false);

                return new ResponseContext
                {
                    Success = true,
                    StatusCode = 200,
                    Data = new { Message = "Credential updated successfully", Credential = new { credential.Identifier, credential.TenantId, credential.Name, credential.Active } }
                };
            });
        }

        /// <summary>
        /// Update tenant labels route.
        /// </summary>
        private async Task PutTenantLabelsRoute(HttpContextBase ctx)
        {
            await WrappedRequestHandler(ctx, RequestTypeEnum.IndexManagement, async (reqCtx) =>
            {
                string? tenantId = ctx.Request.Url.Parameters["id"];
                if (String.IsNullOrEmpty(tenantId))
                {
                    return new ResponseContext(false, 400, "Tenant ID is required");
                }

                AuthContext? auth = await _Auth!.AuthenticateBearerAsync(GetAuthToken(ctx) ?? "").ConfigureAwait(false);
                if (auth == null || !auth.HasAdminAccess || (!auth.IsGlobalAdmin && auth.TenantId != tenantId))
                {
                    return new ResponseContext(false, 403, "Admin access required");
                }

                TenantMetadata? tenant = await _Database!.Tenants.ReadByIdentifierAsync(tenantId).ConfigureAwait(false);
                if (tenant == null)
                {
                    return new ResponseContext(false, 404, "Tenant not found");
                }

                string body = await GetRequestBody(ctx);
                if (String.IsNullOrEmpty(body))
                {
                    return new ResponseContext(false, 400, "Request body is required");
                }

                UpdateLabelsRequest? request = JsonSerializer.Deserialize<UpdateLabelsRequest>(body, _JsonOptions);
                if (request == null)
                {
                    return new ResponseContext(false, 400, "Invalid request");
                }

                await _Database.Labels.ReplaceTenantLabelsAsync(tenantId, request.Labels).ConfigureAwait(false);

                return new ResponseContext
                {
                    Success = true,
                    StatusCode = 200,
                    Data = new { Message = "Tenant labels updated successfully", TenantId = tenantId, Labels = request.Labels }
                };
            });
        }

        /// <summary>
        /// Update tenant tags route.
        /// </summary>
        private async Task PutTenantTagsRoute(HttpContextBase ctx)
        {
            await WrappedRequestHandler(ctx, RequestTypeEnum.IndexManagement, async (reqCtx) =>
            {
                string? tenantId = ctx.Request.Url.Parameters["id"];
                if (String.IsNullOrEmpty(tenantId))
                {
                    return new ResponseContext(false, 400, "Tenant ID is required");
                }

                AuthContext? auth = await _Auth!.AuthenticateBearerAsync(GetAuthToken(ctx) ?? "").ConfigureAwait(false);
                if (auth == null || !auth.HasAdminAccess || (!auth.IsGlobalAdmin && auth.TenantId != tenantId))
                {
                    return new ResponseContext(false, 403, "Admin access required");
                }

                TenantMetadata? tenant = await _Database!.Tenants.ReadByIdentifierAsync(tenantId).ConfigureAwait(false);
                if (tenant == null)
                {
                    return new ResponseContext(false, 404, "Tenant not found");
                }

                string body = await GetRequestBody(ctx);
                if (String.IsNullOrEmpty(body))
                {
                    return new ResponseContext(false, 400, "Request body is required");
                }

                UpdateTagsRequest? request = JsonSerializer.Deserialize<UpdateTagsRequest>(body, _JsonOptions);
                if (request == null)
                {
                    return new ResponseContext(false, 400, "Invalid request");
                }

                await _Database.Tags.ReplaceTenantTagsAsync(tenantId, request.Tags).ConfigureAwait(false);

                return new ResponseContext
                {
                    Success = true,
                    StatusCode = 200,
                    Data = new { Message = "Tenant tags updated successfully", TenantId = tenantId, Tags = request.Tags }
                };
            });
        }

        /// <summary>
        /// Update user labels route.
        /// </summary>
        private async Task PutUserLabelsRoute(HttpContextBase ctx)
        {
            await WrappedRequestHandler(ctx, RequestTypeEnum.IndexManagement, async (reqCtx) =>
            {
                string? tenantId = ctx.Request.Url.Parameters["id"];
                string? userId = ctx.Request.Url.Parameters["userId"];

                if (String.IsNullOrEmpty(tenantId) || String.IsNullOrEmpty(userId))
                {
                    return new ResponseContext(false, 400, "Tenant ID and User ID are required");
                }

                AuthContext? auth = await _Auth!.AuthenticateBearerAsync(GetAuthToken(ctx) ?? "").ConfigureAwait(false);
                if (auth == null || !auth.HasAdminAccess || (!auth.IsGlobalAdmin && auth.TenantId != tenantId))
                {
                    return new ResponseContext(false, 403, "Admin access required");
                }

                UserMaster? user = await _Database!.Users.ReadByIdentifierAsync(tenantId, userId).ConfigureAwait(false);
                if (user == null)
                {
                    return new ResponseContext(false, 404, "User not found");
                }

                string body = await GetRequestBody(ctx);
                if (String.IsNullOrEmpty(body))
                {
                    return new ResponseContext(false, 400, "Request body is required");
                }

                UpdateLabelsRequest? request = JsonSerializer.Deserialize<UpdateLabelsRequest>(body, _JsonOptions);
                if (request == null)
                {
                    return new ResponseContext(false, 400, "Invalid request");
                }

                await _Database.Labels.ReplaceUserLabelsAsync(tenantId, userId, request.Labels).ConfigureAwait(false);

                return new ResponseContext
                {
                    Success = true,
                    StatusCode = 200,
                    Data = new { Message = "User labels updated successfully", UserId = userId, Labels = request.Labels }
                };
            });
        }

        /// <summary>
        /// Update user tags route.
        /// </summary>
        private async Task PutUserTagsRoute(HttpContextBase ctx)
        {
            await WrappedRequestHandler(ctx, RequestTypeEnum.IndexManagement, async (reqCtx) =>
            {
                string? tenantId = ctx.Request.Url.Parameters["id"];
                string? userId = ctx.Request.Url.Parameters["userId"];

                if (String.IsNullOrEmpty(tenantId) || String.IsNullOrEmpty(userId))
                {
                    return new ResponseContext(false, 400, "Tenant ID and User ID are required");
                }

                AuthContext? auth = await _Auth!.AuthenticateBearerAsync(GetAuthToken(ctx) ?? "").ConfigureAwait(false);
                if (auth == null || !auth.HasAdminAccess || (!auth.IsGlobalAdmin && auth.TenantId != tenantId))
                {
                    return new ResponseContext(false, 403, "Admin access required");
                }

                UserMaster? user = await _Database!.Users.ReadByIdentifierAsync(tenantId, userId).ConfigureAwait(false);
                if (user == null)
                {
                    return new ResponseContext(false, 404, "User not found");
                }

                string body = await GetRequestBody(ctx);
                if (String.IsNullOrEmpty(body))
                {
                    return new ResponseContext(false, 400, "Request body is required");
                }

                UpdateTagsRequest? request = JsonSerializer.Deserialize<UpdateTagsRequest>(body, _JsonOptions);
                if (request == null)
                {
                    return new ResponseContext(false, 400, "Invalid request");
                }

                await _Database.Tags.ReplaceUserTagsAsync(tenantId, userId, request.Tags).ConfigureAwait(false);

                return new ResponseContext
                {
                    Success = true,
                    StatusCode = 200,
                    Data = new { Message = "User tags updated successfully", UserId = userId, Tags = request.Tags }
                };
            });
        }

        /// <summary>
        /// Update credential labels route.
        /// </summary>
        private async Task PutCredentialLabelsRoute(HttpContextBase ctx)
        {
            await WrappedRequestHandler(ctx, RequestTypeEnum.IndexManagement, async (reqCtx) =>
            {
                string? tenantId = ctx.Request.Url.Parameters["id"];
                string? credId = ctx.Request.Url.Parameters["credId"];

                if (String.IsNullOrEmpty(tenantId) || String.IsNullOrEmpty(credId))
                {
                    return new ResponseContext(false, 400, "Tenant ID and Credential ID are required");
                }

                AuthContext? auth = await _Auth!.AuthenticateBearerAsync(GetAuthToken(ctx) ?? "").ConfigureAwait(false);
                if (auth == null || !auth.HasAdminAccess || (!auth.IsGlobalAdmin && auth.TenantId != tenantId))
                {
                    return new ResponseContext(false, 403, "Admin access required");
                }

                Credential? credential = await _Database!.Credentials.ReadByIdentifierAsync(tenantId, credId).ConfigureAwait(false);
                if (credential == null)
                {
                    return new ResponseContext(false, 404, "Credential not found");
                }

                string body = await GetRequestBody(ctx);
                if (String.IsNullOrEmpty(body))
                {
                    return new ResponseContext(false, 400, "Request body is required");
                }

                UpdateLabelsRequest? request = JsonSerializer.Deserialize<UpdateLabelsRequest>(body, _JsonOptions);
                if (request == null)
                {
                    return new ResponseContext(false, 400, "Invalid request");
                }

                await _Database.Labels.ReplaceCredentialLabelsAsync(tenantId, credId, request.Labels).ConfigureAwait(false);

                return new ResponseContext
                {
                    Success = true,
                    StatusCode = 200,
                    Data = new { Message = "Credential labels updated successfully", CredentialId = credId, Labels = request.Labels }
                };
            });
        }

        /// <summary>
        /// Update credential tags route.
        /// </summary>
        private async Task PutCredentialTagsRoute(HttpContextBase ctx)
        {
            await WrappedRequestHandler(ctx, RequestTypeEnum.IndexManagement, async (reqCtx) =>
            {
                string? tenantId = ctx.Request.Url.Parameters["id"];
                string? credId = ctx.Request.Url.Parameters["credId"];

                if (String.IsNullOrEmpty(tenantId) || String.IsNullOrEmpty(credId))
                {
                    return new ResponseContext(false, 400, "Tenant ID and Credential ID are required");
                }

                AuthContext? auth = await _Auth!.AuthenticateBearerAsync(GetAuthToken(ctx) ?? "").ConfigureAwait(false);
                if (auth == null || !auth.HasAdminAccess || (!auth.IsGlobalAdmin && auth.TenantId != tenantId))
                {
                    return new ResponseContext(false, 403, "Admin access required");
                }

                Credential? credential = await _Database!.Credentials.ReadByIdentifierAsync(tenantId, credId).ConfigureAwait(false);
                if (credential == null)
                {
                    return new ResponseContext(false, 404, "Credential not found");
                }

                string body = await GetRequestBody(ctx);
                if (String.IsNullOrEmpty(body))
                {
                    return new ResponseContext(false, 400, "Request body is required");
                }

                UpdateTagsRequest? request = JsonSerializer.Deserialize<UpdateTagsRequest>(body, _JsonOptions);
                if (request == null)
                {
                    return new ResponseContext(false, 400, "Invalid request");
                }

                await _Database.Tags.ReplaceCredentialTagsAsync(tenantId, credId, request.Tags).ConfigureAwait(false);

                return new ResponseContext
                {
                    Success = true,
                    StatusCode = 200,
                    Data = new { Message = "Credential tags updated successfully", CredentialId = credId, Tags = request.Tags }
                };
            });
        }

        /// <summary>
        /// Wrapped request handler.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <param name="requestType">Request type.</param>
        /// <param name="handler">Handler function.</param>
        /// <returns>Task.</returns>
        private async Task WrappedRequestHandler(HttpContextBase ctx, RequestTypeEnum requestType, Func<RequestContext, Task<ResponseContext>> handler)
        {
            DateTime startTime = DateTime.UtcNow;
            RequestContext requestContext = BuildRequestContext(ctx, requestType);
            ResponseContext responseContext;

            try
            {
                responseContext = await handler(requestContext);
            }
            catch (Exception e)
            {
                _Logging?.Error(_Header + "exception in " + requestType + ": " + e.Message);
                responseContext = new ResponseContext(false, 500, e.Message);
            }

            responseContext.ProcessingTimeMs = DateTime.UtcNow.Subtract(startTime).TotalMilliseconds;
            await SendResponse(ctx, responseContext);
        }

        /// <summary>
        /// Build request context.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <param name="requestType">Request type.</param>
        /// <returns>Request context.</returns>
        private RequestContext BuildRequestContext(HttpContextBase ctx, RequestTypeEnum requestType)
        {
            RequestContext requestContext = new RequestContext
            {
                RequestType = requestType,
                Method = ctx.Request.Method.ToString(),
                Url = ctx.Request.Url.Full,
                IpAddress = ctx.Request.Source.IpAddress,
                AuthToken = GetAuthToken(ctx)
            };

            return requestContext;
        }


        /// <summary>
        /// Get authentication token from request.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Authentication token.</returns>
        private string? GetAuthToken(HttpContextBase ctx)
        {
            string? authHeader = ctx.Request.Headers["Authorization"];
            if (!string.IsNullOrEmpty(authHeader))
            {
                if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    return authHeader.Substring(7);
                }
            }

            return null;
        }

        /// <summary>
        /// Get request body.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Request body as string.</returns>
        private async Task<string> GetRequestBody(HttpContextBase ctx)
        {
            if (ctx.Request.Data != null && ctx.Request.ContentLength > 0)
            {
                using (StreamReader reader = new StreamReader(ctx.Request.Data, Encoding.UTF8))
                {
                    return await reader.ReadToEndAsync();
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Send response.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <param name="response">Response context.</param>
        /// <returns>Task.</returns>
        private async Task SendResponse(HttpContextBase ctx, ResponseContext response)
        {
            ctx.Response.StatusCode = response.StatusCode;
            ctx.Response.ContentType = "application/json";

            foreach (var header in response.Headers)
            {
                ctx.Response.Headers.Add(header.Key, header.Value);
            }

            string json;
            try
            {
                json = JsonSerializer.Serialize(response, _JsonOptions);
            }
            catch (Exception serializationEx)
            {
                _Logging?.Error(_Header + "serialization error: " + serializationEx.Message);
                // Create a safe fallback response that doesn't contain problematic data
                ResponseContext fallbackResponse = new ResponseContext(false, 500, "Serialization error: " + serializationEx.Message);
                json = JsonSerializer.Serialize(fallbackResponse, _JsonOptions);
            }

            await ctx.Response.Send(json);
        }

        #region OpenAPI-Schema-Helpers

        /// <summary>
        /// Create base response schema.
        /// </summary>
        private OpenApiSchemaMetadata CreateResponseSchema()
        {
            return new OpenApiSchemaMetadata
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchemaMetadata>
                {
                    ["Guid"] = OpenApiSchemaMetadata.String(),
                    ["Success"] = OpenApiSchemaMetadata.Boolean(),
                    ["TimestampUtc"] = OpenApiSchemaMetadata.String("date-time"),
                    ["StatusCode"] = OpenApiSchemaMetadata.Integer(),
                    ["ErrorMessage"] = new OpenApiSchemaMetadata { Type = "string", Nullable = true },
                    ["Data"] = new OpenApiSchemaMetadata { Type = "object" },
                    ["ProcessingTimeMs"] = OpenApiSchemaMetadata.Number("double")
                }
            };
        }

        /// <summary>
        /// Create error response schema.
        /// </summary>
        private OpenApiSchemaMetadata CreateErrorSchema()
        {
            return new OpenApiSchemaMetadata
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchemaMetadata>
                {
                    ["Guid"] = OpenApiSchemaMetadata.String(),
                    ["Success"] = OpenApiSchemaMetadata.Boolean(),
                    ["TimestampUtc"] = OpenApiSchemaMetadata.String("date-time"),
                    ["StatusCode"] = OpenApiSchemaMetadata.Integer(),
                    ["ErrorMessage"] = OpenApiSchemaMetadata.String(),
                    ["ProcessingTimeMs"] = OpenApiSchemaMetadata.Number("double")
                }
            };
        }

        /// <summary>
        /// Create login request schema.
        /// </summary>
        private OpenApiSchemaMetadata CreateLoginRequestSchema()
        {
            return new OpenApiSchemaMetadata
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchemaMetadata>
                {
                    ["Username"] = new OpenApiSchemaMetadata { Type = "string", Description = "Username for authentication" },
                    ["Password"] = new OpenApiSchemaMetadata { Type = "string", Description = "Password for authentication" }
                },
                Required = new List<string> { "Username", "Password" }
            };
        }

        /// <summary>
        /// Create login response schema.
        /// </summary>
        private OpenApiSchemaMetadata CreateLoginResponseSchema()
        {
            OpenApiSchemaMetadata response = CreateResponseSchema();
            response.Properties!["Data"] = new OpenApiSchemaMetadata
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchemaMetadata>
                {
                    ["Token"] = new OpenApiSchemaMetadata { Type = "string", Description = "Bearer token for authentication" },
                    ["Username"] = new OpenApiSchemaMetadata { Type = "string", Description = "Authenticated username" }
                }
            };
            return response;
        }

        /// <summary>
        /// Create validate response schema.
        /// </summary>
        private OpenApiSchemaMetadata CreateValidateResponseSchema()
        {
            OpenApiSchemaMetadata response = CreateResponseSchema();
            response.Properties!["Data"] = new OpenApiSchemaMetadata
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchemaMetadata>
                {
                    ["Valid"] = new OpenApiSchemaMetadata { Type = "boolean", Description = "Whether the token is valid" }
                }
            };
            return response;
        }

        /// <summary>
        /// Create message response schema.
        /// </summary>
        private OpenApiSchemaMetadata CreateMessageSchema()
        {
            OpenApiSchemaMetadata response = CreateResponseSchema();
            response.Properties!["Data"] = new OpenApiSchemaMetadata
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchemaMetadata>
                {
                    ["Message"] = OpenApiSchemaMetadata.String()
                }
            };
            return response;
        }

        /// <summary>
        /// Create indices list schema.
        /// </summary>
        private OpenApiSchemaMetadata CreateIndicesListSchema()
        {
            OpenApiSchemaMetadata response = CreateResponseSchema();
            response.Properties!["Data"] = new OpenApiSchemaMetadata
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchemaMetadata>
                {
                    ["Indices"] = OpenApiSchemaMetadata.CreateArray(CreateIndexSummarySchema()),
                    ["Count"] = OpenApiSchemaMetadata.Integer()
                }
            };
            return response;
        }

        /// <summary>
        /// Create index summary schema.
        /// </summary>
        private OpenApiSchemaMetadata CreateIndexSummarySchema()
        {
            return new OpenApiSchemaMetadata
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchemaMetadata>
                {
                    ["Identifier"] = new OpenApiSchemaMetadata { Type = "string", Description = "Unique identifier of the index" },
                    ["TenantId"] = new OpenApiSchemaMetadata { Type = "string", Description = "Tenant the index belongs to" },
                    ["Name"] = new OpenApiSchemaMetadata { Type = "string", Description = "Display name of the index" },
                    ["Description"] = new OpenApiSchemaMetadata { Type = "string", Nullable = true },
                    ["Enabled"] = OpenApiSchemaMetadata.Boolean(),
                    ["InMemory"] = OpenApiSchemaMetadata.Boolean(),
                    ["CreatedUtc"] = OpenApiSchemaMetadata.String("date-time"),
                    ["Labels"] = OpenApiSchemaMetadata.CreateArray(OpenApiSchemaMetadata.String()),
                    ["Tags"] = new OpenApiSchemaMetadata { Type = "object", Description = "Key-value pairs" }
                }
            };
        }

        /// <summary>
        /// Create index request schema.
        /// </summary>
        private OpenApiSchemaMetadata CreateIndexRequestSchema()
        {
            return new OpenApiSchemaMetadata
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchemaMetadata>
                {
                    ["Name"] = new OpenApiSchemaMetadata { Type = "string", Description = "Display name of the index" },
                    ["Description"] = new OpenApiSchemaMetadata { Type = "string", Description = "Optional description" },
                    ["InMemory"] = new OpenApiSchemaMetadata { Type = "boolean", Description = "Whether to store in memory only (SQLite)" },
                    ["EnableLemmatizer"] = new OpenApiSchemaMetadata { Type = "boolean", Description = "Enable lemmatization" },
                    ["EnableStopWordRemover"] = new OpenApiSchemaMetadata { Type = "boolean", Description = "Enable stop word removal" },
                    ["MinTokenLength"] = new OpenApiSchemaMetadata { Type = "integer", Description = "Minimum token length" },
                    ["MaxTokenLength"] = new OpenApiSchemaMetadata { Type = "integer", Description = "Maximum token length" },
                    ["Labels"] = OpenApiSchemaMetadata.CreateArray(OpenApiSchemaMetadata.String()),
                    ["Tags"] = new OpenApiSchemaMetadata { Type = "object", Description = "Key-value pairs" }
                },
                Required = new List<string> { "Name" }
            };
        }

        /// <summary>
        /// Create index created response schema.
        /// </summary>
        private OpenApiSchemaMetadata CreateIndexCreatedSchema()
        {
            OpenApiSchemaMetadata response = CreateResponseSchema();
            response.Properties!["Data"] = new OpenApiSchemaMetadata
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchemaMetadata>
                {
                    ["Message"] = OpenApiSchemaMetadata.String(),
                    ["Index"] = CreateIndexSummarySchema()
                }
            };
            return response;
        }

        /// <summary>
        /// Create index details schema.
        /// </summary>
        private OpenApiSchemaMetadata CreateIndexDetailsSchema()
        {
            OpenApiSchemaMetadata response = CreateResponseSchema();
            response.Properties!["Data"] = new OpenApiSchemaMetadata
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchemaMetadata>
                {
                    ["Configuration"] = CreateIndexSummarySchema(),
                    ["Statistics"] = new OpenApiSchemaMetadata
                    {
                        Type = "object",
                        Properties = new Dictionary<string, OpenApiSchemaMetadata>
                        {
                            ["DocumentCount"] = OpenApiSchemaMetadata.Integer(),
                            ["TermCount"] = OpenApiSchemaMetadata.Integer(),
                            ["TotalTermOccurrences"] = OpenApiSchemaMetadata.Integer("int64")
                        }
                    }
                }
            };
            return response;
        }

        /// <summary>
        /// Create index update response schema.
        /// </summary>
        private OpenApiSchemaMetadata CreateIndexUpdateSchema()
        {
            OpenApiSchemaMetadata response = CreateResponseSchema();
            response.Properties!["Data"] = new OpenApiSchemaMetadata
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchemaMetadata>
                {
                    ["Message"] = OpenApiSchemaMetadata.String(),
                    ["Index"] = new OpenApiSchemaMetadata
                    {
                        Type = "object",
                        Properties = new Dictionary<string, OpenApiSchemaMetadata>
                        {
                            ["Id"] = OpenApiSchemaMetadata.String(),
                            ["Name"] = OpenApiSchemaMetadata.String(),
                            ["Labels"] = OpenApiSchemaMetadata.CreateArray(OpenApiSchemaMetadata.String()),
                            ["Tags"] = new OpenApiSchemaMetadata { Type = "object" }
                        }
                    }
                }
            };
            return response;
        }

        /// <summary>
        /// Create update labels request schema.
        /// </summary>
        private OpenApiSchemaMetadata CreateUpdateLabelsRequestSchema()
        {
            return new OpenApiSchemaMetadata
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchemaMetadata>
                {
                    ["Labels"] = new OpenApiSchemaMetadata
                    {
                        Type = "array",
                        Items = OpenApiSchemaMetadata.String(),
                        Description = "List of labels to replace existing labels"
                    }
                }
            };
        }

        /// <summary>
        /// Create update tags request schema.
        /// </summary>
        private OpenApiSchemaMetadata CreateUpdateTagsRequestSchema()
        {
            return new OpenApiSchemaMetadata
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchemaMetadata>
                {
                    ["Tags"] = new OpenApiSchemaMetadata
                    {
                        Type = "object",
                        Description = "Key-value pairs to replace existing tags"
                    }
                }
            };
        }

        /// <summary>
        /// Create update custom metadata request schema.
        /// </summary>
        private OpenApiSchemaMetadata CreateUpdateCustomMetadataRequestSchema()
        {
            return new OpenApiSchemaMetadata
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchemaMetadata>
                {
                    ["customMetadata"] = new OpenApiSchemaMetadata
                    {
                        Description = "Custom metadata value (can be any JSON-serializable value: object, array, string, number, boolean, or null)"
                    }
                }
            };
        }

        /// <summary>
        /// Create documents list schema.
        /// </summary>
        private OpenApiSchemaMetadata CreateDocumentsListSchema()
        {
            OpenApiSchemaMetadata response = CreateResponseSchema();
            response.Properties!["Data"] = new OpenApiSchemaMetadata
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchemaMetadata>
                {
                    ["Documents"] = OpenApiSchemaMetadata.CreateArray(CreateDocumentSchema()),
                    ["Count"] = OpenApiSchemaMetadata.Integer()
                }
            };
            return response;
        }

        /// <summary>
        /// Create document schema.
        /// </summary>
        private OpenApiSchemaMetadata CreateDocumentSchema()
        {
            return new OpenApiSchemaMetadata
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchemaMetadata>
                {
                    ["Id"] = new OpenApiSchemaMetadata { Type = "string", Description = "Unique identifier of the document" },
                    ["Name"] = new OpenApiSchemaMetadata { Type = "string", Description = "Name of the document" },
                    ["ContentLength"] = OpenApiSchemaMetadata.Integer(),
                    ["TermCount"] = OpenApiSchemaMetadata.Integer(),
                    ["CreatedUtc"] = OpenApiSchemaMetadata.String("date-time"),
                    ["Labels"] = OpenApiSchemaMetadata.CreateArray(OpenApiSchemaMetadata.String()),
                    ["Tags"] = new OpenApiSchemaMetadata { Type = "object" }
                }
            };
        }

        /// <summary>
        /// Create add document request schema.
        /// </summary>
        private OpenApiSchemaMetadata CreateAddDocumentRequestSchema()
        {
            return new OpenApiSchemaMetadata
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchemaMetadata>
                {
                    ["Id"] = new OpenApiSchemaMetadata { Type = "string", Description = "Optional document ID. Auto-generated if not provided." },
                    ["Content"] = new OpenApiSchemaMetadata { Type = "string", Description = "Document content to index" },
                    ["Labels"] = new OpenApiSchemaMetadata { Type = "array", Items = OpenApiSchemaMetadata.String(), Description = "Optional labels" },
                    ["Tags"] = new OpenApiSchemaMetadata { Type = "object", Description = "Optional key-value tags" }
                },
                Required = new List<string> { "Content" }
            };
        }

        /// <summary>
        /// Create document created response schema.
        /// </summary>
        private OpenApiSchemaMetadata CreateDocumentCreatedSchema()
        {
            OpenApiSchemaMetadata response = CreateResponseSchema();
            response.Properties!["Data"] = new OpenApiSchemaMetadata
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchemaMetadata>
                {
                    ["DocumentId"] = new OpenApiSchemaMetadata { Type = "string", Description = "ID of the created document" },
                    ["Message"] = OpenApiSchemaMetadata.String()
                }
            };
            return response;
        }

        /// <summary>
        /// Create document details schema.
        /// </summary>
        private OpenApiSchemaMetadata CreateDocumentDetailsSchema()
        {
            OpenApiSchemaMetadata response = CreateResponseSchema();
            response.Properties!["Data"] = CreateDocumentSchema();
            return response;
        }

        /// <summary>
        /// Create document update response schema.
        /// </summary>
        private OpenApiSchemaMetadata CreateDocumentUpdateSchema()
        {
            OpenApiSchemaMetadata response = CreateResponseSchema();
            response.Properties!["Data"] = new OpenApiSchemaMetadata
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchemaMetadata>
                {
                    ["Message"] = OpenApiSchemaMetadata.String(),
                    ["Document"] = CreateDocumentSchema()
                }
            };
            return response;
        }

        /// <summary>
        /// Create search request schema.
        /// </summary>
        private OpenApiSchemaMetadata CreateSearchRequestSchema()
        {
            return new OpenApiSchemaMetadata
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchemaMetadata>
                {
                    ["Query"] = new OpenApiSchemaMetadata { Type = "string", Description = "Search query string" },
                    ["MaxResults"] = new OpenApiSchemaMetadata { Type = "integer", Description = "Maximum number of results (default: 100)", Default = 100 },
                    ["UseAndLogic"] = new OpenApiSchemaMetadata { Type = "boolean", Description = "Use AND logic instead of OR (default: false)", Default = false },
                    ["Labels"] = new OpenApiSchemaMetadata { Type = "array", Items = OpenApiSchemaMetadata.String(), Description = "Filter by labels (AND logic)" },
                    ["Tags"] = new OpenApiSchemaMetadata { Type = "object", Description = "Filter by tags (AND logic, exact match)" }
                },
                Required = new List<string> { "Query" }
            };
        }

        /// <summary>
        /// Create search results schema.
        /// </summary>
        private OpenApiSchemaMetadata CreateSearchResultsSchema()
        {
            OpenApiSchemaMetadata response = CreateResponseSchema();
            response.Properties!["Data"] = new OpenApiSchemaMetadata
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchemaMetadata>
                {
                    ["Query"] = OpenApiSchemaMetadata.String(),
                    ["Results"] = OpenApiSchemaMetadata.CreateArray(CreateSearchResultItemSchema()),
                    ["TotalCount"] = OpenApiSchemaMetadata.Integer(),
                    ["SearchTime"] = new OpenApiSchemaMetadata { Type = "number", Format = "double", Description = "Search time in milliseconds" }
                }
            };
            return response;
        }

        /// <summary>
        /// Create search result item schema.
        /// </summary>
        private OpenApiSchemaMetadata CreateSearchResultItemSchema()
        {
            return new OpenApiSchemaMetadata
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchemaMetadata>
                {
                    ["DocumentId"] = OpenApiSchemaMetadata.String(),
                    ["DocumentName"] = OpenApiSchemaMetadata.String(),
                    ["Score"] = OpenApiSchemaMetadata.Number("double"),
                    ["MatchedTerms"] = OpenApiSchemaMetadata.CreateArray(OpenApiSchemaMetadata.String()),
                    ["Labels"] = OpenApiSchemaMetadata.CreateArray(OpenApiSchemaMetadata.String()),
                    ["Tags"] = new OpenApiSchemaMetadata { Type = "object" }
                }
            };
        }

        #endregion

        #endregion
    }
}