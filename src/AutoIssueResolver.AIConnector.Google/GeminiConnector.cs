using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using AutoIssueResolver.AIConnector.Abstractions;
using AutoIssueResolver.AIConnector.Abstractions.Configuration;
using AutoIssueResolver.AIConnector.Abstractions.Extensions;
using AutoIssueResolver.AIConnector.Abstractions.Models;
using AutoIssueResolver.AIConnector.Google.Models;
using AutoIssueResolver.AIConnectors.Base;
using AutoIssueResolver.AIConnectors.Base.UnifiedModels;
using AutoIssueResolver.GitConnector.Abstractions;
using AutoIssueResolver.Persistence.Abstractions.Entities;
using AutoIssueResolver.Persistence.Abstractions.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutoIssueResolver.AIConnector.Google;

/// <summary>
///   Implements the <see cref="IAIConnector" /> interface for Google Gemini models.
/// </summary>
public class GeminiConnector(
  [FromKeyedServices("google")] HttpClient httpClient,
  IOptions<AiAgentConfiguration> configuration,
  ISourceCodeConnector sourceCodeConnector,
  IReportingRepository reportingRepository,
  ILogger<GeminiConnector> logger): AIConnectorBase(logger, configuration, reportingRepository, httpClient, sourceCodeConnector)
{
  #region Static

  private const string PLACEHOLDER_MODEL_NAME = "{{MODEL}}";

  private const string API_PATH_CHAT = $"v1beta/models/{PLACEHOLDER_MODEL_NAME}:generateContent";
  private const string API_PATH_CACHE = "v1beta/cachedContents";

  #endregion

  protected override List<AIModels> SupportedModels { get; } = [AIModels.GeminiFlashLite,];

  #region Methods

  protected override async Task<object> CreateRequestObject(Prompt prompt, CancellationToken cancellationToken)
  {
    var jsonSchema = ResponseSchema;
    var request = new ChatRequest([new Content([new TextPart(await PreparePromptText(prompt, cancellationToken)),]),], SystemInstruction: CreateSystemPrompt(), GenerationConfig: new GenerationConfiguration("application/json", JsonNode.Parse(jsonSchema), MAX_OUTPUT_TOKENS));

    return request;
  }

  protected override string GetResponsesApiPath()
  {
    return CreateUrl(API_PATH_CHAT);
  }

  protected override async Task<AiResponse> ParseResponse(HttpResponseMessage response, CancellationToken cancellationToken)
  {
    var chatResponse = await response.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken);
    var usageMetadata = chatResponse?.UsageMetadata;

    var candidate = chatResponse?.Candidates.FirstOrDefault();

    if (candidate == null)
    {
      logger.LogWarning("No candidates found in Gemini response. Something seems to have gone wrong with the request.");
      throw new UnsuccessfulResultException("No candidates found in Gemini response", true) {  UsageMetadata = usageMetadata, };
    }

    if (candidate.FinishReason != "STOP")
    {
      logger.LogWarning("Gemini response finished with reason: {FinishReason}. Something seems to have gone wrong with the request.", candidate.FinishReason);
      throw new UnsuccessfulResultException($"Gemini response finished with reason: {candidate.FinishReason}", true) {  UsageMetadata = usageMetadata, };
    }

    var responseContent = candidate.Content.Parts.FirstOrDefault()?.Text ?? string.Empty;

    if (string.IsNullOrWhiteSpace(responseContent))
    {
      logger.LogWarning("Gemini response content is empty. Something seems to have gone wrong with the request.");
      throw new UnsuccessfulResultException("Gemini response content is empty", true) {  UsageMetadata = usageMetadata, };
    }

    return new AiResponse(responseContent, usageMetadata ?? new UsageMetadata(0, 0, 0, 0, 0));
  }


  private async Task<string?> CreateCache(CachedContent cachedContent, CancellationToken cancellationToken = default)
  {
    var requestReference = await reportingRepository.InitializeRequest(EfRequestType.CacheCreation, token: cancellationToken);
    UsageMetadata? usageMetadata = null;

    try
    {
      logger.LogDebug("Sending cache creation request to Gemini API: {Url}", CreateUrl(API_PATH_CACHE));
      var response = await httpClient.PostAsJsonAsync(CreateUrl(API_PATH_CACHE), cachedContent, cancellationToken);

      if (!response.IsSuccessStatusCode)
      {
        var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
        if (response.StatusCode != HttpStatusCode.BadRequest)
        {
          logger.LogWarning("Failed to create cache in Gemini: {ReasonPhrase} - {Content}. Cached content will be added to the individual requests", response.ReasonPhrase, errorContent);

          return null;
        }

        //Assume, that there is just not enough content to cache, so we just return null
        logger.LogWarning("Failed to create cache in Gemini, assuming that the cached content was too small. Cached content will be added to individual requests: {ReasonPhrase} - {Content}", response.ReasonPhrase,
                          errorContent);

        return null;
      }

      logger.LogDebug("Reading cache creation response from Gemini API.");
      var cachedContentResponse = await response.Content.ReadFromJsonAsync<CachedContentResponse>(cancellationToken);
      usageMetadata = cachedContentResponse?.UsageMetadata;

      logger.LogInformation("Cache created in Gemini: {CacheName}", cachedContentResponse?.Name);

      logger.LogDebug("Ending reporting request for Gemini cache creation.");
      await reportingRepository.EndRequest(requestReference.Id, EfRequestStatus.Succeeded, usageMetadata?.TotalTokenCount ?? 0, token: cancellationToken);

      return cachedContentResponse?.Name;
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Setting up cache for Gemini connector failed. Request will be marked as failed. Request reference: {RequestReferenceId}", requestReference.Id);
      await reportingRepository.EndRequest(requestReference.Id, EfRequestStatus.Failed, token: cancellationToken);
      throw;
    }
  }

  private string CreateUrl(string relativeUrl)
  {
    var url = relativeUrl.Replace(PLACEHOLDER_MODEL_NAME, configuration.Value.Model.GetModelName()).TrimEnd('/') + $"?key={configuration.Value.Token}";
    logger.LogTrace("Constructed Gemini API URL: {Url}", url);
    return url;
  }

  private static Content CreateSystemPrompt()
  {
    // This is static, so no logging needed here.
    return new Content([
      new TextPart(SYSTEM_PROMPT),
    ]);
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

  #endregion
}