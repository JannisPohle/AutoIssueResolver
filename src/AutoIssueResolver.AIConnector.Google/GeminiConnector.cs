using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using AutoIssueResolver.AIConnector.Abstractions;
using AutoIssueResolver.AIConnector.Abstractions.Configuration;
using AutoIssueResolver.AIConnector.Abstractions.Extensions;
using AutoIssueResolver.AIConnector.Abstractions.Models;
using AutoIssueResolver.AIConnector.Google.Models;
using AutoIssueResolver.Application.Abstractions;
using AutoIssueResolver.GitConnector.Abstractions;
using AutoIssueResolver.Persistence.Abstractions.Entities;
using AutoIssueResolver.Persistence.Abstractions.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutoIssueResolver.AIConnector.Google;

//TODO create a base class for AI Connectors (e.g. containing the system prompt, the response schema, ...)
//TODO add reporting info to the database; check how we can do this mostly in the base class
//TODO add logging
//TODO Setup caching of file contents (only works, if the total file contents are larger than 4000 tokens). Specify json schema for cached files (probably just file name and content) --> See bruno collection for examples

/// <summary>
///   Implements the <see cref="IAIConnector" /> interface for Google Gemini models.
/// </summary>
public class GeminiConnector(
  [FromKeyedServices("google")] HttpClient httpClient,
  IOptions<AiAgentConfiguration> configuration,
  IRunMetadata metadata,
  ISourceCodeConnector sourceCodeConnector,
  IReportingRepository reportingRepository,
  ILogger<GeminiConnector> logger): IAIConnector
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
      // Cache already exists, no need to create it again
      return;
    }

    try
    {
      var cache = new CachedContent(await CreateCacheContent(), configuration.Value.Model, "300s", CreateSystemPrompt(), "AutoIssueResolver Cache");

      var cacheName = await CreateCache(cache, cancellationToken);

      metadata.CacheName = cacheName;
    }
    catch (Exception e)
    {
      logger.LogError(e, "Unknown error occured while setting up caching for Gemini connector");
    }
  }

  /// <inheritdoc />
  public async Task<bool> CanHandleModel(AIModels model, CancellationToken cancellationToken = default)
  {
    if (AIModels.GeminiFlashLite == model)
    {
      return true;
    }

    return false;
  }

  /// <inheritdoc />
  public async Task<Response> GetResponse(Prompt prompt, CancellationToken cancellationToken = default)
  {
    if (!await CanHandleModel(configuration.Value.Model, cancellationToken))
    {
      throw new InvalidOperationException("The model is not supported by this connector.");
    }

    var requestReference = await reportingRepository.InitializeRequest(EfRequestType.CodeGeneration, token: cancellationToken);

    var jsonSchema = """
                     {
                       "title": "Replacements",
                       "description": "Contains a list of replacements that should be done in the code to fix the issue.",
                       "type": "array",
                        "items": {
                          "type": "object",
                          "description": "Replacement for a specific file, that should be applied to fix an issue.",
                          "properties": {
                            "newCode": {
                              "type": "string",
                              "description": "The updated code that should replace the old code to fix the issue. Should contain the complete code for the file that should be changed"
                            },
                            "fileName": {
                              "type": "string",
                              "description": "The name of the file that should be changed"
                            }
                          },
                          "required": ["newCode", "fileName"]
                       }
                     }
                     """.ReplaceLineEndings();

    var request = new ChatRequest([new Content([new TextPart(prompt.PromptText),]),], GenerationConfig: new GenerationConfiguration("application/json", JsonNode.Parse(jsonSchema)));

    if (!string.IsNullOrWhiteSpace(metadata.CacheName))
    {
      request.CachedContent = metadata.CacheName;
    }
    else
    {
      // If no cache is available, we assume that the content is too small to be cached, so we send all the conent in the request
      var cacheContent = await CreateCacheContent();
      request.Contents.AddRange(cacheContent);
      request.SystemInstruction ??= CreateSystemPrompt();
    }

    var response = await httpClient.PostAsJsonAsync(CreateUrl(API_PATH_CHAT), request, cancellationToken);

    if (!response.IsSuccessStatusCode)
    {
      throw new Exception($"Failed to get response from Gemini ({response.ReasonPhrase}): {await response.Content.ReadAsStringAsync(cancellationToken)}");
    }

    var chatResponse = await response.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken);

    var responseContent = chatResponse?.Candidates.FirstOrDefault()?.Content.Parts.FirstOrDefault()?.Text ?? string.Empty;
    var replacements = JsonSerializer.Deserialize<List<Replacement>>(responseContent, JsonSerializerOptions.Web);

    await reportingRepository.EndRequest(requestReference.Id, chatResponse.UsageMetadata.TotalTokenCount, chatResponse.UsageMetadata.CachedContentTokenCount, chatResponse.UsageMetadata.PromptTokenCount,
                                         chatResponse.UsageMetadata.CandidatesTokenCount + chatResponse.UsageMetadata.ThoughtsTokenCount, cancellationToken);

    return new Response(responseContent, replacements);
  }

  private async Task<string?> CreateCache(CachedContent cachedContent, CancellationToken cancellationToken = default)
  {
    var requestReference = await reportingRepository.InitializeRequest(EfRequestType.CacheCreation, token: cancellationToken);
    UsageMetadata? usageMetadata = null;

    try
    {
      var response = await httpClient.PostAsJsonAsync(CreateUrl(API_PATH_CACHE), cachedContent, cancellationToken);

      if (!response.IsSuccessStatusCode)
      {
        if (response.StatusCode != HttpStatusCode.BadRequest)
        {
          logger.LogWarning("Failed to create cache in Gemini: {ReasonPhrase} - {Content}. Cached content will be added to the individual requests", response.ReasonPhrase, await response.Content.ReadAsStringAsync(cancellationToken));

          return null;
        }

        //Assume, that there is just not enough content to cache, so we just return null
        logger.LogWarning("Failed to create cache in Gemini, assuming that the cached content was too small. Cached content will be added to individual requests: {ReasonPhrase} - {Content}", response.ReasonPhrase,
                          await response.Content.ReadAsStringAsync(cancellationToken));

        return null;
      }

      var cachedContentResponse = await response.Content.ReadFromJsonAsync<CachedContentResponse>(cancellationToken);
      usageMetadata = cachedContentResponse?.UsageMetadata;

      return cachedContentResponse?.Name;
    }
    finally
    {
      await reportingRepository.EndRequest(requestReference.Id, usageMetadata?.TotalTokenCount ?? 0, token: cancellationToken);
    }
  }

  private string CreateUrl(string relativeUrl)
  {
    return relativeUrl.Replace(PLACEHOLDER_MODEL_NAME, configuration.Value.Model.GetModelName()).TrimEnd('/') + $"?key={configuration.Value.Token}";
  }

  private static Content CreateSystemPrompt()
  {
    return new Content([
      new TextPart("You are a helpful AI assistant that helps to fix code issues. You will receive a description for a code smell that should be fixed in a specific class. The response should contain the complete code for the files that should be changed."),
    ]);
  }

  private async Task<List<Content>> CreateCacheContent()
  {
    var files = await sourceCodeConnector.GetAllFiles(cancellationToken: CancellationToken.None);

    //TODO add file path to to the content, so that the AI can give this value back after fixing the issue
    var parts = files.Select(file => new InlineDataPart(new InlineData("text/plain", file.FileContent))).Cast<Part>().ToList();

    var content = new Content(parts);

    return [content,];
  }

  #endregion
}