using System.Text.Json.Nodes;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// The scorer, scored. <see cref="JsonDelta"/> decides what "the port matches" means
/// for every <c>resolve</c> test, and some of its rules — a missing key against an
/// explicit null above all — occur in NO golden, so the conformance tests could
/// never notice them break. Each expectation below is the output of
/// <c>conformance.js</c>'s own <c>delta</c> on the same inputs, measured
/// 2026-10-07, not reasoned from its source.
/// </summary>
public class JsonDeltaTests
{
    [Theory]
    [InlineData("{\"a\":null}", "{}", "[{\"path\":\"a\",\"from\":null,\"to\":null}]")]
    [InlineData("{}", "{\"a\":null}", "[{\"path\":\"a\",\"from\":null,\"to\":null}]")]
    [InlineData("{\"a\":1,\"b\":[1,2]}", "{\"b\":[1,2,3],\"a\":1,\"c\":{\"d\":1}}", "[{\"path\":\"b.2\",\"from\":null,\"to\":3},{\"path\":\"c\",\"from\":null,\"to\":{\"d\":1}}]")]
    [InlineData("{\"a\":[1]}", "{\"a\":{\"0\":1}}", "[{\"path\":\"a\",\"from\":[1],\"to\":{\"0\":1}}]")]
    [InlineData("{\"a\":\"x\"}", "{\"a\":\"y\"}", "[{\"path\":\"a\",\"from\":\"x\",\"to\":\"y\"}]")]
    [InlineData("[{\"s\":1}]", "[{\"s\":1,\"r\":{\"v\":[\"1\"]}}]", "[{\"path\":\"0.r\",\"from\":null,\"to\":{\"v\":[\"1\"]}}]")]
    [InlineData("{\"a\":{}}", "{\"a\":[]}", "[{\"path\":\"a\",\"from\":{},\"to\":[]}]")]
    [InlineData("{\"a\":{\"b\":[1]}}", "{\"a\":{\"b\":[1]}}", "[]")]
    public void Delta_matches_Node(string before, string after, string expected)
    {
        Assert.Equal(expected, JsonDelta.Of(JsonNode.Parse(before), JsonNode.Parse(after)).ToJsonString());
    }
}
