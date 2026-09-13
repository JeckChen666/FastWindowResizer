# Windows 安装包

采用 Inno Setup 6.7+，包装现有自包含发布目录，应用本身不需要修改或额外安装 .NET。

```powershell
.\build.ps1
.\scripts\build-installer.ps1
```

也可以指定已经发布的版本，避免重新编译应用：

```powershell
.\scripts\build-installer.ps1 -PublishDirectory dist/releases/v1.0.0/FastWindowResizer -OutputDirectory dist/releases/v1.0.0
```

编译器安装位置会自动查找，也可传 `-CompilerPath`。编译器下载见 [Inno Setup 官网](https://jrsoftware.org/isdl.php)。

安装行为：

- 当前用户安装，默认 `%LOCALAPPDATA%\Programs\FastWindowResizer`，不申请管理员权限。
- 简体中文或英文向导，可修改目录，默认创建开始菜单快捷方式，可选桌面快捷方式。
- 安装结束可启动应用；静默安装不会自动启动。
- Windows“已安装的应用”提供卸载入口。
- 安装或卸载前检测应用单实例互斥量，提示退出正在运行的工具。
- 卸载仅移除指向本安装目录的自启项，不修改其他便携副本的自启项。
- 自启仍通过应用托盘菜单设置；从便携版迁移后应在安装版重新勾选。

重复安装使用固定 AppId，沿用已有安装目录与选项。安装程序只卸载它记录的文件，不递归删除用户额外放入的文件。

`Languages/ChineseSimplified.isl` 来自 Inno Setup 官方源码仓库的 [is-6_7_3 版本](https://github.com/jrsoftware/issrc/blob/is-6_7_3/Files/Languages/Unofficial/ChineseSimplified.isl)，保留文件头中的译者信息。这是项目收录的非官方简体中文翻译。

运行 `./tests/installer-check.ps1` 可执行实际安装/重复安装/卸载检查。该检查要求当前用户没有正式安装的同名版本，临时使用 `output/installer-check/` 目录和开始菜单快捷方式，并在结束后恢复原有自启值及便携进程。
