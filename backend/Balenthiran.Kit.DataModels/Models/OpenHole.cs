using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

// ── Why two subclasses for one shape ───────────────────────────────────────
//
// `kit.js`'s `resolve` writes an open hole with TWO DIFFERENT KEY ORDERS:
//
//   no noun owns the hole   { slot, key: '?slot', at }
//   a noun owns it          { key, slot, at }
//
// Key order is output (the goldens are byte-compared), and one C# class can have
// only one `[JsonPropertyOrder]`. The unowned shape is in NO golden — measured: 0
// of 5 open holes across all eleven — so it is scored by `ResolveEdgeTests`'
// Node-generated fixture instead, and would otherwise be the one branch nothing
// checked. `[JsonDerivedType]` with no discriminator serialises the runtime type's
// contract and writes no `$type`.

/// <summary>A hole nothing filled.</summary>
[JsonDerivedType(typeof(OwnedOpenHole))]
[JsonDerivedType(typeof(UnownedOpenHole))]
public abstract class OpenHole : IOpenHole
{
    public abstract string Key { get; init; }

    public abstract string Slot { get; init; }

    public abstract string At { get; init; }
}
