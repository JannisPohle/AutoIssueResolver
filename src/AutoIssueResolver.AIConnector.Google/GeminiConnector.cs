using System.Net.Http.Json;
using System.Text.Json.Nodes;
using AutoIssueResolver.AIConnector.Abstractions;
using AutoIssueResolver.AIConnector.Abstractions.Configuration;
using AutoIssueResolver.AIConnector.Abstractions.Extensions;
using AutoIssueResolver.AIConnector.Abstractions.Models;
using AutoIssueResolver.AIConnector.Google.Models;
using AutoIssueResolver.AIConnectors.Base;
using AutoIssueResolver.AIConnectors.Base.UnifiedModels;
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
  IReportingRepository reportingRepository,
  ILogger<GeminiConnector> logger): AIConnectorBase(logger, configuration, reportingRepository, httpClient)
{
  #region Static

  private const string PLACEHOLDER_MODEL_NAME = "{{MODEL}}";

  private const string API_PATH_CHAT = $"v1beta/models/{PLACEHOLDER_MODEL_NAME}:generateContent";

  #endregion

  protected override List<AIModels> SupportedModels { get; } = [AIModels.GeminiFlashLite,];

  #region Methods

  protected override async Task<object> CreateRequestObject(Prompt prompt, CancellationToken cancellationToken)
  {
    var jsonSchema = prompt.ResponseSchema != null ? JsonNode.Parse(prompt.ResponseSchema.ResponseSchemaText) : null;
    var responseType = prompt.ResponseSchema?.SchemaType == SchemaType.Json ? "application/json" : "text/plain";
    var systemPrompt = prompt.SystemPrompt != null ? CreateSystemPrompt(prompt.SystemPrompt.SystemPromptText) : null;
    var request = new ChatRequest([new Content([new TextPart(prompt.PromptText),]),], SystemInstruction: systemPrompt, GenerationConfig: new GenerationConfiguration(responseType, jsonSchema, GetMaxOutputTokens(configuration.Value.Model)));

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

  private string CreateUrl(string relativeUrl)
  {
    var url = relativeUrl.Replace(PLACEHOLDER_MODEL_NAME, configuration.Value.Model.GetModelName()).TrimEnd('/') + $"?key={configuration.Value.Token}";
    logger.LogTrace("Constructed Gemini API URL: {Url}", url);
    return url;
  }

  private static Content CreateSystemPrompt(string promptText)
  {
    // This is static, so no logging needed here.
    return new Content([
      new TextPart(promptText),
    ], "system");
  }

  #endregion
}