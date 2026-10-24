using UniCompanion.Api.Features.Community;
using UniCompanion.Api.Features.Activity;
using UniCompanion.Api;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using UniCompanion.Api.Features.Accounts;
using UniCompanion.Api.Features.Courses;
using UniCompanion.Api.Features.Study;
using UniCompanion.Infrastructure;
using UniCompanion.Infrastructure.Identity;
using UniCompanion.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddInfrastructure(builder.Configuration);
if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"))
    builder.Services.Configure<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions>(BrowserSession.Scheme,
        options => options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<SafeExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddValidation();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 1_048_576);
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("community", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous", _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("accounts", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = builder.Configuration.GetValue("Accounts:RequestsPerMinute", 20),
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});
var app = builder.Build();
if (args.Contains("--migrate"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    return;
}
if (Array.IndexOf(args, "--import") is var importIndex && importIndex >= 0)
{
    if (importIndex + 1 >= args.Length) throw new InvalidOperationException("Supply a catalogue path after --import.");
    await using var scope = app.Services.CreateAsyncScope();
    var importer = ActivatorUtilities.CreateInstance<UniCompanion.Infrastructure.Courses.CatalogueImporter>(scope.ServiceProvider);
    var result = await importer.ImportAsync(args[importIndex + 1]);
    Console.WriteLine($"Imported {result.Courses} courses and {result.Materials} materials.");
    return;
}
if (args.Contains("--index"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var indexer = ActivatorUtilities.CreateInstance<UniCompanion.Infrastructure.Ai.CorpusIndexer>(scope.ServiceProvider);
    var result = await indexer.IndexAsync();
    Console.WriteLine($"Indexed {result.Documents} documents and {result.Chunks} chunks.");
    return;
}
if (Array.IndexOf(args, "--evaluate") is var evaluationIndex && evaluationIndex >= 0)
{
    if (evaluationIndex + 1 >= args.Length) throw new InvalidOperationException("Supply an evaluation file after --evaluate.");
    await using var scope = app.Services.CreateAsyncScope();
    var evaluator = ActivatorUtilities.CreateInstance<UniCompanion.Infrastructure.Ai.RetrievalEvaluator>(scope.ServiceProvider);
    if (!await evaluator.EvaluateAsync(args[evaluationIndex + 1])) Environment.ExitCode = 1;
    return;
}
if (Array.IndexOf(args, "--evaluate-study") is var studyIndex && studyIndex >= 0)
{
    if (!app.Environment.IsDevelopment()) throw new InvalidOperationException("Study evaluation is local development only.");
    if (studyIndex + 2 >= args.Length) throw new InvalidOperationException("Supply manifest and private report paths.");
    await using var scope = app.Services.CreateAsyncScope();
    var evaluator = ActivatorUtilities.CreateInstance<UniCompanion.Infrastructure.Study.StudyEvaluator>(scope.ServiceProvider);
    await evaluator.RunAsync(args[studyIndex + 1], args[studyIndex + 2], CancellationToken.None, studyIndex + 3 < args.Length ? args[studyIndex + 3].Split(',') : null);
    return;
}
if (Array.IndexOf(args, "--evaluate-practice") is var practiceIndex && practiceIndex >= 0)
{
    if (practiceIndex + 2 >= args.Length) throw new InvalidOperationException("Supply manifest and private output paths.");
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<UniCompanion.Infrastructure.Practice.PracticeEvaluator>().RunAsync(args[practiceIndex + 1], args[practiceIndex + 2], CancellationToken.None, practiceIndex + 3 < args.Length ? args[practiceIndex + 3].Split(',') : null);
    return;
}

if (args.Contains("--seed-community"))
{
    if (!app.Environment.IsDevelopment()) throw new InvalidOperationException("Community demo seeding is development-only.");
    await using var scope = app.Services.CreateAsyncScope();
    var seeder = ActivatorUtilities.CreateInstance<UniCompanion.Infrastructure.Community.CommunitySeeder>(scope.ServiceProvider);
    var seeded = await seeder.SeedAsync();
    Console.WriteLine($"Community sample: {seeded.Users} disabled fictional users, {seeded.Posts} threads, {seeded.Replies} replies, {seeded.Votes} votes. No real users were changed.");
    return;
}
if (args.Contains("--bootstrap-admin"))
{
    await AdminBootstrap.RunAsync(app.Services, builder.Configuration);
    return;
}
app.UseExceptionHandler();
app.UseStatusCodePages();
if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing")) app.UseHttpsRedirection();
app.UseAuthentication();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Request-Id"] = context.TraceIdentifier;
    context.Response.Headers.CacheControl = "no-store";
    if (context.User.Identity?.IsAuthenticated == true)
    {
        var bearer = context.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);
        if (!bearer && !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method) && !HttpMethods.IsOptions(context.Request.Method)
            && !BrowserSession.OriginAllowed(context, builder.Configuration, app.Environment))
        {
            context.Response.StatusCode = 403;
            return;
        }
        var signIn = context.RequestServices.GetRequiredService<SignInManager<AppUser>>();
        var user = await signIn.ValidateSecurityStampAsync(context.User);
        if (user is null || !await signIn.CanSignInAsync(user) || await signIn.UserManager.IsLockedOutAsync(user))
        {
            context.Response.StatusCode = 401;
            return;
        }
    }
    await next(context);
});
app.UseAuthorization();
app.UseRateLimiter();
app.MapGet("/health/live", () => Results.Ok(new { status = "alive" })).ExcludeFromDescription();
app.MapGet("/health/ready", async (AppDbContext db, CancellationToken cancellationToken) =>
    await db.Database.CanConnectAsync(cancellationToken) ? Results.Ok(new { status = "ready" }) : Results.StatusCode(503)).ExcludeFromDescription();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.MapAccounts();
app.MapBrowserSession();
app.MapCourses();
app.MapCommunity();
app.MapActivity();
UniCompanion.Api.Features.Practice.PracticeEndpoints.MapPractice(app);
UniCompanion.Api.Features.Tutor.TutorEndpoints.MapTutor(app);
app.MapStudy();
await app.RunAsync();

public partial class Program;
