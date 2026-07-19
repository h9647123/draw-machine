using System.Text.Json;
using System.Text.Json.Serialization;

namespace DrawMachineDesktop;

internal sealed class AppState
{
    public List<string> Names { get; set; } = new();
    public List<StudentRecord> Students { get; set; } = new();
    public string FileName { get; set; } = "未加载";
    public string SourceFilePath { get; set; } = string.Empty;
    public List<string> RecentRosterFiles { get; set; } = new();
    public List<StudentRecord> PreviousStudents { get; set; } = new();
    public string PreviousFileName { get; set; } = string.Empty;
    public string PreviousSourceFilePath { get; set; } = string.Empty;
    public string LastWinner { get; set; } = string.Empty;
    public List<string> DrawHistory { get; set; } = new();
    public float ResultFontSize { get; set; } = 48f;
    public double DrawDurationSeconds { get; set; } = 5.0;
    public int FloatingShade { get; set; } = 35;
    public bool AlwaysOnTop { get; set; }
    public bool SilentStartup { get; set; }
    public bool StartGuideShown { get; set; }
    public bool AvoidRepeatDraw { get; set; } = true;
    public bool AutoCopyResult { get; set; }
    public List<string> ExcludedStudentKeys { get; set; } = new();
    public List<string> DrawnStudentKeys { get; set; } = new();
    public List<string> DrawnGroupKeys { get; set; } = new();
    public string LastDrawUndoKind { get; set; } = string.Empty;
    public string LastDrawUndoKey { get; set; } = string.Empty;
    public string LastDrawUndoHistoryEntry { get; set; } = string.Empty;
    public string LastDrawReplayMode { get; set; } = string.Empty;
    public string LastLessonPackageDirectory { get; set; } = string.Empty;
    public string LastExportDirectory { get; set; } = string.Empty;

    [JsonIgnore]
    public string RecoveryNotice { get; private set; } = string.Empty;

    public static event Action<string, string>? SaveFailed;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static string GetStoragePath()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DrawMachineDesktop");

        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "state.json");
    }

    public static AppState Load()
    {
        return LoadFromPath(GetStoragePath());
    }

    internal static AppState LoadFromPathForSelfCheck(string path)
    {
        return LoadFromPath(path);
    }

    private static AppState LoadFromPath(string path)
    {
        var backupPath = GetBackupPath(path);
        if (!File.Exists(path))
        {
            if (File.Exists(backupPath) && TryReadState(backupPath, out var backupState))
            {
                TryRestoreStateFile(backupPath, path);
                backupState.RecoveryNotice = "未找到配置文件，已从自动备份恢复名单和设置。";
                return backupState;
            }

            return new AppState();
        }

        if (TryReadState(path, out var state))
        {
            return state;
        }

        var preservedPath = TryMoveAsideDamagedState(path);
        if (File.Exists(backupPath) && TryReadState(backupPath, out var restoredState))
        {
            TryRestoreStateFile(backupPath, path);
            restoredState.RecoveryNotice = string.IsNullOrWhiteSpace(preservedPath)
                ? "配置文件损坏，已从自动备份恢复名单和设置。"
                : $"配置文件损坏，已从自动备份恢复名单和设置。\n损坏文件已保留：{preservedPath}";
            return restoredState;
        }

        var cleanState = new AppState();
        cleanState.RecoveryNotice = string.IsNullOrWhiteSpace(preservedPath)
            ? "配置文件损坏，且没有可用备份。已使用空白配置启动，可重新导入名单。"
            : $"配置文件损坏，且没有可用备份。已使用空白配置启动，可重新导入名单。\n损坏文件已保留：{preservedPath}";
        return cleanState;
    }

    public void Save()
    {
        SaveToPath(GetStoragePath());
    }

    internal void SaveToPathForSelfCheck(string path)
    {
        SaveToPath(path);
    }

    private void SaveToPath(string path)
    {
        var tempPath = string.Empty;

        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(this, JsonOptions);
            tempPath = Path.Combine(
                directory ?? string.Empty,
                $"state-{Guid.NewGuid():N}.tmp");

            File.WriteAllText(tempPath, json);
            if (File.Exists(path))
            {
                var backupPath = GetBackupPath(path);
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }

                File.Replace(tempPath, path, backupPath, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempPath, path);
            }
        }
        catch (Exception ex)
        {
            NotifySaveFailed(path, ex);
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(tempPath) && File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch
                {
                    // A stale temp file is harmless; the next save uses a new name.
                }
            }
        }
    }

    private static void NotifySaveFailed(string path, Exception exception)
    {
        try
        {
            SaveFailed?.Invoke(path, exception.Message);
        }
        catch
        {
            // Save failure reporting must never crash normal app actions.
        }
    }

    private static string GetBackupPath(string path)
    {
        return path + ".bak";
    }

    private static bool TryReadState(string path, out AppState state)
    {
        try
        {
            var json = File.ReadAllText(path);
            state = Normalize(JsonSerializer.Deserialize<AppState>(json, JsonOptions) ?? new AppState());
            return true;
        }
        catch
        {
            state = new AppState();
            return false;
        }
    }

    private static string TryMoveAsideDamagedState(string path)
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directory))
            {
                return string.Empty;
            }

            var targetPath = GetUniqueDamagedStatePath(directory);
            File.Move(path, targetPath);
            return targetPath;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string GetUniqueDamagedStatePath(string directory)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        for (var index = 0; index < 100; index++)
        {
            var suffix = index == 0 ? string.Empty : $"-{index}";
            var path = Path.Combine(directory, $"state-damaged-{timestamp}{suffix}.json");
            if (!File.Exists(path))
            {
                return path;
            }
        }

        return Path.Combine(directory, $"state-damaged-{timestamp}-{Guid.NewGuid():N}.json");
    }

    private static void TryRestoreStateFile(string backupPath, string targetPath)
    {
        try
        {
            File.Copy(backupPath, targetPath, overwrite: true);
        }
        catch
        {
            // The in-memory restored state still lets the app start and save later.
        }
    }

    private static AppState Normalize(AppState state)
    {
        state.Names ??= new();
        state.Students ??= new();
        state.RecentRosterFiles ??= new();
        state.PreviousStudents ??= new();
        state.DrawHistory ??= new();
        state.ExcludedStudentKeys ??= new();
        state.DrawnStudentKeys ??= new();
        state.DrawnGroupKeys ??= new();
        state.FileName ??= "未加载";
        state.SourceFilePath ??= string.Empty;
        state.PreviousFileName ??= string.Empty;
        state.PreviousSourceFilePath ??= string.Empty;
        state.LastWinner ??= string.Empty;
        state.LastDrawUndoKind ??= string.Empty;
        state.LastDrawUndoKey ??= string.Empty;
        state.LastDrawUndoHistoryEntry ??= string.Empty;
        state.LastDrawReplayMode ??= string.Empty;
        state.LastLessonPackageDirectory ??= string.Empty;
        state.LastExportDirectory ??= string.Empty;
        return state;
    }
}

internal sealed class StudentRecord
{
    public string Sequence { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;

    public string DisplayName => string.IsNullOrWhiteSpace(Sequence)
        ? Name
        : $"{Sequence} {Name}";
}
