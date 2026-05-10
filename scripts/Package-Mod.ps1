param(
    [string]$Configuration = "Release",
    [string]$OutputDirectory = "dist",
    [string]$PackageFolderName = "ResearchTree-main",
    [string]$LocalDeployPath = "D:\SteamLibrary\steamapps\common\RimWorld\Mods\ResearchTree-main",
    [switch]$SkipBuild,
    [switch]$SkipLocalDeploy,
    [switch]$NoTimestamp,
    [switch]$KeepStage
)

$ErrorActionPreference = "Stop"

function Resolve-RepoRoot {
    $scriptDir = Split-Path -Parent $PSCommandPath
    return (Resolve-Path -LiteralPath (Join-Path $scriptDir "..")).Path
}

function Copy-PackageItem {
    param(
        [string]$SourceRelativePath,
        [string]$DestinationName
    )

    $source = Join-Path $RepoRoot $SourceRelativePath
    if (-not (Test-Path -LiteralPath $source)) {
        throw "Required package item is missing: $SourceRelativePath"
    }

    $destination = Join-Path $StageMod $DestinationName
    Copy-Item -LiteralPath $source -Destination $destination -Recurse -Force
}

function Get-ProjectVersion {
    $projectPath = Join-Path $RepoRoot "Source\ResearchTree\FluffyResearchTree.csproj"
    if (-not (Test-Path -LiteralPath $projectPath)) {
        return "dev"
    }

    [xml]$project = Get-Content -LiteralPath $projectPath -Raw
    $version = $project.Project.PropertyGroup |
        ForEach-Object { $_.Version, $_.FileVersion } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Select-Object -First 1

    if ([string]::IsNullOrWhiteSpace($version)) {
        return "dev"
    }

    return ($version -replace '[^\w\.-]', '_')
}

function Assert-XmlLoads {
    $xmlRoots = @("About", "Languages", "1.6")
    $xmlFiles = @()
    foreach ($root in $xmlRoots) {
        $path = Join-Path $RepoRoot $root
        if (Test-Path -LiteralPath $path) {
            $xmlFiles += Get-ChildItem -LiteralPath $path -Recurse -Filter "*.xml"
        }
    }

    $loadFolders = Join-Path $RepoRoot "LoadFolders.xml"
    if (Test-Path -LiteralPath $loadFolders) {
        $xmlFiles += Get-Item -LiteralPath $loadFolders
    }

    foreach ($file in $xmlFiles) {
        try {
            $document = New-Object System.Xml.XmlDocument
            $document.Load($file.FullName)
        }
        catch {
            throw "XML parse failed: $($file.FullName) - $($_.Exception.Message)"
        }
    }

    Write-Host "XML checked: $($xmlFiles.Count)"
}

function Assert-AboutMetadata {
    $aboutPath = Join-Path $RepoRoot "About\About.xml"
    if (-not (Test-Path -LiteralPath $aboutPath)) {
        throw "Required About.xml is missing: $aboutPath"
    }

    $about = New-Object System.Xml.XmlDocument
    $about.Load($aboutPath)
    if ([string]::IsNullOrWhiteSpace($about.ModMetaData.packageId)) {
        throw "About.xml is missing packageId."
    }

    if ($about.ModMetaData.modClass -ne $null) {
        throw "About.xml contains unsupported RimWorld metadata field: modClass"
    }
}

function Assert-WorkshopAssets {
    $required = @(
        "About\About.xml",
        "About\Preview.png",
        "About\ModIcon.png"
    )

    foreach ($path in $required) {
        $fullPath = Join-Path $RepoRoot $path
        if (-not (Test-Path -LiteralPath $fullPath)) {
            throw "Required workshop asset is missing: $fullPath"
        }
    }
}

function Assert-ZipIsClean {
    param([string]$ZipPath)

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::OpenRead($ZipPath)
    try {
        $entryNames = $zip.Entries | ForEach-Object { $_.FullName -replace '\\', '/' }
        $badEntries = $entryNames | Where-Object {
            $_ -match '(^|/)Source/' -or
            $_ -match '(^|/)Docs/' -or
            $_ -match '(^|/)\.git/' -or
            $_ -match '(^|/)\.omx/' -or
            $_ -match '(^|/)\.package_stage/' -or
            $_ -match '(^|/)dist/' -or
            $_ -match '(^|/)scripts/'
        }

        if ($badEntries.Count -gt 0) {
            $names = $badEntries -join [Environment]::NewLine
            throw "Package contains excluded entries:$([Environment]::NewLine)$names"
        }

        $requiredEntries = @(
            "$PackageFolderName/About/About.xml",
            "$PackageFolderName/About/Preview.png",
            "$PackageFolderName/About/ModIcon.png",
            "$PackageFolderName/LoadFolders.xml",
            "$PackageFolderName/1.6/Assemblies/FluffyResearchTree.dll",
            "$PackageFolderName/1.6/Patches/ResearchButton.xml",
            "$PackageFolderName/Assets/AssetBundles/Mlie_ResearchTree",
            "$PackageFolderName/Languages/English/Keyed/KeyedTranslations.xml"
        )

        foreach ($entry in $requiredEntries) {
            if ($entryNames -notcontains $entry) {
                throw "Package is missing required entry: $entry"
            }
        }

        Write-Host "Zip entries checked: $($zip.Entries.Count)"
        Write-Host "Excluded entries found: 0"
    }
    finally {
        $zip.Dispose()
    }
}

