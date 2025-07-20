using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AutoIssueResolver.AIConnector.Abstractions;
using AutoIssueResolver.AIConnector.Abstractions.Configuration;
using AutoIssueResolver.AIConnector.Abstractions.Models;
using AutoIssueResolver.AIConnectors.Base.UnifiedModels;
using AutoIssueResolver.Application.Abstractions.Models;
using AutoIssueResolver.GitConnector.Abstractions;
using AutoIssueResolver.Persistence.Abstractions.Entities;
using AutoIssueResolver.Persistence.Abstractions.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;

namespace AutoIssueResolver.AIConnectors.Base;

public abstract class AIConnectorBase(ILogger<AIConnectorBase> logger, IOptions<AiAgentConfiguration> configuration, IReportingRepository reportingRepository, HttpClient httpClient, ISourceCodeConnector sourceCodeConnector): IAIConnector
{
  #region Static

  protected const string SYSTEM_PROMPT =
    "You are a Software Developer tasked with fixing Code Smells. You will receive a description for a code smell that should be fixed in a specific class, as well as the content of other possibly relevant classes. "
    + "Here are some rules that you should follow when fixing the code smell:\n"
    + "1. Respond only in the provided JSON format"
    + "2. Do not change anything else in the code, just fix the issue that is described in the request. Do not add any comments, explanations or unnecessary whitespace to the code. Do not change the formatting of the code."
    + "3. Use the provided file paths in the responses to identify the files."
    + "4. The response should contain the *complete* code for the files that should be changed."
    + "5. Ensure that the code is still valid after your changes and compiles without errors. Do not change the code in a way that would break the compilation or introduce new issues.";

  protected const int MAX_OUTPUT_TOKENS = 2500;

  private const string ADDITIONAL_PROPERTIES = "\"additionalProperties\": false,";

  private const string RESPONSE_SCHEMA = """
                                         {
                                           "title": "Replacements",
                                           "description": "Contains a list of replacements that should be done in the code to fix the issue.",
                                           "type": "object",
                                           {{ADDITIONAL_PROPERTIES}}
                                           "required": ["replacements"],
                                           "properties": {
                                             "replacements": {
                                               "type": "array",
                                               "description": "A list of code replacements that should be applied to fix the issue.",
                                               {{ADDITIONAL_PROPERTIES}}
                                               "items": {
                                                  "type": "object",
                                                  "description": "Replacement for a specific file, that should be applied to fix an issue.",
                                                  "properties": {
                                                    "newCode": {
                                                      "type": "string",
                                                      "description": "The updated code that should replace the old code to fix the issue. Should contain the complete code for the file that should be changed"
                                                    },
                                                    "filePath": {
                                                      "type": "string",
                                                      "description": "The path of the file that should be changed (relative to the repository root). This should be the same path as provided in the source code files in the cache."
                                                    }
                                                  },
                                                  {{ADDITIONAL_PROPERTIES}}
                                                  "required": ["newCode", "filePath"]
                                               }
                                             }
                                           }
                                         }
                                         """;

  private static readonly ResiliencePipeline<(Response, UsageMetadata)> _retryPolicy =
    new ResiliencePipelineBuilder<(Response, UsageMetadata)>().AddRetry(new RetryStrategyOptions<(Response, UsageMetadata)>
    {
      ShouldHandle = new PredicateBuilder<(Response, UsageMetadata)>().Handle<UnsuccessfulResultException>(ex => ex.CanRetry),
      BackoffType = DelayBackoffType.Constant,
      UseJitter = true,
      MaxRetryAttempts = 3,
      Delay = TimeSpan.FromSeconds(5),
      OnRetry = async arguments =>
      {
        var retryLogger = arguments.Context.Properties.GetValue(new ResiliencePropertyKey<ILogger?>("logger"), null);
        var retryReportingRepository = arguments.Context.Properties.GetValue(new ResiliencePropertyKey<IReportingRepository?>("repository"), null);
        var requestReference = arguments.Context.Properties.GetValue(new ResiliencePropertyKey<EfRequest?>("request"), null);

        var usageMetadata = (arguments.Outcome.Exception as UnsuccessfulResultException)?.UsageMetadata;

        retryLogger?.LogInformation(arguments.Outcome.Exception, "(Retry {RetryCount}). Request to the AI failed: '{ErrorMessage}'. Retrying request to AI API after {WaitTime}. Usage Statistics: {UsageMetadata}",
                                    arguments.AttemptNumber, arguments.Outcome.Exception?.Message, arguments.Duration, usageMetadata);

        if (retryReportingRepository != null && requestReference != null)
        {
          await retryReportingRepository.IncrementRequestRetries(requestReference.Id, usageMetadata?.TotalTokenCount ?? 0, usageMetadata?.CachedContentTokenCount ?? 0, usageMetadata?.ActualRequestTokenCount ?? 0,
                                                                 usageMetadata?.ActualResponseTokenCount ?? 0);
          retryLogger?.LogDebug("Incremented retry counter for request {RequestId}.", requestReference.Id);
        }
      },
    }).Build();

