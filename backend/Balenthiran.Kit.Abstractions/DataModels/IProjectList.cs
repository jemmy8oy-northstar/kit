namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>The body of `GET /api/projects`. Rows are a summary or an error, so they are written by runtime type.</summary>
public interface IProjectList
{
    IReadOnlyList<object> Projects { get; }
}
