using Microsoft.AspNetCore.Identity;

namespace UniCompanion.Infrastructure.Identity;

public sealed class AppUser : IdentityUser
{
    public string DisplayName { get; set; } = "Student";
    public bool IsActive { get; set; } = true;
    public string University { get; private set; } = "Horizon University";
}
