# FastWindowResizer

一个 Windows 托盘小工具：右键托盘图标，点击目标窗口，将它找回到当前操作屏幕中央。

## 使用

1. 打开 `dist/FastWindowResizer/FastWindowResizer.exe`。
2. 在任务栏通知区域找到蓝色窗口图标。若被 Windows 收起，在托盘的隐藏图标菜单中寻找；可自行拖到常显区域。
3. 右键图标，再点击要找回的窗口。
4. 退出时右键图标，选择“退出”。

## 开机自启

右键托盘图标，点击菜单顶部的“开机自启”。勾选后，当前用户下次登录 Windows 时自动运行；再次点击可关闭。首次使用默认不启用，不需要管理员权限。

自启记录指向当前运行的 EXE，路径含空格也可以使用。请先把便携文件夹放到固定位置再开启；移动文件夹后，在新位置启动程序并重新勾选。删除工具前可先取消勾选。

勾选状态表示已为当前程序位置写入自启配置。如果曾在 Windows 设置或任务管理器的“启动应用”中禁用它，还需要在那里重新启用。自启发生在用户登录后，Windows 可能延迟启动。

程序没有主窗口，不注册快捷键，也不会在后台持续调整窗口。对普通窗口保留合理尺寸；最小化、最大化窗口先还原；异常过小或过大的可缩放窗口会调整尺寸。固定尺寸对话框只移动。

应用与托盘使用蓝底白色“窗口＋向内箭头”图标，包含 16、20、24、32、48、64、256 像素版本。图标来源与生成提示词见源码目录中的 `Assets/icon-prompt.md`。

目标显示器取打开菜单时鼠标所在的显示器，位置使用排除任务栏后的工作区。列表按窗口显示，同一应用可以有多项；浏览器标签页不单独显示。默认仅显示当前虚拟桌面的任务窗口，内容接近 Alt+Tab 的“仅窗口”列表。

## 构建与检查

需要 Windows x64 和 .NET 10 SDK。项目不需要第三方 NuGet 包或完整 Visual Studio。

```powershell
# 构建自包含便携版本
.\build.ps1

# 运行受控窗口与几何计算检查
.\build.ps1 -Test
```

构建脚本优先使用 `%LOCALAPPDATA%/FastWindowResizer/dotnet/dotnet.exe`，否则使用 PATH 中的 `dotnet`。当前开发环境的 SDK 安装在该用户目录，没有修改系统 PATH。

测试会短暂显示自建窗口和菜单，只对这些测试窗口执行移动和缩放，不修改真实应用的窗口位置。窗口列表测试会读取当前桌面窗口信息。测试项目不包含在应用编译中，也不随便携版本交付。

便携版本自带运行时，需要保留整个输出文件夹，不能只复制其中的 EXE。可选用下面的命令生成依赖目标电脑已安装 .NET 10 Desktop Runtime 的较小版本：

```powershell
dotnet publish FastWindowResizer.csproj -c Release -r win-x64 --self-contained false -o dist/framework-dependent
```

## 已知边界

- 工具必须运行在目标应用所在的交互式用户会话中。
- 默认普通权限运行。管理员应用可能需要以管理员身份运行本工具才能调整。
- 无响应、特殊全屏、自行锁定尺寸或位置的应用可能无法调整；失败时使用托盘通知提示，系统通知设置可能影响提示显示。
- 不保证与所有 Windows 版本、虚拟桌面设置下的 Alt+Tab 内容逐项一致。
- 不提供自动修复、布局管理或后台网络功能。

设计范围与验收场景见 [PLAN.md](docs/PLAN.md)。实测结果见 [VALIDATION.md](docs/VALIDATION.md)。

首轮实测：闲置工作集约 36.7 MiB、私有内存约 8.6 MiB，10 秒采样内 CPU 时间增量为 0。自包含文件夹约 109 MiB，主要为随附运行时。25 项受控检查通过；真实远程重连、混合 DPI 多屏及长期运行仍需使用中验证。

## 项目结构

```text
FastWindowResizer/
  *.cs / *.csproj          单个 WinForms 应用项目
  app.manifest            Windows 权限和兼容性声明
  build.ps1               构建与检查入口
  Assets/                 应用图标、预览和来源说明
    source/               图标生成原图与完整提示词
  docs/                   规划、验证记录
  scripts/                图标导出工具（Python + Pillow）
  tests/                  受控窗口和自启配置检查
  dist/                   本地发布产物，不纳入 Git
```

源码、最终图标和原始图稿纳入 Git；编译目录、发布包、中间生成文件、个人 IDE 配置及 `.env` 文件不纳入 Git。正常构建不需要图像 API 密钥或 Python。

运行中的程序可能占用原发布目录；可用 `./build.ps1 -OutputDirectory dist/check` 发布到独立目录。修改原图后，用 `python scripts/prepare_icon.py` 重新导出图标。
