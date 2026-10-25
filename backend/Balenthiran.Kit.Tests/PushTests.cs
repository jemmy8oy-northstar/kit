using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.DataModels.Models;
using Balenthiran.Kit.Services;
using Balenthiran.Kit.WebApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// kit-ui.beh <c>BEH-NOTIFY-3</c>: a new feed entry reaches his phone as a web push, once. The
/// network is a fake <see cref="IPushSender"/>; the one test of the real sender scripts the push
/// service's HTTP answers and checks the request is VAPID-signed and encrypted.
/// </summary>
public class PushTests
{
    private const string Key = "BNotARealPublicKey";
    private const string PrivateKey = "the-private-vapid-key-do-not-print";

    // ---- the routes: signed in only, idempotent, off without a key pair -----------------------

    [Fact]
    public void Subscribing_twice_to_one_endpoint_keeps_one_subscription_with_the_newest_keys()
    {
        var store = new MemoryPushStore();
        var router = Router(store);

        Assert.Equal(200, router.Route("POST", "/api/push/subscriptions", body: Subscription("https://push.example/a", "k1", "a1")).Status);
        Assert.Equal(200, router.Route("POST", "/api/push/subscriptions", body: Subscription("https://push.example/a", "k2", "a2")).Status);
        Assert.Equal(200, router.Route("POST", "/api/push/subscriptions", body: Subscription("https://push.example/b", "k3", "a3")).Status);

        var all = store.All();
        Assert.Equal(["https://push.example/a", "https://push.example/b"], all.Select(s => s.Endpoint).Order());
        Assert.Equal(("k2", "a2"), (all.Single(s => s.Endpoint.EndsWith("/a", StringComparison.Ordinal)).P256dh, all.Single(s => s.Endpoint.EndsWith("/a", StringComparison.Ordinal)).Auth));
    }

    [Fact]
    public void Unsubscribing_forgets_the_endpoint_and_is_idempotent()
    {
        var store = new MemoryPushStore();
        var router = Router(store);
        router.Route("POST", "/api/push/subscriptions", body: Subscription("https://push.example/a", "k", "a"));

        Assert.Equal(200, router.Route("POST", "/api/push/subscriptions/remove", body: Json(new { endpoint = "https://push.example/a" })).Status);
        Assert.Equal(200, router.Route("POST", "/api/push/subscriptions/remove", body: Json(new { endpoint = "https://push.example/a" })).Status);

        Assert.Empty(store.All());
    }

    [Fact]
    public void GET_push_reports_whether_this_Kit_can_push_and_with_which_key()
    {
        var on = Assert.IsType<PushStatus>(Router(new MemoryPushStore()).Route("GET", "/api/push").Body);
        var off = Assert.IsType<PushStatus>(Router(null, key: null).Route("GET", "/api/push").Body);

        Assert.True(on.Configured);
        Assert.Equal(Key, on.PublicKey);
        Assert.False(off.Configured);
        Assert.Null(off.PublicKey);
    }

    [Fact]
    public void Without_the_key_pair_the_subscription_routes_answer_503_with_the_reason_and_store_nothing()
    {
        var store = new MemoryPushStore();
        var router = Router(store, key: null);

        var r = router.Route("POST", "/api/push/subscriptions", body: Subscription("https://push.example/a", "k", "a"));

        Assert.Equal(503, r.Status);
        Assert.Equal("push-not-configured", Assert.IsType<ApiError>(r.Body).Error);
        Assert.Contains("kit-push", Assert.IsType<ApiError>(r.Body).Reason, StringComparison.Ordinal);
        Assert.Equal(503, router.Route("POST", "/api/push/subscriptions/remove", body: Json(new { endpoint = "https://push.example/a" })).Status);
        Assert.Empty(store.All());
    }

