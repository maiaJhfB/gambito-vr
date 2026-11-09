$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) {
    throw 'Compilador .NET Framework indisponível. Use os testes EditMode no Unity.'
}

$output = Join-Path $projectRoot 'Temp\CoreTests'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$runner = Join-Path $output 'ChessVR.Tests.exe'
$core = Get-ChildItem -LiteralPath (Join-Path $projectRoot 'Assets\ChessVR\Core') -Filter '*.cs'
$tests = Join-Path $projectRoot 'Assets\ChessVR\Tests\EditMode\ChessRulesTests.cs'

& $compiler /nologo /langversion:5 /codepage:65001 /define:CHESSVR_STANDALONE "/out:$runner" $core.FullName $tests
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $runner
exit $LASTEXITCODE
