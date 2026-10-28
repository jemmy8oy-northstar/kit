namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>
/// Coverage on the project page: either the ids a test names and the ids none does
/// (<see cref="IAvailableCoverage"/>), or why it could not be read (<see cref="IUnavailableCoverage"/>).
/// Never a zero that means "nothing was read".
/// </summary>
public interface ICoverage
{
    bool Available { get; }
}
