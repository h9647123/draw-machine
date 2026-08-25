using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;
using Microsoft.Win32;

namespace DrawMachineInstaller;

internal static class Program
{
    private const string AppName = "抽号机";
    private const string MainExeName = "抽号机.exe";
    private const string StartupRegistryName = "DrawMachineDesktop";
    private const string StartupRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string InstallerRegistryPath = @"Software\DrawMachineDesktop";
    private const string InstallDirectoryRegistryValue = "InstallDirectory";
    private const string CompletionLaunchButtonText = "打开抽号机";
    private const string CompletionOpenFolderButtonText = "打开安装位置";
    private const string CompletionFinishButtonText = "稍后打开";
    private const string InstallerSelfCheckArgument = "--installer-self-check";
    private const string SelfCheckOutputArgument = "--self-check-output";

    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (HasArgument(args, InstallerSelfCheckArgument))
        {
            return RunInstallerSelfCheck(args);
        }

        try
        {
            var options = ShowInstallOptionsDialog();
            if (options is null)
            {
                return 0;
            }

            var installResult = Install(options);
            if (installResult is null)
            {
                return 1;
            }

            ShowCompletionDialog(installResult);
            return 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"安装失败：{ex.Message}\n\n如果抽号机正在运行，请先关闭后重新安装。",
                $"{AppName} 安装失败",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 2;
        }
    }

    private static int RunInstallerSelfCheck(string[] args)
    {
        var outputPath = GetArgumentValue(args, SelfCheckOutputArgument);
        try
        {
            var result = BuildInstallerSelfCheckResult();
            WriteSelfCheckResult(outputPath, result);
            return 0;
        }
        catch (Exception ex)
        {
            WriteSelfCheckResult(outputPath, new InstallerSelfCheckResult
            {
                Ok = false,
                Error = ex.ToString()
            });
            return 2;
        }
    }

    private static InstallerSelfCheckResult BuildInstallerSelfCheckResult()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var tempPayloadPath = Path.Combine(Path.GetTempPath(), $"DrawMachine-payload-{Guid.NewGuid():N}.exe");
        try
        {
            using var payload = assembly.GetManifestResourceStream("DrawMachine.exe")
                ?? throw new InvalidOperationException("安装包缺少主程序资源。");

            using var sha256 = SHA256.Create();
            var payloadHash = Convert.ToHexString(sha256.ComputeHash(payload));
            payload.Position = 0;
            using (var output = File.Create(tempPayloadPath))
            {
                payload.CopyTo(output);
            }

            var installerVersion = FileVersionInfo.GetVersionInfo(Application.ExecutablePath);
            var payloadVersion = FileVersionInfo.GetVersionInfo(tempPayloadPath);
            var installDir = GetInstallDirectory();
            var installerOptionTexts = BuildInstallerOptionTexts();
            var installerOptionsSelfCheckOk = RunInstallerOptionsSelfCheck(installerOptionTexts);
            var targetExePath = Path.Combine(installDir, MainExeName);
            return new InstallerSelfCheckResult
            {
                Ok = true,
                InstallerFileVersion = installerVersion.FileVersion ?? string.Empty,
                InstallerProductVersion = installerVersion.ProductVersion ?? string.Empty,
                PayloadFileVersion = payloadVersion.FileVersion ?? string.Empty,
                PayloadProductVersion = payloadVersion.ProductVersion ?? string.Empty,
                PayloadSizeBytes = new FileInfo(tempPayloadPath).Length,
                PayloadSha256 = payloadHash,
                InstallDirectory = installDir,
                MainExeName = MainExeName,
                TargetExePath = targetExePath,
                DesktopShortcutPath = GetDesktopShortcutPath(),
                StartMenuShortcutPath = GetStartMenuShortcutPath(),
                SupportsCustomInstallDirectory = true,
                SupportsDesktopShortcutChoice = true,
                SupportsStartMenuShortcutChoice = true,
                SupportsStartupChoice = true,
                SupportsLaunchAfterInstallChoice = true,
                InstallerOptionsSelfCheckOk = installerOptionsSelfCheckOk,
                InstallerOptionTexts = installerOptionTexts,
                DefaultInstallDirectory = GetDefaultInstallDirectory(),
                StartupRegistryName = StartupRegistryName,
                StartupRegistryPath = StartupRegistryPath,
                StartupCommandSample = BuildStartupCommand(targetExePath),
                CompletionLaunchButtonText = CompletionLaunchButtonText,
                CompletionFinishButtonText = CompletionFinishButtonText,
                CompletionDefaultAction = CompletionLaunchButtonText
            };
        }
        finally
        {
            if (File.Exists(tempPayloadPath))
            {
                try
                {
                    File.Delete(tempPayloadPath);
                }
                catch
                {
                    // A stale temp payload does not affect installation.
                }
            }
        }
    }

    private static string BuildInstallerOptionTexts()
    {
        return string.Join(
            " / ",
            new[]
            {
                "安装位置",
                "浏览...",
                "创建桌面快捷方式",
                "创建开始菜单快捷方式",
                "开机自启到托盘",
                "安装完成后打开抽号机"
            });
    }

    private static bool RunInstallerOptionsSelfCheck(string optionTexts)
    {
        return optionTexts.Contains("安装位置", StringComparison.Ordinal)
            && optionTexts.Contains("浏览...", StringComparison.Ordinal)
            && optionTexts.Contains("创建桌面快捷方式", StringComparison.Ordinal)
            && optionTexts.Contains("创建开始菜单快捷方式", StringComparison.Ordinal)
            && optionTexts.Contains("开机自启到托盘", StringComparison.Ordinal)
            && optionTexts.Contains("安装完成后打开抽号机", StringComparison.Ordinal);
    }

    private static bool HasArgument(string[] args, string name)
    {
        return args.Any(arg => string.Equals(arg, name, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetArgumentValue(string[] args, string name)
    {
        var prefix = name + "=";
        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];
            if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return arg[prefix.Length..].Trim('"');
            }

            if (string.Equals(arg, name, StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                return args[index + 1].Trim('"');
            }
        }

        return string.Empty;
    }

    private static void WriteSelfCheckResult(string outputPath, InstallerSelfCheckResult result)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return;
        }

        var fullPath = Path.GetFullPath(outputPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(fullPath, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static InstallOptions? ShowInstallOptionsDialog()
    {
        using var dialog = new Form
        {
            Text = $"{AppName} 安装向导",
            StartPosition = FormStartPosition.CenterScreen,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowIcon = true,
            ShowInTaskbar = true,
            ClientSize = new Size(660, 430),
            Font = SystemFonts.MessageBoxFont
        };

        var extractedIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        if (extractedIcon is not null)
        {
            dialog.Icon = extractedIcon;
            dialog.FormClosed += (_, _) => extractedIcon.Dispose();
        }

        var title = new Label
        {
            Text = $"安装 {AppName}",
            AutoSize = false,
            Location = new Point(28, 22),
            Size = new Size(560, 32),
            Font = new Font(SystemFonts.MessageBoxFont!, FontStyle.Bold)
        };

        var subtitle = new Label
        {
            Text = "选择安装位置和常用启动选项。",
            AutoSize = false,
            Location = new Point(28, 56),
            Size = new Size(590, 24)
        };

        var locationLabel = new Label
        {
            Text = "安装位置",
            AutoSize = false,
            Location = new Point(28, 102),
            Size = new Size(590, 24)
        };

        var installPathBox = new TextBox
        {
            Text = GetInstallDirectory(),
            Location = new Point(28, 130),
            Size = new Size(486, 27)
        };

        var browseButton = new Button
        {
            Text = "浏览...",
            Location = new Point(526, 128),
            Size = new Size(92, 32)
        };

        var hint = new Label
        {
            Text = "默认安装到当前用户目录，不需要管理员权限。选择受保护目录时，Windows 可能会拒绝写入。",
            AutoSize = false,
            Location = new Point(28, 166),
            Size = new Size(590, 40)
        };

        var optionsGroup = new GroupBox
        {
            Text = "安装选项",
            Location = new Point(28, 218),
            Size = new Size(590, 112)
        };

        var desktopShortcutBox = new CheckBox
        {
            Text = "创建桌面快捷方式",
            Checked = true,
            Location = new Point(18, 30),
            Size = new Size(240, 24)
        };

        var startMenuShortcutBox = new CheckBox
        {
            Text = "创建开始菜单快捷方式",
            Checked = true,
            Location = new Point(18, 66),
            Size = new Size(240, 24)
        };

        var startupBox = new CheckBox
        {
            Text = "开机自启到托盘",
            Checked = IsStartupRegistrationEnabled(),
            Location = new Point(300, 30),
            Size = new Size(230, 24)
        };

        var launchBox = new CheckBox
        {
            Text = "安装完成后打开抽号机",
            Checked = false,
            Location = new Point(300, 66),
            Size = new Size(230, 24)
        };

        optionsGroup.Controls.AddRange(new Control[]
        {
            desktopShortcutBox,
            startMenuShortcutBox,
            startupBox,
            launchBox
        });

        var installButton = new Button
        {
            Text = "安装",
            Location = new Point(410, 362),
            Size = new Size(96, 34)
        };

        var cancelButton = new Button
        {
            Text = "取消",
            DialogResult = DialogResult.Cancel,
            Location = new Point(522, 362),
            Size = new Size(96, 34)
        };

        browseButton.Click += (_, _) =>
        {
            using var folderDialog = new FolderBrowserDialog
            {
                Description = "选择抽号机安装位置",
                SelectedPath = GetFolderBrowserInitialPath(installPathBox.Text)
            };

            if (folderDialog.ShowDialog(dialog) == DialogResult.OK)
            {
                installPathBox.Text = folderDialog.SelectedPath;
            }
        };

        installButton.Click += (_, _) =>
        {
            if (!TryNormalizeInstallDirectory(installPathBox.Text, out var normalizedPath, out var error))
            {
                MessageBox.Show(
                    dialog,
                    error,
                    $"{AppName} 安装位置",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                installPathBox.Focus();
                installPathBox.SelectAll();
                return;
            }

            dialog.Tag = new InstallOptions(
                normalizedPath,
                desktopShortcutBox.Checked,
                startMenuShortcutBox.Checked,
                startupBox.Checked,
                launchBox.Checked);
            dialog.DialogResult = DialogResult.OK;
            dialog.Close();
        };

        dialog.Controls.AddRange(new Control[]
        {
            title,
            subtitle,
            locationLabel,
            installPathBox,
            browseButton,
            hint,
            optionsGroup,
            installButton,
            cancelButton
        });
        dialog.AcceptButton = installButton;
        dialog.CancelButton = cancelButton;

        return dialog.ShowDialog() == DialogResult.OK && dialog.Tag is InstallOptions options
            ? options
            : null;
    }

    private static InstallResult? Install(InstallOptions options)
    {
        Directory.CreateDirectory(options.InstallDirectory);

        var targetExe = Path.Combine(options.InstallDirectory, MainExeName);
        if (!CloseRunningAppIfNeeded(targetExe))
        {
            return null;
        }

        ExtractPayload(targetExe);
        var shortcutResults = ConfigureShortcuts(targetExe, options);
        var startupResult = ConfigureStartup(targetExe, options.EnableStartup);
        var saveInstallDirectoryResult = SaveInstallDirectory(options.InstallDirectory);

        var launchStarted = false;
        var launchError = string.Empty;
        if (options.LaunchAfterInstall)
        {
            launchStarted = TryStart(targetExe, options.InstallDirectory, out launchError);
        }

        return new InstallResult(
            options,
            targetExe,
            shortcutResults,
            startupResult,
            saveInstallDirectoryResult,
            launchStarted,
            launchError);
    }

    private static string GetFolderBrowserInitialPath(string currentText)
    {
        if (TryNormalizeInstallDirectory(currentText, out var normalizedPath, out _))
        {
            var parent = Directory.Exists(normalizedPath)
                ? normalizedPath
                : Path.GetDirectoryName(normalizedPath);
            if (!string.IsNullOrWhiteSpace(parent) && Directory.Exists(parent))
            {
                return parent;
            }
        }

        return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    }

    private static bool TryNormalizeInstallDirectory(string text, out string normalizedPath, out string error)
    {
        normalizedPath = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(text))
        {
            error = "请选择安装位置。";
            return false;
        }

        try
        {
            var expanded = Environment.ExpandEnvironmentVariables(text.Trim().Trim('"'));
            if (expanded.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            {
                error = "安装位置包含 Windows 不支持的字符。";
                return false;
            }

            var fullPath = Path.GetFullPath(expanded);
            var root = Path.GetPathRoot(fullPath);
            var trimmedFullPath = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var trimmedRoot = root?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.IsNullOrWhiteSpace(trimmedFullPath)
                || string.Equals(trimmedFullPath, trimmedRoot, StringComparison.OrdinalIgnoreCase))
            {
                error = "请选择一个具体文件夹，不要直接安装到磁盘根目录。";
                return false;
            }

            normalizedPath = trimmedFullPath;
            return true;
        }
        catch (Exception ex)
        {
            error = $"安装位置无效：{ex.Message}";
            return false;
        }
    }

    private static bool CloseRunningAppIfNeeded(string targetExe)
    {
        var runningProcesses = FindRunningAppProcesses(targetExe).ToList();
        if (runningProcesses.Count == 0)
        {
            return true;
        }

        var result = MessageBox.Show(
            "检测到抽号机正在运行。\n\n安装器需要先关闭正在运行的程序，才能无损覆盖升级主程序文件。\n是否关闭并继续安装？",
            $"{AppName} 正在运行",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result != DialogResult.Yes)
        {
            return false;
        }

        foreach (var process in runningProcesses)
        {
            try
            {
                if (!process.HasExited && process.CloseMainWindow())
                {
                    process.WaitForExit(5000);
                }

                if (!process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit(3000);
                }
            }
            catch
            {
                // Continue; any remaining file lock is reported by the copy step.
            }
            finally
            {
                process.Dispose();
            }
        }

        return true;
    }

    private static IEnumerable<Process> FindRunningAppProcesses(string targetExe)
    {
        var expectedPath = Path.GetFullPath(targetExe);
        var processName = Path.GetFileNameWithoutExtension(targetExe);
        foreach (var process in Process.GetProcessesByName(processName))
        {
            string? processPath = null;
            try
            {
                processPath = process.MainModule?.FileName;
            }
            catch
            {
                process.Dispose();
                continue;
            }

            if (string.Equals(Path.GetFullPath(processPath ?? string.Empty), expectedPath, StringComparison.OrdinalIgnoreCase))
            {
                yield return process;
            }
            else
            {
                process.Dispose();
            }
        }
    }

    private static void ExtractPayload(string targetExe)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var tempExe = $"{targetExe}.installing";

        try
        {
            if (File.Exists(tempExe))
            {
                File.Delete(tempExe);
            }

            using var payload = assembly.GetManifestResourceStream("DrawMachine.exe")
                ?? throw new InvalidOperationException("安装包缺少主程序资源。");
            using (var output = File.Create(tempExe))
            {
                payload.CopyTo(output);
                output.Flush();
            }

            File.Move(tempExe, targetExe, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempExe))
            {
                try
                {
                    File.Delete(tempExe);
                }
                catch
                {
                    // A leftover temp file does not block the installed app.
                }
            }
        }
    }

    private static IReadOnlyList<ShortcutResult> ConfigureShortcuts(string targetExe, InstallOptions options)
    {
        var results = new List<ShortcutResult>();
        var desktopShortcut = GetDesktopShortcutPath();
        var startMenuShortcut = GetStartMenuShortcutPath();

        TryApplyShortcut("桌面快捷方式", desktopShortcut, targetExe, options.InstallDirectory, options.CreateDesktopShortcut, results);
        TryApplyShortcut("开始菜单快捷方式", startMenuShortcut, targetExe, options.InstallDirectory, options.CreateStartMenuShortcut, results);

        return results;
    }

    private static void TryApplyShortcut(
        string name,
        string shortcutPath,
        string targetExe,
        string installDir,
        bool shouldCreate,
        ICollection<ShortcutResult> results)
    {
        try
        {
            if (shouldCreate)
            {
                var shortcutDir = Path.GetDirectoryName(shortcutPath);
                if (!string.IsNullOrWhiteSpace(shortcutDir))
                {
                    Directory.CreateDirectory(shortcutDir);
                }

                CreateShortcut(shortcutPath, targetExe, installDir);
                results.Add(new ShortcutResult(name, shortcutPath, true, true, null));
                return;
            }

            DeleteShortcut(shortcutPath);
            results.Add(new ShortcutResult(name, shortcutPath, false, true, null));
        }
        catch (Exception ex)
        {
            results.Add(new ShortcutResult(name, shortcutPath, shouldCreate, false, ex.Message));
        }
    }

    private static OperationResult ConfigureStartup(string targetExe, bool enableStartup)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(StartupRegistryPath, writable: true)
                ?? throw new InvalidOperationException("无法打开开机自启注册表位置。");

            if (enableStartup)
            {
                key.SetValue(StartupRegistryName, BuildStartupCommand(targetExe));
            }
            else
            {
                key.DeleteValue(StartupRegistryName, throwOnMissingValue: false);
            }

            return new OperationResult(true, null);
        }
        catch (Exception ex)
        {
            return new OperationResult(false, ex.Message);
        }
    }

    private static OperationResult SaveInstallDirectory(string installDir)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(InstallerRegistryPath, writable: true)
                ?? throw new InvalidOperationException("无法打开安装器注册表位置。");
            key.SetValue(InstallDirectoryRegistryValue, installDir);
            return new OperationResult(true, null);
        }
        catch (Exception ex)
        {
            return new OperationResult(false, ex.Message);
        }
    }

    private static string BuildStartupCommand(string targetExe)
    {
        return $"\"{targetExe}\" --silent-startup";
    }

    private static bool IsStartupRegistrationEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryPath, writable: false);
        var value = key?.GetValue(StartupRegistryName)?.ToString();
        return !string.IsNullOrWhiteSpace(value);
    }

    private static void ShowCompletionDialog(InstallResult result)
    {
        using var dialog = new Form
        {
            Text = $"{AppName} 安装完成",
            StartPosition = FormStartPosition.CenterScreen,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowIcon = false,
            ShowInTaskbar = true,
            ClientSize = new Size(560, 300),
            Font = SystemFonts.MessageBoxFont
        };

        var title = new Label
        {
            Text = "安装完成",
            AutoSize = false,
            Location = new Point(24, 20),
            Size = new Size(500, 32),
            Font = new Font(SystemFonts.MessageBoxFont!, FontStyle.Bold)
        };

        var completionText = BuildCompletionText(result);
        var details = new TextBox
        {
            Text = completionText,
            AccessibleName = completionText,
            AccessibleDescription = completionText,
            Location = new Point(24, 64),
            Size = new Size(512, 146),
            BorderStyle = BorderStyle.None,
            Multiline = true,
            ReadOnly = true,
            TabStop = false,
            BackColor = dialog.BackColor,
            ScrollBars = ScrollBars.Vertical
        };

        var launchButton = new Button
        {
            Text = CompletionLaunchButtonText,
            Location = new Point(160, 238),
            Size = new Size(112, 34)
        };
        var openFolderButton = new Button
        {
            Text = CompletionOpenFolderButtonText,
            Location = new Point(284, 238),
            Size = new Size(124, 34)
        };
        var finishButton = new Button
        {
            Text = CompletionFinishButtonText,
            DialogResult = DialogResult.Cancel,
            Location = new Point(420, 238),
            Size = new Size(96, 34)
        };

        launchButton.Click += (_, _) =>
        {
            if (TryStart(result.TargetExePath, result.Options.InstallDirectory, out var error))
            {
                dialog.DialogResult = DialogResult.OK;
                dialog.Close();
                return;
            }

            MessageBox.Show(
                dialog,
                $"启动失败：{error}\n\n可以点击“{CompletionOpenFolderButtonText}”，手动运行 {MainExeName}。",
                $"{AppName} 启动失败",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        };

        openFolderButton.Click += (_, _) =>
        {
            if (!TryOpenFolder(result.Options.InstallDirectory, out var error))
            {
                MessageBox.Show(
                    dialog,
                    $"打开安装位置失败：{error}\n\n安装位置：{result.Options.InstallDirectory}",
                    $"{AppName} 安装位置",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        };

        dialog.Controls.AddRange(new Control[] { title, details, launchButton, openFolderButton, finishButton });
        dialog.AcceptButton = launchButton;
        dialog.CancelButton = finishButton;

        dialog.ShowDialog();
    }

    private static string BuildCompletionText(InstallResult result)
    {
        var text = new StringBuilder();
        text.AppendLine("抽号机已安装到：");
        text.AppendLine(result.Options.InstallDirectory);
        text.AppendLine();

        var createdShortcuts = result.ShortcutResults
            .Where(shortcut => shortcut.Requested && shortcut.Applied)
            .ToList();
        if (createdShortcuts.Count > 0)
        {
            text.AppendLine("已创建：");
            foreach (var shortcut in createdShortcuts)
            {
                text.AppendLine($"- {shortcut.Name}");
            }

            text.AppendLine();
        }

        var removedShortcuts = result.ShortcutResults
            .Where(shortcut => !shortcut.Requested && shortcut.Applied)
            .ToList();
        if (removedShortcuts.Count > 0)
        {
            text.AppendLine("未勾选的快捷方式已保持关闭：");
            foreach (var shortcut in removedShortcuts)
            {
                text.AppendLine($"- {shortcut.Name}");
            }

            text.AppendLine();
        }

        text.AppendLine(result.Options.EnableStartup
            ? (result.StartupResult.Ok ? "开机自启已启用，启动时会静默进入托盘。" : $"开机自启设置失败：{result.StartupResult.Error}")
            : (result.StartupResult.Ok ? "开机自启未启用。" : $"关闭开机自启失败：{result.StartupResult.Error}"));
        text.AppendLine();

        if (result.Options.LaunchAfterInstall)
        {
            text.AppendLine(result.LaunchStarted
                ? "抽号机已启动。"
                : $"抽号机启动失败：{result.LaunchError}");
            text.AppendLine();
        }

        if (!result.SaveInstallDirectoryResult.Ok)
        {
            text.AppendLine($"安装位置已生效，但保存升级默认位置失败：{result.SaveInstallDirectoryResult.Error}");
            text.AppendLine();
        }

        var failedShortcuts = result.ShortcutResults
            .Where(shortcut => !shortcut.Applied)
            .ToList();
        if (failedShortcuts.Count == 0)
        {
            text.AppendLine($"可以稍后从快捷方式打开，或点击“{CompletionOpenFolderButtonText}”查看安装位置。");
            return text.ToString();
        }

        text.AppendLine("以下安装选项未完成：");
        foreach (var shortcut in failedShortcuts)
        {
            var action = shortcut.Requested ? "创建" : "移除";
            text.AppendLine($"- {action}{shortcut.Name}：{shortcut.Error}");
        }

        text.AppendLine();
        text.AppendLine($"主程序已安装成功，仍可点击“{CompletionLaunchButtonText}”，或打开安装位置后运行 {MainExeName}。");
        return text.ToString();
    }

    private static string GetInstallDirectory()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(InstallerRegistryPath, writable: false);
            var savedPath = key?.GetValue(InstallDirectoryRegistryValue)?.ToString();
            if (TryNormalizeInstallDirectory(savedPath ?? string.Empty, out var normalizedPath, out _))
            {
                return normalizedPath;
            }
        }
        catch
        {
            // Fall back to the per-user default when the installer registry key is unavailable.
        }

        return GetDefaultInstallDirectory();
    }

    private static string GetDefaultInstallDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DrawMachineDesktop");
    }

    private static string GetDesktopShortcutPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            $"{AppName}.lnk");
    }

    private static string GetStartMenuShortcutPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            AppName,
            $"{AppName}.lnk");
    }

    private static bool TryStart(string targetExe, string installDir, out string error)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = targetExe,
                WorkingDirectory = installDir,
                UseShellExecute = true
            });
            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool TryOpenFolder(string installDir, out string error)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = installDir,
                UseShellExecute = true
            });
            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static void CreateShortcut(string shortcutPath, string targetExe, string installDir)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("无法创建快捷方式：WScript.Shell 不可用。");
        var shell = Activator.CreateInstance(shellType)
            ?? throw new InvalidOperationException("无法创建快捷方式：Shell 初始化失败。");

        object? shortcut = null;
        try
        {
            shortcut = shellType.InvokeMember(
                "CreateShortcut",
                BindingFlags.InvokeMethod,
                null,
                shell,
                new object[] { shortcutPath });

            dynamic shortcutObject = shortcut!;
            shortcutObject.TargetPath = targetExe;
            shortcutObject.WorkingDirectory = installDir;
            shortcutObject.IconLocation = targetExe;
            shortcutObject.Save();
        }
        finally
        {
            if (shortcut != null)
            {
                Marshal.FinalReleaseComObject(shortcut);
            }

            Marshal.FinalReleaseComObject(shell);
        }
    }

    private static void DeleteShortcut(string shortcutPath)
    {
        if (!File.Exists(shortcutPath))
        {
            return;
        }

        File.Delete(shortcutPath);

        var shortcutDir = Path.GetDirectoryName(shortcutPath);
        if (!string.IsNullOrWhiteSpace(shortcutDir)
            && Directory.Exists(shortcutDir)
            && !Directory.EnumerateFileSystemEntries(shortcutDir).Any()
            && string.Equals(Path.GetFileName(shortcutDir), AppName, StringComparison.OrdinalIgnoreCase))
        {
            Directory.Delete(shortcutDir);
        }
    }

    private sealed class InstallerSelfCheckResult
    {
        public bool Ok { get; set; }
        public string Error { get; set; } = string.Empty;
        public string InstallerFileVersion { get; set; } = string.Empty;
        public string InstallerProductVersion { get; set; } = string.Empty;
        public string PayloadFileVersion { get; set; } = string.Empty;
        public string PayloadProductVersion { get; set; } = string.Empty;
        public long PayloadSizeBytes { get; set; }
        public string PayloadSha256 { get; set; } = string.Empty;
        public string InstallDirectory { get; set; } = string.Empty;
        public string MainExeName { get; set; } = string.Empty;
        public string TargetExePath { get; set; } = string.Empty;
        public string DesktopShortcutPath { get; set; } = string.Empty;
        public string StartMenuShortcutPath { get; set; } = string.Empty;
        public bool SupportsCustomInstallDirectory { get; set; }
        public bool SupportsDesktopShortcutChoice { get; set; }
        public bool SupportsStartMenuShortcutChoice { get; set; }
        public bool SupportsStartupChoice { get; set; }
        public bool SupportsLaunchAfterInstallChoice { get; set; }
        public bool InstallerOptionsSelfCheckOk { get; set; }
        public string InstallerOptionTexts { get; set; } = string.Empty;
        public string DefaultInstallDirectory { get; set; } = string.Empty;
        public string StartupRegistryName { get; set; } = string.Empty;
        public string StartupRegistryPath { get; set; } = string.Empty;
        public string StartupCommandSample { get; set; } = string.Empty;
        public string CompletionLaunchButtonText { get; set; } = string.Empty;
        public string CompletionFinishButtonText { get; set; } = string.Empty;
        public string CompletionDefaultAction { get; set; } = string.Empty;
    }

    private sealed record InstallOptions(
        string InstallDirectory,
        bool CreateDesktopShortcut,
        bool CreateStartMenuShortcut,
        bool EnableStartup,
        bool LaunchAfterInstall);

    private sealed record InstallResult(
        InstallOptions Options,
        string TargetExePath,
        IReadOnlyList<ShortcutResult> ShortcutResults,
        OperationResult StartupResult,
        OperationResult SaveInstallDirectoryResult,
        bool LaunchStarted,
        string LaunchError);

    private sealed record OperationResult(bool Ok, string? Error);

    private sealed record ShortcutResult(string Name, string Path, bool Requested, bool Applied, string? Error);
}
