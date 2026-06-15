param(
    [string]$Url = "http://127.0.0.1:5088",
    [string]$DataRoot = "",
    [string]$AutomationDll = "",
    [string]$AutomationExe = ""
)

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "BuildServer/BuildServer.csproj"

if ($DataRoot) { $env:BUILD_SERVER_DATA_ROOT = $DataRoot }
if ($AutomationDll) { $env:BUILD_SERVER_AUTOMATION_DLL = $AutomationDll }
if ($AutomationExe) { $env:BUILD_SERVER_AUTOMATION_EXE = $AutomationExe }
$env:ASPNETCORE_URLS = $Url

dotnet run --project $project
