using System.Globalization;
using System.Text.RegularExpressions;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.DataModels.Models;

namespace Balenthiran.Kit.Services;

/// <summary>
/// BEH-BOARD-4: when each built behaviour was completed — "when the behaviour last stopped being
/// pending, read from git history, not stored in the .beh", and "a behaviour with no history (never
/// pending) is completed when it was added".
/// </summary>
public static partial class Completions
{
    /// <summary>
    /// Every behaviour in <paramref name="text"/>, in file order, and whether it is <c>pending</c>.
    /// </summary>
    /// <remarks>
    /// ⚠️ A SCAN, not <see cref="CorpusParser"/>: an old commit was written in an older grammar, and
    /// one line the parser now refuses must not lose a behaviour its whole history. Only the two
    /// lines BEH-BOARD-4 reads are recognised, by the parser's own rules: lines are trimmed (so
    /// indentation means nothing), <c>#</c> is a comment, a <c>behaviour</c> line opens a behaviour,
    /// a <c>feature</c> line closes it, and a bare <c>pending</c> marks the open one.
    /// <c>CompletionTests</c> holds this to the parser over every corpus in the repository.
    /// </remarks>
    public static IReadOnlyList<KeyValuePair<string, bool>> Scan(string text)
    {
        var order = new List<string>();
        var pending = new Dictionary<string, bool>(StringComparer.Ordinal);
        string? cur = null;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim(CorpusParser.WsChars);
            if (line.Length == 0 || line.StartsWith('#')) continue;

            var m = BehaviourLine().Match(line);
            if (m.Success)
            {
                cur = m.Groups[1].Value;
                if (pending.TryAdd(cur, false)) order.Add(cur);
                continue;
            }

            if (FeatureLine().IsMatch(line))
            {
                cur = null;
            }
            else if (line == "pending" && cur is not null)
            {
                pending[cur] = true;
            }
        }

        return order.Select(id => new KeyValuePair<string, bool>(id, pending[id])).ToList();
    }

    /// <summary>
    /// The completion of every behaviour <paramref name="current"/> says is built, in its order.
    /// </summary>
    /// <param name="current">The corpus as served now — which may be ahead of the history.</param>
    /// <param name="versions">The file's commits, oldest first, each as <see cref="Scan"/> read it.</param>
    public static List<Completion> Of(IReadOnlyList<KeyValuePair<string, bool>> current, IReadOnlyList<ICorpusVersion> versions)
    {
        var added = new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);
        var stopped = new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);
        // Kept across versions that lack the behaviour, so one deleted while pending and restored
        // built is completed on its return, not on its first appearance.
        var lastSeen = new Dictionary<string, bool>(StringComparer.Ordinal);
        var newest = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var v in versions)
        {
            newest = v.Pending.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            foreach (var (id, isPending) in newest)
            {
                added.TryAdd(id, v.At);
                if (!isPending && lastSeen.TryGetValue(id, out var was) && was)
                {
                    stopped[id] = v.At;
                }

                lastSeen[id] = isPending;
            }
        }

        var result = new List<Completion>();
        foreach (var (id, isPending) in current)
        {
            if (isPending) continue;

            // Built here but pending (or absent) at the newest commit: newer than anything git
            // holds — an uncommitted edit on a laptop, or a deployed clone behind GitHub's read.
            string? at = null;
            if (newest.TryGetValue(id, out var last) && !last)
            {
                at = Iso(stopped.TryGetValue(id, out var s) ? s : added[id]);
            }

            result.Add(new Completion { Id = id, At = at });
        }

        return result;
    }

    private static string Iso(DateTimeOffset at) =>
        at.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    // The parser's whitespace, not `\s`: the two differ on U+0085 and U+FEFF.
    [GeneratedRegex($@"^behaviour[{CorpusParser.Ws}]+([A-Z][A-Z0-9-]*)(?:[{CorpusParser.Ws}]|\z)")]
    private static partial Regex BehaviourLine();

    [GeneratedRegex($@"^feature(?:[{CorpusParser.Ws}]|\z)")]
    private static partial Regex FeatureLine();
}
