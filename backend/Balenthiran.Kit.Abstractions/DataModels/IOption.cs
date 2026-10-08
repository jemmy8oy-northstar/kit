namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>A choice AND what changes if it is taken. The consequence is not decoration.</summary>
public interface IOption
{
    string Label { get; }

    string Consequence { get; }

    string At { get; }
}
