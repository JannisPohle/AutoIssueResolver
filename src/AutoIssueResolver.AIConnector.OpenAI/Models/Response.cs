using System.Text.Json.Serialization;

namespace AutoIssueResolver.AIConnector.OpenAI.Models;

public record ResponseRoot(
  string Id,
  string Object,
  [property: JsonPropertyName("created_at")] long CreatedAt,
  string Status,
  string? Error,
  [property: JsonPropertyName("incomplete_details")] string? IncompleteDetails,
  string? Instructions,
  [property: JsonPropertyName("max_output_tokens")] int? MaxOutputTokens,
  string Model,
  List<OutputItem>? Output,
  Reasoning Reasoning,
  bool Store,
  double Temperature,
  TextObj Text,
  [property: JsonPropertyName("top_p")] double TopP,
  string Truncation,
  Usage Usage,
  string? User,
  Dictionary<string, object> Metadata
);

public record OutputItem(
  string Type,
  string Id,
  string Status,
  string Role,
  List<ContentItem>? Content
);

public record ContentItem(
  string Type,
  string Text,
  List<object> Annotations
);

public record Reasoning(
  string? Effort,
  string? Summary
);

public record TextObj(
  OutputFormat Format
);

public record OutputFormat(
  string Type
);

public record Usage(
  [property: JsonPropertyName("input_tokens")] int InputTokens,
  [property: JsonPropertyName("input_tokens_details")] InputTokensDetails InputTokensDetails,
  [property: JsonPropertyName("output_tokens")] int OutputTokens,
  [property: JsonPropertyName("output_tokens_details")] OutputTokensDetails OutputTokensDetails,
  [property: JsonPropertyName("total_tokens")] int TotalTokens
);

public record InputTokensDetails(
  [property: JsonPropertyName("cached_tokens")] int CachedTokens
);

public record OutputTokensDetails(
  [property: JsonPropertyName("reasoning_tokens")] int ReasoningTokens
);