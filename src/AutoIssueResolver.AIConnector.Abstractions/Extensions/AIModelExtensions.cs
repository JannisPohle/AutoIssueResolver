using AutoIssueResolver.AIConnector.Abstractions.Models;

namespace AutoIssueResolver.AIConnector.Abstractions.Extensions;

public static class AIModelExtensions
{
  public static string GetModelName(this AIModels model)
  {
    return model switch
    {
      AIModels.GeminiFlashLite => "gemini-2.0-flash-lite",
      _ => throw new ArgumentOutOfRangeException(nameof(model), model, null)
    };
  }
}