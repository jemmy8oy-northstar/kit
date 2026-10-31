namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>One Kit feature and its two switches (BEH-FLAGS-1).</summary>
public interface IFeatureSwitch
{
    string Id { get; }

    string Title { get; }

    /// <summary>Whether the feature is on for the operator. On unless a <c>kit:switch</c> line says off.</summary>
    bool Operator { get; }

    /// <summary>Whether the feature is on for test users. Off unless a <c>kit:switch</c> line says on.</summary>
    bool Testers { get; }
}
