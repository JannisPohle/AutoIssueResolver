using System.Text.Json.Serialization;

namespace AutoIssueResolver.AIConnector.OpenAI.Models;

internal record Request(string Input, string Model, string? Instructions, TextOptions? Text, [property: JsonPropertyName("max_output_tokens")] int? MaxOutputTokens);

internal record TextOptions(Format Format);

internal record Format(string Name, object? Schema, string Type = "json_schema");