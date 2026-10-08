using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.DataModels.Models;

namespace Balenthiran.Kit.Services;

/// <summary>
/// Stage 2 of the engine, ported from <c>kit.js</c>'s <c>resolve</c>.
///
/// Two passes. The first builds a symbol table from every <c>provides</c>, keyed
/// <c>kind:Name.slot</c>: the FIRST provider holds the value, a later identical
/// value joins the contributors, and a later different value is recorded as a
/// challenger and never displaces it. The second walks every step's holes and
/// fills each from the table, through the first noun the step names.
///
/// Detection is same-noun only (kit#23): two behaviours that disagree about one
/// thing through DIFFERENT nouns are not seen here, in Node or in this port.
/// </summary>
public sealed class BehaviourResolver : IBehaviourResolver
{
    /// <inheritdoc />
    public IResolution Resolve(IReadOnlyList<IBehaviour> behaviours)
    {
        // The parser's own types, because resolving WRITES to them (see the
        // interface). A foreign IBehaviour has nowhere to put `resolved`, and
        // silently skipping it would generate a hole as unfilled with no error.
        var parsed = behaviours.Select(b => b as Behaviour
            ?? throw new ArgumentException(
                $"resolve takes the behaviours the parser returned; {b.Id} is a {b.GetType().Name}",
                nameof(behaviours))).ToList();

        var symbols = new OrderedDictionary<string, Symbol>(StringComparer.Ordinal);
        foreach (var b in parsed)
        {
            foreach (var p in b.Provides)
            {
                var key = $"{p.Kind}:{p.Name}.{p.Slot}";
                if (!symbols.TryGetValue(key, out var existing))
                {
                    // `contributors` is a NEW list, but `value` is the provide's own
                    // list, shared — as it is in `kit.js`.
                    symbols.Add(key, new Symbol { Value = p.Value, Contributors = [p.From], At = p.At });
                }
                else if (existing.Value.SequenceEqual(p.Value, StringComparer.Ordinal))
                {
                    // Not de-duplicated: a behaviour providing the same value twice is listed twice.
                    existing.Contributors.Add(p.From);
                }
                else
                {
                    (existing.Conflict ??= []).Add(new Challenger { From = p.From, Value = p.Value, At = p.At });
                }
            }
        }

        foreach (var b in parsed)
        {
            var filled = new List<Filled>();
            var open = new List<OpenHole>();
            foreach (var step in b.Steps)
            {
                foreach (var h in step.Holes)
                {
                    // The owner is the first ref that is not a literal — per hole,
                    // but the answer is the same for every hole in the step.
                    var owner = step.Refs.FirstOrDefault(r => r.Kind != "literal");
                    if (owner is null)
                    {
                        open.Add(new UnownedOpenHole { Slot = h.Slot, Key = $"?{h.Slot}", At = step.At });
                        continue;
                    }

                    var key = $"{owner.Kind}:{owner.Name}.{h.Slot}";
                    if (symbols.TryGetValue(key, out var sym))
                    {
                        // `from` IS the symbol's contributors list and `resolved`
                        // holds the symbol's value list — shared references, as in
                        // Node. Safe because the table is complete by now.
                        filled.Add(new Filled { Key = key, Value = sym.Value, From = sym.Contributors, At = step.At });
                        (step.Resolved ??= new OrderedDictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal))[h.Slot] = sym.Value;
                    }
                    else
                    {
                        open.Add(new OwnedOpenHole { Key = key, Slot = h.Slot, At = step.At });
                    }
                }
            }

            b.Filled = filled;
            b.Open = open;
        }

        var conflicts = new List<Conflict>();
        foreach (var (key, sym) in symbols)
        {
            if (sym.Conflict is not null)
            {
                conflicts.Add(new Conflict { Key = key, Held = sym.Value, Holders = sym.Contributors, Challengers = sym.Conflict });
            }
        }

        return new Resolution { Behaviours = parsed, Symbols = symbols, Conflicts = conflicts };
    }
}
