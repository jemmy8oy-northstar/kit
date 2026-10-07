namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>
/// A hole nothing filled. <see cref="Key"/> is <c>kind:Name.slot</c> when the step
/// names a noun to own it, and <c>?slot</c> when it names only literals.
/// </summary>
public interface IOpenHole
{
    string Key { get; }

    string Slot { get; }

    string At { get; }
}
