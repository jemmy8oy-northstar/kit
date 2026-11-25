using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Services;

/// <summary>What the folder tree under <c>.kit/features</c> holds: its features, and the behaviours of its <c>.kit</c> files.</summary>
public sealed record FeatureFolders(IReadOnlyList<FolderFeature> Features, IReadOnlyList<IBehaviour> Behaviours);
