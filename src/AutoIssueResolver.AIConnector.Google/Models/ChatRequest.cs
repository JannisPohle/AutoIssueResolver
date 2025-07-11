using System.Text;
using System.Text.Json.Serialization;
using AutoIssueResolver.AIConnector.Abstractions.Extensions;
using AutoIssueResolver.AIConnector.Abstractions.Models;

namespace AutoIssueResolver.AIConnector.Google.Models;

//TODO - Add file with shared models for request and response

internal record ChatRequest(List<Content> Contents, string? CachedContent = null, Content? SystemInstruction = null, GenerationConfiguration? GenerationConfig = null)
{
  public string? CachedContent { get; set; } = CachedContent;

  public Content? SystemInstruction { get; set; } = SystemInstruction;
}

[JsonDerivedType(typeof(TextPart))]
[JsonDerivedType(typeof(InlineDataPart))]
internal abstract record Part(bool Thought = false);
internal record TextPart(string Text) : Part;
internal record InlineDataPart([property: JsonPropertyName("inline_data")] InlineData InlineData) : Part;
internal record InlineData
{
  public InlineData(string mimeType, string data)
  {
    MimeType = mimeType;
    Data = Convert.ToBase64String(Encoding.UTF8.GetBytes(data));
  }

  [JsonPropertyName("mime_type")]
  public string MimeType { get; }

  public string Data { get; }

  public void Deconstruct(out string mimeType, out string data)
  {
    mimeType = this.MimeType;
    data = this.Data;
  }
}

internal record Content(List<Part> Parts, string Role = "user");
internal record GenerationConfiguration(string ResponseMimeType, object? ResponseSchema);

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

  public List<Content> Contents { get; }

  public string Model { get; }

  public string TTL { get; }

  public Content? SystemInstruction { get; }

  public string? DisplayName { get; }
};

internal record CachedContentResponse(string Name, UsageMetadata UsageMetadata);

internal record UsageMetadata(int PromptTokenCount, int CachedContentTokenCount, int TotalTokenCount, int CandidatesTokenCount, int ThoughtsTokenCount);