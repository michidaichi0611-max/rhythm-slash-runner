# Android tools reject non-ASCII project paths (this folder lives under "デスクトップ\ランニングゲーム").
# Mirrors the Unity project to an ASCII path, builds the APK there, and copies it back to Builds/Android.
# Round 36: -Store builds the Google Play upload (.aab, release-signed with the key from tools/make_upload_key.ps1);
# it asks for the key password (never stored) and keeps the build number (versionCode) in step with the project.
param(
  [string]$Unity = "C:\Program Files\Unity\Hub\Editor\6000.3.22f1\Editor\Unity.exe",
  [string]$Work = "$env:LOCALAPPDATA\RhythmSlashRunnerBuild",
  [switch]$Store,
  [string]$KeyStore = "$env:USERPROFILE\rsr_keys\upload.keystore",
  [string]$Alias = "upload"
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$src = Join-Path $root "unity_project"
$dst = Join-Path $Work "unity_project"
New-Item -ItemType Directory -Force $dst | Out-Null
foreach ($dir in "Assets", "Packages", "ProjectSettings") {
  robocopy (Join-Path $src $dir) (Join-Path $dst $dir) /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
  if ($LASTEXITCODE -ge 8) { throw "robocopy failed for $dir ($LASTEXITCODE)" }
}
$numberFile = Join-Path $src "build_number.txt"
if (Test-Path $numberFile) { Copy-Item $numberFile (Join-Path $dst "build_number.txt") -Force }

$name = if ($Store) { "RhythmSlashRunner.aab" } else { "RhythmSlashRunner.apk" }
$method = if ($Store) { "BuildAndroidAab" } else { "BuildAndroid" }
if ($Store) {
  if (-not (Test-Path $KeyStore)) { throw "No upload key at $KeyStore. Run tools/make_upload_key.ps1 first." }
  $secure = Read-Host "Upload key password" -AsSecureString
  $plain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure))
  $env:RSR_KEYSTORE = $KeyStore; $env:RSR_KEYSTORE_PASS = $plain; $env:RSR_KEY_ALIAS = $Alias; $env:RSR_KEY_PASS = $plain
}
$log = Join-Path $Work "build_android.log"
$built = Join-Path $Work "Builds\Android\$name"
if (Test-Path $built) { Remove-Item $built -Force } # never report an old build as this one
try {
  & $Unity -batchmode -quit -projectPath $dst -buildTarget Android -executeMethod "RhythmSlashRunner.EditorTools.BuildTools.$method" -logFile $log | Out-Null
} finally {
  Remove-Item Env:RSR_KEYSTORE_PASS, Env:RSR_KEY_PASS -ErrorAction SilentlyContinue
}
if (-not (Test-Path $built)) { Select-String -Path $log -Pattern "error|Exception" | Select-Object -First 20; throw "$name was not produced. See $log" }
if ($Store) { Copy-Item (Join-Path $dst "build_number.txt") $numberFile -Force } # the store build bumped the versionCode
$out = Join-Path $root "Builds\Android"
New-Item -ItemType Directory -Force $out | Out-Null
Copy-Item $built $out -Force
Write-Output ("Built: " + (Join-Path $out $name) + "  " + [math]::Round((Get-Item $built).Length / 1MB, 1) + " MB")
