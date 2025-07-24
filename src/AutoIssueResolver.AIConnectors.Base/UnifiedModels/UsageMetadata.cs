namespace AutoIssueResolver.AIConnectors.Base.UnifiedModels;

/// <summary>
///   Metadata about token usage in a request.
/// </summary>
/// <param name="PromptTokenCount">Total Tokens used by the prompt, including cached tokens</param>
/// <param name="CachedContentTokenCount">Number of cached tokens used for the prompt</param>
/// <param name="TotalTokenCount">Total number of tokens used for the request, including request and response tokens</param>
/// <param name="CandidatesTokenCount">Number of tokens used to generate the response(s)</param>
/// <param name="ThoughtsTokenCount">Number of tokens used for reasoning (if available)</param>
public record UsageMetadata(int PromptTokenCount, int CachedContentTokenCount, int TotalTokenCount, int CandidatesTokenCount, int ThoughtsTokenCount)
{
  /// <summary>
  /// Gets the total number of tokens actually used in the request, which includes both request and response tokens (including cached tokens).
  /// </summary>
  public int ActualUsedTokens => ActualRequestTokenCount + ActualResponseTokenCount;

  /// <summary>
  /// Gets the number of tokens used in the request, including cached content.
  /// </summary>
  public int ActualRequestTokenCount => PromptTokenCount;

  /// <summary>
  /// Gets the total number of tokens used for the response, including tokens for reasoning.
  /// </summary>
  public int ActualResponseTokenCount => CandidatesTokenCount + ThoughtsTokenCount;
};