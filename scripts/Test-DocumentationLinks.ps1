[CmdletBinding()]
param(
    [string]$RootPath = ''
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($RootPath)) {
    $RootPath = Split-Path -Parent $PSScriptRoot
}

$repositoryRoot = [System.IO.Path]::GetFullPath($RootPath)
$repositoryPrefix = $repositoryRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
$gitSafeDirectory = $repositoryRoot.Replace('\', '/')
$markdownFiles = @(& git -c "safe.directory=$gitSafeDirectory" -C $repositoryRoot ls-files -- '*.md')
if ($LASTEXITCODE -ne 0) {
    throw "git ls-files failed with exit code $LASTEXITCODE."
}

$errors = [System.Collections.Generic.List[string]]::new()
$linkPattern = [regex]'!?\[[^\]]*\]\((?<target>(?:\\.|[^)])*)\)'
$referencePattern = [regex]'(?m)^\s*\[[^\]]+\]:\s*(?<target>\S+)'

foreach ($relativeFile in $markdownFiles) {
    $documentPath = Join-Path $repositoryRoot $relativeFile
    $content = [System.IO.File]::ReadAllText($documentPath)
    $content = [regex]::Replace($content, '(?ms)^\s*```.*?^\s*```\s*$', '')
    $content = [regex]::Replace($content, '`[^`\r\n]*`', '')
    $matches = @($linkPattern.Matches($content)) + @($referencePattern.Matches($content))

    foreach ($match in $matches) {
        $target = $match.Groups['target'].Value.Trim()
        if ($target.StartsWith('<') -and $target.EndsWith('>')) {
            $target = $target[1..($target.Length - 2)] -join ''
        }
        elseif ($target.Contains(' ')) {
            $target = $target.Split(' ', 2)[0]
        }

        if ([string]::IsNullOrWhiteSpace($target) -or
            $target.StartsWith('#') -or
            $target.StartsWith('//') -or
            $target -match '^[a-zA-Z][a-zA-Z0-9+.-]*:') {
            continue
        }

        $pathPart = [System.Uri]::UnescapeDataString(($target -split '#', 2)[0]).Replace('/', [System.IO.Path]::DirectorySeparatorChar)
        if ([string]::IsNullOrWhiteSpace($pathPart)) {
            continue
        }

        $resolvedPath = [System.IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $documentPath) $pathPart))
        if ($resolvedPath -ne $repositoryRoot -and -not $resolvedPath.StartsWith($repositoryPrefix, [StringComparison]::OrdinalIgnoreCase)) {
            $errors.Add("$relativeFile -> $target (repository外を参照しています)")
            continue
        }

        if (-not (Test-Path -LiteralPath $resolvedPath)) {
            $errors.Add("$relativeFile -> $target (参照先がありません)")
        }
    }
}

if ($errors.Count -gt 0) {
    $errors | ForEach-Object { [Console]::Error.WriteLine($_) }
    throw "Markdown link validation failed with $($errors.Count) error(s)."
}

Write-Output "Markdown link validation passed: $($markdownFiles.Count) tracked document(s)."
