using AutoIssueResolver.GitConnector.Abstractions;
using AutoIssueResolver.Persistence.Abstractions.Repositories;
using Microsoft.Extensions.Logging;

namespace AutoIssueResolver.AIConnectors.Base;

public class AIConnectorBase(ILogger<AIConnectorBase> logger)
{
  protected const string SYSTEM_PROMPT = "You are a helpful AI assistant that helps to fix code issues. You will receive a description for a code smell that should be fixed in a specific class. "
                                         + "The response should contain the complete code for the files that should be changed. "
                                         + "Use the provided file paths in the responses to identify the files. "
                                         + "Do not change anything else in the code, just fix the issues that are described in the prompt. Do not add any comments, explanations or unnecessary whitespace to the code.";

  private const string ADDITIONAL_PROPERTIES = "\"additionalProperties\": false,";
  private const string RESPONSE_SCHEMA = """
                                                                               {
                                                                                 "title": "Replacements",
                                                                                 "description": "Contains a list of replacements that should be done in the code to fix the issue.",
                                                                                 "type": "object",
                                                                                 {{ADDITIONAL_PROPERTIES}}
                                                                                 "required": ["replacements"],
                                                                                 "properties": {
                                                                                   "replacements": {
                                                                                     "type": "array",
                                                                                     "description": "A list of code replacements that should be applied to fix the issue.",
                                                                                     {{ADDITIONAL_PROPERTIES}}
                                                                                     "items": {
                                                                                        "type": "object",
                                                                                        "description": "Replacement for a specific file, that should be applied to fix an issue.",
                                                                                        "properties": {
                                                                                          "newCode": {
                                                                                            "type": "string",
                                                                                            "description": "The updated code that should replace the old code to fix the issue. Should contain the complete code for the file that should be changed"
                                                                                          },
                                                                                          "filePath": {
                                                                                            "type": "string",
                                                                                            "description": "The path of the file that should be changed (relative to the repository root). This should be the same path as provided in the source code files in the cache."
                                                                                          }
                                                                                        },
                                                                                        {{ADDITIONAL_PROPERTIES}}
                                                                                        "required": ["newCode", "filePath"]
                                                                                     }
                                                                                   }
                                                                                 }
                                                                               }
                                                                               """;

  /// <summary>
  /// Gets the default response json schema
  /// </summary>
  protected static string ResponseSchema => RESPONSE_SCHEMA.Replace("{{ADDITIONAL_PROPERTIES}}", "").ReplaceLineEndings();

  /// <summary>
  /// Gets the response json schema with "additionalProperties" set to false (required e.g. for OpenAI)
  /// </summary>
  protected static string ResponseSchemaWithAdditionalProperties => RESPONSE_SCHEMA.Replace("{{ADDITIONAL_PROPERTIES}}", ADDITIONAL_PROPERTIES).ReplaceLineEndings();
}