# Clean the deployment directory first
$deploymentPath = "C:\Github\minimalsqlexport\Deployment\MinimalSqlExport"

# Check if the directory exists, if so, delete it
if (Test-Path $deploymentPath) {
    Remove-Item -Path $deploymentPath -Recurse -Force
    Write-Host "Deployment directory cleaned."
}

# Create the directory again
New-Item -Path $deploymentPath -ItemType Directory -Force | Out-Null
Write-Host "Created fresh deployment directory."

# Now publish the application
Set-Location -Path C:\Github\minimalsqlexport\Source\MinimalSqlExport\MinimalSqlExport.Core
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true --self-contained true -p:IncludeNativeLibrariesForSelfExtract=true -o $deploymentPath

# Create logsettings.json with Information level in the deployment directory
$logSettingsContent = @{
    LogLevel = "Information"
} | ConvertTo-Json -Depth 1

Set-Content -Path "$deploymentPath\logsettings.json" -Value $logSettingsContent
Write-Host "Created logsettings.json with Information log level."

# Continue with language folder cleanup...
$languageFolders = Get-ChildItem -Directory -Path $deploymentPath -Recurse | 
    Where-Object { 
        $_.Name -match '^[a-z]{2}(-[A-Z]{2})?$' -and 
        $_.Name -ne 'en' -and 
        $_.Name -ne 'nl' -and 
        $_.Name -ne 'en-US' -and 
        $_.Name -ne 'nl-NL'
    }

# Remove the folders without confirmation for automation
$languageFolders | Remove-Item -Recurse -Force
Write-Host "Language folders removed successfully."

# Return to original directory
Set-Location -Path C:\Github\minimalsqlexport\Source\MinimalSqlExport\MinimalSqlExport.Core