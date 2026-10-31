using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Exceptions;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.DataModels.Models;

namespace Balenthiran.Kit.Services;

/// <summary>
/// Ported from <c>project.js</c>'s <c>project</c> and <c>ui.js</c>'s <c>summary</c>:
/// parse, resolve, generate and report one corpus, in the shape the page reads.
/// Recomputed on every read, never cached — the page re-reads after a write and
/// must see the REGENERATED test.
///
/// Coverage (BEH-HEALTH-1) comes from <see cref="ICoverageStore"/>, which hosted Kit fills in the
/// background from the repo a corpus's <c># kit:repo</c> line names; local Kit has no store and
/// reports it unavailable, as <c>project.js</c> does without <c>--repo</c>.
/// </summary>
public sealed class ProjectViewer(
    ICorpusDirectory corpora,
    ICorpusParser parser,
    IBehaviourResolver resolver,
    ITestGenerator generator,
    IProjectReporter reporter,
    ICoverageStore? coverage = null,
    ICoverageCounter? counter = null) : IProjectViewer
{
    private const string NoRepo = "no --repo given, so no test files were read";
    private const string NoDirective = "no `# kit:repo` line names where this app's tests live";
    private const string NotReadYet = "not read yet";

    private readonly ICoverageCounter counts = counter ?? new CoverageCounter(new TestTitleReader());

    // `/^#\s*kit:not-a-real-app\b/m` and `/^#\s*kit:duplicate-corpus\s+(\S+)/m`, in
    // JavaScript's terms: `^` under /m starts after ANY line terminator, `\s` is the
    // parser's JavaScript whitespace class, and `\b` after `p` is a non-word ASCII
    // character or the end.
    private static readonly Regex NotReal = new(
        $@"(?:^|(?<=[\n\r\u2028\u2029]))#[{CorpusParser.Ws}]*kit:not-a-real-app(?![A-Za-z0-9_])", RegexOptions.Compiled);

    // kit#284 slice 4, the same shape as the two directives either side of it.
    private static readonly Regex Idea = new(
        $@"(?:^|(?<=[\n\r  ]))#[{CorpusParser.Ws}]*kit:idea(?![A-Za-z0-9_-])", RegexOptions.Compiled);

    private static readonly Regex DuplicateOf = new(
        $@"(?:^|(?<=[\n\r\u2028\u2029]))#[{CorpusParser.Ws}]*kit:duplicate-corpus[{CorpusParser.Ws}]+([^{CorpusParser.Ws}]+)", RegexOptions.Compiled);

    /// <inheritdoc />
    public IProjectView View(string app)
    {
        // ui.js's projectOf reports ANY throw from project() as this project's could-not-look.
        // Each narrower catch this port tried left a door: a binding the write route accepted
        // (a non-string route, a lone surrogate) or a bindings file that arrived by git took
        // down GET /api/projects for every reader. An I/O error's message names a server path,
        // and the list is unauthenticated, so that reason is said without it.
        try
        {
            return Project(app);
        }
        catch (ProjectionFailedException)
        {
            throw;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new ProjectionFailedException($"{app}: the corpus or its bindings could not be read", e);
        }
        catch (Exception e)
        {
            throw new ProjectionFailedException(e.Message, e);
        }
    }

    private ProjectView Project(string app)
    {
        var src = corpora.Read(app);
        var corpus = parser.ParseCorpus(src, $"{app}.beh");
        var resolution = resolver.Resolve(corpus.Behaviours);
        var behaviours = resolution.Behaviours.Cast<Behaviour>().ToList();
        if (behaviours.Count == 0)
        {
            throw new ProjectionFailedException($"{app}.beh parsed to zero behaviours");
        }

        var bindings = corpora.Bindings(app);
        var symbols = resolution.Symbols.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
        var generated = behaviours.Select(b =>
        {
            var g = generator.Generate(b, bindings, symbols);
            return new GeneratedView { Id = b.Id, Code = g.Code, Missing = g.Missing.ToList(), Stats = (GenerateStats)g.Stats };
        }).ToList();

        var report = (EngineReport)reporter.Report(resolution.Behaviours, resolution.Conflicts, bindings);
        var corpusNouns = CorpusNouns();
        NounView Decorate(NounRequirement n) => new()
        {
            Noun = n.Noun,
            Kind = n.Kind,
            Name = n.Name,
            UsedBy = n.UsedBy,
            Bound = n.Bound,
            Satisfied = n.Satisfied,
            Needs = n.Needs,
            Binding = n.Bound ? bindings[n.Noun]?.DeepClone() : null,
            SharedWith = corpusNouns.Where(c => c.App != app && c.Nouns.Contains(n.Noun)).Select(c => c.App).Order(StringComparer.Ordinal).ToList(),
        };

        return new ProjectView
        {
            App = app,
            Corpus = corpora.RelativePath(app),
            NotReal = NotReal.IsMatch(src),
            DuplicateOf = DuplicateOf.Match(src) is { Success: true } m ? m.Groups[1].Value : null,
            Behaviours = behaviours.Select(b => new BehaviourView
            {
                Id = b.Id,
                Title = b.Title,
                Actor = b.Actor,
                Steps = b.Steps.Select(s => new StepView { Kind = s.Kind, Verb = s.Verb, Text = s.Text, Refs = s.Refs, Holes = s.Holes }).ToList(),
                Open = (b.Open ?? []).Select(h => h.Key).ToList(),
                Filled = (b.Filled ?? []).Select(f => new FilledView { Key = f.Key, Value = f.Value }).ToList(),
                Source = b.Source,
                Review = b.Review,
                Asks = b.Asks,
                At = b.At,
                Pending = b.Pending,
                Layer = b.Layer,
                In = (b.In ?? []).Select(t => t.Id).ToList(),
                Priority = b.Priority,
                Paused = b.Paused,
                PausedUntil = b.PausedUntil,
                NeedsContext = b.NeedsContext,
                OnDev = b.OnDev,
            }).ToList(),
            Conflicts = resolution.Conflicts.Cast<Conflict>().ToList(),
            Generated = generated,
            Coverage = Coverage(app, src, resolution.Behaviours),
            Adjudication = report.Adjudication,
            Surface = report.Surface,
            Questions = report.Questions,
            Requires = new RequiresView
            {
                Nouns = report.Requires.Nouns.Select(Decorate).ToList(),
                Missing = report.Requires.Missing.Select(Decorate).ToList(),
                Insufficient = report.Requires.Insufficient.Select(Decorate).ToList(),
                Satisfied = report.Requires.Satisfied,
            },
            Features = corpus.Features.Select(f => new FeatureView
            {
                Id = f.Id, Title = f.Title, In = f.In.Select(t => t.Id).ToList(), At = f.At, Stage = f.Stage,
                Epics = f.Epics.Count > 0 ? f.Epics.Select(t => t.Id).ToList() : null,
            }).ToList(),
            Epics = corpus.Epics.Count > 0 ? corpus.Epics.Select(e => new Epic { Id = e.Id, Title = e.Title, At = e.At }).ToList() : null,
            Decisions = corpus.Decisions.Count > 0
                ? corpus.Decisions.Select(d => new DecisionView
                {
                    Id = d.Id, Title = d.Title, Part = d.Part, Chosen = d.Chosen,
                    Considered = d.Considered.ToList(), Because = d.Because.ToList(),
                    Review = new Review { State = d.Review.State, Note = d.Review.Note },
                    Source = new Source { Origin = d.Source.Origin, Ref = d.Source.Ref },
                    At = d.At,
                }).ToList()
                : null,
            // kit#237: files naming the same project are shown as one; no directive, its own.
            Project = corpus.Project ?? app,
            Idea = Idea.IsMatch(src) ? true : null,
            Principles = Principles(src, app) is { Count: > 0 } p ? p : null,
        };
    }

    /// <summary>
    /// BEH-HEALTH-1: the app's coverage from what the store last read of its tests repo. Local Kit
    /// has no store and keeps <c>project.js</c>'s sentence. Every other way of not knowing says
    /// which — a missing line, a repo not read yet, a failed read, a missing mapping, a refusal —
    /// because a bare "unavailable" cannot be acted on.
    /// </summary>
    private object Coverage(string app, string src, IReadOnlyList<IBehaviour> behaviours)
    {
        if (coverage is null)
        {
            return Unavailable(NoRepo);
        }

        var (repo, problem) = RepoDirective.Read(src);
        if (repo is null)
        {
            return Unavailable(problem ?? NoDirective);
        }

        var tests = corpora.ReadTestsText(app);
        if (tests is null)
        {
            return Unavailable($"no {app}.tests.json mapping");
        }

        if (coverage.Reading(repo) is not { } reading)
        {
            return Unavailable(coverage.Error(repo) ?? NotReadYet);
        }

        if (reading.Refusal is not null)
        {
            return Unavailable(reading.Refusal);
        }

        JsonObject map;
        try
        {
            map = JsonNode.Parse(tests.TrimStart('﻿')) as JsonObject ?? throw new JsonException("not an object");
        }
        catch (JsonException)
        {
            return Unavailable($"{app}.tests.json is not a JSON object");
        }

        var counted = counts.Count(behaviours, map, reading.Titles, $"{app}.tests.json");
        return counted.Refusal is { } refusal
            ? Unavailable(refusal)
            : new AvailableCoverage
            {
                Available = true,
                Covered = counted.Covered.Select(b => b.Id).ToList(),
                Uncovered = counted.Uncovered.Select(b => b.Id).ToList(),
                LastImprovedBy = CoverageCounter.ReadLastImprovedBy(map),
            };
    }

    private static UnavailableCoverage Unavailable(string reason) => new() { Available = false, Reason = reason };

    // kit#284 slice 5: `# kit:principle [global] <Name>: <what it means>`, one per line.
    private static readonly Regex PrincipleLine = new(
        $@"^#[{CorpusParser.Ws}]*kit:principle(?![A-Za-z0-9_-])(.*)\z", RegexOptions.Compiled);

    private static readonly Regex PrincipleRest = new(
        $@"^[{CorpusParser.Ws}]+(global[{CorpusParser.Ws}]+)?([^:]+?)[{CorpusParser.Ws}]*:[{CorpusParser.Ws}]*(\S.*?)[{CorpusParser.Ws}]*\z", RegexOptions.Compiled);

    /// <summary>
    /// The file's principles in order. A `kit:principle` line that does not say a name and what it
    /// means is REFUSED rather than skipped — a principle that silently vanished is one nobody
    /// knows they lost, which is the failure this whole feature exists to prevent.
    /// </summary>
    private static List<PrincipleView> Principles(string src, string app)
    {
        var found = new List<PrincipleView>();
        var lines = src.TrimStart('﻿').Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var m = PrincipleLine.Match(lines[i]);
            if (!m.Success)
            {
                continue;
            }

            var rest = PrincipleRest.Match(m.Groups[1].Value);
            if (!rest.Success)
            {
                throw new ProjectionFailedException($"{app}.beh:{i + 1}: kit:principle wants \"[global] <Name>: <what it means>\", got: {m.Groups[1].Value.Trim()}");
            }

            found.Add(new PrincipleView
            {
                Name = rest.Groups[2].Value.Trim(),
                Text = rest.Groups[3].Value,
                Global = rest.Groups[1].Success,
                At = $"{app}.beh:{i + 1}",
            });
        }

        return found;
    }

    /// <inheritdoc />
    public object Summary(string app)
    {
        ProjectView p;
        try
        {
            p = (ProjectView)View(app);
        }
        catch (ProjectionFailedException e)
        {
            return new ProjectError { App = app, Error = e.Message };
        }

        return new ProjectSummary
        {
            App = app,
            Corpus = p.Corpus,
            NotReal = p.NotReal,
            DuplicateOf = p.DuplicateOf,
            Behaviours = p.Behaviours.Count,
            Conflicts = p.Conflicts.Count,
            Coverage = p.Coverage is AvailableCoverage a
                ? new CoverageSummary { Available = true, Covered = a.Covered.Count, Uncovered = a.Uncovered.Count, Reason = null, LastImprovedBy = a.LastImprovedBy }
                : new CoverageSummary { Available = false, Covered = null, Uncovered = null, Reason = ((UnavailableCoverage)p.Coverage).Reason },
            Unreviewed = p.Adjudication.Unreviewed.Count,
            Project = p.Project,
            Idea = p.Idea,
            // Only the primary file (named for its project) can grant: see PromoteDirective.
            Promote = p.Project == app && PromoteDirective.IsGranted(corpora.Read(app)) ? "self" : null,
        };
    }

    /// <summary>
    /// <c>writer.js</c>'s <c>corpusNouns</c>: every corpus's step-reference nouns. A
    /// corpus that does not parse is SKIPPED — one broken corpus must not block
    /// reading another.
    /// </summary>
    private List<(string App, HashSet<string> Nouns)> CorpusNouns()
    {
        var found = new List<(string, HashSet<string>)>();
        foreach (var app in corpora.Corpora())
        {
            try
            {
                var nouns = parser.Parse(corpora.Read(app), $"{app}.beh")
                    .SelectMany(b => b.Steps).SelectMany(s => s.Refs)
                    .Where(r => r.Kind != "literal").Select(r => $"{r.Kind}:{r.Name}");
                found.Add((app, new HashSet<string>(nouns, StringComparer.Ordinal)));
            }
            // Another corpus that will not parse, or that a git pull removed between the listing
            // and the read, is skipped — as writer.js's corpusNouns does — not this project's failure.
            catch (Exception e) when (e is CorpusParseException or IOException or UnauthorizedAccessException)
            {
            }
        }

        return found;
    }
}
