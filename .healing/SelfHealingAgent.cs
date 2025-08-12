using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;

namespace SelfHealingPipeline
{
    public class PipelineFailure
    {
        public string StepName { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public string Logs { get; set; } = string.Empty;
        public string Repository { get; set; } = string.Empty;
        public string Branch { get; set; } = string.Empty;
        public string Commit { get; set; } = string.Empty;
        public Dictionary<string, object> Context { get; set; } = new();
    }

    public class HealingAnalysis
    {
        public string RootCause { get; set; } = string.Empty;
        public int Confidence { get; set; }
        public List<HealingFix> Fixes { get; set; } = new();
        public bool CanAutomate { get; set; }
        public string Reasoning { get; set; } = string.Empty;
        public string RiskLevel { get; set; } = string.Empty;
    }

    public class HealingFix
    {
        public string Command { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string RiskLevel { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }

    public class ClaudeHealingAgent
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _model;
        private readonly HashSet<string> _safeCommands;
        private readonly HashSet<string> _forbiddenPatterns;

        public ClaudeHealingAgent(string apiKey, string model = "claude-3-5-sonnet-20241022")
        {
            _httpClient = new HttpClient();
            _apiKey = apiKey;
            _model = model;
            
            _safeCommands = new HashSet<string>
            {
                "dotnet restore", "dotnet build", "dotnet test", "dotnet clean",
                "npm install", "npm update", "npm run build", "npm run test",
                "git checkout", "git reset", "git clean",
                "powershell -Command", "pwsh -Command"
            };
            
            _forbiddenPatterns = new HashSet<string>
            {
                "rm -rf", "Remove-Item -Recurse -Force", "format c:",
                "sudo", "runas", "net user", "reg delete"
            };
        }

        public async Task<HealingAnalysis> AnalyzePipelineFailure(PipelineFailure failure)
        {
            var prompt = $@"You are an expert DevOps engineer analyzing a CI/CD pipeline failure in a .NET/C# project.

Pipeline Context:
- Repository: {failure.Repository}
- Branch: {failure.Branch}  
- Commit: {failure.Commit}
- Failed Step: {failure.StepName}
- Error Message: {failure.ErrorMessage}
- Build Logs (last 5000 chars): {failure.Logs.Substring(Math.Max(0, failure.Logs.Length - 5000))}
- Additional Context: {JsonSerializer.Serialize(failure.Context)}

Focus on common .NET/C# pipeline issues:
1. NuGet package restoration problems
2. Build configuration errors
3. Test failures and flaky tests
4. Dependency version conflicts
5. PowerShell script execution issues
6. Environment variable problems

Please analyze this failure and provide:
1. Root cause analysis
2. Confidence level (1-10) in your diagnosis  
3. Step-by-step fix recommendations (prefer PowerShell/dotnet CLI commands)
4. Risk assessment of suggested fixes
5. Whether the fix can be automated safely

Respond ONLY in this JSON format:
{{
  ""root_cause"": ""detailed explanation of the root cause"",
  ""confidence"": 8,
  ""fixes"": [
    {{
      ""command"": ""dotnet restore --force"",
      ""description"": ""Force restore NuGet packages"",
      ""risk_level"": ""low"",
      ""type"": ""dependency""
    }}
  ],
  ""can_automate"": true,
  ""reasoning"": ""explanation of why this fix is recommended"",
  ""risk_level"": ""low""
}}";

            var requestBody = new
            {
                model = _model,
                max_tokens = 4000,
                temperature = 0.1,
                messages = new[]
                {
                    new { role = "user", content = prompt }
                }
            };

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
            _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync("https://api.anthropic.com/v1/messages", content);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var claudeResponse = JsonSerializer.Deserialize<ClaudeResponse>(responseContent);
                
                var analysisJson = claudeResponse?.Content?[0]?.Text ?? "{}";
                
                // Clean up JSON if Claude adds markdown formatting
                analysisJson = analysisJson.Replace("```json", "").Replace("```", "").Trim();
                
                return JsonSerializer.Deserialize<HealingAnalysis>(analysisJson) ?? new HealingAnalysis();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calling Claude API: {ex.Message}");
                return new HealingAnalysis 
                { 
                    RootCause = $"Failed to analyze: {ex.Message}",
                    Confidence = 0,
                    CanAutomate = false
                };
            }
        }

