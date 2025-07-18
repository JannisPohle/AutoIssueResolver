using AutoIssueResolver.AIConnector.Abstractions;
using AutoIssueResolver.AIConnectors.Base.UnifiedModels;

namespace AutoIssueResolver.AIConnector.Google.Models;

/// <summary>
///   Represents a chat response from the Google AI connector.
/// </summary>
internal record ChatResponse(List<Candidate> Candidates, UsageMetadata UsageMetadata);

/// <summary>
///   Represents a candidate response.
/// </summary>
internal record Candidate(ResponseContent Content, string FinishReason);

/// <summary>
///   Content of a candidate response.
/// </summary>
internal record ResponseContent(List<TextPart> Parts);