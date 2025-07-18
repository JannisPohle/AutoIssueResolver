using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using AutoIssueResolver.AIConnector.Abstractions;
using AutoIssueResolver.AIConnector.Abstractions.Configuration;
using AutoIssueResolver.AIConnector.Abstractions.Extensions;
using AutoIssueResolver.AIConnector.Abstractions.Models;
using AutoIssueResolver.AIConnector.OpenAI.Models;
using AutoIssueResolver.AIConnectors.Base;
using AutoIssueResolver.AIConnectors.Base.UnifiedModels;
using AutoIssueResolver.GitConnector.Abstractions;
using AutoIssueResolver.Persistence.Abstractions.Entities;
using AutoIssueResolver.Persistence.Abstractions.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutoIssueResolver.AIConnector.OpenAI;

public class OpenAIConnector(ILogger<OpenAIConnector> logger, [FromKeyedServices("openAI")] HttpClient httpClient, IOptions<AiAgentConfiguration> configuration, ISourceCodeConnector sourceCodeConnector, IReportingRepository reportingRepository): AIConnectorBase(logger, configuration, reportingRepository, httpClient), IAIConnector
{
  private const string API_PATH_RESPONSES = "v1/responses";

  public override Task<bool> CanHandleModel(AIModels model, CancellationToken cancellationToken = default)
  {
    logger.LogDebug("Checking if OpenAIConnector can handle model: {Model}", model);
    if (AIModels.GPT4oNano == model)
    {
      return Task.FromResult(true);
    }

    return Task.FromResult(false);
  }
  protected override async Task<object> CreateRequestObject(Prompt prompt, CancellationToken cancellationToken)
  {
    //TODO set max output tokens
    var request = new Request(await PreparePromptText(prompt, cancellationToken), configuration.Value.Model.GetModelName(), SYSTEM_PROMPT, new TextOptions(new Format("response_schema", JsonNode.Parse(ResponseSchemaWithAdditionalProperties))));

    return request;
  }

  protected override string GetResponsesApiPath()
  {
    return API_PATH_RESPONSES;
  }

  protected override async Task<AiResponse> ParseResponse(HttpResponseMessage response, CancellationToken cancellationToken)
  {
    var chatResponse = await response.Content.ReadFromJsonAsync<ResponseRoot>(cancellationToken);
    var usageMetadata = new UsageMetadata(chatResponse?.Usage?.InputTokens ?? 0, chatResponse?.Usage?.InputTokensDetails?.CachedTokens ?? 0, chatResponse?.Usage?.TotalTokens ?? 0, chatResponse?.Usage?.OutputTokens ?? 0, chatResponse?.Usage?.OutputTokensDetails?.ReasoningTokens ?? 0);

    //TODO handle unsuccessful response (based on the status in the response itself)

    var responseText = chatResponse?.Output?.FirstOrDefault(o => o.Role == "assistant")?.Content?.FirstOrDefault(c => c.Type == "output_text")?.Text ?? string.Empty;
    return new AiResponse(responseText, usageMetadata);
  }

  private async Task<string> PreparePromptText(Prompt prompt, CancellationToken cancellationToken)
  {
    var files = await GetFileContents(cancellationToken);

    var finalPrompt = $"# Files{Environment.NewLine}{files}{Environment.NewLine}# Prompt{Environment.NewLine}{prompt.PromptText}";

    return finalPrompt;
  }

  public override Task SetupCaching(CancellationToken cancellationToken = default)
  {
    // OpenAI API does not support explicit caching
    return Task.CompletedTask;
  }

  private async Task<string> GetFileContents(CancellationToken cancellationToken)
  {
    var files = await sourceCodeConnector.GetAllFiles(cancellationToken: cancellationToken);
    if (files.Count == 0)
    {
      logger.LogWarning("No files found in the repository.");
      return string.Empty;
    }

    logger.LogDebug("Found {FileCount} files.", files.Count);
    return JsonSerializer.Serialize(files, JsonSerializerOptions.Web);
  }
}