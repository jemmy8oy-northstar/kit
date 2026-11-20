namespace Balenthiran.Kit.Services;

/// <summary>What the past sessions on one machine cost: each week's total (keyed by its Monday), how many sessions were
/// counted, and how many were skipped because they carry no reported dollar cost.</summary>
public sealed record PastCostReport(IReadOnlyDictionary<DateOnly, decimal> WeekTotals, int Counted, int Skipped);
