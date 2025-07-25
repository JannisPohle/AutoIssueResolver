SELECT
    '(' ||
    'applicationRunId: "' || ApplicationRunId || '",' ||
    'numberOfFailedRequests: ' || IFNULL(SUM(CASE WHEN Status = 2 THEN 1 ELSE 0 END), 0) || ',' ||
    'numberOfCompilationErrors: 0,' ||
    'issuesWithCompilationErrors: (),' ||
    'numberOfTestFailures: 0,' ||
    'issuesWithTestFailures: (),' ||
    'numberOfFixedIssues: 0,' ||
    'totalNumberOfRemainingIssues: 0,' ||
    'unfixedIssues: (),' ||
    'newIssues: (),' ||
    'totalTokensUsed: ' || IFNULL(SUM(TotalTokensUsed), 0) || ',' ||
    'inputTokensUsed: ' || IFNULL(SUM(PromptTokens), 0) || ',' ||
    'outputTokensUsed: ' || IFNULL(SUM(ResponseTokens), 0) || ',' ||
    'totalRunDurationInSeconds: ' || IFNULL((JULIANDAY(MAX(EndTimeUtc)) - JULIANDAY(MIN(StartTimeUtc))) * 86400.0, 0) || ',' ||
    'totalNumberOfRetriedRequests: ' || IFNULL(SUM(CASE WHEN Retries > 0 THEN 1 ELSE 0 END), 0) || ',' ||
    'totalNumberOfRetries: ' || IFNULL(SUM(Retries), 0) || ',' ||
    'issuesWithRetries: (' ||
    CASE
        WHEN GROUP_CONCAT(CASE WHEN Retries > 0 THEN '"' || CodeSmellReference || '"' END) IS NULL THEN ''
        ELSE GROUP_CONCAT(CASE WHEN Retries > 0 THEN '"' || CodeSmellReference || '"' END)
        END
        || '),' ||
    'pipelineLink: "",' ||
    'qualityNotes: "",' ||
    ')'
        AS testResults
FROM Requests
WHERE ApplicationRunId = '';