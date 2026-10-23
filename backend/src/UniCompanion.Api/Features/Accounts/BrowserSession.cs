using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using UniCompanion.Infrastructure.Identity;

namespace UniCompanion.Api.Features.Accounts;

public static class BrowserSession
{
    public const string Scheme = "BrowserSession";
    public const string CookieName = "uni.session";

    public static bool OriginAllowed(HttpContext context, IConfiguration config, IHostEnvironment environment)
    {
        var origin = context.Request.Headers.Origin.ToString();
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.AbsolutePath != "/" || uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0)
            return false;
        var configured = config["Accounts:WebOrigin"] ?? config["Accounts:PublicBaseUrl"];
        if (Uri.TryCreate(configured, UriKind.Absolute, out var expected) && uri.GetLeftPart(UriPartial.Authority) == expected.GetLeftPart(UriPartial.Authority)) return true;
        return (environment.IsDevelopment() || environment.IsEnvironment("Testing")) &&
            (origin == "http://localhost:8081" || origin == "http://127.0.0.1:8081");
    }

    public static IEndpointRouteBuilder MapBrowserSession(this IEndpointRouteBuilder endpoints)
    {
        var routes = endpoints.MapGroup("/api/v1/accounts/web").RequireRateLimiting("accounts");
        routes.MapPost("/login", async (AccountEndpoints.LoginRequest request, HttpContext context,
            SignInManager<AppUser> signIn, IConfiguration config, IHostEnvironment environment) =>
        {
            if (!OriginAllowed(context, config, environment)) return Results.StatusCode(403);
            if (request.Email.Length > 254 || request.Password.Length > 128) return Results.Unauthorized();
            signIn.AuthenticationScheme = Scheme;
            var result = await signIn.PasswordSignInAsync(request.Email.Trim(), request.Password, isPersistent: true, lockoutOnFailure: true);
            return result.Succeeded ? Results.NoContent() : Results.Unauthorized();
        });
        routes.MapPost("/logout", async (HttpContext context, IConfiguration config, IHostEnvironment environment) =>
        {
            if (!OriginAllowed(context, config, environment)) return Results.StatusCode(403);
            await context.SignOutAsync(Scheme);
            return Results.NoContent();
        });
        return endpoints;
    }
}
