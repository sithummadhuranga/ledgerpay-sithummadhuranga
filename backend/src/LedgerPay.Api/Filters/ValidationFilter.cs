using System.Text.Json;
using FluentValidation;
using LedgerPay.Api.Errors;
using LedgerPay.Domain.Constants;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LedgerPay.Api.Filters;

// Runs the FluentValidation validator of every request body before the controller sees it,
// so a service never gets a request that has not been checked.
public sealed class ValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var errors = new Dictionary<string, string[]>();

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (context.HttpContext.RequestServices.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var result = await validator.ValidateAsync(
                new ValidationContext<object>(argument), context.HttpContext.RequestAborted);

            foreach (var group in result.Errors.GroupBy(error => FieldName(error.PropertyName)))
            {
                errors[group.Key] = group.Select(error => error.ErrorMessage).Distinct().ToArray();
            }
        }

        if (errors.Count > 0)
        {
            context.Result = Problems.Result(context.HttpContext, ErrorCodes.ValidationFailed, errors);
            return;
        }

        await next();
    }

    // The field name as the client wrote it in JSON. A problem with the body as a whole is called "body".
    public static string FieldName(string propertyName)
    {
        var name = propertyName.StartsWith("$.", StringComparison.Ordinal) ? propertyName[2..] : propertyName;
        return name is "" or "$" ? "body" : JsonNamingPolicy.CamelCase.ConvertName(name);
    }
}
