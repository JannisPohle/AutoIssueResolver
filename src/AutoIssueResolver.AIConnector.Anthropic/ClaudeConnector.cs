using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AutoIssueResolver.AIConnector.Abstractions.Configuration;
using AutoIssueResolver.AIConnector.Abstractions.Extensions;
using AutoIssueResolver.AIConnector.Abstractions.Models;
using AutoIssueResolver.AIConnector.Anthropic.Models;
using AutoIssueResolver.AIConnectors.Base;
using AutoIssueResolver.AIConnectors.Base.UnifiedModels;
using AutoIssueResolver.GitConnector.Abstractions;
using AutoIssueResolver.Persistence.Abstractions.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Response = AutoIssueResolver.AIConnector.Anthropic.Models.Response;

namespace AutoIssueResolver.AIConnector.Anthropic;

public class ClaudeConnector(ILogger<ClaudeConnector> logger, IOptions<AiAgentConfiguration> configuration, IReportingRepository reportingRepository, [FromKeyedServices("anthropic")] HttpClient httpClient, ISourceCodeConnector sourceCodeConnector): AIConnectorBase(logger, configuration, reportingRepository, httpClient, sourceCodeConnector)
{
  private const string API_PATH_MESSAGES = "v1/messages";
  //To force Claude to actually return the response in the expected format, we need to prefill i'ts response with the start of the json
  private const string ASSISTANT_MESSAGE_PREFIX = "{ \"replacements\":[{\"filePath\": \"";

  protected override List<AIModels> SupportedModels { get; } = [AIModels.ClaudeHaiku3,];

  public override Task SetupCaching(List<string> rules, CancellationToken cancellationToken = default)
  {
    // Caching is possible, but only makes sense if caches are reused multiple times
    return Task.CompletedTask;
  }

  protected override async Task<object> CreateRequestObject(Prompt prompt, CancellationToken cancellationToken)
  {
    var request = new Request(configuration.Value.Model.GetModelName(), [new Message(await PreparePromptText(prompt, cancellationToken)), new Message(ASSISTANT_MESSAGE_PREFIX, "assistant")], [new SystemPrompt(SYSTEM_PROMPT), new SystemPrompt(GetResponseFormatAsSystemMessage())], MAX_OUTPUT_TOKENS);

    return request;
  }

  protected override string GetResponsesApiPath()
  {
    return API_PATH_MESSAGES;
  }

  protected override async Task<AiResponse> ParseResponse(HttpResponseMessage response, CancellationToken cancellationToken)
  {
    var chatResponse = await response.Content.ReadFromJsonAsync<Response>(cancellationToken);

    if (chatResponse == null)
    {
      logger.LogWarning("No response received from Anthropic API. Something seems to have gone wrong with the request.");
      throw new UnsuccessfulResultException("No response received from Anthropic API", true);
    }

    var usageMetadata = new UsageMetadata(chatResponse.Usage?.InputTokens ?? 0, 0, chatResponse.Usage?.InputTokens ?? 0 + chatResponse.Usage?.OutputTokens ?? 0, chatResponse.Usage?.OutputTokens ?? 0, 0);
    if (chatResponse.StopReason != "end_turn")
    {
      logger.LogWarning("Anthropic response finished with reason: {FinishReason}. Something seems to have gone wrong with the request.", chatResponse.StopReason);
      throw new UnsuccessfulResultException($"Anthropic response finished with reason: {chatResponse.StopReason}", true) { UsageMetadata = usageMetadata, };
    }

    var responseContent = chatResponse.Content?.FirstOrDefault(c => c.Type == "text")?.Text ?? string.Empty;

    if (string.IsNullOrWhiteSpace(responseContent))
    {
      logger.LogWarning("Anthropic response content is empty. Something seems to have gone wrong with the request.");
      throw new UnsuccessfulResultException("Anthropic response content is empty", true) { UsageMetadata = usageMetadata, };
    }

    if (!responseContent.StartsWith(ASSISTANT_MESSAGE_PREFIX))
    {
      responseContent = ASSISTANT_MESSAGE_PREFIX + responseContent;
    }

    return new AiResponse(responseContent, usageMetadata);
  }

  private static string GetResponseFormatAsSystemMessage()
  {
    var sb = new StringBuilder();

    sb.AppendLine("# Output format");
    sb.AppendLine("Respond in json format with the following schema:");
    sb.AppendLine("```json");
    sb.AppendLine(ResponseSchema);
    sb.AppendLine("```");

    return sb.ToString();
  }


  private async Task<string> PreparePromptText(Prompt prompt, CancellationToken cancellationToken)
  {
    var files = await GetFileContents(prompt, cancellationToken);

    var sb = new StringBuilder();

    sb.AppendLine(prompt.PromptText);
    sb.AppendLine();
    sb.AppendLine();

    return FormatFilesForPromptText(files, sb).ToString();
  }
}