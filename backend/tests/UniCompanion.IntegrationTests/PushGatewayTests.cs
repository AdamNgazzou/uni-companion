using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using UniCompanion.Infrastructure.Push;
using Xunit;

namespace UniCompanion.IntegrationTests;

public sealed class PushGatewayTests
{
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> reply) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => reply(request);
    }
    private static HttpResponseMessage Response(string text, HttpStatusCode status = HttpStatusCode.OK) => new(status) { Content = new StringContent(text, Encoding.UTF8, "application/json") };
    [Fact]
    public async Task OfficialGatewaySendsOnlyGenericPreviewAndOpaqueActivityIdThenReadsReceipt()
    {
        var activity = Guid.NewGuid(); var calls = 0;
        using var client = new HttpClient(new Handler(async request =>
        {
            Assert.Equal("https", request.RequestUri!.Scheme); Assert.Equal("exp.host", request.RequestUri.Host); Assert.Equal("Bearer", request.Headers.Authorization!.Scheme); Assert.Equal("fixture-only", request.Headers.Authorization.Parameter);
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            if (++calls == 1)
            {
                Assert.Equal("/--/api/v2/push/send", request.RequestUri.AbsolutePath);
                Assert.Equal("Uni Companion", body.RootElement.GetProperty("title").GetString()); Assert.Equal("You have a new activity update.", body.RootElement.GetProperty("body").GetString());
                var data = body.RootElement.GetProperty("data"); Assert.Single(data.EnumerateObject()); Assert.Equal(activity, data.GetProperty("activityId").GetGuid());
                return Response("""{"data":{"status":"ok","id":"ticket"}}""");
            }
            Assert.Equal("/--/api/v2/push/getReceipts", request.RequestUri.AbsolutePath); Assert.Equal("ticket", body.RootElement.GetProperty("ids")[0].GetString());
            return Response("""{"data":{"ticket":{"status":"error","details":{"error":"DeviceNotRegistered"}}}}""");
        }));
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Push:AccessToken"] = "fixture-only" }).Build(); var gateway = new ExpoPushGateway(client, config);
        Assert.Equal("accepted", (await gateway.SendAsync("ExpoPushToken[fixture_only_123456789]", activity, CancellationToken.None)).Status);
        Assert.Equal("DeviceNotRegistered", (await gateway.ReceiptAsync("ticket", CancellationToken.None)).ErrorCode);
    }
    [Fact]
    public async Task RateLimitIsRetryableAndOversizedResponseIsBounded()
    {
        var config = new ConfigurationBuilder().Build();
        using var rate = new HttpClient(new Handler(_ => Task.FromResult(Response("{}", HttpStatusCode.TooManyRequests))));
        var error = await Assert.ThrowsAsync<PushTransportException>(() => new ExpoPushGateway(rate, config).SendAsync("token", Guid.NewGuid(), CancellationToken.None)); Assert.True(error.Retryable);
        using var huge = new HttpClient(new Handler(_ => Task.FromResult(Response(new string('x', 40000)))));
        var bound = await Assert.ThrowsAsync<PushTransportException>(() => new ExpoPushGateway(huge, config).SendAsync("token", Guid.NewGuid(), CancellationToken.None)); Assert.False(bound.Retryable);
    }
}
