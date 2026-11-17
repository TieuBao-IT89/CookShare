param(
    [ValidateSet('Desktop', 'Mobile')][string]$Viewport = 'Desktop',
    [string]$BrowserPath = ''
)
$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$temporaryDirectory = Join-Path ([IO.Path]::GetTempPath()) ('cookshare-serving-tests-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $temporaryDirectory | Out-Null
$server = $null

try {
    if (-not $BrowserPath) {
        $BrowserPath = @('C:\Program Files\Google\Chrome\Application\chrome.exe', 'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe') |
            Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    }
    if (-not $BrowserPath) { throw 'Chrome/Edge is required for this browser harness.' }

    $python = Get-Command python -ErrorAction SilentlyContinue
    if (-not $python) { throw 'Python is required for the local browser test server.' }

    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    $listener.Start()
    $port = $listener.LocalEndpoint.Port
    $listener.Stop()

    $server = Start-Process -FilePath $python.Source `
        -ArgumentList @('-m', 'http.server', $port, '--bind', '127.0.0.1', '--directory', "`"$repositoryRoot`"") `
        -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $temporaryDirectory 'server.log') `
        -RedirectStandardError (Join-Path $temporaryDirectory 'server-error.log')

    $testUrl = "http://127.0.0.1:$port/CookShare.Tests/Frontend/recipe-serving-scaler.browser.html"
    $ready = $false
    for ($attempt = 0; $attempt -lt 20; $attempt++) {
        try {
            Invoke-WebRequest -UseBasicParsing -Uri $testUrl -TimeoutSec 1 | Out-Null
            $ready = $true
            break
        } catch {
            Start-Sleep -Milliseconds 100
        }
    }
    if (-not $ready) { throw 'The isolated browser test server did not start.' }

    $windowSize = if ($Viewport -eq 'Mobile') { '390,844' } else { '1440,1000' }
    $profile = Join-Path $temporaryDirectory 'browser-profile'
    $outputPath = Join-Path $temporaryDirectory 'browser.html'
    $errorPath = Join-Path $temporaryDirectory 'browser-error.log'
    $browserArguments = @(
        '--headless',
        '--disable-gpu',
        '--no-first-run',
        '--no-default-browser-check',
        '--disable-background-networking',
        '--no-sandbox',
        "--window-size=$windowSize",
        '--virtual-time-budget=15000',
        "--user-data-dir=`"$profile`"",
        '--dump-dom',
        $testUrl
    )
    Start-Process -FilePath $BrowserPath -ArgumentList $browserArguments `
        -WindowStyle Hidden -Wait -PassThru `
        -RedirectStandardOutput $outputPath -RedirectStandardError $errorPath | Out-Null

    $document = Get-Content -LiteralPath $outputPath -Raw -Encoding UTF8
    $result = [regex]::Match($document, '<pre id="results">([\s\S]*?)</pre>')
    if (-not $result.Success) { throw "Browser test output was not found. See $errorPath" }

    $output = [Net.WebUtility]::HtmlDecode($result.Groups[1].Value)
    Write-Output $output
    if ($document -notmatch 'data-test-status="passed"') {
        throw "Recipe serving scaler browser tests failed. See $errorPath"
    }
} finally {
    if ($server -and -not $server.HasExited) {
        Stop-Process -Id $server.Id
    }
    if (Test-Path -LiteralPath $temporaryDirectory) {
        Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force
    }
}
