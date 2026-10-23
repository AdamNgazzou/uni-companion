using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using UniCompanion.Infrastructure.Identity;
using Xunit;

namespace UniCompanion.IntegrationTests;

public sealed class AccountTests(ApiFixture app) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task ConfirmationIsRequiredAndPasswordsAreHashed()
    {
        var user = await app.RegisterAsync(confirm: false);
        var denied = await app.Client.PostAsJsonAsync("/api/v1/accounts/login", new { email = user.Email, password = ApiFixture.Password });
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        await using var scope = app.Services.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>().FindByEmailAsync(user.Email);
        Assert.NotNull(stored);
        Assert.NotEqual(ApiFixture.Password, stored.PasswordHash);
        Assert.False(stored.EmailConfirmed);
    }

    [Fact]
    public async Task AuthRejectsMissingInvalidAndTamperedTokens()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.Client.GetAsync("/api/v1/accounts/me")).StatusCode);
        var user = await app.RegisterAsync();
        using var valid = app.AsUser(user.Access);
        Assert.Equal(HttpStatusCode.OK, (await valid.GetAsync("/api/v1/accounts/me")).StatusCode);
        using var invalid = app.AsUser(user.Access + "tampered");
        Assert.Equal(HttpStatusCode.Unauthorized, (await invalid.GetAsync("/api/v1/accounts/me")).StatusCode);
        var refresh = await app.Client.PostAsJsonAsync("/api/v1/accounts/refresh", new { refreshToken = "invalid" });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task RefreshWorksAndLogoutAllRevokesAccessAndRefreshImmediately()
    {
        var user = await app.RegisterAsync();
        var refreshed = await app.Client.PostAsJsonAsync("/api/v1/accounts/refresh", new { refreshToken = user.Refresh });
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        using var client = app.AsUser(user.Access);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/v1/accounts/logout-all", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/accounts/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.Client.PostAsJsonAsync("/api/v1/accounts/refresh", new { refreshToken = user.Refresh })).StatusCode);
        var tokens = await app.LoginAsync(user.Email);
        using var current = app.AsUser(tokens.Access);
        Assert.Equal(HttpStatusCode.OK, (await current.GetAsync("/api/v1/accounts/me")).StatusCode);
    }

    [Fact]
    public async Task DisablementRevokesAccessRefreshAndNewLogin()
    {
        var user = await app.RegisterAsync();
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var stored = (await users.FindByEmailAsync(user.Email))!;
            stored.IsActive = false;
            Assert.True((await users.UpdateAsync(stored)).Succeeded);
        }
        using var client = app.AsUser(user.Access);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/accounts/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.Client.PostAsJsonAsync("/api/v1/accounts/refresh", new { refreshToken = user.Refresh })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.Client.PostAsJsonAsync("/api/v1/accounts/login", new { email = user.Email, password = ApiFixture.Password })).StatusCode);
    }

    [Fact]
    public async Task PasswordResetDoesNotEnumerateAndRevokesOldTokens()
    {
        var user = await app.RegisterAsync();
        foreach (var address in new[] { user.Email, "missing@example.test" })
            Assert.Equal(HttpStatusCode.Accepted, (await app.Client.PostAsJsonAsync("/api/v1/accounts/forgot-password", new { email = address })).StatusCode);
        var code = app.Email.Messages[user.Email].Split('\n')[1];
        var reset = await app.Client.PostAsJsonAsync("/api/v1/accounts/reset-password", new { email = user.Email, code, newPassword = "Different-Test-2026!" });
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        using var client = app.AsUser(user.Access);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/accounts/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.Client.PostAsJsonAsync("/api/v1/accounts/refresh", new { refreshToken = user.Refresh })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await app.Client.PostAsJsonAsync("/api/v1/accounts/reset-password", new { email = user.Email, code, newPassword = "Another-Test-2026!" })).StatusCode);
    }

    [Fact]
    public async Task RoleInjectionAndNullPasswordsAreBadRequests()
    {
        var injected = await app.Client.PostAsJsonAsync("/api/v1/accounts/register", new { email = "attacker@example.test", password = ApiFixture.Password, displayName = "Attacker", roles = new[] { "Admin" } });
        Assert.Equal(HttpStatusCode.BadRequest, injected.StatusCode);
        var invalid = await app.Client.PostAsJsonAsync("/api/v1/accounts/login", new { email = "a@example.test", password = (string?)null });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task ExpiredAccessAndRefreshAreRejected()
    {
        var account = await app.RegisterAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var signIn = scope.ServiceProvider.GetRequiredService<SignInManager<AppUser>>();
        var user = (await users.FindByEmailAsync(account.Email))!;
        var principal = await signIn.CreateUserPrincipalAsync(user);
        var options = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<BearerTokenOptions>>().Get(IdentityConstants.BearerScheme);
        var ticket = new AuthenticationTicket(principal, new AuthenticationProperties
        {
            ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(-1)
        }, IdentityConstants.BearerScheme);
        using var expired = app.AsUser(options.BearerTokenProtector.Protect(ticket));
        Assert.Equal(HttpStatusCode.Unauthorized, (await expired.GetAsync("/api/v1/accounts/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.Client.PostAsJsonAsync("/api/v1/accounts/refresh", new { refreshToken = options.RefreshTokenProtector.Protect(ticket) })).StatusCode);
    }

    [Fact]
    public async Task RepeatedBadPasswordsLockTheAccount()
    {
        var account = await app.RegisterAsync();
        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await app.Client.PostAsJsonAsync("/api/v1/accounts/login", new { email = account.Email, password = "Wrong-Test-2026!" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.Client.PostAsJsonAsync("/api/v1/accounts/login", new { email = account.Email, password = ApiFixture.Password })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.Client.PostAsJsonAsync("/api/v1/accounts/refresh", new { refreshToken = account.Refresh })).StatusCode);
    }

    [Fact]
    public async Task DuplicateRegistrationAndUnknownAddressHaveSameAcceptedResponse()
    {
        var user = await app.RegisterAsync();
        var duplicate = await app.Client.PostAsJsonAsync("/api/v1/accounts/register", new { email = user.Email, password = ApiFixture.Password, displayName = "Other" });
        var fresh = await app.Client.PostAsJsonAsync("/api/v1/accounts/register", new { email = Guid.NewGuid() + "@example.test", password = ApiFixture.Password, displayName = "Other" });
        Assert.Equal(HttpStatusCode.Accepted, duplicate.StatusCode);
        Assert.Equal(await fresh.Content.ReadAsStringAsync(), await duplicate.Content.ReadAsStringAsync());
    }
}
