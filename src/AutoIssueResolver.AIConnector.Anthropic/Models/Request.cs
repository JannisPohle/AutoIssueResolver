using System.Text.Json.Serialization;

namespace AutoIssueResolver.AIConnector.Anthropic.Models;

internal record Request(string Model, List<Message> Messages, string System, [property: JsonPropertyName("max_tokens")]  int? MaxTokens = null);

internal record Message(string Content, string Role = "user");