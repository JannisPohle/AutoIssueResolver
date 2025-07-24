using AutoIssueResolver.AIConnector.Abstractions.Models;

namespace AutoIssueResolver.Application;

/// <summary>
/// Contains prompts and response schemas used in the application.
/// </summary>
public static class Prompts
{
  public const string RULE_ID_PLACEHOLDER = "{{RULE_ID}}";
  public const string RULE_TITLE_PLACEHOLDER = "{{RULE_TITLE}}";
  public const string ISSUE_FILE_PATH_PLACEHOLDER = "{{ISSUE_FILE_PATH}}";
  public const string ISSUE_RANGE_START_LINE_PLACEHOLDER = "{{ISSUE_RANGE_START_LINE}}";
  public const string ISSUE_RANGE_END_LINE_PLACEHOLDER = "{{ISSUE_RANGE_END_LINE}}";
  public const string ISSUE_DESCRIPTION_PLACEHOLDER = "{{ISSUE_DESCRIPTION}}";
  public const string PROGRAMMING_LANGUAGE_PLACEHOLDER = "{{PROGRAMMING_LANGUAGE}}";

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

  public const string PROMPT_TEMPLATE = $$"""
                                          # Approach
                                          To fix the code smell, please follow these steps:
                                          1. **Understand the Code Smell**: Read the description of the code smell to understand what it is and why it is considered a problem.
                                          2. **Analyze the Code**: Look at the provided code to identify where the code smell occurs.
                                          3. **Propose a Fix**: Suggest a code change that addresses the code smell while maintaining the original functionality of the code.

                                          # Code Smell Details

                                          **Programming Language**: {{PROGRAMMING_LANGUAGE_PLACEHOLDER}}
                                          **Analysis Rule Key**: {{RULE_ID_PLACEHOLDER}}
                                          **Rule Title**: {{RULE_TITLE_PLACEHOLDER}}
                                          **File Path**: {{ISSUE_FILE_PATH_PLACEHOLDER}}
                                          **Affected Lines**: {{ISSUE_RANGE_START_LINE_PLACEHOLDER}}-{{ISSUE_RANGE_END_LINE_PLACEHOLDER}}

                                          ## Code Smell Description
                                           {{ISSUE_DESCRIPTION_PLACEHOLDER}}
                                          """;
}