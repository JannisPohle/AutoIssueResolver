using System.Net.Http.Json;
using System.Text.Json.Nodes;
using AutoIssueResolver.AIConnector.Abstractions.Configuration;
using AutoIssueResolver.AIConnector.Abstractions.Extensions;
using AutoIssueResolver.AIConnector.Abstractions.Models;
using AutoIssueResolver.AIConnector.OpenAI.Models;
using AutoIssueResolver.AIConnectors.Base;
using AutoIssueResolver.AIConnectors.Base.UnifiedModels;
using AutoIssueResolver.Persistence.Abstractions.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutoIssueResolver.AIConnector.OpenAI;

public class OpenAIConnector(ILogger<OpenAIConnector> logger, [FromKeyedServices("openAI")] HttpClient httpClient, IOptions<AiAgentConfiguration> configuration, IReportingRepository reportingRepository): AIConnectorBase(logger, configuration, reportingRepository, httpClient)
{
  private const string API_PATH_RESPONSES = "v1/responses";

  protected override List<AIModels> SupportedModels { get; } = [AIModels.GPT4oNano, AIModels.o3, AIModels.o3Mini, AIModels.o4Mini, AIModels.GPT4o, AIModels.GPT41, AIModels.GPT41Mini];

  protected override async Task<object> CreateRequestObject(Prompt prompt, CancellationToken cancellationToken)
  {
    var schema = prompt.ResponseSchema != null ? new TextOptions(new Format("response_schema", JsonNode.Parse(prompt.ResponseSchema.ResponseSchemaTextWithAdditionalProperties))) : null;

    var request = new Request(prompt.PromptText, configuration.Value.Model.GetModelName(), prompt.SystemPrompt?.SystemPromptText, schema, GetMaxOutputTokens(configuration.Value.Model));

    return request;
  }

  protected override string GetResponsesApiPath()
  {
    return API_PATH_RESPONSES;
  }

  protected override async Task<AiResponse> ParseResponse(HttpResponseMessage response, CancellationToken cancellationToken)
  {
    var chatResponse = await response.Content.ReadFromJsonAsync<ResponseRoot>(cancellationToken);

    if (chatResponse == null)
    {
      logger.LogWarning("No response received from OpenAI API. Something seems to have gone wrong with the request.");
      throw new UnsuccessfulResultException("No response received from OpenAI API", true);
    }

    var usageMetadata = new UsageMetadata(chatResponse.Usage?.InputTokens ?? 0, chatResponse.Usage?.InputTokensDetails?.CachedTokens ?? 0, chatResponse.Usage?.TotalTokens ?? 0, chatResponse.Usage?.OutputTokens ?? 0, chatResponse.Usage?.OutputTokensDetails?.ReasoningTokens ?? 0);

    if (!chatResponse.Status.Equals("completed", StringComparison.OrdinalIgnoreCase))
    {
      logger.LogWarning("OpenAI response finished with reason: {FinishReason}. Something seems to have gone wrong with the request.", chatResponse.Status);
      throw new UnsuccessfulResultException($"OpenAI response finished with reason: {chatResponse.Status}", true) {  UsageMetadata = usageMetadata, };
    }

    var responseContent = chatResponse.Output?.FirstOrDefault(o => o.Role == "assistant")?.Content?.FirstOrDefault(c => c.Type == "output_text")?.Text ?? string.Empty;

    if (string.IsNullOrWhiteSpace(responseContent))
    {
      logger.LogWarning("OpenAI response content is empty. Something seems to have gone wrong with the request.");
      throw new UnsuccessfulResultException("OpenAI response content is empty", true) {  UsageMetadata = usageMetadata, };
    }

    return new AiResponse(responseContent, usageMetadata);
  }
}