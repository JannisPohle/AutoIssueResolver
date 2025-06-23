using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using AutoIssueResolver.AIConnector.Abstractions;
using AutoIssueResolver.AIConnector.Abstractions.Extensions;
using AutoIssueResolver.AIConnector.Abstractions.Models;
using AutoIssueResolver.AIConnector.Google.Models;
using Microsoft.Extensions.DependencyInjection;

namespace AutoIssueResolver.AIConnector.Google;

public class GeminiConnector([FromKeyedServices("google")] HttpClient httpClient): IAIConnector
{
  #region Static

  private const string API_PATH_CHAT = "chat/completions";

  #endregion

  #region Methods

  public async Task<bool> CanHandleModel(AIModels model, CancellationToken cancellationToken = default)
  {
    if (AIModels.GeminiFlashLite == model)
    {
      return true;
    }

    return false;
  }

  public async Task<Response> GetResponse(Prompt prompt, CancellationToken cancellationToken = default)
  {
    if (!await CanHandleModel(prompt.Model, cancellationToken))
    {
      throw new InvalidOperationException("The model is not supported by this connector.");
    }

    var jsonSchema = """
                     {
                       "description": "Replacements",
                       "type": "object",
                       "properties": {
                         "newCode": {
                           "type": "string",
                           "description": "The updated code that should replace the old code to fix the issue."
                         },
                         "startingLine": {
                           "type": "integer",
                           "description": "The line number of the original file content where the new code starts to replace existing code."
                         },
                         "endLine": {
                           "type": "integer",
                           "description": "The line number of the original file content where the new code ends to replace existing code"
                         }
                       },
                       "required": ["newCode", "startingLine", "endLine"]
                     }
                     """.ReplaceLineEndings();

    var request = new ChatRequest(
                                  prompt.Model.GetModelName(),
                                  [new Message("user", prompt.PromptText)],
                                  new ResponseFormat("json_schema", new JsonSchema("Code Replacements", JsonObject.Parse(jsonSchema)))
                                 );

    var response = await httpClient.PostAsJsonAsync(API_PATH_CHAT, request, cancellationToken);

    if (!response.IsSuccessStatusCode)
    {
      throw new Exception($"Failed to get response from Gemini ({response.ReasonPhrase}): {await response.Content.ReadAsStringAsync(cancellationToken)}");
    }

    var chatResponse = await response.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken);

    var responseContent = chatResponse?.Choices.FirstOrDefault()?.Message.Content;
    var replacment = JsonSerializer.Deserialize<Replacement>(responseContent, JsonSerializerOptions.Web);

    return new Response(chatResponse?.Choices.FirstOrDefault()?.Message.Content ?? string.Empty, replacment);
  }

  #endregion
}