namespace CodeReview.Core.Models;

/// <summary>
/// Everything the review pipeline needs to know about a single pull request:
/// where it came from, what it changes, and what it was supposed to implement.
/// </summary>
public record PullRequestContext
{
    public required string Owner { get; init; }
    public required string Repository { get; init; }
    public required int PullRequestNumber { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required string HeadSha { get; init; }
    public required string BaseSha { get; init; }

    /// <summary>Unified diff for all changed files in the pull request.</summary>
    public required string Diff { get; init; }

    /// <summary>
    /// The text of the linked issue/ticket (fetched separately from GitHub Issues,
    /// or extracted from a "Closes #123" / "Fixes #123" reference in the PR body).
    /// Null when no linked ticket could be found.
    /// </summary>
    public string? LinkedTicketDescription { get; init; }

    public IReadOnlyList<string> ChangedFilePaths { get; init; } = Array.Empty<string>();

    /// <summary>Where <see cref="LinkedTicketDescription"/> came from.</summary>
    public TicketSource TicketSource { get; init; } = TicketSource.None;

    /// <summary>
    /// Optional link to the ticket in whatever system owns it - a GitHub issue URL, or
    /// a Jira browse URL pasted alongside manually-supplied requirements. Stored purely
    /// as provenance; nothing is fetched from it.
    /// </summary>
    public string? TicketUrl { get; init; }
}
