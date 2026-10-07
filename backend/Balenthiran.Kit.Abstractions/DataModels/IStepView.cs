namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>A step as the project view serves it: what was written, without what resolve added.</summary>
public interface IStepView
{
    string Kind { get; }

    string Verb { get; }

    string Text { get; }

    IReadOnlyList<IReference> Refs { get; }

    IReadOnlyList<IHole> Holes { get; }
}
