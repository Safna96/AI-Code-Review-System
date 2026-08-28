namespace CodeReview.Core.Models;

/// <summary>
/// Where the ticket text used for a review came from.
///
/// Recorded on every report because the evaluation (objective 6) mixes runs driven
/// by real GitHub issues with runs where the requirements were supplied by hand to
/// test prompt sensitivity. Without this, the two are indistinguishable afterwards
/// and the results cannot be separated.
/// </summary>
public enum TicketSource
{
    /// <summary>No ticket text was available - the pull request linked to nothing.</summary>
    None,

    /// <summary>Resolved automatically from a "Closes #N" reference in the pull request body.</summary>
    GitHubIssue,

    /// <summary>Supplied by hand for this run, overriding whatever the pull request linked to.</summary>
    ManualOverride
}
