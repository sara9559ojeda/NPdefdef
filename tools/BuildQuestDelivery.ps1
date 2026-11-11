param(
    [string]$EditorPath = 'C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe',
    [switch]$CreateSigningKey
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$signingDirectory = Join-Path $projectRoot 'Builds\Signing'
New-Item -ItemType Directory -Force -Path $signingDirectory | Out-Null
$owner = [Security.Principal.WindowsIdentity]::GetCurrent().User
$aclTool = Join-Path ([Environment]::GetEnvironmentVariable('SystemRoot')) 'System32\icacls.exe'
& $aclTool $signingDirectory '/inheritance:r' '/grant:r' ('*' + $owner.Value + ':(OI)(CI)F') '/q' | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Cannot restrict access to the private signing directory' }
$keystorePath = Join-Path $signingDirectory 'neurophobia-delivery.keystore'
$passwordPath = Join-Path $signingDirectory 'delivery-password.dpapi.xml'
$configurationPath = Join-Path $signingDirectory 'build-signing.json'
$androidRoot = Join-Path (Split-Path -Parent $EditorPath) 'Data\PlaybackEngines\AndroidPlayer'

function New-IsolatedProcess([string]$Executable) {
    $processInfo = [Diagnostics.ProcessStartInfo]::new()
    $processInfo.FileName = $Executable
    $processInfo.WorkingDirectory = $projectRoot
    $processInfo.UseShellExecute = $false
    $processInfo.CreateNoWindow = $true
    $processInfo.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    $processInfo.Environment.Clear()
    foreach ($name in @('SystemRoot', 'WINDIR', 'COMSPEC', 'Path', 'PATHEXT', 'TEMP', 'TMP', 'USERPROFILE', 'LOCALAPPDATA', 'APPDATA', 'PROGRAMDATA', 'ProgramFiles', 'ProgramFiles(x86)', 'ProgramW6432', 'USERNAME', 'USERDOMAIN', 'HOMEDRIVE', 'HOMEPATH', 'NUMBER_OF_PROCESSORS')) {
        $value = [Environment]::GetEnvironmentVariable($name)
        if ($null -ne $value) { $processInfo.Environment[$name] = $value }
    }
    return $processInfo
}

if ($CreateSigningKey) {
    if ((Test-Path -LiteralPath $keystorePath) -or (Test-Path -LiteralPath $passwordPath)) {
        throw 'Signing material already exists. Reuse it without CreateSigningKey.'
    }
    $password = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(36))
    ConvertTo-SecureString -String $password -AsPlainText -Force | Export-Clixml -LiteralPath $passwordPath
    $keytool = New-IsolatedProcess (Join-Path $androidRoot 'OpenJDK\bin\keytool.exe')
    $keytool.Environment['NP_PRIVATE_SIGNING_PASSWORD'] = $password
    foreach ($argument in @('-genkeypair', '-keystore', $keystorePath, '-storetype', 'JKS', '-alias', 'neurophobia-delivery', '-keyalg', 'RSA', '-keysize', '3072', '-validity', '3650', '-dname', 'CN=Sara Maria Ojeda Lopez, OU=Academic Demo, O=NEUROPHOBIA, C=CO', '-storepass:env', 'NP_PRIVATE_SIGNING_PASSWORD', '-keypass:env', 'NP_PRIVATE_SIGNING_PASSWORD', '-noprompt')) {
        $keytool.ArgumentList.Add($argument)
    }
    $keyProcess = [Diagnostics.Process]::Start($keytool)
    $keyProcess.WaitForExit()
    $keytool.Environment.Clear()
    if ($keyProcess.ExitCode -ne 0) { throw 'Private signing key generation failed' }
    $password = $null
}

if (!(Test-Path -LiteralPath $keystorePath) -or !(Test-Path -LiteralPath $passwordPath)) {
    throw 'Private signing material is missing. Run with CreateSigningKey once.'
}
$protectedPassword = Import-Clixml -LiteralPath $passwordPath
$password = [Net.NetworkCredential]::new('', $protectedPassword).Password
@{
    keystorePath = $keystorePath
    keystorePassword = $password
    aliasName = 'neurophobia-delivery'
    aliasPassword = $password
} | ConvertTo-Json -Compress | Set-Content -LiteralPath $configurationPath -Encoding utf8NoBOM
$password = $null
$unity = New-IsolatedProcess $EditorPath
$unity.Environment['NP_ANDROID_SIGNING_CONFIG'] = $configurationPath
foreach ($argument in @('-batchmode', '-quit', '-projectPath', $projectRoot, '-executeMethod', 'BuildQuest3.Build', '-logFile', (Join-Path $projectRoot 'Logs\delivery-build-isolated.log'))) {
    $unity.ArgumentList.Add($argument)
}
$buildProcess = [Diagnostics.Process]::Start($unity)
try {
    Write-Output ('Unity build started with isolated environment. PID: ' + $buildProcess.Id)
    $buildProcess.WaitForExit()
    if ($buildProcess.ExitCode -ne 0) { throw 'Unity build failed. Inspect the isolated build log without printing environment blocks.' }
    Write-Output 'Quest delivery build completed.'
} finally {
    if (Test-Path -LiteralPath $configurationPath) { Remove-Item -LiteralPath $configurationPath }
}
