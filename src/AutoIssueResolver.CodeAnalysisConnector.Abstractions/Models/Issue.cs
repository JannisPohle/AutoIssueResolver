namespace AutoIssueResolver.CodeAnalysisConnector.Abstractions.Models;

public record TextRange(int StartLine, int EndLine);

public record Issue(RuleIdentifier RuleIdentifier, string FilePath, TextRange Range);