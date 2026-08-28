namespace CodeReview.Api.Options;

public class SonarQubeOptions
{
    public const string SectionName = "SonarQube";

    /// <summary>Base URL of the SonarQube server, e.g. http://localhost:9000</summary>
    public required string BaseUrl { get; set; }

    public required string ApiToken { get; set; }

    /// <summary>SonarQube project key that the target repository is registered under.</summary>
    public required string ProjectKey { get; set; }

    /// <summary>
    /// Whether to scope the issue query to a specific pull request
    /// (<c>&amp;pullRequest=N</c>).
    ///
    /// Pull request analysis is a Developer Edition feature: on Community Build the
    /// scanner refuses <c>sonar.pullrequest.*</c> outright, so no PR-scoped analysis
    /// can exist and querying for one always returns zero issues. Left false, the
    /// query returns the project's current issues instead, which are then filtered
    /// to the files the pull request touched.
    ///
    /// The trade-off: that filter is per-file, not per-line, so a pre-existing issue
    /// on an untouched line of a changed file can still surface. Set to true only on
    /// Developer Edition or above, where true diff-scoped analysis is available.
    /// </summary>
    public bool UsePullRequestAnalysis { get; set; }
}
