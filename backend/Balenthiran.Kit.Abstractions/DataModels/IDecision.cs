namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>
/// A <c>decision</c> block (FEAT-ARCH, BEH-ARCH-1): one architecture decision, what was chosen,
/// what else was considered, and why. It belongs to one part of the system.
/// </summary>
public interface IDecision
{
    string Id { get; }

    string Title { get; }

    /// <summary>One of frontend, backend, data, deployment.</summary>
    string Part { get; }

    string Chosen { get; }

    IReadOnlyList<string> Considered { get; }

    IReadOnlyList<string> Because { get; }

    ISource Source { get; }

    IReview Review { get; }

    string At { get; }
}
