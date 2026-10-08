using System.Globalization;
using System.Text.Json.Nodes;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.DataModels.Models;

namespace Balenthiran.Kit.Services;

/// <summary>
/// Ported from <c>project.js</c>'s <c>report</c> and the four engine functions it
/// composes: <c>kit.js</c>'s <c>adjudication</c>, <c>surface</c> and <c>questions</c>,
/// and <c>requires.js</c>'s <c>requirements</c>.
///
/// ⚠️ Three orderings, deliberately different, each Node's own: the question sheet
/// is a STABLE sort on tier (List.Sort is unstable, so it is OrderBy); a noun's
/// <c>usedBy</c> and verbs are code-unit order (<c>Array.prototype.sort()</c>); and
/// the noun list itself is <c>localeCompare</c> — ICU collation, where
/// <c>field:name</c> sorts before <c>field:Name</c> and <c>region:a_b</c> before
/// <c>region:ab</c>. That last one is invariant-culture comparison here, which is
/// ICU only while globalization is enabled; under invariant-globalization mode it
/// silently becomes ordinal, and the edge corpus is what goes red.
/// </summary>
public sealed class ProjectReporter : IProjectReporter
{
    private static readonly Dictionary<string, Requirement> Requirements = new(StringComparer.Ordinal)
    {
        ["addressable"] = new(
            "addressable",
            "addressable from a test: a role plus an accessible name (preferred), an associated <label>, or a stable locator",
            b => (b.Get("role").Truthy && b.Get("name").Truthy) || b.Get("label").Truthy || b.Get("locator").Truthy),
        ["label"] = new(
            "label",
            "a form field with an associated <label> — `fills` addresses fields by label only, so a role or a bare locator will not do here",
            b => b.Get("label").Truthy),
        ["route"] = new("route", "a route that serves this page", b => b.Get("route").Truthy),
        ["urlPattern"] = new(
            "urlPattern",
            "a URL that a regex can recognise once navigation has settled — this is NOT the same obligation as the route, and a page reached by both `opens` and `lands` owes both",
            b => b.Get("urlPattern").Truthy),
        ["state"] = new(
            "state",
            "a way to put the app into this state before the test acts — a seed, a fixture, or a route mock. This is a precondition, not a thing on screen",
            b => b.Get("state").Truthy),
        ["fixture"] = new("fixture", "an upload fixture: a file name and a mime type", b => b.Get("fixture").Truthy),
    };

    /// <inheritdoc />
    public IEngineReport Report(IReadOnlyList<IBehaviour> behaviours, IReadOnlyList<IConflict> conflicts, JsonObject bindings)
    {
        var parsed = behaviours.Select(b => b as Behaviour
            ?? throw new ArgumentException(
                $"report takes the behaviours the parser returned; {b.Id} is a {b.GetType().Name}",
                nameof(behaviours))).ToList();
        var resolved = conflicts.Select(c => c as Conflict
            ?? throw new ArgumentException($"report takes the conflicts resolve returned; {c.Key} is a {c.GetType().Name}", nameof(conflicts))).ToList();

        return new EngineReport
        {
            Adjudication = Adjudication(parsed),
            Surface = Surface(parsed, out _),
            Questions = Questions(parsed, resolved),
            Requires = RequiresOf(parsed, bindings),
        };
    }

    private static AdjudicationReport Adjudication(List<Behaviour> behaviours)
    {
        var inferred = behaviours.Where(b => b.Source.Origin == "inferred").ToList();
        return new AdjudicationReport
        {
            Defined = behaviours.Count(b => b.Source.Origin == "defined"),
            Inferred = inferred.Count,
            Unreviewed = inferred.Where(b => b.Review.State == "unreviewed").Select(b => b.Id).ToList(),
            Approved = inferred.Where(b => b.Review.State == "approved").Select(b => b.Id).ToList(),

            // Every behaviour, not only inferred ones — as in Node.
            Denied = behaviours.Where(b => b.Review.State == "denied").Select(b => b.Id).ToList(),

            // `!b.source.ref`: an empty reference is as untraceable as none.
            Untraceable = behaviours.Where(b => string.IsNullOrEmpty(b.Source.Ref)).Select(b => b.Id).ToList(),
        };
    }

