param([string]$BrowserPath = '')

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$previousBrowserOptIn = $env:COOKSHARE_RUN_BROWSER_TESTS
$previousPostgresOptIn = $env:COOKSHARE_RUN_POSTGRES_TESTS
$previousBrowserPath = $env:COOKSHARE_BROWSER_PATH
try {
    $env:COOKSHARE_RUN_BROWSER_TESTS = '1'
    $env:COOKSHARE_RUN_POSTGRES_TESTS = '0'
    if ($BrowserPath) { $env:COOKSHARE_BROWSER_PATH = $BrowserPath }
    dotnet test (Join-Path $repositoryRoot 'CookShare.Tests/CookShare.Tests.csproj') --no-restore --filter 'FullyQualifiedName~AccountSettingsBrowserTests' --logger 'console;verbosity=normal'
    if ($LASTEXITCODE -ne 0) { throw 'Live Account Settings browser verification failed.' }
} finally {
    $env:COOKSHARE_RUN_BROWSER_TESTS = $previousBrowserOptIn
    $env:COOKSHARE_RUN_POSTGRES_TESTS = $previousPostgresOptIn
    $env:COOKSHARE_BROWSER_PATH = $previousBrowserPath
}
