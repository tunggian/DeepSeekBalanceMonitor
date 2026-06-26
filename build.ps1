$ErrorActionPreference = "Stop"

function Run-Step($path, [string[]]$arguments) {
  & $path @arguments
  if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

# ---- 自动检测 csc.exe（64位优先，32位 fallback） ----
$csc = if (Test-Path "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe") {
  "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
} elseif (Test-Path "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe") {
  "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
} else {
  throw "找不到 csc.exe，请确认已安装 .NET Framework 4.x SDK"
}

Write-Host "使用编译器: $csc" -ForegroundColor Cyan

# ---- 公共引用 ----
$refs = @(
  "/r:System.dll",
  "/r:System.Core.dll",
  "/r:System.Drawing.dll",
  "/r:System.Windows.Forms.dll",
  "/r:System.Web.Extensions.dll",
  "/r:System.Security.dll",
  "/r:System.Net.Http.dll"
)

# ---- 通用源文件（UI + 业务逻辑） ----
$commonSources = @(
  "Core.cs",
  "NativeMethods.cs",
  "IconPainter.cs",
  "FloatingBalanceForm.cs",
  "SettingsForm.cs",
  "TrayAppContext.cs",
  "Program.cs"
)

# ---- 构建主 EXE ----
Write-Host "`n>> 构建 DeepSeekBalanceMonitor.exe ..." -ForegroundColor Yellow
Run-Step $csc (@(
  "/nologo",
  "/target:winexe",
  "/optimize+",
  "/win32icon:AppIcon.ico",
  "/out:DeepSeekBalanceMonitor.exe"
) + $refs + $commonSources)

# ---- 构建测试 EXE ----
Write-Host "`n>> 构建 Tests.exe ..." -ForegroundColor Yellow
Run-Step $csc (@(
  "/nologo",
  "/target:exe",
  "/out:Tests.exe"
) + $refs + @("Core.cs", "Tests.cs"))

# ---- 运行测试 ----
Write-Host "`n>> 运行测试 ..." -ForegroundColor Yellow
Run-Step ".\Tests.exe" @()

Write-Host "`n✓ 全部完成" -ForegroundColor Green
