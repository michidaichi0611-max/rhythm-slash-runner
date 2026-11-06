# Round 36: makes the Google Play "upload key" once. Run it yourself; it asks for a password (typed by you, never stored).
# The key file goes outside the project (not in OneDrive) -- copy it to a USB stick or similar as a backup.
# Lose it and you need Google support to reset it; leak it and others can sign as you.
param(
  [string]$KeyStore = "$env:USERPROFILE\rsr_keys\upload.keystore",
  [string]$Alias = "upload",
  [string]$KeyTool = "C:\Program Files\Unity\Hub\Editor\6000.3.22f1\Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK\bin\keytool.exe"
)
$ErrorActionPreference = "Stop"
if (Test-Path $KeyStore) { throw "A key already exists at $KeyStore (never make a second one for the same app)." }
New-Item -ItemType Directory -Force (Split-Path -Parent $KeyStore) | Out-Null
Write-Output "Making the upload key at $KeyStore (alias '$Alias'). keytool will ask for a password and your name."
& $KeyTool -genkeypair -v -keystore $KeyStore -alias $Alias -keyalg RSA -keysize 2048 -validity 10000 -storetype PKCS12
if (Test-Path $KeyStore) { Write-Output "Done. Back up $KeyStore and remember the password." }
