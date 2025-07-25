using System.Net.Http.Json;
using System.Text;
using AutoIssueResolver.AIConnector.Abstractions.Configuration;
using AutoIssueResolver.AIConnector.Abstractions.Extensions;
using AutoIssueResolver.AIConnector.Abstractions.Models;
using AutoIssueResolver.AIConnector.DeepSeek.Models;
using AutoIssueResolver.AIConnectors.Base;
using AutoIssueResolver.AIConnectors.Base.UnifiedModels;
using AutoIssueResolver.Persistence.Abstractions.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Response = AutoIssueResolver.AIConnector.DeepSeek.Models.Response;

namespace AutoIssueResolver.AIConnector.DeepSeek;

public class DeepSeekConnector(ILogger<DeepSeekConnector> logger, [FromKeyedServices("deepseek")] HttpClient httpClient, IOptions<AiAgentConfiguration> configuration, IReportingRepository reportingRepository): AIConnectorBase(logger, configuration, reportingRepository, httpClient)
{
  private const string API_PATH_CHAT = "chat/completions";
  protected override List<AIModels> SupportedModels { get; } = [AIModels.DeepSeekChat,];

  protected override async Task<object> CreateRequestObject(Prompt prompt, CancellationToken cancellationToken)
  {
    var request = new Request(
                              configuration.Value.Model.GetModelName(),
                              [new Message(prompt.PromptText)],
                              new ResponseFormat(),
                              GetMaxOutputTokens(configuration.Value.Model));

    if (prompt.SystemPrompt != null)
    {
      request.Messages.Add(new Message(prompt.SystemPrompt.SystemPromptText, "system"));
    }

    if (prompt.ResponseSchema != null)
    {
      request.Messages.Add(GetResponseFormatAsSystemMessage(prompt.ResponseSchema.ResponseSchemaText));
    }

    return request;
  }

  protected override string GetResponsesApiPath()
  {
    return API_PATH_CHAT;
  }

  protected override async Task<AiResponse> ParseResponse(HttpResponseMessage response, CancellationToken cancellationToken)
  {
    var chatResponse = await response.Content.ReadFromJsonAsync<Response>(cancellationToken);

    if (chatResponse == null)
    {
      logger.LogWarning("No response received from DeepSeek API. Something seems to have gone wrong with the request.");
      throw new UnsuccessfulResultException("No response received from DeepSeek API", true);
    }

    var usageMetadata = new UsageMetadata(chatResponse.Usage?.PromptTokens ?? 0, chatResponse.Usage?.PromptCacheHitTokens ?? 0, chatResponse.Usage?.TotalTokens ?? 0, chatResponse.Usage?.CompletionTokens ?? 0, chatResponse.Usage?.CompletionTokenDetails?.ReasoningTokens ?? 0);

    var choice = chatResponse.Choices?.OrderBy(choice => choice.Index).FirstOrDefault(choice => choice.Message.Role == "assistant");
    if (choice == null)
    {
      logger.LogWarning("DeepSeek response has no choice with the assistant role. Something seems to have gone wrong with the request.");
      throw new UnsuccessfulResultException("DeepSeek response has no choice with the assistant role", true) { UsageMetadata = usageMetadata, };
    }

    if (choice.FinishReason != "stop")
    {
      logger.LogWarning("DeepSeek response finished with reason: {FinishReason}. Something seems to have gone wrong with the request.", choice.FinishReason);
      throw new UnsuccessfulResultException($"DeepSeek response finished with reason: {choice.FinishReason}", true) { UsageMetadata = usageMetadata, };
    }

    var responseContent = choice.Message.Content ?? string.Empty;
    if (string.IsNullOrWhiteSpace(responseContent))
    {
      logger.LogWarning("DeepSeek response content is empty. Something seems to have gone wrong with the request.");
      throw new UnsuccessfulResultException("DeepSeek response content is empty", true) { UsageMetadata = usageMetadata, };
    }

    return new AiResponse(responseContent, usageMetadata);
  }

  private static Message GetResponseFormatAsSystemMessage(string responseSchema)
  {
    var sb = new StringBuilder();

    sb.AppendLine("# Output format");
    sb.AppendLine("Respond in json format with the following schema:");
    sb.AppendLine("```json");
    sb.AppendLine(responseSchema);
    sb.AppendLine("```");

    return new Message(sb.ToString(), "system");
  }
}