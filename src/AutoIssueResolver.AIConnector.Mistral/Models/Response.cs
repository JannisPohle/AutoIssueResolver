using System.Text.Json.Serialization;

namespace AutoIssueResolver.AIConnector.Mistral.Models;

internal record Response(Usage Usage, List<Choice> Choices);

internal record Usage([property: JsonPropertyName("prompt_tokens")] int PromptTokens, [property: JsonPropertyName("completion_tokens")] int CompletionTokens, [property: JsonPropertyName("total_tokens")] int TotalTokens);

internal record Choice([property: JsonPropertyName("finish_reason")] string FinishReason, ResponseMessage Message);

internal record ResponseMessage(string Content, string Role);