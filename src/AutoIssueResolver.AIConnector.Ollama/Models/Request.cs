namespace AutoIssueResolver.AIConnector.Ollama.Models;

internal record Request(string Model, string Prompt, string? System, object? Format, bool? Think = null, bool Stream = false);