    private static SurfaceReport Surface(List<Behaviour> behaviours, out HashSet<string> unserved)
    {
        var byId = ById(behaviours);
        var errors = new List<string>();
        foreach (var b in behaviours)
        {
            foreach (var s in b.Serves)
            {
                if (!byId.TryGetValue(s.Id, out var target))
                {
                    errors.Add($"{s.At}: {b.Id} serves {s.Id}, which is not in the corpus");
                }
                else if (target.Source.Origin != "defined")
                {
                    errors.Add($"{s.At}: {b.Id} serves {s.Id}, which is itself inferred — the chain must end at a defined behaviour");
                }

                if (b.Source.Origin == "defined")
                {
                    errors.Add($"{s.At}: {b.Id} is defined, so it is served rather than serving — drop the serves line");
                }
            }
        }

        var inferred = behaviours.Where(b => b.Source.Origin == "inferred").ToList();
        var unservedIds = inferred.Where(b => b.Serves.Count == 0).Select(b => b.Id).ToList();
        unserved = new HashSet<string>(unservedIds, StringComparer.Ordinal);
        return new SurfaceReport
        {
            Errors = errors,
            Served = inferred.Where(b => b.Serves.Count > 0).Select(b => b.Id).ToList(),
            Unserved = unservedIds,
        };
    }

    private static List<object> Questions(List<Behaviour> behaviours, List<Conflict> conflicts)
    {
        var byId = ById(behaviours);
        Surface(behaviours, out var unserved);

        // What a question cites is its evidence, never also its own row.
        var citedBy = new HashSet<string>(behaviours.SelectMany(b => b.Cites).Select(c => c.Id), StringComparer.Ordinal);
        List<Citation> Evidence(Behaviour b) => b.Cites.Select(c =>
        {
            var t = byId.GetValueOrDefault(c.Id);
            return new Citation
            {
                Id = c.Id,
                Title = t?.Title ?? c.Id,
                Ref = t?.Source.Ref,
                Contracts = t is null ? [] : Contracts(t),
            };
        }).ToList();

        var rows = new List<(string Tier, object Question)>();
        foreach (var c in conflicts)
        {
            var sides = c.Holders.Concat(c.Challengers.Select(x => x.From)).ToList();

            // The question can be authored on either side of the collision.
            var owner = sides.Select(byId.GetValueOrDefault).FirstOrDefault(b => b is not null && !string.IsNullOrEmpty(b.Asks));
            rows.Add(("decision", new ConflictQuestion
            {
                Kind = "conflict",
                Tier = "decision",
                Key = c.Key,
                Title = $"Two behaviours disagree about {c.Key}",
                Sides = sides.Select(id =>
                {
                    var b = byId.GetValueOrDefault(id);
                    var ch = c.Challengers.FirstOrDefault(x => x.From == id);
                    return new QuestionSide { Id = id, Title = b?.Title ?? id, Ref = b?.Source.Ref, Value = ch?.Value ?? c.Held };
                }).ToList(),
                Held = c.Held,
                Challengers = c.Challengers,
                Asks = owner?.Asks,
                Options = owner?.Options ?? [],
                Recommend = owner?.Recommend,
                Against = owner?.Against,
                Owner = owner?.Id,
                Cites = owner is null ? [] : Evidence(owner),
            }));
        }

        foreach (var b in behaviours)
        {
            // A defined behaviour is not up for adjudication, and a cited one is
            // already on the page inside the question it is evidence for.
            if (b.Source.Origin != "inferred" || b.Review.State != "unreviewed" || citedBy.Contains(b.Id))
            {
                continue;
            }

            var detected = unserved.Contains(b.Id);
            var tier = detected || !string.IsNullOrEmpty(b.Asks) ? "decision" : "review";
            rows.Add((tier, new BehaviourQuestion
            {
                Kind = detected ? "unserved" : "review",
                Tier = tier,
                Key = b.Id,
                Id = b.Id,
                Title = b.Title,
                Source = b.Source,
                Serves = b.Serves.Select(s => s.Id).ToList(),
                Contracts = Contracts(b),
                Asks = b.Asks,
                Options = b.Options,
                Recommend = b.Recommend,
                Against = b.Against,
                Cites = Evidence(b),
            }));
        }

        // Stable, as Array.prototype.sort is: within a tier, corpus order.
        return rows.OrderBy(r => r.Tier == "decision" ? 0 : 1).Select(r => r.Question).ToList();
    }

