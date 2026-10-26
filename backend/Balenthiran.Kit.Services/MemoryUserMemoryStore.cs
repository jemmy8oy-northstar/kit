using System.Collections.Concurrent;
using Balenthiran.Kit.Abstractions.Services;

namespace Balenthiran.Kit.Services;

/// <summary>
/// <see cref="IUserMemoryStore"/> for a Kit with no <c>KIT_DATABASE</c>. ⚠️ Memory lives in this
/// process only, so a restart forgets it; only the Postgres store is meant for the deployed Kit.
/// </summary>
public sealed class MemoryUserMemoryStore : IUserMemoryStore
{
    private readonly ConcurrentDictionary<string, string> bodies = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public string? Read(string userId) => bodies.GetValueOrDefault(userId);

    /// <inheritdoc />
    public void Write(string userId, string body) => bodies[userId] = body;
}
