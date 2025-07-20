using System.Text.Json.Serialization;

namespace AutoIssueResolver.AIConnector.Anthropic.Models;

internal record Request(string Model, List<Message> Messages, List<SystemPrompt> System, [property: JsonPropertyName("max_tokens")]  int? MaxTokens = null);

internal record Message(string Content, string Role = "user");

internal record SystemPrompt(string Text, string Type = "text");