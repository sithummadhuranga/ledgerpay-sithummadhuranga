using System.Net;
using System.Text.Json;
using LedgerPay.IntegrationTests.Support;

namespace LedgerPay.IntegrationTests.Api;

public class OpenApiDocumentTests(SqlServerFixture sql)
{
    private const string Url = "/openapi/v1.json";

    private static readonly string[] Operations =
    [
        "post /api/v1/auth/register",
        "post /api/v1/auth/login",
        "post /api/v1/auth/refresh",
        "post /api/v1/auth/logout",
        "get /api/v1/auth/sessions",
        "delete /api/v1/auth/sessions/{id}",
        "get /api/v1/wallets/me",
        "get /api/v1/wallets/lookup",
        "get /api/v1/wallets/me/transactions",
        "get /api/v1/transfers/quote",
        "post /api/v1/transfers",
        "get /api/v1/transactions/{reference}",
        "post /api/v1/admin/topups",
        "patch /api/v1/admin/wallets/{walletNumber}/status",
        "get /api/v1/admin/users",
        "get /api/v1/admin/users/{walletNumber}",
        "get /api/v1/admin/transactions",
        "get /api/v1/admin/audit-logs",
        "get /api/v1/admin/staff",
        "patch /api/v1/admin/staff/restriction",
        "get /api/v1/health"
    ];

    private async Task<JsonDocument> DocumentAsync()
    {
        var response = await sql.Api.CreateClient().GetAsync(Url, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ApiCalls.ReadAsync(response);
    }

    private static JsonElement Operation(JsonDocument document, string key)
    {
        var parts = key.Split(' ');
        return document.RootElement.GetProperty("paths").GetProperty(parts[1]).GetProperty(parts[0]);
    }

    [Fact]
    public async Task The_document_is_public_and_describes_the_api()
    {
        using var document = await DocumentAsync();

        Assert.StartsWith("3.", document.RootElement.GetProperty("openapi").GetString());
        var info = document.RootElement.GetProperty("info");
        Assert.Equal("LedgerPay API", info.GetProperty("title").GetString());
        Assert.Equal("v1", info.GetProperty("version").GetString());
        Assert.False(string.IsNullOrWhiteSpace(info.GetProperty("description").GetString()));
    }

    [Fact]
    public async Task Every_route_of_the_api_is_in_the_document_and_nothing_else_is()
    {
        using var document = await DocumentAsync();

        var found = document.RootElement.GetProperty("paths").EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject().Select(operation => $"{operation.Name} {path.Name}"))
            .Order()
            .ToList();

        Assert.Equal(Operations.Order(), found);
    }

    [Fact]
    public async Task Every_operation_has_a_summary_and_at_least_one_response()
    {
        using var document = await DocumentAsync();

        foreach (var key in Operations)
        {
            var operation = Operation(document, key);
            Assert.True(operation.TryGetProperty("summary", out var summary) && !string.IsNullOrWhiteSpace(summary.GetString()), $"{key} has no summary");
            Assert.True(operation.GetProperty("responses").EnumerateObject().Any(), $"{key} has no responses");
        }
    }

    [Fact]
    public async Task Sign_in_is_described_as_a_bearer_token_and_asked_for_by_the_protected_routes_only()
    {
        using var document = await DocumentAsync();

        var scheme = document.RootElement.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
        Assert.Equal("http", scheme.GetProperty("type").GetString());
        Assert.Equal("bearer", scheme.GetProperty("scheme").GetString());
        Assert.Equal("JWT", scheme.GetProperty("bearerFormat").GetString());
        string[] open = ["post /api/v1/auth/register", "post /api/v1/auth/login", "post /api/v1/auth/refresh", "post /api/v1/auth/logout", "get /api/v1/health"];
        foreach (var key in Operations)
        {
            var hasSecurity = Operation(document, key).TryGetProperty("security", out var security) && security.GetArrayLength() > 0;
            Assert.Equal(!open.Contains(key), hasSecurity);
        }
    }

    [Fact]
    public async Task The_protected_routes_document_401_and_the_role_gated_ones_also_403()
    {
        using var document = await DocumentAsync();

        string[] gated =
        [
            "get /api/v1/wallets/me", "post /api/v1/transfers", "post /api/v1/admin/topups", "patch /api/v1/admin/wallets/{walletNumber}/status",
            "get /api/v1/admin/users", "get /api/v1/admin/users/{walletNumber}", "get /api/v1/admin/transactions", "get /api/v1/admin/audit-logs",
            "get /api/v1/admin/staff", "patch /api/v1/admin/staff/restriction"
        ];
        Assert.True(Operation(document, "get /api/v1/transactions/{reference}").GetProperty("responses").TryGetProperty("401", out _));
        foreach (var key in gated)
        {
            var responses = Operation(document, key).GetProperty("responses");
            Assert.True(responses.TryGetProperty("401", out _), $"{key} has no 401");
            Assert.True(responses.TryGetProperty("403", out _), $"{key} has no 403");
        }

        Assert.False(Operation(document, "post /api/v1/auth/login").GetProperty("responses").TryGetProperty("403", out _));
    }

