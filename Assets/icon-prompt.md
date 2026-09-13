# FastWindowResizer 图标生成说明

用途：Windows 应用图标及系统托盘图标。

构思：一个清楚的应用窗口轮廓，结合指向中心的简洁箭头，表达“将窗口找回屏幕中央”。保持与现有蓝色图标的视觉延续。

## 生成提示词

Use case: logo-brand
Asset type: Windows application icon and system tray icon for FastWindowResizer.
Primary request: Create one clean, distinctive icon for a utility that brings lost off-screen application windows back to the center of the display.
Subject: A bold application-window silhouette integrated with a single simple inward arrow, suggesting returning a window to the center.
Style: Flat, precise geometric shapes, restrained and practical desktop utility design.
Composition: One centered symbol on a square canvas, balanced margins, large solid shapes that remain recognizable at 16 and 32 pixels.
Color palette: Continue the existing app's blue and white visual identity; strong contrast.
Background: Truly transparent outside the icon silhouette; preserve alpha.
Constraints: No lettering, no words, no watermark, no mockup, no presentation board, no multiple variants in one image. No fine details, hairline strokes, complex perspective, reflections, or elaborate shadows.

## 输出与接入

- 保存生成原图为 Assets/app-generated-v2.png，保留透明通道。
- 检查 16、20、24、32、48、64 和 256 像素下的可辨认度。
- 导出包含上述尺寸的 Assets/app-generated-v2.ico。
- 保留现有 Assets/app.ico；接入时更新项目图标与托盘资源引用。

## 实际生成记录

- 使用用户明确授权的 CLI/API 模式，调用 imagegen 技能自带 image_gen.py；请求模型 gpt-image-2，quality=high，size=1024x1024。
- 服务地址使用用户指定的兼容 API；密钥仅通过调用进程环境变量传入，没有写入项目文件。
- 实际完整提示词见 [source/icon-v2-prompt.txt](source/icon-v2-prompt.txt)。由于 CLI 模型的透明参数限制，提示词请求纯绿色背景供本地去除。
- 服务实际返回 1254×1254 RGBA 图片，已经具有透明通道。因此最终使用原始 alpha，不使用背景移除结果。
- 原始返回图片保存在 Assets/source/app-generated-v2-original.png，保留原始 alpha；重复的中间副本放入忽略版本管理的 output/ 目录。
- 使用 `python scripts/prepare_icon.py` 裁去多余透明边距并统一为 1024×1024，导出 PNG、7 尺寸 ICO 和深浅背景预览。脚本仅需 Pillow，不调用 API。
- 新资源文件以 v2 命名，原 Assets/app.ico 保留；应用 EXE 图标和托盘嵌入资源均已指向新 ICO。

状态：生成与小尺寸视觉检查已完成。
