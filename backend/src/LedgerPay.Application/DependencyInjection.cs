using FluentValidation;
using LedgerPay.Application.Common;
using LedgerPay.Application.Settings;
using LedgerPay.Application.Transfers;
using Microsoft.Extensions.DependencyInjection;

namespace LedgerPay.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<LedgerSettingsProvider>();
        services.AddScoped<ITransferService, TransferService>();
        services.AddSingleton<IValidator<TransferRequest>, TransferRequestValidator>();

        return services;
    }
}
