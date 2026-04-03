namespace Verbex.Server.API.REST
{
    using SyslogLogging;
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Net;
    using System.Reflection.PortableExecutable;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Verbex;
    using Verbex.Database;
    using Verbex.Database.Interfaces;
    using Verbex.DTO.Requests;
    using Verbex.DTO.Responses;
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
        private BackupService? _BackupService = null;
        private RequestHistoryService? _RequestHistory = null;
        private readonly ConditionalWeakTable<HttpContextBase, RequestHistoryCaptureContext> _RequestHistoryContexts = new ConditionalWeakTable<HttpContextBase, RequestHistoryCaptureContext>();
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

            // Initialize backup service
            _BackupService = new BackupService(indexManager, settings.DataDirectory, logging);
            _RequestHistory = new RequestHistoryService(settings.RequestHistory, database, logging);
            _RequestHistory.InitializeAsync().GetAwaiter().GetResult();

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
            _RequestHistory?.Dispose();
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
                settings.Tags.Add(new OpenApiTag { Name = "Tenants", Description = "Tenant administration operations" });
                settings.Tags.Add(new OpenApiTag { Name = "Users", Description = "User administration operations" });
                settings.Tags.Add(new OpenApiTag { Name = "Credentials", Description = "API credential administration operations" });
                settings.Tags.Add(new OpenApiTag { Name = "Request History", Description = "Request audit and exploration operations" });

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
                HttpMethod.PUT, "/v1.0/indices/{id}", PutIndexRoute,
                metadata => metadata
                    .WithTag("Indices")
                    .WithDescription("Update an index's core properties (name, description, enabled status).")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "The unique identifier of the index"))
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json(
                        CreateUpdateIndexRequestSchema(),
                        "Index properties to update",
                        required: true))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Index updated successfully", CreateIndexUpdateSchema()))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest(CreateErrorSchema()))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound())
                    .WithResponse(409, OpenApiResponseMetadata.Create("Index with this name already exists")),
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
                HttpMethod.POST, "/v1.0/indices/{id}/cache/rebuild", PostIndexCacheRebuildRoute,
                metadata => metadata
                    .WithTag("Indices")
                    .WithDescription("Rebuild the term ID cache for an index. Use this if the database has been modified externally.")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "The unique identifier of the index"))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Cache rebuild result", CreateCacheRebuildResponseSchema()))
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

            #region Terms-Routes

            _Webserver.Routes.PostAuthentication.Parameter.Add(
                HttpMethod.GET, "/v1.0/indices/{id}/terms/top", GetTopTermsRoute,
                metadata => metadata
                    .WithTag("Terms")
                    .WithDescription("Get the top terms in an index sorted by document frequency (most common first).")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "The unique identifier of the index"))
                    .WithParameter(OpenApiParameterMetadata.Query("limit", "Maximum number of terms to return (default: 10)", required: false))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Top terms with their frequencies", CreateTopTermsResponseSchema()))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()),
                ExceptionRoute);

            #endregion

            #region Backup-Restore-Routes

            _Webserver.Routes.PostAuthentication.Parameter.Add(
                HttpMethod.POST, "/v1.0/indices/{id}/backup", PostIndexBackupRoute,
                metadata => metadata
                    .WithTag("Backup & Restore")
                    .WithDescription("Create a backup of an index. Returns a ZIP archive containing the index database and metadata.")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "The unique identifier of the index to backup"))
                    .WithResponse(200, OpenApiResponseMetadata.Create("Backup archive (application/zip)"))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound())
                    .WithResponse(423, OpenApiResponseMetadata.Create("Index is locked")),
                ExceptionRoute);

            _Webserver.Routes.PostAuthentication.Static.Add(
                HttpMethod.POST, "/v1.0/indices/restore", PostIndicesRestoreRoute,
                metadata => metadata
                    .WithTag("Backup & Restore")
                    .WithDescription("Restore a backup to create a new index. Upload a backup archive (.vbx file) via multipart form data.")
                    .WithResponse(201, OpenApiResponseMetadata.Created(CreateRestoreResultSchema()))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest(CreateErrorSchema()))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(409, OpenApiResponseMetadata.Create("Index with specified ID already exists"))
                    .WithResponse(415, OpenApiResponseMetadata.Create("Invalid file format"))
                    .WithResponse(422, OpenApiResponseMetadata.Create("Backup validation failed")),
                ExceptionRoute);

            _Webserver.Routes.PostAuthentication.Parameter.Add(
                HttpMethod.POST, "/v1.0/indices/{id}/restore", PostIndexRestoreRoute,
                metadata => metadata
                    .WithTag("Backup & Restore")
                    .WithDescription("Restore a backup by replacing an existing index. Upload a backup archive (.vbx file) via multipart form data.")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "The unique identifier of the index to replace"))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Restore result", CreateRestoreResultSchema()))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest(CreateErrorSchema()))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound())
                    .WithResponse(415, OpenApiResponseMetadata.Create("Invalid file format"))
                    .WithResponse(422, OpenApiResponseMetadata.Create("Backup validation failed"))
                    .WithResponse(423, OpenApiResponseMetadata.Create("Index is locked")),
                ExceptionRoute);

            #endregion

            #region Document-Routes

            _Webserver.Routes.PostAuthentication.Parameter.Add(
                HttpMethod.GET, "/v1.0/indices/{id}/documents", GetIndexDocumentsRoute,
                metadata => metadata
                    .WithTag("Documents")
                    .WithDescription("List documents in an index with pagination. Returns TotalCount for proper pagination support. Use limit=0 to return all documents.")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "The unique identifier of the index"))
                    .WithParameter(OpenApiParameterMetadata.Query("limit", "Maximum number of documents to return (default: 100, use 0 for no limit)", required: false))
                    .WithParameter(OpenApiParameterMetadata.Query("offset", "Number of documents to skip (default: 0)", required: false))
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
                HttpMethod.DELETE, "/v1.0/indices/{id}/documents", BatchDeleteIndexDocumentsRoute,
                metadata => metadata
                    .WithTag("Documents")
                    .WithDescription("Delete multiple documents from an index by IDs. Returns which documents were deleted and which were not found.")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "The unique identifier of the index"))
                    .WithParameter(OpenApiParameterMetadata.Query("ids", "Comma-separated list of document IDs to delete", required: true))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Batch delete result", CreateBatchDeleteResultSchema()))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest(CreateErrorSchema()))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()),
                ExceptionRoute);

            _Webserver.Routes.PostAuthentication.Parameter.Add(
                HttpMethod.POST, "/v1.0/indices/{id}/documents/batch", BatchAddIndexDocumentsRoute,
                metadata => metadata
                    .WithTag("Documents")
                    .WithDescription("Add multiple documents to an index in a single request. Returns which documents were added and which failed.")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "The unique identifier of the index"))
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json(
                        CreateBatchAddDocumentsRequestSchema(),
                        "Documents to add",
                        required: true))
                    .WithResponse(201, OpenApiResponseMetadata.Created(CreateBatchAddDocumentsResponseSchema()))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest(CreateErrorSchema()))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()),
                ExceptionRoute);

            _Webserver.Routes.PostAuthentication.Parameter.Add(
                HttpMethod.POST, "/v1.0/indices/{id}/documents/exists", BatchCheckDocumentsExistRoute,
                metadata => metadata
                    .WithTag("Documents")
                    .WithDescription("Check if multiple documents exist in an index. Returns which IDs exist and which do not.")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "The unique identifier of the index"))
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json(
                        CreateBatchCheckExistenceRequestSchema(),
                        "Document IDs to check",
                        required: true))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Existence check result", CreateBatchCheckExistenceResponseSchema()))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest(CreateErrorSchema()))
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

            #region Request-History-Routes

            _Webserver.Routes.PostAuthentication.Static.Add(
                HttpMethod.GET, "/v1.0/requesthistory", GetRequestHistoryRoute,
                metadata => metadata
                    .WithTag("Request History")
                    .WithDescription("Search request history entries visible to the authenticated principal.")
                    .WithResponse(200, OpenApiResponseMetadata.Json("Request history search results", CreateRequestHistoryListSchema()))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
                ExceptionRoute);

            _Webserver.Routes.PostAuthentication.Static.Add(
                HttpMethod.GET, "/v1.0/requesthistory/summary", GetRequestHistorySummaryRoute,
                metadata => metadata
                    .WithTag("Request History")
                    .WithDescription("Get aggregated request history buckets for charting and dashboard summaries.")
                    .WithResponse(200, OpenApiResponseMetadata.Json("Request history summary", CreateRequestHistorySummarySchema()))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
                ExceptionRoute);

            _Webserver.Routes.PostAuthentication.Static.Add(
                HttpMethod.DELETE, "/v1.0/requesthistory/bulk", DeleteRequestHistoryBulkRoute,
                metadata => metadata
                    .WithTag("Request History")
                    .WithDescription("Delete all request history entries matching the supplied filters.")
                    .WithResponse(200, OpenApiResponseMetadata.Json("Bulk delete result", CreateRequestHistoryBulkDeleteSchema()))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
                ExceptionRoute);

            _Webserver.Routes.PostAuthentication.Parameter.Add(
                HttpMethod.GET, "/v1.0/requesthistory/{id}", GetRequestHistoryEntryRoute,
                metadata => metadata
                    .WithTag("Request History")
                    .WithDescription("Get a single request history entry by ID.")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "The request history entry identifier"))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Request history entry", CreateRequestHistoryEntrySchema()))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()),
                ExceptionRoute);

            _Webserver.Routes.PostAuthentication.Parameter.Add(
                HttpMethod.GET, "/v1.0/requesthistory/{id}/detail", GetRequestHistoryDetailRoute,
                metadata => metadata
                    .WithTag("Request History")
                    .WithDescription("Get the stored request and response detail for a request history entry.")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "The request history entry identifier"))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Request history detail", CreateRequestHistoryDetailSchema()))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()),
                ExceptionRoute);

            _Webserver.Routes.PostAuthentication.Parameter.Add(
                HttpMethod.DELETE, "/v1.0/requesthistory/{id}", DeleteRequestHistoryEntryRoute,
                metadata => metadata
                    .WithTag("Request History")
                    .WithDescription("Delete a request history entry and its stored detail payload.")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "The request history entry identifier"))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Delete result", CreateResponseSchema()))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()),
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

            await PersistRequestHistoryAsync(ctx).ConfigureAwait(false);
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

                // Parse enumeration query parameters
                EnumerationQuery query = EnumerationQuery.Parse(
                    ctx.Request.Query?.Elements?["maxResults"],
                    ctx.Request.Query?.Elements?["skip"],
                    ctx.Request.Query?.Elements?["continuationToken"],
                    ctx.Request.Query?.Elements?["ordering"]
                );

                // Handle continuation token if provided
                if (!String.IsNullOrEmpty(query.ContinuationToken) &&
                    EnumerationResult<IndexMetadataResponse>.TryParseContinuationToken(query.ContinuationToken, out int tokenSkip))
                {
                    query.Skip = tokenSkip;
                }

                List<IndexMetadata> allIndices = _IndexManager!.GetAllMetadata(tenantId);
                long totalCount = allIndices.Count;

                // Apply ordering
                IEnumerable<IndexMetadata> ordered = query.Ordering == EnumerationOrderEnum.CreatedAscending
                    ? allIndices.OrderBy(x => x.CreatedUtc)
                    : allIndices.OrderByDescending(x => x.CreatedUtc);

                // Apply pagination
                List<IndexMetadataResponse> pagedIndices = ordered
                    .Skip(query.Skip)
                    .Take(query.MaxResults)
                    .Select(m => new IndexMetadataResponse
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
                    })
                    .ToList();

                EnumerationResult<IndexMetadataResponse> result = new EnumerationResult<IndexMetadataResponse>(query, pagedIndices, totalCount);

                return new ResponseContext
                {
                    Success = true,
                    StatusCode = 200,
                    Data = result
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

                // Check if custom identifier already exists
                if (!String.IsNullOrEmpty(createRequest.Identifier) && _IndexManager!.IndexExists(createRequest.Identifier))
                {
                    return new ResponseContext(false, 409, "Index with identifier '" + createRequest.Identifier + "' already exists");
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
                                CustomMetadata = created.CustomMetadata,
                                CacheConfiguration = created.CacheConfiguration
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

                object? statistics = await _IndexManager!.GetIndexStatisticsAsync(indexId);
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
        /// Update index core properties route.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        private async Task PutIndexRoute(HttpContextBase ctx)
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

                UpdateIndexRequest? request = JsonSerializer.Deserialize<UpdateIndexRequest>(body, _JsonOptions);
                if (request == null)
                {
                    return new ResponseContext(false, 400, "Invalid JSON in request body");
                }

                if (!request.Validate(out string errorMessage))
                {
                    return new ResponseContext(false, 400, errorMessage);
                }

                try
                {
                    IndexMetadata? updated = await _IndexManager.UpdateIndexAsync(
                        tenantId,
                        indexId,
                        request.Name,
                        request.Description,
                        request.Enabled).ConfigureAwait(false);

                    if (updated != null)
                    {
                        return new ResponseContext
                        {
                            Success = true,
                            StatusCode = 200,
                            Data = new
                            {
                                Message = "Index updated successfully",
                                Index = new
                                {
                                    Identifier = updated.Identifier,
                                    Name = updated.Name,
                                    Description = updated.Description,
                                    Enabled = updated.Enabled,
                                    Labels = updated.Labels,
                                    Tags = updated.Tags
                                }
                            }
                        };
                    }
                    else
                    {
                        return new ResponseContext(false, 500, "Failed to update index");
                    }
                }
                catch (InvalidOperationException ex)
                {
                    return new ResponseContext(false, 409, ex.Message);
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

                InvertedIndex? index = _IndexManager!.GetIndex(indexId);
                if (index == null)
                {
                    return new ResponseContext(false, 404, "Index not found");
                }

                try
                {
                    // Check for batch retrieval via ?ids= query parameter
                    string? idsParam = ctx.Request.Query?.Elements?["ids"];

                    if (!String.IsNullOrEmpty(idsParam))
                    {
                        // Batch retrieval mode - preserves existing behavior
                        List<string> requestedIds = idsParam
                            .Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(id => id.Trim())
                            .Distinct()
                            .ToList();

                        if (requestedIds.Count == 0)
                        {
                            return new ResponseContext(false, 400, "No valid document IDs provided");
                        }

                        List<DocumentMetadata> documents = await index.GetDocumentsWithMetadataAsync(requestedIds).ConfigureAwait(false);
                        HashSet<string> foundIds = documents.Select(d => d.DocumentId).ToHashSet();
                        List<string> notFoundIds = requestedIds.Where(id => !foundIds.Contains(id)).ToList();

                        return new ResponseContext
                        {
                            Success = true,
                            StatusCode = 200,
                            Data = new
                            {
                                Documents = documents,
                                NotFound = notFoundIds,
                                Count = documents.Count,
                                RequestedCount = requestedIds.Count
                            }
                        };
                    }
                    else
                    {
                        // Parse tag.* query parameters for filtering
                        Dictionary<string, string>? tagParams = null;
                        NameValueCollection? queryElements = ctx.Request.Query?.Elements;
                        if (queryElements != null)
                        {
                            foreach (string? key in queryElements.AllKeys)
                            {
                                if (key != null && key.StartsWith("tag.", StringComparison.OrdinalIgnoreCase) && key.Length > 4)
                                {
                                    string tagKey = key.Substring(4);
                                    string? tagValue = queryElements[key];
                                    if (tagValue != null)
                                    {
                                        tagParams ??= new Dictionary<string, string>();
                                        tagParams[tagKey] = tagValue;
                                    }
                                }
                            }
                        }

                        // Paginated list behavior using EnumerationQuery
                        EnumerationQuery query = EnumerationQuery.Parse(
                            ctx.Request.Query?.Elements?["maxResults"],
                            ctx.Request.Query?.Elements?["skip"],
                            ctx.Request.Query?.Elements?["continuationToken"],
                            ctx.Request.Query?.Elements?["ordering"],
                            ctx.Request.Query?.Elements?["labels"],
                            tagParams
                        );

                        // Handle continuation token if provided
                        if (!String.IsNullOrEmpty(query.ContinuationToken) &&
                            EnumerationResult<DocumentMetadata>.TryParseContinuationToken(query.ContinuationToken, out int tokenSkip))
                        {
                            query.Skip = tokenSkip;
                        }

                        bool hasFilters = (query.Labels != null && query.Labels.Count > 0) ||
                                          (query.Tags != null && query.Tags.Count > 0);

                        long totalCount;
                        if (hasFilters)
                        {
                            totalCount = await index.GetDocumentCountAsync(query.Labels, query.Tags).ConfigureAwait(false);
                        }
                        else
                        {
                            totalCount = await index.GetDocumentCountAsync().ConfigureAwait(false);
                        }

                        // Use database-level pagination for efficiency
                        // Database orders by created_utc DESC by default
                        List<DocumentMetadata> pagedDocuments;

                        if (query.Ordering == EnumerationOrderEnum.CreatedAscending)
                        {
                            // For ascending order, calculate offset from the end
                            // e.g., total=100, skip=0, limit=10 ascending = offset 90 descending, then reverse
                            int availableFromEnd = (int)Math.Max(0, totalCount - query.Skip);
                            int actualLimit = Math.Min(query.MaxResults, availableFromEnd);
                            int ascendingOffset = (int)Math.Max(0, totalCount - query.Skip - actualLimit);

                            if (hasFilters)
                            {
                                pagedDocuments = await index.GetDocumentsAsync(
                                    limit: actualLimit,
                                    offset: ascendingOffset,
                                    labels: query.Labels,
                                    tags: query.Tags
                                ).ConfigureAwait(false);
                            }
                            else
                            {
                                pagedDocuments = await index.GetDocumentsAsync(
                                    limit: actualLimit,
                                    offset: ascendingOffset
                                ).ConfigureAwait(false);
                            }

                            // Reverse to get ascending order
                            pagedDocuments.Reverse();
                        }
                        else
                        {
                            // Descending order - use database directly
                            if (hasFilters)
                            {
                                pagedDocuments = await index.GetDocumentsAsync(
                                    limit: query.MaxResults,
                                    offset: query.Skip,
                                    labels: query.Labels,
                                    tags: query.Tags
                                ).ConfigureAwait(false);
                            }
                            else
                            {
                                pagedDocuments = await index.GetDocumentsAsync(
                                    limit: query.MaxResults,
                                    offset: query.Skip
                                ).ConfigureAwait(false);
                            }
                        }

                        EnumerationResult<DocumentMetadata> result = new EnumerationResult<DocumentMetadata>(query, pagedDocuments, totalCount);

                        return new ResponseContext
                        {
                            Success = true,
                            StatusCode = 200,
                            Data = result
                        };
                    }
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

                InvertedIndex? index = _IndexManager!.GetIndex(indexId);
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

                // Check if custom document ID already exists
                if (!String.IsNullOrEmpty(documentRequest.Id))
                {
                    bool docExists = await index.DocumentExistsAsync(documentRequest.Id).ConfigureAwait(false);
                    if (docExists)
                    {
                        return new ResponseContext(false, 409, "Document with ID '" + documentRequest.Id + "' already exists");
                    }
                }

                try
                {
                    // Add the document - use the correct overload based on whether ID is provided
                    AddDocumentResult result;
                    if (!String.IsNullOrEmpty(documentRequest.Id))
                    {
                        // Use provided ID
                        result = await index.AddDocumentWithMetricsAsync(documentRequest.Id, documentRequest.Id, documentRequest.Content).ConfigureAwait(false);
                    }
                    else
                    {
                        // Let the index generate an ID
                        result = await index.AddDocumentWithMetricsAsync(
                            IdGenerator.GenerateDocumentId(),
                            documentRequest.Content).ConfigureAwait(false);
                    }

                    string documentId = result.DocumentId;

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
                        Data = new
                        {
                            DocumentId = documentId,
                            Message = "Document added successfully",
                            Metrics = result.Metrics
                        }
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

                InvertedIndex? index = _IndexManager!.GetIndex(indexId);
                if (index == null)
                {
                    return new ResponseContext(false, 404, "Index not found");
                }

                try
                {
                    // Use GetDocumentWithMetadataAsync for single query with JOINs
                    DocumentMetadata? document = await index.GetDocumentWithMetadataAsync(docId);
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

                InvertedIndex? index = _IndexManager!.GetIndex(indexId);
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

                InvertedIndex? index = _IndexManager!.GetIndex(indexId);
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
        /// Batch delete documents from index route.
        /// Deletes multiple documents specified via the ids query parameter.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        private async Task BatchDeleteIndexDocumentsRoute(HttpContextBase ctx)
        {
            await WrappedRequestHandler(ctx, RequestTypeEnum.Document, async (reqCtx) =>
            {
                string? indexId = ctx.Request.Url.Parameters["id"];
                if (String.IsNullOrEmpty(indexId))
                {
                    return new ResponseContext(false, 400, "Index ID is required");
                }

                InvertedIndex? index = _IndexManager!.GetIndex(indexId);
                if (index == null)
                {
                    return new ResponseContext(false, 404, "Index not found");
                }

                string? idsParam = ctx.Request.Query?.Elements?["ids"];
                if (String.IsNullOrEmpty(idsParam))
                {
                    return new ResponseContext(false, 400, "The 'ids' query parameter is required");
                }

                List<string> requestedIds = idsParam
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(id => id.Trim())
                    .Distinct()
                    .ToList();

                if (requestedIds.Count == 0)
                {
                    return new ResponseContext(false, 400, "No valid document IDs provided");
                }

                try
                {
                    // Use optimized batch deletion method
                    BatchDeleteResponse deleteResult = await index.RemoveDocumentsBatchAsync(requestedIds).ConfigureAwait(false);

                    return new ResponseContext
                    {
                        Success = true,
                        StatusCode = 200,
                        Data = deleteResult
                    };
                }
                catch (Exception ex)
                {
                    return new ResponseContext(false, 500, $"Error deleting documents: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Batch add documents to index route.
        /// Adds multiple documents in a single request.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        private async Task BatchAddIndexDocumentsRoute(HttpContextBase ctx)
        {
            await WrappedRequestHandler(ctx, RequestTypeEnum.Document, async (reqCtx) =>
            {
                string? indexId = ctx.Request.Url.Parameters["id"];
                if (String.IsNullOrEmpty(indexId))
                {
                    return new ResponseContext(false, 400, "Index ID is required");
                }

                InvertedIndex? index = _IndexManager!.GetIndex(indexId);
                if (index == null)
                {
                    return new ResponseContext(false, 404, "Index not found");
                }

                string body = await GetRequestBody(ctx);
                if (String.IsNullOrEmpty(body))
                {
                    return new ResponseContext(false, 400, "Request body is required");
                }

                DTO.Requests.BatchAddDocumentsRequest? request = JsonSerializer.Deserialize<DTO.Requests.BatchAddDocumentsRequest>(body, _JsonOptions);
                if (request == null)
                {
                    return new ResponseContext(false, 400, "Invalid JSON in request body");
                }

                if (!request.Validate(out string errorMessage))
                {
                    return new ResponseContext(false, 400, errorMessage);
                }

                try
                {
                    // Documents are already in the correct format (BatchAddDocumentItem)
                    BatchAddDocumentsResponse addResult = await index.AddDocumentsBatchAsync(request.Documents).ConfigureAwait(false);

                    return new ResponseContext
                    {
                        Success = true,
                        StatusCode = 201,
                        Data = addResult
                    };
                }
                catch (Exception ex)
                {
                    return new ResponseContext(false, 500, $"Error adding documents: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Batch check documents exist route.
        /// Checks if multiple documents exist in an index.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        private async Task BatchCheckDocumentsExistRoute(HttpContextBase ctx)
        {
            await WrappedRequestHandler(ctx, RequestTypeEnum.Document, async (reqCtx) =>
            {
                string? indexId = ctx.Request.Url.Parameters["id"];
                if (String.IsNullOrEmpty(indexId))
                {
                    return new ResponseContext(false, 400, "Index ID is required");
                }

                InvertedIndex? index = _IndexManager!.GetIndex(indexId);
                if (index == null)
                {
                    return new ResponseContext(false, 404, "Index not found");
                }

                string body = await GetRequestBody(ctx);
                if (String.IsNullOrEmpty(body))
                {
                    return new ResponseContext(false, 400, "Request body is required");
                }

                DTO.Requests.BatchCheckExistenceRequest? request = JsonSerializer.Deserialize<DTO.Requests.BatchCheckExistenceRequest>(body, _JsonOptions);
                if (request == null)
                {
                    return new ResponseContext(false, 400, "Invalid JSON in request body");
                }

                if (!request.Validate(out string errorMessage))
                {
                    return new ResponseContext(false, 400, errorMessage);
                }

                try
                {
                    BatchCheckExistenceResponse existResult = await index.DocumentsExistBatchAsync(request.Ids).ConfigureAwait(false);

                    return new ResponseContext
                    {
                        Success = true,
                        StatusCode = 200,
                        Data = existResult
                    };
                }
                catch (Exception ex)
                {
                    return new ResponseContext(false, 500, $"Error checking documents: {ex.Message}");
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

                InvertedIndex? index = _IndexManager!.GetIndex(indexId);
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
                            SearchTime = searchResults.SearchTime.TotalMilliseconds,
                            TimingInfo = searchResults.TimingInfo != null ? new {
                                TermLookupMs = searchResults.TimingInfo.TermLookupMs,
                                TermsFound = searchResults.TimingInfo.TermsFound,
                                MainSearchMs = searchResults.TimingInfo.MainSearchMs,
                                MatchesFound = searchResults.TimingInfo.MatchesFound,
                                TermFrequenciesMs = searchResults.TimingInfo.TermFrequenciesMs,
                                TermFrequencyRecords = searchResults.TimingInfo.TermFrequencyRecords,
                                DocumentMetadataMs = searchResults.TimingInfo.DocumentMetadataMs,
                                DocumentsFetched = searchResults.TimingInfo.DocumentsFetched,
                                DocumentCountMs = searchResults.TimingInfo.DocumentCountMs,
                                TotalDocuments = searchResults.TimingInfo.TotalDocuments
                            } : null
                        }
                    };
                }
                catch (Exception ex)
                {
                    return new ResponseContext(false, 500, $"Error performing search: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Get top terms in an index.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        private async Task GetTopTermsRoute(HttpContextBase ctx)
        {
            await WrappedRequestHandler(ctx, RequestTypeEnum.Search, async (reqCtx) =>
            {
                string? indexId = ctx.Request.Url.Parameters["id"];
                if (String.IsNullOrEmpty(indexId))
                {
                    return new ResponseContext(false, 400, "Index ID is required");
                }

                InvertedIndex? index = _IndexManager!.GetIndex(indexId);
                if (index == null)
                {
                    return new ResponseContext(false, 404, "Index not found");
                }

                int limit = 10;
                string? limitParam = ctx.Request.Query?.Elements?["limit"];
                if (!String.IsNullOrEmpty(limitParam) && Int32.TryParse(limitParam, out int parsedLimit) && parsedLimit > 0)
                {
                    limit = parsedLimit;
                }

                try
                {
                    List<TermRecord> topTerms = await index.GetTopTermsAsync(limit).ConfigureAwait(false);

                    // Convert to dictionary format { "term": documentFrequency }
                    Dictionary<string, int> result = new Dictionary<string, int>();
                    foreach (TermRecord term in topTerms)
                    {
                        result[term.Term] = term.DocumentFrequency;
                    }

                    return new ResponseContext
                    {
                        Success = true,
                        StatusCode = 200,
                        Data = result
                    };
                }
                catch (Exception ex)
                {
                    return new ResponseContext(false, 500, $"Error getting top terms: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Rebuild term ID cache for an index.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        private async Task PostIndexCacheRebuildRoute(HttpContextBase ctx)
        {
            await WrappedRequestHandler(ctx, RequestTypeEnum.IndexManagement, async (reqCtx) =>
            {
                string? indexId = ctx.Request.Url.Parameters["id"];
                if (String.IsNullOrEmpty(indexId))
                {
                    return new ResponseContext(false, 400, "Index ID is required");
                }

                InvertedIndex? index = _IndexManager!.GetIndex(indexId);
                if (index == null)
                {
                    return new ResponseContext(false, 404, "Index not found");
                }

                try
                {
                    int termsLoaded = await index.RebuildTermCacheAsync().ConfigureAwait(false);

                    return new ResponseContext
                    {
                        Success = true,
                        StatusCode = 200,
                        Data = new
                        {
                            Success = true,
                            TermsLoaded = termsLoaded,
                            IndexId = indexId
                        }
                    };
                }
                catch (Exception ex)
                {
                    return new ResponseContext(false, 500, $"Error rebuilding cache: {ex.Message}");
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

                // Parse enumeration query parameters
                EnumerationQuery query = EnumerationQuery.Parse(
                    ctx.Request.Query?.Elements?["maxResults"],
                    ctx.Request.Query?.Elements?["skip"],
                    ctx.Request.Query?.Elements?["continuationToken"],
                    ctx.Request.Query?.Elements?["ordering"]
                );

                // Handle continuation token if provided
                if (!String.IsNullOrEmpty(query.ContinuationToken) &&
                    EnumerationResult<TenantMetadata>.TryParseContinuationToken(query.ContinuationToken, out int tokenSkip))
                {
                    query.Skip = tokenSkip;
                }

                List<TenantMetadata> allTenants;
                if (auth.IsGlobalAdmin)
                {
                    // Global admins can see all tenants
                    allTenants = await _Database!.Tenants.ReadManyAsync().ConfigureAwait(false);
                }
                else
                {
                    // Tenant admins can only see their own tenant
                    TenantMetadata? tenant = await _Database!.Tenants.ReadByIdentifierAsync(auth.TenantId).ConfigureAwait(false);
                    allTenants = tenant != null ? new List<TenantMetadata> { tenant } : new List<TenantMetadata>();
                }

                long totalCount = allTenants.Count;

                // Apply ordering
                IEnumerable<TenantMetadata> ordered = query.Ordering == EnumerationOrderEnum.CreatedAscending
                    ? allTenants.OrderBy(x => x.CreatedUtc)
                    : allTenants.OrderByDescending(x => x.CreatedUtc);

                // Apply pagination
                List<TenantMetadata> pagedTenants = ordered
                    .Skip(query.Skip)
                    .Take(query.MaxResults)
                    .ToList();

                EnumerationResult<TenantMetadata> result = new EnumerationResult<TenantMetadata>(query, pagedTenants, totalCount);

                return new ResponseContext
                {
                    Success = true,
                    StatusCode = 200,
                    Data = result
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

                // Parse enumeration query parameters
                EnumerationQuery query = EnumerationQuery.Parse(
                    ctx.Request.Query?.Elements?["maxResults"],
                    ctx.Request.Query?.Elements?["skip"],
                    ctx.Request.Query?.Elements?["continuationToken"],
                    ctx.Request.Query?.Elements?["ordering"]
                );

                // Handle continuation token if provided
                if (!String.IsNullOrEmpty(query.ContinuationToken) &&
                    EnumerationResult<object>.TryParseContinuationToken(query.ContinuationToken, out int tokenSkip))
                {
                    query.Skip = tokenSkip;
                }

                List<UserMaster> allUsers = await _Database!.Users.ReadManyAsync(tenantId).ConfigureAwait(false);
                long totalCount = allUsers.Count;

                // Apply ordering
                IEnumerable<UserMaster> ordered = query.Ordering == EnumerationOrderEnum.CreatedAscending
                    ? allUsers.OrderBy(x => x.CreatedUtc)
                    : allUsers.OrderByDescending(x => x.CreatedUtc);

                // Apply pagination and project to response format
                List<object> pagedUsers = ordered
                    .Skip(query.Skip)
                    .Take(query.MaxResults)
                    .Select(u => new { u.Identifier, u.TenantId, u.Email, u.FirstName, u.LastName, u.IsAdmin, u.Active, u.CreatedUtc } as object)
                    .ToList();

                EnumerationResult<object> result = new EnumerationResult<object>(query, pagedUsers, totalCount);

                return new ResponseContext
                {
                    Success = true,
                    StatusCode = 200,
                    Data = result
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

                // Parse enumeration query parameters
                EnumerationQuery query = EnumerationQuery.Parse(
                    ctx.Request.Query?.Elements?["maxResults"],
                    ctx.Request.Query?.Elements?["skip"],
                    ctx.Request.Query?.Elements?["continuationToken"],
                    ctx.Request.Query?.Elements?["ordering"]
                );

                // Handle continuation token if provided
                if (!String.IsNullOrEmpty(query.ContinuationToken) &&
                    EnumerationResult<object>.TryParseContinuationToken(query.ContinuationToken, out int tokenSkip))
                {
                    query.Skip = tokenSkip;
                }

                List<Credential> allCredentials = await _Database!.Credentials.ReadManyAsync(tenantId).ConfigureAwait(false);
                long totalCount = allCredentials.Count;

                // Apply ordering
                IEnumerable<Credential> ordered = query.Ordering == EnumerationOrderEnum.CreatedAscending
                    ? allCredentials.OrderBy(x => x.CreatedUtc)
                    : allCredentials.OrderByDescending(x => x.CreatedUtc);

                // Apply pagination and project to response format
                List<object> pagedCredentials = ordered
                    .Skip(query.Skip)
                    .Take(query.MaxResults)
                    .Select(c => new { c.Identifier, c.TenantId, c.UserId, c.Name, c.Active, c.CreatedUtc, TokenPreview = c.BearerToken.Substring(0, Math.Min(8, c.BearerToken.Length)) + "..." } as object)
                    .ToList();

                EnumerationResult<object> result = new EnumerationResult<object>(query, pagedCredentials, totalCount);

                return new ResponseContext
                {
                    Success = true,
                    StatusCode = 200,
                    Data = result
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

        private async Task GetRequestHistoryRoute(HttpContextBase ctx)
        {
            await WrappedRequestHandler(ctx, RequestTypeEnum.IndexManagement, async (reqCtx) =>
            {
                if (_RequestHistory == null || !_RequestHistory.Enabled)
                {
                    return new ResponseContext(false, 503, "Request history is disabled");
                }

                AuthContext? auth = await _Auth!.AuthenticateBearerAsync(GetAuthToken(ctx) ?? "").ConfigureAwait(false);
                if (auth == null || !auth.IsAuthenticated)
                {
                    return new ResponseContext(false, 401, "Authentication required");
                }

                Dictionary<string, string> queryParameters = GetQueryParameters(ctx);
                RequestHistoryQuery query = RequestHistoryQuery.Parse(
                    queryParameters.GetValueOrDefault("page"),
                    queryParameters.GetValueOrDefault("pageSize"),
                    queryParameters.GetValueOrDefault("method"),
                    queryParameters.GetValueOrDefault("route"),
                    queryParameters.GetValueOrDefault("tenantId"),
                    queryParameters.GetValueOrDefault("userId"),
                    queryParameters.GetValueOrDefault("credentialId"),
                    queryParameters.GetValueOrDefault("indexId"),
                    queryParameters.GetValueOrDefault("sourceIp"),
                    queryParameters.GetValueOrDefault("principal"),
                    queryParameters.GetValueOrDefault("statusCode"),
                    queryParameters.GetValueOrDefault("fromUtc"),
                    queryParameters.GetValueOrDefault("toUtc"),
                    queryParameters.GetValueOrDefault("bucketMinutes"));

                ApplyRequestHistoryScope(auth, query);
                (List<RequestHistoryEntry> entries, long totalCount) = await _RequestHistory.SearchAsync(query).ConfigureAwait(false);

                return new ResponseContext
                {
                    Success = true,
                    StatusCode = 200,
                    Data = new
                    {
                        Objects = entries,
                        TotalCount = totalCount,
                        Page = query.Page,
                        PageSize = query.PageSize,
                        TotalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize)
                    }
                };
            });
        }

        private async Task GetRequestHistorySummaryRoute(HttpContextBase ctx)
        {
            await WrappedRequestHandler(ctx, RequestTypeEnum.IndexManagement, async (reqCtx) =>
            {
                if (_RequestHistory == null || !_RequestHistory.Enabled)
                {
                    return new ResponseContext(false, 503, "Request history is disabled");
                }

                AuthContext? auth = await _Auth!.AuthenticateBearerAsync(GetAuthToken(ctx) ?? "").ConfigureAwait(false);
                if (auth == null || !auth.IsAuthenticated)
                {
                    return new ResponseContext(false, 401, "Authentication required");
                }

                Dictionary<string, string> queryParameters = GetQueryParameters(ctx);
                RequestHistoryQuery query = RequestHistoryQuery.Parse(
                    null,
                    null,
                    queryParameters.GetValueOrDefault("method"),
                    queryParameters.GetValueOrDefault("route"),
                    queryParameters.GetValueOrDefault("tenantId"),
                    queryParameters.GetValueOrDefault("userId"),
                    queryParameters.GetValueOrDefault("credentialId"),
                    queryParameters.GetValueOrDefault("indexId"),
                    queryParameters.GetValueOrDefault("sourceIp"),
                    queryParameters.GetValueOrDefault("principal"),
                    queryParameters.GetValueOrDefault("statusCode"),
                    queryParameters.GetValueOrDefault("fromUtc"),
                    queryParameters.GetValueOrDefault("toUtc"),
                    queryParameters.GetValueOrDefault("bucketMinutes"));

                ApplyRequestHistoryScope(auth, query);
                RequestHistorySummary summary = await _RequestHistory.GetSummaryAsync(query).ConfigureAwait(false);

                return new ResponseContext
                {
                    Success = true,
                    StatusCode = 200,
                    Data = summary
                };
            });
        }

        private async Task GetRequestHistoryEntryRoute(HttpContextBase ctx)
        {
            await WrappedRequestHandler(ctx, RequestTypeEnum.IndexManagement, async (reqCtx) =>
            {
                if (_RequestHistory == null || !_RequestHistory.Enabled)
                {
                    return new ResponseContext(false, 503, "Request history is disabled");
                }

                string? id = ctx.Request.Url.Parameters["id"];
                if (String.IsNullOrWhiteSpace(id))
                {
                    return new ResponseContext(false, 400, "Entry ID is required");
                }

                AuthContext? auth = await _Auth!.AuthenticateBearerAsync(GetAuthToken(ctx) ?? "").ConfigureAwait(false);
                if (auth == null || !auth.IsAuthenticated)
                {
                    return new ResponseContext(false, 401, "Authentication required");
                }

                RequestHistoryEntry? entry = await _RequestHistory.GetEntryAsync(id).ConfigureAwait(false);
                if (entry == null)
                {
                    return new ResponseContext(false, 404, "Entry not found");
                }

                if (!CanAccessRequestHistoryEntry(auth, entry))
                {
                    return new ResponseContext(false, 403, "Access denied");
                }

                return new ResponseContext
                {
                    Success = true,
                    StatusCode = 200,
                    Data = entry
                };
            });
        }

        private async Task GetRequestHistoryDetailRoute(HttpContextBase ctx)
        {
            await WrappedRequestHandler(ctx, RequestTypeEnum.IndexManagement, async (reqCtx) =>
            {
                if (_RequestHistory == null || !_RequestHistory.Enabled)
                {
                    return new ResponseContext(false, 503, "Request history is disabled");
                }

                string? id = ctx.Request.Url.Parameters["id"];
                if (String.IsNullOrWhiteSpace(id))
                {
                    return new ResponseContext(false, 400, "Entry ID is required");
                }

                AuthContext? auth = await _Auth!.AuthenticateBearerAsync(GetAuthToken(ctx) ?? "").ConfigureAwait(false);
                if (auth == null || !auth.IsAuthenticated)
                {
                    return new ResponseContext(false, 401, "Authentication required");
                }

                RequestHistoryEntry? entry = await _RequestHistory.GetEntryAsync(id).ConfigureAwait(false);
                if (entry == null)
                {
                    return new ResponseContext(false, 404, "Entry not found");
                }

                if (!CanAccessRequestHistoryEntry(auth, entry))
                {
                    return new ResponseContext(false, 403, "Access denied");
                }

                RequestHistoryDetail? detail = await _RequestHistory.GetDetailAsync(id).ConfigureAwait(false);
                if (detail == null)
                {
                    return new ResponseContext(false, 404, "Detail not found");
                }

                return new ResponseContext
                {
                    Success = true,
                    StatusCode = 200,
                    Data = detail
                };
            });
        }

        private async Task DeleteRequestHistoryEntryRoute(HttpContextBase ctx)
        {
            await WrappedRequestHandler(ctx, RequestTypeEnum.IndexManagement, async (reqCtx) =>
            {
                if (_RequestHistory == null || !_RequestHistory.Enabled)
                {
                    return new ResponseContext(false, 503, "Request history is disabled");
                }

                string? id = ctx.Request.Url.Parameters["id"];
                if (String.IsNullOrWhiteSpace(id))
                {
                    return new ResponseContext(false, 400, "Entry ID is required");
                }

                AuthContext? auth = await _Auth!.AuthenticateBearerAsync(GetAuthToken(ctx) ?? "").ConfigureAwait(false);
                if (auth == null || !auth.IsAuthenticated)
                {
                    return new ResponseContext(false, 401, "Authentication required");
                }

                RequestHistoryEntry? entry = await _RequestHistory.GetEntryAsync(id).ConfigureAwait(false);
                if (entry == null)
                {
                    return new ResponseContext(false, 404, "Entry not found");
                }

                if (!CanAccessRequestHistoryEntry(auth, entry))
                {
                    return new ResponseContext(false, 403, "Access denied");
                }

                bool deleted = await _RequestHistory.DeleteAsync(id).ConfigureAwait(false);
                return new ResponseContext
                {
                    Success = deleted,
                    StatusCode = deleted ? 200 : 404,
                    Data = new { Deleted = deleted, Id = id },
                    ErrorMessage = deleted ? null : "Entry not found"
                };
            });
        }

        private async Task DeleteRequestHistoryBulkRoute(HttpContextBase ctx)
        {
            await WrappedRequestHandler(ctx, RequestTypeEnum.IndexManagement, async (reqCtx) =>
            {
                if (_RequestHistory == null || !_RequestHistory.Enabled)
                {
                    return new ResponseContext(false, 503, "Request history is disabled");
                }

                AuthContext? auth = await _Auth!.AuthenticateBearerAsync(GetAuthToken(ctx) ?? "").ConfigureAwait(false);
                if (auth == null || !auth.IsAuthenticated)
                {
                    return new ResponseContext(false, 401, "Authentication required");
                }

                Dictionary<string, string> queryParameters = GetQueryParameters(ctx);
                RequestHistoryQuery query = RequestHistoryQuery.Parse(
                    null,
                    null,
                    queryParameters.GetValueOrDefault("method"),
                    queryParameters.GetValueOrDefault("route"),
                    queryParameters.GetValueOrDefault("tenantId"),
                    queryParameters.GetValueOrDefault("userId"),
                    queryParameters.GetValueOrDefault("credentialId"),
                    queryParameters.GetValueOrDefault("indexId"),
                    queryParameters.GetValueOrDefault("sourceIp"),
                    queryParameters.GetValueOrDefault("principal"),
                    queryParameters.GetValueOrDefault("statusCode"),
                    queryParameters.GetValueOrDefault("fromUtc"),
                    queryParameters.GetValueOrDefault("toUtc"),
                    queryParameters.GetValueOrDefault("bucketMinutes"));

                ApplyRequestHistoryScope(auth, query);
                RequestHistoryBulkDeleteResult deleteResult = await _RequestHistory.BulkDeleteAsync(query).ConfigureAwait(false);

                return new ResponseContext
                {
                    Success = true,
                    StatusCode = 200,
                    Data = deleteResult
                };
            });
        }

        #region Backup-Restore-Handlers

        /// <summary>
        /// Create backup of an index.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        private async Task PostIndexBackupRoute(HttpContextBase ctx)
        {
            try
            {
                // Get tenant from auth context
                AuthContext? auth = await _Auth!.AuthenticateBearerAsync(GetAuthToken(ctx) ?? "").ConfigureAwait(false);
                string tenantId = !String.IsNullOrEmpty(auth?.TenantId) ? auth.TenantId : "default";

                string? indexId = ctx.Request.Url.Parameters["id"];
                if (String.IsNullOrEmpty(indexId))
                {
                    await SendResponse(ctx, new ResponseContext(false, 400, "Index ID is required"));
                    return;
                }

                if (!_IndexManager!.IndexExists(indexId))
                {
                    await SendResponse(ctx, new ResponseContext(false, 404, "Index not found"));
                    return;
                }

                // Create backup (supports both on-disk and in-memory indices)
                Stream backupStream = await _BackupService!.CreateBackupAsync(tenantId, indexId).ConfigureAwait(false);

                // Generate filename
                string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                string filename = $"backup-{indexId}-{timestamp}.vbx";

                // Send binary response
                await SendBinaryResponse(ctx, backupStream, "application/zip", filename);
            }
            catch (InvalidOperationException ex)
            {
                await SendResponse(ctx, new ResponseContext(false, 400, ex.Message));
            }
            catch (Exception ex)
            {
                _Logging?.Error(_Header + "backup failed: " + ex.Message);
                await SendResponse(ctx, new ResponseContext(false, 500, "Backup failed: " + ex.Message));
            }
        }

        /// <summary>
        /// Restore backup to create a new index.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        private async Task PostIndicesRestoreRoute(HttpContextBase ctx)
        {
            try
            {
                // Get tenant from auth context
                AuthContext? auth = await _Auth!.AuthenticateBearerAsync(GetAuthToken(ctx) ?? "").ConfigureAwait(false);
                string tenantId = !String.IsNullOrEmpty(auth?.TenantId) ? auth.TenantId : "default";

                // Parse multipart form data
                MultipartFormData? formData = await ParseMultipartFormDataAsync(ctx).ConfigureAwait(false);
                if (formData == null || formData.FileData == null || formData.FileData.Length == 0)
                {
                    await SendResponse(ctx, new ResponseContext(false, 400, "Backup file is required. Use multipart/form-data with 'file' field."));
                    return;
                }

                // Validate content type
                if (!String.IsNullOrEmpty(formData.FileContentType) &&
                    !formData.FileContentType.Contains("zip") &&
                    !formData.FileContentType.Contains("octet-stream"))
                {
                    await SendResponse(ctx, new ResponseContext(false, 415, "Invalid file format. Expected ZIP archive (.vbx file)."));
                    return;
                }

                string? newName = formData.Fields.ContainsKey("name") ? formData.Fields["name"] : null;
                string? newId = formData.Fields.ContainsKey("indexId") ? formData.Fields["indexId"] : null;

                // Perform restore
                using MemoryStream backupStream = new MemoryStream(formData.FileData);
                RestoreResult result = await _BackupService!.RestoreNewAsync(tenantId, backupStream, newName, newId).ConfigureAwait(false);

                if (result.Success)
                {
                    await SendResponse(ctx, new ResponseContext
                    {
                        Success = true,
                        StatusCode = 201,
                        Data = new
                        {
                            Message = result.Message,
                            IndexId = result.IndexId,
                            Warnings = result.Warnings
                        }
                    });
                }
                else
                {
                    await SendResponse(ctx, new ResponseContext(false, 422, result.Message));
                }
            }
            catch (Exception ex)
            {
                _Logging?.Error(_Header + "restore (new) failed: " + ex.Message);
                await SendResponse(ctx, new ResponseContext(false, 500, "Restore failed: " + ex.Message));
            }
        }

        /// <summary>
        /// Restore backup by replacing an existing index.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        private async Task PostIndexRestoreRoute(HttpContextBase ctx)
        {
            try
            {
                // Get tenant from auth context
                AuthContext? auth = await _Auth!.AuthenticateBearerAsync(GetAuthToken(ctx) ?? "").ConfigureAwait(false);
                string tenantId = !String.IsNullOrEmpty(auth?.TenantId) ? auth.TenantId : "default";

                string? indexId = ctx.Request.Url.Parameters["id"];
                if (String.IsNullOrEmpty(indexId))
                {
                    await SendResponse(ctx, new ResponseContext(false, 400, "Index ID is required"));
                    return;
                }

                if (!_IndexManager!.IndexExists(indexId))
                {
                    await SendResponse(ctx, new ResponseContext(false, 404, "Index not found"));
                    return;
                }

                IndexMetadata? metadata = _IndexManager.GetMetadata(indexId);
                if (metadata != null && metadata.InMemory)
                {
                    await SendResponse(ctx, new ResponseContext(false, 400, "Cannot restore to in-memory indices"));
                    return;
                }

                // Parse multipart form data
                MultipartFormData? formData = await ParseMultipartFormDataAsync(ctx).ConfigureAwait(false);
                if (formData == null || formData.FileData == null || formData.FileData.Length == 0)
                {
                    await SendResponse(ctx, new ResponseContext(false, 400, "Backup file is required. Use multipart/form-data with 'file' field."));
                    return;
                }

                // Validate content type
                if (!String.IsNullOrEmpty(formData.FileContentType) &&
                    !formData.FileContentType.Contains("zip") &&
                    !formData.FileContentType.Contains("octet-stream"))
                {
                    await SendResponse(ctx, new ResponseContext(false, 415, "Invalid file format. Expected ZIP archive (.vbx file)."));
                    return;
                }

                // Perform restore
                using MemoryStream backupStream = new MemoryStream(formData.FileData);
                RestoreResult result = await _BackupService!.RestoreReplaceAsync(tenantId, indexId, backupStream).ConfigureAwait(false);

                if (result.Success)
                {
                    await SendResponse(ctx, new ResponseContext
                    {
                        Success = true,
                        StatusCode = 200,
                        Data = new
                        {
                            Message = result.Message,
                            IndexId = result.IndexId,
                            Warnings = result.Warnings
                        }
                    });
                }
                else
                {
                    await SendResponse(ctx, new ResponseContext(false, 422, result.Message));
                }
            }
            catch (Exception ex)
            {
                _Logging?.Error(_Header + "restore (replace) failed: " + ex.Message);
                await SendResponse(ctx, new ResponseContext(false, 500, "Restore failed: " + ex.Message));
            }
        }

        /// <summary>
        /// Send a binary response with file download headers.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <param name="stream">Data stream to send.</param>
        /// <param name="contentType">Content type.</param>
        /// <param name="filename">Filename for Content-Disposition header.</param>
        /// <returns>Task.</returns>
        private async Task SendBinaryResponse(HttpContextBase ctx, Stream stream, string contentType, string filename)
        {
            RequestHistoryCaptureContext capture = GetOrCreateRequestHistoryCapture(ctx);
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = contentType;
            ctx.Response.Headers.Add("Content-Disposition", $"attachment; filename=\"{filename}\"");
            ctx.Response.Headers.Add("Access-Control-Allow-Origin", "*");

            long length = stream.CanSeek ? stream.Length : 0;
            capture.StatusCode = 200;
            capture.Success = true;
            capture.ResponseContentType = contentType;
            capture.ResponseHeaders = MergeResponseHeaders(ctx.Response.Headers, null);
            capture.ResponseSizeBytes = length;
            capture.ResponseBody = $"Binary response ({contentType}, {length} bytes, filename {filename})";
            capture.ResponseBodyTruncated = false;
            capture.IsBinaryResponse = true;
            capture.ResponseFilename = filename;

            using (stream)
            {
                await ctx.Response.Send(stream.Length, stream).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Parse multipart form data from request.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Parsed form data or null if parsing fails.</returns>
        private async Task<MultipartFormData?> ParseMultipartFormDataAsync(HttpContextBase ctx)
        {
            if (ctx.Request.Data == null || ctx.Request.ContentLength <= 0)
            {
                return null;
            }

            string? contentType = ctx.Request.ContentType;
            if (String.IsNullOrEmpty(contentType) || !contentType.Contains("multipart/form-data"))
            {
                return null;
            }

            // Extract boundary from content type
            Match boundaryMatch = Regex.Match(contentType, @"boundary=(?:""([^""]+)""|([^\s;]+))");
            if (!boundaryMatch.Success)
            {
                return null;
            }

            string boundary = boundaryMatch.Groups[1].Success ? boundaryMatch.Groups[1].Value : boundaryMatch.Groups[2].Value;
            byte[] boundaryBytes = Encoding.ASCII.GetBytes("--" + boundary);
            byte[] crlfCrlf = Encoding.ASCII.GetBytes("\r\n\r\n");
            byte[] lflf = Encoding.ASCII.GetBytes("\n\n");
            byte[] crlf = Encoding.ASCII.GetBytes("\r\n");
            byte[] lf = Encoding.ASCII.GetBytes("\n");

            MultipartFormData result = new MultipartFormData();

            // Read entire body as raw bytes
            byte[] bodyData;
            using (MemoryStream ms = new MemoryStream())
            {
                await ctx.Request.Data.CopyToAsync(ms).ConfigureAwait(false);
                bodyData = ms.ToArray();
            }

            RequestHistoryCaptureContext capture = GetOrCreateRequestHistoryCapture(ctx);
            capture.RequestBodyCaptured = true;
            capture.RequestSizeBytes = bodyData.LongLength;
            capture.RequestContentType = ctx.Request.ContentType;
            capture.RawRequestBody = null;
            capture.RequestBody = $"Multipart form-data request ({bodyData.LongLength} bytes)";
            capture.RequestBodyTruncated = false;

            // Find all boundary positions in raw bytes
            List<int> boundaryPositions = new List<int>();
            int searchStart = 0;
            while (searchStart < bodyData.Length)
            {
                int pos = FindByteSequence(bodyData, boundaryBytes, searchStart);
                if (pos < 0) break;
                boundaryPositions.Add(pos);
                searchStart = pos + boundaryBytes.Length;
            }

            // Process each part between boundaries
            for (int i = 0; i < boundaryPositions.Count - 1; i++)
            {
                int partStart = boundaryPositions[i] + boundaryBytes.Length;
                int partEnd = boundaryPositions[i + 1];

                // Skip leading CRLF or LF after boundary
                if (partStart < bodyData.Length && bodyData[partStart] == '\r') partStart++;
                if (partStart < bodyData.Length && bodyData[partStart] == '\n') partStart++;

                // Skip trailing CRLF or LF before next boundary
                if (partEnd > 0 && bodyData[partEnd - 1] == '\n') partEnd--;
                if (partEnd > 0 && bodyData[partEnd - 1] == '\r') partEnd--;

                if (partStart >= partEnd) continue;

                // Find header/body separator in this part
                int headerEndIndex = FindByteSequence(bodyData, crlfCrlf, partStart, partEnd);
                int separatorLength = 4;
                if (headerEndIndex < 0)
                {
                    headerEndIndex = FindByteSequence(bodyData, lflf, partStart, partEnd);
                    separatorLength = 2;
                }

                if (headerEndIndex < 0 || headerEndIndex >= partEnd) continue;

                // Extract headers as string (headers are always ASCII/UTF-8 text)
                string headers = Encoding.UTF8.GetString(bodyData, partStart, headerEndIndex - partStart);

                // Body starts after the separator
                int bodyStart = headerEndIndex + separatorLength;
                int bodyLength = partEnd - bodyStart;

                if (bodyLength < 0) continue;

                // Parse Content-Disposition
                Match dispositionMatch = Regex.Match(headers, @"Content-Disposition:\s*form-data;\s*name=""([^""]+)""(?:;\s*filename=""([^""]+)"")?", RegexOptions.IgnoreCase);
                if (!dispositionMatch.Success)
                {
                    continue;
                }

                string fieldName = dispositionMatch.Groups[1].Value;
                string? fileName = dispositionMatch.Groups[2].Success ? dispositionMatch.Groups[2].Value : null;

                if (!String.IsNullOrEmpty(fileName))
                {
                    // This is a file field - extract raw bytes without any encoding conversion
                    byte[] fileData = new byte[bodyLength];
                    Array.Copy(bodyData, bodyStart, fileData, 0, bodyLength);
                    result.FileData = fileData;
                    result.FileName = fileName;
                    capture.RequestBody = $"Multipart form-data request ({bodyData.LongLength} bytes, file {fileName})";

                    Match contentTypeMatch = Regex.Match(headers, @"Content-Type:\s*([^\r\n]+)", RegexOptions.IgnoreCase);
                    if (contentTypeMatch.Success)
                    {
                        result.FileContentType = contentTypeMatch.Groups[1].Value.Trim();
                    }
                }
                else
                {
                    // This is a regular form field - convert to string
                    string fieldValue = Encoding.UTF8.GetString(bodyData, bodyStart, bodyLength).Trim();
                    result.Fields[fieldName] = fieldValue;
                }
            }

            return result;
        }

        /// <summary>
        /// Find byte sequence in array starting from a given position.
        /// </summary>
        /// <param name="data">The byte array to search in.</param>
        /// <param name="sequence">The byte sequence to find.</param>
        /// <param name="startIndex">Starting index for search (default 0).</param>
        /// <param name="endIndex">End index for search (default -1 means end of array).</param>
        /// <returns>Index of the sequence if found, -1 otherwise.</returns>
        private int FindByteSequence(byte[] data, byte[] sequence, int startIndex = 0, int endIndex = -1)
        {
            if (endIndex < 0) endIndex = data.Length;
            int maxIndex = Math.Min(endIndex, data.Length) - sequence.Length;

            for (int i = startIndex; i <= maxIndex; i++)
            {
                bool found = true;
                for (int j = 0; j < sequence.Length; j++)
                {
                    if (data[i + j] != sequence[j])
                    {
                        found = false;
                        break;
                    }
                }
                if (found)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Container for parsed multipart form data.
        /// </summary>
        private class MultipartFormData
        {
            public Dictionary<string, string> Fields { get; set; } = new Dictionary<string, string>();
            public byte[]? FileData { get; set; }
            public string? FileName { get; set; }
            public string? FileContentType { get; set; }
        }

        private class RequestHistoryCaptureContext
        {
            public string Id { get; set; } = Guid.NewGuid().ToString();
            public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
            public RequestTypeEnum RequestType { get; set; } = RequestTypeEnum.Unknown;
            public string HttpMethod { get; set; } = "GET";
            public string RequestUrl { get; set; } = "/";
            public string RouteTemplate { get; set; } = "/";
            public string? SourceIp { get; set; }
            public string? AuthToken { get; set; }
            public string? RequestContentType { get; set; }
            public Dictionary<string, string> RequestHeaders { get; set; } = new Dictionary<string, string>();
            public Dictionary<string, string> RouteParameters { get; set; } = new Dictionary<string, string>();
            public Dictionary<string, string> QueryParameters { get; set; } = new Dictionary<string, string>();
            public bool RequestBodyCaptured { get; set; }
            public string? RawRequestBody { get; set; }
            public string? RequestBody { get; set; }
            public long RequestSizeBytes { get; set; }
            public bool RequestBodyTruncated { get; set; }
            public int StatusCode { get; set; }
            public bool Success { get; set; }
            public string? ResponseContentType { get; set; }
            public Dictionary<string, string> ResponseHeaders { get; set; } = new Dictionary<string, string>();
            public string? ResponseBody { get; set; }
            public long ResponseSizeBytes { get; set; }
            public bool ResponseBodyTruncated { get; set; }
            public bool IsBinaryResponse { get; set; }
            public string? ResponseFilename { get; set; }
            public double DurationMs { get; set; }
        }

        private RequestHistoryCaptureContext GetOrCreateRequestHistoryCapture(HttpContextBase ctx)
        {
            return _RequestHistoryContexts.GetValue(ctx, CreateRequestHistoryCapture);
        }

        private RequestHistoryCaptureContext CreateRequestHistoryCapture(HttpContextBase ctx)
        {
            return new RequestHistoryCaptureContext
            {
                Id = Guid.NewGuid().ToString(),
                CreatedUtc = DateTime.UtcNow,
                RequestType = RequestTypeEnum.Unknown,
                HttpMethod = ctx.Request.Method.ToString(),
                RequestUrl = ctx.Request.Url.Full,
                RouteTemplate = NormalizeRouteTemplate(ctx),
                SourceIp = ctx.Request.Source.IpAddress,
                AuthToken = GetAuthToken(ctx),
                RequestContentType = ctx.Request.ContentType,
                RequestHeaders = SanitizeHeaders(ctx.Request.Headers),
                RouteParameters = GetRouteParameters(ctx),
                QueryParameters = GetQueryParameters(ctx)
            };
        }

        private async Task PersistRequestHistoryAsync(HttpContextBase ctx)
        {
            if (_RequestHistory == null || !_RequestHistory.Enabled)
            {
                return;
            }

            RequestHistoryCaptureContext capture = GetOrCreateRequestHistoryCapture(ctx);

            try
            {
                if (!ShouldTrackRequestHistory(capture))
                {
                    return;
                }

                capture.RouteTemplate = NormalizeRouteTemplate(ctx);
                capture.RouteParameters = GetRouteParameters(ctx);
                capture.QueryParameters = GetQueryParameters(ctx);
                capture.DurationMs = ctx.Response.Timestamp.TotalMs.HasValue ? ctx.Response.Timestamp.TotalMs.Value : 0;

                AuthContext? auth = null;
                if (!String.IsNullOrWhiteSpace(capture.AuthToken))
                {
                    auth = await _Auth!.AuthenticateBearerAsync(capture.AuthToken).ConfigureAwait(false);
                }

                RequestHistoryEntry entry = new RequestHistoryEntry
                {
                    Id = capture.Id,
                    TenantId = auth?.TenantId,
                    UserId = auth?.UserId,
                    CredentialId = auth?.CredentialId,
                    PrincipalType = auth == null
                        ? null
                        : auth.IsGlobalAdmin
                            ? "GlobalAdmin"
                            : auth.IsTenantAdmin
                                ? "TenantAdmin"
                                : "User",
                    PrincipalName = auth == null
                        ? null
                        : !String.IsNullOrWhiteSpace(auth.Email)
                            ? auth.Email
                            : auth.IsGlobalAdmin
                                ? "Global Admin"
                                : auth.UserId,
                    RequestType = capture.RequestType.ToString(),
                    HttpMethod = capture.HttpMethod,
                    RouteTemplate = capture.RouteTemplate,
                    RequestUrl = capture.RequestUrl,
                    SourceIp = capture.SourceIp,
                    IndexId = capture.RouteParameters.TryGetValue("id", out string? indexId) && capture.RouteTemplate.Contains("/indices/")
                        ? indexId
                        : null,
                    DocumentId = capture.RouteParameters.TryGetValue("docId", out string? docId) ? docId : null,
                    StatusCode = capture.StatusCode,
                    Success = capture.Success,
                    RequestContentType = capture.RequestContentType,
                    ResponseContentType = capture.ResponseContentType,
                    RequestSizeBytes = capture.RequestSizeBytes,
                    ResponseSizeBytes = capture.ResponseSizeBytes,
                    RequestBodyTruncated = capture.RequestBodyTruncated,
                    ResponseBodyTruncated = capture.ResponseBodyTruncated,
                    IsBinaryResponse = capture.IsBinaryResponse,
                    DurationMs = capture.DurationMs,
                    CreatedUtc = capture.CreatedUtc,
                    LastUpdateUtc = DateTime.UtcNow
                };

                RequestHistoryDetail detail = new RequestHistoryDetail
                {
                    Id = capture.Id,
                    RouteParameters = capture.RouteParameters,
                    QueryParameters = capture.QueryParameters,
                    RequestHeaders = capture.RequestHeaders,
                    RequestBody = capture.RequestBody,
                    RequestBodyBytes = capture.RequestSizeBytes,
                    RequestBodyTruncated = capture.RequestBodyTruncated,
                    ResponseHeaders = capture.ResponseHeaders,
                    ResponseBody = capture.ResponseBody,
                    ResponseBodyBytes = capture.ResponseSizeBytes,
                    ResponseBodyTruncated = capture.ResponseBodyTruncated,
                    ResponseFilename = capture.ResponseFilename,
                    Notes = capture.IsBinaryResponse ? "Binary responses are summarized and not stored verbatim." : null
                };

                await _RequestHistory.RecordAsync(entry, detail).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _Logging?.Warn(_Header + "request history persistence failed: " + e.Message);
            }
            finally
            {
                _RequestHistoryContexts.Remove(ctx);
            }
        }

        private bool ShouldTrackRequestHistory(RequestHistoryCaptureContext capture)
        {
            if (_Settings == null || !_Settings.RequestHistory.Enabled)
            {
                return false;
            }

            string path = capture.RouteTemplate ?? String.Empty;
            if (String.Equals(capture.HttpMethod, "OPTIONS", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (capture.RequestType == RequestTypeEnum.HealthCheck)
            {
                return false;
            }

            if (path.StartsWith("/openapi.json", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/v1.0/requesthistory", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        private void StoreCapturedRequestBody(RequestHistoryCaptureContext capture, string body)
        {
            capture.RequestBodyCaptured = true;
            capture.RawRequestBody = body;
            capture.RequestSizeBytes = Encoding.UTF8.GetByteCount(body);
            capture.RequestBody = TruncateText(body, _Settings!.RequestHistory.MaxRequestBodyBytes, out bool truncated);
            capture.RequestBodyTruncated = truncated;
        }

        private static string NormalizeRouteTemplate(HttpContextBase ctx)
        {
            string raw = ctx.Request.Url.RawWithQuery ?? ctx.Request.Url.Full ?? "/";
            int queryIndex = raw.IndexOf('?');
            if (queryIndex >= 0)
            {
                raw = raw.Substring(0, queryIndex);
            }

            Dictionary<string, string> parameters = GetRouteParameters(ctx);
            if (parameters.Count < 1)
            {
                return raw;
            }

            string[] segments = raw.Split('/', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < segments.Length; i++)
            {
                foreach (KeyValuePair<string, string> parameter in parameters)
                {
                    if (String.Equals(segments[i], parameter.Value, StringComparison.Ordinal))
                    {
                        segments[i] = "{" + parameter.Key + "}";
                        break;
                    }
                }
            }

            return "/" + String.Join("/", segments);
        }

        private static Dictionary<string, string> GetRouteParameters(HttpContextBase ctx)
        {
            Dictionary<string, string> ret = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (ctx.Request.Url.Parameters == null)
            {
                return ret;
            }

            foreach (string key in ctx.Request.Url.Parameters.Keys)
            {
                if (!String.IsNullOrWhiteSpace(key))
                {
                    string? value = ctx.Request.Url.Parameters[key];
                    if (!String.IsNullOrWhiteSpace(value))
                    {
                        ret[key] = value;
                    }
                }
            }

            return ret;
        }

        private static Dictionary<string, string> GetQueryParameters(HttpContextBase ctx)
        {
            Dictionary<string, string> ret = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (ctx.Request.Query?.Elements == null)
            {
                return ret;
            }

            foreach (string key in ctx.Request.Query.Elements.Keys)
            {
                if (!String.IsNullOrWhiteSpace(key))
                {
                    string? value = ctx.Request.Query.Elements[key];
                    if (!String.IsNullOrWhiteSpace(value))
                    {
                        ret[key] = value;
                    }
                }
            }

            return ret;
        }

        private static Dictionary<string, string> SanitizeHeaders(NameValueCollection? headers)
        {
            Dictionary<string, string> ret = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (headers == null)
            {
                return ret;
            }

            for (int i = 0; i < headers.Count; i++)
            {
                string? key = headers.GetKey(i);
                if (String.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                string? value = headers.Get(i);
                if (String.Equals(key, "Authorization", StringComparison.OrdinalIgnoreCase))
                {
                    ret[key] = "(redacted)";
                }
                else if (!String.IsNullOrWhiteSpace(value))
                {
                    ret[key] = value;
                }
            }

            return ret;
        }

        private static Dictionary<string, string> MergeResponseHeaders(NameValueCollection? responseHeaders, Dictionary<string, string>? extraHeaders)
        {
            Dictionary<string, string> ret = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (responseHeaders != null)
            {
                for (int i = 0; i < responseHeaders.Count; i++)
                {
                    string? key = responseHeaders.GetKey(i);
                    string? value = responseHeaders.Get(i);
                    if (!String.IsNullOrWhiteSpace(key) && !String.IsNullOrWhiteSpace(value))
                    {
                        ret[key] = value;
                    }
                }
            }

            if (extraHeaders != null)
            {
                foreach (KeyValuePair<string, string> header in extraHeaders)
                {
                    ret[header.Key] = header.Value;
                }
            }

            return ret;
        }

        private static string TruncateText(string text, int maxBytes, out bool truncated)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            if (bytes.Length <= maxBytes)
            {
                truncated = false;
                return text;
            }

            truncated = true;
            string shortened = Encoding.UTF8.GetString(bytes, 0, maxBytes);
            return shortened + "\n...(truncated)";
        }

        #endregion

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
            RequestHistoryCaptureContext capture = GetOrCreateRequestHistoryCapture(ctx);
            capture.RequestType = requestType;
            capture.RouteTemplate = NormalizeRouteTemplate(ctx);
            capture.RouteParameters = GetRouteParameters(ctx);
            capture.QueryParameters = GetQueryParameters(ctx);
            capture.RequestHeaders = SanitizeHeaders(ctx.Request.Headers);
            capture.RequestContentType = ctx.Request.ContentType;

            RequestContext requestContext = new RequestContext
            {
                RequestType = requestType,
                Method = ctx.Request.Method.ToString(),
                Url = ctx.Request.Url.Full,
                IpAddress = ctx.Request.Source.IpAddress,
                AuthToken = GetAuthToken(ctx),
                Headers = capture.RequestHeaders
            };

            return requestContext;
        }

        private static void ApplyRequestHistoryScope(AuthContext auth, RequestHistoryQuery query)
        {
            if (auth.IsGlobalAdmin)
            {
                return;
            }

            query.TenantId = auth.TenantId;

            if (!auth.HasAdminAccess)
            {
                query.UserId = auth.UserId;
                query.CredentialId = auth.CredentialId;
            }
        }

        private static bool CanAccessRequestHistoryEntry(AuthContext auth, RequestHistoryEntry entry)
        {
            if (auth.IsGlobalAdmin)
            {
                return true;
            }

            if (!String.Equals(auth.TenantId, entry.TenantId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (auth.HasAdminAccess)
            {
                return true;
            }

            return String.Equals(auth.UserId, entry.UserId, StringComparison.OrdinalIgnoreCase)
                || String.Equals(auth.CredentialId, entry.CredentialId, StringComparison.OrdinalIgnoreCase);
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
            RequestHistoryCaptureContext capture = GetOrCreateRequestHistoryCapture(ctx);
            if (capture.RequestBodyCaptured)
            {
                return capture.RawRequestBody ?? String.Empty;
            }

            if (ctx.Request.Data != null && ctx.Request.ContentLength > 0)
            {
                using (StreamReader reader = new StreamReader(ctx.Request.Data, Encoding.UTF8))
                {
                    string body = await reader.ReadToEndAsync().ConfigureAwait(false);
                    StoreCapturedRequestBody(capture, body);
                    return body;
                }
            }

            capture.RequestBodyCaptured = true;
            capture.RawRequestBody = String.Empty;
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
            RequestHistoryCaptureContext capture = GetOrCreateRequestHistoryCapture(ctx);
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

            capture.StatusCode = response.StatusCode;
            capture.Success = response.Success;
            capture.ResponseContentType = ctx.Response.ContentType;
            capture.ResponseHeaders = MergeResponseHeaders(ctx.Response.Headers, response.Headers);
            capture.ResponseSizeBytes = Encoding.UTF8.GetByteCount(json);
            capture.ResponseBody = TruncateText(json, _Settings!.RequestHistory.MaxResponseBodyBytes, out bool truncated);
            capture.ResponseBodyTruncated = truncated;

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
        /// Create batch delete result schema.
        /// </summary>
        private OpenApiSchemaMetadata CreateBatchDeleteResultSchema()
        {
            OpenApiSchemaMetadata response = CreateResponseSchema();
            response.Properties!["Data"] = new OpenApiSchemaMetadata
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchemaMetadata>
                {
                    ["Deleted"] = OpenApiSchemaMetadata.CreateArray(OpenApiSchemaMetadata.String()),
                    ["NotFound"] = OpenApiSchemaMetadata.CreateArray(OpenApiSchemaMetadata.String()),
                    ["DeletedCount"] = OpenApiSchemaMetadata.Integer(),
                    ["NotFoundCount"] = OpenApiSchemaMetadata.Integer(),
                    ["RequestedCount"] = OpenApiSchemaMetadata.Integer()
                }
            };
            return response;
        }

        /// <summary>
        /// Create batch add documents request schema.
        /// </summary>
        private OpenApiSchemaMetadata CreateBatchAddDocumentsRequestSchema()
        {
            return new OpenApiSchemaMetadata
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchemaMetadata>
                {
                    ["Documents"] = OpenApiSchemaMetadata.CreateArray(new OpenApiSchemaMetadata
                    {
                        Type = "object",
                        Properties = new Dictionary<string, OpenApiSchemaMetadata>
                        {
                            ["Id"] = new OpenApiSchemaMetadata { Type = "string", Description = "Optional document ID (auto-generated if not provided)" },
                            ["Name"] = new OpenApiSchemaMetadata { Type = "string", Description = "Document name/path" },
                            ["Content"] = new OpenApiSchemaMetadata { Type = "string", Description = "Document content to index" },
                            ["Labels"] = OpenApiSchemaMetadata.CreateArray(OpenApiSchemaMetadata.String()),
                            ["Tags"] = new OpenApiSchemaMetadata { Type = "object", Description = "Key-value pairs" },
                            ["CustomMetadata"] = new OpenApiSchemaMetadata { Type = "object", Description = "Custom metadata (any JSON value)" }
                        },
                        Required = new List<string> { "Name", "Content" }
                    })
                },
                Required = new List<string> { "Documents" }
            };
        }

        /// <summary>
        /// Create batch add documents response schema.
        /// </summary>
        private OpenApiSchemaMetadata CreateBatchAddDocumentsResponseSchema()
        {
            OpenApiSchemaMetadata response = CreateResponseSchema();
            response.Properties!["Data"] = new OpenApiSchemaMetadata
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchemaMetadata>
                {
                    ["Added"] = OpenApiSchemaMetadata.CreateArray(new OpenApiSchemaMetadata
                    {
                        Type = "object",
                        Properties = new Dictionary<string, OpenApiSchemaMetadata>
                        {
                            ["DocumentId"] = OpenApiSchemaMetadata.String(),
                            ["Name"] = OpenApiSchemaMetadata.String(),
                            ["Success"] = OpenApiSchemaMetadata.Boolean()
                        }
                    }),
                    ["Failed"] = OpenApiSchemaMetadata.CreateArray(new OpenApiSchemaMetadata
                    {
                        Type = "object",
                        Properties = new Dictionary<string, OpenApiSchemaMetadata>
                        {
                            ["Name"] = OpenApiSchemaMetadata.String(),
                            ["Success"] = OpenApiSchemaMetadata.Boolean(),
                            ["ErrorMessage"] = OpenApiSchemaMetadata.String()
                        }
                    }),
                    ["AddedCount"] = OpenApiSchemaMetadata.Integer(),
                    ["FailedCount"] = OpenApiSchemaMetadata.Integer(),
                    ["RequestedCount"] = OpenApiSchemaMetadata.Integer()
                }
            };
            return response;
        }

        /// <summary>
        /// Create batch check existence request schema.
        /// </summary>
        private OpenApiSchemaMetadata CreateBatchCheckExistenceRequestSchema()
        {
            return new OpenApiSchemaMetadata
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchemaMetadata>
                {
                    ["Ids"] = OpenApiSchemaMetadata.CreateArray(OpenApiSchemaMetadata.String())
                },
                Required = new List<string> { "Ids" }
            };
        }

        /// <summary>
        /// Create batch check existence response schema.
        /// </summary>
        private OpenApiSchemaMetadata CreateBatchCheckExistenceResponseSchema()
        {
            OpenApiSchemaMetadata response = CreateResponseSchema();
            response.Properties!["Data"] = new OpenApiSchemaMetadata
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchemaMetadata>
                {
                    ["Exists"] = OpenApiSchemaMetadata.CreateArray(OpenApiSchemaMetadata.String()),
                    ["NotFound"] = OpenApiSchemaMetadata.CreateArray(OpenApiSchemaMetadata.String()),
                    ["ExistsCount"] = OpenApiSchemaMetadata.Integer(),
                    ["NotFoundCount"] = OpenApiSchemaMetadata.Integer(),
                    ["RequestedCount"] = OpenApiSchemaMetadata.Integer()
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
        /// Create update index request schema.
        /// </summary>
        private OpenApiSchemaMetadata CreateUpdateIndexRequestSchema()
        {
            return new OpenApiSchemaMetadata
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchemaMetadata>
                {
                    ["Name"] = new OpenApiSchemaMetadata
                    {
                        Type = "string",
                        Description = "New name for the index (optional)"
                    },
                    ["Description"] = new OpenApiSchemaMetadata
                    {
                        Type = "string",
                        Description = "New description for the index (optional)"
                    },
                    ["Enabled"] = new OpenApiSchemaMetadata
                    {
                        Type = "boolean",
                        Description = "New enabled status for the index (optional)"
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

        /// <summary>
        /// Create top terms response schema.
        /// </summary>
        private OpenApiSchemaMetadata CreateTopTermsResponseSchema()
        {
            OpenApiSchemaMetadata response = CreateResponseSchema();
            response.Properties!["Data"] = new OpenApiSchemaMetadata
            {
                Type = "object",
                Description = "Dictionary of terms with their document frequencies (term -> count)"
            };
            return response;
        }

        /// <summary>
        /// Create cache rebuild response schema.
        /// </summary>
        private OpenApiSchemaMetadata CreateCacheRebuildResponseSchema()
        {
            OpenApiSchemaMetadata response = CreateResponseSchema();
            response.Properties!["Data"] = new OpenApiSchemaMetadata
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchemaMetadata>
                {
                    ["Success"] = new OpenApiSchemaMetadata { Type = "boolean", Description = "Whether the rebuild was successful" },
                    ["TermsLoaded"] = new OpenApiSchemaMetadata { Type = "integer", Description = "Number of terms loaded into the cache" },
                    ["IndexId"] = new OpenApiSchemaMetadata { Type = "string", Description = "The index ID" }
                }
            };
            return response;
        }

        /// <summary>
        /// Create restore result schema.
        /// </summary>
        private OpenApiSchemaMetadata CreateRestoreResultSchema()
        {
            OpenApiSchemaMetadata response = CreateResponseSchema();
            response.Properties!["Data"] = new OpenApiSchemaMetadata
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchemaMetadata>
                {
                    ["Success"] = new OpenApiSchemaMetadata { Type = "boolean", Description = "Whether the restore was successful" },
                    ["IndexId"] = new OpenApiSchemaMetadata { Type = "string", Description = "The restored index ID" },
                    ["Message"] = new OpenApiSchemaMetadata { Type = "string", Description = "Result message" },
                    ["Warnings"] = OpenApiSchemaMetadata.CreateArray(OpenApiSchemaMetadata.String())
                }
            };
            return response;
        }

        private OpenApiSchemaMetadata CreateRequestHistoryEntrySchema()
        {
            return new OpenApiSchemaMetadata
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchemaMetadata>
                {
                    ["Id"] = OpenApiSchemaMetadata.String(),
                    ["TenantId"] = new OpenApiSchemaMetadata { Type = "string", Nullable = true },
                    ["UserId"] = new OpenApiSchemaMetadata { Type = "string", Nullable = true },
                    ["CredentialId"] = new OpenApiSchemaMetadata { Type = "string", Nullable = true },
                    ["PrincipalType"] = new OpenApiSchemaMetadata { Type = "string", Nullable = true },
                    ["PrincipalName"] = new OpenApiSchemaMetadata { Type = "string", Nullable = true },
                    ["RequestType"] = OpenApiSchemaMetadata.String(),
                    ["HttpMethod"] = OpenApiSchemaMetadata.String(),
                    ["RouteTemplate"] = OpenApiSchemaMetadata.String(),
                    ["RequestUrl"] = OpenApiSchemaMetadata.String(),
                    ["SourceIp"] = new OpenApiSchemaMetadata { Type = "string", Nullable = true },
                    ["IndexId"] = new OpenApiSchemaMetadata { Type = "string", Nullable = true },
                    ["DocumentId"] = new OpenApiSchemaMetadata { Type = "string", Nullable = true },
                    ["StatusCode"] = OpenApiSchemaMetadata.Integer(),
                    ["Success"] = OpenApiSchemaMetadata.Boolean(),
                    ["RequestContentType"] = new OpenApiSchemaMetadata { Type = "string", Nullable = true },
                    ["ResponseContentType"] = new OpenApiSchemaMetadata { Type = "string", Nullable = true },
                    ["RequestSizeBytes"] = OpenApiSchemaMetadata.Integer("int64"),
                    ["ResponseSizeBytes"] = OpenApiSchemaMetadata.Integer("int64"),
                    ["RequestBodyTruncated"] = OpenApiSchemaMetadata.Boolean(),
                    ["ResponseBodyTruncated"] = OpenApiSchemaMetadata.Boolean(),
                    ["IsBinaryResponse"] = OpenApiSchemaMetadata.Boolean(),
                    ["DurationMs"] = OpenApiSchemaMetadata.Number("double"),
                    ["CreatedUtc"] = OpenApiSchemaMetadata.String("date-time"),
                    ["LastUpdateUtc"] = OpenApiSchemaMetadata.String("date-time")
                }
            };
        }

        private OpenApiSchemaMetadata CreateRequestHistoryListSchema()
        {
            OpenApiSchemaMetadata response = CreateResponseSchema();
            response.Properties!["Data"] = new OpenApiSchemaMetadata
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchemaMetadata>
                {
                    ["Objects"] = OpenApiSchemaMetadata.CreateArray(CreateRequestHistoryEntrySchema()),
                    ["TotalCount"] = OpenApiSchemaMetadata.Integer("int64"),
                    ["Page"] = OpenApiSchemaMetadata.Integer(),
                    ["PageSize"] = OpenApiSchemaMetadata.Integer(),
                    ["TotalPages"] = OpenApiSchemaMetadata.Integer()
                }
            };
            return response;
        }

        private OpenApiSchemaMetadata CreateRequestHistoryDetailSchema()
        {
            OpenApiSchemaMetadata response = CreateResponseSchema();
            response.Properties!["Data"] = new OpenApiSchemaMetadata
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchemaMetadata>
                {
                    ["Id"] = OpenApiSchemaMetadata.String(),
                    ["RouteParameters"] = new OpenApiSchemaMetadata { Type = "object" },
                    ["QueryParameters"] = new OpenApiSchemaMetadata { Type = "object" },
                    ["RequestHeaders"] = new OpenApiSchemaMetadata { Type = "object" },
                    ["RequestBody"] = new OpenApiSchemaMetadata { Type = "string", Nullable = true },
                    ["RequestBodyBytes"] = OpenApiSchemaMetadata.Integer("int64"),
                    ["RequestBodyTruncated"] = OpenApiSchemaMetadata.Boolean(),
                    ["ResponseHeaders"] = new OpenApiSchemaMetadata { Type = "object" },
                    ["ResponseBody"] = new OpenApiSchemaMetadata { Type = "string", Nullable = true },
                    ["ResponseBodyBytes"] = OpenApiSchemaMetadata.Integer("int64"),
                    ["ResponseBodyTruncated"] = OpenApiSchemaMetadata.Boolean(),
                    ["ResponseFilename"] = new OpenApiSchemaMetadata { Type = "string", Nullable = true },
                    ["Notes"] = new OpenApiSchemaMetadata { Type = "string", Nullable = true }
                }
            };
            return response;
        }

        private OpenApiSchemaMetadata CreateRequestHistorySummarySchema()
        {
            OpenApiSchemaMetadata response = CreateResponseSchema();
            response.Properties!["Data"] = new OpenApiSchemaMetadata
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchemaMetadata>
                {
                    ["Buckets"] = OpenApiSchemaMetadata.CreateArray(new OpenApiSchemaMetadata
                    {
                        Type = "object",
                        Properties = new Dictionary<string, OpenApiSchemaMetadata>
                        {
                            ["BucketStartUtc"] = OpenApiSchemaMetadata.String("date-time"),
                            ["BucketEndUtc"] = OpenApiSchemaMetadata.String("date-time"),
                            ["TotalCount"] = OpenApiSchemaMetadata.Integer(),
                            ["SuccessCount"] = OpenApiSchemaMetadata.Integer(),
                            ["FailureCount"] = OpenApiSchemaMetadata.Integer(),
                            ["AverageDurationMs"] = OpenApiSchemaMetadata.Number("double")
                        }
                    }),
                    ["TotalCount"] = OpenApiSchemaMetadata.Integer(),
                    ["SuccessCount"] = OpenApiSchemaMetadata.Integer(),
                    ["FailureCount"] = OpenApiSchemaMetadata.Integer(),
                    ["AverageDurationMs"] = OpenApiSchemaMetadata.Number("double")
                }
            };
            return response;
        }

        private OpenApiSchemaMetadata CreateRequestHistoryBulkDeleteSchema()
        {
            OpenApiSchemaMetadata response = CreateResponseSchema();
            response.Properties!["Data"] = new OpenApiSchemaMetadata
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchemaMetadata>
                {
                    ["DeletedCount"] = OpenApiSchemaMetadata.Integer(),
                    ["DeletedIds"] = OpenApiSchemaMetadata.CreateArray(OpenApiSchemaMetadata.String())
                }
            };
            return response;
        }

        #endregion

        #endregion
    }
}
