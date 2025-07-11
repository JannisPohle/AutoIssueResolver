using System.Text;
using System.Text.Json.Serialization;
using AutoIssueResolver.AIConnector.Abstractions.Extensions;
using AutoIssueResolver.AIConnector.Abstractions.Models;

namespace AutoIssueResolver.AIConnector.Google.Models;

//TODO - Add file with shared models for request and response

/// <summary>
/// Represents a chat request to the Google AI connector.
/// </summary>
internal record ChatRequest(List<Content> Contents, string? CachedContent = null, Content? SystemInstruction = null, GenerationConfiguration? GenerationConfig = null)
{
  /// <summary>
  /// Cached content to use for the request, if available.
  /// </summary>
  public string? CachedContent { get; set; } = CachedContent;

  /// <summary>
  /// System instruction to guide the AI, if provided.
  /// </summary>
  public Content? SystemInstruction { get; set; } = SystemInstruction;
}

/// <summary>
/// Represents a part of the chat content.
/// </summary>
[JsonDerivedType(typeof(TextPart))]
[JsonDerivedType(typeof(InlineDataPart))]
internal abstract record Part(bool Thought = false);

/// <summary>
/// Text part of the chat content.
/// </summary>
internal record TextPart(string Text) : Part;

/// <summary>
/// Inline data part of the chat content.
/// </summary>
internal record InlineDataPart([property: JsonPropertyName("inline_data")] InlineData InlineData) : Part;

/// <summary>
/// Represents inline data with MIME type and base64-encoded content.
/// </summary>
internal record InlineData
{
  public InlineData(string mimeType, string data)
  {
    MimeType = mimeType;
    Data = Convert.ToBase64String(Encoding.UTF8.GetBytes(data));
  }

  /// <summary>
  /// MIME type of the data.
  /// </summary>
  [JsonPropertyName("mime_type")]
  public string MimeType { get; }

  /// <summary>
  /// Base64-encoded data.
  /// </summary>
  public string Data { get; }

  public void Deconstruct(out string mimeType, out string data)
  {
    mimeType = this.MimeType;
    data = this.Data;
  }
}

/// <summary>
/// Represents chat content with parts and a role.
/// </summary>
internal record Content(List<Part> Parts, string Role = "user");

/// <summary>
/// Configuration for response generation.
/// </summary>
internal record GenerationConfiguration(string ResponseMimeType, object? ResponseSchema);

/// <summary>
/// Represents cached chat content for reuse.
/// </summary>
internal record CachedContent
{
  public CachedContent(List<Content> contents, AIModels model, string ttl, Content? systemInstruction = null, string? displayName = null)
  {
    Contents = contents;
    Model = $"models/{model.GetModelName()}";
    TTL = ttl;
    SystemInstruction = systemInstruction;
    DisplayName = displayName;
  }

  /// <summary>
  /// List of chat contents.
  /// </summary>
  public List<Content> Contents { get; }

  /// <summary>
  /// Model identifier.
  /// </summary>
  public string Model { get; }

  /// <summary>
  /// Time-to-live for the cache entry.
  /// </summary>
  public string TTL { get; }

  /// <summary>
  /// Optional system instruction.
  /// </summary>
  public Content? SystemInstruction { get; }

  /// <summary>
  /// Optional display name for the cache entry.
  /// </summary>
  public string? DisplayName { get; }
};

/// <summary>
/// Response for a cached content request.
/// </summary>
internal record CachedContentResponse(string Name, UsageMetadata UsageMetadata);

/// <summary>
/// Metadata about token usage in a request.
/// </summary>
internal record UsageMetadata(int PromptTokenCount, int CachedContentTokenCount, int TotalTokenCount, int CandidatesTokenCount, int ThoughtsTokenCount);