using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using UniCompanion.Infrastructure.Identity;

namespace UniCompanion.Api.Features.Accounts;

public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccounts(this IEndpointRouteBuilder endpoints)
    {
        var accounts = endpoints.MapGroup("/api/v1/accounts").WithTags("Accounts");
        accounts.MapPost("/register", RegisterAsync).RequireRateLimiting("accounts");
        accounts.MapPost("/login", LoginAsync).RequireRateLimiting("accounts");
        accounts.MapPost("/refresh", RefreshAsync).RequireRateLimiting("accounts");
        accounts.MapGet("/confirm-email", ConfirmEmailAsync).RequireRateLimiting("accounts");
        accounts.MapPost("/resend-confirmation", ResendAsync).RequireRateLimiting("accounts");
        accounts.MapPost("/forgot-password", ForgotPasswordAsync).RequireRateLimiting("accounts");
        accounts.MapPost("/reset-password", ResetPasswordAsync).RequireRateLimiting("accounts");
        accounts.MapPost("/change-password", ChangePasswordAsync).RequireAuthorization().RequireRateLimiting("accounts");
        accounts.MapGet("/me", async (HttpContext context, UserManager<AppUser> users) =>
        {
            var user = (await users.GetUserAsync(context.User))!;
            return Results.Ok(new { user.Id, user.Email, user.DisplayName, user.University, roles = await users.GetRolesAsync(user) });
        }).RequireAuthorization();
        accounts.MapPatch("/me", async (ProfileRequest request, HttpContext context, UserManager<AppUser> users) =>
        {
            if (string.IsNullOrWhiteSpace(request.DisplayName) || request.DisplayName.Trim().Length > 80) return Results.BadRequest();
            var user = (await users.GetUserAsync(context.User))!;
            user.DisplayName = request.DisplayName.Trim();
            return (await users.UpdateAsync(user)).Succeeded ? Results.NoContent() : Results.Problem(statusCode: 409);
        }).RequireAuthorization();
        accounts.MapPost("/logout-all", async (HttpContext context, UserManager<AppUser> users) =>
        {
            var result = await users.UpdateSecurityStampAsync((await users.GetUserAsync(context.User))!);
            return result.Succeeded ? Results.NoContent() : Results.Problem(statusCode: 409);
        }).RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> RegisterAsync(RegisterRequest request, UserManager<AppUser> users,
        IAccountEmail email, IConfiguration config, CancellationToken cancellationToken)
    {
        if (!new EmailAddressAttribute().IsValid(request.Email) || request.Email.Length > 254 ||
            string.IsNullOrWhiteSpace(request.DisplayName) || request.DisplayName.Trim().Length > 80 || request.Password.Length > 128)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["registration"] = ["Provide a valid email, name (1–80 characters), and password (12–128 characters)."] });
        var candidate = new AppUser { UserName = request.Email.Trim(), Email = request.Email.Trim(), DisplayName = request.DisplayName.Trim() };
        var passwordErrors = new List<IdentityError>();
        foreach (var validator in users.PasswordValidators)
        {
            var validation = await validator.ValidateAsync(users, candidate, request.Password);
            passwordErrors.AddRange(validation.Errors);
        }
        if (passwordErrors.Count > 0) return Errors(passwordErrors);
        // The same accepted response is returned for an existing address; never return a user's record/token.
        if (await users.FindByEmailAsync(candidate.Email) is not null) return Accepted();
        var result = await users.CreateAsync(candidate, request.Password);
        if (!result.Succeeded)
        {
            if (result.Errors.Any(e => e.Code is "DuplicateEmail" or "DuplicateUserName")) return Accepted();
            return Errors(result.Errors);
        }
        await SendConfirmationAsync(candidate, users, email, config, cancellationToken);
        return Accepted();
    }

    private static async Task<IResult> LoginAsync(LoginRequest request, SignInManager<AppUser> signIn)
    {
        if (request.Email.Length > 254 || request.Password.Length > 128) return Results.Unauthorized();
        signIn.AuthenticationScheme = IdentityConstants.BearerScheme;
        var result = await signIn.PasswordSignInAsync(request.Email.Trim(), request.Password, isPersistent: false, lockoutOnFailure: true);
        return result.Succeeded ? Results.Empty : Results.Problem(statusCode: 401, title: "Unable to sign in.");
    }

    private static async Task<IResult> RefreshAsync(RefreshRequest request,
        IOptionsMonitor<BearerTokenOptions> options, SignInManager<AppUser> signIn, TimeProvider clock)
    {
        if (request.RefreshToken.Length > 16_384) return Results.Unauthorized();
        var ticket = options.Get(IdentityConstants.BearerScheme).RefreshTokenProtector.Unprotect(request.RefreshToken);
        if (ticket?.Properties.ExpiresUtc is not { } expires || expires <= clock.GetUtcNow() ||
            await signIn.ValidateSecurityStampAsync(ticket.Principal) is not { IsActive: true } user ||
            !await signIn.CanSignInAsync(user) || await signIn.UserManager.IsLockedOutAsync(user))
            return Results.Unauthorized();
        return Results.SignIn(await signIn.CreateUserPrincipalAsync(user), authenticationScheme: IdentityConstants.BearerScheme);
    }

    private static async Task<IResult> ConfirmEmailAsync(string userId, string code, UserManager<AppUser> users)
    {
        if (await users.FindByIdAsync(userId) is not { IsActive: true } user || !TryDecode(code, out var decoded))
            return Results.BadRequest(new { message = "Invalid or expired confirmation link." });
        var result = await users.ConfirmEmailAsync(user, decoded);
        return result.Succeeded ? Results.Ok(new { message = "Email confirmed. You can sign in." }) : Results.BadRequest(new { message = "Invalid or expired confirmation link." });
    }

    private static async Task<IResult> ResendAsync(EmailRequest request, UserManager<AppUser> users,
        IAccountEmail email, IConfiguration config, CancellationToken cancellationToken)
    {
        if (await users.FindByEmailAsync(request.Email) is { IsActive: true, EmailConfirmed: false } user)
            await SendConfirmationAsync(user, users, email, config, cancellationToken);
        return Accepted();
    }

    private static async Task<IResult> ForgotPasswordAsync(EmailRequest request, UserManager<AppUser> users,
        IAccountEmail email, CancellationToken cancellationToken)
    {
        if (await users.FindByEmailAsync(request.Email) is { IsActive: true, EmailConfirmed: true } user)
        {
            var code = Encode(await users.GeneratePasswordResetTokenAsync(user));
            await email.SendAsync(user.Email!, "Reset your Uni Companion password", $"Your password reset code is:\n{code}\n\nIf you did not request this, ignore this email.", cancellationToken);
        }
        return Accepted();
    }

    private static async Task<IResult> ResetPasswordAsync(ResetPasswordRequest request, UserManager<AppUser> users)
    {
        if (request.NewPassword.Length > 128 || !TryDecode(request.Code, out var code) ||
            await users.FindByEmailAsync(request.Email) is not { IsActive: true, EmailConfirmed: true } user)
            return InvalidReset();
        var result = await users.ResetPasswordAsync(user, code, request.NewPassword);
        return result.Succeeded ? Results.NoContent() : InvalidReset();
    }

    private static async Task<IResult> ChangePasswordAsync(ChangePasswordRequest request, HttpContext context, UserManager<AppUser> users)
    {
        if (request.NewPassword.Length > 128 || request.CurrentPassword.Length > 128) return Results.BadRequest();
        var result = await users.ChangePasswordAsync((await users.GetUserAsync(context.User))!, request.CurrentPassword, request.NewPassword);
        return result.Succeeded ? Results.NoContent() : Errors(result.Errors);
    }

    private static async Task SendConfirmationAsync(AppUser user, UserManager<AppUser> users, IAccountEmail email,
        IConfiguration config, CancellationToken cancellationToken)
    {
        var baseUrl = config["Accounts:PublicBaseUrl"] ?? throw new InvalidOperationException("Public URL is required.");
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
            throw new InvalidOperationException("Accounts:PublicBaseUrl must be an absolute HTTP(S) URL.");
        var link = QueryHelpers.AddQueryString(baseUrl.TrimEnd('/') + "/api/v1/accounts/confirm-email", new Dictionary<string, string?>
        {
            ["userId"] = user.Id,
            ["code"] = Encode(await users.GenerateEmailConfirmationTokenAsync(user))
        });
        await email.SendAsync(user.Email!, "Confirm your Uni Companion account", $"Confirm your email:\n{link}\n\nIf you did not register, ignore this email.", cancellationToken);
    }

    private static IResult Accepted() => Results.Accepted(value: new { message = "If this address is eligible, an email will arrive shortly." });
    private static IResult InvalidReset() => Results.BadRequest(new { message = "Invalid reset request or password." });
    private static IResult Errors(IEnumerable<IdentityError> errors) => Results.ValidationProblem(errors.GroupBy(e => e.Code).ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray()));
    private static string Encode(string value) => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(value));
    private static bool TryDecode(string code, out string decoded)
    {
        decoded = "";
        if (code.Length > 4096) return false;
        try { decoded = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code)); return true; }
        catch (FormatException) { return false; }
    }

    public sealed record ProfileRequest([Required] string DisplayName);
    public sealed record RegisterRequest([Required] string Email, [Required] string Password, [Required] string DisplayName);
    public sealed record LoginRequest([Required] string Email, [Required] string Password);
    public sealed record RefreshRequest([Required] string RefreshToken);
    public sealed record EmailRequest([Required] string Email);
    public sealed record ResetPasswordRequest([Required] string Email, [Required] string Code, [Required] string NewPassword);
    public sealed record ChangePasswordRequest([Required] string CurrentPassword, [Required] string NewPassword);
}
