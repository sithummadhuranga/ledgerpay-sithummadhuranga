using Microsoft.AspNetCore.HttpOverrides;

namespace LedgerPay.Api.Extensions;

public static class ForwardedHeadersExtensions
{
    public const string SettingName = "ForwardedHeaders:Enabled";

    // Off by default. Turn it on only when the API sits behind a proxy that you control, such as the platform's
    // front end, because then every request reaches the API from the proxy and the client address is only in the header.
    // With it on, any sender is trusted as a proxy, so it must not be reachable except through the proxy.
    public static IServiceCollection AddForwardedHeadersIfEnabled(this IServiceCollection services, IConfiguration configuration)
    {
        if (configuration.GetValue<bool>(SettingName))
        {
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;

                // Only the address the nearest proxy added is believed. Earlier entries were written by the client.
                options.ForwardLimit = 1;
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();
            });
        }

        return services;
    }

    public static WebApplication UseForwardedHeadersIfEnabled(this WebApplication app)
    {
        if (app.Configuration.GetValue<bool>(SettingName))
        {
            app.UseForwardedHeaders();
        }

        return app;
    }
}
