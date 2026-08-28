using System.ClientModel;
using System.Text.Json;
using CodeReview.Api.Options;
using CodeReview.Core.Models;
using CodeReview.Core.Services;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;

namespace CodeReview.Api.Services.Ai;

public class OpenAiReviewService : IOpenAiReviewService
{
    private readonly ChatClient _chatClient;
    private readonly ILogger<OpenAiReviewService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const int MaxAttempts = 5;

    public OpenAiReviewService(IOptions<OpenAiOptions> options, ILogger<OpenAiReviewService> logger)
    {
        _logger = logger;

        // A BaseUrl is only supplied when pointing at an OpenAI-compatible provider
        // other than OpenAI itself (Groq, Gemini's compatibility layer, OpenRouter,
        // a local Ollama). Left unset, the SDK's own default endpoint is used.
        var credential = new ApiKeyCredential(options.Value.ApiKey);
        if (string.IsNullOrWhiteSpace(options.Value.BaseUrl))
        {
            _chatClient = new ChatClient(model: options.Value.Model, credential: credential);
        }
        else
        {
            var clientOptions = new OpenAIClientOptions { Endpoint = new Uri(options.Value.BaseUrl) };
            _chatClient = new ChatClient(model: options.Value.Model, credential: credential, options: clientOptions);
            logger.LogInformation("Using OpenAI-compatible endpoint {BaseUrl} with model {Model}",
                options.Value.BaseUrl, options.Value.Model);
        }
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

        ChatCompletion? completion = await CompleteWithRetryAsync(userPrompt, chatOptions, context.PullRequestNumber, cancellationToken);
        if (completion is null)
        {
            return FallbackResult();
        }

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
    /// Calls the model, retrying transient failures with exponential backoff.
    /// Free-tier endpoints in particular return 503 under load often enough that a
    /// single attempt fails most of the time; without this a review run dies on a
    /// server-side hiccup that would have succeeded a few seconds later.
    /// Returns null once the retries are exhausted, so the caller degrades to a
    /// static-analysis-only review rather than throwing.
    /// </summary>
    private async Task<ChatCompletion?> CompleteWithRetryAsync(
        string userPrompt, ChatCompletionOptions chatOptions, int pullRequestNumber, CancellationToken cancellationToken)
    {
        ChatMessage[] messages =
        [
            new SystemChatMessage(PromptBuilder.SystemPrompt),
            new UserChatMessage(userPrompt)
        ];

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                return await _chatClient.CompleteChatAsync(messages, chatOptions, cancellationToken);
            }
            catch (ClientResultException ex) when (IsTransient(ex.Status) && attempt < MaxAttempts)
            {
                // 2s, 4s, 8s, 16s - long enough for a rate-limit window to roll over,
                // short enough that a review still completes while a PR author waits.
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                _logger.LogWarning(
                    "LLM call for PR #{PullRequestNumber} failed with status {Status} (attempt {Attempt}/{MaxAttempts}); retrying in {Delay}s",
                    pullRequestNumber, ex.Status, attempt, MaxAttempts, delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LLM call for PR #{PullRequestNumber} failed on attempt {Attempt}", pullRequestNumber, attempt);
                return null;
            }
        }

        _logger.LogError("LLM call for PR #{PullRequestNumber} failed after {MaxAttempts} attempts", pullRequestNumber, MaxAttempts);
        return null;
    }

    /// <summary>Server-side or rate-limit failures worth retrying; 4xx client errors are not.</summary>
    private static bool IsTransient(int status) => status is 408 or 429 or 500 or 502 or 503 or 504;

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
