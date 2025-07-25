using System.Net.Http.Json;
using System.Text;
using AutoIssueResolver.AIConnector.Abstractions.Configuration;
using AutoIssueResolver.AIConnector.Abstractions.Extensions;
using AutoIssueResolver.AIConnector.Abstractions.Models;
using AutoIssueResolver.AIConnector.Anthropic.Models;
using AutoIssueResolver.AIConnectors.Base;
using AutoIssueResolver.AIConnectors.Base.UnifiedModels;
using AutoIssueResolver.Persistence.Abstractions.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Response = AutoIssueResolver.AIConnector.Anthropic.Models.Response;
using SystemPrompt = AutoIssueResolver.AIConnector.Anthropic.Models.SystemPrompt;

namespace AutoIssueResolver.AIConnector.Anthropic;

public class ClaudeConnector(ILogger<ClaudeConnector> logger, IOptions<AiAgentConfiguration> configuration, IReportingRepository reportingRepository, [FromKeyedServices("anthropic")] HttpClient httpClient): AIConnectorBase(logger, configuration, reportingRepository, httpClient)
{
  private const string API_PATH_MESSAGES = "v1/messages";
  //To force Claude to actually return the response in the expected format, we need to prefill i'ts response with the start of the json
  private const string ASSISTANT_MESSAGE_PREFIX = "{ \"replacements\":[{\"filePath\": \"";

  protected override List<AIModels> SupportedModels { get; } = [AIModels.ClaudeHaiku3,];

  protected override async Task<object> CreateRequestObject(Prompt prompt, CancellationToken cancellationToken)
  {
    var request = new Request(configuration.Value.Model.GetModelName(), [new Message(prompt.PromptText), new Message(ASSISTANT_MESSAGE_PREFIX, "assistant")], [], GetMaxOutputTokens(configuration.Value.Model));

    if (prompt.SystemPrompt != null)
    {
      request.System.Add(new SystemPrompt(prompt.SystemPrompt.SystemPromptText));
    }

    if (prompt.ResponseSchema != null)
    {
      request.System.Add(new SystemPrompt(GetResponseFormatAsSystemMessage(prompt.ResponseSchema.ResponseSchemaText)));
    }

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

  private static string GetResponseFormatAsSystemMessage(string responseSchema)
  {
    var sb = new StringBuilder();

    sb.AppendLine("# Output format");
    sb.AppendLine("Respond in json format with the following schema:");
    sb.AppendLine("```json");
    sb.AppendLine(responseSchema);
    sb.AppendLine("```");

    return sb.ToString();
  }
}