namespace AutoIssueResolver.AIConnector.Abstractions.Models;

/// <summary>
///   Represents a prompt to be sent to the AI.
/// </summary>
public record Prompt(string PromptText, string RuleId, SystemPrompt? SystemPrompt = null, ResponseSchema? ResponseSchema = null);

/// <summary>
/// Represents a system prompt that provides context or instructions for the AI.
/// </summary>
public record SystemPrompt(string SystemPromptText);

/// <summary>
/// Represents a schema for the response from the AI.
/// </summary>
public record ResponseSchema(string Schema, SchemaType SchemaType = SchemaType.Json)
{
  private const string ADDITIONAL_PROPERTIES = "\"additionalProperties\": false,";

  /// <summary>
  /// Placeholder that can be added to a schema to indicate where additional properties should be inserted (if required). Use <see cref="ResponseSchemaText"/> or <see cref="ResponseSchemaTextWithAdditionalProperties"/> for the actual schema text.
  /// </summary>
  public const string ADDITIONAL_PROPERTIES_PLACEHOLDER = "{{ADDITIONAL_PROPERTIES}}";
  
  /// <summary>
  ///   Gets the default response json schema
  /// </summary>
  public string ResponseSchemaText => Schema.Replace(ADDITIONAL_PROPERTIES_PLACEHOLDER, "").ReplaceLineEndings();

  /// <summary>
  ///   Gets the response json schema with "additionalProperties" set to false (required e.g. for OpenAI)
  /// </summary>
  public string ResponseSchemaTextWithAdditionalProperties => Schema.Replace(ADDITIONAL_PROPERTIES_PLACEHOLDER, ADDITIONAL_PROPERTIES).ReplaceLineEndings();
};

/// <summary>
/// Contains all supported schema types for AI responses.
/// </summary>
public enum SchemaType
{
  Json,
}