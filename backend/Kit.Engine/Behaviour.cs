using System.Text.Json.Serialization;

namespace Kit.Engine;

// ── Why every property carries an explicit [JsonPropertyOrder] ──────────────
//
// The conformance goldens are compared BYTE-FOR-BYTE, so JSON key order is part
// of the specification, not presentation. In JavaScript that order is the
// object literal's insertion order in `kit.js:36`; here it would otherwise be
// the C# compiler's declaration order, which is an accident of how this file is
// typed and would change silently if someone moved a property while tidying.
//
// Stating the order makes it reviewable and makes a reorder a deliberate edit.
// The orders below are MEASURED from the committed goldens (147 behaviours, 11
// corpora), not read off `kit.js`:
//
//   behaviour   id,title,actor,steps,unknowns,provides,serves,at,asks,options,
//               recommend,against,cites,source,review[,reviewExplicit]
//   step        kind,verb,text,refs,holes,at
//   ref         kind,name          hole      slot
//   unknown     slot,at            provide   kind,name,slot,value,from,at
//   idRef       id,at              option    label,consequence,at
//   recommend   label,why,at       source    origin,ref        review  state,note
//
// ⚠️ `null` is WRITTEN, never omitted: `asks`, `recommend`, `against`, `actor`,
// `source.ref` and `review.note` all appear as `null` in the goldens, because in
// JavaScript the key exists with a null value. The ONE genuinely absent key is
// `reviewExplicit`, which `kit.js:86` assigns only when a `review` line was
// written — measured on 26 of 147 behaviours, always `true`, always last.

/// <summary>One behaviour: the unit a corpus is a list of.</summary>
public sealed class Behaviour
{
    [JsonPropertyOrder(1)]
    public required string Id { get; init; }

    [JsonPropertyOrder(2)]
    public required string Title { get; init; }

    [JsonPropertyOrder(3)]
    public string? Actor { get; set; }

    [JsonPropertyOrder(4)]
    public List<Step> Steps { get; } = [];

    [JsonPropertyOrder(5)]
    public List<Unknown> Unknowns { get; } = [];

    [JsonPropertyOrder(6)]
    public List<Provide> Provides { get; } = [];

    [JsonPropertyOrder(7)]
    public List<IdRef> Serves { get; } = [];

    [JsonPropertyOrder(8)]
    public required string At { get; init; }

    [JsonPropertyOrder(9)]
    public string? Asks { get; set; }

    [JsonPropertyOrder(10)]
    public List<Option> Options { get; } = [];

    [JsonPropertyOrder(11)]
    public Recommendation? Recommend { get; set; }

    [JsonPropertyOrder(12)]
    public string? Against { get; set; }

    [JsonPropertyOrder(13)]
    public List<IdRef> Cites { get; } = [];

    // Default `defined`/`approved` so a corpus written before these existed still
    // parses. The asymmetry is deliberate and is `kit.js:40`'s: an INFERENCE has
    // to say so, because the whole risk is an inference passing itself off as a
    // requirement. Silence means a human wrote it.
    [JsonPropertyOrder(14)]
    public Source Source { get; set; } = new() { Origin = "defined", Ref = null };

    [JsonPropertyOrder(15)]
    public Review Review { get; set; } = new() { State = "approved", Note = null };

    // Absent, not null, when no `review` line was written — see the header.
    [JsonPropertyOrder(16)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ReviewExplicit { get; set; }
}

/// <summary>A step: a verb plus noun references. `?slot` marks a hole.</summary>
public sealed class Step
{
    [JsonPropertyOrder(1)]
    public required string Kind { get; init; }

    [JsonPropertyOrder(2)]
    public required string Verb { get; init; }

    [JsonPropertyOrder(3)]
    public required string Text { get; init; }

    [JsonPropertyOrder(4)]
    public List<Reference> Refs { get; init; } = [];

    [JsonPropertyOrder(5)]
    public List<Hole> Holes { get; init; } = [];

    [JsonPropertyOrder(6)]
    public required string At { get; init; }
}

/// <summary>A noun reference inside a step. `kind` is `literal` for a quoted string.</summary>
public sealed class Reference
{
    [JsonPropertyOrder(1)]
    public required string Kind { get; init; }

    [JsonPropertyOrder(2)]
    public required string Name { get; init; }
}

/// <summary>A hole as it appears on a step: the slot name alone.</summary>
public sealed class Hole
{
    [JsonPropertyOrder(1)]
    public required string Slot { get; init; }
}

/// <summary>
/// The same hole as it appears on the behaviour's `unknowns`, which carries the
/// line it was found on. Deliberately a separate type from <see cref="Hole"/>:
/// the two shapes differ by exactly one key in the goldens, and sharing one type
/// with an optional `at` would make a missing `at` serialise as `null` rather
/// than be absent, which is a different document.
/// </summary>
public sealed class Unknown
{
    [JsonPropertyOrder(1)]
    public required string Slot { get; init; }

    [JsonPropertyOrder(2)]
    public required string At { get; init; }
}

/// <summary>Where an inference is written down, so an approve/deny has something to point at.</summary>
public sealed class Provide
{
    [JsonPropertyOrder(1)]
    public required string Kind { get; init; }

    [JsonPropertyOrder(2)]
    public required string Name { get; init; }

    [JsonPropertyOrder(3)]
    public required string Slot { get; init; }

    [JsonPropertyOrder(4)]
    public required List<string> Value { get; init; }

    [JsonPropertyOrder(5)]
    public required string From { get; init; }

    [JsonPropertyOrder(6)]
    public required string At { get; init; }
}

/// <summary>A reference to another behaviour by id — what `serves` and `cites` both are.</summary>
public sealed class IdRef
{
    [JsonPropertyOrder(1)]
    public required string Id { get; init; }

    [JsonPropertyOrder(2)]
    public required string At { get; init; }
}

/// <summary>A choice AND what changes if it is taken. The consequence is not decoration.</summary>
public sealed class Option
{
    [JsonPropertyOrder(1)]
    public required string Label { get; init; }

    [JsonPropertyOrder(2)]
    public required string Consequence { get; init; }

    [JsonPropertyOrder(3)]
    public required string At { get; init; }
}

/// <summary>A recommendation and why. One per behaviour, or none.</summary>
public sealed class Recommendation
{
    [JsonPropertyOrder(1)]
    public required string Label { get; init; }

    [JsonPropertyOrder(2)]
    public required string Why { get; init; }

    [JsonPropertyOrder(3)]
    public required string At { get; init; }
}

/// <summary>Whether a human wrote this behaviour or something inferred it, and from what.</summary>
public sealed class Source
{
    [JsonPropertyOrder(1)]
    public required string Origin { get; init; }

    [JsonPropertyOrder(2)]
    public required string? Ref { get; init; }
}

/// <summary>Whether anyone has ruled on this behaviour, and what they said.</summary>
public sealed class Review
{
    [JsonPropertyOrder(1)]
    public required string State { get; init; }

    [JsonPropertyOrder(2)]
    public required string? Note { get; init; }
}
