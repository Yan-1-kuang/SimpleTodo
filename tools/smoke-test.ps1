<#
.SYNOPSIS
    SimpleTodo 图形界面冒烟测试。

.DESCRIPTION
    启动编译好的 EXE，确认：
      1. 进程没有崩溃退出；
      2. 主窗口出现，标题正确；
      3. 任务列表控件（SysListView32）及其列头（SysHeader32）已渲染出来。

    这一步用于弥补 --selftest 只覆盖逻辑层、不覆盖界面的缺口。

.PARAMETER ExePath
    被测 EXE 路径，默认 dist\SimpleTodo.exe。

.PARAMETER CapturePath
    可选：把窗口截图保存到该 PNG 路径（README 截图可用它生成）。

.PARAMETER KeepData
    保留测试用的数据目录，便于排查问题。

.EXAMPLE
    pwsh -File tools/smoke-test.ps1
    pwsh -File tools/smoke-test.ps1 -CapturePath docs/screenshot.png
#>
[CmdletBinding()]
param(
    [string]$ExePath,
    [string]$CapturePath,
    [switch]$KeepData
)

$ErrorActionPreference = 'Stop'

$toolsDirectory = $PSScriptRoot
if (-not $toolsDirectory) { $toolsDirectory = (Get-Location).Path }
$repoRoot = Split-Path -Parent $toolsDirectory

if (-not $ExePath) { $ExePath = Join-Path $repoRoot 'dist\SimpleTodo.exe' }
if (-not (Test-Path -LiteralPath $ExePath)) {
    throw "找不到被测程序：$ExePath`n请先运行 build.ps1。"
}
$ExePath = (Resolve-Path -LiteralPath $ExePath).Path

