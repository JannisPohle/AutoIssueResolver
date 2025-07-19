using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using AutoIssueResolver.AIConnector.Abstractions.Configuration;
using AutoIssueResolver.AIConnector.Abstractions.Extensions;
using AutoIssueResolver.AIConnector.Abstractions.Models;
using AutoIssueResolver.AIConnector.Ollama.Models;
using AutoIssueResolver.AIConnectors.Base;
using AutoIssueResolver.AIConnectors.Base.UnifiedModels;
using AutoIssueResolver.GitConnector.Abstractions;
using AutoIssueResolver.Persistence.Abstractions.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutoIssueResolver.AIConnector.Ollama;

public class OllamaConnector(ILogger<OllamaConnector> logger, IOptions<AiAgentConfiguration> configuration, IReportingRepository reportingRepository, [FromKeyedServices("ollama")] HttpClient httpClient, ISourceCodeConnector sourceCodeConnector): AIConnectorBase(logger, configuration, reportingRepository, httpClient, sourceCodeConnector)
{
  private const string API_PATH_GENERATE = "api/generate";

  protected override List<AIModels> SupportedModels { get; } = [AIModels.Phi4,];

  public override Task SetupCaching(List<string> rules, CancellationToken cancellationToken = default)
  {
    // Caching not required for local models
    return Task.CompletedTask;
  }

  protected override async Task<object> CreateRequestObject(Prompt prompt, CancellationToken cancellationToken)
  {
    var request = new Request(configuration.Value.Model.GetModelName(), await PreparePromptText(prompt, cancellationToken), SYSTEM_PROMPT, JsonNode.Parse(ResponseSchemaWithAdditionalProperties));

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

  private async Task<string> PreparePromptText(Prompt prompt, CancellationToken cancellationToken)
  {
    var files = await GetFileContents(prompt, cancellationToken);

    var sb = new StringBuilder();

    sb.AppendLine(prompt.PromptText);
    sb.AppendLine();
    sb.AppendLine();
    sb.AppendLine("# Files");
    foreach (var file in files)
    {
      sb.AppendLine($"## File Path: {file.FilePath}");
      sb.AppendLine("Content:");
      sb.AppendLine("```");
      sb.AppendLine(file.FileContent);
      sb.AppendLine("```");
    }
    return sb.ToString();
  }
}