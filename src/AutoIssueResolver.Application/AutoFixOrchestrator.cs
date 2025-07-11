using AutoIssueResolver.AIConnector.Abstractions;
using AutoIssueResolver.AIConnector.Abstractions.Configuration;
using AutoIssueResolver.AIConnector.Abstractions.Extensions;
using AutoIssueResolver.AIConnector.Abstractions.Models;
using AutoIssueResolver.Application.Abstractions;
using AutoIssueResolver.CodeAnalysisConnector.Abstractions;
using AutoIssueResolver.CodeAnalysisConnector.Abstractions.Configuration;
using AutoIssueResolver.CodeAnalysisConnector.Abstractions.Models;
using AutoIssueResolver.GitConnector.Abstractions;
using AutoIssueResolver.GitConnector.Abstractions.Configuration;
using AutoIssueResolver.Persistence.Abstractions.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutoIssueResolver.Application;

public class AutoFixOrchestrator(ILogger<AutoFixOrchestrator> logger, IServiceScopeFactory scopeFactory, IOptions<AiAgentConfiguration> aiConfiguration, IOptions<CodeAnalysisConfiguration> codeAnalysisConfiguration, IOptions<GitConfiguration> gitConfig, IHostApplicationLifetime hostApplicationLifetime, IRunMetadata metadata): BackgroundService
{
  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    await using var scope = scopeFactory.CreateAsyncScope();
    logger.LogInformation("Starting TestHostedService");

    var branchName = $"auto-fix/{aiConfiguration.Value.Model.GetModelVendor()}/{aiConfiguration.Value.Model.GetModelName()}/{metadata.CorrelationId}-auto-fix";
    metadata.BranchName = branchName;

    var reportingRepository = scope.ServiceProvider.GetRequiredService<IReportingRepository>();
    await reportingRepository.InitializeApplicationRun(stoppingToken);

    try
    {

      var codeAnalysisConnector = FindCodeAnalysisConnector(scope);
      var aiConnector = FindAIConnector(scope);
      var git = scope.ServiceProvider.GetRequiredService<ISourceCodeConnector>();
      await git.CloneRepository(stoppingToken);
      await git.CheckoutBranch(stoppingToken);
      await git.CreateBranch(branchName, stoppingToken);

      await aiConnector.SetupCaching(stoppingToken);

      var issues = await codeAnalysisConnector.GetIssues(new Project(codeAnalysisConfiguration.Value.ProjectKey, "cs" /*TODO make this configurable (list)*/), stoppingToken);
      logger.LogInformation("Issues ({issueCount}): {issues}", issues.Count, issues);

      foreach (var issue in issues.Take(1))
      {
        var rule = await codeAnalysisConnector.GetRule(issue.RuleIdentifier, stoppingToken);
        var prompt = await CreatePrompt(issue, rule, git);
        var response = await aiConnector.GetResponse(prompt, stoppingToken);
        logger.LogInformation("Response: {response}", response);
        await ReplaceFileContents(response.CodeReplacement.First(), issue.FilePath, git); //TODO replace multiple files, if necessary
        await git.CommitChanges(GetCommitMessage(issue, rule), stoppingToken);
        logger.LogInformation("AI Response retrieved successfully");
      }
      logger.LogInformation("All issues have been worked on, pushing changes to remote repository");
      await git.PushChanges(stoppingToken);
    }
    finally
    {
      await reportingRepository.EndApplicationRun(stoppingToken);
    }

    hostApplicationLifetime.StopApplication();
  }

  public async Task StopAsync(CancellationToken cancellationToken)
  {
    logger.LogInformation("Shutting down TestHostedService");
  }

  private IAIConnector FindAIConnector(AsyncServiceScope scope)
  {
    var aiConnector = scope.ServiceProvider.GetKeyedService<IAIConnector>(aiConfiguration.Value.Model);
    if (aiConnector == null)
    {
      throw new InvalidOperationException($"No AI connector found for model {aiConfiguration.Value.Model}");
    }
    return aiConnector;
  }
  
  private ICodeAnalysisConnector FindCodeAnalysisConnector(AsyncServiceScope scope)
  {
    var codeAnalysisConnector = scope.ServiceProvider.GetKeyedService<ICodeAnalysisConnector>(codeAnalysisConfiguration.Value.Type);
    if (codeAnalysisConnector == null)
    {
      throw new InvalidOperationException($"No Code Analysis connector found for model {codeAnalysisConfiguration.Value.Type}");
    }
    return codeAnalysisConnector;
  }

  private async Task<Prompt> CreatePrompt(Issue issue, Rule rule, ISourceCodeConnector sourceCodeConnector)
  {
    //TODO introduce CoT prompt
    //TODO optimize prompt for caching (ensure variable content is placed at the end)
    //TODO move some general restrictions into the system prompt (e.g. "You are a code assistant. Your task is to fix issues in code based on static code analysis.", // "Please provide the full updated file contents that fix the issue.")
    var fileContent = await sourceCodeConnector.GetFileContent(issue.FilePath);
    return new Prompt($$"""
                        You are a code assistant. Your task is to fix issues in code based on static code analysis.

                        **Analysis Rule Key**: {{rule.RuleId}}
                        **Rule Title**: {{rule.Title}}
                        **Issue Description**: {{rule.Description}}
                        **Affected Lines**: {{issue.Range.StartLine}}-{{issue.Range.EndLine}}

                        Below is the content of the affected file:

                        ```
                        {{fileContent}}
                        ```
                        """);
  }

  private async Task ReplaceFileContents(Replacement replacement, string filePath, ISourceCodeConnector sourceCodeConnector)
  {
    // var originalContent = await git.GetFileContent(filePath);
    // var lines = originalContent.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
    // if (replacement.StartingLine < 1 || replacement.EndLine > lines.Length)
    // {
    //   logger.LogWarning("Replacement line numbers are out of range.");
    //   throw new ArgumentOutOfRangeException("Replacement line numbers are out of range.");
    // }
    // var startLine = replacement.StartingLine - 1; // Adjust for zero-based index
    // var endLine = replacement.EndLine - 1; // Adjust for zero-based index
    // var newLines = new List<string>(lines);
    // newLines.RemoveRange(startLine, endLine - startLine + 1); // Remove the lines to be replaced
    // newLines.InsertRange(startLine, replacement.NewCode.Split(new[] { Environment.NewLine }, StringSplitOptions.None)); // Insert the new lines
    // var newContent = string.Join(Environment.NewLine, newLines);
    // await git.UpdateFileContent(filePath, newContent);
    await sourceCodeConnector.UpdateFileContent(filePath, replacement.NewCode);
  }

  private string GetCommitMessage(Issue issue, Rule rule)
  {
    return gitConfig.Value.CommitMessageTemplate.Replace("{{ID}}", rule.RuleId).Replace("{{TITLE}}", rule.Title).Replace("{{FILE_NAME}}", issue.FilePath);
  }
}