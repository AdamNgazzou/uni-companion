using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UniCompanion.Infrastructure.Identity;
using UniCompanion.Infrastructure.Ai;
using UniCompanion.Core.Study;
using UniCompanion.Infrastructure.Persistence;
using UniCompanion.Infrastructure.Storage;
using UniCompanion.Infrastructure.Study;

namespace UniCompanion.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connection = config.GetConnectionString("Database")
            ?? throw new InvalidOperationException("ConnectionStrings:Database is required. See backend/README.md.");
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connection));
        var dataRoot = Path.GetFullPath(config["Storage:Root"] ?? ".local");
        Directory.CreateDirectory(dataRoot);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(dataRoot, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        services.AddDataProtection().SetApplicationName("UniCompanion")
            .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataRoot, "keys")));
        services.AddAuthentication("AppSession").AddPolicyScheme("AppSession", "App session", options =>
        {
            options.ForwardDefaultSelector = context => context.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? IdentityConstants.BearerScheme : "BrowserSession";
        }).AddCookie("BrowserSession", options =>
        {
            options.Cookie.Name = "uni.session";
            options.Cookie.Path = "/api/v1";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.ExpireTimeSpan = TimeSpan.FromDays(14);
            options.SlidingExpiration = true;
            options.Events.OnValidatePrincipal = async context =>
            {
                var signIn = context.HttpContext.RequestServices.GetRequiredService<SignInManager<AppUser>>();
                var user = context.Principal is null ? null : await signIn.ValidateSecurityStampAsync(context.Principal);
                if (user is null || !await signIn.CanSignInAsync(user) || await signIn.UserManager.IsLockedOutAsync(user)) context.RejectPrincipal();
            };
            options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
            options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
        }).AddBearerToken(IdentityConstants.BearerScheme, options =>
        {
            options.BearerTokenExpiration = TimeSpan.FromMinutes(5);
            options.RefreshTokenExpiration = TimeSpan.FromDays(14);
        });
        services.AddIdentityCore<AppUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.SignIn.RequireConfirmedEmail = true;
            options.Password.RequiredLength = 12;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        }).AddRoles<IdentityRole>().AddEntityFrameworkStores<AppDbContext>()
          .AddSignInManager<ActiveSignInManager>().AddDefaultTokenProviders();
        services.AddAuthorization();
        services.Configure<EmailOptions>(config.GetSection("Email"));
        services.AddScoped<IAccountEmail, SmtpAccountEmail>();
        services.AddSingleton<PythonAiBridge>();
        services.AddSingleton<IQueryEmbedding>(sp => sp.GetRequiredService<PythonAiBridge>());
        services.AddSingleton<ITextGeneration>(sp => sp.GetRequiredService<PythonAiBridge>());
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<CourseSearch>();
        services.AddScoped<StudyAccess>();
        services.AddScoped<StudyJobs>();
        services.AddScoped<StudyComposer>();
        services.AddScoped<StudyProcessor>();
        services.AddSingleton(new PrivateFileStore(Path.Combine(dataRoot, "blobs")));
        return services;
    }
}
