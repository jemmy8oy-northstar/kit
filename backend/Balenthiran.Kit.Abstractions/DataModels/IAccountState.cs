namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>The answer to creating an account or changing its password (BEH-USERS-1).</summary>
public interface IAccountState
{
    bool Ok { get; }

    string User { get; }
}
