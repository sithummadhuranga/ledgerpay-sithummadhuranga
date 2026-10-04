using System.Text.Json.Serialization;
using LedgerPay.Api.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi;

namespace LedgerPay.Api.Extensions;

// The OpenAPI document comes from the routes. This adds the bearer token, the 401, 403 and 429 answers,
// and that the Idempotency-Key header is required.
public static class ApiDocsExtensions
{
    public const string DocumentPath = "/openapi/v1.json";
    private const string BearerScheme = "Bearer";
    private const string JsonType = "application/json";

    public static IServiceCollection AddApiDocs(this IServiceCollection services)
    {
        // The document is made with the minimal API JSON options, not the MVC ones, so enums are set up here too.
        services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        services.AddOpenApi("v1", options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "LedgerPay API",
                    Version = "v1",
                    Description = "A small LKR digital wallet with a double-entry ledger. Sign in at /api/v1/auth/login, "
                        + "then send the token as a Bearer token. Errors use RFC 7807 Problem Details with a stable code."
                };

                // The document is served by the API itself, so it needs no server address. A client uses its own origin.
                document.Servers = [];

                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes[BearerScheme] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "The access token from /api/v1/auth/login. It lasts 15 minutes."
                };

                // The health check is a framework endpoint, which the route scan does not list, so it is added here.
                document.Paths[HealthExtensions.Path] = new OpenApiPathItem
                {
                    Operations = new Dictionary<HttpMethod, OpenApiOperation>
                    {
                        [HttpMethod.Get] = new OpenApiOperation
                        {
                            Summary = "Answers Healthy when the API can reach its database and Unhealthy when it cannot. No token needed.",
                            Tags = new HashSet<OpenApiTagReference> { new("Health", document) },
                            Responses = new OpenApiResponses
                            {
                                ["200"] = new OpenApiResponse { Description = "Healthy" },
                                ["503"] = new OpenApiResponse { Description = "Unhealthy" }
                            }
                        }
                    }
                };
                return Task.CompletedTask;
            });

            options.AddOperationTransformer((operation, context, _) =>
            {
                var metadata = context.Description.ActionDescriptor.EndpointMetadata;
                var needsToken = metadata.OfType<IAuthorizeData>().Any() && !metadata.OfType<IAllowAnonymous>().Any();
                if (needsToken)
                {
                    operation.Security ??= [];
                    operation.Security.Add(new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference(BearerScheme, context.Document)] = []
                    });

                    operation.Responses ??= [];
                    operation.Responses.TryAdd("401", new OpenApiResponse { Description = "No token, or the token is expired or not valid. Code UNAUTHENTICATED." });

                    var needsRole = metadata.OfType<IAuthorizeData>().Any(data => !string.IsNullOrEmpty(data.Policy) || !string.IsNullOrEmpty(data.Roles));
                    if (needsRole)
                    {
                        operation.Responses.TryAdd("403", new OpenApiResponse { Description = "The signed-in user's role is not allowed here. Code FORBIDDEN." });
                    }
                }

                if (metadata.OfType<EnableRateLimitingAttribute>().Any())
                {
                    operation.Responses ??= [];
                    operation.Responses.TryAdd("429", new OpenApiResponse { Description = "Too many requests. Wait for the number of seconds in the Retry-After header. Code RATE_LIMITED." });
                }

                // MVC lists every format it could write. The API only speaks JSON, so the others are left out.
                foreach (var content in new[] { operation.RequestBody?.Content }.Concat(operation.Responses?.Values.Select(response => response.Content) ?? []))
                {
                    foreach (var mediaType in content?.Keys.Where(key => key is not (JsonType or Problems.ContentType)).ToList() ?? [])
                    {
                        content!.Remove(mediaType);
                    }
                }

                foreach (var parameter in operation.Parameters?.OfType<OpenApiParameter>() ?? [])
                {
                    if (parameter.Name == ControllerResultExtensions.IdempotencyKeyHeader)
                    {
                        parameter.Required = true;
                        parameter.Description = "A new value for each money move, up to 100 characters from A-Z, a-z, 0-9, _ and -. "
                            + "Sending the same key and body again gives the first answer. The same key with another body gives 409.";
                    }
                }

                return Task.CompletedTask;
            });
        });

        return services;
    }

    // The document and the page that shows it. Both are public: they describe the routes and hold no data.
    public static WebApplication UseApiDocs(this WebApplication app)
    {
        app.MapOpenApi().AllowAnonymous();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint(DocumentPath, "LedgerPay API v1");
            options.RoutePrefix = "swagger";
            options.DocumentTitle = "LedgerPay API";
        });
        return app;
    }
}
