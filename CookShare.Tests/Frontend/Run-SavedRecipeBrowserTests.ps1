param(
    [string]$BrowserPath = '',
    [ValidateSet('Desktop', 'Mobile')]
    [string]$Viewport = 'Desktop'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if ([string]::IsNullOrWhiteSpace($BrowserPath)) {
    $browserCandidates = @(
        'C:\Program Files\Google\Chrome\Application\chrome.exe',
        'C:\Program Files (x86)\Google\Chrome\Application\chrome.exe',
        'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe'
    )
    $BrowserPath = $browserCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if ([string]::IsNullOrWhiteSpace($BrowserPath) -or -not (Test-Path -LiteralPath $BrowserPath)) {
    throw 'Install Chrome/Edge or supply -BrowserPath to run this isolated browser harness.'
}

$artifactDirectory = Join-Path $repositoryRoot 'artifacts/issue94'
New-Item -ItemType Directory -Force -Path $artifactDirectory | Out-Null
$runId = [Guid]::NewGuid().ToString('N')
$profilePath = Join-Path $artifactDirectory "chrome-saved-$runId"
$outputPath = Join-Path $artifactDirectory "js-tests-$($Viewport.ToLowerInvariant()).html"
$errorPath = Join-Path $artifactDirectory "js-tests-$($Viewport.ToLowerInvariant()).log"
$harnessUri = [Uri]::new((Join-Path $PSScriptRoot 'recipe-saved.browser.html')).AbsoluteUri
if ($Viewport -eq 'Mobile') { $harnessUri += '?viewport=390' }
$windowSize = if ($Viewport -eq 'Mobile') { '390,844' } else { '1440,1000' }
$browserArguments = @(
    '--headless', '--disable-gpu', '--no-sandbox', '--disable-software-rasterizer',
    '--no-first-run', '--no-default-browser-check', '--disable-background-networking',
    '--allow-file-access-from-files', '--virtual-time-budget=15000',
    "--window-size=$windowSize", "--user-data-dir=`"$profilePath`"", '--dump-dom', "`"$harnessUri`""
)
Start-Process -FilePath $BrowserPath -ArgumentList $browserArguments -WindowStyle Hidden -Wait -PassThru `
    -RedirectStandardOutput $outputPath -RedirectStandardError $errorPath | Out-Null
$document = Get-Content -LiteralPath $outputPath -Encoding UTF8 -Raw
$result = [regex]::Match($document, '<pre id="results">([\s\S]*?)</pre>').Groups[1].Value
Write-Output $result
if ($document -notmatch '<body data-test-status="passed">') {
    throw "Saved Recipes browser tests failed or did not complete. See $outputPath and $errorPath."
}
