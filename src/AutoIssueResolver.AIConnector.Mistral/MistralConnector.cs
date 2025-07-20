using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using AutoIssueResolver.AIConnector.Abstractions.Configuration;
using AutoIssueResolver.AIConnector.Abstractions.Extensions;
using AutoIssueResolver.AIConnector.Abstractions.Models;
using AutoIssueResolver.AIConnector.Mistral.Models;
using AutoIssueResolver.AIConnectors.Base;
using AutoIssueResolver.AIConnectors.Base.UnifiedModels;
using AutoIssueResolver.GitConnector.Abstractions;
using AutoIssueResolver.Persistence.Abstractions.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Response = AutoIssueResolver.AIConnector.Mistral.Models.Response;

namespace AutoIssueResolver.AIConnector.Mistral;

public class MistralConnector(ILogger<MistralConnector> logger, [FromKeyedServices("mistral")] HttpClient httpClient, IOptions<AiAgentConfiguration> configuration, ISourceCodeConnector sourceCodeConnector, IReportingRepository reportingRepository): AIConnectorBase(logger, configuration, reportingRepository, httpClient, sourceCodeConnector)
{
  private const string API_PATH_CHAT = "v1/chat/completions";

  protected override List<AIModels> SupportedModels { get; } = [AIModels.DevstralSmall,];

  protected override async Task<object> CreateRequestObject(Prompt prompt, CancellationToken cancellationToken)
  {
    var request = new Request(configuration.Value.Model.GetModelName(), [new Message("system", SYSTEM_PROMPT), new Message("user", await PreparePromptText(prompt, cancellationToken))], new ResponseFormat(new JsonSchema(JsonNode.Parse(ResponseSchemaWithAdditionalProperties), "code_replacements")),
                              MAX_OUTPUT_TOKENS);

    return request;
  }

  protected override string GetResponsesApiPath()
  {
    return API_PATH_CHAT;
  }

  protected override async Task<AiResponse> ParseResponse(HttpResponseMessage response, CancellationToken cancellationToken)
  {
    var chatResponse = await response.Content.ReadFromJsonAsync<Response>(cancellationToken);
    if (chatResponse == null)
    {
      logger.LogWarning("No response received from Mistral API. Something seems to have gone wrong with the request.");
      throw new UnsuccessfulResultException("No response received from Mistral API", true);
    }
    var usageMetadata = new UsageMetadata(chatResponse.Usage?.PromptTokens ?? 0, 0, chatResponse.Usage?.TotalTokens ?? 0, chatResponse.Usage?.CompletionTokens ?? 0, 0);

    if (chatResponse.Choices == null || chatResponse.Choices.Count == 0)
    {
      logger.LogWarning("Mistral response has no choices. Something seems to have gone wrong with the request.");
      throw new UnsuccessfulResultException("Mistral response has no choices", true) { UsageMetadata = usageMetadata, };
    }

    var choice = chatResponse.Choices?.FirstOrDefault(choice => choice.Message.Role == "assistant");
    if (choice == null)
    {
      logger.LogWarning("Mistral response has no choice with the assistant role. Something seems to have gone wrong with the request.");
      throw new UnsuccessfulResultException("Mistral response has no choice with assistant role", true) { UsageMetadata = usageMetadata, };
    }

    if (choice.FinishReason != "stop")
    {
      logger.LogWarning("Mistral response finished with reason: {FinishReason}. Something seems to have gone wrong with the request.", choice.FinishReason);
      throw new UnsuccessfulResultException($"Mistral response finished with reason: {choice.FinishReason}", true) { UsageMetadata = usageMetadata, };
    }

    return new AiResponse(choice.Message.Content, usageMetadata);
  }

  private async Task<string> PreparePromptText(Prompt prompt, CancellationToken cancellationToken)
  {
    var files = await GetFileContents(prompt, cancellationToken);

    var sb = new StringBuilder();

    sb.AppendLine(prompt.PromptText);
    sb.AppendLine();
    sb.AppendLine();
    return FormatFilesForPromptText(files, sb).ToString();
  }
}