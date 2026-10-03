param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
& $compiler /nologo /target:winexe /platform:x64 /optimize+ "/win32manifest:$root\src\550W.Controller\app.manifest" "/win32icon:$root\assets\controller.ico" "/out:$OutputDirectory\550W.Launcher.exe" "$root\src\550W.Launcher\Launcher.cs"
if($LASTEXITCODE -ne 0){throw 'Launcher build failed'}
