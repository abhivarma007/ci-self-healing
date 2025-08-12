# ci-self-healing
This POV is to test the self healing worfklows

# Self-Healing Pipeline

This repository includes an automated self-healing CI/CD pipeline that uses Claude AI to analyze and fix common build issues.

## Quick Start

1. Add `ANTHROPIC_API_KEY` to repository secrets
2. Push code to main branch
3. Watch the pipeline self-heal failures automatically

## How It Works

1. **Main Pipeline** runs your build/test
2. **On Failure** → triggers self-healing job
3. **Claude Analysis** → AI analyzes the error
4. **Auto-Fix** → executes safe healing commands
5. **Retry Pipeline** → re-runs if healing succeeds

## Get Anthropic API Key

1. Visit https://console.anthropic.com/
2. Create account and API key
3. Add as `ANTHROPIC_API_KEY` secret in GitHub repo settings

## Files Structure

- `.github/workflows/self-healing-pipeline.yml` - Main workflow
- `.healing/SelfHealingAgent.cs` - C# healing agent
- `.healing/SelfHealingPipeline.ps1` - PowerShell orchestrator
- `scripts/` - Helper scripts