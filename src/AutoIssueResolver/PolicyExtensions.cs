using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Extensions.Http;

namespace AutoIssueResolver;

internal static class PolicyExtensions
{
  #region Static

  /// <summary>
  ///   Maximum number of retry attempts for HTTP requests.
  /// </summary>
  private const int MAX_RETRY_COUNT = 10;

  #endregion

  #region Methods

  /// <summary>
  ///   Creates an asynchronous retry policy for HTTP requests that handles transient errors
  ///   and HTTP 429 (Too Many Requests) responses using a jitter backoff strategy.
  /// </summary>
  /// <typeparam name="T">
  ///   The type used for logging context.
  /// </typeparam>
  /// <returns>
  ///   An <see cref="IAsyncPolicy{HttpResponseMessage}" /> configured with the retry logic.
  /// </returns>
  public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy<T>(IServiceProvider serviceProvider)
  {
    return HttpPolicyExtensions.HandleTransientHttpError()
                               .OrResult(message => message.StatusCode == HttpStatusCode.TooManyRequests)
                               .WaitAndRetryAsync(MAX_RETRY_COUNT,
                                                  (retry, message, _) =>
                                                  {
                                                    // Fallback to decorrelated jitter backoff
                                                    var defaultWaitTime = Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromSeconds(3), MAX_RETRY_COUNT)
                                                                                 .ElementAt(retry - 1);

                                                    // If Retry-After header is present, use its value
                                                    if (message.Result is not null
                                                        && message.Result.StatusCode == HttpStatusCode.TooManyRequests
                                                        && message.Result.Headers.RetryAfter is not null
                                                        && TryGetRetryTimeFromResponseHeader(message, out var retryAfterDelta))
                                                    {
                                                      return retryAfterDelta;
                                                    }

                                                    return defaultWaitTime;
                                                  },
                                                  (result, waitTime, retryCount, _) =>
                                                    OnRetryAsync<T>(serviceProvider, result, waitTime, retryCount));
  }

  private static bool TryGetRetryTimeFromResponseHeader(DelegateResult<HttpResponseMessage> message, out TimeSpan retryAfterDelta)
  {
    retryAfterDelta = TimeSpan.Zero;

    //Check if delta is present in Retry-After header
    if (message.Result.Headers.RetryAfter!.Delta.HasValue)
    {
      retryAfterDelta = message.Result.Headers.RetryAfter.Delta.Value;

      return true;
    }

    //Check if date is present in Retry-After header
    if (!message.Result.Headers.RetryAfter.Date.HasValue)
    {
      return false;
    }

    var date = message.Result.Headers.RetryAfter.Date.Value;

    if (date > DateTimeOffset.UtcNow)
    {
      // Wait until the specified date
      retryAfterDelta = date - DateTimeOffset.UtcNow;

      return true;
    }

    // If date is in the past, do not wait
    retryAfterDelta = TimeSpan.Zero;

    return true;
  }

  /// <summary>
  ///   Logs details about each retry attempt, including status code, reason, and content.
  /// </summary>
  /// <typeparam name="T">
  ///   The type used for logging context.
  /// </typeparam>
  private static async Task OnRetryAsync<T>(IServiceProvider serviceProvider,
                                            DelegateResult<HttpResponseMessage> result,
                                            TimeSpan waitTime,
                                            int i)
  {
    var logger = serviceProvider.GetRequiredService<ILogger<T>>();

    logger.LogInformation("(Try {Retry}) Retrying request to {Connector} API after {WaitTime} due to response with status {StatusCode}: {Reason} - {Content}",
                          i,
                          typeof(T).Name,
                          waitTime,
                          result.Result.StatusCode,
                          result.Result.ReasonPhrase,
                          await result.Result.Content.ReadAsStringAsync());
  }

  #endregion
}