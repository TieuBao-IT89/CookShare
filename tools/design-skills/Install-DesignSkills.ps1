param([switch]$VerifyOnly)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$lock = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'lock.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$gitRoot = & git -C $repositoryRoot rev-parse --show-toplevel 2>$null
if ($LASTEXITCODE -ne 0 -or -not [IO.Path]::GetFullPath($gitRoot).Equals($repositoryRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Run setup from a Git checkout whose root contains tools/design-skills. Installation into a parent repository is refused.'
}
$npm = if ($env:OS -eq 'Windows_NT') { 'npm.cmd' } else { 'npm' }
$nodeVersion = & node --version
if ($LASTEXITCODE -ne 0 -or [int]($nodeVersion.TrimStart('v').Split('.')[0]) -lt 22) {
    throw 'Node.js 22+ is required. Install it before setting up the design skills.'
}

function Invoke-Checked([string]$Executable, [string[]]$Arguments) {
    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Executable failed (exit $LASTEXITCODE). No existing configuration will be replaced." }
}

function Copy-License([string]$Name, [string]$Destination) {
    if (-not (Test-Path -LiteralPath $Destination)) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('licenses/' + $Name)) -Destination $Destination
    }
}

Push-Location $repositoryRoot
try {
    if (-not $VerifyOnly) {
        $cache = Join-Path $repositoryRoot 'artifacts/design-skills'
        New-Item -ItemType Directory -Force -Path $cache | Out-Null
        $tasteDestination = Join-Path $repositoryRoot '.cursor/skills/design-taste-frontend'
        if (-not (Test-Path -LiteralPath $tasteDestination)) {
            $checkout = Join-Path $cache ('taste-' + [Guid]::NewGuid().ToString('N'))
            Invoke-Checked 'git' @('clone', '--filter=blob:none', '--sparse', '--no-checkout', '--depth', '1', $lock.taste.repository, $checkout)
            Invoke-Checked 'git' @('-C', $checkout, 'fetch', '--depth', '1', 'origin', $lock.taste.commit)
            Invoke-Checked 'git' @('-C', $checkout, 'sparse-checkout', 'set', 'skills/taste-skill')
            Invoke-Checked 'git' @('-C', $checkout, 'checkout', '--detach', 'FETCH_HEAD')
            New-Item -ItemType Directory -Force -Path (Split-Path $tasteDestination) | Out-Null
            Copy-Item -LiteralPath (Join-Path $checkout 'skills/taste-skill') -Destination $tasteDestination -Recurse
        }

        $impeccableDestination = Join-Path $repositoryRoot '.cursor/skills/impeccable'
        if (-not (Test-Path -LiteralPath $impeccableDestination)) {
            if (Get-ChildItem -Path '.cursor/agents/impeccable-*.md' -ErrorAction SilentlyContinue) {
                throw 'Existing Impeccable agent files found. Review this partial installation manually; no files were overwritten.'
            }
            $bundle = Join-Path $cache ('impeccable-' + $lock.impeccable.version + '.zip')
            if (-not (Test-Path -LiteralPath $bundle)) {
                Invoke-WebRequest -UseBasicParsing -Uri $lock.impeccable.bundleUrl -OutFile $bundle -TimeoutSec 90
            }
            if ((Get-FileHash -LiteralPath $bundle -Algorithm SHA256).Hash -ne $lock.impeccable.bundleSha256) {
                throw 'Impeccable bundle checksum differs from the reviewed version. Installation stopped.'
            }
            $priorBundle = $env:IMPECCABLE_BUNDLE_PATH
            $priorCache = $env:IMPECCABLE_HOME
            try {
                $env:IMPECCABLE_BUNDLE_PATH = $bundle
                $env:IMPECCABLE_HOME = Join-Path $cache 'engine-cache'
                Invoke-Checked $npm @('exec', '--yes', '--ignore-scripts', '--cache', (Join-Path $cache 'npm-cache'), '--package', ('impeccable@' + $lock.impeccable.version), '--', 'impeccable', 'install', '--providers=cursor', '--scope=project', '--no-hooks', '--yes')
            } finally {
                $env:IMPECCABLE_BUNDLE_PATH = $priorBundle
                $env:IMPECCABLE_HOME = $priorCache
            }
        }
        Copy-License 'Taste-MIT.txt' (Join-Path $tasteDestination 'LICENSE')
        Copy-License 'Impeccable-Apache-2.0.txt' (Join-Path $impeccableDestination 'LICENSE')
        Copy-License 'Impeccable-NOTICE.md' (Join-Path $impeccableDestination 'NOTICE.md')
    }

    foreach ($entry in $lock.files) {
        $path = Join-Path $repositoryRoot $entry.path
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing design-skill file: $($entry.path). Run setup without -VerifyOnly." }
        # Normalize text line endings only; local Git settings must not invalidate reviewed content.
        $content = [IO.File]::ReadAllText($path).Replace("`r`n", "`n")
        $algorithm = [Security.Cryptography.SHA256]::Create()
        try { $hash = [BitConverter]::ToString($algorithm.ComputeHash([Text.Encoding]::UTF8.GetBytes($content))).Replace('-', '').ToLowerInvariant() } finally { $algorithm.Dispose() }
        if ($hash -ne $entry.sha256) { throw "Design-skill content differs: $($entry.path). Review changes instead of overwriting local files." }
    }
    $hooks = Join-Path $repositoryRoot '.cursor/hooks.json'
    if ((Test-Path -LiteralPath $hooks) -and (Get-Content -LiteralPath $hooks -Raw) -match '(?i)impeccable') {
        throw 'An Impeccable hook is configured. This project setup requires hooks disabled; review that existing configuration explicitly.'
    }
    Write-Output ("Verified Node {0}, Taste {1}, Impeccable {2}, {3} reviewed text files; Impeccable hooks disabled." -f $nodeVersion, $lock.taste.commit.Substring(0, 7), $lock.impeccable.version, $lock.files.Count)
} finally {
    Pop-Location
}
