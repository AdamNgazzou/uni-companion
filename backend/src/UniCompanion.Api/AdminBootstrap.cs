using Microsoft.AspNetCore.Identity;
using UniCompanion.Infrastructure.Identity;

internal static class AdminBootstrap
{
    internal static async Task RunAsync(IServiceProvider services, IConfiguration config)
    {
        var address = config["Bootstrap:Email"] ?? throw new InvalidOperationException("Bootstrap__Email is required; register and confirm this account first.");
        await using var scope = services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var user = await users.FindByEmailAsync(address);
        if (user is not { IsActive: true, EmailConfirmed: true })
            throw new InvalidOperationException("Admin must be an existing active, confirmed account.");
        if (!await roles.RoleExistsAsync("Admin")) Check(await roles.CreateAsync(new IdentityRole("Admin")));
        if (!await users.IsInRoleAsync(user, "Admin")) Check(await users.AddToRoleAsync(user, "Admin"));
        Check(await users.UpdateSecurityStampAsync(user));
        Console.WriteLine("Admin role assigned. Sign in again to obtain current claims.");
    }

    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException("Admin bootstrap failed.");
    }
}
