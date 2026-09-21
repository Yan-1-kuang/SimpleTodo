# 极简待办 SimpleTodo

一个轻量的 Windows 桌面待办清单工具：**单文件 EXE（约 180 KB）**、**零第三方依赖**、**数据保存为可读 JSON**、**完全离线**。

<!--
把下面的 OWNER/REPO 换成你的 GitHub 用户名和仓库名，再删掉这行注释即可显示徽章：

[![build](https://github.com/OWNER/REPO/actions/workflows/build.yml/badge.svg)](https://github.com/OWNER/REPO/actions/workflows/build.yml)
[![license](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![platform](https://img.shields.io/badge/platform-Windows%2010%20%2F%2011-lightgrey.svg)](#运行环境)
-->

## 目录

- [特性](#特性)
- [运行环境](#运行环境)
- [快速开始](#快速开始)
- [快捷键](#快捷键)
- [命令行参数](#命令行参数)
- [数据文件](#数据文件)
- [项目结构](#项目结构)
- [测试](#测试)
- [常见问题](#常见问题)
- [设计说明](#设计说明)
- [路线图](#路线图)
- [许可证](#许可证)

## 特性

**任务管理**

- 顶部输入框输入内容，按 <kbd>Enter</kbd> 即可添加任务
- 双击或 <kbd>F2</kbd> 编辑任务：标题、备注、优先级、截止日期
- <kbd>Delete</kbd> 删除选中任务，支持多选（<kbd>Ctrl</kbd>+<kbd>A</kbd> 全选）
- 单击列表左侧复选框标记完成，已完成任务显示删除线并置灰

**一眼看清状态**

- 优先级：高 / 中 / 低，高优先级任务加粗显示
- 截止日期可选；**逾期显示红色**，**今天到期显示橙色**，无截止日期的任务在按截止日期排序时始终排在最后
- 鼠标悬停显示完整信息（备注、优先级、截止日期、创建/完成时间）

**筛选、搜索与排序**

- 筛选：全部 / 未完成 / 已完成 / 已逾期（<kbd>Ctrl</kbd>+<kbd>1</kbd>…<kbd>4</kbd>）
- 搜索：同时匹配标题与备注，忽略大小写，中文正常（<kbd>Ctrl</kbd>+<kbd>F</kbd> 聚焦，<kbd>Esc</kbd> 清空）
- 排序：单击列标题切换字段与升降序，支持创建时间 / 优先级 / 截止日期 / 任务名 / 完成状态

**数据安全**

- 任何改动立即保存，无需手动保存
- 采用「临时文件 + 原子替换」写入，断电或崩溃不会写坏数据文件
- 数据文件损坏时自动备份为 `tasks.corrupt-时间戳.json` 并明确提示，不会静默丢数据
- 同一数据文件只允许一个窗口（互斥体保护），避免两个实例互相覆盖
- 支持导入 / 导出 JSON，方便备份与迁移

**其它**

- 窗口置顶、高 DPI 感知（高分屏不模糊）、内置快捷键帮助（<kbd>F1</kbd>）
- 界面全中文，无需安装、无需联网、不收集任何数据

## 运行环境

| 系统 | 说明 |
| --- | --- |
| Windows 10 1903+ / Windows 11 | 系统自带 .NET Framework 4.8，**下载即可运行** |
| Windows 7 / 8.1 | 需先安装 [.NET Framework 4.8](https://dotnet.microsoft.com/download/dotnet-framework/net48) |
| 架构 | AnyCPU，32 位与 64 位 Windows 均可 |

## 快速开始

### 方式一：直接使用

1. 从 [Releases](../../releases) 下载 `SimpleTodo.exe`
2. 双击运行

程序会在 `%APPDATA%\SimpleTodo\` 下创建数据文件，首次运行是空清单。

### 方式二：从源码构建

```powershell
git clone https://github.com/OWNER/REPO.git
cd REPO
powershell -ExecutionPolicy Bypass -File build.ps1
```

构建产物：`dist\SimpleTodo.exe`，同时会运行内置自检并输出 `dist\selftest-report.txt`。

**构建要求非常低：**

- Windows
- .NET Framework 4.x 自带的 `csc.exe`（Windows 10/11 默认就有，位于 `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`）
- **不需要** Visual Studio、**.NET SDK**、NuGet、网络

构建脚本会自动查找编译器；如果你的环境比较特殊，可以用 `-CscPath` 手动指定：

```powershell
powershell -File build.ps1 -CscPath "D:\path\to\csc.exe" -Configuration Release
```

## 快捷键

| 快捷键 | 功能 |
| --- | --- |
| <kbd>Enter</kbd> | 在输入框中按下，添加任务 |
| <kbd>Ctrl</kbd>+<kbd>N</kbd> | 聚焦输入框 |
| <kbd>F2</kbd> / 双击 | 编辑选中任务 |
| <kbd>空格</kbd> | 切换选中任务的完成状态 |
| <kbd>Delete</kbd> | 删除选中任务（支持多选） |
| <kbd>Ctrl</kbd>+<kbd>A</kbd> | 全选列表项 |
| <kbd>Ctrl</kbd>+<kbd>F</kbd> | 聚焦搜索框 |
| <kbd>Esc</kbd> | 在搜索框中清空关键词 |
| <kbd>Ctrl</kbd>+<kbd>1</kbd> / <kbd>2</kbd> / <kbd>3</kbd> / <kbd>4</kbd> | 切换筛选：全部 / 未完成 / 已完成 / 已逾期 |
| <kbd>F5</kbd> | 重新从磁盘载入 |
| <kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>E</kbd> | 导出任务到 JSON |
| <kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>I</kbd> | 从 JSON 导入任务 |
| <kbd>F1</kbd> | 快捷键与关于 |

## 命令行参数

```
SimpleTodo.exe [选项]

  --data <文件路径>   指定数据文件（默认 %APPDATA%\SimpleTodo\tasks.json）
  --selftest          运行内置自检并退出（全部通过时退出码为 0）
  --log <文件路径>    与 --selftest 搭配，把自检报告写入文件
  --version           显示版本号
  --help              显示帮助
```

指定 `--data` 后可以在同一台机器上维护多份互相独立的清单，例如：

```powershell
SimpleTodo.exe --data "D:\清单\工作.json"
SimpleTodo.exe --data "D:\清单\个人.json"
```

## 数据文件

默认位置：`%APPDATA%\SimpleTodo\tasks.json`（可在「文件 → 打开数据文件夹」中直接打开）。

格式为 UTF-8 的 JSON，可读、可手工编辑、适合用 git 做版本管理：

```json
{
  "version": 1,
  "items": [
    {
      "id": "6f1c9c1e-3b0a-4b6a-9f4e-2f1f0a8d5c31",
      "title": "写项目周报",
      "note": "包含本周进度与风险",
      "done": false,
      "priority": 2,
      "due": "2026-10-01T00:00:00",
      "created": "2026-09-21T09:12:00",
      "completed": null
    }
  ]
}
```

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `id` | string | GUID，用于稳定排序与导入时去重；缺失会自动生成 |
| `title` | string | 任务内容 |
| `note` | string | 备注，可为空 |
| `done` | bool | 是否已完成 |
| `priority` | number | `0` 低、`1` 中、`2` 高；也接受 `"高"` / `"low"` 等文本 |
| `due` | string / null | 截止日期，`yyyy-MM-ddTHH:mm:ss`，可为 `null` |
| `created` | string | 创建时间 |
| `completed` | string / null | 完成时间；任务未完成时会被自动清空 |

解析时对格式比较宽容：日期也接受 `yyyy-MM-dd`、`yyyy/MM/dd` 等写法；对象与数组允许尾随逗号；未知字段会被忽略；单个条目损坏时只跳过该条目，不会导致整份数据加载失败。

## 项目结构

```
SimpleTodo/
├─ .github/workflows/build.yml   CI：构建 + 自检 + 上传产物 + 打 tag 时发布 Release
├─ assets/app.ico                应用图标（多尺寸，含透明通道）
├─ build.ps1                     构建脚本（csc 直接编译，无需 SDK）
├─ src/
│  ├─ Program.cs                 入口、命令行参数、单实例锁、全局异常处理
│  ├─ SelfTest.cs                内置自检（16 项）
│  ├─ app.manifest               DPI 感知与系统兼容性声明
│  ├─ Models/TodoItem.cs         任务模型与优先级
│  ├─ Services/
│  │  ├─ SimpleJson.cs           手写的极简 JSON 读写器（零依赖）
│  │  ├─ TodoJson.cs             数据文件的序列化 / 反序列化
│  │  ├─ TodoStore.cs            加载与原子保存、损坏自愈
│  │  └─ TodoQuery.cs            筛选、搜索、排序（与界面解耦，可独立测试）
│  └─ UI/
│     ├─ MainForm.cs             主窗口
│     ├─ EditItemDialog.cs       编辑任务对话框
│     └─ HelpDialog.cs           快捷键与关于
├─ tools/
│  ├─ make-icon.cs               图标生成器（可重新生成 assets/app.ico）
│  └─ smoke-test.ps1             界面冒烟测试（启动 EXE 并校验窗口与列表控件）
├─ LICENSE
└─ README.md
```

## 测试

### 逻辑层自检

```powershell
dist\SimpleTodo.exe --selftest
```

覆盖 16 项，包括：JSON 字符串转义往返（引号 / 反斜杠 / 换行 / 中文 / Emoji / 控制字符）、非法 JSON 拒绝、数据文件字段往返、缺省字段与未知字段容错、损坏文件自动备份、保存不残留临时文件、筛选 / 搜索 / 排序（含空截止日期排最后）、优先级解析。

### 界面冒烟测试

```powershell
powershell -ExecutionPolicy Bypass -File tools\smoke-test.ps1
```

启动编译好的 EXE，通过 UI Automation 校验：进程不崩溃、主窗口标题正确、任务列表与列头已渲染、第二个实例会被拒绝。也可以顺便生成一张窗口截图（可用于 README）：

```powershell
powershell -File tools\smoke-test.ps1 -CapturePath docs\screenshot.png
```

### 当前版本的验证结果

- `--selftest`：16 项全部通过
- 冒烟测试：主窗口正常显示（940×620 客户区）、任务列表（`SysListView32`）与列头已渲染
- 单实例：同一数据文件启动第二个实例时仅弹出提示对话框，不会创建第二个主界面
- 图标：9 个尺寸（16–256）均已嵌入 EXE，透明通道正确

## 常见问题

**数据存在哪里？怎么备份？**

默认在 `%APPDATA%\SimpleTodo\tasks.json`。直接复制这个文件即可备份；也可以用「文件 → 导出任务」导出到任意位置。整个文件是纯文本 JSON，出问题时可以用记事本直接修改。

**为什么 EXE 只有 180 KB？**

因为它只使用 Windows 自带的 .NET Framework 类库，没有打包任何运行时。对比之下，.NET 8 的自包含单文件程序通常在 60 MB 以上。代价是目标机器需要有 .NET Framework 4.8 —— 而这在 Windows 10 1903 以后的系统上是默认存在的。

**需要联网吗？会收集数据吗？**

完全不需要联网，也没有任何遥测或联网代码。程序只读写你指定的那个 JSON 文件。

**Windows 提示"已保护你的电脑"（SmartScreen）怎么办？**

这是因为 EXE 没有代码签名证书（签名证书需要付费）。点击「更多信息」→「仍要运行」即可。所有未签名的个人开源程序都会有这个提示。如果介意，可以按上面的步骤自行从源码构建。

**为什么第二个实例只弹一个提示框？**

因为两个窗口同时打开同一个 JSON 文件会互相覆盖数据。程序按数据文件路径加锁，同一份数据只允许一个窗口。如果需要同时管理多份清单，用 `--data` 指定不同文件即可。

**任务会不会因为程序崩溃而丢失？**

每次改动都会立即写入，且采用「临时文件 + 原子替换」的方式，写入过程中断电也不会得到半截文件。如果数据文件被外部程序改坏了，启动时会自动备份为 `tasks.corrupt-时间戳.json` 并提示，原文件保留。

## 设计说明

**为什么选 .NET Framework 4.8 而不是 .NET 8？**

这个项目的目标是一个「下载就能用」的小工具。用 .NET Framework 4.8 编译出来的 EXE 只有一百多 KB，在 Windows 10/11 上双击即运行；而 .NET 8 自包含发布体积大两个数量级。同时它让构建变得极其简单：不需要 SDK、不需要 NuGet、不需要联网，一个 `csc.exe` 就够了。

**为什么手写 JSON 解析器？**

引入 `System.Text.Json` 需要 NuGet 包，引入 `JavaScriptSerializer` 会让数据文件里出现难以阅读的 `\/Date(...)\/` 格式。手写一个约 300 行的解析器，换来的是零依赖 + 人类可读、可手工编辑、可 git diff 的数据文件。这段代码由内置自检严格覆盖（转义、Emoji 代理对、非法输入、尾随逗号等）。

**为什么逻辑和界面分开？**

筛选、搜索、排序都在 `TodoQuery` 里，数据读写在 `TodoStore` / `TodoJson` 里，都不引用任何 WinForms 类型。因此 `--selftest` 能在没有界面的情况下完整验证核心行为，CI 里也能跑。

## 路线图

- [ ] 到期提醒（托盘通知）
- [ ] 重复任务
- [ ] 多清单切换
- [ ] 深色主题
- [ ] 记住窗口大小与位置
- [ ] 拖拽排序 / 手动排序
- [ ] 列表项通过 UI Automation 暴露（便于自动化测试）
- [ ] 中英文双语界面

## 许可证

[MIT](LICENSE)