$workDirectory = Join-Path $repoRoot ('.smoke\run-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
if (Test-Path -LiteralPath $workDirectory) { Remove-Item -LiteralPath $workDirectory -Recurse -Force }
New-Item -ItemType Directory -Path $workDirectory -Force | Out-Null
$dataPath = Join-Path $workDirectory 'tasks.json'

# 准备一份包含各类状态的数据，确保列表渲染路径都被走到
$seed = @'
{
  "version": 1,
  "items": [
    { "id": "11111111-1111-1111-1111-111111111111", "title": "逾期且高优先级的任务", "note": "含 \"引号\" 与 \\ 反斜杠", "done": false, "priority": 2, "due": "2020-01-01T00:00:00", "created": "2025-01-05T09:12:00", "completed": null },
    { "id": "22222222-2222-2222-2222-222222222222", "title": "普通任务", "note": "", "done": false, "priority": 1, "due": "2030-06-30T00:00:00", "created": "2025-01-04T14:00:00", "completed": null },
    { "id": "33333333-3333-3333-3333-333333333333", "title": "无截止日期的低优先级任务", "note": "备注文本", "done": false, "priority": 0, "due": null, "created": "2025-01-03T08:30:00", "completed": null },
    { "id": "44444444-4444-4444-4444-444444444444", "title": "已完成的任务", "note": "", "done": true, "priority": 1, "due": null, "created": "2025-01-02T18:00:00", "completed": "2025-01-02T19:20:00" }
  ]
}
'@
Set-Content -LiteralPath $dataPath -Value $seed -Encoding UTF8

Write-Host "被测程序 : $ExePath"
Write-Host "数据文件 : $dataPath"
Write-Host ''

$failures = New-Object System.Collections.Generic.List[string]
$process = $null

try {
    $process = Start-Process -FilePath $ExePath -ArgumentList @('--data', $dataPath) -PassThru

    # 等待主窗口出现
    $deadline = (Get-Date).AddSeconds(30)
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 250
        $process.Refresh()
        if ($process.HasExited) { break }
        if ($process.MainWindowHandle -ne 0) { break }
    }

    if ($process.HasExited) {
        $failures.Add("进程在窗口出现前就退出了，退出码 $($process.ExitCode)")
    }
    else {
        $title = $process.MainWindowTitle
        if ($title -eq '极简待办 SimpleTodo') {
            Write-Host "[通过] 主窗口标题正确：$title"
        }
        else {
            $failures.Add("主窗口标题不正确，实际为「$title」")
        }

        # 界面控件检查（需要 UI Automation）
        try {
            Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes

            $window = [System.Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle)
            $descendants = $window.FindAll(
                [System.Windows.Automation.TreeScope]::Descendants,
                [System.Windows.Automation.Condition]::TrueCondition)

            $listView = $null
            $header = $null
            foreach ($element in $descendants) {
                $className = $element.Current.ClassName
                if ($className -like '*SysListView32*') { $listView = $element }
                if ($className -like 'SysHeader32*') { $header = $element }
            }

            if ($null -eq $listView) {
                $failures.Add('未找到任务列表控件（SysListView32）')
            }
            elseif ($listView.Current.IsOffscreen) {
                $failures.Add('任务列表控件存在但不可见，说明任务没有加载成功')
            }
            else {
                $rect = $listView.Current.BoundingRectangle
                Write-Host ("[通过] 任务列表已渲染：{0}x{1}" -f [int]$rect.Width, [int]$rect.Height)
            }

            if ($null -eq $header) {
                $failures.Add('未找到列表列头（SysHeader32）')
            }
            else {
                Write-Host '[通过] 列表列头已渲染'
            }

            # 单实例限制：同一数据文件不应再打开第二个主界面
            $second = $null
            try {
                $second = Start-Process -FilePath $ExePath -ArgumentList @('--data', $dataPath) -PassThru

                $secondDeadline = (Get-Date).AddSeconds(20)
                while ((Get-Date) -lt $secondDeadline) {
                    Start-Sleep -Milliseconds 250
                    $second.Refresh()
                    if ($second.HasExited -or $second.MainWindowHandle -ne 0) { break }
                }
                Start-Sleep -Milliseconds 600

                if ($second.HasExited) {
                    $failures.Add("第二个实例直接退出（退出码 $($second.ExitCode)），预期是弹出提示对话框")
                }
                else {
                    $secondWindow = [System.Windows.Automation.AutomationElement]::FromHandle($second.MainWindowHandle)
                    $secondDescendants = $secondWindow.FindAll(
                        [System.Windows.Automation.TreeScope]::Descendants,
                        [System.Windows.Automation.Condition]::TrueCondition)

                    $secondHasList = $false
                    foreach ($element in $secondDescendants) {
                        if ($element.Current.ClassName -like '*SysListView32*') { $secondHasList = $true }
                    }

                    if ($secondHasList) {
                        $failures.Add('第二个实例创建了任务列表，单实例限制失效')
                    }
                    else {
                        Write-Host '[通过] 第二个实例被拒绝（仅显示提示对话框）'
                    }
                }
            }
            catch {
                Write-Warning "跳过单实例检查：$($_.Exception.Message)"
            }
            finally {
                if ($null -ne $second -and -not $second.HasExited) {
                    $second.CloseMainWindow() | Out-Null
                    if (-not $second.WaitForExit(3000)) { $second.Kill() }
                }
            }

            if ($CapturePath) {
                Add-Type -AssemblyName System.Drawing, Microsoft.VisualBasic
                [Microsoft.VisualBasic.Interaction]::AppActivate($process.Id) | Out-Null
                Start-Sleep -Milliseconds 700

                $rect = $window.Current.BoundingRectangle
                $captureDirectory = Split-Path -Parent $CapturePath
                if ($captureDirectory -and -not (Test-Path -LiteralPath $captureDirectory)) {
                    New-Item -ItemType Directory -Path $captureDirectory | Out-Null
                }

                $bounds = New-Object System.Drawing.Rectangle([int]$rect.X, [int]$rect.Y, [int]$rect.Width, [int]$rect.Height)
                $bitmap = New-Object System.Drawing.Bitmap($bounds.Width, $bounds.Height)
                $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
                $graphics.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
                $bitmap.Save($CapturePath, [System.Drawing.Imaging.ImageFormat]::Png)
                $graphics.Dispose()
                $bitmap.Dispose()
                Write-Host "[通过] 已保存截图：$CapturePath"
            }
        }
        catch {
            Write-Warning "跳过界面控件检查（UI Automation 不可用）：$($_.Exception.Message)"
        }
    }
}
finally {
    if ($null -ne $process -and -not $process.HasExited) {
        $process.CloseMainWindow() | Out-Null
        if (-not $process.WaitForExit(3000)) { $process.Kill() }
        Write-Host '已关闭被测程序。'
    }

    if (-not $KeepData -and (Test-Path -LiteralPath $workDirectory)) {
        Remove-Item -LiteralPath $workDirectory -Recurse -Force
    }
    elseif ($KeepData) {
        Write-Host "已保留测试数据目录：$workDirectory"
    }
}

Write-Host ''
if ($failures.Count -gt 0) {
    Write-Host "冒烟测试失败，共 $($failures.Count) 项：" -ForegroundColor Red
    foreach ($failure in $failures) { Write-Host "  - $failure" -ForegroundColor Red }
    exit 1
}

Write-Host '冒烟测试通过。' -ForegroundColor Green
exit 0
