using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UniCompanion.Infrastructure.Persistence;
using Xunit;

namespace UniCompanion.IntegrationTests;

public sealed class BrowserSessionTests(ApiFixture app) : IClassFixture<ApiFixture>
{
    private HttpClient Browser(string? origin = "http://localhost:8081")
    {
        var browser = app.CreateClient();
        if (origin is not null) browser.DefaultRequestHeaders.Add("Origin", origin);
        return browser;
    }
    [Fact]
    public async Task CookieLoginRestoresWithoutReadableTokensAndLogoutClearsIt()
    {
        var account = await app.RegisterAsync(); using var browser = Browser();
        var login = await browser.PostAsJsonAsync("/api/v1/accounts/web/login", new { email = account.Email, password = ApiFixture.Password });
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var cookie = string.Join(';', login.Headers.GetValues("Set-Cookie"));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/v1", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("", await login.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/api/v1/accounts/me")).StatusCode);
        (await browser.PostAsync("/api/v1/accounts/web/logout", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/api/v1/accounts/me")).StatusCode);
    }
    [Theory]
    [InlineData(null)]
    [InlineData("https://evil.example")]
    [InlineData("null")]
    public async Task UntrustedOriginsCannotStartBrowserSessions(string? origin)
    {
        var account = await app.RegisterAsync(); using var browser = Browser(origin);
        Assert.Equal(HttpStatusCode.Forbidden, (await browser.PostAsJsonAsync("/api/v1/accounts/web/login", new { email = account.Email, password = ApiFixture.Password })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/api/v1/accounts/me")).StatusCode);
    }
    [Fact]
    public async Task CookieWritesRequireTrustedOriginAndStampRevocationStillApplies()
    {
        var account = await app.RegisterAsync(); using var browser = Browser();
        (await browser.PostAsJsonAsync("/api/v1/accounts/web/login", new { email = account.Email, password = ApiFixture.Password })).EnsureSuccessStatusCode();
        browser.DefaultRequestHeaders.Remove("Origin");
        Assert.Equal(HttpStatusCode.Forbidden, (await browser.PatchAsJsonAsync("/api/v1/accounts/me", new { displayName = "Unauthorized edit" })).StatusCode);
        browser.DefaultRequestHeaders.Add("Origin", "https://evil.example");
        Assert.Equal(HttpStatusCode.Forbidden, (await browser.PostAsync("/api/v1/accounts/web/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await browser.PatchAsJsonAsync("/api/v1/accounts/me", new { displayName = "Unauthorized edit" })).StatusCode);
        var before = await browser.GetFromJsonAsync<JsonElement>("/api/v1/accounts/me");
        Assert.Equal("Test Student", before.GetProperty("displayName").GetString());
        browser.DefaultRequestHeaders.Remove("Origin"); browser.DefaultRequestHeaders.Add("Origin", "http://localhost:8081");
        (await browser.PatchAsJsonAsync("/api/v1/accounts/me", new { displayName = "Allowed edit" })).EnsureSuccessStatusCode();
        (await browser.PostAsync("/api/v1/accounts/logout-all", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/api/v1/accounts/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.Client.PostAsJsonAsync("/api/v1/accounts/refresh", new { refreshToken = account.Refresh })).StatusCode);
        // A revoked cookie must not lock the browser out of the public sign-in endpoint.
        (await browser.PostAsJsonAsync("/api/v1/accounts/web/login", new { email = account.Email, password = ApiFixture.Password })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/api/v1/accounts/me")).StatusCode);
    }
    [Fact]
    public async Task DisabledAccountsCannotUseOrCreateCookieSessions()
    {
        var account = await app.RegisterAsync(); using var browser = Browser();
        (await browser.PostAsJsonAsync("/api/v1/accounts/web/login", new { email = account.Email, password = ApiFixture.Password })).EnsureSuccessStatusCode();
        await using (var scope = app.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.Where(u => u.Email == account.Email).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, false));
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/api/v1/accounts/me")).StatusCode);
        using var fresh = Browser();
        Assert.Equal(HttpStatusCode.Unauthorized, (await fresh.PostAsJsonAsync("/api/v1/accounts/web/login", new { email = account.Email, password = ApiFixture.Password })).StatusCode);
    }
}
