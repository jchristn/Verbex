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
        /// <param name="logging">Logging module.</param>
        /// <exception cref="ArgumentNullException">Thrown when required parameter is null.</exception>
        public RestServiceHandler(Settings settings, AuthenticationService auth, IndexManager indexManager, LoggingModule logging)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Auth = auth ?? throw new ArgumentNullException(nameof(auth));
            _IndexManager = indexManager ?? throw new ArgumentNullException(nameof(indexManager));
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
            // General
            _Webserver!.Routes.Preflight = PreflightRoute;
            _Webserver.Routes.PostRouting = PostRoutingRoute;

            // Health check routes
            _Webserver.Routes.PreAuthentication.Static.Add(
                HttpMethod.GET, "/", GetHealthRoute,
                metadata => metadata
                    .WithTag("Health")
                    .WithDescription("Root health check endpoint. Returns service status, version, and timestamp."),
                ExceptionRoute);

            _Webserver.Routes.PreAuthentication.Static.Add(
                HttpMethod.GET, "/v1.0/health", GetHealthRoute,
                metadata => metadata
                    .WithTag("Health")
                    .WithDescription("Versioned health check endpoint. Returns service status, version, and timestamp.")
                    .WithResponse(200, OpenApiResponseMetadata.Json("Service is healthy", CreateResponseSchema())),
                ExceptionRoute);

            // Authentication routes
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

            // Index management routes
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
                HttpMethod.DELETE, "/v1.0/indices/{id}", DeleteIndexRoute,
                metadata => metadata
                    .WithTag("Indices")
                    .WithDescription("Delete an index and all its documents permanently.")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "The unique identifier of the index to delete"))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Index deleted successfully", CreateMessageSchema()))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()),
                ExceptionRoute);

            // Index labels and tags update routes
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

            // Index-specific document routes
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

            // Document labels and tags update routes
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

            // Index-specific search routes
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

                // Simple authentication - in real implementation, validate against database
                if (loginRequest.Username == "admin" && loginRequest.Password == "password")
                {
                    string token = _Auth!.GenerateToken(loginRequest.Username);
                    return new ResponseContext
                    {
                        Success = true,
                        StatusCode = 200,
                        Data = new { Token = token, Username = loginRequest.Username }
                    };
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
            await WrappedRequestHandler(ctx, RequestTypeEnum.Authentication, (reqCtx) =>
            {
                string? token = GetAuthToken(ctx);
                bool isValid = !string.IsNullOrEmpty(token) && _Auth!.AuthenticateBearer(token);

                return Task.FromResult(new ResponseContext
                {
                    Success = isValid,
                    StatusCode = isValid ? 200 : 401,
                    Data = new { Valid = isValid }
                });
            });
        }

        /// <summary>
        /// Get all indices route.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        private async Task GetIndicesRoute(HttpContextBase ctx)
        {
            await WrappedRequestHandler(ctx, RequestTypeEnum.IndexManagement, (reqCtx) =>
            {
                var indices = _IndexManager!.GetAllConfigurations().Select(config => new
                {
                    Id = config.Id,
                    Name = config.Name,
                    Description = config.Description,
                    Enabled = config.Enabled,
                    InMemory = config.InMemory,
                    CreatedUtc = config.CreatedUtc,
                    Labels = config.Labels,
                    Tags = config.Tags
                }).ToList();

                return Task.FromResult(new ResponseContext
                {
                    Success = true,
                    StatusCode = 200,
                    Data = new { Indices = indices, Count = indices.Count }
                });
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

                if (_IndexManager!.IndexExists(createRequest.Id))
                {
                    return new ResponseContext(false, 409, "Index with this ID already exists");
                }

                IndexConfiguration config = createRequest.ToIndexConfiguration();

                bool created = await _IndexManager!.CreateIndexAsync(config).ConfigureAwait(false);
                if (created)
                {
                    return new ResponseContext
                    {
                        Success = true,
                        StatusCode = 201,
                        Data = new {
                            Message = "Index created successfully",
                            Index = new {
                                Id = config.Id,
                                Name = config.Name,
                                Description = config.Description,
                                InMemory = config.InMemory,
                                CreatedUtc = config.CreatedUtc,
                                Labels = config.Labels,
                                Tags = config.Tags
                            }
                        }
                    };
                }
                else
                {
                    return new ResponseContext(false, 500, "Failed to create index");
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
        /// Delete index route.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        private async Task DeleteIndexRoute(HttpContextBase ctx)
        {
            await WrappedRequestHandler(ctx, RequestTypeEnum.IndexManagement, async (reqCtx) =>
            {
                string? indexId = ctx.Request.Url.Parameters["id"];
                if (String.IsNullOrEmpty(indexId))
                {
                    return new ResponseContext(false, 400, "Index ID is required");
                }

                if (!_IndexManager!.IndexExists(indexId))
                {
                    return new ResponseContext(false, 404, "Index not found");
                }

                bool deleted = await _IndexManager!.DeleteIndexAsync(indexId).ConfigureAwait(false);
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

                bool updated = _IndexManager.UpdateIndexLabels(indexId, request.Labels);
                if (updated)
                {
                    IndexConfiguration? config = _IndexManager.GetConfiguration(indexId);
                    return new ResponseContext
                    {
                        Success = true,
                        StatusCode = 200,
                        Data = new
                        {
                            Message = "Labels updated successfully",
                            Index = new
                            {
                                Id = config?.Id,
                                Name = config?.Name,
                                Labels = config?.Labels,
                                Tags = config?.Tags
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

                bool updated = _IndexManager.UpdateIndexTags(indexId, request.Tags);
                if (updated)
                {
                    IndexConfiguration? config = _IndexManager.GetConfiguration(indexId);
                    return new ResponseContext
                    {
                        Success = true,
                        StatusCode = 200,
                        Data = new
                        {
                            Message = "Tags updated successfully",
                            Index = new
                            {
                                Id = config?.Id,
                                Name = config?.Name,
                                Labels = config?.Labels,
                                Tags = config?.Tags
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
                    // Add the document
                    string documentPath = documentRequest.Id ?? IdGenerator.GenerateDocumentId();
                    string documentId = await index.AddDocumentAsync(
                        documentPath,
                        documentRequest.Content).ConfigureAwait(false);

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
                _Logging?.Error(_Header + "Exception in " + requestType + ": " + e.Message);
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

            string json = JsonSerializer.Serialize(response, _JsonOptions);

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
                    ["Id"] = new OpenApiSchemaMetadata { Type = "string", Description = "Unique identifier of the index" },
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
                    ["Id"] = new OpenApiSchemaMetadata { Type = "string", Description = "Unique identifier for the index" },
                    ["Name"] = new OpenApiSchemaMetadata { Type = "string", Description = "Display name of the index" },
                    ["Description"] = new OpenApiSchemaMetadata { Type = "string", Description = "Optional description" },
                    ["RepositoryFilename"] = new OpenApiSchemaMetadata { Type = "string", Description = "Filename for persistent storage" },
                    ["InMemory"] = new OpenApiSchemaMetadata { Type = "boolean", Description = "Whether to store in memory only" },
                    ["StorageMode"] = new OpenApiSchemaMetadata { Type = "string", Description = "Storage mode (MemoryOnly, OnDisk)" },
                    ["EnableLemmatizer"] = new OpenApiSchemaMetadata { Type = "boolean", Description = "Enable lemmatization" },
                    ["EnableStopWordRemover"] = new OpenApiSchemaMetadata { Type = "boolean", Description = "Enable stop word removal" },
                    ["MinTokenLength"] = new OpenApiSchemaMetadata { Type = "integer", Description = "Minimum token length" },
                    ["MaxTokenLength"] = new OpenApiSchemaMetadata { Type = "integer", Description = "Maximum token length" },
                    ["Labels"] = OpenApiSchemaMetadata.CreateArray(OpenApiSchemaMetadata.String()),
                    ["Tags"] = new OpenApiSchemaMetadata { Type = "object", Description = "Key-value pairs" }
                },
                Required = new List<string> { "Id", "Name" }
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