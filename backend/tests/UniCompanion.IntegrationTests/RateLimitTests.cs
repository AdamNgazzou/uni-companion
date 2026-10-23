using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace UniCompanion.IntegrationTests;

public sealed class RateLimitTests
{
    [Fact]
    public async Task AccountRequestsAreActuallyRateLimited()
    {
        var app = new ApiFixture { AccountRequestsPerMinute = 1 };
        var lifetime = (IAsyncLifetime)app;
        await lifetime.InitializeAsync();
        try
        {
            Assert.Equal(HttpStatusCode.Accepted, (await app.Client.PostAsJsonAsync("/api/v1/accounts/forgot-password", new { email = "missing@example.test" })).StatusCode);
            Assert.Equal(HttpStatusCode.TooManyRequests, (await app.Client.PostAsJsonAsync("/api/v1/accounts/forgot-password", new { email = "missing@example.test" })).StatusCode);
        }
        finally { await lifetime.DisposeAsync(); }
    }
}