function Assert-PackageFolderNameIsSafe {
    param([string]$FolderName)

    if ([string]::IsNullOrWhiteSpace($FolderName)) {
        throw "Package folder name cannot be empty."
    }

    if ($FolderName -match '[\\/]') {
        throw "Package folder name must be a single folder name: $FolderName"
    }

    if ($FolderName -eq "." -or $FolderName -eq "..") {
        throw "Package folder name is not safe: $FolderName"
    }
}

function Install-LocalMod {
    param(
        [string]$TargetPath,
        [string]$SourceDirectory
    )

    if (-not (Test-Path -LiteralPath $SourceDirectory)) {
        throw "Staged mod directory is missing: $SourceDirectory"
    }

    $targetFolderName = Split-Path -Leaf $TargetPath
    Assert-PackageFolderNameIsSafe -FolderName $targetFolderName

    $modsDirectory = Split-Path -Parent $TargetPath
    if (-not (Test-Path -LiteralPath $modsDirectory)) {
        New-Item -ItemType Directory -Path $modsDirectory -Force | Out-Null
    }

    $resolvedModsDirectory = (Resolve-Path -LiteralPath $modsDirectory).Path
    $modsRoot = [System.IO.Path]::GetFullPath($resolvedModsDirectory.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar)
    $targetFullPath = [System.IO.Path]::GetFullPath((Join-Path $resolvedModsDirectory $targetFolderName))

    if (-not $targetFullPath.StartsWith($modsRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to deploy outside RimWorld Mods directory: $targetFullPath"
    }

    if ((Split-Path -Leaf $targetFullPath) -ne $targetFolderName) {
        throw "Refusing to deploy to unexpected target folder: $targetFullPath"
    }

    if (Test-Path -LiteralPath $targetFullPath) {
        Remove-Item -LiteralPath $targetFullPath -Recurse -Force
    }

    Copy-Item -LiteralPath $SourceDirectory -Destination $resolvedModsDirectory -Recurse -Force

    $requiredDll = Join-Path $targetFullPath "1.6\Assemblies\FluffyResearchTree.dll"
    if (-not (Test-Path -LiteralPath $requiredDll)) {
        throw "Local deploy did not produce the expected DLL: $requiredDll"
    }

    return $targetFullPath
}

$RepoRoot = Resolve-RepoRoot
$ProjectPath = Join-Path $RepoRoot "Source\ResearchTree\FluffyResearchTree.csproj"
$OutputRoot = Join-Path $RepoRoot $OutputDirectory
$StageRoot = Join-Path $RepoRoot ".package_stage"
$StageMod = Join-Path $StageRoot $PackageFolderName
$Version = Get-ProjectVersion
$Stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$ZipBaseName = if ($NoTimestamp) { "${PackageFolderName}_${Version}" } else { "${PackageFolderName}_${Version}_${Stamp}" }
$ZipPath = Join-Path $OutputRoot "$ZipBaseName.zip"

if (-not $SkipBuild) {
    Write-Host "Building $ProjectPath ($Configuration)..."
    dotnet build $ProjectPath -c $Configuration
}

$dllPath = Join-Path $RepoRoot "1.6\Assemblies\FluffyResearchTree.dll"
if (-not (Test-Path -LiteralPath $dllPath)) {
    throw "Release DLL is missing: $dllPath"
}

Assert-XmlLoads
Assert-AboutMetadata
Assert-WorkshopAssets

if (Test-Path -LiteralPath $StageRoot) {
    $resolvedStage = (Resolve-Path -LiteralPath $StageRoot).Path
    if (-not $resolvedStage.StartsWith($RepoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove unexpected staging path: $resolvedStage"
    }

    Remove-Item -LiteralPath $StageRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $StageMod -Force | Out-Null
New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null

Copy-PackageItem -SourceRelativePath "About" -DestinationName "About"
Copy-PackageItem -SourceRelativePath "LoadFolders.xml" -DestinationName "LoadFolders.xml"
Copy-PackageItem -SourceRelativePath "1.6" -DestinationName "1.6"
Copy-PackageItem -SourceRelativePath "Assets" -DestinationName "Assets"
Copy-PackageItem -SourceRelativePath "Languages" -DestinationName "Languages"

if (Test-Path -LiteralPath $ZipPath) {
    Remove-Item -LiteralPath $ZipPath -Force
}

Compress-Archive -LiteralPath $StageMod -DestinationPath $ZipPath -CompressionLevel Optimal
Assert-ZipIsClean -ZipPath $ZipPath

$installedPath = $null
if (-not $SkipLocalDeploy) {
    $installedPath = Install-LocalMod -TargetPath $LocalDeployPath -SourceDirectory $StageMod
}

if (-not $KeepStage) {
    Remove-Item -LiteralPath $StageRoot -Recurse -Force
}

$zipItem = Get-Item -LiteralPath $ZipPath
Write-Host "Package created:"
Write-Host $zipItem.FullName
Write-Host "Size: $($zipItem.Length) bytes"

if ($installedPath -ne $null) {
    Write-Host "Local mod replaced:"
    Write-Host $installedPath
}
else {
    Write-Host "Local mod deploy skipped."
}
