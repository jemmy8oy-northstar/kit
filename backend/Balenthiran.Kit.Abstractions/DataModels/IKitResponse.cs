namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>What the router answers: a status, a content type and a body — the shape `ui.js`'s `route()` returns.</summary>
public interface IKitResponse
{
    int Status { get; }

    string ContentType { get; }

    object Body { get; }
}