    [Fact]
    public void Push_routes_are_for_a_signed_in_session_only()
    {
        var store = new MemoryPushStore();
        var router = Router(store, password: "pw");

        Assert.Equal(401, router.Route("GET", "/api/push").Status);
        Assert.Equal(401, router.Route("POST", "/api/push/subscriptions", body: Subscription("https://push.example/a", "k", "a")).Status);

        var cookie = router.Route("POST", "/api/session", body: Json(new { password = "pw" })).SetCookie!.Split(';')[0];
        Assert.Equal(200, router.Route("GET", "/api/push", cookie).Status);
        Assert.Equal(200, router.Route("POST", "/api/push/subscriptions", cookie, body: Subscription("https://push.example/a", "k", "a")).Status);
        Assert.Single(store.All());
    }

    [Theory]
    [InlineData("""{"endpoint":"http://push.example/a","keys":{"p256dh":"k","auth":"a"}}""")]
    [InlineData("""{"endpoint":"https://push.example/a"}""")]
    [InlineData("""{"endpoint":"https://push.example/a","keys":{"p256dh":"k"}}""")]
    [InlineData("""{"endpoint":42,"keys":{"p256dh":"k","auth":"a"}}""")]
    [InlineData("""["not","an","object"]""")]
    public void A_subscription_that_is_not_the_browsers_is_refused_and_stores_nothing(string body)
    {
        var store = new MemoryPushStore();

        var r = Router(store).Route("POST", "/api/push/subscriptions", body: JsonDocument.Parse(body).RootElement);

        Assert.Equal(400, r.Status);
        Assert.Empty(store.All());
    }

    // ---- start-up: the poller exists only with the key pair -------------------------------------

    [Fact]
    public void Without_the_key_pair_there_is_no_poller_and_no_store()
    {
        var settings = KitSettings.FromEnvironment(Env(), RepoLayout.Root);

        using var services = new ServiceCollection().AddKitServices(settings).BuildServiceProvider();

        Assert.False(settings.Push);
        Assert.Null(services.GetService<PushNotifier>());
        Assert.Null(services.GetService<IPushStore>());
        Assert.DoesNotContain(services.GetServices<IHostedService>(), h => h is PushPoller);
        Assert.False(Assert.IsType<PushStatus>(services.GetRequiredService<IKitRouter>().Route("GET", "/api/push", null).Body).Configured);
    }

    [Fact]
    public void With_the_key_pair_the_poller_runs_over_the_in_memory_store_when_there_is_no_database()
    {
        var settings = KitSettings.FromEnvironment(Env(("KIT_PUSH_PUBLIC_KEY", Key), ("KIT_PUSH_PRIVATE_KEY", PrivateKey)), RepoLayout.Root);
        var services = new ServiceCollection();
        services.AddSingleton<IPushSender>(new FakeSender());

        using var provider = services.AddKitServices(settings).BuildServiceProvider();

        Assert.True(settings.Push);
        Assert.Contains(provider.GetServices<IHostedService>(), h => h is PushPoller);
        Assert.IsType<MemoryPushStore>(provider.GetRequiredService<IPushStore>());
    }

    [Fact]
    public void Settings_never_print_the_private_key_and_refuse_half_a_pair()
    {
        var settings = KitSettings.FromEnvironment(Env(("KIT_PUSH_PUBLIC_KEY", Key), ("KIT_PUSH_PRIVATE_KEY", PrivateKey)), RepoLayout.Root);

        Assert.DoesNotContain(PrivateKey, settings.ToString(), StringComparison.Ordinal);
        Assert.Equal(KitSettings.DefaultPushSubject, settings.PushSubject);

        var e = Assert.Throws<InvalidOperationException>(() => KitSettings.FromEnvironment(Env(("KIT_PUSH_PUBLIC_KEY", Key)), RepoLayout.Root));
        Assert.Contains("KIT_PUSH_PRIVATE_KEY", e.Message, StringComparison.Ordinal);
    }

    // ---- the poller: the mark, exactly once, 410 ------------------------------------------------

