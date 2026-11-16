using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using UniCompanion.Core.Push;

namespace UniCompanion.Infrastructure.Push;

public sealed class ExpoPushGateway(HttpClient client, IConfiguration configuration) : IPushGateway
{
    private async Task<JsonDocument?> PostAsync(string path, object body, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        ct = timeout.Token;
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://exp.host/--/api/v2/push/" + path) { Content = JsonContent.Create(body) };
        if (configuration["Push:AccessToken"] is { Length: > 0 } token) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode) throw new PushTransportException(response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500);
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        // Expo responses are small for a single delivery/receipt. Bound malformed responses.
        var bytes = new byte[32769]; var count = 0;
        while (count < bytes.Length) { var read = await stream.ReadAsync(bytes.AsMemory(count), ct); if (read == 0) break; count += read; }
        if (count > 32768) throw new PushTransportException(false);
        return JsonDocument.Parse(bytes.AsMemory(0, count));
    }
    private static PushOutcome Outcome(JsonElement value, bool receipt)
    {
        if (value.GetProperty("status").GetString() == "ok")
            return receipt ? new("delivered") : new("accepted", value.GetProperty("id").GetString());
        var code = value.TryGetProperty("details", out var details) && details.TryGetProperty("error", out var error) ? error.GetString() : "push_rejected";
        code = code is "DeviceNotRegistered" or "MessageTooBig" or "MessageRateExceeded" or "MismatchSenderId" or "InvalidCredentials" ? code : "push_rejected";
        return new("error", ErrorCode: code, Retryable: code == "MessageRateExceeded");
    }
    public async Task<PushOutcome> SendAsync(string token, Guid activityId, CancellationToken ct)
    {
        using var json = await PostAsync("send", new { to = token, title = "Uni Companion", body = "You have a new activity update.", data = new { activityId }, sound = "default", channelId = "activity", ttl = 3600 }, ct);
        var data = json!.RootElement.GetProperty("data"); return Outcome(data.ValueKind == JsonValueKind.Array ? data[0] : data, false);
    }
    public async Task<PushOutcome> ReceiptAsync(string ticketId, CancellationToken ct)
    {
        using var json = await PostAsync("getReceipts", new { ids = new[] { ticketId } }, ct);
        return json!.RootElement.GetProperty("data").TryGetProperty(ticketId, out var receipt) ? Outcome(receipt, true) : new("pending");
    }
}
public sealed class PushTransportException(bool retryable) : Exception("Push transport unavailable") { public bool Retryable { get; } = retryable; }
