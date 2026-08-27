using CodeReview.Core.Models;

namespace CodeReview.Api.Services.SonarQube;

public interface ISonarQubeService
{
    /// <summary>
    /// Reads the issues SonarQube has already reported for a given pull request analysis.
    /// Assumes `sonar-scanner` has been run against the PR branch (see the GitHub Actions
    /// workflow in the repo root) before this is called — this service does not trigger
    /// scanning itself, it only reads results back via the Web API.
    /// </summary>
    Task<IReadOnlyList<SonarFinding>> GetFindingsForPullRequestAsync(int pullRequestNumber, IReadOnlyList<string> changedFilePaths);
}
