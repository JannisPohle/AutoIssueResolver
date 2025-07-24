using System.Text.Json.Serialization;

namespace AutoIssueResolver.AIConnector.Mistral.Models;

internal record Request(string Model, List<Message> Messages, [property: JsonPropertyName("response_format")] ResponseFormat? ResponseFormat, [property: JsonPropertyName("max_tokens")] int? MaxTokens = null);

internal record Message(string Role, string Content);

internal record ResponseFormat([property: JsonPropertyName("json_schema")] JsonSchema JSonSchema, string Type = "json_schema");

internal record JsonSchema(object? Schema, string Name, bool Strict = true);