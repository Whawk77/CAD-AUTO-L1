---
name: screenshot
description: 当用户在 Codex 中说“启动截图热键”“启动截图插件”“启动常驻截图”“打开截图工具”“启动 Codex 截图”时，启动本地 Windows Codex 截图标注托盘工具；也用于说明、停止、配置或排查该工具。
---

# Codex 截图标注

当用户要求启动截图热键或截图插件时，运行常驻托盘模式：

```powershell
powershell -ExecutionPolicy Bypass -File C:\Users\Administrator\plugins\codex-screenshot\scripts\start-codex-screenshot.ps1
```

常驻模式会在系统托盘运行，并注册全局快捷键 `Ctrl+Shift+S`。启动脚本会先检查是否已有 `CodexScreenshot` 进程，已有进程时直接退出，避免托盘里出现多个插件图标。

## 使用

- `Ctrl+Shift+S`：选择区域截图。
- `Ctrl+Shift+D`：把当前前台 Codex 窗口绑定为粘贴目标。
- 标注工具支持矩形、箭头、画笔、文字、撤销、保存 PNG、复制并粘贴。
- 如果自动粘贴失败，图片仍保留在剪贴板，可手动按 `Ctrl+V`。

## 设置

设置文件位于 `%APPDATA%\CodexScreenshot\settings.json`：

```json
{
  "hotkey": "Ctrl+Shift+S",
  "pasteMode": "autoPaste",
  "targetWindowTitleContains": "Codex",
  "targetWindowHandle": 0,
  "targetWindowTitle": "",
  "saveCopies": true
}
```

托盘菜单里的“设置”会用记事本打开这个文件。

## 构建

```powershell
powershell -ExecutionPolicy Bypass -File C:\Users\Administrator\plugins\codex-screenshot\scripts\build-codex-screenshot.ps1
```
