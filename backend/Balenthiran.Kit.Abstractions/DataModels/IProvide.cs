namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>Where an inference is written down, so an approve/deny has something to point at.</summary>
public interface IProvide
{
    string Kind { get; }

    string Name { get; }

    string Slot { get; }

    IReadOnlyList<string> Value { get; }

    string From { get; }

    string At { get; }
}
