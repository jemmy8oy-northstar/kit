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
/// Hosted Kit reads no repository, so coverage is always the unavailable shape;
/// <c>project.js</c>'s <c>--repo</c> half is not ported because nothing hosted can reach it.
/// </summary>
public sealed class ProjectViewer(
    ICorpusDirectory corpora,
    ICorpusParser parser,
    IBehaviourResolver resolver,
    ITestGenerator generator,
    IProjectReporter reporter) : IProjectViewer
{
    private const string NoRepo = "no --repo given, so no test files were read";

    // `/^#\s*kit:not-a-real-app\b/m` and `/^#\s*kit:duplicate-corpus\s+(\S+)/m`, in
    // JavaScript's terms: `^` under /m starts after ANY line terminator, `\s` is the
    // parser's JavaScript whitespace class, and `\b` after `p` is a non-word ASCII
    // character or the end.
    private static readonly Regex NotReal = new(
        $@"(?:^|(?<=[\n\r\u2028\u2029]))#[{CorpusParser.Ws}]*kit:not-a-real-app(?![A-Za-z0-9_])", RegexOptions.Compiled);

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
        var parsed = parser.Parse(src, $"{app}.beh");
        var resolution = resolver.Resolve(parsed);
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
            }).ToList(),
            Conflicts = resolution.Conflicts.Cast<Conflict>().ToList(),
            Generated = generated,
            Coverage = new UnavailableCoverage { Available = false, Reason = NoRepo },
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
        };
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
            Coverage = new CoverageSummary { Available = false, Covered = null, Uncovered = null, Reason = p.Coverage.Reason },
            Unreviewed = p.Adjudication.Unreviewed.Count,
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
