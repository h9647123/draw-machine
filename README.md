# 抽号机

面向课堂场景的 Windows 抽号工具。它支持导入学生名单、随机抽号、课堂记录与课堂包导出，并提供托盘和悬浮球入口，方便教师在教学过程中快速操作。

## 功能概览

- 导入和管理学生名单，支持按组使用。
- 随机抽取学生，并记录已抽、未抽、到课和缺席状态。
- 导出名单、历史记录、课堂总结和课堂包。
- 使用托盘图标与悬浮球快速打开常用操作。
- 单实例运行，支持登录后静默启动到托盘。

## 运行环境

- Windows 10 或更高版本
- .NET 6 SDK（仅从源码构建时需要）
- Inno Setup 6（仅构建传统安装程序时需要）

## 从源码运行

```powershell
dotnet build .\DrawMachineDesktop\DrawMachineDesktop.csproj -c Release
dotnet run --project .\DrawMachineDesktop\DrawMachineDesktop.csproj -c Release
```

## 构建发布包

在仓库根目录运行：

```powershell
.\scripts\Build-Release.ps1
```

脚本会将桌面程序发布为单文件可执行程序，临时写入安装器所需的嵌入资源，并在 `artifacts\<版本号>` 下产出：

- `desktop\抽号机.exe`：免安装桌面程序
- `installer\抽号机安装器.exe`：内置安装器
- `setup\抽号机安装器.exe`：Inno Setup 安装程序（本机安装 Inno Setup 时生成）

构建时生成的 `DrawMachineInstaller\Payload\DrawMachine.exe` 和所有发布输出均被 Git 忽略，不会进入源码仓库。

## 名单格式

每行使用空格分隔的“序号、姓名、性别、组号”，例如：

```text
1 同学甲 女 1
2 同学乙 男 1
3 同学丙 女 2
```

可参考 `examples/roster-example.txt`。请不要把真实学生名单、课堂记录或其他个人信息提交到公共仓库。

## 参与贡献

欢迎通过 Issue 报告问题或提出建议；提交代码前请先阅读 `CONTRIBUTING.md`。安全问题请遵循 `SECURITY.md` 中的私下报告方式。

## 许可证

本项目使用 MIT License 开源。

## 鸣谢

感谢 OpenAI-Codex 在本项目开发过程中的协助。
