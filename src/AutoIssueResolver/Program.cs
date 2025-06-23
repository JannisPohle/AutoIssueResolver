using System.Net.Http.Headers;
using AutoIssueResolver.AIConnector.Abstractions;
using AutoIssueResolver.AIConnector.Abstractions.Configuration;
using AutoIssueResolver.AIConnector.Abstractions.Models;
using AutoIssueResolver.AIConnector.Google;
using AutoIssueResolver.Application;
using AutoIssueResolver.CodeAnalysisConnector.Abstractions;
using AutoIssueResolver.CodeAnalysisConnector.Abstractions.Configuration;
using AutoIssueResolver.CodeAnalysisConnector.Abstractions.Models;
using AutoIssueResolver.CodeAnalysisConnector.Sonarqube;
using AutoIssueResolver.GitConnector;
using AutoIssueResolver.GitConnector.Abstractions;
using AutoIssueResolver.GitConnector.Abstractions.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// handle configuration
builder.Configuration.AddJsonFile("appSettings.json");
if (builder.Environment.IsDevelopment())
{
  builder.Configuration.AddJsonFile("appSettings.Development.json", optional: true);
}
builder.Configuration.AddEnvironmentVariables("AUTO_ISSUE_RESOLVER_");

builder.Services.Configure<AiAgentConfiguration>(builder.Configuration.GetSection("AiAgent"));
builder.Services.Configure<CodeAnalysisConfiguration>(builder.Configuration.GetSection("CodeAnalysis"));
builder.Services.Configure<GitConfiguration>(builder.Configuration.GetSection("GitConfig"));

builder.Services.AddLogging(loggingBuilder =>
{
  loggingBuilder.AddDebug();
  loggingBuilder.AddConsole();
});

// Configure the ai endpoints
builder.Services.AddHttpClient("google", configureClient =>
{
  configureClient.BaseAddress = new Uri("https://generativelanguage.googleapis.com/v1beta/openai");
  configureClient.DefaultRequestHeaders.Add("Accept", "application/json");
  configureClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", builder.Configuration.GetValue<string>("AiAgent:token"));
}).AddAsKeyed(); // TODO add retry policy for rate limiting

builder.Services.AddHttpClient("sonarqube", configureClient =>
{
  configureClient.BaseAddress = new Uri(builder.Configuration.GetValue<string>("CodeAnalysis:serverUrl"));
  configureClient.DefaultRequestHeaders.Add("Accept", "application/json");
  configureClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", builder.Configuration.GetValue<string>("CodeAnalysis:token"));
}).AddAsKeyed(); // TODO add retry policy


// Register the services
builder.Services.AddHostedService<TestHostedService>()
       .AddTransient<IGit, GitConnector>();
// AI Connectors
builder.Services.AddKeyedTransient<IAIConnector, GeminiConnector>(AIModels.GeminiFlashLite);

// Code Analysis Connectors
builder.Services.AddKeyedTransient<ICodeAnalysisConnector, SonarqubeConnector>(CodeAnalysisTypes.Sonarqube);

var app = builder.Build();

await app.RunAsync();