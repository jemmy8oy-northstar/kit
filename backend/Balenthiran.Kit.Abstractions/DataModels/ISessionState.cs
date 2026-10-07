namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>Whether a lock exists, and whether this caller is past it.</summary>
public interface ISessionState
{
    bool Required { get; }

    bool SignedIn { get; }
}
