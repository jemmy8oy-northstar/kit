namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>
/// One principle (kit#284 slice 5): a <c># kit:principle [global] &lt;Name&gt;: &lt;what it means&gt;</c>
/// line in a corpus. Global ones hold for every project; the rest for the project that declares them.
/// </summary>
public interface IPrincipleView
{
    string Name { get; }

    string Text { get; }

    bool Global { get; }

    /// <summary><c>&lt;corpus&gt;.beh:&lt;line&gt;</c>, where a human goes to change it.</summary>
    string At { get; }
}
