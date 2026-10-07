using System.Text;
using System.Text.RegularExpressions;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.DataModels.Models;

namespace Balenthiran.Kit.Services;

/// <summary>
/// Ported from <c>ui.js</c>'s <c>bundle()</c>. A name is served only if it is IN the
/// directory listing — it is compared, never joined to a path — so there is nothing
/// to traverse with; the <c>..</c> refusal is a belt to that, not the defence.
/// Anything that names a file and is not in the bundle is a JSON 404, never the
/// shell: a stale page asking for a deleted hash must fail visibly.
/// </summary>
/// <param name="distDir">The built bundle: <c>index.html</c> and <c>assets/</c>.</param>
public sealed class UiBundle(string distDir) : IUiBundle
{
    private const string Immutable = "public, max-age=31536000, immutable";
    private const string NoStore = "no-store";

    private const string BuildCmd =
        "npm --prefix prototypes/behaviour-ast/ui ci && npm --prefix prototypes/behaviour-ast/ui run build";

    // Only what Vite emits, plus the fonts and images a UI grows into. Anything else is a download.
    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.Ordinal)
    {
        [".html"] = "text/html; charset=utf-8",
        [".js"] = "text/javascript; charset=utf-8",
        [".css"] = "text/css; charset=utf-8",
        [".json"] = "application/json; charset=utf-8",
        [".map"] = "application/json; charset=utf-8",
        [".svg"] = "image/svg+xml",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".webp"] = "image/webp",
        [".ico"] = "image/x-icon",
        [".woff"] = "font/woff",
        [".woff2"] = "font/woff2",
        [".txt"] = "text/plain; charset=utf-8",
    };

    private static readonly Regex Asset = new(@"^/assets/([^/]+)\z", RegexOptions.Compiled);
    private static readonly Regex RootFile = new(@"^/([^/]+)\z", RegexOptions.Compiled);

    /// <inheritdoc />
    public IKitResponse Serve(string pathname)
    {
        var index = Path.Combine(distDir, "index.html");

        // Checked first: with no bundle every answer below would be a 404, and "you
        // typed the wrong URL" is the wrong sentence for "nobody has built it yet".
        if (!File.Exists(index))
        {
            // Relative to the working directory: this page is screenshotted into public
            // PRs, and an absolute path names the filesystem of whatever host runs it.
            var rel = Path.GetRelativePath(Directory.GetCurrentDirectory(), distDir);
            return Html(503, Encoding.UTF8.GetBytes(
                "<!doctype html><html><head><meta charset=\"utf-8\">"
                + "<title>Kit UI — not built</title></head><body>"
                + "<h1>The Kit UI has not been built</h1>"
                + $"<p>The API is running and answering. The bundle is not in <code>{(rel.Length > 0 ? rel : distDir)}</code>,"
                + " which is gitignored, so a fresh clone has to build it once:</p>"
                + $"<pre>{BuildCmd}</pre>"
                + "<p>Then reload this page. While iterating on the UI itself, run"
                + " <code>npm --prefix prototypes/behaviour-ast/ui run dev</code> instead —"
                + " it proxies <code>/api</code> here and reloads on save.</p>"
                + "</body></html>"));
        }

        if (KitRouter.DecodeUriComponent(pathname) is not { } p)
        {
            return Json(400, "bad-request", "the path is not valid percent-encoding");
        }

        if (p.Split('/').Contains(".."))
        {
            return Json(404, "no-such-file", "a path segment of `..` names nothing in the bundle");
        }

        var asset = Asset.Match(p);
        if (asset.Success)
        {
            var assets = Path.Combine(distDir, "assets");
            var name = asset.Groups[1].Value;
            return FilesIn(assets).Contains(name)
                ? Send(assets, name, Immutable)
                : Json(404, "no-such-asset", $"no bundled asset named '{name}' — the page asking for it was built against a different bundle");
        }

        var root = RootFile.Match(p);
        if (root.Success && FilesIn(distDir).Contains(root.Groups[1].Value))
        {
            return Send(distDir, root.Groups[1].Value, NoStore);
        }

        if (Extname(p).Length > 0)
        {
            return Json(404, "no-such-file", $"nothing named {p} is in the bundle");
        }

        return Html(200, File.ReadAllBytes(index));
    }

    /// <summary>
    /// Node's <c>path.posix.extname</c>, line for line: trailing slashes ignored, a
    /// basename's leading dots are not an extension (<c>.b</c>, <c>..</c>), but
    /// <c>...</c> and <c>b.</c> answer <c>.</c>.
    /// </summary>
    public static string Extname(string path)
    {
        int startDot = -1, startPart = 0, end = -1, preDotState = 0;
        var matchedSlash = true;
        for (var i = path.Length - 1; i >= 0; --i)
        {
            var c = path[i];
            if (c == '/')
            {
                if (!matchedSlash)
                {
                    startPart = i + 1;
                    break;
                }

                continue;
            }

            if (end == -1)
            {
                matchedSlash = false;
                end = i + 1;
            }

            if (c == '.')
            {
                if (startDot == -1)
                {
                    startDot = i;
                }
                else if (preDotState != 1)
                {
                    preDotState = 1;
                }
            }
            else if (startDot != -1)
            {
                preDotState = -1;
            }
        }

        if (startDot == -1 || end == -1 || preDotState == 0
            || (preDotState == 1 && startDot == end - 1 && startDot == startPart + 1))
        {
            return string.Empty;
        }

        return path[startDot..end];
    }

    /// <summary>The names of the FILES directly in a directory; a missing directory is empty, never a throw.</summary>
    private static HashSet<string> FilesIn(string dir) =>
        Directory.Exists(dir)
            ? Directory.EnumerateFiles(dir).Select(f => Path.GetFileName(f)).ToHashSet(StringComparer.Ordinal)
            : [];

    private static KitResponse Send(string dir, string name, string cache) => new()
    {
        Status = 200,
        ContentType = ContentTypes.GetValueOrDefault(Extname(name).ToLowerInvariant(), "application/octet-stream"),
        CacheControl = cache,
        Raw = File.ReadAllBytes(Path.Combine(dir, name)),
    };

    // `no-store` for the shell: it is the only unhashed file that names the hashed
    // ones, and a cached copy outlives a rebuild and asks for deleted assets.
    private static KitResponse Html(int status, byte[] body) =>
        new() { Status = status, ContentType = "text/html; charset=utf-8", CacheControl = NoStore, Raw = body };

    private static KitResponse Json(int status, string error, string reason) =>
        new() { Status = status, ContentType = "application/json", Body = new ApiError { Error = error, Reason = reason } };
}
