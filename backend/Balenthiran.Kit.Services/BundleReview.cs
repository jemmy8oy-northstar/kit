namespace Balenthiran.Kit.Services;

/// <summary>The one line a review leaves on a bundle, straight after its Flag line: <c>Review: agreed — note</c>.</summary>
public sealed record BundleReview(ReviewOutcome Outcome, string Note);
