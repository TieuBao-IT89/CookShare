param(
    [ValidateSet('Desktop', 'Mobile')][string]$Viewport = 'Desktop',
    [string]$BrowserPath = ''
)
$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$artifactDirectory = Join-Path $repositoryRoot 'artifacts/issue120'
New-Item -ItemType Directory -Force -Path $artifactDirectory | Out-Null
if (-not $BrowserPath) {
    $BrowserPath = @('C:\Program Files\Google\Chrome\Application\chrome.exe', 'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe') |
        Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (-not $BrowserPath) { throw 'Chrome/Edge is required for this browser harness.' }
$listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
$listener.Start()
$port = $listener.LocalEndpoint.Port
$listener.Stop()
$server = Start-Process -FilePath (Get-Command python).Source -ArgumentList @('-m', 'http.server', $port, '--bind', '127.0.0.1', '--directory', "`"$repositoryRoot`"") -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $artifactDirectory "server-$Viewport.log") -RedirectStandardError (Join-Path $artifactDirectory "server-$Viewport-error.log")
try {
    $testUrl = "http://127.0.0.1:$port/CookShare.Tests/Frontend/ui-refinement.browser.html"
    $ready = $false
    for ($attempt = 0; $attempt -lt 20; $attempt++) {
        try { Invoke-WebRequest -UseBasicParsing -Uri $testUrl -TimeoutSec 1 | Out-Null; $ready = $true; break } catch { Start-Sleep -Milliseconds 100 }
    }
    if (-not $ready) { throw 'The isolated browser test server did not start.' }
    if ($Viewport -eq 'Mobile') {
        $wrapper = @"
<!doctype html><html><body><iframe id="mobile" src="$testUrl" style="width:360px;height:844px;border:0"></iframe><pre id="results">RUNNING</pre><script>addEventListener('message',event=>{if(event.source===document.getElementById('mobile').contentWindow&&event.data.type==='ui-refinement-test-result'){document.body.dataset.testStatus=event.data.status;document.getElementById('results').textContent=event.data.output;}});</script></body></html>
"@
        [IO.File]::WriteAllText((Join-Path $artifactDirectory 'mobile-wrapper.html'), $wrapper, [Text.Encoding]::UTF8)
        $testUrl = "http://127.0.0.1:$port/artifacts/issue120/mobile-wrapper.html"
    }
    $profile = Join-Path $artifactDirectory ('browser-' + [Guid]::NewGuid().ToString('N'))
    $outputPath = Join-Path $artifactDirectory "browser-$Viewport.html"
    $errorPath = Join-Path $artifactDirectory "browser-$Viewport.log"
    $browserArguments = @('--headless', '--disable-gpu', '--no-first-run', '--no-default-browser-check', '--disable-background-networking', '--no-sandbox', '--window-size=1440,1000', '--virtual-time-budget=15000', "--user-data-dir=`"$profile`"", '--dump-dom', $testUrl)
    Start-Process -FilePath $BrowserPath -ArgumentList $browserArguments -WindowStyle Hidden -Wait -PassThru -RedirectStandardOutput $outputPath -RedirectStandardError $errorPath | Out-Null
    $document = Get-Content -LiteralPath $outputPath -Raw -Encoding UTF8
    Write-Output ([Net.WebUtility]::HtmlDecode([regex]::Match($document, '<pre id="results">([\s\S]*?)</pre>').Groups[1].Value))
    if ($document -notmatch 'data-test-status="passed"') { throw "UI refinement browser tests failed. See $outputPath" }
} finally {
    if (-not $server.HasExited) { Stop-Process -Id $server.Id }
}
