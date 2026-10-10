using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>
/// The question sheet: the document a human opens to rule on what Kit could not. Port of
/// <c>kit.js</c>'s <c>asked</c>, <c>questionErrors</c> and <c>renderSheet</c>.
/// </summary>
public interface IQuestionSheet
{
    /// <summary>
    /// The questions a human wrote on a DEFINED behaviour (kit#73). Kit did not detect them and
    /// cannot rank them, so they are kept apart from <see cref="IEngineReport.Questions"/>.
    /// </summary>
    IReadOnlyList<IQuestion> Asked(IReadOnlyList<IBehaviour> behaviours, IReadOnlyList<IConflict> conflicts);

    /// <summary>Every way a question is half-written. A sheet that ships one looks worked through, so any is a refusal.</summary>
    IReadOnlyList<string> Errors(IEnumerable<IQuestion> questions);

    /// <summary>The sheet, as markdown. <paramref name="rev"/> names the app revision the corpus was read from, or is empty.</summary>
    string Render(string app, IReadOnlyList<IQuestion> questions, IReadOnlyList<IQuestion> asked, string rev);
}
