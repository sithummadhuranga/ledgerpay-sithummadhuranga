using FluentValidation;
using LedgerPay.Application.Idempotency;
using LedgerPay.Application.Settings;
using LedgerPay.Application.Transfers;
using Microsoft.Extensions.DependencyInjection;

namespace LedgerPay.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<LedgerSettingsProvider>();
        services.AddScoped<IdempotencyService>();

        services.AddScoped<ITransferService, TransferService>();

        services.AddSingleton<IValidator<TransferRequest>, TransferRequestValidator>();

        return services;
    }
}
