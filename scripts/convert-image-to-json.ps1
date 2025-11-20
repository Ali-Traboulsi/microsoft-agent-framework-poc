param(
    [Parameter(Mandatory=$true)]
    [string]$ImagePath,
    
    [string]$Message = "Analyze this image"
)

if (-not (Test-Path $ImagePath)) {
    Write-Error "File not found: $ImagePath"
    exit 1
}

$bytes = [System.IO.File]::ReadAllBytes($ImagePath)
$base64 = [Convert]::ToBase64String($bytes)

$ext = [System.IO.Path]::GetExtension($ImagePath).ToLower()
$mediaType = switch ($ext) {
    ".png"  { "image/png" }
    ".jpg"  { "image/jpeg" }
    ".jpeg" { "image/jpeg" }
    ".gif"  { "image/gif" }
    ".webp" { "image/webp" }
    default { "image/png" }
}

$dataUri = "data:$mediaType;base64,$base64"
$fileName = [System.IO.Path]::GetFileName($ImagePath)

$json = @{
    message = $Message
    contents = @(
        @{
            type = "image"
            data = $dataUri
            mediaType = $mediaType
            fileName = $fileName
        }
    )
} | ConvertTo-Json -Depth 5

Write-Output $json
