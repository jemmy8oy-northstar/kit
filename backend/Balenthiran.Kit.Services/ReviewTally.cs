namespace Balenthiran.Kit.Services;

/// <summary>How many flagged bundles each review outcome has, and how many still wait for a review.</summary>
public sealed record ReviewTally(int Flagged, int Awaiting, int Agreed, int Revised, int Escalated);
