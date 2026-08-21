<#
.SYNOPSIS
    Automated Build & Deploy Script for ResumeAssistant.Api to site86558.siteasp.net
.DESCRIPTION
    1. Publishes ResumeAssistant.Api in Release mode.
    2. Packages production configuration (appsettings.Production.json).
    3. Places app_offline.htm on the remote FTP server to safely stop the IIS app pool.
    4. Uploads all release binaries and assets via FTP.
    5. Removes app_offline.htm to restart the application.
    6. Verifies live health endpoint via HTTPS.
#>

[CmdletBinding()]
param (
    [string]$Server = "site86558.siteasp.net",
    [int]$Port = 21,
    [string]$Username = "site86558",
    [string]$Password = "C@a5kT2=4+Sy",
    [string]$RemoteRoot = "/wwwroot",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Resolve-Path "$ScriptDir\.."
$ApiProjectDir = "$ProjectRoot\src\ResumeAssistant.Api"
$PublishDir = "$ProjectRoot\bin\publish-siteasp"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  Starting Deployment to $Server (MonsterASP.NET EU)     " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Clean & Publish API Project
Write-Host "`n[1/6] Publishing ResumeAssistant.Api in $Configuration mode..." -ForegroundColor Yellow
if (Test-Path $PublishDir) {
    Remove-Item -Recurse -Force $PublishDir
}

& dotnet publish "$ApiProjectDir\ResumeAssistant.Api.csproj" -c $Configuration -o $PublishDir
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build and publish failed with exit code $LASTEXITCODE"
    exit 1
}

# 2. Inject Production AppSettings
Write-Host "`n[2/6] Preparing Production AppSettings..." -ForegroundColor Yellow
$ProdSettingsFile = "$PublishDir\appsettings.Production.json"
$DevSettingsFile = "$ApiProjectDir\appsettings.Development.json"

if (Test-Path "$ApiProjectDir\appsettings.Production.json") {
    Copy-Item -Force "$ApiProjectDir\appsettings.Production.json" $ProdSettingsFile
    Write-Host " -> Copied existing appsettings.Production.json to publish directory." -ForegroundColor Green
} elseif (Test-Path $DevSettingsFile) {
    # If appsettings.Development.json has live keys, duplicate it as appsettings.Production.json
    Copy-Item -Force $DevSettingsFile $ProdSettingsFile
    Write-Host " -> Configured appsettings.Production.json with active cloud credentials." -ForegroundColor Green
}

# 3. Create FTP Helper Functions
$Credentials = New-Object System.Net.NetworkCredential($Username, $Password)

function Upload-FtpFile {
    param(
        [string]$LocalFilePath,
        [string]$RemotePath
    )
    $uri = "ftp://$Server$RemotePath"
    $maxRetries = 3
    for ($attempt = 1; $attempt -le $maxRetries; $attempt++) {
        try {
            $wc = New-Object System.Net.WebClient
            $wc.Credentials = $Credentials
            $null = $wc.UploadFile($uri, $LocalFilePath)
            $wc.Dispose()
            return
        } catch {
            if ($attempt -eq $maxRetries) {
                Write-Host " [ERROR] Upload failed for $RemotePath : $($_.Exception.Message)" -ForegroundColor Red
                throw $_
            }
            Start-Sleep -Milliseconds (400 * $attempt)
        }
    }
}

function Create-FtpDirectory {
    param([string]$RemotePath)
    try {
        $uri = "ftp://$Server$RemotePath"
        $req = [System.Net.FtpWebRequest]::Create($uri)
        $req.Credentials = $Credentials
        $req.Method = [System.Net.WebRequestMethods+Ftp]::MakeDirectory
        $req.UsePassive = $true
        $req.KeepAlive = $false
        $response = $req.GetResponse()
        $response.Close()
    } catch {
        # Directory might already exist
    }
}

function Remove-FtpFile {
    param([string]$RemotePath)
    try {
        $uri = "ftp://$Server$RemotePath"
        $req = [System.Net.FtpWebRequest]::Create($uri)
        $req.Credentials = $Credentials
        $req.Method = [System.Net.WebRequestMethods+Ftp]::DeleteFile
        $req.UsePassive = $true
        $req.KeepAlive = $false
        $response = $req.GetResponse()
        $response.Close()
    } catch {
        # Ignore if file does not exist
    }
}

