using Balenthiran.Kit.Abstractions.Services;

namespace Balenthiran.Kit.Services;

/// <summary>
/// <see cref="ITicketStore"/> for a Kit with no <c>KIT_DATABASE</c>. ⚠️ In memory only, so a restart forgets the
/// tickets, as it forgot the pipeline's work items before BEH-ORCH-11.
/// </summary>
public sealed class MemoryTicketStore : ITicketStore
{
    private readonly Lock gate = new();
    private readonly List<PipelineTicket> tickets = [];

    /// <inheritdoc />
    public void Save(PipelineTicket ticket)
    {
        lock (gate)
        {
            var at = tickets.FindIndex(t => t.Project == ticket.Project && t.Slug == ticket.Slug);
            if (at < 0)
            {
                tickets.Add(ticket);
            }
            else
            {
                tickets[at] = ticket;
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<PipelineTicket> Open()
    {
        lock (gate)
        {
            return tickets.Where(t => t.State is not (TicketState.Merged or TicketState.Stopped)).ToList();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<PipelineTicket> Read(string project)
    {
        lock (gate)
        {
            return tickets.Where(t => t.Project == project).ToList();
        }
    }
}
