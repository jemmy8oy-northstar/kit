using Balenthiran.Kit.Abstractions.Services;

namespace Balenthiran.Kit.Services;

/// <summary>No project is public: what a router with no <see cref="ProjectVisibility"/> assumes (BEH-GATE-H1).</summary>
public sealed class NoProjectIsPublic : IProjectVisibility
{
    /// <inheritdoc />
    public bool IsPublic(string app) => false;
}
