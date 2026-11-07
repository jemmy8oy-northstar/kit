using Balenthiran.Kit.Abstractions.Services;

namespace Balenthiran.Kit.Services;

/// <summary>
/// <see cref="ITriagedNoteStore"/> for a Kit with no <c>KIT_DATABASE</c>. ⚠️ In memory only, so a restart forgets
/// which notes were sorted and the next note's triage reads them all again, as before BEH-ORCH-8.
/// </summary>
public sealed class MemoryTriagedNoteStore : ITriagedNoteStore
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, HashSet<string>> sorted = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public IReadOnlySet<string> Read(string project)
    {
        lock (gate)
        {
            return sorted.TryGetValue(project, out var keys) ? keys.ToHashSet(StringComparer.Ordinal) : new HashSet<string>(StringComparer.Ordinal);
        }
    }

    /// <inheritdoc />
    public void Add(string project, IReadOnlyCollection<string> keys)
    {
        if (keys.Count == 0)
        {
            return;
        }

        lock (gate)
        {
            if (!sorted.TryGetValue(project, out var set))
            {
                sorted[project] = set = new HashSet<string>(StringComparer.Ordinal);
            }

            set.UnionWith(keys);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<string> Projects()
    {
        lock (gate)
        {
            return sorted.Keys.Order(StringComparer.Ordinal).ToList();
        }
    }
}