    [Fact]
    public async Task The_first_tick_pushes_nothing_and_a_new_entry_is_pushed_exactly_once_across_ticks()
    {
        var feed = new FakeFeed(Entry("2026-10-25T10:00:00Z", "old news"));
        var store = new MemoryPushStore();
        store.Add(Sub("https://push.example/a"));
        var sender = new FakeSender();
        var notifier = new PushNotifier(feed, store, sender, "/kit");

        await notifier.TickAsync();
        await notifier.TickAsync();
        Assert.Empty(sender.Sent);

        feed.Add(Entry("2026-10-25T10:05:00Z", "raised kit#500"));
        await notifier.TickAsync();
        await notifier.TickAsync();

        var (endpoint, payload) = Assert.Single(sender.Sent);
        Assert.Equal("https://push.example/a", endpoint);
        var body = JsonDocument.Parse(payload).RootElement;
        Assert.Equal("Kit", body.GetProperty("title").GetString());
        Assert.Equal("raised kit#500", body.GetProperty("body").GetString());
        Assert.Equal("/kit/notifications", body.GetProperty("url").GetString());
    }

    [Fact]
    public async Task Entries_that_arrive_together_are_pushed_oldest_first_even_within_one_second()
    {
        var feed = new FakeFeed(Entry("2026-10-25T10:00:00Z", "seed"));
        var store = new MemoryPushStore();
        store.Add(Sub("https://push.example/a"));
        var sender = new FakeSender();
        var notifier = new PushNotifier(feed, store, sender, string.Empty);
        await notifier.TickAsync();

        feed.Add(Entry("2026-10-25T10:01:00Z", "first"));
        feed.Add(Entry("2026-10-25T10:01:00Z", "second"));
        await notifier.TickAsync();

        Assert.Equal(["first", "second"], sender.Sent.Select(s => JsonDocument.Parse(s.Payload).RootElement.GetProperty("body").GetString()));
    }

    [Fact]
    public async Task Ticks_racing_each_other_push_an_entry_once_and_the_mark_survives_a_restart()
    {
        var feed = new FakeFeed(Entry("2026-10-25T10:00:00Z", "seed"));
        var store = new MemoryPushStore();
        store.Add(Sub("https://push.example/a"));
        var sender = new FakeSender();
        await new PushNotifier(feed, store, sender, "/kit").TickAsync();
        feed.Add(Entry("2026-10-25T10:05:00Z", "new"));

        // Eight ticks at once, over two notifiers sharing one store (a restart's overlap, or a second pod).
        var a = new PushNotifier(feed, store, sender, "/kit");
        var b = new PushNotifier(feed, store, sender, "/kit");
        await Task.WhenAll(Enumerable.Range(0, 8).Select(i => Task.Run(() => (i % 2 == 0 ? a : b).TickAsync())));

        Assert.Single(sender.Sent);

        // A restart: new notifier, same store. The mark is the store's, so nothing is pushed again.
        await new PushNotifier(feed, store, sender, "/kit").TickAsync();
        Assert.Single(sender.Sent);
    }

    [Fact]
    public void The_mark_moves_only_from_the_value_the_caller_saw()
    {
        var store = new MemoryPushStore();

        Assert.True(store.AdvanceMark(null, "m1"));
        Assert.False(store.AdvanceMark(null, "m2"));
        Assert.False(store.AdvanceMark("other", "m2"));
        Assert.True(store.AdvanceMark("m1", "m2"));
        Assert.Equal("m2", store.Mark());
    }

    [Fact]
    public void The_postgres_store_keeps_subscriptions_and_moves_the_mark_atomically()
    {
        // Needs a real database, so CI without one skips it by passing. Run it with KIT_TEST_DATABASE set.
        if (Environment.GetEnvironmentVariable("KIT_TEST_DATABASE") is not { Length: > 0 } connection)
        {
            return;
        }

        using var store = new PostgresPushStore(connection);
        var endpoint = $"https://push.example/{Guid.NewGuid():N}";

        Assert.True(store.Add(Sub(endpoint)));
        Assert.False(store.Add(new PushSubscription { Endpoint = endpoint, P256dh = "k2", Auth = "a2" }));
        Assert.Equal("k2", store.All().Single(s => s.Endpoint == endpoint).P256dh);
        Assert.True(store.Remove(endpoint));
        Assert.False(store.Remove(endpoint));

        var first = store.Mark();
        var next = $"m{Guid.NewGuid():N}";
        Assert.True(store.AdvanceMark(first, next));
        Assert.False(store.AdvanceMark(first, "stale"));
        Assert.Equal(next, store.Mark());
    }

