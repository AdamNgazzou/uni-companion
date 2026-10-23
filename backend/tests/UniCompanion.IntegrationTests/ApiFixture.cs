using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection;
using UniCompanion.Infrastructure.Storage;
using UniCompanion.Core.Study;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using UniCompanion.Infrastructure.Identity;
using UniCompanion.Infrastructure.Persistence;
using Xunit;

namespace UniCompanion.IntegrationTests;

public sealed class TestEmail : IAccountEmail
{
    public ConcurrentDictionary<string, string> Messages { get; } = new();
    public Task SendAsync(string address, string subject, string body, CancellationToken cancellationToken)
    {
        Messages[address] = body;
        return Task.CompletedTask;
    }
}

public sealed class ApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string databaseName = "companion_test_" + Guid.NewGuid().ToString("N");
    private readonly string dataRoot = Path.Combine(Path.GetTempPath(), "companion-tests-" + Guid.NewGuid());
    private string connection = "";
    public TestEmail Email { get; } = new();
    public TestGeneration Generation { get; } = new();
    public int AccountRequestsPerMinute { get; init; } = 10000;
    public HttpClient Client { get; private set; } = null!;
    public const string Password = "Companion-Test-2026!";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Database"] = connection,
            ["Storage:Root"] = dataRoot,
            ["Accounts:RequestsPerMinute"] = AccountRequestsPerMinute.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Logging:LogLevel:Default"] = "Warning"
        }));
        builder.ConfigureServices(services =>
        {
            // Minimal hosting config callbacks can run after production service registration.
            // Replace captured runtime resources explicitly, and assert the DB before migrating.
            services.RemoveAll<AppDbContext>();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connection));
            services.RemoveAll<PrivateFileStore>();
            services.AddSingleton(new PrivateFileStore(Path.Combine(dataRoot, "blobs")));
            services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataRoot, "keys")));
            services.RemoveAll<IQueryEmbedding>();
            services.AddSingleton<IQueryEmbedding, FakeEmbedding>();
            services.RemoveAll<ITextGeneration>();
            services.AddSingleton<ITextGeneration>(Generation);
            services.RemoveAll<IAccountEmail>();
            services.AddSingleton<IAccountEmail>(Email);
        });
    }

    async Task IAsyncLifetime.InitializeAsync()
    {
        var original = Environment.GetEnvironmentVariable("ConnectionStrings__Database")
            ?? throw new InvalidOperationException("Run tests using python3 backend/scripts/dev.py test (real PostgreSQL required).");
        var cs = new NpgsqlConnectionStringBuilder(original) { Database = "postgres" };
        await using var admin = new NpgsqlConnection(cs.ConnectionString);
        await admin.OpenAsync();
        await using (var command = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin)) await command.ExecuteNonQueryAsync();
        cs.Database = databaseName;
        connection = cs.ConnectionString;
        Client = CreateClient();
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(databaseName, db.Database.GetDbConnection().Database);
        await db.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        var cs = new NpgsqlConnectionStringBuilder(connection) { Database = "postgres" };
        await using var admin = new NpgsqlConnection(cs.ConnectionString);
        await admin.OpenAsync();
        if (!databaseName.StartsWith("companion_test_", StringComparison.Ordinal)) throw new InvalidOperationException();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", admin);
        await command.ExecuteNonQueryAsync();
        if (Directory.Exists(dataRoot)) Directory.Delete(dataRoot, recursive: true);
    }

    public async Task<(string Email, string Access, string Refresh)> RegisterAsync(bool confirm = true)
    {
        var address = Guid.NewGuid().ToString("N") + "@example.test";
        var response = await Client.PostAsJsonAsync("/api/v1/accounts/register", new { email = address, password = Password, displayName = "Test Student" });
        response.EnsureSuccessStatusCode();
        if (!confirm) return (address, "", "");
        var link = Email.Messages[address].Split('\n').Single(line => line.StartsWith("http", StringComparison.Ordinal));
        (await Client.GetAsync(new Uri(link).PathAndQuery)).EnsureSuccessStatusCode();
        var tokens = await LoginAsync(address);
        return (address, tokens.Access, tokens.Refresh);
    }

    public async Task<(string Access, string Refresh)> LoginAsync(string email)
    {
        var response = await Client.PostAsJsonAsync("/api/v1/accounts/login", new { email, password = Password });
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (json.GetProperty("accessToken").GetString()!, json.GetProperty("refreshToken").GetString()!);
    }

    public HttpClient AsUser(string access)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);
        return client;
    }
}
