using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CodeReview.Api.Options;
using CodeReview.Core.Models;
using Microsoft.Extensions.Options;

namespace CodeReview.Api.Services.SonarQube;

public class SonarQubeService(HttpClient httpClient, IOptions<SonarQubeOptions> options, ILogger<SonarQubeService> logger)
    : ISonarQubeService
{
    private readonly SonarQubeOptions _options = options.Value;

    public async Task<IReadOnlyList<SonarFinding>> GetFindingsForPullRequestAsync(
        int pullRequestNumber, IReadOnlyList<string> changedFilePaths)
    {
        var url = $"api/issues/search?componentKeys={Uri.EscapeDataString(_options.ProjectKey)}" +
                  "&resolved=false&ps=500";

        if (_options.UsePullRequestAnalysis)
        {
            url += $"&pullRequest={pullRequestNumber}";
        }

        SonarIssuesSearchResponse? response;
        try
        {
            response = await httpClient.GetFromJsonAsync<SonarIssuesSearchResponse>(url);
        }
        catch (HttpRequestException ex)
        {
            // SonarQube not reachable, or no analysis has run for this PR yet — degrade
            // gracefully so the LLM review can still proceed on ticket + diff alone.
            logger.LogWarning(ex, "Could not fetch SonarQube findings for PR #{PullRequestNumber}", pullRequestNumber);
            return Array.Empty<SonarFinding>();
        }

        if (response?.Issues is null)
        {
            return Array.Empty<SonarFinding>();
        }

        var changedSet = new HashSet<string>(changedFilePaths, StringComparer.OrdinalIgnoreCase);

        return response.Issues
            .Select(MapToFinding)
            .Where(f => changedSet.Count == 0 || changedSet.Contains(f.FilePath))
            .ToList();
    }

    private static SonarFinding MapToFinding(SonarIssueDto dto) => new()
    {
        RuleKey = dto.Rule,
        Type = dto.Type switch
        {
            "BUG" => SonarIssueType.Bug,
            "VULNERABILITY" => SonarIssueType.Vulnerability,
            "SECURITY_HOTSPOT" => SonarIssueType.SecurityHotspot,
            _ => SonarIssueType.CodeSmell
        },
        Severity = dto.Severity switch
        {
            "BLOCKER" => SonarSeverity.Blocker,
            "CRITICAL" => SonarSeverity.Critical,
            "MAJOR" => SonarSeverity.Major,
            "MINOR" => SonarSeverity.Minor,
            _ => SonarSeverity.Info
        },
        Message = dto.Message,
        // SonarQube reports the component as "<projectKey>:<path>"; strip the prefix.
        FilePath = dto.Component.Contains(':') ? dto.Component[(dto.Component.IndexOf(':') + 1)..] : dto.Component,
        Line = dto.Line
    };

    private class SonarIssuesSearchResponse
    {
        [JsonPropertyName("issues")]
        public List<SonarIssueDto> Issues { get; set; } = new();
    }

    private class SonarIssueDto
    {
        [JsonPropertyName("rule")] public string Rule { get; set; } = "";
        [JsonPropertyName("severity")] public string Severity { get; set; } = "";
        [JsonPropertyName("type")] public string Type { get; set; } = "";
        [JsonPropertyName("message")] public string Message { get; set; } = "";
        [JsonPropertyName("component")] public string Component { get; set; } = "";
        [JsonPropertyName("line")] public int? Line { get; set; }
    }
}
