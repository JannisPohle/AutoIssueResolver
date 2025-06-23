using System.Text.Json.Serialization;

namespace AutoIssueResolver.AIConnector.Google.Models;

public record ChatResponse(List<Choice> Choices);

public record Choice(Message Message);

public record Message(string Role, string Content);