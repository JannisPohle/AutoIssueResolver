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

/// <summary>
///   Orchestrates the auto-fix process by integrating AI, code analysis, and source control.
/// </summary>
public class AutoFixOrchestrator(
  ILogger<AutoFixOrchestrator> logger,
  IServiceScopeFactory scopeFactory,
  IOptions<AiAgentConfiguration> aiConfiguration,
  IOptions<CodeAnalysisConfiguration> codeAnalysisConfiguration,
  IOptions<SourceCodeConfiguration> gitConfig,
  IHostApplicationLifetime hostApplicationLifetime,
  IRunMetadata metadata): BackgroundService
{
  #region Members

  private IAIConnector _aiConnector = null!;
  private ICodeAnalysisConnector _codeAnalysisConnector = null!;
  private ISourceCodeConnector _git = null!;
  private IReportingRepository _reportingRepository = null!;

  #endregion

  #region Methods

  /// <inheritdoc />
  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    logger.LogInformation("Starting TestHostedService");

    ValidateConfiguration();
    logger.LogDebug("Configuration validated");

    PrepareMetadata();
    logger.LogDebug("Metadata prepared: BranchName={BranchName}", metadata.BranchName);

    await using var scope = PrepareRequiredServices();
    logger.LogDebug("Required services prepared");

    await _reportingRepository!.InitializeApplicationRun(stoppingToken);
    logger.LogInformation("Application run initialized: {CorrelationId}", metadata.CorrelationId);

    try
    {
      OperationResult result;

      logger.LogDebug("Setting up source code repository");
      if (!(result = await SetupSourceCode(stoppingToken)).CanContinue)
      {
        logger.LogError("Source code setup failed, stopping application");
        StopApplication(result);
        return;
      }
      logger.LogDebug("Source code setup completed successfully");

      logger.LogDebug("Retrieving issues from code analysis");
      (var issues, result) = await GetIssues(stoppingToken);

      if (!result.CanContinue)
      {
        logger.LogError("Failed to retrieve issues, stopping application");
        StopApplication(result);
        return;
      }

      logger.LogInformation("Issues ({IssueCount}): {Issues}", issues.Count, issues);

      foreach (var issue in issues)
      {
        logger.LogDebug("Processing issue: {RuleId} in {FilePath}", issue.RuleIdentifier, issue.FilePath);
        await FixIssue(issue, stoppingToken);
      }

      logger.LogDebug("All issues have been worked on, pushing changes to remote repository");
      await _git.PushChanges(stoppingToken);
      logger.LogInformation("All issues have been worked on and changes are pushed to remote repository");
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Unhandled exception in orchestrator");
      StopApplication(OperationResult.FatalResult(ex, ExitCodes.UnknownError));
    }
    finally
    {
      logger.LogInformation("Ending application run: {CorrelationId}", metadata.CorrelationId);
      await _reportingRepository.EndApplicationRun(stoppingToken);
    }

    logger.LogInformation("Stopping application");
    hostApplicationLifetime.StopApplication();
  }

  /// <inheritdoc />
  public override async Task StopAsync(CancellationToken cancellationToken)
  {
    logger.LogInformation("Shutting down AutoFixOrchestrator");
    await base.StopAsync(cancellationToken);
  }

  private async Task<(List<Issue>, OperationResult)> GetIssues(CancellationToken stoppingToken)
  {
    try
    {
      logger.LogDebug("Requesting issues from code analysis connector");
      var issues = await _codeAnalysisConnector.GetIssues(new Project(codeAnalysisConfiguration.Value.ProjectKey!, "cs"), stoppingToken);

      logger.LogInformation("Retrieved {Count} issues from code analysis", issues.Count);
      return (issues, OperationResult.SuccessfulResult);
    }
    catch (Exception e)
    {
      logger.LogError(e, "Error retrieving issues from code analysis");
      return ([], OperationResult.FatalResult(e, ExitCodes.CodeAnalysisError));
    }
  }

  private async Task FixIssue(Issue issue, CancellationToken stoppingToken)
  {
    try
    {
      logger.LogTrace("Getting rule for issue {RuleId}", issue.RuleIdentifier);
      var rule = await _codeAnalysisConnector.GetRule(issue.RuleIdentifier, stoppingToken);

      logger.LogDebug("Creating prompt for issue {RuleId}", issue.RuleIdentifier);
      var prompt = await CreatePrompt(issue, rule);

      logger.LogDebug("Requesting AI response for issue {RuleId}", issue.RuleIdentifier);
      var response = await _aiConnector.GetResponse(prompt, stoppingToken);

      logger.LogDebug("AI response received for issue {RuleId}", issue.RuleIdentifier);
      logger.LogDebug("AI Response: {Response}", response);

      await ReplaceFileContents(response.CodeReplacement, issue);

      logger.LogDebug("Committing changes for issue {RuleId}", issue.RuleIdentifier);
      await _git.CommitChanges(GetCommitMessage(issue, rule), stoppingToken);

      logger.LogInformation("Issue {RuleId} fixed and committed", issue.RuleIdentifier);
    }
    catch (Exception e)
    {
      logger.LogWarning(e, "Failed to fix issue {RuleId} in {FilePath}", issue.RuleIdentifier, issue.FilePath);
    }
  }

  private async Task<Prompt> CreatePrompt(Issue issue, Rule rule)
  {
    logger.LogDebug("Fetching file content for {FilePath}", issue.FilePath);

    logger.LogTrace("Creating prompt for rule {RuleId} and file {FilePath}", rule.RuleId, issue.FilePath);
    return new Prompt($$"""
                        # Approach
                        To fix the code smell, please follow these steps:
                        1. **Understand the Code Smell**: Read the description of the code smell to understand what it is and why it is considered a problem.
                        2. **Analyze the Code**: Look at the provided code to identify where the code smell occurs.
                        3. **Propose a Fix**: Suggest a code change that addresses the code smell while maintaining the original functionality of the code.
                        
                        # Code Smell Details
                        
                        ** Programming Language**: C#
                        **Analysis Rule Key**: {{rule.RuleId}}
                        **Rule Title**: {{rule.Title}}
                        **File Path**: {{issue.FilePath}}
                        **Affected Lines**: {{issue.Range.StartLine}}-{{issue.Range.EndLine}}
                        **Code Smell Description**: {{rule.Description}}
                        """, rule.ShortIdentifier ?? string.Empty);
  }

  private async Task ReplaceFileContents(List<Replacement> replacements, Issue issue)
  {
    foreach (var replacement in replacements)
    {
      try
      {
        await UpdateFileContent(replacement);
      }
      catch (FileNotFoundException ex)
      {
        logger.LogDebug("Path {FilePath} provided by the AI response does not exist, trying to find a matching file by the name.", replacement.FilePath);
        var result = await TryFixFilePathAndUpdateContent(replacement, issue);
        if (!result)
        {
          logger.LogWarning(ex, "File {FilePath} does not exist and could therefore not be updated. Also no matching file found for the file name only in the repository", replacement.FilePath);
        }
        else
        {
          logger.LogDebug("File {FilePath} updated successfully by searching for a matching file name", replacement.FilePath);
        }
      }
      catch (Exception e)
      {
        logger.LogWarning(e, "Failed to update file {FileName}", replacement.FilePath);
      }
    }
  }

  private async Task UpdateFileContent(Replacement replacement)
  {
    logger.LogInformation("Updating file: {FileName}", replacement.FilePath);
    await _git.UpdateFileContent(replacement.FilePath, replacement.NewCode);
    logger.LogDebug("File {FileName} updated", replacement.FilePath);
  }
  
  private async Task<bool> TryFixFilePathAndUpdateContent(Replacement replacement, Issue issue)
  {
    // Get relevant files from the git repository
    var allFiles = await _git.GetAllFiles("*.cs", issue.RuleIdentifier.ShortIdentifier);
    // Find the file that matches the replacement file path
    var fileName = Path.GetFileName(replacement.FilePath);
    logger.LogDebug("Searching for file with name {FileName} in the repository", fileName);
    var matchingFile = allFiles.Where(f => Path.GetFileName(f.FilePath).Equals(fileName, StringComparison.OrdinalIgnoreCase)).ToList();

    if (matchingFile.Count == 0)
    {
      logger.LogDebug("No matching file found for {FileName} in the repository", fileName);
      return false;
    }

    if (matchingFile.Count > 1)
    {
      logger.LogDebug("Multiple matching files found for {FileName} in the repository. Cannot determine which file to update.", fileName);
      return false;
    }

    await _git.UpdateFileContent(matchingFile[0].FilePath, replacement.NewCode);
    logger.LogDebug("File {FileName} updated successfully", replacement.FilePath);

    return true;
  }

  private string GetCommitMessage(Issue issue, Rule rule)
  {
    var message = gitConfig.Value.CommitMessageTemplate.Replace("{{ID}}", rule.RuleId).Replace("{{TITLE}}", rule.Title).Replace("{{FILE_NAME}}", issue.FilePath);
    logger.LogDebug("Generated commit message: {CommitMessage}", message);
    return message;
  }

  private void PrepareMetadata()
  {
    var branchName = $"auto-fix/{aiConfiguration.Value.Model.GetModelVendor().Replace('(', '_').Replace(')', '_').Replace(' ', '-')}/{aiConfiguration.Value.Model.GetModelName().Replace(':', '-')}/{metadata.CorrelationId}-auto-fix";
    metadata.BranchName = branchName;
    metadata.ModelName = aiConfiguration.Value.Model.GetModelName();
    logger.LogDebug("Prepared metadata with branch name: {BranchName}", branchName);
  }

  private void ValidateConfiguration()
  {
    // Validate AI configuration
    if (aiConfiguration.Value == null || aiConfiguration.Value.Model == AIModels.None)
    {
      logger.LogError("Invalid AI configuration: Model or Token is missing.");
      throw new InvalidOperationException("AI configuration is invalid. Model and Token must be set.");
    }

    // Validate Code Analysis configuration
    if (codeAnalysisConfiguration.Value == null || string.IsNullOrWhiteSpace(codeAnalysisConfiguration.Value.ProjectKey) || codeAnalysisConfiguration.Value.Type == CodeAnalysisTypes.None)
    {
      logger.LogError("Invalid Code Analysis configuration: ProjectKey or Type is missing.");
      throw new InvalidOperationException("Code Analysis configuration is invalid. ProjectKey and Type must be set.");
    }

    // Validate Git configuration
    if (gitConfig.Value == null || string.IsNullOrWhiteSpace(gitConfig.Value.Repository) || string.IsNullOrWhiteSpace(gitConfig.Value.Branch) || string.IsNullOrWhiteSpace(gitConfig.Value.CommitMessageTemplate))
    {
      logger.LogError("Invalid Git configuration: Repository, Branch, or CommitMessageTemplate is missing.");
      throw new InvalidOperationException("Git configuration is invalid. Repository, Branch, and CommitMessageTemplate must be set.");
    }

    // Validate Git credentials
    if (gitConfig.Value.Credentials != null && !string.IsNullOrWhiteSpace(gitConfig.Value.Credentials.Username) && string.IsNullOrWhiteSpace(gitConfig.Value.Credentials.Password))
    {
      logger.LogError("Invalid Git credentials: Username is set, but password is missing.");
      throw new InvalidOperationException("Invalid Git credentials: Username is set, but password is missing");
    }

    logger.LogDebug("ValidateConfiguration completed successfully.");
  }

  private AsyncServiceScope PrepareRequiredServices()
  {
    logger.LogDebug("Preparing required services");
    var scope = scopeFactory.CreateAsyncScope();
    _codeAnalysisConnector = FindCodeAnalysisConnector(scope);
    _aiConnector = FindAiConnector(scope);
    _git = scope.ServiceProvider.GetRequiredService<ISourceCodeConnector>();
    _reportingRepository = scope.ServiceProvider.GetRequiredService<IReportingRepository>();
    logger.LogDebug("Required services resolved");
    return scope;
  }

  private async Task<OperationResult> SetupSourceCode(CancellationToken stoppingToken)
  {
    try
    {
      logger.LogDebug("Initializing repository");
      await _git.CloneRepository(stoppingToken);

      logger.LogDebug("Creating new branch: {BranchName}", metadata.BranchName);
      await _git.CreateBranch(metadata.BranchName!, stoppingToken);
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to setup source code");
      return OperationResult.FatalResult(e, ExitCodes.SourceCodeConnectionError);
    }

    return OperationResult.SuccessfulResult;
  }

  private IAIConnector FindAiConnector(AsyncServiceScope scope)
  {
    logger.LogDebug("Resolving AI connector for model {Model}", aiConfiguration.Value.Model);
    var aiConnector = scope.ServiceProvider.GetKeyedService<IAIConnector>(aiConfiguration.Value.Model);

    if (aiConnector == null)
    {
      logger.LogError("No AI connector found for model {Model}", aiConfiguration.Value.Model);
      throw new InvalidOperationException($"No AI connector found for model {aiConfiguration.Value.Model}");
    }

    logger.LogDebug("AI connector resolved");
    return aiConnector;
  }

  private ICodeAnalysisConnector FindCodeAnalysisConnector(AsyncServiceScope scope)
  {
    logger.LogDebug("Resolving Code Analysis connector for type {Type}", codeAnalysisConfiguration.Value.Type);
    var codeAnalysisConnector = scope.ServiceProvider.GetKeyedService<ICodeAnalysisConnector>(codeAnalysisConfiguration.Value.Type);

    if (codeAnalysisConnector == null)
    {
      logger.LogError("No Code Analysis connector found for type {Type}", codeAnalysisConfiguration.Value.Type);
      throw new InvalidOperationException($"No Code Analysis connector found for model {codeAnalysisConfiguration.Value.Type}");
    }

    logger.LogDebug("Code Analysis connector resolved");
    return codeAnalysisConnector;
  }

  private void StopApplication(OperationResult result)
  {
    logger.LogInformation("Stopping application with exit code {ExitCode}", result.ExitCodes);
    Environment.ExitCode = (int) result.ExitCodes;
    hostApplicationLifetime.StopApplication();
  }

  #endregion
}