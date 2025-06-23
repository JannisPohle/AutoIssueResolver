using System.Text.Json.Serialization;

namespace AutoIssueResolver.AIConnector.Google.Models;

public record ChatRequest(string Model, List<Message> Messages, [property: JsonPropertyName("response_format")] ResponseFormat? ResponseFormat = null);

public record ResponseFormat(string Type, [property: JsonPropertyName("json_schema")] JsonSchema? JsonSchema);

public record JsonSchema(string Name, object? Schema);