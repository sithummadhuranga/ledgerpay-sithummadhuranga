using System.Text.Json.Serialization;
using LedgerPay.Api.Errors;
using LedgerPay.Api.Filters;
using LedgerPay.Domain.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace LedgerPay.Api.Extensions;

public static class ApiBehaviorExtensions
{
    public static IServiceCollection AddApiControllers(this IServiceCollection services)
    {
        services
            .AddControllers(options =>
            {
                // A missing field is reported by the FluentValidation validator, in words the client can use.
                // MVC's own required check would answer before the validator runs.
                options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
                options.Filters.Add<ValidationFilter>();

                // Every action needs a signed-in user, so a new controller is closed until it says otherwise.
                // Public actions have to say [AllowAnonymous].
                options.Filters.Add(new AuthorizeFilter());
            })
            .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
            .ConfigureApiBehaviorOptions(options =>
            {
                // The framework's own 404, 415 and similar answers stay empty here, and the status page turns them
                // into the one error shape with a stable code.
                options.SuppressMapClientErrors = true;

                // A body that is not JSON, or has the wrong types in it, is answered in the same shape as any other
                // bad input. The parser's own message is never sent, because it names internals.
                options.InvalidModelStateResponseFactory = context =>
                {
                    var bodyParameters = context.ActionDescriptor.Parameters
                        .Where(parameter => parameter.BindingInfo?.BindingSource == BindingSource.Body)
                        .Select(parameter => parameter.Name)
                        .ToHashSet();

                    // The key of a problem with the body as a whole is the name of the action parameter, which the client never sees.
                    var errors = new Dictionary<string, string[]>();
                    foreach (var (key, entry) in context.ModelState.Where(pair => pair.Value is { Errors.Count: > 0 }))
                    {
                        errors[bodyParameters.Contains(key) ? "body" : ValidationFilter.FieldName(key)] = ["The value is not valid."];
                    }

                    if (errors.Count == 0)
                    {
                        errors["body"] = ["A request body is required."];
                    }

                    return Problems.Result(context.HttpContext, ErrorCodes.ValidationFailed, errors);
                };
            });

        return services;
    }
}