    private static RequiresReport RequiresOf(List<Behaviour> behaviours, JsonObject bindings)
    {
        var byNoun = new OrderedDictionary<string, (string Kind, string Name, OrderedDictionary<string, HashSet<string>> Needs, HashSet<string> UsedBy)>(StringComparer.Ordinal);
        foreach (var b in behaviours)
        {
            foreach (var step in b.Steps)
            {
                foreach (var (noun, kind, name, req) in RequirementsOf(step))
                {
                    if (!byNoun.TryGetValue(noun, out var entry))
                    {
                        entry = (kind, name, new(StringComparer.Ordinal), new(StringComparer.Ordinal));
                        byNoun.Add(noun, entry);
                    }

                    entry.UsedBy.Add(b.Id);
                    if (!entry.Needs.TryGetValue(req, out var verbs))
                    {
                        verbs = new HashSet<string>(StringComparer.Ordinal);
                        entry.Needs.Add(req, verbs);
                    }

                    verbs.Add(step.Verb);
                }
            }
        }

        var nouns = byNoun.Select(kv =>
        {
            // An own property, and JSON null counts as no binding at all.
            var binding = bindings.TryGetPropertyValue(kv.Key, out var v) && v is not null ? new JsValue(true, v) : (JsValue?)null;
            var needs = kv.Value.Needs.Select(n => new Need
            {
                Id = n.Key,
                Surface = Requirements[n.Key].Surface,
                Verbs = n.Value.Order(StringComparer.Ordinal).ToList(),
                Met = binding is { } bv && bv.Truthy && Requirements[n.Key].Satisfied(bv),
            }).ToList();
            return new NounRequirement
            {
                Noun = kv.Key,
                Kind = kv.Value.Kind,
                Name = kv.Value.Name,
                UsedBy = kv.Value.UsedBy.Order(StringComparer.Ordinal).ToList(),
                Bound = binding is not null,
                Satisfied = needs.All(n => n.Met),
                Needs = needs,
            };
        }).OrderBy(n => n.Noun, StringComparer.Create(CultureInfo.InvariantCulture, CompareOptions.None)).ToList();

        return new RequiresReport
        {
            Nouns = nouns,
            Missing = nouns.Where(n => !n.Bound).ToList(),
            Insufficient = nouns.Where(n => n.Bound && !n.Satisfied).ToList(),
            Satisfied = nouns.Where(n => n.Satisfied).Select(n => n.Noun).ToList(),
        };
    }

    /// <summary><c>requirementsOf(step)</c>: every obligation one step places on the app.</summary>
    private static IEnumerable<(string Noun, string Kind, string Name, string Req)> RequirementsOf(Step step)
    {
        // `fills form:X` names the FORM; the obligation falls on the fields resolve wrote
        // onto it. `fills field:X with …` names its field, so the obligation is that noun's (kit#151).
        if (step.Verb == "fills")
        {
            var field = step.Refs.FirstOrDefault(r => r.Kind == "field");
            if (field is not null)
            {
                yield return ($"field:{field.Name}", "field", field.Name, "label");
                yield break;
            }

            foreach (var f in step.Resolved?.GetValueOrDefault("fields") ?? [])
            {
                yield return ($"field:{f}", "field", f, "label");
            }

            yield break;
        }

        var nouns = step.Refs.Where(r => r.Kind != "literal").ToList();
        IEnumerable<(Reference Ref, string Req)> picks = step.Verb switch
        {
            "state" => nouns.Take(1).Select(r => (r, "state")),
            "opens" => nouns.Take(1).Select(r => (r, "route")),
            "lands" => nouns.Take(1).Select(r => (r, "urlPattern")),
            "activates" or "sees" or "shows" => nouns.Take(1).Select(r => (r, "addressable")),
            "attaches" => nouns.Where(n => n.Kind == "file").Select(r => (r, "fixture"))
                .Concat(nouns.Where(n => n.Kind == "field").Select(r => (r, "addressable"))),
            _ => [],
        };
        foreach (var (r, req) in picks)
        {
            yield return ($"{r.Kind}:{r.Name}", r.Kind, r.Name, req);
        }
    }

    private static List<string> Contracts(Behaviour b) => b.Steps.Where(s => s.Kind == "contract").Select(s => s.Text).ToList();

    // `new Map(behaviours.map(b => [b.id, b]))`: a later duplicate id wins.
    private static Dictionary<string, Behaviour> ById(List<Behaviour> behaviours)
    {
        var byId = new Dictionary<string, Behaviour>(StringComparer.Ordinal);
        foreach (var b in behaviours)
        {
            byId[b.Id] = b;
        }

        return byId;
    }

    private sealed record Requirement(string Id, string Surface, Func<JsValue, bool> Satisfied);
}
