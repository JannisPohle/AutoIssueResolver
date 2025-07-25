using AutoIssueResolver.AIConnector.Abstractions.Models;

namespace AutoIssueResolver.AIConnector.Abstractions.Extensions;

/// <summary>
///   Provides extension methods for the <see cref="AIModels" /> enum.
/// </summary>
public static class AiModelExtensions
{
  #region Methods

  /// <summary>
  ///   Gets the standardized model name for the specified AI model.
  /// </summary>
  /// <exception cref="ArgumentOutOfRangeException">
  ///   Thrown when the <paramref name="model" /> value is not recognized.
  /// </exception>
  public static string GetModelName(this AIModels model)
  {
    return model switch
    {
      AIModels.GeminiFlashLite => "gemini-2.0-flash-lite",
      AIModels.Gemini25Pro => "gemini-2.5-pro",
      AIModels.Gemini20Flash => "gemini-2.0-flash",
      AIModels.GPT4oNano => "gpt-4.1-nano",
      AIModels.GPT41 => "gpt-4.1",
      AIModels.GPT41Mini => "gpt-4.1-mini",
      AIModels.GPT4o => "gpt-4o",
      AIModels.o3 => "o3",
      AIModels.o3Mini => "o3-mini",
      AIModels.o4Mini => "o4-mini",
      AIModels.ClaudeHaiku3 => "claude-3-haiku-20240307",
      AIModels.DevstralSmall => "devstral-small-2505",
      AIModels.Phi4 => "phi4:latest",
      AIModels.DeepSeekChat => "deepseek-chat",
      _ => throw new ArgumentOutOfRangeException(nameof(model), model, "Unsupported AI model."),
    };
  }

  /// <summary>
  ///   Gets the vendor name associated with the specified AI model.
  /// </summary>
  /// <exception cref="ArgumentOutOfRangeException">
  ///   Thrown when the <paramref name="model" /> value is not recognized.
  /// </exception>
  public static string GetModelVendor(this AIModels model)
  {
    switch (model)
    {
      case AIModels.GeminiFlashLite:
      case AIModels.Gemini20Flash:
      case AIModels.Gemini25Pro:
        return "Google";
      case AIModels.GPT4oNano:
      case AIModels.GPT41:
      case AIModels.GPT41Mini:
      case AIModels.GPT4o:
      case AIModels.o3:
      case AIModels.o3Mini:
      case AIModels.o4Mini:
        return "OpenAI";
      case AIModels.ClaudeHaiku3:
        return "Anthropic";
      case AIModels.DevstralSmall:
        return "MistralAI";
      case AIModels.Phi4:
        return "Microsoft (Lokal)";
      case AIModels.DeepSeekChat:
        return "DeepSeek";
      default:
        throw new ArgumentOutOfRangeException(nameof(model), model, "Unsupported AI model.");
    }
  }

  /// <summary>
  /// Returns a value indicating whether the specified AI model is a reasoning model.
  /// </summary>
  /// <param name="model"></param>
  /// <returns></returns>
  /// <exception cref="ArgumentOutOfRangeException"></exception>
  public static bool IsReasoningModel(this AIModels model)
  {
    switch (model)
    {
      case AIModels.GeminiFlashLite:
      case AIModels.Gemini20Flash:
      case AIModels.GPT4oNano:
      case AIModels.GPT41:
      case AIModels.GPT41Mini:
      case AIModels.GPT4o:
      case AIModels.ClaudeHaiku3:
      case AIModels.DevstralSmall:
      case AIModels.Phi4:
      case AIModels.DeepSeekChat:
        return false;
      case AIModels.Gemini25Pro:
      case AIModels.o3:
      case AIModels.o3Mini:
      case AIModels.o4Mini:
        return true;
      default:
        throw new ArgumentOutOfRangeException(nameof(model), model, "Unsupported AI model.");
    }
  }

  #endregion
}