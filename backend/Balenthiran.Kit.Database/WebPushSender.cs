using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Services;

namespace Balenthiran.Kit.Database;

/// <summary>
/// <see cref="IPushSender"/> for the Web Push protocol (RFC 8030). <see cref="VapidSigner"/> signs the
/// VAPID token (RFC 8292) and <see cref="PushPayloadEncryptor"/> encrypts the body as <c>aes128gcm</c>
/// (RFC 8291), which Apple's push service for home-screen web apps requires. No package is involved.
/// </summary>
public sealed class WebPushSender(HttpClient http, string subject, string publicKey, string privateKey) : IPushSender
{
    private readonly PushPayloadEncryptor encryptor = new();

    private readonly VapidSigner signer = new(subject, publicKey, privateKey);

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
        var jwt = signer.Token($"{uri.Scheme}://{uri.Authority}", DateTimeOffset.UtcNow);

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
