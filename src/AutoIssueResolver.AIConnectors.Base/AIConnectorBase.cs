using System.Net.Http.Json;
using System.Text.Json;
using AutoIssueResolver.AIConnector.Abstractions;
using AutoIssueResolver.AIConnector.Abstractions.Configuration;
using AutoIssueResolver.AIConnector.Abstractions.Models;
using AutoIssueResolver.AIConnectors.Base.UnifiedModels;
using AutoIssueResolver.Persistence.Abstractions.Entities;
using AutoIssueResolver.Persistence.Abstractions.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;

namespace AutoIssueResolver.AIConnectors.Base;

public abstract class AIConnectorBase(ILogger<AIConnectorBase> logger, IOptions<AiAgentConfiguration> configuration, IReportingRepository reportingRepository, HttpClient httpClient): IAIConnector
{
  #region Static

  protected const int MAX_OUTPUT_TOKENS = 2500;

  #endregion

  #region Properties

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

  public async Task<Response<T>> GetResponse<T>(Prompt prompt, CancellationToken cancellationToken = default)
  {
    logger.LogInformation("Getting response for prompt...");

    if (!await CanHandleModel(configuration.Value.Model, cancellationToken))
    {
      logger.LogError("The model {Model} is not supported by this connector.", configuration.Value.Model);

      throw new InvalidOperationException("The model is not supported by this connector.");
    }

    var requestReference = await reportingRepository.InitializeRequest(prompt.RuleId, token: cancellationToken);
    UsageMetadata? usageMetadata = null;

    var context = ResilienceContextPool.Shared.Get(cancellationToken);

    try
    {
      context.Properties.Set(new ResiliencePropertyKey<ILogger?>("logger"), logger);
      context.Properties.Set(new ResiliencePropertyKey<EfRequest?>("request"), requestReference);
      context.Properties.Set(new ResiliencePropertyKey<IReportingRepository?>("repository"), reportingRepository);
      context.Properties.Set(new ResiliencePropertyKey<Prompt?>("prompt"), prompt);

      (var response, usageMetadata) = await GetRetryPolicy<T>().ExecuteAsync(async state => await GetAiResponseInternal<T>(state.Properties.GetValue(new ResiliencePropertyKey<Prompt>("prompt"), new Prompt(string.Empty, string.Empty)), state.CancellationToken), context);

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

  private async Task<(Response<T>, UsageMetadata)> GetAiResponseInternal<T>(Prompt prompt, CancellationToken cancellationToken)
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

    T? parsedResponse;

    try
    {
      parsedResponse = JsonSerializer.Deserialize<T>(aiResponse.ResponseText, JsonSerializerOptions.Web);
    }
    catch (JsonException ex)
    {
      if (TryCleanupAIResponseAndDeserialize<T>(aiResponse.ResponseText, out parsedResponse))
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

    logger.LogInformation("Received response from API and tried to deserialize to target type {ResponseType} replacements, using a total of {TotalTokenCount} tokens (Cached: {CachedTokens}, Request: {RequestTokens}, Response: {ResponseTokens}) .",
                          typeof(T), aiResponse.UsageMetadata.ActualUsedTokens, aiResponse.UsageMetadata.CachedContentTokenCount, aiResponse.UsageMetadata.ActualRequestTokenCount, aiResponse.UsageMetadata.ActualResponseTokenCount);

    return (new Response<T>(aiResponse.ResponseText, parsedResponse), aiResponse.UsageMetadata);
  }

  protected abstract Task<object> CreateRequestObject(Prompt prompt, CancellationToken cancellationToken);

  protected abstract string GetResponsesApiPath();

  protected abstract Task<AiResponse> ParseResponse(HttpResponseMessage response, CancellationToken cancellationToken);

  private bool TryCleanupAIResponseAndDeserialize<T>(string responseText, out T? parsedResponse)
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
      parsedResponse = JsonSerializer.Deserialize<T>(responseText, JsonSerializerOptions.Web);

      if (EqualityComparer<T>.Default.Equals(parsedResponse, default))
      {
        logger.LogDebug("Failed to deserialize AI response: {ResponseText}", responseText);

        return false;
      }

      return true;
    }
    catch (JsonException ex)
    {
      logger.LogDebug(ex, "Failed to deserialize AI response: {ResponseText}", responseText);
      parsedResponse = default;

      return false;
    }
  }

  private static ResiliencePipeline<(Response<T>, UsageMetadata)> GetRetryPolicy<T>() =>
    new ResiliencePipelineBuilder<(Response<T>, UsageMetadata)>().AddRetry(new RetryStrategyOptions<(Response<T>, UsageMetadata)>
    {
      ShouldHandle = new PredicateBuilder<(Response<T>, UsageMetadata)>().Handle<UnsuccessfulResultException>(ex => ex.CanRetry),
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
}