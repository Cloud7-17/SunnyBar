# 构建与便携包

## 环境与版本

Windows x64、.NET SDK 10.0.403。global.json 固定该版本且关闭 roll-forward；CI 从相同文件安装 SDK，避免构建机自动选择其他 feature/patch 版本。更新 SDK 时先修改 global.json 并重新验证。便携用户不需要 SDK。

应用版本在 Directory.Build.props 中，当前 0.2.0-preview.1。自包含运行时版本固定于 SunnyBar.App.csproj，当前 10.0.14；更新 .NET 补丁时须修改它、重新打包并回归。日常开发构建仍使用已安装共享运行时。

依赖恢复需要访问 NuGet 官方源；便携包运行离线。请使用可信 SDK 和依赖源。

## 构建、测试、渲染与打包

在仓库根目录：

```powershell
dotnet restore SunnyBar.slnx
dotnet build SunnyBar.slnx -c Release --no-restore -m:1 -nr:false
dotnet test SunnyBar.slnx -c Release --no-build --no-restore -m:1 -nr:false --logger trx --results-directory artifacts/test-results
dotnet run --project tools/SunnyBar.VisualChecks -c Release -- artifacts/visual-checks
./scripts/publish.ps1 -Dotnet dotnet
```

若 PowerShell 的脚本策略禁止执行，可在本次构建进程使用以下命令；它不更改系统执行策略：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ./scripts/publish.ps1
```

该脚本会优先使用项目内 SDK，也支持 `-Dotnet` 指定可执行文件。普通用户启动便携版只需双击 EXE。

VisualChecks 是隔离的 WPF 绑定检查与演示图生成器。使用固定时钟、北京和内置标记，配置写入指定输出目录下的 test-data，不读取真实设置、不改启动注册。截图为 WPF 渲染图，不等于多 DPI 的人工桌面验收。

脚本使用 Release / win-x64 / self-contained，关闭裁剪、单文件与调试符号，保留完整运行文件。每次创建新的 staging 目录，复制应用说明、MIT 与官方运行时许可证，生成 release.json 和 SHA256SUMS.txt，再压缩整个目录。

输出：

- artifacts/releases/SunnyBar-0.2.0-preview.1-win-x64.zip
- 同名 .zip.sha256：整个 ZIP 的 SHA256
- artifacts/staging/<随机编号>/SunnyBar-0.2.0-preview.1-win-x64：本次未压缩文件

不承诺 ZIP 字节级复现：时间戳与归档元数据会变化。构建步骤、源码版本、SDK 与运行时版本均可明确复用。

## 解压校验

```powershell
Expand-Archive ./artifacts/releases/SunnyBar-0.2.0-preview.1-win-x64.zip -DestinationPath ./artifacts/unpacked-check
./scripts/verify-portable.ps1 -Directory ./artifacts/unpacked-check/SunnyBar-0.2.0-preview.1-win-x64
```

校验每个文件的哈希，拒绝清单外文件、重复条目和逃逸路径，检查自包含 runtimeconfig 与必要运行时文件。SHA256 证明文件完整性，不代表代码签名或可信来源认证。

发布前再从解压目录实际双击 EXE；退出旧版，避免单实例锁只唤醒旧进程。确认加载目录内 coreclr.dll，随后按 RELEASE_CHECKLIST.md 在干净 Windows 电脑验证。

自动启动检查：先从托盘退出所有 Sunny Bar，再运行 `./scripts/smoke-portable.ps1 -Directory <解压目录> -ResultPath artifacts/portable-smoke-result.json`。此脚本以无效 DOTNET_ROOT 和仅有系统目录的 PATH 启动本包，确认包内 coreclr.dll、窗口样式、单实例、正常退出和用户配置/启动项不变。它不会注销、改缩放或开启自启动。

## Windows CI

`.github/workflows/build.yml` 在 push、pull_request 或手动触发时执行 Release 编译、测试、隔离 WPF 绑定检查、便携包与哈希检查，解压后的独立运行时启动与退出验证，并上传测试结果和 ZIP。只授予 contents:read，不自动发布 Release 或推送提交。

Actions 版本按官方仓库文档配置：[checkout](https://github.com/actions/checkout)、[setup-dotnet](https://github.com/actions/setup-dotnet)、[upload-artifact](https://github.com/actions/upload-artifact)。远程仓库为 https://github.com/Cloud7-17/SunnyBar 。本机已执行等价构建步骤，云端实际结果见仓库 Actions 与 VALIDATION.md 的最新记录。

## 便携卸载与后续安装器

便携版不写安装目录注册，不附带卸载器。先取消登录启动、保存并退出，再删除解压目录；数据目录可选保留或自行清除。V1.0 的安装器、签名和公开 Release 留待干净电脑验收及仓库配置完成后处理。