    [Fact]
    public async Task The_limited_routes_document_429()
    {
        using var document = await DocumentAsync();

        string[] limited =
        [
            "post /api/v1/auth/register", "post /api/v1/auth/login", "post /api/v1/auth/refresh", "post /api/v1/auth/logout",
            "get /api/v1/auth/sessions", "delete /api/v1/auth/sessions/{id}", "get /api/v1/wallets/lookup",
            "get /api/v1/transfers/quote", "post /api/v1/transfers", "post /api/v1/admin/topups",
            "get /api/v1/admin/users", "get /api/v1/admin/users/{walletNumber}", "get /api/v1/admin/transactions", "get /api/v1/admin/audit-logs",
            "get /api/v1/admin/staff", "patch /api/v1/admin/staff/restriction"
        ];
        foreach (var key in Operations)
        {
            var has429 = Operation(document, key).GetProperty("responses").TryGetProperty("429", out _);
            Assert.Equal(limited.Contains(key), has429);
        }
    }

    [Theory]
    [InlineData("post /api/v1/transfers", true)]
    [InlineData("post /api/v1/admin/topups", true)]
    [InlineData("patch /api/v1/admin/wallets/{walletNumber}/status", false)]
    [InlineData("get /api/v1/transfers/quote", false)]
    [InlineData("post /api/v1/auth/login", false)]
    public async Task The_idempotency_key_header_is_listed_as_required_on_the_two_money_posts_only(string key, bool expected)
    {
        using var document = await DocumentAsync();

        var operation = Operation(document, key);
        var header = operation.TryGetProperty("parameters", out var parameters)
            ? parameters.EnumerateArray().FirstOrDefault(parameter => parameter.GetProperty("name").GetString() == "Idempotency-Key")
            : default;

        Assert.Equal(expected, header.ValueKind == JsonValueKind.Object);
        if (expected)
        {
            Assert.Equal("header", header.GetProperty("in").GetString());
            Assert.True(header.GetProperty("required").GetBoolean());
        }
    }

    [Fact]
    public async Task Answers_are_described_as_json_only_and_the_document_names_no_server()
    {
        using var document = await DocumentAsync();

        var ok = Operation(document, "post /api/v1/transfers").GetProperty("responses").GetProperty("201").GetProperty("content");
        Assert.Equal(["application/json"], ok.EnumerateObject().Select(content => content.Name));
        Assert.False(document.RootElement.TryGetProperty("servers", out var servers) && servers.GetArrayLength() > 0);
    }

    [Fact]
    public async Task Enums_are_listed_by_name_as_the_api_sends_them()
    {
        using var document = await DocumentAsync();

        var text = document.RootElement.GetProperty("components").GetProperty("schemas").GetRawText();

        Assert.Contains("\"Frozen\"", text);
        Assert.Contains("\"Received\"", text);
        Assert.Contains("\"TopUp\"", text);
    }

    [Fact]
    public async Task The_swagger_page_is_public_and_points_at_the_document()
    {
        var client = sql.Api.CreateClient();

        var page = await client.GetAsync("/swagger/index.html", TestContext.Current.CancellationToken);
        var settings = await client.GetAsync("/swagger/index.js", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Equal(HttpStatusCode.OK, settings.StatusCode);
        Assert.Contains("/openapi/v1.json", await settings.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task The_exported_file_in_the_docs_folder_is_the_document_the_api_serves()
    {
        var response = await sql.Api.CreateClient().GetAsync(Url, TestContext.Current.CancellationToken);
        var served = Pretty(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var file = Path.Combine(RepositoryRoot(), "docs", "openapi.json");

        // To refresh the file after a change to the API: UPDATE_OPENAPI=1 dotnet test --project tests/LedgerPay.IntegrationTests
        if (Environment.GetEnvironmentVariable("UPDATE_OPENAPI") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            await File.WriteAllTextAsync(file, served, TestContext.Current.CancellationToken);
        }

        Assert.True(File.Exists(file), "docs/openapi.json is missing. Run the tests once with UPDATE_OPENAPI=1.");
        Assert.Equal(served, await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken));
    }

    private static string Pretty(string json)
    {
        using var document = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true }).ReplaceLineEndings("\n") + "\n";
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "backend", "LedgerPay.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found.");
    }
}
