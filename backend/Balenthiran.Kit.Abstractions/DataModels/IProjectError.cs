namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>A list row for a corpus that could not be projected — reported AS an error, never as zero behaviours.</summary>
public interface IProjectError
{
    string App { get; }

    string Error { get; }
}
