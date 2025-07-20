using System.Text.Json.Serialization;

namespace AutoIssueResolver.AIConnector.DeepSeek.Models;

internal record Response(List<Choice> Choices, Usage Usage);

internal record Choice([property: JsonPropertyName("finish_reason")] string FinishReason, int Index, MessageContent Message);

internal record MessageContent(string Content, string Role);

internal record Usage([property: JsonPropertyName("completion_tokens")] int CompletionTokens, [property: JsonPropertyName("prompt_tokens")] int PromptTokens, [property: JsonPropertyName("prompt_cache_hit_tokens")] int PromptCacheHitTokens, [property: JsonPropertyName("prompt_cache_miss_tokens")] int PromptCacheMissTokens, [property: JsonPropertyName("total_tokens")] int TotalTokens, [property: JsonPropertyName("completion_tokens_details")] CompletionTokenDetails? CompletionTokenDetails);

internal record CompletionTokenDetails([property: JsonPropertyName("reasoning_tokens")] int ReasoningTokens);