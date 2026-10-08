namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>The answer to signing in or out. The token is never here — it travels only as an HttpOnly cookie.</summary>
public interface ISignInState
{
    bool Ok { get; }

    bool SignedIn { get; }
}
