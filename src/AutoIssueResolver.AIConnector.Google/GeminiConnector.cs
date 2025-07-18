using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using AutoIssueResolver.AIConnector.Abstractions;
using AutoIssueResolver.AIConnector.Abstractions.Configuration;
using AutoIssueResolver.AIConnector.Abstractions.Extensions;
using AutoIssueResolver.AIConnector.Abstractions.Models;
using AutoIssueResolver.AIConnector.Google.Models;
using AutoIssueResolver.AIConnectors.Base;
using AutoIssueResolver.AIConnectors.Base.UnifiedModels;
using AutoIssueResolver.Application.Abstractions;
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
  IRunMetadata metadata,
  ISourceCodeConnector sourceCodeConnector,
  IReportingRepository reportingRepository,
  ILogger<GeminiConnector> logger): AIConnectorBase(logger), IAIConnector
{
  #region Static

  private const string PLACEHOLDER_MODEL_NAME = "{{MODEL}}";

  private const string API_PATH_CHAT = $"v1beta/models/{PLACEHOLDER_MODEL_NAME}:generateContent";
  private const string API_PATH_CACHE = "v1beta/cachedContents";

  #endregion

  #region Methods

  /// <inheritdoc />
  public async Task SetupCaching(CancellationToken cancellationToken = default)
  {
    if (!string.IsNullOrWhiteSpace(metadata.CacheName))
    {
      logger.LogDebug("Cache already exists with name: {CacheName}", metadata.CacheName);
      return;
    }

    try
    {
      logger.LogInformation("Setting up Gemini cache...");
      var cache = new CachedContent(await CreateCacheContent(), configuration.Value.Model, "300s", CreateSystemPrompt(), "AutoIssueResolver Cache");

      var cacheName = await CreateCache(cache, cancellationToken);

      metadata.CacheName = cacheName;
      logger.LogInformation("Gemini cache setup complete. CacheName: {CacheName}", cacheName);
    }
    catch (Exception e)
    {
      logger.LogError(e, "Unknown error occured while setting up caching for Gemini connector");
    }
  }

  /// <inheritdoc />
  public async Task<bool> CanHandleModel(AIModels model, CancellationToken cancellationToken = default)
  {
    logger.LogDebug("Checking if GeminiConnector can handle model: {Model}", model);
    if (AIModels.GeminiFlashLite == model)
    {
      return true;
    }

    return false;
  }

  //TODO set max output tokens
  /// <inheritdoc />
  public async Task<Response> GetResponse(Prompt prompt, CancellationToken cancellationToken = default)
  {
    logger.LogInformation("Getting response from Gemini for prompt...");
    if (!await CanHandleModel(configuration.Value.Model, cancellationToken))
    {
      logger.LogError("The model {Model} is not supported by this connector.", configuration.Value.Model);
      throw new InvalidOperationException("The model is not supported by this connector.");
    }

    var requestReference = await reportingRepository.InitializeRequest(EfRequestType.CodeGeneration, token: cancellationToken);

    UsageMetadata usageMetadata = null;
    try
    {
      logger.LogDebug("Preparing JSON schema for Gemini request.");
      var jsonSchema = ResponseSchema;

      var request = new ChatRequest([new Content([new TextPart(prompt.PromptText),]),], GenerationConfig: new GenerationConfiguration("application/json", JsonNode.Parse(jsonSchema)));

      await AddCachedContent(request);

      logger.LogDebug("Sending request to Gemini API: {Url}", CreateUrl(API_PATH_CHAT));
      var response = await httpClient.PostAsJsonAsync(CreateUrl(API_PATH_CHAT), request, cancellationToken);

      if (!response.IsSuccessStatusCode)
      {
        var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogError("Failed to get response from Gemini ({ReasonPhrase}): {Content}", response.ReasonPhrase, errorContent);
        throw new Exception($"Failed to get response from Gemini ({response.ReasonPhrase}): {errorContent}");
      }

      logger.LogDebug("Reading response from Gemini API.");
      var chatResponse = await response.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken);
      usageMetadata = chatResponse?.UsageMetadata;

      var responseContent = chatResponse?.Candidates.FirstOrDefault()?.Content.Parts.FirstOrDefault()?.Text ?? string.Empty;
      logger.LogTrace("Gemini raw response content: {ResponseContent}", responseContent);

      //TODO handle invalid response content
      var replacements = JsonSerializer.Deserialize<ReplacementResponse>(responseContent, JsonSerializerOptions.Web);

      logger.LogInformation("Received response from Gemini with {ReplacementCount} replacements, using a total of {TotalTokenCount} tokens (Cached: {CachedTokens}, Request: {RequestTokens}, Response: {ResponseTokens}) .", replacements?.Replacements.Count ?? 0, usageMetadata?.ActualUsedTokens, usageMetadata?.CachedContentTokenCount, usageMetadata?.ActualRequestTokenCount, usageMetadata?.ActualResponseTokenCount);

      return new Response(responseContent, replacements?.Replacements ?? []);
    }
    finally
    {
      logger.LogDebug("Ending reporting request for Gemini response.");
      await reportingRepository.EndRequest(requestReference.Id, usageMetadata?.TotalTokenCount ?? 0, usageMetadata?.CachedContentTokenCount, usageMetadata?.ActualRequestTokenCount,
                                           usageMetadata?.ActualResponseTokenCount, cancellationToken);
    }
  }

  private async Task AddCachedContent(ChatRequest request)
  {
    if (!string.IsNullOrWhiteSpace(metadata.CacheName))
    {
      logger.LogDebug("Adding cached content reference to Gemini request: {CacheName}", metadata.CacheName);
      request.CachedContent = metadata.CacheName;
    }
    else
    {
      logger.LogDebug("No cache available, adding full cache content to Gemini request.");
      var cacheContent = await CreateCacheContent();
      request.Contents.AddRange(cacheContent);
      request.SystemInstruction ??= CreateSystemPrompt();
    }
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

      return cachedContentResponse?.Name;
    }
    finally
    {
      logger.LogDebug("Ending reporting request for Gemini cache creation.");
      await reportingRepository.EndRequest(requestReference.Id, usageMetadata?.TotalTokenCount ?? 0, token: cancellationToken);
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

  private async Task<List<Content>> CreateCacheContent()
  {
    logger.LogDebug("Fetching all source files for Gemini cache content.");
    var files = await sourceCodeConnector.GetAllFiles(cancellationToken: CancellationToken.None);

    var parts = files.Select(file => new InlineDataPart(new InlineData("text/plain", JsonSerializer.Serialize(file, JsonSerializerOptions.Web)))).Cast<Part>().ToList();

    var content = new Content(parts);

    logger.LogDebug("Created Gemini cache content with {FileCount} files.", files.Count);
    return [content,];
  }

  #endregion
}