$unityPath = "C:\Program Files\Unity\Hub\Editor\2022.3.43f1\Editor\Unity.exe"
if (-not (Test-Path $unityPath)) {
    $unityPath = (Get-ChildItem -Path "C:\Program Files\Unity\Hub\Editor" -Filter "Unity.exe" -Recurse | Select-Object -First 1).FullName
}
if ($unityPath) {
    Start-Process -Wait -FilePath $unityPath -ArgumentList "-quit", "-batchmode", "-projectPath", "`"e:\Programs\Unity\Projects\AstroRiftGames\ProjectGrimhold\Project Grimhold`"", "-executeMethod", "ValidateCatalogs.RunValidation", "-logFile", "unity_validation_log.txt"
} else {
    Write-Output "Unity not found."
}
