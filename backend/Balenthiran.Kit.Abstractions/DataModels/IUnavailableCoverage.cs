namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>Coverage that could not be read, and why — the sentence the UI shows in place of a number.</summary>
public interface IUnavailableCoverage : ICoverage
{
    string Reason { get; }
}
