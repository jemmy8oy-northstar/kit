namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>Coverage that could not be read, and why. Hosted Kit reads no repository, so this is the only shape it serves.</summary>
public interface IUnavailableCoverage
{
    bool Available { get; }

    string Reason { get; }
}
