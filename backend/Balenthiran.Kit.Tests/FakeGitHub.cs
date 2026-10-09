using System.Net;
using System.Text;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// GitHub for <see cref="GitHubPullRequestOpenerTests"/>: answers from a script, in order, and
/// records every request it was sent — so a test can assert what was NOT sent as well as what was.
/// </summary>
public sealed class FakeGitHub(params Func<HttpRequestMessage, HttpResponseMessage>[] answers) : HttpMessageHandler
{
    private int next;

    public List<(HttpMethod Method, Uri Uri, string? Authorization, string? Body)> Sent { get; } = [];

    public static Func<HttpRequestMessage, HttpResponseMessage> Json(HttpStatusCode status, string json) =>
        _ => new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Sent.Add((request.Method, request.RequestUri!, request.Headers.Authorization?.ToString(), body));
        if (next >= answers.Length)
        {
            throw new InvalidOperationException($"unscripted request #{next + 1}: {request.Method} {request.RequestUri}");
        }

        return answers[next++](request);
    }
}
