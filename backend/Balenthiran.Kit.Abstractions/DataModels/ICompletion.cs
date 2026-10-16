namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>When one built behaviour was completed (BEH-BOARD-4).</summary>
public interface ICompletion
{
    string Id { get; }

    /// <summary>
    /// ISO 8601: the commit in which the behaviour last stopped being <c>pending</c>, or the one
    /// that added it if it never was. Null when the corpus as served says built but the newest
    /// commit still says pending — an edit not yet committed, or a clone behind the read — which
    /// makes it the most recent of all, not the oldest.
    /// </summary>
    string? At { get; }
}