        public async Task<bool> ExecuteHealingFixes(List<HealingFix> fixes)
        {
            foreach (var fix in fixes)
            {
                if (!ValidateSafetyOfCommand(fix.Command))
                {
                    Console.WriteLine($"❌ Skipping unsafe command: {fix.Command}");
                    continue;
                }

                Console.WriteLine($"🔧 Executing fix: {fix.Description}");
                Console.WriteLine($"   Command: {fix.Command}");

                var success = await ExecuteCommand(fix.Command);
                if (!success)
                {
                    Console.WriteLine($"❌ Fix failed: {fix.Description}");
                    return false;
                }
                
                Console.WriteLine($"✅ Fix completed: {fix.Description}");
            }

            return true;
        }

        private bool ValidateSafetyOfCommand(string command)
        {
            // Check for forbidden patterns
            foreach (var pattern in _forbiddenPatterns)
            {
                if (command.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            // Check if command starts with safe patterns
            foreach (var safeCmd in _safeCommands)
            {
                if (command.StartsWith(safeCmd, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            Console.WriteLine($"⚠️  Command not in safe list: {command}");
            return false;
        }

        private async Task<bool> ExecuteCommand(string command)
        {
            try
            {
                ProcessStartInfo startInfo;
                
                if (command.StartsWith("powershell", StringComparison.OrdinalIgnoreCase) || 
                    command.StartsWith("pwsh", StringComparison.OrdinalIgnoreCase))
                {
                    startInfo = new ProcessStartInfo
                    {
                        FileName = "pwsh",
                        Arguments = command.Substring(command.IndexOf("-Command") + 8).Trim(),
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };
                }
                else
                {
                    var parts = command.Split(' ', 2);
                    startInfo = new ProcessStartInfo
                    {
                        FileName = parts[0],
                        Arguments = parts.Length > 1 ? parts[1] : "",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };
                }

                using var process = Process.Start(startInfo);
                if (process == null) return false;

                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    Console.WriteLine($"Command failed with exit code {process.ExitCode}");
                    Console.WriteLine($"Error: {error}");
                    return false;
                }

                Console.WriteLine($"Output: {output}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception executing command: {ex.Message}");
                return false;
            }
        }

        private class ClaudeResponse
        {
            public ClaudeContent[]? Content { get; set; }
        }

        private class ClaudeContent
        {
            public string? Text { get; set; }
        }
    }

    // Main program for testing
    public class Program
    {
        public static async Task Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: SelfHealingAgent <anthropic-api-key> <failure-context-json>");
                return;
            }

            var apiKey = args[0];
            var failureJson = args[1];

            try
            {
                var failure = JsonSerializer.Deserialize<PipelineFailure>(failureJson);
                if (failure == null)
                {
                    Console.WriteLine("Invalid failure context JSON");
                    return;
                }

                var agent = new ClaudeHealingAgent(apiKey);
                
                Console.WriteLine("🔍 Analyzing pipeline failure...");
                var analysis = await agent.AnalyzePipelineFailure(failure);

                Console.WriteLine($"📊 Analysis Results:");
                Console.WriteLine($"   Root Cause: {analysis.RootCause}");
                Console.WriteLine($"   Confidence: {analysis.Confidence}/10");
                Console.WriteLine($"   Risk Level: {analysis.RiskLevel}");
                Console.WriteLine($"   Can Automate: {analysis.CanAutomate}");

                if (analysis.CanAutomate && analysis.Confidence >= 7)
                {
                    Console.WriteLine("\n🔧 Attempting automated healing...");
                    var success = await agent.ExecuteHealingFixes(analysis.Fixes);
                    
                    if (success)
                    {
                        Console.WriteLine("✅ Pipeline successfully healed!");
                        Environment.Exit(0);
                    }
                    else
                    {
                        Console.WriteLine("❌ Automated healing failed");
                        Environment.Exit(1);
                    }
                }
                else
                {
                    Console.WriteLine($"\n⚠️  Automated healing not recommended (confidence: {analysis.Confidence}, can_automate: {analysis.CanAutomate})");
                    Console.WriteLine("Suggested fixes:");
                    foreach (var fix in analysis.Fixes)
                    {
                        Console.WriteLine($"  - {fix.Description}: {fix.Command}");
                    }
                    Environment.Exit(1);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Environment.Exit(1);
            }
        }
    }
}