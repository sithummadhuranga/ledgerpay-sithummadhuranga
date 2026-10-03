using FluentValidation;
using LedgerPay.Application;
using LedgerPay.Application.Transfers;
using LedgerPay.Infrastructure;
using LedgerPay.IntegrationTests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace LedgerPay.IntegrationTests.Transfers;

public class DependencyInjectionTests(SqlServerFixture sql)
{
    [Fact]
    public async Task The_transfer_service_and_its_validator_resolve_from_the_registered_services()
    {
        var services = new ServiceCollection()
            .AddInfrastructure(sql.AdminConnectionString)
            .AddApplication();
        await using var provider = services.BuildServiceProvider(validateScopes: true);
        await using var scope = provider.CreateAsyncScope();

        var service = scope.ServiceProvider.GetRequiredService<ITransferService>();
        var validator = scope.ServiceProvider.GetRequiredService<IValidator<TransferRequest>>();

        Assert.IsType<TransferService>(service);
        Assert.IsType<TransferRequestValidator>(validator);
    }

    [Fact]
    public async Task A_service_resolved_from_the_container_can_quote_a_transfer()
    {
        var services = new ServiceCollection()
            .AddInfrastructure(sql.AdminConnectionString)
            .AddApplication();
        await using var provider = services.BuildServiceProvider(validateScopes: true);
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ITransferService>();

        var result = await service.QuoteAsync(5000.00m, TestContext.Current.CancellationToken);

        Assert.Equal(25.00m, result.Value!.Fee);
    }
}
