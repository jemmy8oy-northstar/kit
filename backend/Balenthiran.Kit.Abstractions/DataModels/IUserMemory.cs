namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>The body of <c>GET</c> and <c>POST /api/me/memory</c> (BEH-USERS-4): the signed-in user's own memory.</summary>
public interface IUserMemory
{
    /// <summary>What Kit remembers about this user; empty before they have written anything.</summary>
    string Body { get; }
}