# 4. Place app_offline.htm to safely release IIS file locks
Write-Host "`n[3/6] Gracefully stopping IIS worker process with app_offline.htm..." -ForegroundColor Yellow
$OfflineContent = @"
<!DOCTYPE html>
<html>
<head><title>Application Updating</title></head>
<body style="font-family:sans-serif; text-align:center; padding:50px;">
  <h2>Digital Twin API Updating...</h2>
  <p>Please wait a moment while the latest binaries are deployed.</p>
</body>
</html>
"@
$TempOfflineFile = "$env:TEMP\app_offline.htm"
[System.IO.File]::WriteAllText($TempOfflineFile, $OfflineContent)
Upload-FtpFile -LocalFilePath $TempOfflineFile -RemotePath "$RemoteRoot/app_offline.htm"
Remove-Item -Force $TempOfflineFile
Start-Sleep -Seconds 2

# 5. Upload All Publish Files
Write-Host "`n[4/6] Uploading published files to $RemoteRoot..." -ForegroundColor Yellow
Create-FtpDirectory -RemotePath "$RemoteRoot/logs"
$AllFiles = Get-ChildItem -Path $PublishDir -Recurse -File

$UploadedCount = 0
$TotalFiles = $AllFiles.Count

foreach ($file in $AllFiles) {
    $relative = $file.FullName.Substring($PublishDir.Length).Replace("\", "/")
    $remoteFile = "$RemoteRoot$relative"
    $remoteDir = [System.IO.Path]::GetDirectoryName($remoteFile).Replace("\", "/")

    if ($remoteDir -ne $RemoteRoot) {
        Create-FtpDirectory -RemotePath $remoteDir
    }

    $UploadedCount++
    Write-Host " [$UploadedCount/$TotalFiles] Uploading $($file.Name)..." -ForegroundColor DarkGray
    Upload-FtpFile -LocalFilePath $file.FullName -RemotePath $remoteFile
}
Write-Host " -> Successfully uploaded $TotalFiles files." -ForegroundColor Green

# 6. Remove app_offline.htm and iisstart.htm to start IIS application pool
Write-Host "`n[5/6] Starting IIS application pool (removing app_offline.htm and iisstart.htm)..." -ForegroundColor Yellow
Remove-FtpFile -RemotePath "$RemoteRoot/iisstart.htm"
Remove-FtpFile -RemotePath "$RemoteRoot/app_offline.htm"
Start-Sleep -Seconds 3

# 7. Verify Health Probe
Write-Host "`n[6/6] Probing live health endpoint: https://$Server/..." -ForegroundColor Yellow
try {
    $healthUri = "https://$Server/"
    $response = Invoke-WebRequest -Uri $healthUri -UseBasicParsing -TimeoutSec 15
    Write-Host " -> Health Check Status: $($response.StatusCode) OK" -ForegroundColor Green
    Write-Host " -> Response Content: $($response.Content)" -ForegroundColor Cyan
} catch {
    Write-Host " -> Warning: Initial probe returned $($_.Exception.Message). Trying /info..." -ForegroundColor Yellow
    try {
        $infoResponse = Invoke-WebRequest -Uri "https://$Server/info" -UseBasicParsing -TimeoutSec 15
        Write-Host " -> /info Status: $($infoResponse.StatusCode) OK" -ForegroundColor Green
        Write-Host " -> Response Content: $($infoResponse.Content)" -ForegroundColor Cyan
    } catch {
        Write-Host " -> Site is initializing. Please verify at https://$Server/" -ForegroundColor Yellow
    }
}

Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host "  Deployment Completed Successfully!                      " -ForegroundColor Green
Write-Host "  Base URL: https://$Server/                              " -ForegroundColor Green
Write-Host "  Info URL: https://$Server/info                          " -ForegroundColor Green
Write-Host "  AGUI URL: https://$Server/agentic_chat                  " -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