    [Fact]
    public async Task A_subscription_the_push_service_calls_gone_is_deleted_and_other_failures_stop_nobody()
    {
        var feed = new FakeFeed(Entry("2026-10-25T10:00:00Z", "seed"));
        var store = new MemoryPushStore();
        foreach (var e in new[] { "gone", "fails", "throws", "fine" })
        {
            store.Add(Sub($"https://push.example/{e}"));
        }

        var sender = new FakeSender
        {
            Answer = s => s.Endpoint.EndsWith("/gone", StringComparison.Ordinal) ? PushOutcome.Gone
                : s.Endpoint.EndsWith("/fails", StringComparison.Ordinal) ? PushOutcome.Failed
                : s.Endpoint.EndsWith("/throws", StringComparison.Ordinal) ? throw new HttpRequestException("push service down")
                : PushOutcome.Sent,
        };
        var notifier = new PushNotifier(feed, store, sender, "/kit");
        await notifier.TickAsync();

        feed.Add(Entry("2026-10-25T10:05:00Z", "news"));
        await notifier.TickAsync();

        // Every phone was tried; only the one the service called Gone was forgotten.
        Assert.Equal(["fails", "fine", "gone", "throws"], sender.Sent.Select(s => s.Endpoint[(s.Endpoint.LastIndexOf('/') + 1)..]).Order());
        Assert.Equal(["fails", "fine", "throws"], store.All().Select(s => s.Endpoint[(s.Endpoint.LastIndexOf('/') + 1)..]).Order());
    }

    [Fact]
    public async Task A_feed_that_cannot_be_read_pushes_nothing_and_moves_nothing()
    {
        var feed = new FakeFeed(Entry("2026-10-25T10:00:00Z", "seed"));
        var store = new MemoryPushStore();
        store.Add(Sub("https://push.example/a"));
        var sender = new FakeSender();
        var notifier = new PushNotifier(feed, store, sender, "/kit");
        await notifier.TickAsync();
        var mark = store.Mark();

        feed.Reason = "GitHub answered 404";
        await notifier.TickAsync();
        feed.Reason = null;
        await notifier.TickAsync();

        Assert.Empty(sender.Sent);
        Assert.Equal(mark, store.Mark());
    }

    [Fact]
    public async Task A_long_message_is_cut_for_the_phone_without_splitting_a_character()
    {
        var feed = new FakeFeed(Entry("2026-10-25T10:00:00Z", "seed"));
        var store = new MemoryPushStore();
        store.Add(Sub("https://push.example/a"));
        var sender = new FakeSender();
        var notifier = new PushNotifier(feed, store, sender, "/kit");
        await notifier.TickAsync();

        feed.Add(Entry("2026-10-25T10:05:00Z", new string('x', 178) + "😀" + new string('y', 50)));
        await notifier.TickAsync();

        var body = JsonDocument.Parse(Assert.Single(sender.Sent).Payload).RootElement.GetProperty("body").GetString()!;
        Assert.True(body.Length <= PushNotifier.BodyLength);
        Assert.EndsWith("…", body, StringComparison.Ordinal);
        Assert.False(body.Any(char.IsSurrogate));
        Assert.Equal("short", PushNotifier.Body("short"));
    }

    // ---- the real sender, against a scripted push service ---------------------------------------

