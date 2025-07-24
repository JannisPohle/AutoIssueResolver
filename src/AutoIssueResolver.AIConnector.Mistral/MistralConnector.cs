using System.Net.Http.Json;
using System.Text.Json.Nodes;
using AutoIssueResolver.AIConnector.Abstractions.Configuration;
using AutoIssueResolver.AIConnector.Abstractions.Extensions;
using AutoIssueResolver.AIConnector.Abstractions.Models;
using AutoIssueResolver.AIConnector.Mistral.Models;
using AutoIssueResolver.AIConnectors.Base;
using AutoIssueResolver.AIConnectors.Base.UnifiedModels;
using AutoIssueResolver.Persistence.Abstractions.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Response = AutoIssueResolver.AIConnector.Mistral.Models.Response;

namespace AutoIssueResolver.AIConnector.Mistral;

public class MistralConnector(ILogger<MistralConnector> logger, [FromKeyedServices("mistral")] HttpClient httpClient, IOptions<AiAgentConfiguration> configuration, IReportingRepository reportingRepository): AIConnectorBase(logger, configuration, reportingRepository, httpClient)
{
  private const string API_PATH_CHAT = "v1/chat/completions";

  protected override List<AIModels> SupportedModels { get; } = [AIModels.DevstralSmall,];

  protected override async Task<object> CreateRequestObject(Prompt prompt, CancellationToken cancellationToken)
  {
    var schema = prompt.ResponseSchema != null ? new ResponseFormat(new JsonSchema(JsonNode.Parse(prompt.ResponseSchema.ResponseSchemaTextWithAdditionalProperties), "response_schema")) : null;

    var request = new Request(configuration.Value.Model.GetModelName(), [new Message("user", prompt.PromptText)], schema,
                              MAX_OUTPUT_TOKENS);

    if (prompt.SystemPrompt != null)
    {
      request.Messages.Add(new Message("system", prompt.SystemPrompt.SystemPromptText));
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
      logger.LogWarning("No response received from Mistral API. Something seems to have gone wrong with the request.");
      throw new UnsuccessfulResultException("No response received from Mistral API", true);
    }
    var usageMetadata = new UsageMetadata(chatResponse.Usage?.PromptTokens ?? 0, 0, chatResponse.Usage?.TotalTokens ?? 0, chatResponse.Usage?.CompletionTokens ?? 0, 0);

    if (chatResponse.Choices == null || chatResponse.Choices.Count == 0)
    {
      logger.LogWarning("Mistral response has no choices. Something seems to have gone wrong with the request.");
      throw new UnsuccessfulResultException("Mistral response has no choices", true) { UsageMetadata = usageMetadata, };
    }

    var choice = chatResponse.Choices?.FirstOrDefault(choice => choice.Message.Role == "assistant");
    if (choice == null)
    {
      logger.LogWarning("Mistral response has no choice with the assistant role. Something seems to have gone wrong with the request.");
      throw new UnsuccessfulResultException("Mistral response has no choice with assistant role", true) { UsageMetadata = usageMetadata, };
    }

    if (choice.FinishReason != "stop")
    {
      logger.LogWarning("Mistral response finished with reason: {FinishReason}. Something seems to have gone wrong with the request.", choice.FinishReason);
      throw new UnsuccessfulResultException($"Mistral response finished with reason: {choice.FinishReason}", true) { UsageMetadata = usageMetadata, };
    }

    return new AiResponse(choice.Message.Content, usageMetadata);
  }
}