using FluentValidation;
using LedgerPay.Api.Controllers;
using LedgerPay.IntegrationTests.Support;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;

namespace LedgerPay.IntegrationTests.Api;

public class ValidatorCoverageTests(SqlServerFixture sql)
{
    // The validation filter skips a request type that has no validator, so a new request type would go in unchecked.
    [Fact]
    public void Every_request_type_an_action_takes_has_a_validator()
    {
        var actions = sql.Api.Services.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>()
            .Where(action => action.ControllerTypeInfo.Assembly == typeof(AuthController).Assembly)
            .ToList();
        Assert.NotEmpty(actions);

        var requestTypes = actions
            .SelectMany(action => action.Parameters)
            .Where(parameter => parameter.BindingInfo?.BindingSource is { } source && (source == BindingSource.Body || source == BindingSource.Query))
            .Select(parameter => parameter.ParameterType)
            .Where(type => type.IsClass && type != typeof(string))
            .Distinct()
            .ToList();

        Assert.Contains(requestTypes, type => type.Name == "TransferRequest");
        foreach (var type in requestTypes)
        {
            var validatorType = typeof(IValidator<>).MakeGenericType(type);
            Assert.True(sql.Api.Services.GetService(validatorType) is not null, $"{type.Name} has no validator");
        }
    }
}
