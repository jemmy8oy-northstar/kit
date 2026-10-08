using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

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
public sealed class Behaviour : IBehaviour
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

    // Written by `resolve` and absent before it, for the same reason as
    // `Step.Resolved`. `kit.js` returns `{ ...b, filled, open }`, so they follow
    // every parsed key — including `reviewExplicit` — in that order.
    [JsonPropertyOrder(17)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Filled>? Filled { get; set; }

    [JsonPropertyOrder(18)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<OpenHole>? Open { get; set; }

    // The interface view. Explicit, so System.Text.Json — which serialises
    // public members only — never sees it, and the wire shape stays the
    // concrete properties above with their stated order.
    IReadOnlyList<IStep> IBehaviour.Steps => Steps;
    IReadOnlyList<IUnknown> IBehaviour.Unknowns => Unknowns;
    IReadOnlyList<IProvide> IBehaviour.Provides => Provides;
    IReadOnlyList<IIdRef> IBehaviour.Serves => Serves;
    IReadOnlyList<IOption> IBehaviour.Options => Options;
    IRecommendation? IBehaviour.Recommend => Recommend;
    IReadOnlyList<IIdRef> IBehaviour.Cites => Cites;
    ISource IBehaviour.Source => Source;
    IReview IBehaviour.Review => Review;
    IReadOnlyList<IFilled>? IBehaviour.Filled => Filled;
    IReadOnlyList<IOpenHole>? IBehaviour.Open => Open;
}