    [Theory]
    [InlineData(HttpStatusCode.Created, PushOutcome.Sent)]
    [InlineData(HttpStatusCode.Gone, PushOutcome.Gone)]
    [InlineData(HttpStatusCode.NotFound, PushOutcome.Gone)]
    [InlineData(HttpStatusCode.InternalServerError, PushOutcome.Failed)]
    public async Task The_sender_signs_with_VAPID_encrypts_the_payload_and_reads_the_push_services_answer(HttpStatusCode answer, PushOutcome expected)
    {
        var keys = NewVapidKeys();
        var handler = new Scripted(answer);
        var sender = new WebPushSender(new HttpClient(handler), KitSettings.DefaultPushSubject, keys.PublicKey, keys.PrivateKey);
        using var device = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var subscription = DeviceSubscription(device);

        var outcome = await sender.SendAsync(subscription, """{"title":"Kit","body":"hello"}""");

        Assert.Equal(expected, outcome);
        var sent = Assert.Single(handler.Requests);
        Assert.Equal("https://push.example/device", sent.Uri);
        Assert.StartsWith("vapid t=eyJ", sent.Authorization, StringComparison.Ordinal);
        Assert.EndsWith($", k={keys.PublicKey}", sent.Authorization, StringComparison.Ordinal);
        Assert.Equal("aes128gcm", sent.ContentEncoding);
        Assert.Equal("86400", sent.Ttl);
        Assert.Equal("""{"title":"Kit","body":"hello"}""", Decrypt(device, subscription.Auth, sent.Body));
    }

    [Fact]
    public void The_payload_is_RFC_8291_aes128gcm_and_differs_every_time_it_is_sealed()
    {
        using var device = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var subscription = DeviceSubscription(device);
        var encryptor = new PushPayloadEncryptor();
        var message = System.Text.Encoding.UTF8.GetBytes("""{"body":"héllo 😀"}""");

        var one = encryptor.Encrypt(subscription.P256dh, subscription.Auth, message);
        var two = encryptor.Encrypt(subscription.P256dh, subscription.Auth, message);

        Assert.NotEqual(one, two);
        Assert.Equal("""{"body":"héllo 😀"}""", Decrypt(device, subscription.Auth, one));
        Assert.Equal(4096u, System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(one.AsSpan(16, 4)));
        Assert.Equal(65, one[20]);
    }

