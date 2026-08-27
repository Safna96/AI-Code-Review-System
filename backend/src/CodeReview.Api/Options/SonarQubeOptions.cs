namespace CodeReview.Api.Options;

public class SonarQubeOptions
{
    public const string SectionName = "SonarQube";

    /// <summary>Base URL of the SonarQube server, e.g. http://localhost:9000</summary>
    public required string BaseUrl { get; set; }

    public required string ApiToken { get; set; }

    /// <summary>SonarQube project key that the target repository is registered under.</summary>
    public required string ProjectKey { get; set; }
}
