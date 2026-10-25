using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Services;

namespace Balenthiran.Kit.Database;

/// <summary>
/// <see cref="IPushSender"/> for the Web Push protocol (RFC 8030, VAPID RFC 8292). The <c>WebPush</c>
/// package (web-push-csharp, pinned) signs the VAPID token; <see cref="PushPayloadEncryptor"/>
/// encrypts the body as <c>aes128gcm</c> (RFC 8291), which the package cannot, and which Apple's push
/// service for home-screen web apps requires. Apple's, Google's and Mozilla's services all answer alike.
/// </summary>
public sealed class WebPushSender(HttpClient http, string subject, string publicKey, string privateKey) : IPushSender
{
    private static readonly TimeSpan TokenLife = TimeSpan.FromHours(12);

    private readonly PushPayloadEncryptor encryptor = new();

    /// <summary>How long a push service keeps a message for a phone that is off, in seconds: a day, then it is stale news.</summary>
    public const int TimeToLive = 86400;

    /// <inheritdoc />
    public async Task<PushOutcome> SendAsync(IPushSubscription subscription, string payload, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = Request(subscription, payload);
            using var response = await http.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return PushOutcome.Sent;
            }

            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
            {
                return PushOutcome.Gone;
            }

            await Console.Error.WriteLineAsync($"kit: the push service answered {(int)response.StatusCode} for one subscription");
            return PushOutcome.Failed;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or FormatException or ArgumentException or CryptographicException && !cancellationToken.IsCancellationRequested)
        {
            // An unreachable push service, or a subscription whose keys do not decode: that device only.
            await Console.Error.WriteLineAsync($"kit: could not push to one subscription — {e.GetType().Name}: {e.Message}");
            return PushOutcome.Failed;
        }
    }

    private HttpRequestMessage Request(IPushSubscription subscription, string payload)
    {
        var uri = new Uri(subscription.Endpoint);
        var expiry = DateTimeOffset.UtcNow.Add(TokenLife).ToUnixTimeSeconds();

        // The package's header is `WebPush <jwt>`, the draft's scheme; RFC 8292 spells it `vapid t=<jwt>, k=<key>`.
        var jwt = WebPush.VapidHelper.GetVapidHeaders($"{uri.Scheme}://{uri.Authority}", subject, publicKey, privateKey, expiry)["Authorization"];
        jwt = jwt[(jwt.IndexOf(' ', StringComparison.Ordinal) + 1)..];

        var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new ByteArrayContent(encryptor.Encrypt(subscription.P256dh, subscription.Auth, Encoding.UTF8.GetBytes(payload))),
        };
        request.Headers.TryAddWithoutValidation("Authorization", $"vapid t={jwt}, k={publicKey}");
        request.Headers.Add("TTL", TimeToLive.ToString(System.Globalization.CultureInfo.InvariantCulture));
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        request.Content.Headers.ContentEncoding.Add("aes128gcm");
        return request;
    }
}
