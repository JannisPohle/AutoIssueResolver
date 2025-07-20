using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AutoIssueResolver.AIConnector.Abstractions;
using AutoIssueResolver.AIConnector.Abstractions.Configuration;
using AutoIssueResolver.AIConnector.Abstractions.Extensions;
using AutoIssueResolver.AIConnector.Abstractions.Models;
using AutoIssueResolver.AIConnector.OpenAI.Models;
using AutoIssueResolver.AIConnectors.Base;
using AutoIssueResolver.AIConnectors.Base.UnifiedModels;
using AutoIssueResolver.Application.Abstractions.Models;
using AutoIssueResolver.GitConnector.Abstractions;
using AutoIssueResolver.Persistence.Abstractions.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutoIssueResolver.AIConnector.OpenAI;

public class OpenAIConnector(ILogger<OpenAIConnector> logger, [FromKeyedServices("openAI")] HttpClient httpClient, IOptions<AiAgentConfiguration> configuration, ISourceCodeConnector sourceCodeConnector, IReportingRepository reportingRepository): AIConnectorBase(logger, configuration, reportingRepository, httpClient, sourceCodeConnector), IAIConnector
{
  private const string API_PATH_RESPONSES = "v1/responses";

  protected override List<AIModels> SupportedModels { get; } = [AIModels.GPT4oNano,];

  protected override async Task<object> CreateRequestObject(Prompt prompt, CancellationToken cancellationToken)
  {
    var request = new Request(await PreparePromptText(prompt, cancellationToken), configuration.Value.Model.GetModelName(), SYSTEM_PROMPT, new TextOptions(new Format("response_schema", JsonNode.Parse(ResponseSchemaWithAdditionalProperties))), MAX_OUTPUT_TOKENS);

    return request;
  }

  protected override string GetResponsesApiPath()
  {
    return API_PATH_RESPONSES;
  }

  protected override async Task<AiResponse> ParseResponse(HttpResponseMessage response, CancellationToken cancellationToken)
  {
    var chatResponse = await response.Content.ReadFromJsonAsync<ResponseRoot>(cancellationToken);

    if (chatResponse == null)
    {
      logger.LogWarning("No response received from OpenAI API. Something seems to have gone wrong with the request.");
      throw new UnsuccessfulResultException("No response received from OpenAI API", true);
    }

    var usageMetadata = new UsageMetadata(chatResponse.Usage?.InputTokens ?? 0, chatResponse.Usage?.InputTokensDetails?.CachedTokens ?? 0, chatResponse.Usage?.TotalTokens ?? 0, chatResponse.Usage?.OutputTokens ?? 0, chatResponse.Usage?.OutputTokensDetails?.ReasoningTokens ?? 0);

    if (!chatResponse.Status.Equals("completed", StringComparison.OrdinalIgnoreCase))
    {
      logger.LogWarning("OpenAI response finished with reason: {FinishReason}. Something seems to have gone wrong with the request.", chatResponse.Status);
      throw new UnsuccessfulResultException($"OpenAI response finished with reason: {chatResponse.Status}", true) {  UsageMetadata = usageMetadata, };
    }

    var responseContent = chatResponse.Output?.FirstOrDefault(o => o.Role == "assistant")?.Content?.FirstOrDefault(c => c.Type == "output_text")?.Text ?? string.Empty;

    if (string.IsNullOrWhiteSpace(responseContent))
    {
      logger.LogWarning("OpenAI response content is empty. Something seems to have gone wrong with the request.");
      throw new UnsuccessfulResultException("OpenAI response content is empty", true) {  UsageMetadata = usageMetadata, };
    }

    return new AiResponse(responseContent, usageMetadata);
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