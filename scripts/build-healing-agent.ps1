# Build script for the healing agent
Write-Output "🔨 Building Self-Healing Agent..."

Push-Location ".healing"
try {
    dotnet build --configuration Release --output .
    
    if ($LASTEXITCODE -eq 0) {
        Write-Output "✅ Self-healing agent built successfully"
        Write-Output "📍 Agent location: .healing/SelfHealingAgent.exe"
    } else {
        Write-Output "❌ Failed to build self-healing agent"
        exit 1
    }
}
finally {
    Pop-Location
}