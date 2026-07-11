# DeepSeek 余额监控

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.x-blue)
![Platform](https://img.shields.io/badge/Platform-Windows-lightgrey)

Windows 桌面悬浮窗工具，实时监控 DeepSeek API 账户余额与可用状态。托盘图标显示余额概览，悬浮窗支持贴边隐藏、拖拽移动、明暗主题。

## 功能

- **实时余额查询** — 通过 DeepSeek `/user/balance` API 定期获取余额（默认每 1 分钟刷新，可调）
- **桌面悬浮窗** — 紧凑卡片显示余额及状态指示点，拖拽移动，贴边自动隐藏
- **位置记忆** — 拖动结束后立即保存悬浮窗位置，下次启动自动恢复
- **托盘图标** — 通知区图标直接显示余额概览，双击打开悬浮窗，右键提供菜单
- **多主题** — 明亮 / 黑暗 / 跟随系统三种模式，设置页即时预览
- **安全存储** — API Key 通过 Windows DPAPI（`DataProtectionScope.CurrentUser`）加密保存在本地
- **开机自启** — 可选开机自动启动
- **轻量无依赖** — 单 EXE 文件，仅需 .NET Framework 4.x（Windows 自带）

## 截图

| 明亮模式 | 黑暗模式 |
|---------|---------|
| ![悬浮窗-明亮](screenshots/悬浮窗-明亮.png) | ![悬浮窗-黑暗](screenshots/悬浮窗-黑暗.png) |
| ![设置页-明亮](screenshots/设置页-明亮.png) | ![设置页-黑暗](screenshots/设置页-黑暗.png) |

## 快速开始

1. 从 [Releases](https://github.com/tunggian/DeepSeekBalanceMonitor/releases) 下载最新版 `DeepSeekBalanceMonitor.exe`
2. 双击运行，程序将在系统托盘显示图标
3. 右键托盘图标 →「设置」
4. 输入你的 DeepSeek API Key
5. 关闭设置窗口，悬浮窗即显示余额

> 没有 API Key？前往 [DeepSeek 开放平台](https://platform.deepseek.com/) 注册并创建。

## 更新内容

### v1.3.0（2026-07-11）

- 新增悬浮窗位置记忆：拖动结束后立即保存，重新启动软件时恢复到上次位置。

## 从源码构建

需要 .NET Framework 4.x SDK（`csc.exe`，Windows 自带）。

```powershell
.\build.ps1
```

构建产物：`DeepSeekBalanceMonitor.exe`

### 手工编译

```powershell
$csc = "${env:WINDIR}\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

& $csc /nologo /target:winexe /optimize+ /win32icon:AppIcon.ico /out:DeepSeekBalanceMonitor.exe `
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll `
  /r:System.Web.Extensions.dll /r:System.Security.dll /r:System.Net.Http.dll `
  Core.cs NativeMethods.cs IconPainter.cs FloatingBalanceForm.cs SettingsForm.cs TrayAppContext.cs Program.cs
```

## 配置

配置文件位于：`%AppData%\DeepSeekBalanceMonitor\settings.ini`

| 配置项 | 说明 | 默认值 |
|-------|------|--------|
| `refresh_minutes` | 余额刷新间隔（分钟） | `1` |
| `api_key` | DPAPI 加密存储的 API Key | 空 |
| `theme_mode` | 主题模式：`Light` / `Dark` / `System` | `System` |
| `auto_hide` | 悬浮窗贴边自动隐藏 | `true` |
| `always_on_top` | 悬浮窗置顶 | `true` |
| `auto_start` | 开机自启 | `false` |
| `dock_side` | 贴边方向：`Left` / `Right` / `Top` / `Bottom` | 自动 |
| `window_x` / `window_y` | 悬浮窗位置 | 自动 |

## 技术栈

- **语言** — C# 7.3+ (.NET Framework 4.x)
- **UI** — WinForms，全部自绘（`OnPaint`）
- **HTTP** — `HttpClient` 单例，超时 30 秒，自动重试一次
- **加密** — `System.Security.Cryptography.ProtectedData` (DPAPI)
- **构建** — 纯 `csc.exe` 命令行编译，无 MSBuild / Visual Studio 依赖

## 项目结构

```
DeepSeekBalanceMonitor/
├── Core.cs              # DPI 缩放、模型、设置管理、DPAPI 加密、API 客户端
├── NativeMethods.cs     # Win32 P/Invoke
├── IconPainter.cs       # 托盘图标自绘
├── FloatingBalanceForm.cs  # 悬浮窗桌面窗体（贴边、拖拽、自绘布局）
├── SettingsForm.cs      # 设置窗口
├── TrayAppContext.cs    # 托盘上下文（菜单、刷新调度、开机自启）
├── Program.cs           # 入口 + 全局 HTTP 配置
├── build.ps1            # 构建脚本
├── AppIcon.ico          # 应用程序图标（编译进 EXE）
├── refresh-icon.png     # 刷新按钮图标（运行时加载）
├── CHANGELOG.md         # 变更日志
└── .gitignore
```

## 许可

MIT License

---

*本工具仅用于查询 DeepSeek API 余额，不会收集或上传任何个人信息。API Key 仅在你本地加密存储，用于直接调用 DeepSeek 官方接口。*
