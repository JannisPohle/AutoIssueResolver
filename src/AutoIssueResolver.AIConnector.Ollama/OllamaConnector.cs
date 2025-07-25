using System.Net.Http.Json;
using System.Text.Json.Nodes;
using AutoIssueResolver.AIConnector.Abstractions.Configuration;
using AutoIssueResolver.AIConnector.Abstractions.Extensions;
using AutoIssueResolver.AIConnector.Abstractions.Models;
using AutoIssueResolver.AIConnector.Ollama.Models;
using AutoIssueResolver.AIConnectors.Base;
using AutoIssueResolver.AIConnectors.Base.UnifiedModels;
using AutoIssueResolver.Persistence.Abstractions.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutoIssueResolver.AIConnector.Ollama;

public class OllamaConnector(ILogger<OllamaConnector> logger, IOptions<AiAgentConfiguration> configuration, IReportingRepository reportingRepository, [FromKeyedServices("ollama")] HttpClient httpClient): AIConnectorBase(logger, configuration, reportingRepository, httpClient)
{
  private const string API_PATH_GENERATE = "api/generate";

  protected override List<AIModels> SupportedModels { get; } = [AIModels.Phi4Local, AIModels.DevstralLocal, AIModels.CodeLlamaLocal, AIModels.Gemma3Local, AIModels.DeepSeekReasonerLocal];

  protected override async Task<object> CreateRequestObject(Prompt prompt, CancellationToken cancellationToken)
  {
    var schema = prompt.ResponseSchema != null ? JsonNode.Parse(prompt.ResponseSchema.ResponseSchemaTextWithAdditionalProperties) : null;
    var request = new Request(configuration.Value.Model.GetModelName(), prompt.PromptText, prompt.SystemPrompt?.SystemPromptText, schema);

    return request;
  }

  protected override string GetResponsesApiPath()
  {
    return API_PATH_GENERATE;
  }

  protected override async Task<AiResponse> ParseResponse(HttpResponseMessage response, CancellationToken cancellationToken)
  {
    var chatResponse = await response.Content.ReadFromJsonAsync<ApiResponse>(cancellationToken);

    if (chatResponse == null)
    {
      logger.LogWarning("No response received from Ollama API. Something seems to have gone wrong with the request.");
      throw new UnsuccessfulResultException("No response received from Ollama API", true);
    }

    var usageMetadata = new UsageMetadata(chatResponse.PromptEvalCount, 0, chatResponse.PromptEvalCount + chatResponse.EvalCount, chatResponse.EvalCount, 0);
    if (!chatResponse.Done || chatResponse.DoneReason != "stop")
    {
      logger.LogWarning("Ollama response finished with reason: {FinishReason}. Something seems to have gone wrong with the request.", chatResponse.DoneReason);
      throw new UnsuccessfulResultException($"Ollama response finished with reason: {chatResponse.DoneReason}", true) { UsageMetadata = usageMetadata, };
    }

    var responseContent = chatResponse.Response;
    if (string.IsNullOrWhiteSpace(responseContent))
    {
      logger.LogWarning("Ollama response content is empty. Something seems to have gone wrong with the request.");
      throw new UnsuccessfulResultException("Ollama response content is empty", true) { UsageMetadata = usageMetadata, };
    }

    return new AiResponse(responseContent, usageMetadata);
  }
}