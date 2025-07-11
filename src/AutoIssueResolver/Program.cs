using System.Net.Http.Headers;
using AutoIssueResolver;
using AutoIssueResolver.AIConnector.Abstractions;
using AutoIssueResolver.AIConnector.Abstractions.Configuration;
using AutoIssueResolver.AIConnector.Abstractions.Models;
using AutoIssueResolver.AIConnector.Google;
using AutoIssueResolver.Application;
using AutoIssueResolver.Application.Abstractions;
using AutoIssueResolver.CodeAnalysisConnector.Abstractions;
using AutoIssueResolver.CodeAnalysisConnector.Abstractions.Configuration;
using AutoIssueResolver.CodeAnalysisConnector.Abstractions.Models;
using AutoIssueResolver.CodeAnalysisConnector.Sonarqube;
using AutoIssueResolver.GitConnector;
using AutoIssueResolver.GitConnector.Abstractions;
using AutoIssueResolver.GitConnector.Abstractions.Configuration;
using AutoIssueResolver.Persistence;
using AutoIssueResolver.Persistence.Abstractions.Configuration;
using AutoIssueResolver.Persistence.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// handle configuration
builder.Configuration.AddJsonFile("appSettings.json");

if (builder.Environment.IsDevelopment())
{
  builder.Configuration.AddJsonFile("appSettings.Development.json", true);
}

builder.Configuration.AddEnvironmentVariables("AUTO_ISSUE_RESOLVER_");

var dbConfigurationSection = builder.Configuration.GetSection("Db");
builder.Services.Configure<DatabaseConfiguration>(dbConfigurationSection);
builder.Services.Configure<AiAgentConfiguration>(builder.Configuration.GetSection("AiAgent"));
builder.Services.Configure<CodeAnalysisConfiguration>(builder.Configuration.GetSection("CodeAnalysis"));
builder.Services.Configure<SourceCodeConfiguration>(builder.Configuration.GetSection("GitConfig"));

builder.Services.AddLogging(loggingBuilder =>
{
  loggingBuilder.AddDebug();
  loggingBuilder.AddConsole();
});

// Configure the ai endpoints
builder.Services.AddHttpClient("google", configureClient =>
       {
         configureClient.BaseAddress = new Uri("https://generativelanguage.googleapis.com");
         configureClient.DefaultRequestHeaders.Add("Accept", "application/json");
       })
       .AddAsKeyed()
       .AddPolicyHandler((serviceProvider, _) => PolicyExtensions.GetRetryPolicy<GeminiConnector>(serviceProvider));

builder.Services.AddHttpClient("sonarqube", configureClient =>
       {
         configureClient.BaseAddress = new Uri(builder.Configuration.GetValue<string>("CodeAnalysis:serverUrl") ?? string.Empty);
         configureClient.DefaultRequestHeaders.Add("Accept", "application/json");
         configureClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", builder.Configuration.GetValue<string>("CodeAnalysis:token"));
       })
       .AddAsKeyed()
       .AddPolicyHandler((serviceProvider, _) => PolicyExtensions.GetRetryPolicy<SonarqubeConnector>(serviceProvider));

// Register the services
builder.Services
       .AddHostedService<MigrationRunner>() // Must be registered as the first hosted service, to ensure the database is available for subsequent services!
       .AddHostedService<AutoFixOrchestrator>()
       .AddTransient<ISourceCodeConnector, GitConnector>()
       .AddSingleton<IRunMetadata, RunMetadata>();

// AI Connectors
builder.Services.AddKeyedTransient<IAIConnector, GeminiConnector>(AIModels.GeminiFlashLite);

// Code Analysis Connectors
builder.Services.AddKeyedTransient<ICodeAnalysisConnector, SonarqubeConnector>(CodeAnalysisTypes.Sonarqube);

// Persistence
builder.Services.AddSqlitePersistence(dbConfigurationSection.Get<DatabaseConfiguration>());

var app = builder.Build();

await app.RunAsync();