using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CodeReview.Api.Options;
using CodeReview.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CodeReview.Api.Controllers;

[ApiController]
[Route("api/webhook")]
public class WebhookController(
    ReviewOrchestrator orchestrator,
    IOptions<GitHubOptions> gitHubOptions,
    ILogger<WebhookController> logger) : ControllerBase
{
    private static readonly HashSet<string> RelevantActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "opened", "reopened", "synchronize"
    };

    /// <summary>
    /// Entry point registered as the GitHub webhook URL for `pull_request` events
    /// (see Settings → Webhooks on the target repository).
    /// </summary>
    [HttpPost("github")]
    public async Task<IActionResult> HandleGitHubWebhook()
    {
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync();
        Request.Body.Position = 0;

        if (!IsValidSignature(rawBody, Request.Headers["X-Hub-Signature-256"]))
        {
            logger.LogWarning("Rejected webhook call with invalid signature");
            return Unauthorized();
        }

        var eventName = Request.Headers["X-GitHub-Event"].ToString();
        if (!string.Equals(eventName, "pull_request", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(new { message = $"Ignored event type '{eventName}'" });
        }

        using var payload = JsonDocument.Parse(rawBody);
        var root = payload.RootElement;
        var action = root.GetProperty("action").GetString();
        if (action is null || !RelevantActions.Contains(action))
        {
            return Ok(new { message = $"Ignored action '{action}'" });
        }

        var pullRequestNumber = root.GetProperty("number").GetInt32();
        var owner = root.GetProperty("repository").GetProperty("owner").GetProperty("login").GetString()!;
        var repository = root.GetProperty("repository").GetProperty("name").GetString()!;

        // Fire-and-forget from the webhook's perspective: GitHub expects a fast 2xx response
        // and will retry if the endpoint takes too long, so the actual review runs in the
        // background while we acknowledge receipt immediately.
        _ = Task.Run(async () =>
        {
            try
            {
                await orchestrator.RunAsync(owner, repository, pullRequestNumber);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Review run failed for {Owner}/{Repository}#{PullRequestNumber}", owner, repository, pullRequestNumber);
            }
        });

        return Accepted(new { message = "Review queued", owner, repository, pullRequestNumber });
    }

    private bool IsValidSignature(string rawBody, string? signatureHeader)
    {
        if (string.IsNullOrEmpty(signatureHeader) || !signatureHeader.StartsWith("sha256="))
        {
            return false;
        }

        var secretBytes = Encoding.UTF8.GetBytes(gitHubOptions.Value.WebhookSecret);
        var bodyBytes = Encoding.UTF8.GetBytes(rawBody);
        var computedHash = HMACSHA256.HashData(secretBytes, bodyBytes);
        var computedSignature = "sha256=" + Convert.ToHexString(computedHash).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedSignature),
            Encoding.UTF8.GetBytes(signatureHeader));
    }
}
