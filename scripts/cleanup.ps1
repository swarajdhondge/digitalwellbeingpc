# Clean build artifacts
dotnet clean
Write-Output "Cleaned dotnet."
Get-ChildItem -Path . -Include bin, obj -Recurse -Directory | Remove-Item -Recurse -Force
Write-Output "Cleaned all bin and obj folders."

