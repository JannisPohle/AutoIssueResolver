using AutoIssueResolver.AIConnector.Abstractions.Models;

namespace AutoIssueResolver.Application;

/// <summary>
/// Contains prompts and response schemas used in the application.
/// </summary>
public static class Prompts
{
  /// <summary>
  /// Contains the default response schema, containing a placeholder for the "additionalProperties" field.
  /// </summary>
  public const string RESPONSE_SCHEMA_TEMPLATE = $$"""
                                         {
                                           "title": "Replacements",
                                           "description": "Contains a list of replacements that should be done in the code to fix the issue.",
                                           "type": "object",
                                           {{ResponseSchema.ADDITIONAL_PROPERTIES_PLACEHOLDER}}
                                           "required": ["replacements"],
                                           "properties": {
                                             "replacements": {
                                               "type": "array",
                                               "description": "A list of code replacements that should be applied to fix the issue.",
                                               {{ResponseSchema.ADDITIONAL_PROPERTIES_PLACEHOLDER}}
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
                                                  {{ResponseSchema.ADDITIONAL_PROPERTIES_PLACEHOLDER}}
                                                  "required": ["newCode", "filePath"]
                                               }
                                             }
                                           }
                                         }
                                         """;

  /// <summary>
  /// Contains the system prompt that is used to instruct the AI on how to fix code smells.
  /// </summary>
  public const string SYSTEM_PROMPT =
    "You are a Software Developer tasked with fixing Code Smells. You will receive a description for a code smell that should be fixed in a specific class, as well as the content of other possibly relevant classes. "
    + "Here are some rules that must be followed when fixing the code smell:\n"
    + "1. Respond only in the provided JSON format\n"
    + "2. Do not change anything else in the code, just fix the issue that is described in the request. Do not add any comments, explanations or unnecessary whitespace to the code. Do not change the formatting of the code.\n"
    + "3. Use the provided file paths in the responses to identify the files.\n"
    + "4. The response should contain the *complete* code for the files that should be changed.\n"
    + "5. Ensure that the code is still valid after your changes and compiles without errors. Do not change the code in a way that would break the compilation or introduce new issues.";
}