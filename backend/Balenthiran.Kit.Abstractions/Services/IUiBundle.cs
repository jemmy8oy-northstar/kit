using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>The built UI, served out of one directory — every request that is not under <c>/api</c>.</summary>
public interface IUiBundle
{
    /// <summary>Serve one raw, still-encoded path, base path already removed.</summary>
    IKitResponse Serve(string pathname);
}
