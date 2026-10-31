using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>A <c>decision</c> block (BEH-ARCH-1). Not served as is: the project view maps it to a <see cref="DecisionView"/>.</summary>
public sealed class Decision : IDecision
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public string Part { get; set; } = "";

    public string Chosen { get; set; } = "";

    public List<string> Considered { get; } = [];

    public List<string> Because { get; } = [];

    // Unlike a behaviour, a decision nobody has ruled on is unreviewed, not approved: the whole
    // point of the block is that its author is guessing until someone says otherwise.
    public Source Source { get; set; } = new() { Origin = "defined", Ref = null };

    public Review Review { get; set; } = new() { State = "unreviewed", Note = null };

    public required string At { get; init; }

    IReadOnlyList<string> IDecision.Considered => Considered;

    IReadOnlyList<string> IDecision.Because => Because;

    ISource IDecision.Source => Source;

    IReview IDecision.Review => Review;
}
