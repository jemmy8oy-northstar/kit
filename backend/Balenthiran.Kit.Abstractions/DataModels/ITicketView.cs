namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>One comment on a ticket on <c>GET /api/projects/:project/tickets</c> (BEH-ORCH-16): a question, a block reason, or a reply.</summary>
public interface ITicketCommentView
{
    /// <summary>"kit" for a comment Kit posted; otherwise the name of who wrote it.</summary>
    string Author { get; }

    string Body { get; }

    /// <summary>ISO 8601 UTC.</summary>
    string At { get; }
}

/// <summary>One ticket on <c>GET /api/projects/:project/tickets</c> (BEH-ORCH-16): where it is, whether it is blocked, and its thread.</summary>
public interface ITicketView
{
    string Slug { get; }

    string Title { get; }

    /// <summary>new, ready, planned, built, merged, or stopped (a row from before BEH-ORCH-16).</summary>
    string State { get; }

    int Priority { get; }

    /// <summary>The card ids the ticket's branch changes.</summary>
    IReadOnlyList<string> Cards { get; }

    string? PullRequest { get; }

    /// <summary>The blocked tag's reason; null when the ticket is not blocked.</summary>
    string? BlockedBecause { get; }

    /// <summary>Oldest first.</summary>
    IReadOnlyList<ITicketCommentView> Comments { get; }
}

/// <summary>The answer to <c>POST /api/projects/:project/tickets/:slug/comments</c> (BEH-ORCH-16 slice 3).</summary>
public interface ITicketReplyResult
{
    /// <summary>True when the reply woke the first agent on the ticket; false when it is only on the thread.</summary>
    bool Woke { get; }
}

/// <summary>The body of <c>GET /api/projects/:project/tickets</c>: whether the pipeline is on, and the project's tickets, oldest first.</summary>
public interface ITicketList
{
    bool On { get; }

    IReadOnlyList<ITicketView> Tickets { get; }
}
