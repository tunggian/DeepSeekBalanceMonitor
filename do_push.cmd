@echo off
cd /d "D:\Produce\ToDo\AICoding\Deepseek余额监控"
git add -A
if errorlevel 1 exit /b 1
git commit -m "feat: 初始提交 - DeepSeek API 余额托盘监控程序

- 桌面悬浮窗实时显示 DeepSeek 账户余额
- 支持贴边隐藏、拖拽移动
- 明/暗/跟随系统三种主题
- 托盘图标显示余额概览
- DPAPI 加密存储 API Key
- 轻量无依赖，仅需 .NET Framework 4.x

Co-Authored-By: Claude <noreply@anthropic.com>"
if errorlevel 1 exit /b 1
git push -u origin master
if errorlevel 1 exit /b 1
echo "✓ 推送完成"
