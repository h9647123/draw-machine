using System.Text.Json;

namespace DrawMachineDesktop;

internal static class Program
{
    private const string SingleInstanceMutexName = @"Local\DrawMachineDesktop.SingleInstance";
    private const string ShowMainEventName = @"Local\DrawMachineDesktop.ShowMain";
    private const string StartupSelfCheckArgument = "--startup-self-check";
    private const string SelfCheckOutputArgument = "--self-check-output";

    [STAThread]
    static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (HasArgument(args, StartupSelfCheckArgument))
        {
            return RunStartupSelfCheck(args);
        }

        var startupSilent = args.Any(arg => string.Equals(arg, "--silent-startup", StringComparison.OrdinalIgnoreCase));
        using var mutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            SignalExistingInstance();
            return 0;
        }

        using var showMainEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowMainEventName);
        Application.Run(new MainForm(startupSilent, showMainEvent));
        return 0;
    }

    private static int RunStartupSelfCheck(string[] args)
    {
        var outputPath = GetArgumentValue(args, SelfCheckOutputArgument);
        try
        {
            using var form = new MainForm(enableTrayIcon: false);
            var result = form.RunHeadlessStartupSelfCheck();
            WriteSelfCheckResult(outputPath, result);
            return 0;
        }
        catch (Exception ex)
        {
            WriteSelfCheckResult(outputPath, new StartupSelfCheckResult
            {
                Ok = false,
                Version = string.Empty,
                BuildDate = string.Empty,
                Error = ex.ToString()
            });
            return 2;
        }
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

    private static void WriteSelfCheckResult(string outputPath, StartupSelfCheckResult result)
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

    private static void SignalExistingInstance()
    {
        try
        {
            using var showMainEvent = EventWaitHandle.OpenExisting(ShowMainEventName);
            showMainEvent.Set();
        }
        catch
        {
            MessageBox.Show(
                "抽号机已在运行，可在右下角隐藏图标中打开。",
                "抽号机",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }
}
