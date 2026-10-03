param([string]$OutputDirectory=(Join-Path $PSScriptRoot 'artifacts\550W-V4.6-Beta-Portable'))
$ErrorActionPreference='Stop'
function InvokeBuildDotnet([string[]]$Arguments){ & dotnet.exe @Arguments; if($LASTEXITCODE -ne 0){throw "dotnet failed: $($Arguments[0])"} }
Push-Location $PSScriptRoot
try {
    InvokeBuildDotnet -Arguments @('restore','550W-AI-Controller.sln','-r','win-x64','/p:PublishReadyToRun=true')
    InvokeBuildDotnet -Arguments @('test','tests/550W.Tests/550W.Tests.csproj','-c','Release','--no-restore')
    $destination=[System.IO.Path]::GetFullPath($OutputDirectory)
    foreach($project in @('src/550W.Controller/550W.Controller.csproj','src/550W.Animation/550W.Animation.csproj')){
        InvokeBuildDotnet -Arguments @('publish',$project,'-c','Release','-r','win-x64','--self-contained','true','--no-restore','-o',$destination,'/p:PublishReadyToRun=true','/p:DebugType=None','/p:DebugSymbols=false')
    }
    & (Join-Path $PSScriptRoot 'scripts/build-launcher.ps1') -OutputDirectory $destination
    & (Join-Path $PSScriptRoot 'scripts/build-voice-worker.ps1') -OutputDirectory $destination
    foreach($file in @('README.md','AUDIT.md','TEST-REPORT.md','CHANGELOG.md','LICENSE','THIRD-PARTY-NOTICES.md')){if(Test-Path -LiteralPath $file){Copy-Item -LiteralPath $file -Destination $destination -Force}}
    foreach($folder in @('licenses','docs')){if(Test-Path -LiteralPath $folder){Copy-Item -LiteralPath $folder -Destination $destination -Recurse -Force}}
    Write-Output "550W V4.6 Beta portable: $destination"
} finally {Pop-Location}
