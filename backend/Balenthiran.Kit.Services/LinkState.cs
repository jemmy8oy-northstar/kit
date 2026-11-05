namespace Balenthiran.Kit.Services;

/// <summary>What <see cref="LinkVerification.Classify"/> found for one test link.</summary>
public enum LinkState
{
    /// <summary>Nothing recorded to compare against.</summary>
    Unrecorded,

    /// <summary>The code it exercises still hashes to what was recorded.</summary>
    Verified,

    /// <summary>A recorded file is gone, or the files now hash differently.</summary>
    PossiblyBroken,
}
