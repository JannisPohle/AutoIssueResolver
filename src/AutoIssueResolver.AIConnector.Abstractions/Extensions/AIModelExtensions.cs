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
      AIModels.GPT4oNano => "gpt-4.1-nano",
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
    return model switch
    {
      AIModels.GeminiFlashLite => "Google",
      AIModels.GPT4oNano => "OpenAI",
      _ => throw new ArgumentOutOfRangeException(nameof(model), model, "Unsupported AI model."),
    };
  }

  #endregion
}