    /// <summary>RFC 8291 section 5 and Appendix A, fetched from the RFC: the published inputs must give the published body.</summary>
    [Fact]
    public void The_encryptor_reproduces_RFC_8291_Appendix_A_byte_for_byte()
    {
        var asPublic = FromBase64Url("BP4z9KsN6nGRTbVYI_c7VJSPQTBtkgcy27mlmlMoZIIgDll6e3vCYLocInmYWAmS6TlzAC8wEqKK6PBru3jl7A8");
        using var server = ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            D = FromBase64Url("yfWPiYE-n46HLnH0KqZOF1fJJU3MYrct3AELtAQ-oRw"),
            Q = new ECPoint { X = asPublic[1..33], Y = asPublic[33..65] },
        });

        var body = new PushPayloadEncryptor().Encrypt(
            "BCVxsr7N_eNgVRqvHtD0zTZsEc6-VV-JvLexhqUzORcxaOzi6-AYWXvTBHm4bjyPjs7Vd8pZGH6SRpkNtoIAiw4",
            "BTBZMqHH6r4Tts7J_aSIgg",
            System.Text.Encoding.ASCII.GetBytes("When I grow up, I want to be a watermelon"),
            server,
            FromBase64Url("DGv6ra1nlYgDCS1FRnbzlw"));

        Assert.Equal(
            "DGv6ra1nlYgDCS1FRnbzlwAAEABBBP4z9KsN6nGRTbVYI_c7VJSPQTBtkgcy27ml"
            + "mlMoZIIgDll6e3vCYLocInmYWAmS6TlzAC8wEqKK6PBru3jl7A_yl95bQpu6cVPT"
            + "pK4Mqgkf1CXztLVBSt2Ks3oZwbuwXPXLWyouBWLVWGNWQexSgSxsj_Qulcy4a-fN",
            Base64Url(body));
    }

    [Fact]
    public void The_VAPID_token_is_an_ES256_JWT_with_audience_expiry_and_subject_signed_by_the_key_pair()
    {
        var keys = NewVapidKeys();
        var now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);

        var token = new VapidSigner("mailto:kit@balenthiran.co.uk", keys.PublicKey, keys.PrivateKey).Token("https://push.example", now);

        var parts = token.Split('.');
        Assert.Equal(3, parts.Length);
        Assert.Equal("""{"typ":"JWT","alg":"ES256"}""", System.Text.Encoding.UTF8.GetString(FromBase64Url(parts[0])));
        var claims = JsonDocument.Parse(FromBase64Url(parts[1])).RootElement;
        Assert.Equal("https://push.example", claims.GetProperty("aud").GetString());
        Assert.Equal(now.AddHours(12).ToUnixTimeSeconds(), claims.GetProperty("exp").GetInt64());
        Assert.Equal("mailto:kit@balenthiran.co.uk", claims.GetProperty("sub").GetString());

        // Verified with the public key alone, as the push service does; r||s, 64 bytes, not DER.
        var point = FromBase64Url(keys.PublicKey);
        using var verifier = ECDsa.Create(new ECParameters { Curve = ECCurve.NamedCurves.nistP256, Q = new ECPoint { X = point[1..33], Y = point[33..65] } });
        var signature = FromBase64Url(parts[2]);
        Assert.Equal(64, signature.Length);
        var signed = System.Text.Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]);
        Assert.True(verifier.VerifyData(signed, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
        Assert.False(verifier.VerifyData(System.Text.Encoding.ASCII.GetBytes(parts[0] + "." + parts[1] + "x"), signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
    }

    [Fact]
    public void A_subscription_with_a_bad_key_fails_that_device_only()
    {
        var encryptor = new PushPayloadEncryptor();

        Assert.Throws<FormatException>(() => encryptor.Encrypt("AAAA", "AAAA", [1]));
    }

    // ---- helpers --------------------------------------------------------------------------------

    private static PushSubscription Sub(string endpoint) => new() { Endpoint = endpoint, P256dh = "k", Auth = "a" };

    private static Notification Entry(string at, string text) => new() { At = at, Kind = "note", Text = text };

    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value);

    private static JsonElement Subscription(string endpoint, string p256dh, string auth) =>
        Json(new { endpoint, expirationTime = (string?)null, keys = new { p256dh, auth } });

    private static Func<string, string?> Env(params (string Key, string Value)[] pairs)
    {
        var env = pairs.ToDictionary(p => p.Key, p => p.Value);
        return k => env.GetValueOrDefault(k);
    }

    /// <summary>A subscription as a browser would hold it: the device's public key and a random auth secret.</summary>
    private static PushSubscription DeviceSubscription(ECDiffieHellman device)
    {
        var q = device.ExportParameters(false).Q;
        var point = new byte[] { 0x04 }.Concat(q.X!).Concat(q.Y!).ToArray();
        return new PushSubscription { Endpoint = "https://push.example/device", P256dh = Base64Url(point), Auth = Base64Url(RandomNumberGenerator.GetBytes(16)) };
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>A fresh VAPID pair in the form the secret holds: the private scalar and the uncompressed public point, base64url.</summary>
    private static (string PublicKey, string PrivateKey) NewVapidKeys()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var p = key.ExportParameters(true);
        var point = new byte[] { 0x04 }.Concat(p.Q.X!).Concat(p.Q.Y!).ToArray();
        return (Base64Url(point), Base64Url(p.D!));
    }

    private static byte[] FromBase64Url(string value)
    {
        var s = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s.PadRight(s.Length + ((4 - (s.Length % 4)) % 4), '='));
    }

    /// <summary>
    /// What the phone does with RFC 8291's body, written from the RFC's section 3.4 and 4 rather than
    /// from the encryptor: header, then ECDH with the sender's key, then the two HKDF steps, then AES-GCM.
    /// </summary>
    private static string Decrypt(ECDiffieHellman device, string auth, byte[] body)
    {
        var salt = body[..16];
        var keyIdLength = body[20];
        var asPublic = body[21..(21 + keyIdLength)];
        var sealedRecord = body[(21 + keyIdLength)..];
        using var sender = ECDiffieHellman.Create(new ECParameters { Curve = ECCurve.NamedCurves.nistP256, Q = new ECPoint { X = asPublic[1..33], Y = asPublic[33..65] } });
        var q = device.ExportParameters(false).Q;
        var uaPublic = new byte[] { 0x04 }.Concat(q.X!).Concat(q.Y!).ToArray();

        var prkKey = HKDF.Extract(HashAlgorithmName.SHA256, device.DeriveRawSecretAgreement(sender.PublicKey), FromBase64Url(auth));
        var ikm = HKDF.Expand(HashAlgorithmName.SHA256, prkKey, 32, [.. "WebPush: info\0"u8, .. uaPublic, .. asPublic]);
        var prk = HKDF.Extract(HashAlgorithmName.SHA256, ikm, salt);
        var cek = HKDF.Expand(HashAlgorithmName.SHA256, prk, 16, "Content-Encoding: aes128gcm\0"u8.ToArray());
        var nonce = HKDF.Expand(HashAlgorithmName.SHA256, prk, 12, "Content-Encoding: nonce\0"u8.ToArray());

        var cipher = sealedRecord[..^16];
        var plain = new byte[cipher.Length];
        using (var aes = new AesGcm(cek, 16))
        {
            aes.Decrypt(nonce, cipher, sealedRecord[^16..], plain);
        }

        // Padding delimiter: the last record ends in 0x02, after the message and before any zero padding.
        Assert.Equal(0x02, plain[^1]);
        return System.Text.Encoding.UTF8.GetString(plain[..^1]);
    }

    private static KitRouter Router(IPushStore? store, string? key = Key, string? password = null)
    {
        var corpora = new CorpusDirectory(RepoLayout.FrozenBehaviours, RepoLayout.FrozenRoot);
        var viewer = new ProjectViewer(corpora, new CorpusParser(), new BehaviourResolver(), new TestGenerator(), new ProjectReporter());
        return new KitRouter(
            corpora,
            viewer,
            new UiBundle(Path.Combine(RepoLayout.Root, "no-bundle-here")),
            password,
            new OriginPolicy(new UrlParser(), null),
            new SessionStore(),
            new SignInThrottle(),
            pushStore: store,
            pushPublicKey: key);
    }

    private sealed class FakeFeed(params Notification[] seed) : INotificationReader
    {
        private readonly List<Notification> entries = [.. seed.Reverse()];

        /// <summary>Set to make the feed unreadable, as a failed GitHub read does.</summary>
        public string? Reason { get; set; }

        /// <summary>The agent appends: the newest goes first, as the reader lists it.</summary>
        public void Add(Notification entry) => entries.Insert(0, entry);

        public INotificationFeed Recent() => new NotificationFeed { Entries = [.. entries], Reason = Reason };

        public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeSender : IPushSender
    {
        private readonly object gate = new();
        private readonly List<(string Endpoint, string Payload)> sent = [];

        public Func<IPushSubscription, PushOutcome> Answer { get; set; } = _ => PushOutcome.Sent;

        public IReadOnlyList<(string Endpoint, string Payload)> Sent
        {
            get
            {
                lock (gate)
                {
                    return [.. sent];
                }
            }
        }

        public Task<PushOutcome> SendAsync(IPushSubscription subscription, string payload, CancellationToken cancellationToken = default)
        {
            lock (gate)
            {
                sent.Add((subscription.Endpoint, payload));
            }

            return Task.FromResult(Answer(subscription));
        }
    }

    private sealed class Scripted(HttpStatusCode answer) : HttpMessageHandler
    {
        public List<(string Uri, string Authorization, string ContentEncoding, string Ttl, byte[] Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
            Requests.Add((
                request.RequestUri!.ToString(),
                string.Join(' ', request.Headers.GetValues("Authorization")),
                string.Join(',', request.Content.Headers.ContentEncoding),
                string.Join(',', request.Headers.GetValues("TTL")),
                body));
            return new HttpResponseMessage(answer);
        }
    }
}
