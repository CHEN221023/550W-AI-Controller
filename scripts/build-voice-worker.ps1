param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$sourceRoot=Split-Path -Parent $PSScriptRoot
$voiceDestination=Join-Path $OutputDirectory 'VoiceEngines\espeak-ng'
New-Item -ItemType Directory -Path $voiceDestination -Force | Out-Null
Copy-Item -Path (Join-Path $sourceRoot 'VoiceEngines\espeak-ng\*') -Destination $voiceDestination -Recurse -Force
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:winexe /platform:x64 /optimize+ "/out:$voiceDestination\550W.VoiceWorker.exe" (Join-Path $sourceRoot 'src\550W.VoiceWorker\VoiceWorker.cs')
if($LASTEXITCODE -ne 0){throw 'Voice worker build failed'}
Copy-Item -LiteralPath (Join-Path $sourceRoot 'src\550W.VoiceWorker\VoiceWorker.cs') -Destination $voiceDestination -Force
Copy-Item -LiteralPath $PSCommandPath -Destination $voiceDestination -Force
