<#
.SYNOPSIS
    编译 SimpleTodo，生成单文件 EXE。

.DESCRIPTION
    只依赖 Windows 自带的 .NET Framework 4.x 编译器（csc.exe）：
    无需 Visual Studio、无需 .NET SDK、无需联网、无需任何 NuGet 包。

.PARAMETER Configuration
    Release（默认，开启优化）或 Debug（带调试信息）。

.PARAMETER OutputDirectory
    输出目录，默认 dist。

.PARAMETER CscPath
    手动指定 csc.exe 路径（默认自动查找 Framework64/Framework 目录）。

.PARAMETER SkipSelfTest
    跳过编译后的内置自检。

.EXAMPLE
    pwsh -File build.ps1
    pwsh -File build.ps1 -Configuration Debug -OutputDirectory out
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$OutputDirectory = 'dist',

    [string]$CscPath,

    [switch]$SkipSelfTest
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
if (-not $root) { $root = (Get-Location).Path }

function Find-Csc {
    param([string]$Explicit)

    if ($Explicit) {
        if (Test-Path -LiteralPath $Explicit) { return (Resolve-Path -LiteralPath $Explicit).Path }
        throw "指定的 csc.exe 不存在：$Explicit"
    }

    $candidates = @(
        (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'),
        (Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe')
    )

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) { return $candidate }
    }

    throw @"
未找到 C# 编译器 csc.exe。
请安装 .NET Framework 4.8（Windows 10/11 一般已内置），或用 -CscPath 指定路径。
"@
}

$csc = Find-Csc -Explicit $CscPath
$frameworkDirectory = Split-Path -Parent $csc

$sourceRoot = Join-Path $root 'src'
if (-not (Test-Path -LiteralPath $sourceRoot)) { throw "找不到源码目录：$sourceRoot" }

$sources = @(Get-ChildItem -LiteralPath $sourceRoot -Recurse -Filter '*.cs' -File |
    Sort-Object FullName |
    Select-Object -ExpandProperty FullName)

if ($sources.Count -eq 0) { throw "在 $sourceRoot 下没有找到任何 .cs 源文件" }

$outputDirectoryPath = Join-Path $root $OutputDirectory
if (-not (Test-Path -LiteralPath $outputDirectoryPath)) {
    New-Item -ItemType Directory -Path $outputDirectoryPath | Out-Null
}

$outputPath = Join-Path $outputDirectoryPath 'SimpleTodo.exe'

# 只引用 .NET Framework 自带程序集，不引入任何第三方依赖
$referenceNames = @('System.dll', 'System.Core.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll')
$references = @()
foreach ($name in $referenceNames) {
    $path = Join-Path $frameworkDirectory $name
    if (-not (Test-Path -LiteralPath $path)) { throw "缺少程序集：$path" }
    $references += "/reference:$path"
}

$compilerArguments = @(
    '/nologo'
    '/target:winexe'
    '/platform:anycpu'
    '/langversion:5'      # 与 .NET Framework 4.x 自带编译器保持一致的语言版本
    '/codepage:65001'     # 源码为 UTF-8（无 BOM），必须显式指定代码页，否则中文会乱码
    '/warn:4'
    "/out:$outputPath"
)

if ($Configuration -eq 'Release') {
    $compilerArguments += '/optimize+'
    $compilerArguments += '/debug-'
}
else {
    $compilerArguments += '/optimize-'
    $compilerArguments += '/debug+'
    $compilerArguments += '/define:DEBUG'
}

$manifestPath = Join-Path $sourceRoot 'app.manifest'
if (Test-Path -LiteralPath $manifestPath) {
    $compilerArguments += "/win32manifest:$manifestPath"
}

$iconPath = Join-Path $root 'assets\app.ico'
if (Test-Path -LiteralPath $iconPath) {
    $compilerArguments += "/win32icon:$iconPath"
}

$compilerArguments += $references
$compilerArguments += $sources

Write-Host "编译器 : $csc"
Write-Host "配置   : $Configuration"
Write-Host "源文件 : $($sources.Count) 个"
Write-Host "输出   : $outputPath"
Write-Host ''

if (Test-Path -LiteralPath $outputPath) { Remove-Item -LiteralPath $outputPath -Force }

& $csc $compilerArguments 2>&1 | ForEach-Object { Write-Host $_ }
$compilerExitCode = $LASTEXITCODE

if ($compilerExitCode -ne 0) {
    throw "编译失败，csc.exe 退出码：$compilerExitCode"
}

if (-not (Test-Path -LiteralPath $outputPath)) {
    throw "编译未报错，但没有生成 $outputPath"
}

$file = Get-Item -LiteralPath $outputPath
Write-Host ''
Write-Host ("编译成功：{0}（{1:N0} 字节）" -f $file.FullName, $file.Length)

if ($SkipSelfTest) {
    Write-Host '已跳过内置自检（-SkipSelfTest）。'
    return
}

# 用 --log 收集自检结果，避免依赖控制台附加行为，在 CI 中同样可靠
Write-Host ''
Write-Host '运行内置自检…'
$reportPath = Join-Path $outputDirectoryPath 'selftest-report.txt'
if (Test-Path -LiteralPath $reportPath) { Remove-Item -LiteralPath $reportPath -Force }

$process = Start-Process -FilePath $outputPath `
    -ArgumentList @('--selftest', '--log', $reportPath) `
    -Wait -PassThru -WindowStyle Hidden

if (Test-Path -LiteralPath $reportPath) {
    Get-Content -LiteralPath $reportPath -Encoding UTF8 | Write-Host
}
else {
    Write-Warning "自检未生成报告文件：$reportPath"
}

if ($process.ExitCode -ne 0) {
    throw "内置自检未通过，退出码：$($process.ExitCode)"
}

Write-Host ''
Write-Host '全部完成。可直接运行：'
Write-Host "  $outputPath"
