using System.Text.Json.Serialization;

namespace AutoIssueResolver.AIConnector.Google.Models;

internal record ChatResponse(List<Candidate> Candidates, UsageMetadata UsageMetadata);

internal record Candidate(ResponseContent Content);

internal record ResponseContent(List<TextPart> Parts);