# 贡献指南

感谢你愿意改进抽号机。

## 开始前

1. 请先搜索现有 Issue，避免重复报告或开发。
2. 不要提交真实学生名单、课堂历史、安装包或其他生成的二进制文件。
3. 保持改动范围聚焦；一个提交只处理一个明确问题。

## 本地验证

在提交 Pull Request 前，至少运行：

```powershell
dotnet build .\DrawMachineDesktop\DrawMachineDesktop.csproj -c Release
dotnet build .\DrawMachineInstaller\DrawMachineInstaller.csproj -c Release
```

如果改动涉及发布或安装流程，请使用 `scripts/Build-Release.ps1` 完成一次完整构建。

## Pull Request

请在说明中写明：

- 解决的问题和实现方式；
- 已执行的验证命令；
- 对界面、数据格式或安装行为的影响。
