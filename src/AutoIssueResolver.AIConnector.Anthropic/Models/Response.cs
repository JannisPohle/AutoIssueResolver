using System.Text.Json.Serialization;

namespace AutoIssueResolver.AIConnector.Anthropic.Models;

internal record Response(List<ResponseContent> Content, Usage Usage, [property: JsonPropertyName("stop_reason")] string StopReason);

internal record ResponseContent(string Type, string Text);

internal record Usage([property: JsonPropertyName("input_tokens")] int InputTokens, [property: JsonPropertyName("output_tokens")]  int OutputTokens);