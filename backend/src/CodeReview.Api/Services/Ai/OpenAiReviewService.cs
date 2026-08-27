using System.Text.Json;
using CodeReview.Api.Options;
using CodeReview.Core.Models;
using CodeReview.Core.Services;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace CodeReview.Api.Services.Ai;

public class OpenAiReviewService : IOpenAiReviewService
{
    private readonly ChatClient _chatClient;
    private readonly ILogger<OpenAiReviewService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public OpenAiReviewService(IOptions<OpenAiOptions> options, ILogger<OpenAiReviewService> logger)
    {
        _logger = logger;
        _chatClient = new ChatClient(model: options.Value.Model, apiKey: options.Value.ApiKey);
    }

    public async Task<LlmReviewResult> ReviewAsync(
        PullRequestContext context, IReadOnlyList<SonarFinding> sonarFindings, CancellationToken cancellationToken = default)
    {
        var userPrompt = PromptBuilder.BuildUserPrompt(context, sonarFindings);

        var chatOptions = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat(),
            Temperature = 0.1f
        };

        ChatCompletion completion = await _chatClient.CompleteChatAsync(
            messages:
            [
                new SystemChatMessage(PromptBuilder.SystemPrompt),
                new UserChatMessage(userPrompt)
            ],
            options: chatOptions,
            cancellationToken: cancellationToken);

        var rawJson = completion.Content.Count > 0 ? completion.Content[0].Text : null;
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            _logger.LogWarning("GPT-4o returned an empty response for PR #{PullRequestNumber}", context.PullRequestNumber);
            return FallbackResult();
        }

        try
        {
            var result = JsonSerializer.Deserialize<LlmReviewResult>(rawJson, JsonOptions);
            if (result is not null)
            {
                return result;
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse GPT-4o JSON response for PR #{PullRequestNumber}: {Raw}",
                context.PullRequestNumber, rawJson);
        }

        return FallbackResult();
    }

    /// <summary>
    /// Returned when the LLM call fails or its response cannot be parsed, so a single bad
    /// response degrades to "no AI findings" rather than crashing the whole review pipeline.
    /// </summary>
    private static LlmReviewResult FallbackResult() => new()
    {
        Summary = "The AI reviewer could not produce a structured assessment for this change. " +
                  "Only static-analysis findings (if any) are shown below.",
        Confidence = 0
    };
}
