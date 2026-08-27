namespace CodeReview.Api.Options;

public class GitHubOptions
{
    public const string SectionName = "GitHub";

    /// <summary>Personal access token or GitHub App installation token used by Octokit.</summary>
    public required string AccessToken { get; set; }

    /// <summary>Shared secret configured on the GitHub webhook, used to verify X-Hub-Signature-256.</summary>
    public required string WebhookSecret { get; set; }
}
