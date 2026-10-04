using LedgerPay.Api.Errors;
using LedgerPay.Api.Extensions;
using LedgerPay.Api.Handlers;
using LedgerPay.Api.Middleware;
using LedgerPay.Application;
using LedgerPay.Infrastructure;
using LedgerPay.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Api");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:Api is not set. Use the limited API login, never sa and never the migration login.");
}

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

builder.Services
    .AddInfrastructure(connectionString)
    .AddJwtTokens(jwt)
    .AddApplication()
    .AddJwtAuthentication(jwt)
    .AddFrontendCors(builder.Configuration)
    .AddApiControllers();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseExceptionHandler(_ => { });
app.UseStatusCodePages(context =>
{
    var code = Problems.CodeForStatus(context.HttpContext.Response.StatusCode);
    return code is null ? Task.CompletedTask : Problems.WriteAsync(context.HttpContext, code);
});
app.UseRouting();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Public so the integration tests can start the whole app in memory.
public partial class Program;
