namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>BEH-ORCH-16: one comment on a ticket; a question or a block reason is posted as one.</summary>
public sealed record TicketComment(string Project, string Slug, string Author, string Body, DateTimeOffset At);