  #endregion

  #region Properties

  /// <summary>
  ///   Gets the default response json schema
  /// </summary>
  protected static string ResponseSchema => RESPONSE_SCHEMA.Replace("{{ADDITIONAL_PROPERTIES}}", "").ReplaceLineEndings();

  /// <summary>
  ///   Gets the response json schema with "additionalProperties" set to false (required e.g. for OpenAI)
  /// </summary>
  protected static string ResponseSchemaWithAdditionalProperties => RESPONSE_SCHEMA.Replace("{{ADDITIONAL_PROPERTIES}}", ADDITIONAL_PROPERTIES).ReplaceLineEndings();

  /// <summary>
  /// Gets a list of supported models by the current connector
  /// </summary>
  protected abstract List<AIModels> SupportedModels { get; }

  #endregion

  #region Methods

  public virtual Task<bool> CanHandleModel(AIModels model, CancellationToken cancellationToken = default)
  {
    logger.LogDebug("Checking if model {Model} can be handled by this connector", model);
    if (SupportedModels.Contains(model))
    {
      logger.LogDebug("Model {Model} is supported by this connector.", model);
      return Task.FromResult(true);
    }

    logger.LogDebug("Model {Model} is not supported by this connector.", model);
    return Task.FromResult(false);
  }

  public async Task<Response> GetResponse(Prompt prompt, CancellationToken cancellationToken = default)
  {
    logger.LogInformation("Getting response for prompt...");

    if (!await CanHandleModel(configuration.Value.Model, cancellationToken))
    {
      logger.LogError("The model {Model} is not supported by this connector.", configuration.Value.Model);

      throw new InvalidOperationException("The model is not supported by this connector.");
    }

    var requestReference = await reportingRepository.InitializeRequest(EfRequestType.CodeGeneration, token: cancellationToken);
    UsageMetadata? usageMetadata = null;

    var context = ResilienceContextPool.Shared.Get(cancellationToken);

    try
    {
      context.Properties.Set(new ResiliencePropertyKey<ILogger?>("logger"), logger);
      context.Properties.Set(new ResiliencePropertyKey<EfRequest?>("request"), requestReference);
      context.Properties.Set(new ResiliencePropertyKey<IReportingRepository?>("repository"), reportingRepository);
      context.Properties.Set(new ResiliencePropertyKey<Prompt?>("prompt"), prompt);

      (var response, usageMetadata) = await _retryPolicy.ExecuteAsync(async state => await GetAiResponseInternal(state.Properties.GetValue(new ResiliencePropertyKey<Prompt>("prompt"), new Prompt(string.Empty, string.Empty)), state.CancellationToken), context);

      logger.LogDebug("Ending reporting request for OpenAI response.");

      await reportingRepository.EndRequest(requestReference.Id, EfRequestStatus.Succeeded, usageMetadata?.TotalTokenCount ?? 0, usageMetadata?.CachedContentTokenCount ?? 0, usageMetadata?.ActualRequestTokenCount ?? 0,
                                           usageMetadata?.ActualResponseTokenCount ?? 0, cancellationToken);

      return response;
    }
    catch (UnsuccessfulResultException ex)
    {
      logger.LogInformation(ex, "Trying to get a response from the AI model failed on the last retry, request will be marked as failed.");
      if (ex.UsageMetadata != null)
      {
        await reportingRepository.EndRequest(requestReference.Id, EfRequestStatus.Failed, ex.UsageMetadata.TotalTokenCount, ex.UsageMetadata.CachedContentTokenCount, ex.UsageMetadata.ActualRequestTokenCount, ex.UsageMetadata.ActualResponseTokenCount, cancellationToken);
      }

      throw;
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "An unexpected error occurred while trying to get a response from the AI model. Request will be marked as failed.");
      await reportingRepository.EndRequest(requestReference.Id, EfRequestStatus.Failed, token: cancellationToken);
      throw;
    }
    finally
    {
      ResilienceContextPool.Shared.Return(context);
    }
  }

  private async Task<(Response, UsageMetadata)> GetAiResponseInternal(Prompt prompt, CancellationToken cancellationToken)
  {
    logger.LogDebug("Preparing content for API request.");
    var request = await CreateRequestObject(prompt, cancellationToken);

    logger.LogDebug("Sending request to API: {Url}", GetResponsesApiPath());
    var response = await httpClient.PostAsJsonAsync(GetResponsesApiPath(), request, cancellationToken);

    if (!response.IsSuccessStatusCode)
    {
      var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
      logger.LogError("Failed to get response from API ({ReasonPhrase}): {Content}", response.ReasonPhrase, errorContent);

      throw new UnsuccessfulResultException($"Failed to get response from API ({response.ReasonPhrase}): {errorContent}", false);
    }

    logger.LogDebug("Reading response from API.");
    var aiResponse = await ParseResponse(response, cancellationToken);

    ReplacementResponse? replacementResponse;

    try
    {
      replacementResponse = JsonSerializer.Deserialize<ReplacementResponse>(aiResponse.ResponseText, JsonSerializerOptions.Web);
    }
    catch (JsonException ex)
    {
      if (TryCleanupAIResponseAndDeserialize(aiResponse.ResponseText, out replacementResponse))
      {
        logger.LogDebug("Successfully cleaned up AI response and deserialized replacements.");
      }
      else
      {
        logger.LogError(ex, "Failed to deserialize AI response: {ResponseText}", aiResponse.ResponseText);
        throw new UnsuccessfulResultException("Failed to deserialize AI response", ex, true) { UsageMetadata = aiResponse.UsageMetadata, };
      }
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to parse replacement response from API.");
      throw new UnsuccessfulResultException("Failed to parse replacement response from API.", e, true) {  UsageMetadata = aiResponse.UsageMetadata, };
    }

    logger.LogInformation("Received response from API with {ReplacementCount} replacements, using a total of {TotalTokenCount} tokens (Cached: {CachedTokens}, Request: {RequestTokens}, Response: {ResponseTokens}) .",
                          replacementResponse?.Replacements.Count ?? 0, aiResponse.UsageMetadata.ActualUsedTokens, aiResponse.UsageMetadata.CachedContentTokenCount, aiResponse.UsageMetadata.ActualRequestTokenCount, aiResponse.UsageMetadata.ActualResponseTokenCount);

    return (new Response(aiResponse.ResponseText, replacementResponse?.Replacements ?? []), aiResponse.UsageMetadata);
  }

  protected abstract Task<object> CreateRequestObject(Prompt prompt, CancellationToken cancellationToken);

  protected abstract string GetResponsesApiPath();

  protected abstract Task<AiResponse> ParseResponse(HttpResponseMessage response, CancellationToken cancellationToken);

  protected async Task<List<SourceFile>> GetFileContents(Prompt prompt, CancellationToken cancellationToken)
  {
    var files = await sourceCodeConnector.GetAllFiles(folderFilter: prompt.RuleId, cancellationToken: cancellationToken);
    if (files.Count == 0)
    {
      logger.LogWarning("No files found in the repository for rule {RuleId}.", prompt.RuleId);
      return [];
    }

    logger.LogDebug("Found {FileCount} files.", files.Count);

    return files;
  }

  protected StringBuilder FormatFilesForPromptText(List<SourceFile> files, StringBuilder builder)
  {
    builder ??= new StringBuilder();

    builder.AppendLine("# Files");
    builder.AppendLine();

    foreach (var file in files)
    {
      builder.AppendLine($"## File Path: {file.FilePath}");
      builder.AppendLine("Content:");
      builder.AppendLine("```");
      builder.AppendLine(file.FileContent);
      builder.AppendLine("```");
    }

    return builder;
  }

  private bool TryCleanupAIResponseAndDeserialize(string responseText, out ReplacementResponse? replacements)
  {
    if (!responseText.StartsWith('{') && responseText.Contains('{'))
    {
      responseText = responseText[responseText.IndexOf('{', StringComparison.InvariantCultureIgnoreCase)..];
      logger.LogDebug("Response text does not start with '{{', trying to find the first '{{' character to extract the JSON object.");
    }

    if (!responseText.EndsWith('}') && responseText.Contains('}'))
    {
      responseText = responseText[..(responseText.LastIndexOf('}') + 1)];
      logger.LogDebug("Response text does not end with '}}', trying to find the last '}}' character to extract the JSON object.");
    }

    if (responseText.Contains('\n'))
    {
      responseText = responseText.Replace("\n", "\\n");
      logger.LogDebug("Response text contains newlines, escaping them to avoid deserialization issues.");
    }

    try
    {
      logger.LogDebug("Trying to deserialize the AI response text after cleanup: {ResponseText}", responseText);
      replacements = JsonSerializer.Deserialize<ReplacementResponse>(responseText, JsonSerializerOptions.Web);

      if (replacements == null)
      {
        logger.LogDebug("Failed to deserialize AI response: {ResponseText}", responseText);

        return false;
      }

      return true;
    }
    catch (JsonException ex)
    {
      logger.LogDebug(ex, "Failed to deserialize AI response: {ResponseText}", responseText);
      replacements = null;

      return false;
    }
  }

  #endregion
}