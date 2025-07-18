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

public class OpenAIConnector(ILogger<OpenAIConnector> logger, [FromKeyedServices("openAI")] HttpClient httpClient, IOptions<AiAgentConfiguration> configuration, ISourceCodeConnector sourceCodeConnector, IReportingRepository reportingRepository): AIConnectorBase(logger), IAIConnector
{
  private const string API_PATH_RESPONSES = "v1/responses";

  public async Task<bool> CanHandleModel(AIModels model, CancellationToken cancellationToken = default)
  {
    logger.LogDebug("Checking if OpenAIConnector can handle model: {Model}", model);
    if (AIModels.GPT4oNano == model)
    {
      return true;
    }

    return false;
  }

  //TODO move to base class (Strategy Pattern)
  public async Task<Response> GetResponse(Prompt prompt, CancellationToken cancellationToken = default)
  {
    logger.LogInformation("Getting response from OpenAI for prompt...");
    if (!await CanHandleModel(configuration.Value.Model, cancellationToken))
    {
      logger.LogError("The model {Model} is not supported by this connector.", configuration.Value.Model);
      throw new InvalidOperationException("The model is not supported by this connector.");
    }

    var requestReference = await reportingRepository.InitializeRequest(EfRequestType.CodeGeneration, token: cancellationToken);
    UsageMetadata? usageMetadata = null;

    try
    {
      logger.LogDebug("Preparing content for OpenAI request.");
      var request = new Request(await PreparePromptText(prompt, cancellationToken), configuration.Value.Model.GetModelName(), SYSTEM_PROMPT, new TextOptions(new Format("response_schema", JsonNode.Parse(ResponseSchemaWithAdditionalProperties))));

      logger.LogDebug("Sending request to OpenAI API: {Url}", CreateUrl(API_PATH_RESPONSES));
      var response = await httpClient.PostAsJsonAsync(CreateUrl(API_PATH_RESPONSES), request, cancellationToken);

      if (!response.IsSuccessStatusCode)
      {
        var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogError("Failed to get response from OpenAI ({ReasonPhrase}): {Content}", response.ReasonPhrase, errorContent);

        throw new Exception($"Failed to get response from OpenAI ({response.ReasonPhrase}): {errorContent}");
      }

      logger.LogDebug("Reading response from OpenAI API.");
      var chatResponse = await response.Content.ReadFromJsonAsync<ResponseRoot>(cancellationToken);
      usageMetadata = new UsageMetadata(chatResponse.Usage.InputTokens, chatResponse.Usage.InputTokensDetails.CachedTokens, chatResponse.Usage.TotalTokens, chatResponse.Usage.OutputTokens, chatResponse.Usage.OutputTokensDetails.ReasoningTokens);

      //TODO handle unsuccessful response (based on the status in the response itself)
      var responseContent = chatResponse?.Output?.FirstOrDefault(o => o.Role == "assistant")?.Content?.FirstOrDefault(c => c.Type == "output_text")?.Text ?? string.Empty;

      //TODO handle invalid response content
      var replacements = JsonSerializer.Deserialize<ReplacementResponse>(responseContent, JsonSerializerOptions.Web);

      logger.LogInformation("Received response from Gemini with {ReplacementCount} replacements, using a total of {TotalTokenCount} tokens (Cached: {CachedTokens}, Request: {RequestTokens}, Response: {ResponseTokens}) .", replacements?.Replacements.Count ?? 0, usageMetadata?.ActualUsedTokens, usageMetadata?.CachedContentTokenCount, usageMetadata?.ActualRequestTokenCount, usageMetadata?.ActualResponseTokenCount);

      return new Response(responseContent, replacements?.Replacements ?? []);
    }
    finally
    {
      logger.LogDebug("Ending reporting request for OpenAI response.");
      await reportingRepository.EndRequest(requestReference.Id, usageMetadata?.TotalTokenCount ?? 0, usageMetadata?.CachedContentTokenCount, usageMetadata?.ActualRequestTokenCount,
                                           usageMetadata?.ActualResponseTokenCount, cancellationToken);
    }
  }

  private async Task<string> PreparePromptText(Prompt prompt, CancellationToken cancellationToken)
  {
    var files = await GetFileContents(cancellationToken);

    var finalPrompt = $"# Files{Environment.NewLine}{files}{Environment.NewLine}# Prompt{Environment.NewLine}{prompt.PromptText}";

    return finalPrompt;
  }

  public Task SetupCaching(CancellationToken cancellationToken = default)
  {
    // OpenAI API does not support explicit caching
    return Task.CompletedTask;
  }

  private string CreateUrl(string relativeUrl)
  {
    logger.LogTrace("Constructed Gemini API URL: {Url}", relativeUrl);
    return relativeUrl;
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