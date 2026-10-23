using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace UniCompanion.Infrastructure.Identity;

public sealed class ActiveSignInManager(
    UserManager<AppUser> users, IHttpContextAccessor accessor,
    IUserClaimsPrincipalFactory<AppUser> factory, IOptions<IdentityOptions> options,
    ILogger<SignInManager<AppUser>> logger, IAuthenticationSchemeProvider schemes,
    IUserConfirmation<AppUser> confirmation)
    : SignInManager<AppUser>(users, accessor, factory, options, logger, schemes, confirmation)
{
    public override async Task<bool> CanSignInAsync(AppUser user) => user.IsActive && await base.CanSignInAsync(user);
}
