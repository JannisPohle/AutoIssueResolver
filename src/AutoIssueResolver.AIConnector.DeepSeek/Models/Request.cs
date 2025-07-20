using System.Text.Json.Serialization;

namespace AutoIssueResolver.AIConnector.DeepSeek.Models;

internal record Request(string Model, List<Message> Messages, [property: JsonPropertyName("response_format")] ResponseFormat ResponseFormat, [property: JsonPropertyName("max_tokens")] int? MaxTokens = null);

internal record Message(string Content, string Role = "user");

internal record ResponseFormat(string Type = "json_object");