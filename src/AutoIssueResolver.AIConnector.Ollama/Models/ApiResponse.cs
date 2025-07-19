using System.Text.Json.Serialization;

namespace AutoIssueResolver.AIConnector.Ollama.Models;

internal record ApiResponse(string Response, bool Done, [property: JsonPropertyName("done_reason")]  string DoneReason, [property: JsonPropertyName("prompt_eval_count")]  int PromptEvalCount, [property: JsonPropertyName("eval_count")]  int EvalCount);