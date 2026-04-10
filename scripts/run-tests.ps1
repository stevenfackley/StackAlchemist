param(
    [switch]$Quiet
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot
$workerProjectPath = Join-Path $repoRoot "src/StackAlchemist.Worker/StackAlchemist.Worker.csproj"
$engineTestsProjectPath = Join-Path $repoRoot "src/StackAlchemist.Engine.Tests/StackAlchemist.Engine.Tests.csproj"
$workerTestsProjectPath = Join-Path $repoRoot "src/StackAlchemist.Worker.Tests/StackAlchemist.Worker.Tests.csproj"
$dotnetCliHome = Join-Path $repoRoot ".dotnet-cli"

foreach ($requiredPath in @($workerProjectPath, $engineTestsProjectPath, $workerTestsProjectPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        throw "Could not find required project at $requiredPath"
    }
}

$null = New-Item -ItemType Directory -Path $dotnetCliHome -Force

$env:DOTNET_CLI_HOME = $dotnetCliHome
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_NOLOGO = "1"

$verbosity = if ($Quiet) { "quiet" } else { "minimal" }

Push-Location $repoRoot
try {
    & dotnet restore $workerProjectPath --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet restore failed with exit code $LASTEXITCODE"
    }

    foreach ($testProject in @($engineTestsProjectPath, $workerTestsProjectPath)) {
        & dotnet test $testProject --nologo --no-restore -v $verbosity
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet test failed for $testProject with exit code $LASTEXITCODE"
        }
    }
}
finally {
    Pop-Location
}
