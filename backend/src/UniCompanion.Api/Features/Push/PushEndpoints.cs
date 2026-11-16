using System.Security.Claims;
using UniCompanion.Infrastructure.Push;

namespace UniCompanion.Api.Features.Push;

public static class PushEndpoints
{
    public static IEndpointRouteBuilder MapPush(this IEndpointRouteBuilder endpoints)
    {
        var routes = endpoints.MapGroup("/api/v1/devices/push").WithTags("Phone notifications");
        routes.MapGet("/status", (PushConfiguration configuration) => Results.Ok(new { available = configuration.Enabled, projectId = configuration.Enabled ? configuration.ProjectId : null })).RequireAuthorization();
        routes.MapPost("/initialize", async (InstallationProof proof, HttpContext context, PushDevices devices, CancellationToken ct) =>
        { await devices.InitializeAsync(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!, proof, ct); return Results.NoContent(); }).RequireAuthorization().RequireRateLimiting("community");
        routes.MapPost("/register", async (DeviceRegistration request, HttpContext context, PushDevices devices, CancellationToken ct) =>
        { await devices.RegisterAsync(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!, request, ct); return Results.NoContent(); }).RequireAuthorization().RequireRateLimiting("community");
        routes.MapPost("/revoke", async (InstallationProof proof, PushDevices devices, CancellationToken ct) =>
        { await devices.RevokeAsync(proof, ct); return Results.NoContent(); }).AllowAnonymous().RequireRateLimiting("accounts");
        return endpoints;
    }
}
