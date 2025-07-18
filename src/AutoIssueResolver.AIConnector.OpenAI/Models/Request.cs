namespace AutoIssueResolver.AIConnector.OpenAI.Models;

internal record Request(string Input, string Model, string? Instructions, TextOptions? Text);

internal record TextOptions(Format Format);

internal record Format(string Name, object? Schema, string Type = "json_schema");