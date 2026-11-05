namespace Balenthiran.Kit.Services;

/// <summary>One request to the model proxy, as the host read it.</summary>
/// <param name="RunToken">The <c>x-api-key</c> header: the run token, NOT Anthropic's key.</param>
/// <param name="AnthropicVersion">The caller's <c>anthropic-version</c>, passed through.</param>
/// <param name="AnthropicBeta">The caller's <c>anthropic-beta</c>, passed through.</param>
/// <param name="CountTokens">True for <c>messages/count_tokens</c>, which costs nothing.</param>
/// <param name="Body">The request body; null when it ran past <see cref="AgentProxy.MaxBody"/>.</param>
public sealed record AgentCall(string? RunToken, string? AnthropicVersion, string? AnthropicBeta, bool CountTokens, byte[]? Body);
