using System.Drawing.Drawing2D;
using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Win32;

namespace DrawMachineDesktop;

internal sealed class MainForm : Form
{
    private const float MinFontSize = 24f;
    private const float MaxFontSize = 210f;
    private const float MinAutoFitFontSize = 12f;
    private const float PresentationPreferredFontSize = 156f;
    private const int FloatingDockWidth = 328;
    private const int FloatingCollapsedWidth = 72;
    private const int FloatingDockHeight = 220;
    private const int FloatingToolbarHeight = 58;
    private const int FloatingStatusTop = 70;
    private const int FloatingGenderTopOffset = FloatingIconSize + 18;
    private const int FloatingStatusMinHeight = 18;
    private const int FloatingStatusMaxHeight = 132;
    private const int FloatingIconSize = 46;
    private const int FloatingLogoSize = 58;
    private const float FloatingScaleMax = 1.38f;
    private const int NormalCardMinimumWidth = 720;
    private const int NormalCardMinimumHeight = 760;
    private const int NormalHostPadding = 24;
    private const string StartupRegistryName = "DrawMachineDesktop";
    private const int MaxRecentRosterFiles = 8;
    private const int StartupRevealWarmupPasses = 8;
    private const int StartupRevealMaxWaitMs = 1400;
    private const int MainCountdownMinimumHeight = 104;
    private const string AppVersion = "v175";
    private const string AppBuildDate = "2026-08-25";
    private const string UpdateRepository = "h9647123/draw-machine";
    private const string UpdateApiPath = "https://api.github.com/repos/h9647123/draw-machine/releases/latest";
    private static readonly string[] UpdateApiSources =
    {
        UpdateApiPath,
        $"https://ghfast.top/{UpdateApiPath}",
        $"https://gh-proxy.com/{UpdateApiPath}",
        $"https://ghproxy.net/{UpdateApiPath}"
    };
    private const string CoreDrawActionText = "开始抽号";
    private const string DemoDrawActionText = "试用抽号";
    private const string DrawingActionText = "抽号中...";
    private const string EmptyRosterResultText = "粘贴名单或导入文件\n点试用抽号先体验";
    private const string FirstUseHint = "首次使用：先点“粘贴名单”；已有 Excel/TXT 点“导入文件”。想先试流程，点“试用抽号”。";
    private const string NewLessonMenuPath = "设置 > 抽号 > 新一节课";
    private const string AbsenceMenuPath = "设置 > 名单 > 本节缺席";
    private const string ImportSuccessPrimaryActionText = "开始抽号";
    private const string ImportSuccessAbsenceActionText = "设置本节缺席";
    private const string ImportSuccessDismissActionText = "稍后开始";

    private enum OverviewStatusFilter
    {
        All,
        Present,
        Absent,
        Undrawn,
        Drawn
    }

    private enum ImportSuccessAction
    {
        None,
        EditAbsences,
        StartDraw
    }

    private enum ImportRecoveryAction
    {
        None,
        PasteRoster,
        SaveTemplate,
        ImportFile
    }

    private static readonly Color FloatingTransparentKey = Color.FromArgb(255, 1, 2, 3);
    private readonly AppState _state = AppState.Load();
    private readonly Random _random = new();

    private readonly BufferedPanel _normalHost = new();
    private readonly BufferedPanel _cardPanel = new();
    private readonly BufferedPanel _resultPanel = new();
    private readonly BufferedPanel _floatingHost = new();
    private readonly BufferedPanel _floatingDrawSurface = new();

    private readonly SmoothLabel _fileNameValue = new();
    private readonly SmoothLabel _countdownValue = new();
    private readonly SmoothLabel _roundProgressValue = new();
    private readonly SmoothLabel _resultLabel = new();
    private readonly TextBox _lastWinnerValue = new();
    private readonly SmoothLabel _fontSizeValue = new();
    private readonly SmoothLabel _hintLabel = new();
    private readonly FloatingIconButton _floatingTitleLabel = new();
    private readonly SmoothLabel _floatingResultLabel = new();
    private readonly SmoothLabel _floatingCountdownLabel = new();

    private readonly Button _drawButton = new RoundedButton();
    private readonly Button _drawMaleButton = new RoundedButton();
    private readonly Button _drawFemaleButton = new RoundedButton();
    private readonly Button _drawGroupButton = new RoundedButton();
    private readonly Button _startGuideButton = new RoundedButton();
    private readonly Button _quickDemoDrawButton = new RoundedButton();
    private readonly Button _loadListButton = new RoundedButton();
    private readonly Button _reloadFileButton = new RoundedButton();
    private readonly Button _openSourceFileButton = new RoundedButton();
    private readonly Button _recentRosterButton = new RoundedButton();
    private readonly Button _pasteListButton = new RoundedButton();
    private readonly Button _demoListButton = new RoundedButton();
    private readonly Button _restoreRosterButton = new RoundedButton();
    private readonly Button _listOverviewButton = new RoundedButton();
    private readonly Button _absenceButton = new RoundedButton();
    private readonly Button _presentationModeButton = new RoundedButton();
    private readonly Button _exampleButton = new RoundedButton();
    private readonly Button _floatingModeButton = new RoundedButton();
    private readonly Button _startupButton = new RoundedButton();
    private readonly Button _silentStartupButton = new RoundedButton();
    private readonly Button _topMostButton = new RoundedButton();
    private readonly Button _avoidRepeatButton = new RoundedButton();
    private readonly Button _autoCopyButton = new RoundedButton();
    private readonly Button _resetRoundButton = new RoundedButton();
    private readonly Button _undoDrawButton = new RoundedButton();
    private readonly Button _redrawButton = new RoundedButton();
    private readonly Button _aboutButton = new();
    private readonly Button _quickGuideButton = new();
    private readonly FloatingIconButton _floatingImportButton = new();
    private readonly FloatingIconButton _floatingMaleButton = new();
    private readonly FloatingIconButton _floatingFemaleButton = new();
    private readonly Button _increaseFontButton = new();
    private readonly Button _decreaseFontButton = new();
    private readonly Button _copyResultButton = new RoundedButton();
    private readonly Button _copyLessonSummaryButton = new RoundedButton();
    private readonly Button _exportLessonSummaryButton = new RoundedButton();
    private readonly Button _copyRosterButton = new RoundedButton();
    private readonly Button _exportRosterButton = new RoundedButton();
    private readonly Button _viewHistoryButton = new RoundedButton();
    private readonly Button _exportHistoryButton = new RoundedButton();
    private readonly Button _exportLessonPackageButton = new RoundedButton();
    private readonly Button _clearHistoryButton = new RoundedButton();
    private readonly Button _newLessonButton = new RoundedButton();
    private readonly Button _moreActionsButton = new RoundedButton();
    private readonly FloatingIconButton _floatingRestoreButton = new();
    private readonly FloatingIconButton _floatingExitButton = new();

    private readonly NumericUpDown _drawDurationInput = new();
    private readonly Button _duration3Button = new RoundedButton();
    private readonly Button _duration5Button = new RoundedButton();
    private readonly Button _duration8Button = new RoundedButton();
    private readonly TrackBar _floatingShadeInput = new();
    private readonly SmoothLabel _floatingShadeValue = new();

    private readonly ContextMenuStrip _floatingMenu = new();
    private readonly ContextMenuStrip _trayMenu = new();
    private readonly ContextMenuStrip _genderMenu = new();
    private readonly ContextMenuStrip _recentRosterMenu = new();
    private readonly ContextMenuStrip _mainMoreMenu = new();
    private readonly NotifyIcon _trayIcon = new();
    private readonly ToolStripMenuItem _restoreMenuItem = new("恢复主界面");
    private readonly ToolStripMenuItem _floatingStartGuideMenuItem = new("上课向导");
    private readonly ToolStripMenuItem _floatingDrawMenuItem = new("开始抽号");
    private readonly ToolStripMenuItem _floatingDrawMaleMenuItem = new("抽男生");
    private readonly ToolStripMenuItem _floatingDrawFemaleMenuItem = new("抽女生");
    private readonly ToolStripMenuItem _floatingDrawGroupMenuItem = new("抽小组");
    private readonly ToolStripMenuItem _floatingUndoDrawMenuItem = new("撤销上次");
    private readonly ToolStripMenuItem _floatingRedrawMenuItem = new("重抽一次");
    private readonly ToolStripMenuItem _floatingDurationMenuItem = new("抽号时长");
    private readonly ToolStripMenuItem _floatingDuration3MenuItem = new("3秒");
    private readonly ToolStripMenuItem _floatingDuration5MenuItem = new("5秒");
    private readonly ToolStripMenuItem _floatingDuration8MenuItem = new("8秒");
    private readonly ToolStripMenuItem _floatingCopyResultMenuItem = new("复制结果");
    private readonly ToolStripMenuItem _floatingCopyLessonSummaryMenuItem = new("复制课堂摘要");
    private readonly ToolStripMenuItem _floatingExportLessonSummaryMenuItem = new("导出课堂摘要");
    private readonly ToolStripMenuItem _floatingExportLessonPackageMenuItem = new("导出课堂包");
    private readonly ToolStripMenuItem _floatingOpenLessonPackageDirectoryMenuItem = new("打开课堂包位置");
    private readonly ToolStripMenuItem _floatingAutoCopyMenuItem = new("自动复制结果：关");
    private readonly ToolStripMenuItem _floatingAvoidRepeatMenuItem = new("避免重复：关");
    private readonly ToolStripMenuItem _floatingResetRoundMenuItem = new("重置本轮");
    private readonly ToolStripMenuItem _floatingNewLessonMenuItem = new("新一节课");
    private readonly ToolStripMenuItem _floatingViewHistoryMenuItem = new("查看历史");
    private readonly ToolStripMenuItem _floatingExportHistoryMenuItem = new("导出历史");
    private readonly ToolStripMenuItem _floatingClearHistoryMenuItem = new("清空历史");
    private readonly ToolStripMenuItem _floatingListOverviewMenuItem = new("名单概览");
    private readonly ToolStripMenuItem _floatingUndrawnOverviewMenuItem = new("未抽名单");
    private readonly ToolStripMenuItem _floatingCopyUndrawnRosterMenuItem = new("复制未抽名单");
    private readonly ToolStripMenuItem _floatingExportUndrawnRosterMenuItem = new("导出未抽名单");
    private readonly ToolStripMenuItem _floatingCopyDrawnRosterMenuItem = new("复制已抽名单");
    private readonly ToolStripMenuItem _floatingExportDrawnRosterMenuItem = new("导出已抽名单");
    private readonly ToolStripMenuItem _floatingAbsenceMenuItem = new("本节缺席");
    private readonly ToolStripMenuItem _floatingCopyAbsentRosterMenuItem = new("复制缺席名单");
    private readonly ToolStripMenuItem _floatingExportAbsentRosterMenuItem = new("导出缺席名单");
    private readonly ToolStripMenuItem _floatingCopyPresentRosterMenuItem = new("复制在场名单");
    private readonly ToolStripMenuItem _floatingExportPresentRosterMenuItem = new("导出在场名单");
    private readonly ToolStripMenuItem _floatingCopyRosterMenuItem = new("复制名单");
    private readonly ToolStripMenuItem _floatingExportRosterMenuItem = new("导出名单");
    private readonly ToolStripMenuItem _floatingPresentationModeMenuItem = new("全屏展示");
    private readonly ToolStripMenuItem _importMenuItem = new("导入文件");
    private readonly ToolStripMenuItem _floatingReloadFileMenuItem = new("重载当前文件");
    private readonly ToolStripMenuItem _floatingOpenSourceFileMenuItem = new("打开当前文件");
    private readonly ToolStripMenuItem _floatingRecentRosterMenuItem = new("最近名单");
    private readonly ToolStripMenuItem _floatingPasteListMenuItem = new("粘贴名单");
    private readonly ToolStripMenuItem _floatingDemoListMenuItem = new("试用名单");
    private readonly ToolStripMenuItem _floatingRestoreRosterMenuItem = new("恢复上次名单");
    private readonly ToolStripMenuItem _floatingQuickGuideMenuItem = new("操作速查");
    private readonly ToolStripMenuItem _exitMenuItem = new("退出程序");
    private readonly ToolStripMenuItem _trayShowMainMenuItem = new("打开主界面");
    private readonly ToolStripMenuItem _trayFloatingMenuItem = new("悬浮球模式");
    private readonly ToolStripMenuItem _trayStartGuideMenuItem = new("上课向导");
    private readonly ToolStripMenuItem _trayDrawMenuItem = new("开始抽号");
    private readonly ToolStripMenuItem _trayDrawMaleMenuItem = new("抽男生");
    private readonly ToolStripMenuItem _trayDrawFemaleMenuItem = new("抽女生");
    private readonly ToolStripMenuItem _trayDrawGroupMenuItem = new("抽小组");
    private readonly ToolStripMenuItem _trayUndoDrawMenuItem = new("撤销上次");
    private readonly ToolStripMenuItem _trayRedrawMenuItem = new("重抽一次");
    private readonly ToolStripMenuItem _trayDurationMenuItem = new("抽号时长");
    private readonly ToolStripMenuItem _trayDuration3MenuItem = new("3秒");
    private readonly ToolStripMenuItem _trayDuration5MenuItem = new("5秒");
    private readonly ToolStripMenuItem _trayDuration8MenuItem = new("8秒");
    private readonly ToolStripMenuItem _trayCopyResultMenuItem = new("复制结果");
    private readonly ToolStripMenuItem _trayCopyLessonSummaryMenuItem = new("复制课堂摘要");
    private readonly ToolStripMenuItem _trayExportLessonSummaryMenuItem = new("导出课堂摘要");
    private readonly ToolStripMenuItem _trayExportLessonPackageMenuItem = new("导出课堂包");
    private readonly ToolStripMenuItem _trayOpenLessonPackageDirectoryMenuItem = new("打开课堂包位置");
    private readonly ToolStripMenuItem _trayAutoCopyMenuItem = new("自动复制结果：关");
    private readonly ToolStripMenuItem _trayAvoidRepeatMenuItem = new("避免重复：关");
    private readonly ToolStripMenuItem _trayResetRoundMenuItem = new("重置本轮");
    private readonly ToolStripMenuItem _trayNewLessonMenuItem = new("新一节课");
    private readonly ToolStripMenuItem _trayViewHistoryMenuItem = new("查看历史");
    private readonly ToolStripMenuItem _trayExportHistoryMenuItem = new("导出历史");
    private readonly ToolStripMenuItem _trayClearHistoryMenuItem = new("清空历史");
    private readonly ToolStripMenuItem _trayListOverviewMenuItem = new("名单概览");
    private readonly ToolStripMenuItem _trayUndrawnOverviewMenuItem = new("未抽名单");
    private readonly ToolStripMenuItem _trayCopyUndrawnRosterMenuItem = new("复制未抽名单");
    private readonly ToolStripMenuItem _trayExportUndrawnRosterMenuItem = new("导出未抽名单");
    private readonly ToolStripMenuItem _trayCopyDrawnRosterMenuItem = new("复制已抽名单");
    private readonly ToolStripMenuItem _trayExportDrawnRosterMenuItem = new("导出已抽名单");
    private readonly ToolStripMenuItem _trayAbsenceMenuItem = new("本节缺席");
    private readonly ToolStripMenuItem _trayCopyAbsentRosterMenuItem = new("复制缺席名单");
    private readonly ToolStripMenuItem _trayExportAbsentRosterMenuItem = new("导出缺席名单");
    private readonly ToolStripMenuItem _trayCopyPresentRosterMenuItem = new("复制在场名单");
    private readonly ToolStripMenuItem _trayExportPresentRosterMenuItem = new("导出在场名单");
    private readonly ToolStripMenuItem _trayCopyRosterMenuItem = new("复制名单");
    private readonly ToolStripMenuItem _trayExportRosterMenuItem = new("导出名单");
    private readonly ToolStripMenuItem _trayPresentationModeMenuItem = new("全屏展示");
    private readonly ToolStripMenuItem _trayImportMenuItem = new("导入文件");
    private readonly ToolStripMenuItem _trayReloadFileMenuItem = new("重载当前文件");
    private readonly ToolStripMenuItem _trayOpenSourceFileMenuItem = new("打开当前文件");
    private readonly ToolStripMenuItem _trayRecentRosterMenuItem = new("最近名单");
    private readonly ToolStripMenuItem _trayPasteListMenuItem = new("粘贴名单");
    private readonly ToolStripMenuItem _trayDemoListMenuItem = new("试用名单");
    private readonly ToolStripMenuItem _trayRestoreRosterMenuItem = new("恢复上次名单");
    private readonly ToolStripMenuItem _trayQuickGuideMenuItem = new("操作速查");
    private readonly ToolStripMenuItem _trayExitMenuItem = new("退出程序");

    private readonly System.Windows.Forms.Timer _countdownTimer = new();
    private readonly System.Windows.Forms.Timer _startupFadeTimer = new();
    private readonly System.Windows.Forms.Timer _rollingTimer = new();
    private readonly System.Windows.Forms.Timer _fontRepeatTimer = new();
    private readonly System.Windows.Forms.Timer _floatingAnimationTimer = new();
    private readonly System.Windows.Forms.Timer _floatingStatusAnimationTimer = new();
    private readonly System.Windows.Forms.Timer _floatingGenderAnimationTimer = new();

    private DrawMode _drawMode = DrawMode.Student;
    private bool _isDrawing;
    private bool _fontIncreaseDirection = true;
    private bool _isFloatingMode;
    private string _currentResultText = string.Empty;
    private DateTime _drawEndTime;
    private double _remainingSeconds;
    private double _totalSeconds;
    private Rectangle _normalBounds;
    private FormBorderStyle _normalFormBorderStyle = FormBorderStyle.Sizable;
    private bool _normalTopMost;
    private Point _floatingDragStartCursor;
    private Point _floatingDragStartLocation;
    private bool _floatingDragActive;
    private bool _floatingDragMoved;
    private bool _floatingIsExpanded = true;
    private bool _floatingAnimationActive;
    private bool _floatingDirectionSwitchPending;
    private bool _floatingDirectionSwitchTargetLeft;
    private bool _floatingActionsVisible = true;
    private bool _floatingGenderMenuVisible;
    private bool _floatingGenderMenuTargetVisible;
    private bool _floatingGenderAnimationActive;
    private bool _floatingReserveGenderSpace;
    private float _floatingToolbarProgress = 1f;
    private float _floatingToolbarTargetProgress = 1f;
    private float _floatingGenderProgress;
    private float _floatingStatusHeight = FloatingStatusMinHeight;
    private float _floatingTargetStatusHeight = FloatingStatusMinHeight;
    private bool _floatingExpandLeft;
    private string _currentDrawReplayMode = "student";
    private bool _isPresentationMode;
    private Rectangle _presentationBounds;
    private FormBorderStyle _presentationFormBorderStyle = FormBorderStyle.Sizable;
    private FormWindowState _presentationWindowState = FormWindowState.Normal;
    private Size _presentationMinimumSize;
    private Size _presentationMaximumSize;
    private bool _startupSilentRequested;
    private bool _normalStartupLayoutReady;
    private bool _startupPresentationCompleted;
    private bool _headlessStartupSelfCheck;
    private bool _showWithoutActivation;
    private int _startupRevealWarmupPass;
    private DateTime _startupRevealStartedAt;
    private bool _exitRequested;
    private bool _trayIconDisposed;
    private bool _trayHideTipShown;
    private DateTime _lastSaveFailureNoticeAt;
    private string _lastSaveFailureMessage = string.Empty;
    private readonly EventWaitHandle? _showMainEvent;
    private readonly CancellationTokenSource _singleInstanceSignalCts = new();
    private List<StudentRecord> _drawPool = new();
    private List<StudentGroup> _groupPool = new();

    public MainForm(bool startupSilentRequested = false, EventWaitHandle? showMainEvent = null, bool enableTrayIcon = true)
    {
        _startupSilentRequested = startupSilentRequested;
        _showMainEvent = showMainEvent;
        AppState.SaveFailed += HandleStateSaveFailed;
        FormClosed += (_, _) =>
        {
            AppState.SaveFailed -= HandleStateSaveFailed;
            _startupFadeTimer.Stop();
            _startupFadeTimer.Dispose();
        };
        Text = "抽号机";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(460, 360);
        ClientSize = new Size(1180, 820);
        AutoScaleMode = AutoScaleMode.Dpi;
        DoubleBuffered = true;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        KeyPreview = true;
        AllowDrop = true;
        Opacity = 0;
        ShowInTaskbar = false;

        _countdownTimer.Interval = 100;
        _countdownTimer.Tick += CountdownTimer_Tick;

        _startupFadeTimer.Interval = 15;
        _startupFadeTimer.Tick += StartupFadeTimer_Tick;

        _rollingTimer.Interval = 140;
        _rollingTimer.Tick += RollingTimer_Tick;

        _fontRepeatTimer.Interval = 100;
        _fontRepeatTimer.Tick += (_, _) => AdjustFontSize(_fontIncreaseDirection);

        _floatingAnimationTimer.Interval = 12;
        _floatingAnimationTimer.Tick += FloatingAnimationTimer_Tick;

        _floatingStatusAnimationTimer.Interval = 12;
        _floatingStatusAnimationTimer.Tick += FloatingStatusAnimationTimer_Tick;

        _floatingGenderAnimationTimer.Interval = 12;
        _floatingGenderAnimationTimer.Tick += FloatingGenderAnimationTimer_Tick;

        Resize += (_, _) =>
        {
            if (_isFloatingMode)
            {
                LayoutFloatingControls();
                UpdateFloatingRegion();
            }
            else
            {
                LayoutNormalCard();
            }

            RefreshResultFonts();
        };

        BuildLayout();
        WireDragImport(this);
        WireDragImportRecursive(_normalHost);
        if (enableTrayIcon)
        {
            BuildTrayIcon();
        }

        ApplyStateToUi();
        RefreshNormalStartupLayout();
        _normalStartupLayoutReady = true;
        Shown += (_, _) => CompleteStartupPresentation();
        if (enableTrayIcon)
        {
            StartSingleInstanceSignalListener();
        }
    }

    internal StartupSelfCheckResult RunHeadlessStartupSelfCheck()
    {
        _headlessStartupSelfCheck = true;
        _showWithoutActivation = true;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        Opacity = 0;
        Bounds = new Rectangle(-32000, -32000, 1180, 820);
        TopMost = false;

        (bool EmptyHeaderOk,
            bool ReadyHeaderOk,
            string EmptyHeaderVisibleButtonTexts,
            string ReadyHeaderVisibleButtonTexts) headerDailyActionSelfCheck;
        (bool Ok,
            int ImportedStudentCount,
            string ReadyHeaderTexts,
            string MainActionText,
            string ResultText,
            string ClassroomStatusText,
            bool SavedStateReadable) firstUseImportWorkflowSelfCheck;
        (bool Ok,
            bool Started,
            bool Completed,
            string Winner,
            int HistoryCount,
            string ButtonText,
            bool ButtonEnabled,
            int DrawnStudentKeyCount,
            string ResultText) coreDrawWorkflowSelfCheck;
        try
        {
            Show();
            Application.DoEvents();
            PrepareNormalSurfaceForReveal();
            Application.DoEvents();
            headerDailyActionSelfCheck = RunHeaderDailyActionSelfCheck();
            firstUseImportWorkflowSelfCheck = RunFirstUseImportWorkflowSelfCheck();
            coreDrawWorkflowSelfCheck = RunCoreDrawWorkflowSelfCheck();
        }
        finally
        {
            Hide();
            _showWithoutActivation = false;
            _headlessStartupSelfCheck = false;
        }

        var startupCriticalControlsReady = AreStartupCriticalControlsReady();
        var startupSurfaceReady = IsNormalStartupSurfaceReady();
        if (!startupSurfaceReady)
        {
            throw new InvalidOperationException("Main startup surface did not finish layout.");
        }

        var countdownSample = FormatMainCountdownText("剩余时间: 5.00s");
        var countdownLineCount = GetMainCountdownLineCount(countdownSample);
        if (countdownLineCount != 2 || _countdownValue.Height < MainCountdownMinimumHeight)
        {
            throw new InvalidOperationException("Main countdown text does not have stable two-line room.");
        }

        var stateRecoverySelfCheck = RunAppStateRecoverySelfCheck();
        var stateSaveSelfCheck = RunAppStateSaveSelfCheck();
        var rosterImportParserSelfCheck = RunRosterImportParserSelfCheck();
        var avoidRepeatRolloverSelfCheck = RunAvoidRepeatRolloverSelfCheck();

        ShowMainMoreMenu();
        Application.DoEvents();
        var mainMoreMenuTopLevelTexts = _mainMoreMenu.Items
            .OfType<ToolStripMenuItem>()
            .Select(item => item.Text)
            .ToArray();
        var rosterMenu = _mainMoreMenu.Items
            .OfType<ToolStripMenuItem>()
            .FirstOrDefault(item => string.Equals(item.Text, "名单", StringComparison.Ordinal));
        var rosterMenuDirectTexts = rosterMenu?.DropDownItems
            .OfType<ToolStripMenuItem>()
            .Select(item => item.Text)
            .ToArray() ?? Array.Empty<string>();
        var prepareRosterMenuDirectTexts = rosterMenu?.DropDownItems
            .OfType<ToolStripMenuItem>()
            .FirstOrDefault(item => string.Equals(item.Text, "准备名单", StringComparison.Ordinal))
            ?.DropDownItems
            .OfType<ToolStripMenuItem>()
            .Select(item => item.Text)
            .ToArray() ?? Array.Empty<string>();
        var rosterFilesMenuDirectTexts = rosterMenu?.DropDownItems
            .OfType<ToolStripMenuItem>()
            .FirstOrDefault(item => string.Equals(item.Text, "文件和导出", StringComparison.Ordinal))
            ?.DropDownItems
            .OfType<ToolStripMenuItem>()
            .Select(item => item.Text)
            .ToArray() ?? Array.Empty<string>();
        var displayMenu = _mainMoreMenu.Items
            .OfType<ToolStripMenuItem>()
            .FirstOrDefault(item => string.Equals(item.Text, "显示", StringComparison.Ordinal));
        var displayMenuDirectTexts = displayMenu?.DropDownItems
            .OfType<ToolStripMenuItem>()
            .Select(item => item.Text)
            .ToArray() ?? Array.Empty<string>();
        var floatingDisplayMenuDirectTexts = displayMenu?.DropDownItems
            .OfType<ToolStripMenuItem>()
            .FirstOrDefault(item => string.Equals(item.Text, "悬浮球", StringComparison.Ordinal))
            ?.DropDownItems
            .OfType<ToolStripMenuItem>()
            .Select(item => item.Text)
            .ToArray() ?? Array.Empty<string>();
        var resultFontMenuDirectTexts = displayMenu?.DropDownItems
            .OfType<ToolStripMenuItem>()
            .FirstOrDefault(item => string.Equals(item.Text, "结果字号", StringComparison.Ordinal))
            ?.DropDownItems
            .OfType<ToolStripMenuItem>()
            .Select(item => item.Text)
            .ToArray() ?? Array.Empty<string>();
        var windowStartupMenuDirectTexts = displayMenu?.DropDownItems
            .OfType<ToolStripMenuItem>()
            .FirstOrDefault(item => string.Equals(item.Text, "窗口和启动", StringComparison.Ordinal))
            ?.DropDownItems
            .OfType<ToolStripMenuItem>()
            .Select(item => item.Text)
            .ToArray() ?? Array.Empty<string>();
        var drawMenu = _mainMoreMenu.Items
            .OfType<ToolStripMenuItem>()
            .FirstOrDefault(item => string.Equals(item.Text, "抽号", StringComparison.Ordinal));
        var drawMenuDirectTexts = drawMenu?.DropDownItems
            .OfType<ToolStripMenuItem>()
            .Select(item => item.Text)
            .ToArray() ?? Array.Empty<string>();
        var rangeDrawMenuDirectTexts = drawMenu?.DropDownItems
            .OfType<ToolStripMenuItem>()
            .FirstOrDefault(item => string.Equals(item.Text, "按范围抽号", StringComparison.Ordinal))
            ?.DropDownItems
            .OfType<ToolStripMenuItem>()
            .Select(item => item.Text)
            .ToArray() ?? Array.Empty<string>();
        var roundActionsMenuDirectTexts = drawMenu?.DropDownItems
            .OfType<ToolStripMenuItem>()
            .FirstOrDefault(item => string.Equals(item.Text, "本轮操作", StringComparison.Ordinal))
            ?.DropDownItems
            .OfType<ToolStripMenuItem>()
            .Select(item => item.Text)
            .ToArray() ?? Array.Empty<string>();
        var drawSettingsMenuDirectTexts = drawMenu?.DropDownItems
            .OfType<ToolStripMenuItem>()
            .FirstOrDefault(item => string.Equals(item.Text, "抽号设置", StringComparison.Ordinal))
            ?.DropDownItems
            .OfType<ToolStripMenuItem>()
            .Select(item => item.Text)
            .ToArray() ?? Array.Empty<string>();
        var recordsMenuDirectTexts = drawMenu?.DropDownItems
            .OfType<ToolStripMenuItem>()
            .FirstOrDefault(item => string.Equals(item.Text, "记录和导出", StringComparison.Ordinal))
            ?.DropDownItems
            .OfType<ToolStripMenuItem>()
            .Select(item => item.Text)
            .ToArray() ?? Array.Empty<string>();
        var recordsLessonFilesMenuDirectTexts = drawMenu?.DropDownItems
            .OfType<ToolStripMenuItem>()
            .FirstOrDefault(item => string.Equals(item.Text, "记录和导出", StringComparison.Ordinal))
            ?.DropDownItems
            .OfType<ToolStripMenuItem>()
            .FirstOrDefault(item => string.Equals(item.Text, "课堂文件", StringComparison.Ordinal))
            ?.DropDownItems
            .OfType<ToolStripMenuItem>()
            .Select(item => item.Text)
            .ToArray() ?? Array.Empty<string>();
        var recordsHistoryManagementMenuDirectTexts = drawMenu?.DropDownItems
            .OfType<ToolStripMenuItem>()
            .FirstOrDefault(item => string.Equals(item.Text, "记录和导出", StringComparison.Ordinal))
            ?.DropDownItems
            .OfType<ToolStripMenuItem>()
            .FirstOrDefault(item => string.Equals(item.Text, "历史管理", StringComparison.Ordinal))
            ?.DropDownItems
            .OfType<ToolStripMenuItem>()
            .Select(item => item.Text)
            .ToArray() ?? Array.Empty<string>();
        _mainMoreMenu.Close();
        var floatingContextMenuTopLevelTexts = _floatingMenu.Items
            .OfType<ToolStripMenuItem>()
            .Select(item => item.Text)
            .ToArray();
        var floatingContextDrawMenuDirectTexts = _floatingMenu.Items
            .OfType<ToolStripMenuItem>()
            .FirstOrDefault(item => string.Equals(item.Text, "抽号操作", StringComparison.Ordinal))
            ?.DropDownItems
            .OfType<ToolStripMenuItem>()
            .Select(item => item.Text)
            .ToArray() ?? Array.Empty<string>();
        var floatingContextRosterMenuDirectTexts = _floatingMenu.Items
            .OfType<ToolStripMenuItem>()
            .FirstOrDefault(item => string.Equals(item.Text, "名单", StringComparison.Ordinal))
            ?.DropDownItems
            .OfType<ToolStripMenuItem>()
            .Select(item => item.Text)
            .ToArray() ?? Array.Empty<string>();
        var floatingContextRecordsMenuDirectTexts = _floatingMenu.Items
            .OfType<ToolStripMenuItem>()
            .FirstOrDefault(item => string.Equals(item.Text, "记录", StringComparison.Ordinal))
            ?.DropDownItems
            .OfType<ToolStripMenuItem>()
            .Select(item => item.Text)
            .ToArray() ?? Array.Empty<string>();
        if (mainMoreMenuTopLevelTexts.Length > 4
            || mainMoreMenuTopLevelTexts.Contains("课后", StringComparer.Ordinal)
            || mainMoreMenuTopLevelTexts.Length == 0
            || !string.Equals(mainMoreMenuTopLevelTexts[0], "抽号", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Main more menu still exposes too many top-level feature buckets.");
        }

        if (!drawMenuDirectTexts.Contains("新一节课", StringComparer.Ordinal)
            || recordsMenuDirectTexts.Contains("新一节课", StringComparer.Ordinal))
        {
            throw new InvalidOperationException("New lesson is not discoverable directly under the draw menu.");
        }

        if (drawMenuDirectTexts.Length > 6
            || drawMenuDirectTexts.Contains("抽男生", StringComparer.Ordinal)
            || drawMenuDirectTexts.Contains("抽女生", StringComparer.Ordinal)
            || drawMenuDirectTexts.Contains("抽小组", StringComparer.Ordinal)
            || drawMenuDirectTexts.Any(text => text.StartsWith("抽号时长", StringComparison.Ordinal))
            || !drawMenuDirectTexts.Contains("按范围抽号", StringComparer.Ordinal)
            || !drawMenuDirectTexts.Contains("本轮操作", StringComparer.Ordinal)
            || !drawMenuDirectTexts.Contains("抽号设置", StringComparer.Ordinal)
            || !rangeDrawMenuDirectTexts.Contains("抽男生", StringComparer.Ordinal)
            || !rangeDrawMenuDirectTexts.Contains("抽女生", StringComparer.Ordinal)
            || !rangeDrawMenuDirectTexts.Contains("抽小组", StringComparer.Ordinal)
            || !roundActionsMenuDirectTexts.Contains("撤销上次", StringComparer.Ordinal)
            || !roundActionsMenuDirectTexts.Contains("重抽一次", StringComparer.Ordinal)
            || !roundActionsMenuDirectTexts.Contains("重置本轮", StringComparer.Ordinal)
            || !drawSettingsMenuDirectTexts.Any(text => text.StartsWith("抽号时长", StringComparison.Ordinal))
            || !drawSettingsMenuDirectTexts.Any(text => text.StartsWith("避免重复", StringComparison.Ordinal))
            || !drawSettingsMenuDirectTexts.Any(text => text.StartsWith("自动复制", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Draw menu still reads like a feature list instead of grouped classroom actions.");
        }

        if (recordsMenuDirectTexts.Length > 4
            || recordsMenuDirectTexts.Contains("导出摘要", StringComparer.Ordinal)
            || recordsMenuDirectTexts.Contains("导出课堂包", StringComparer.Ordinal)
            || recordsMenuDirectTexts.Contains("打开包位置", StringComparer.Ordinal)
            || recordsMenuDirectTexts.Contains("导出历史", StringComparer.Ordinal)
            || recordsMenuDirectTexts.Contains("清空历史", StringComparer.Ordinal)
            || !recordsMenuDirectTexts.Contains("复制结果", StringComparer.Ordinal)
            || !recordsMenuDirectTexts.Contains("查看历史", StringComparer.Ordinal)
            || !recordsMenuDirectTexts.Contains("课堂文件", StringComparer.Ordinal)
            || !recordsMenuDirectTexts.Contains("历史管理", StringComparer.Ordinal)
            || !recordsLessonFilesMenuDirectTexts.Contains("复制摘要", StringComparer.Ordinal)
            || !recordsLessonFilesMenuDirectTexts.Contains("导出摘要", StringComparer.Ordinal)
            || !recordsLessonFilesMenuDirectTexts.Contains("导出课堂包", StringComparer.Ordinal)
            || !recordsLessonFilesMenuDirectTexts.Contains("打开包位置", StringComparer.Ordinal)
            || !recordsHistoryManagementMenuDirectTexts.Contains("导出历史", StringComparer.Ordinal)
            || !recordsHistoryManagementMenuDirectTexts.Contains("清空历史", StringComparer.Ordinal))
        {
            throw new InvalidOperationException("Records and export actions still read like a long feature list.");
        }

        if (floatingContextMenuTopLevelTexts.Length > 8
            || !floatingContextMenuTopLevelTexts.Contains("开始抽号", StringComparer.Ordinal)
            || !floatingContextMenuTopLevelTexts.Contains("抽号操作", StringComparer.Ordinal)
            || !floatingContextMenuTopLevelTexts.Contains("名单", StringComparer.Ordinal)
            || !floatingContextMenuTopLevelTexts.Contains("记录", StringComparer.Ordinal)
            || !floatingContextMenuTopLevelTexts.Contains("显示", StringComparer.Ordinal)
            || !floatingContextMenuTopLevelTexts.Contains("帮助", StringComparer.Ordinal)
            || floatingContextMenuTopLevelTexts.Contains("抽男生", StringComparer.Ordinal)
            || floatingContextMenuTopLevelTexts.Contains("导出课堂包", StringComparer.Ordinal)
            || floatingContextMenuTopLevelTexts.Contains("复制未抽名单", StringComparer.Ordinal)
            || !floatingContextDrawMenuDirectTexts.Contains("抽男生", StringComparer.Ordinal)
            || !floatingContextDrawMenuDirectTexts.Contains("抽女生", StringComparer.Ordinal)
            || !floatingContextDrawMenuDirectTexts.Contains("抽小组", StringComparer.Ordinal)
            || !floatingContextRosterMenuDirectTexts.Contains("上课向导", StringComparer.Ordinal)
            || !floatingContextRosterMenuDirectTexts.Contains("准备名单", StringComparer.Ordinal)
            || !floatingContextRosterMenuDirectTexts.Contains("本节名单", StringComparer.Ordinal)
            || !floatingContextRecordsMenuDirectTexts.Contains("复制结果", StringComparer.Ordinal)
            || !floatingContextRecordsMenuDirectTexts.Contains("查看历史", StringComparer.Ordinal)
            || !floatingContextRecordsMenuDirectTexts.Contains("课堂文件", StringComparer.Ordinal)
            || !floatingContextRecordsMenuDirectTexts.Contains("历史管理", StringComparer.Ordinal))
        {
            throw new InvalidOperationException("Floating context menu still exposes secondary features as a long list.");
        }

        if (rosterMenuDirectTexts.Length > 5
            || !rosterMenuDirectTexts.Contains("上课向导", StringComparer.Ordinal)
            || !rosterMenuDirectTexts.Any(text => text.StartsWith("本节缺席", StringComparison.Ordinal))
            || !rosterMenuDirectTexts.Contains("名单概览", StringComparer.Ordinal)
            || !rosterMenuDirectTexts.Contains("准备名单", StringComparer.Ordinal)
            || !rosterMenuDirectTexts.Contains("文件和导出", StringComparer.Ordinal)
            || rosterMenuDirectTexts.Contains("名单模板", StringComparer.Ordinal)
            || rosterMenuDirectTexts.Contains("试用名单", StringComparer.Ordinal)
            || rosterMenuDirectTexts.Contains("恢复名单", StringComparer.Ordinal)
            || rosterMenuDirectTexts.Contains("最近名单", StringComparer.Ordinal)
            || rosterMenuDirectTexts.Contains("复制名单", StringComparer.Ordinal)
            || rosterMenuDirectTexts.Contains("导出名单", StringComparer.Ordinal)
            || !prepareRosterMenuDirectTexts.Contains("名单模板", StringComparer.Ordinal)
            || !prepareRosterMenuDirectTexts.Contains("试用名单", StringComparer.Ordinal)
            || !prepareRosterMenuDirectTexts.Contains("恢复名单", StringComparer.Ordinal)
            || !prepareRosterMenuDirectTexts.Contains("最近名单", StringComparer.Ordinal)
            || rosterFilesMenuDirectTexts.Length < 4
            || !rosterFilesMenuDirectTexts.Contains("重载文件", StringComparer.Ordinal)
            || !rosterFilesMenuDirectTexts.Contains("复制名单", StringComparer.Ordinal)
            || !rosterFilesMenuDirectTexts.Contains("导出名单", StringComparer.Ordinal))
        {
            throw new InvalidOperationException("Roster menu still reads like a feature list instead of grouped setup actions.");
        }

        if (displayMenuDirectTexts.Length > 4
            || !displayMenuDirectTexts.Any(text => text.Contains("全屏", StringComparison.Ordinal))
            || !displayMenuDirectTexts.Contains("悬浮球", StringComparer.Ordinal)
            || !displayMenuDirectTexts.Contains("结果字号", StringComparer.Ordinal)
            || !displayMenuDirectTexts.Contains("窗口和启动", StringComparer.Ordinal)
            || displayMenuDirectTexts.Any(text => text.Contains("置顶", StringComparison.Ordinal))
            || displayMenuDirectTexts.Any(text => text.Contains("开机启动", StringComparison.Ordinal))
            || displayMenuDirectTexts.Any(text => text.Contains("静默启动", StringComparison.Ordinal))
            || displayMenuDirectTexts.Any(text => text.Contains("深浅", StringComparison.Ordinal))
            || displayMenuDirectTexts.Any(text => text.Contains("字号", StringComparison.Ordinal) && !string.Equals(text, "结果字号", StringComparison.Ordinal))
            || !floatingDisplayMenuDirectTexts.Contains("悬浮球模式", StringComparer.Ordinal)
            || !floatingDisplayMenuDirectTexts.Contains("悬浮球深浅", StringComparer.Ordinal)
            || !resultFontMenuDirectTexts.Contains("放大结果字号", StringComparer.Ordinal)
            || !resultFontMenuDirectTexts.Contains("缩小结果字号", StringComparer.Ordinal)
            || !windowStartupMenuDirectTexts.Any(text => text.Contains("置顶", StringComparison.Ordinal))
            || !windowStartupMenuDirectTexts.Any(text => text.Contains("开机启动", StringComparison.Ordinal))
            || !windowStartupMenuDirectTexts.Any(text => text.Contains("静默启动", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Display menu still exposes secondary window settings directly.");
        }

        var emptyMainActionButton = GetMainDrawButtonIdleText(0);
        var readyMainActionButton = GetMainDrawButtonIdleText(1);
        var headerSetupButtonTexts = GetHeaderSetupButtonTexts();
        if (!string.Equals(emptyMainActionButton, DemoDrawActionText, StringComparison.Ordinal)
            || !string.Equals(readyMainActionButton, CoreDrawActionText, StringComparison.Ordinal)
            || !EmptyRosterResultText.Contains(DemoDrawActionText, StringComparison.Ordinal)
            || !FirstUseHint.Contains(DemoDrawActionText, StringComparison.Ordinal)
            || !FirstUseHint.Contains("粘贴名单", StringComparison.Ordinal)
            || !FirstUseHint.Contains("导入文件", StringComparison.Ordinal)
            || FirstUseHint.Contains("更换名单", StringComparison.Ordinal)
            || EmptyRosterResultText.Contains(CoreDrawActionText, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Empty roster draw path does not clearly expose demo draw as the main action.");
        }

        if (headerSetupButtonTexts.Length != 4
            || !string.Equals(headerSetupButtonTexts[0], "粘贴名单", StringComparison.Ordinal)
            || !string.Equals(headerSetupButtonTexts[1], "导入文件", StringComparison.Ordinal)
            || !string.Equals(headerSetupButtonTexts[2], "悬浮球", StringComparison.Ordinal)
            || !string.Equals(headerSetupButtonTexts[3], "设置", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("First-use setup buttons do not put paste/import before secondary actions.");
        }

        if (!headerDailyActionSelfCheck.EmptyHeaderOk
            || !headerDailyActionSelfCheck.ReadyHeaderOk)
        {
            throw new InvalidOperationException("Header actions do not switch cleanly between first-use setup and daily class workflow.");
        }

        var importSuccessDefaultAction = GetImportSuccessDefaultActionText(canStartDraw: true, hasImportWarnings: false);
        var importSuccessWarningDefaultAction = GetImportSuccessDefaultActionText(canStartDraw: true, hasImportWarnings: true);
        if (!string.Equals(importSuccessDefaultAction, ImportSuccessPrimaryActionText, StringComparison.Ordinal)
            || !string.Equals(importSuccessWarningDefaultAction, ImportSuccessDismissActionText, StringComparison.Ordinal)
            || !BuildImportSuccessNextStepText(hasImportWarnings: false).Contains("Enter", StringComparison.Ordinal)
            || !BuildImportSuccessNextStepText(hasImportWarnings: true).Contains("先确认", StringComparison.Ordinal)
            || !string.Equals(ImportSuccessDismissActionText, "稍后开始", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Import success dialog does not choose a safe default action.");
        }

        var emptyRosterStatusText = BuildEmptyClassroomStatusText();
        var emptyRoundProgressText = BuildEmptyRoundProgressText();
        if (!emptyRosterStatusText.Contains("待准备", StringComparison.Ordinal)
            || !emptyRosterStatusText.Contains(DemoDrawActionText, StringComparison.Ordinal)
            || !emptyRoundProgressText.Contains(DemoDrawActionText, StringComparison.Ordinal)
            || emptyRosterStatusText.Contains("未加载", StringComparison.Ordinal)
            || emptyRoundProgressText.Contains("无名单", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Empty roster status still reads like an error state.");
        }

        var startGuideText = BuildStartGuideText();
        if (startGuideText.Contains("课堂包", StringComparison.Ordinal)
            || startGuideText.Contains("摘要", StringComparison.Ordinal)
            || startGuideText.Contains("导出", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Start guide is not focused on the core draw workflow.");
        }

        var quickGuideText = BuildQuickGuideText();
        var shortcutKeysRemoved = !quickGuideText.Contains("Ctrl+", StringComparison.OrdinalIgnoreCase)
            && !quickGuideText.Contains("F1", StringComparison.OrdinalIgnoreCase)
            && !quickGuideText.Contains("F2", StringComparison.OrdinalIgnoreCase)
            && !quickGuideText.Contains("F5", StringComparison.OrdinalIgnoreCase)
            && !quickGuideText.Contains("F6", StringComparison.OrdinalIgnoreCase)
            && !quickGuideText.Contains("F7", StringComparison.OrdinalIgnoreCase)
            && !quickGuideText.Contains("F8", StringComparison.OrdinalIgnoreCase)
            && !quickGuideText.Contains("F11", StringComparison.OrdinalIgnoreCase)
            && !quickGuideText.Contains("Esc", StringComparison.OrdinalIgnoreCase);
        if (!shortcutKeysRemoved)
        {
            throw new InvalidOperationException("Quick guide still advertises keyboard shortcuts.");
        }

        var settingsDialogSelfCheck = RunSettingsDialogSelfCheck();
        if (!settingsDialogSelfCheck.Ok)
        {
            throw new InvalidOperationException("Settings dialog self-check failed.");
        }

        if (_drawButton.Parent is null
            || _drawMaleButton.Parent is null
            || _drawFemaleButton.Parent is null
            || _drawGroupButton.Parent is null
            || !ReferenceEquals(_drawButton.Parent, _drawMaleButton.Parent)
            || !ReferenceEquals(_drawButton.Parent, _drawFemaleButton.Parent)
            || !ReferenceEquals(_drawButton.Parent, _drawGroupButton.Parent)
            || _floatingModeButton.Parent is null
            || !ReferenceEquals(_floatingModeButton.Parent, _moreActionsButton.Parent)
            || !string.Equals(_floatingModeButton.Text, "悬浮球", StringComparison.Ordinal)
            || !string.Equals(_moreActionsButton.Text, "设置", StringComparison.Ordinal)
            || _absenceButton.Parent is null
            || ReferenceEquals(_absenceButton.Parent, _drawButton.Parent))
        {
            throw new InvalidOperationException("Main direct actions do not expose the core draw workflow.");
        }

        var mainFooterDecluttered = _lastWinnerValue.Parent is not null
            && _copyResultButton.Parent is null
            && _viewHistoryButton.Parent is null
            && _newLessonButton.Parent is null
            && _decreaseFontButton.Parent is null
            && _fontSizeValue.Parent is null
            && _increaseFontButton.Parent is null;
        if (!mainFooterDecluttered)
        {
            throw new InvalidOperationException("Main footer still exposes secondary actions.");
        }

        var mainDurationControlsHidden = _drawDurationInput.Parent is null
            && _duration3Button.Parent is null
            && _duration5Button.Parent is null
            && _duration8Button.Parent is null;
        if (!mainDurationControlsHidden)
        {
            throw new InvalidOperationException("Main surface still exposes duration tuning controls.");
        }

        return new StartupSelfCheckResult
        {
            Ok = true,
            Version = AppVersion,
            BuildDate = AppBuildDate,
            StudentCount = _state.Students.Count,
            HistoryCount = _state.DrawHistory.Count,
            CardWidth = _cardPanel.Width,
            CardHeight = _cardPanel.Height,
            CountdownWidth = _countdownValue.Width,
            CountdownHeight = _countdownValue.Height,
            CountdownSample = countdownSample,
            CountdownLineCount = countdownLineCount,
            StartupSurfaceReady = startupSurfaceReady,
            StartupCriticalControlsReady = startupCriticalControlsReady,
            StartupWarmupPasses = StartupRevealWarmupPasses,
            StartupRevealMaxWaitMs = StartupRevealMaxWaitMs,
            HeadlessTopMost = TopMost,
            MainMoreMenuTopLevelTexts = string.Join(" / ", mainMoreMenuTopLevelTexts),
            RosterMenuDirectTexts = string.Join(" / ", rosterMenuDirectTexts),
            PrepareRosterMenuDirectTexts = string.Join(" / ", prepareRosterMenuDirectTexts),
            RosterFilesMenuDirectTexts = string.Join(" / ", rosterFilesMenuDirectTexts),
            DisplayMenuDirectTexts = string.Join(" / ", displayMenuDirectTexts),
            FloatingDisplayMenuDirectTexts = string.Join(" / ", floatingDisplayMenuDirectTexts),
            ResultFontMenuDirectTexts = string.Join(" / ", resultFontMenuDirectTexts),
            WindowStartupMenuDirectTexts = string.Join(" / ", windowStartupMenuDirectTexts),
            MainMoreDrawMenuDirectTexts = string.Join(" / ", drawMenuDirectTexts),
            RangeDrawMenuDirectTexts = string.Join(" / ", rangeDrawMenuDirectTexts),
            RoundActionsMenuDirectTexts = string.Join(" / ", roundActionsMenuDirectTexts),
            DrawSettingsMenuDirectTexts = string.Join(" / ", drawSettingsMenuDirectTexts),
            RecordsMenuDirectTexts = string.Join(" / ", recordsMenuDirectTexts),
            RecordsLessonFilesMenuDirectTexts = string.Join(" / ", recordsLessonFilesMenuDirectTexts),
            RecordsHistoryManagementMenuDirectTexts = string.Join(" / ", recordsHistoryManagementMenuDirectTexts),
            FloatingContextMenuTopLevelTexts = string.Join(" / ", floatingContextMenuTopLevelTexts),
            FloatingContextDrawMenuDirectTexts = string.Join(" / ", floatingContextDrawMenuDirectTexts),
            FloatingContextRosterMenuDirectTexts = string.Join(" / ", floatingContextRosterMenuDirectTexts),
            FloatingContextRecordsMenuDirectTexts = string.Join(" / ", floatingContextRecordsMenuDirectTexts),
            NewLessonMenuPath = NewLessonMenuPath,
            EmptyMainActionButton = emptyMainActionButton,
            ReadyMainActionButton = readyMainActionButton,
            EmptyRosterText = EmptyRosterResultText,
            EmptyRosterStatusText = emptyRosterStatusText,
            EmptyRoundProgressText = emptyRoundProgressText,
            FirstUseHint = FirstUseHint,
            HeaderSetupButtonTexts = string.Join(" / ", headerSetupButtonTexts),
            EmptyHeaderVisibleButtonTexts = headerDailyActionSelfCheck.EmptyHeaderVisibleButtonTexts,
            ReadyHeaderVisibleButtonTexts = headerDailyActionSelfCheck.ReadyHeaderVisibleButtonTexts,
            PasteRosterButton = _pasteListButton.Text,
            ImportFileButton = _loadListButton.Text,
            ImportSuccessDefaultAction = importSuccessDefaultAction,
            ImportSuccessWarningDefaultAction = importSuccessWarningDefaultAction,
            ImportSuccessDismissAction = ImportSuccessDismissActionText,
            StartGuideText = startGuideText,
            MainActionButton = _drawButton.Text,
            RosterButton = _loadListButton.Text,
            MoreButton = _moreActionsButton.Text,
            ShortcutKeysRemoved = shortcutKeysRemoved,
            SettingsDialogSelfCheckOk = settingsDialogSelfCheck.Ok,
            SettingsDialogTabTexts = settingsDialogSelfCheck.TabTexts,
            SettingsDialogDrawControlTexts = settingsDialogSelfCheck.DrawControlTexts,
            MainFooterDecluttered = mainFooterDecluttered,
            MainDurationControlsHidden = mainDurationControlsHidden,
            CorruptStateBackupRecoveryOk = stateRecoverySelfCheck.CorruptStateBackupRecoveryOk,
            MissingStateBackupRecoveryOk = stateRecoverySelfCheck.MissingStateBackupRecoveryOk,
            StateRecoveryStudentCount = stateRecoverySelfCheck.RecoveredStudentCount,
            StateRecoveryHistoryCount = stateRecoverySelfCheck.RecoveredHistoryCount,
            StateRecoveryNoticeSample = stateRecoverySelfCheck.RecoveryNoticeSample,
            DamagedStatePreserved = stateRecoverySelfCheck.DamagedStatePreserved,
            RestoredStateFileReadable = stateRecoverySelfCheck.RestoredStateFileReadable,
            StateSaveSelfCheckOk = stateSaveSelfCheck.Ok,
            StateSavePrimaryReadable = stateSaveSelfCheck.PrimaryReadable,
            StateSaveBackupReadable = stateSaveSelfCheck.BackupReadable,
            StateSaveTempFilesCleaned = stateSaveSelfCheck.TempFilesCleaned,
            StateSaveBackupLastWinner = stateSaveSelfCheck.BackupLastWinner,
            FirstUseImportWorkflowSelfCheckOk = firstUseImportWorkflowSelfCheck.Ok,
            FirstUseImportStudentCount = firstUseImportWorkflowSelfCheck.ImportedStudentCount,
            FirstUseImportReadyHeaderTexts = firstUseImportWorkflowSelfCheck.ReadyHeaderTexts,
            FirstUseImportMainActionText = firstUseImportWorkflowSelfCheck.MainActionText,
            FirstUseImportResultText = firstUseImportWorkflowSelfCheck.ResultText,
            FirstUseImportClassroomStatusText = firstUseImportWorkflowSelfCheck.ClassroomStatusText,
            FirstUseImportSavedStateReadable = firstUseImportWorkflowSelfCheck.SavedStateReadable,
            CoreDrawWorkflowSelfCheckOk = coreDrawWorkflowSelfCheck.Ok,
            CoreDrawWorkflowStarted = coreDrawWorkflowSelfCheck.Started,
            CoreDrawWorkflowCompleted = coreDrawWorkflowSelfCheck.Completed,
            CoreDrawWorkflowWinner = coreDrawWorkflowSelfCheck.Winner,
            CoreDrawWorkflowHistoryCount = coreDrawWorkflowSelfCheck.HistoryCount,
            CoreDrawWorkflowButtonText = coreDrawWorkflowSelfCheck.ButtonText,
            CoreDrawWorkflowButtonEnabled = coreDrawWorkflowSelfCheck.ButtonEnabled,
            CoreDrawWorkflowDrawnStudentKeyCount = coreDrawWorkflowSelfCheck.DrawnStudentKeyCount,
            CoreDrawWorkflowResultText = coreDrawWorkflowSelfCheck.ResultText,
            RosterImportParserSelfCheckOk = rosterImportParserSelfCheck.Ok,
            RosterImportParserCaseCount = rosterImportParserSelfCheck.CaseCount,
            RosterImportParserStudentCount = rosterImportParserSelfCheck.StudentCount,
            RosterImportParserSamples = rosterImportParserSelfCheck.Samples,
            AvoidRepeatRolloverSelfCheckOk = avoidRepeatRolloverSelfCheck.Ok,
            AvoidRepeatRolloverStudentPoolCount = avoidRepeatRolloverSelfCheck.StudentPoolCount,
            AvoidRepeatRolloverGroupPoolCount = avoidRepeatRolloverSelfCheck.GroupPoolCount,
            AvoidRepeatRolloverHint = avoidRepeatRolloverSelfCheck.Hint
        };
    }

    private static (bool CorruptStateBackupRecoveryOk,
        bool MissingStateBackupRecoveryOk,
        int RecoveredStudentCount,
        int RecoveredHistoryCount,
        string RecoveryNoticeSample,
        bool DamagedStatePreserved,
        bool RestoredStateFileReadable) RunAppStateRecoverySelfCheck()
    {
        var tempDirectory = Path.Combine(
            Path.GetTempPath(),
            $"DrawMachineDesktop-state-recovery-selfcheck-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(tempDirectory);

            var corruptScenarioDirectory = Path.Combine(tempDirectory, "corrupt-primary");
            Directory.CreateDirectory(corruptScenarioDirectory);
            var corruptStatePath = Path.Combine(corruptScenarioDirectory, "state.json");
            var corruptBackupPath = corruptStatePath + ".bak";
            WriteSelfCheckState(corruptBackupPath, CreateRecoveryProbeState());
            File.WriteAllText(corruptStatePath, "{ broken state json", Encoding.UTF8);

            var corruptRecoveredState = AppState.LoadFromPathForSelfCheck(corruptStatePath);
            var damagedFiles = Directory.GetFiles(corruptScenarioDirectory, "state-damaged-*.json");
            var corruptPrimaryReadable = TryReadSelfCheckState(corruptStatePath, out var corruptPrimaryState);
            var damagedStatePreserved = damagedFiles.Length == 1
                && File.ReadAllText(damagedFiles[0], Encoding.UTF8).Contains("broken state json", StringComparison.Ordinal);
            var corruptRecoveryOk = corruptPrimaryReadable
                && IsRecoveryProbeState(corruptRecoveredState)
                && IsRecoveryProbeState(corruptPrimaryState)
                && corruptRecoveredState.RecoveryNotice.Contains("配置文件损坏", StringComparison.Ordinal)
                && corruptRecoveredState.RecoveryNotice.Contains("自动备份恢复", StringComparison.Ordinal)
                && damagedStatePreserved;

            var missingScenarioDirectory = Path.Combine(tempDirectory, "missing-primary");
            Directory.CreateDirectory(missingScenarioDirectory);
            var missingStatePath = Path.Combine(missingScenarioDirectory, "state.json");
            var missingBackupPath = missingStatePath + ".bak";
            WriteSelfCheckState(missingBackupPath, CreateRecoveryProbeState());

            var missingRecoveredState = AppState.LoadFromPathForSelfCheck(missingStatePath);
            var missingPrimaryReadable = TryReadSelfCheckState(missingStatePath, out var missingPrimaryState);
            var missingRecoveryOk = missingPrimaryReadable
                && IsRecoveryProbeState(missingRecoveredState)
                && IsRecoveryProbeState(missingPrimaryState)
                && missingRecoveredState.RecoveryNotice.Contains("未找到配置文件", StringComparison.Ordinal)
                && missingRecoveredState.RecoveryNotice.Contains("自动备份恢复", StringComparison.Ordinal);

            if (!corruptRecoveryOk || !missingRecoveryOk)
            {
                throw new InvalidOperationException("App state backup recovery self-check failed.");
            }

            return (
                CorruptStateBackupRecoveryOk: corruptRecoveryOk,
                MissingStateBackupRecoveryOk: missingRecoveryOk,
                RecoveredStudentCount: corruptRecoveredState.Students.Count,
                RecoveredHistoryCount: corruptRecoveredState.DrawHistory.Count,
                RecoveryNoticeSample: corruptRecoveredState.RecoveryNotice.ReplaceLineEndings(" "),
                DamagedStatePreserved: damagedStatePreserved,
                RestoredStateFileReadable: corruptPrimaryReadable && missingPrimaryReadable);
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, recursive: true);
                }
            }
            catch
            {
                // A stale temp self-check directory does not affect the app state.
            }
        }
    }

    private static AppState CreateRecoveryProbeState()
    {
        return new AppState
        {
            Names = new List<string> { "自检学生A", "自检学生B" },
            Students = new List<StudentRecord>
            {
                new() { Sequence = "1", Name = "自检学生A", Gender = "男", Group = "一组" },
                new() { Sequence = "2", Name = "自检学生B", Gender = "女", Group = "二组" }
            },
            FileName = "v162恢复自检名单.txt",
            SourceFilePath = @"C:\DrawMachineSelfCheck\v162恢复自检名单.txt",
            RecentRosterFiles = new List<string> { @"C:\DrawMachineSelfCheck\v162恢复自检名单.txt" },
            LastWinner = "自检学生A",
            DrawHistory = new List<string> { "1 自检学生A" },
            DrawDurationSeconds = 3.0,
            FloatingShade = 42,
            AvoidRepeatDraw = true,
            AutoCopyResult = true,
            DrawnStudentKeys = new List<string> { "1|自检学生A" },
            LastDrawUndoKind = "Student",
            LastDrawUndoKey = "1|自检学生A",
            LastDrawUndoHistoryEntry = "1 自检学生A",
            LastDrawReplayMode = "Student"
        };
    }

    private static (bool Ok,
        bool PrimaryReadable,
        bool BackupReadable,
        bool TempFilesCleaned,
        string BackupLastWinner) RunAppStateSaveSelfCheck()
    {
        var tempDirectory = Path.Combine(
            Path.GetTempPath(),
            $"DrawMachineDesktop-state-save-selfcheck-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(tempDirectory);
            var statePath = Path.Combine(tempDirectory, "state.json");
            var backupPath = statePath + ".bak";

            var firstState = CreateRecoveryProbeState();
            firstState.FileName = "第一次保存名单.txt";
            firstState.LastWinner = "第一次保存";
            firstState.DrawHistory = new List<string> { "第一次保存" };
            firstState.SaveToPathForSelfCheck(statePath);

            if (!File.Exists(statePath) || File.Exists(backupPath))
            {
                throw new InvalidOperationException("Initial app state save did not create the expected primary-only file.");
            }

            var secondState = CreateRecoveryProbeState();
            secondState.FileName = "第二次保存名单.txt";
            secondState.LastWinner = "第二次保存";
            secondState.DrawHistory = new List<string> { "第二次保存" };
            secondState.ResultFontSize = 64f;
            secondState.SaveToPathForSelfCheck(statePath);

            var primaryReadable = TryReadSelfCheckState(statePath, out var primaryState);
            var backupReadable = TryReadSelfCheckState(backupPath, out var backupState);
            var loadedState = AppState.LoadFromPathForSelfCheck(statePath);
            var tempFilesCleaned = Directory.GetFiles(tempDirectory, "state-*.tmp").Length == 0;
            var saveOk = primaryReadable
                && backupReadable
                && tempFilesCleaned
                && string.Equals(primaryState.LastWinner, "第二次保存", StringComparison.Ordinal)
                && Math.Abs(primaryState.ResultFontSize - 64f) < 0.001f
                && string.Equals(loadedState.LastWinner, "第二次保存", StringComparison.Ordinal)
                && string.Equals(backupState.LastWinner, "第一次保存", StringComparison.Ordinal);

            if (!saveOk)
            {
                throw new InvalidOperationException("App state save backup self-check failed.");
            }

            return (
                Ok: true,
                PrimaryReadable: primaryReadable,
                BackupReadable: backupReadable,
                TempFilesCleaned: tempFilesCleaned,
                BackupLastWinner: backupState.LastWinner);
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, recursive: true);
                }
            }
            catch
            {
                // A stale temp self-check directory does not affect the app state.
            }
        }
    }

    private (bool Ok,
        int ImportedStudentCount,
        string ReadyHeaderTexts,
        string MainActionText,
        string ResultText,
        string ClassroomStatusText,
        bool SavedStateReadable) RunFirstUseImportWorkflowSelfCheck()
    {
        var snapshot = CaptureAppStateSnapshot();
        var originalHint = _hintLabel.Text;
        var originalResultText = _currentResultText;
        try
        {
            ClearStateForFirstUseSelfCheck();
            UpdateResetRoundButton();
            UpdateUndoDrawButton();
            UpdateHistoryActionState();
            UpdateRosterActionState();
            UpdateClassroomStatusDisplay();
            SetIdleResultText();

            var emptyHeaderTexts = GetVisibleHeaderButtonTexts();
            if (!emptyHeaderTexts.SequenceEqual(new[] { "粘贴名单", "导入文件", "悬浮球", "设置" }, StringComparer.Ordinal))
            {
                throw new InvalidOperationException("First-use import workflow did not start from the expected empty header.");
            }

            var parseResult = ParseStudents(new[]
            {
                "序号\t姓名\t性别\t小组",
                "1\t张三\t男\t第一组",
                "2\t李四\t女\t第二组",
                "3\t王五\t男\t第二组"
            });
            var imported = ApplyImportedStudents(
                parseResult,
                "首次使用自检名单",
                "首次使用自检粘贴名单",
                showSuccessMessage: false);
            var loadedState = AppState.Load();
            var readyHeaderTexts = GetVisibleHeaderButtonTexts();
            var resultText = _resultLabel.Text.ReplaceLineEndings(" ");
            var classroomStatusText = _fileNameValue.Text.ReplaceLineEndings(" ");
            var savedStateReadable = loadedState.Students.Count == 3
                && string.Equals(loadedState.FileName, "首次使用自检名单", StringComparison.Ordinal)
                && loadedState.DrawnStudentKeys.Count == 0
                && loadedState.ExcludedStudentKeys.Count == 0;
            var workflowOk = imported
                && _state.Students.Count == 3
                && _state.Names.Count == 3
                && _drawButton.Enabled
                && string.Equals(_drawButton.Text, CoreDrawActionText, StringComparison.Ordinal)
                && _absenceButton.Visible
                && _absenceButton.Enabled
                && readyHeaderTexts.SequenceEqual(new[] { "粘贴名单", "导入文件", "本节缺席", "悬浮球", "设置" }, StringComparer.Ordinal)
                && resultText.Contains("已加载 3 名学生", StringComparison.Ordinal)
                && classroomStatusText.Contains("在场 3/3", StringComparison.Ordinal)
                && savedStateReadable;

            if (!workflowOk)
            {
                throw new InvalidOperationException("First-use import workflow self-check failed.");
            }

            return (
                Ok: true,
                ImportedStudentCount: _state.Students.Count,
                ReadyHeaderTexts: string.Join(" / ", readyHeaderTexts),
                MainActionText: _drawButton.Text,
                ResultText: resultText,
                ClassroomStatusText: classroomStatusText,
            SavedStateReadable: savedStateReadable);
        }
        finally
        {
            RestoreAppStateSnapshot(snapshot);
            _hintLabel.Text = originalHint;
            SetResultDisplay(originalResultText, updateFont: true);
            UpdateResetRoundButton();
            UpdateUndoDrawButton();
            UpdateHistoryActionState();
            UpdateRosterActionState();
            UpdateClassroomStatusDisplay();
            SetIdleResultText();
        }
    }

    private (bool Ok,
        bool Started,
        bool Completed,
        string Winner,
        int HistoryCount,
        string ButtonText,
        bool ButtonEnabled,
        int DrawnStudentKeyCount,
        string ResultText) RunCoreDrawWorkflowSelfCheck()
    {
        var snapshot = CaptureAppStateSnapshot();
        var originalHint = _hintLabel.Text;
        var originalResultText = _currentResultText;
        try
        {
            ClearStateForFirstUseSelfCheck();
            var imported = ApplyImportedStudents(
                ParseStudents(new[]
                {
                    "序号\t姓名\t性别\t小组",
                    "1\t自检抽号A\t男\t一组",
                    "2\t自检抽号B\t女\t二组",
                    "3\t自检抽号C\t男\t二组"
                }),
                "核心抽号自检名单",
                "核心抽号自检粘贴名单",
                showSuccessMessage: false);
            if (!imported || _state.Students.Count != 3)
            {
                throw new InvalidOperationException("Core draw workflow self-check could not import its roster.");
            }

            _state.DrawDurationSeconds = 0.15d;
            _state.AvoidRepeatDraw = true;
            _state.AutoCopyResult = false;
            _state.ExcludedStudentKeys.Clear();
            _state.DrawnStudentKeys.Clear();
            _state.DrawnGroupKeys.Clear();
            _state.DrawHistory.Clear();
            _state.LastWinner = string.Empty;
            ClearUndoSnapshot();
            UpdateResetRoundButton();
            UpdateUndoDrawButton();
            UpdateHistoryActionState();
            UpdateRosterActionState();
            UpdateClassroomStatusDisplay();
            SetIdleResultText();

            StartDraw();
            var started = _isDrawing
                && !_drawButton.Enabled
                && string.Equals(_drawButton.Text, DrawingActionText, StringComparison.Ordinal);
            var deadline = DateTime.UtcNow.AddSeconds(3);
            while (_isDrawing && DateTime.UtcNow < deadline)
            {
                Application.DoEvents();
                Thread.Sleep(15);
            }

            Application.DoEvents();
            var completed = !_isDrawing;
            var winner = _state.LastWinner;
            var historyLines = GetVisibleHistoryLines();
            var resultText = _resultLabel.Text.ReplaceLineEndings(" ");
            var returnedReady = _drawButton.Enabled
                && string.Equals(_drawButton.Text, CoreDrawActionText, StringComparison.Ordinal);
            var ok = started
                && completed
                && !string.IsNullOrWhiteSpace(winner)
                && _state.DrawHistory.Count == 1
                && historyLines.Count == 1
                && historyLines[0].Contains(winner, StringComparison.Ordinal)
                && resultText.Contains(winner, StringComparison.Ordinal)
                && returnedReady
                && _state.DrawnStudentKeys.Count == 1;

            if (!ok)
            {
                throw new InvalidOperationException("Core draw workflow self-check failed.");
            }

            return (
                Ok: true,
                Started: started,
                Completed: completed,
                Winner: winner,
                HistoryCount: _state.DrawHistory.Count,
                ButtonText: _drawButton.Text,
                ButtonEnabled: _drawButton.Enabled,
                DrawnStudentKeyCount: _state.DrawnStudentKeys.Count,
                ResultText: resultText);
        }
        finally
        {
            if (_isDrawing)
            {
                _countdownTimer.Stop();
                _rollingTimer.Stop();
                _isDrawing = false;
            }

            RestoreAppStateSnapshot(snapshot);
            _hintLabel.Text = originalHint;
            SetResultDisplay(originalResultText, updateFont: true);
            UpdateResetRoundButton();
            UpdateUndoDrawButton();
            UpdateHistoryActionState();
            UpdateRosterActionState();
            UpdateClassroomStatusDisplay();
            SetIdleResultText();
        }
    }

    private AppStateSnapshot CaptureAppStateSnapshot()
    {
        return new AppStateSnapshot(
            Names: _state.Names.ToList(),
            Students: CloneStudents(_state.Students),
            FileName: _state.FileName,
            SourceFilePath: _state.SourceFilePath,
            RecentRosterFiles: _state.RecentRosterFiles.ToList(),
            PreviousStudents: CloneStudents(_state.PreviousStudents),
            PreviousFileName: _state.PreviousFileName,
            PreviousSourceFilePath: _state.PreviousSourceFilePath,
            LastWinner: _state.LastWinner,
            DrawHistory: _state.DrawHistory.ToList(),
            ResultFontSize: _state.ResultFontSize,
            DrawDurationSeconds: _state.DrawDurationSeconds,
            FloatingShade: _state.FloatingShade,
            AlwaysOnTop: _state.AlwaysOnTop,
            SilentStartup: _state.SilentStartup,
            StartGuideShown: _state.StartGuideShown,
            AvoidRepeatDraw: _state.AvoidRepeatDraw,
            AutoCopyResult: _state.AutoCopyResult,
            ExcludedStudentKeys: _state.ExcludedStudentKeys.ToList(),
            DrawnStudentKeys: _state.DrawnStudentKeys.ToList(),
            DrawnGroupKeys: _state.DrawnGroupKeys.ToList(),
            LastDrawUndoKind: _state.LastDrawUndoKind,
            LastDrawUndoKey: _state.LastDrawUndoKey,
            LastDrawUndoHistoryEntry: _state.LastDrawUndoHistoryEntry,
            LastDrawReplayMode: _state.LastDrawReplayMode,
            LastLessonPackageDirectory: _state.LastLessonPackageDirectory,
            LastExportDirectory: _state.LastExportDirectory);
    }

    private void RestoreAppStateSnapshot(AppStateSnapshot snapshot)
    {
        _state.Names = snapshot.Names.ToList();
        _state.Students = CloneStudents(snapshot.Students);
        _state.FileName = snapshot.FileName;
        _state.SourceFilePath = snapshot.SourceFilePath;
        _state.RecentRosterFiles = snapshot.RecentRosterFiles.ToList();
        _state.PreviousStudents = CloneStudents(snapshot.PreviousStudents);
        _state.PreviousFileName = snapshot.PreviousFileName;
        _state.PreviousSourceFilePath = snapshot.PreviousSourceFilePath;
        _state.LastWinner = snapshot.LastWinner;
        _state.DrawHistory = snapshot.DrawHistory.ToList();
        _state.ResultFontSize = snapshot.ResultFontSize;
        _state.DrawDurationSeconds = snapshot.DrawDurationSeconds;
        _state.FloatingShade = snapshot.FloatingShade;
        _state.AlwaysOnTop = snapshot.AlwaysOnTop;
        _state.SilentStartup = snapshot.SilentStartup;
        _state.StartGuideShown = snapshot.StartGuideShown;
        _state.AvoidRepeatDraw = snapshot.AvoidRepeatDraw;
        _state.AutoCopyResult = snapshot.AutoCopyResult;
        _state.ExcludedStudentKeys = snapshot.ExcludedStudentKeys.ToList();
        _state.DrawnStudentKeys = snapshot.DrawnStudentKeys.ToList();
        _state.DrawnGroupKeys = snapshot.DrawnGroupKeys.ToList();
        _state.LastDrawUndoKind = snapshot.LastDrawUndoKind;
        _state.LastDrawUndoKey = snapshot.LastDrawUndoKey;
        _state.LastDrawUndoHistoryEntry = snapshot.LastDrawUndoHistoryEntry;
        _state.LastDrawReplayMode = snapshot.LastDrawReplayMode;
        _state.LastLessonPackageDirectory = snapshot.LastLessonPackageDirectory;
        _state.LastExportDirectory = snapshot.LastExportDirectory;
    }

    private void ClearStateForFirstUseSelfCheck()
    {
        _state.Names.Clear();
        _state.Students.Clear();
        _state.FileName = "未加载";
        _state.SourceFilePath = string.Empty;
        _state.PreviousStudents.Clear();
        _state.PreviousFileName = string.Empty;
        _state.PreviousSourceFilePath = string.Empty;
        _state.LastWinner = string.Empty;
        _state.DrawHistory.Clear();
        _state.ExcludedStudentKeys.Clear();
        _state.DrawnStudentKeys.Clear();
        _state.DrawnGroupKeys.Clear();
        ClearUndoSnapshot();
    }

    private static void WriteSelfCheckState(string path, AppState state)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
    }

    private static bool TryReadSelfCheckState(string path, out AppState state)
    {
        try
        {
            state = JsonSerializer.Deserialize<AppState>(File.ReadAllText(path, Encoding.UTF8)) ?? new AppState();
            return true;
        }
        catch
        {
            state = new AppState();
            return false;
        }
    }

    private static bool IsRecoveryProbeState(AppState state)
    {
        return state.Students.Count == 2
            && state.Students.Any(student => string.Equals(student.Name, "自检学生A", StringComparison.Ordinal))
            && state.DrawHistory.Count == 1
            && string.Equals(state.LastWinner, "自检学生A", StringComparison.Ordinal)
            && Math.Abs(state.DrawDurationSeconds - 3.0) < 0.001
            && state.AutoCopyResult
            && state.DrawnStudentKeys.Contains("1|自检学生A", StringComparer.Ordinal);
    }

    private string[] GetHeaderSetupButtonTexts()
    {
        if (_pasteListButton.Parent is null
            || !ReferenceEquals(_pasteListButton.Parent, _loadListButton.Parent)
            || !ReferenceEquals(_pasteListButton.Parent, _floatingModeButton.Parent)
            || !ReferenceEquals(_pasteListButton.Parent, _moreActionsButton.Parent))
        {
            return Array.Empty<string>();
        }

        return _pasteListButton.Parent.Controls
            .Cast<Control>()
            .Where(control => ReferenceEquals(control, _pasteListButton)
                || ReferenceEquals(control, _loadListButton)
                || ReferenceEquals(control, _floatingModeButton)
                || ReferenceEquals(control, _moreActionsButton))
            .Select(control => control.Text)
            .ToArray();
    }

    private (bool EmptyHeaderOk,
        bool ReadyHeaderOk,
        string EmptyHeaderVisibleButtonTexts,
        string ReadyHeaderVisibleButtonTexts) RunHeaderDailyActionSelfCheck()
    {
        var originalStudents = CloneStudents(_state.Students);
        var originalNames = _state.Names.ToList();
        var originalExcludedKeys = _state.ExcludedStudentKeys.ToList();
        try
        {
            _state.Students.Clear();
            _state.Names.Clear();
            _state.ExcludedStudentKeys.Clear();
            UpdateRosterActionState();
            var emptyHeaderTexts = GetVisibleHeaderButtonTexts();
            var emptyHeaderOk = emptyHeaderTexts.SequenceEqual(
                new[] { "粘贴名单", "导入文件", "悬浮球", "设置" },
                StringComparer.Ordinal);

            _state.Students = new List<StudentRecord>
            {
                new() { Sequence = "1", Name = "自检学生A", Gender = "男", Group = "一组" },
                new() { Sequence = "2", Name = "自检学生B", Gender = "女", Group = "二组" }
            };
            _state.Names = _state.Students.Select(student => student.DisplayName).ToList();
            _state.ExcludedStudentKeys.Clear();
            UpdateRosterActionState();
            var readyHeaderTexts = GetVisibleHeaderButtonTexts();
            var readyHeaderOk = readyHeaderTexts.SequenceEqual(
                new[] { "粘贴名单", "导入文件", "本节缺席", "悬浮球", "设置" },
                StringComparer.Ordinal);

            return (
                EmptyHeaderOk: emptyHeaderOk,
                ReadyHeaderOk: readyHeaderOk,
                EmptyHeaderVisibleButtonTexts: string.Join(" / ", emptyHeaderTexts),
                ReadyHeaderVisibleButtonTexts: string.Join(" / ", readyHeaderTexts));
        }
        finally
        {
            _state.Students = originalStudents;
            _state.Names = originalNames;
            _state.ExcludedStudentKeys = originalExcludedKeys;
            UpdateRosterActionState();
        }
    }

    private string[] GetVisibleHeaderButtonTexts()
    {
        if (_pasteListButton.Parent is null)
        {
            return Array.Empty<string>();
        }

        return _pasteListButton.Parent.Controls
            .Cast<Control>()
            .Where(control => control.Visible
                && (ReferenceEquals(control, _pasteListButton)
                    || ReferenceEquals(control, _loadListButton)
                    || ReferenceEquals(control, _absenceButton)
                    || ReferenceEquals(control, _floatingModeButton)
                    || ReferenceEquals(control, _moreActionsButton)))
            .Select(control => control.Text)
            .ToArray();
    }

    private (bool Ok, int CaseCount, int StudentCount, string Samples) RunRosterImportParserSelfCheck()
    {
        var cases = new[]
        {
            new RosterImportParserProbe(
                Name: "Excel表格",
                Lines: new[]
                {
                    "序号\t姓名\t性别\t小组",
                    "1\t张三\t男\t第一组",
                    "2\t李四\t女\t第二组",
                    "3\t王五\t男\t第三组"
                },
                ExpectedNames: new[] { "张三", "李四", "王五" },
                ExpectedGenders: new[] { "男", "女", "男" },
                ExpectedGroups: new[] { "一", "二", "三" }),
            new RosterImportParserProbe(
                Name: "普通粘贴名单",
                Lines: new[] { "张三、李四；王五" },
                ExpectedNames: new[] { "张三", "李四", "王五" },
                ExpectedGenders: Array.Empty<string>(),
                ExpectedGroups: Array.Empty<string>()),
            new RosterImportParserProbe(
                Name: "连续编号名单",
                Lines: new[] { "1张三 2李四 3王五" },
                ExpectedNames: new[] { "张三", "李四", "王五" },
                ExpectedGenders: Array.Empty<string>(),
                ExpectedGroups: Array.Empty<string>()),
            new RosterImportParserProbe(
                Name: "乱序表头",
                Lines: new[]
                {
                    "姓名\t学号\t小组\t性别",
                    "张三\t7\t第一组\t男",
                    "李四\t8\t第二组\t女"
                },
                ExpectedNames: new[] { "张三", "李四" },
                ExpectedGenders: new[] { "男", "女" },
                ExpectedGroups: new[] { "一", "二" })
        };

        var totalStudents = 0;
        var samples = new List<string>();
        foreach (var testCase in cases)
        {
            var parseResult = ParseStudents(testCase.Lines);
            var importReport = NormalizeImportedStudents(parseResult.Students);
            if (parseResult.Errors.Count > 0
                || importReport.Students.Count != testCase.ExpectedNames.Length
                || !importReport.Students.Select(student => student.Name).SequenceEqual(testCase.ExpectedNames, StringComparer.Ordinal)
                || (testCase.ExpectedGenders.Length > 0 && !importReport.Students.Select(student => student.Gender).SequenceEqual(testCase.ExpectedGenders, StringComparer.Ordinal))
                || (testCase.ExpectedGroups.Length > 0 && !importReport.Students.Select(student => student.Group).SequenceEqual(testCase.ExpectedGroups, StringComparer.Ordinal)))
            {
                throw new InvalidOperationException($"Roster import parser self-check failed: {testCase.Name}.");
            }

            totalStudents += importReport.Students.Count;
            samples.Add($"{testCase.Name}:{importReport.Students.Count}");
        }

        return (
            Ok: true,
            CaseCount: cases.Length,
            StudentCount: totalStudents,
            Samples: string.Join(" / ", samples));
    }

    private (bool Ok, int StudentPoolCount, int GroupPoolCount, string Hint) RunAvoidRepeatRolloverSelfCheck()
    {
        var originalStudents = CloneStudents(_state.Students);
        var originalNames = _state.Names.ToList();
        var originalExcludedStudentKeys = _state.ExcludedStudentKeys.ToList();
        var originalDrawnStudentKeys = _state.DrawnStudentKeys.ToList();
        var originalDrawnGroupKeys = _state.DrawnGroupKeys.ToList();
        var originalAvoidRepeatDraw = _state.AvoidRepeatDraw;
        var originalHint = _hintLabel.Text;

        try
        {
            var students = new List<StudentRecord>
            {
                new() { Sequence = "1", Name = "自检学生A", Gender = "男", Group = "一组" },
                new() { Sequence = "2", Name = "自检学生B", Gender = "女", Group = "一组" },
                new() { Sequence = "3", Name = "自检学生C", Gender = "男", Group = "二组" }
            };
            _state.Students = CloneStudents(students);
            _state.Names = _state.Students.Select(student => student.DisplayName).ToList();
            _state.ExcludedStudentKeys.Clear();
            _state.AvoidRepeatDraw = true;
            _state.DrawnStudentKeys = _state.Students.Select(GetStudentKey).ToList();
            _state.DrawnGroupKeys.Clear();

            var studentPool = BuildStudentDrawPool(_state.Students);
            var studentRolloverOk = studentPool.Count == _state.Students.Count
                && _state.DrawnStudentKeys.Count == 0
                && _hintLabel.Text.Contains("自动开始新一轮", StringComparison.Ordinal);

            var groups = _state.Students
                .Where(student => !string.IsNullOrWhiteSpace(student.Group))
                .GroupBy(student => student.Group)
                .Select(group => new StudentGroup(group.Key, group.ToList()))
                .ToList();
            _state.DrawnGroupKeys = groups.Select(GetGroupKey).ToList();
            var groupPool = BuildGroupDrawPool(groups);
            var groupRolloverOk = groupPool.Count == groups.Count
                && _state.DrawnGroupKeys.Count == 0
                && _hintLabel.Text.Contains("自动开始新一轮", StringComparison.Ordinal);

            if (!studentRolloverOk || !groupRolloverOk)
            {
                throw new InvalidOperationException("Avoid-repeat rollover self-check failed.");
            }

            return (
                Ok: true,
                StudentPoolCount: studentPool.Count,
                GroupPoolCount: groupPool.Count,
                Hint: _hintLabel.Text);
        }
        finally
        {
            _state.Students = originalStudents;
            _state.Names = originalNames;
            _state.ExcludedStudentKeys = originalExcludedStudentKeys;
            _state.DrawnStudentKeys = originalDrawnStudentKeys;
            _state.DrawnGroupKeys = originalDrawnGroupKeys;
            _state.AvoidRepeatDraw = originalAvoidRepeatDraw;
            _hintLabel.Text = originalHint;
            UpdateResetRoundButton();
            UpdateRosterActionState();
        }
    }

    private void CompleteStartupPresentation()
    {
        if (_startupPresentationCompleted)
        {
            return;
        }

        if (_headlessStartupSelfCheck)
        {
            return;
        }

        _startupRevealWarmupPass = 0;
        _startupRevealStartedAt = DateTime.UtcNow;
        ContinueStartupPresentationWarmup();
    }

    private void ContinueStartupPresentationWarmup()
    {
        if (_startupPresentationCompleted || IsDisposed || Disposing)
        {
            return;
        }

        PrepareNormalSurfaceForReveal();
        _startupRevealWarmupPass++;

        var elapsedMs = (DateTime.UtcNow - _startupRevealStartedAt).TotalMilliseconds;
        var shouldKeepWarming = _startupRevealWarmupPass < StartupRevealWarmupPasses
            || (!IsNormalStartupSurfaceReady() && elapsedMs < StartupRevealMaxWaitMs);
        if (shouldKeepWarming)
        {
            BeginInvoke(new MethodInvoker(ContinueStartupPresentationWarmup));
            return;
        }

        BeginInvoke(new MethodInvoker(FinishStartupPresentation));
    }

    private void FinishStartupPresentation()
    {
        if (_startupPresentationCompleted)
        {
            return;
        }

        _startupPresentationCompleted = true;
        if (_startupSilentRequested && _state.SilentStartup && !HasStartupRecoveryNotice())
        {
            StartSilentFloatingMode();
            return;
        }

        PrimeNormalSurfaceForReveal();
        if (!IsNormalStartupSurfaceReady())
        {
            PrepareNormalSurfaceForReveal();
        }

        ShowInTaskbar = true;
        Opacity = 0;
        StartStartupFadeIn();
    }

    private void StartStartupFadeIn()
    {
        if (IsDisposed || Disposing)
        {
            return;
        }

        _startupFadeTimer.Stop();
        _startupFadeTimer.Start();
    }

    private void StartupFadeTimer_Tick(object? sender, EventArgs e)
    {
        if (IsDisposed || Disposing)
        {
            _startupFadeTimer.Stop();
            return;
        }

        Opacity = Math.Min(1d, Opacity + 0.12d);
        if (Opacity < 1d)
        {
            return;
        }

        _startupFadeTimer.Stop();
        Refresh();
        QueueStartupRecoveryAndGuide();
    }

    private void QueueStartupRecoveryAndGuide()
    {
        if (HasStartupRecoveryNotice())
        {
            BeginInvoke(new MethodInvoker(ShowStartupRecoveryAndGuide));
            return;
        }

        BeginInvoke(new MethodInvoker(ShowStartupResumeHint));
    }

    private bool HasStartupRecoveryNotice()
    {
        return !string.IsNullOrWhiteSpace(_state.RecoveryNotice);
    }

    private void PrepareNormalSurfaceForReveal()
    {
        if (_isFloatingMode || IsDisposed || Disposing)
        {
            return;
        }

        _normalHost.Visible = true;
        _floatingHost.Visible = false;
        CreateStartupControlHandles();
        RefreshNormalStartupLayout();
        CreateControlTree(_normalHost);
        PerformLayoutTree(_normalHost);
        RefreshResultFonts();
        ApplyMainCountdownTypography(_countdownValue.Text);
        _normalStartupLayoutReady = true;
        UpdateControlTree(_normalHost);
    }

    private void CreateStartupControlHandles()
    {
        foreach (var control in GetStartupCriticalControls())
        {
            if (!control.IsDisposed)
            {
                control.CreateControl();
            }
        }
    }

    private bool IsNormalStartupSurfaceReady()
    {
        return _normalHost.IsHandleCreated
            && _cardPanel.IsHandleCreated
            && _resultPanel.IsHandleCreated
            && _resultLabel.IsHandleCreated
            && _countdownValue.IsHandleCreated
            && _fileNameValue.IsHandleCreated
            && _roundProgressValue.IsHandleCreated
            && AreStartupCriticalControlsReady()
            && _normalHost.ClientSize.Width > 0
            && _normalHost.ClientSize.Height > 0
            && _cardPanel.Width >= NormalCardMinimumWidth
            && _cardPanel.Height >= NormalCardMinimumHeight
            && _resultPanel.ClientSize.Width > 0
            && _resultPanel.ClientSize.Height > 0
            && _countdownValue.ClientSize.Width > 0
            && _countdownValue.ClientSize.Height >= MainCountdownMinimumHeight
            && GetMainCountdownLineCount(FormatMainCountdownText("剩余时间: 5.00s")) == 2;
    }

    private Control[] GetStartupCriticalControls()
    {
        return new Control[]
        {
            _normalHost,
            _cardPanel,
            _fileNameValue,
            _countdownValue,
            _roundProgressValue,
            _resultPanel,
            _resultLabel,
            _drawButton,
            _loadListButton,
            _pasteListButton,
            _moreActionsButton,
            _lastWinnerValue,
            _hintLabel
        };
    }

    private bool AreStartupCriticalControlsReady()
    {
        foreach (var control in GetStartupCriticalControls())
        {
            if (control.IsDisposed
                || !control.IsHandleCreated
                || control.Parent is null
                || control.Width <= 0
                || control.Height <= 0)
            {
                return false;
            }
        }

        return true;
    }

    private void PrimeNormalSurfaceForReveal()
    {
        if (_isFloatingMode || IsDisposed || Disposing || _normalHost.Width <= 0 || _normalHost.Height <= 0)
        {
            return;
        }

        PrepareNormalSurfaceForReveal();
    }

    private static void CreateControlTree(Control control)
    {
        if (!control.IsDisposed)
        {
            control.CreateControl();
        }

        foreach (Control child in control.Controls)
        {
            CreateControlTree(child);
        }
    }

    private static void PerformLayoutTree(Control control)
    {
        foreach (Control child in control.Controls)
        {
            PerformLayoutTree(child);
        }

        control.PerformLayout();
    }

    private static void UpdateControlTree(Control control)
    {
        foreach (Control child in control.Controls)
        {
            UpdateControlTree(child);
        }

        if (!control.IsDisposed && control.IsHandleCreated)
        {
            control.Invalidate();
            control.Update();
        }
    }

    private void HandleStateSaveFailed(string path, string message)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired && IsHandleCreated)
        {
            BeginInvoke(new Action(() => HandleStateSaveFailed(path, message)));
            return;
        }

        _hintLabel.Text = "设置暂时无法保存，当前操作可继续；请检查磁盘空间或权限。";

        var now = DateTime.UtcNow;
        var noticeMessage = $"{path}|{message}";
        if (string.Equals(_lastSaveFailureMessage, noticeMessage, StringComparison.Ordinal)
            && (now - _lastSaveFailureNoticeAt).TotalSeconds < 30)
        {
            return;
        }

        _lastSaveFailureMessage = noticeMessage;
        _lastSaveFailureNoticeAt = now;

        var pathText = string.IsNullOrWhiteSpace(path) ? "配置文件位置不可用" : path;
        MessageBox.Show(
            this,
            $"设置暂时无法保存到磁盘。\n\n当前操作可以继续，但本次名单、设置或历史可能不会在下次启动后保留。\n\n配置位置：{pathText}\n错误信息：{message}\n\n请检查磁盘空间、文件权限，或稍后重新尝试。",
            "保存设置失败",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
    }

    private void ShowStartupRecoveryAndGuide()
    {
        if (HasStartupRecoveryNotice())
        {
            _hintLabel.Text = _state.RecoveryNotice.ReplaceLineEndings(" ");
            MessageBox.Show(
                this,
                _state.RecoveryNotice,
                "配置已恢复",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

    }

    private void ShowStartupResumeHint()
    {
        if (_isDrawing || HasStartupRecoveryNotice())
        {
            return;
        }

        var hint = BuildStartupResumeHint();
        if (!string.IsNullOrWhiteSpace(hint))
        {
            _hintLabel.Text = hint;
        }
    }

    private string BuildStartupResumeHint()
    {
        var students = GetStudents();
        if (students.Count == 0)
        {
            return FirstUseHint;
        }

        var activeCount = GetActiveStudents().Count;
        var historyCount = GetVisibleHistoryLines().Count;
        var lastResult = GetCopyableResultText();
        var hasLessonState = historyCount > 0
            || _state.DrawnStudentKeys.Count > 0
            || _state.DrawnGroupKeys.Count > 0
            || _state.ExcludedStudentKeys.Count > 0;
        if (!hasLessonState)
        {
            return $"名单 {students.Count} 人已就绪，在场 {activeCount} 人。可直接抽号；有缺席点“{AbsenceMenuPath}”。";
        }

        var lastText = string.IsNullOrWhiteSpace(lastResult)
            ? string.Empty
            : $"，上次 {TrimForStatusLine(lastResult)}";
        return $"已恢复：名单 {students.Count} 人，在场 {activeCount} 人，历史 {historyCount} 条{lastText}。可继续抽号；换课点“{NewLessonMenuPath}”。";
    }

    private void RefreshNormalStartupLayout()
    {
        if (_isFloatingMode)
        {
            return;
        }

        _normalHost.PerformLayout();
        LayoutNormalCard();
        _cardPanel.PerformLayout();
        RefreshResultFonts();
        _normalHost.Invalidate(true);
    }

    private void BuildTrayIcon()
    {
        _trayIcon.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
        Icon = _trayIcon.Icon;
        _trayIcon.Text = "抽号机";
        _trayIcon.Visible = true;
        _trayIcon.DoubleClick += (_, _) => ShowMainFromTray();

        _trayShowMainMenuItem.Click += (_, _) => ShowMainFromTray();
        _trayFloatingMenuItem.Click += (_, _) => ShowFloatingFromTray();
        _trayStartGuideMenuItem.Click += (_, _) => ShowMainThenStartGuide();
        _trayDrawMenuItem.Click += (_, _) => RunTrayAction(StartDraw);
        _trayDrawMaleMenuItem.Click += (_, _) => RunTrayAction(() => StartGenderDraw("男"));
        _trayDrawFemaleMenuItem.Click += (_, _) => RunTrayAction(() => StartGenderDraw("女"));
        _trayDrawGroupMenuItem.Click += (_, _) => RunTrayAction(DrawGroup);
        _trayUndoDrawMenuItem.Click += (_, _) => UndoLastDraw();
        _trayRedrawMenuItem.Click += (_, _) => RedrawLastDraw();
        ConfigureDurationPresetMenu(_trayDurationMenuItem, _trayDuration3MenuItem, _trayDuration5MenuItem, _trayDuration8MenuItem);
        _trayCopyResultMenuItem.Click += (_, _) => CopyCurrentResult();
        _trayCopyLessonSummaryMenuItem.Click += (_, _) => CopyLessonSummary();
        _trayExportLessonSummaryMenuItem.Click += (_, _) => ShowMainThenExportLessonSummary();
        _trayExportLessonPackageMenuItem.Click += (_, _) => ShowMainThenExportLessonPackage();
        _trayOpenLessonPackageDirectoryMenuItem.Click += (_, _) => ShowMainThenOpenLessonPackageDirectory();
        _trayAutoCopyMenuItem.Click += (_, _) => ToggleAutoCopyResult();
        _trayAvoidRepeatMenuItem.Click += (_, _) => ToggleAvoidRepeat();
        _trayResetRoundMenuItem.Click += (_, _) => ResetAvoidRepeatRound();
        _trayNewLessonMenuItem.Click += (_, _) => ShowMainThenStartNewLesson();
        _trayViewHistoryMenuItem.Click += (_, _) => ShowMainThenViewHistory();
        _trayExportHistoryMenuItem.Click += (_, _) => ShowMainThenExportHistory();
        _trayClearHistoryMenuItem.Click += (_, _) => ShowMainThenClearHistory();
        _trayListOverviewMenuItem.Click += (_, _) => ShowMainThenListOverview();
        _trayUndrawnOverviewMenuItem.Click += (_, _) => ShowMainThenUndrawnOverview();
        _trayCopyUndrawnRosterMenuItem.Click += (_, _) => CopyUndrawnRoster();
        _trayExportUndrawnRosterMenuItem.Click += (_, _) => ExportUndrawnRoster();
        _trayCopyDrawnRosterMenuItem.Click += (_, _) => CopyDrawnRoster();
        _trayExportDrawnRosterMenuItem.Click += (_, _) => ExportDrawnRoster();
        _trayAbsenceMenuItem.Click += (_, _) => ShowMainThenEditAbsences();
        _trayCopyAbsentRosterMenuItem.Click += (_, _) => CopyAbsentRoster();
        _trayExportAbsentRosterMenuItem.Click += (_, _) => ShowMainThenExportAbsentRoster();
        _trayCopyPresentRosterMenuItem.Click += (_, _) => CopyPresentRoster();
        _trayExportPresentRosterMenuItem.Click += (_, _) => ShowMainThenExportPresentRoster();
        _trayCopyRosterMenuItem.Click += (_, _) => CopyCurrentRoster();
        _trayExportRosterMenuItem.Click += (_, _) => ShowMainThenExportRoster();
        _trayPresentationModeMenuItem.Click += (_, _) => ShowMainThenTogglePresentationMode();
        _trayImportMenuItem.Click += (_, _) => ShowMainThenImport();
        _trayReloadFileMenuItem.Click += (_, _) => ShowMainThenReloadCurrentFile();
        _trayOpenSourceFileMenuItem.Click += (_, _) => ShowMainThenOpenCurrentFile();
        _trayPasteListMenuItem.Click += (_, _) => ShowMainThenPasteNames();
        _trayDemoListMenuItem.Click += (_, _) => ShowMainThenLoadDemoRoster();
        _trayRestoreRosterMenuItem.Click += (_, _) => ShowMainThenRestorePreviousRoster();
        _trayQuickGuideMenuItem.Click += (_, _) => ShowMainThenQuickGuide();
        _trayExitMenuItem.Click += (_, _) => ConfirmExitApplication();

        var trayDrawActionsGroup = CreateMenuGroup(
            "抽号操作",
            _trayDrawMaleMenuItem,
            _trayDrawFemaleMenuItem,
            _trayDrawGroupMenuItem,
            new ToolStripSeparator(),
            _trayUndoDrawMenuItem,
            _trayRedrawMenuItem,
            _trayResetRoundMenuItem,
            _trayNewLessonMenuItem,
            new ToolStripSeparator(),
            _trayDurationMenuItem,
            _trayAvoidRepeatMenuItem,
            _trayAutoCopyMenuItem);
        var trayPrepareRosterGroup = CreateMenuGroup(
            "准备名单",
            _trayImportMenuItem,
            _trayPasteListMenuItem,
            _trayDemoListMenuItem,
            _trayRestoreRosterMenuItem,
            _trayRecentRosterMenuItem,
            new ToolStripSeparator(),
            _trayReloadFileMenuItem,
            _trayOpenSourceFileMenuItem);
        var trayRoundRosterGroup = CreateMenuGroup(
            "本节名单",
            _trayAbsenceMenuItem,
            _trayListOverviewMenuItem,
            _trayUndrawnOverviewMenuItem,
            new ToolStripSeparator(),
            _trayCopyUndrawnRosterMenuItem,
            _trayExportUndrawnRosterMenuItem,
            _trayCopyDrawnRosterMenuItem,
            _trayExportDrawnRosterMenuItem,
            new ToolStripSeparator(),
            _trayCopyAbsentRosterMenuItem,
            _trayExportAbsentRosterMenuItem,
            _trayCopyPresentRosterMenuItem,
            _trayExportPresentRosterMenuItem,
            new ToolStripSeparator(),
            _trayCopyRosterMenuItem,
            _trayExportRosterMenuItem);
        var trayRosterGroup = CreateMenuGroup(
            "名单",
            _trayStartGuideMenuItem,
            trayPrepareRosterGroup,
            trayRoundRosterGroup);
        var trayLessonFilesGroup = CreateMenuGroup(
            "课堂文件",
            _trayCopyLessonSummaryMenuItem,
            _trayExportLessonSummaryMenuItem,
            _trayExportLessonPackageMenuItem,
            _trayOpenLessonPackageDirectoryMenuItem);
        var trayHistoryManagementGroup = CreateMenuGroup(
            "历史管理",
            _trayExportHistoryMenuItem,
            _trayClearHistoryMenuItem);
        var trayRecordsGroup = CreateMenuGroup(
            "记录",
            _trayCopyResultMenuItem,
            _trayViewHistoryMenuItem,
            trayLessonFilesGroup,
            trayHistoryManagementGroup);
        var trayDisplayGroup = CreateMenuGroup(
            "显示",
            _trayPresentationModeMenuItem);
        var trayHelpGroup = CreateMenuGroup(
            "帮助",
            _trayQuickGuideMenuItem);

        _trayMenu.Items.AddRange(new ToolStripItem[]
        {
            _trayShowMainMenuItem,
            _trayFloatingMenuItem,
            new ToolStripSeparator(),
            _trayDrawMenuItem,
            trayDrawActionsGroup,
            trayRosterGroup,
            trayRecordsGroup,
            trayDisplayGroup,
            trayHelpGroup,
            new ToolStripSeparator(),
            _trayExitMenuItem
        });
        _trayIcon.ContextMenuStrip = _trayMenu;
    }

    private void HideToTray()
    {
        if (_isPresentationMode)
        {
            ExitPresentationMode();
        }

        Hide();
        Opacity = 1;
        ShowInTaskbar = false;
        if (!_trayHideTipShown)
        {
            _trayHideTipShown = true;
            _trayIcon.ShowBalloonTip(
                2200,
                "抽号机已隐藏",
                "程序仍在运行，可从右下角隐藏图标中打开。",
                ToolTipIcon.Info);
        }
    }

    private void StartSilentFloatingMode()
    {
        Opacity = 0;
        ShowInTaskbar = false;
        if (!_isFloatingMode)
        {
            EnterFloatingMode();
        }

        ShowInTaskbar = false;
        Opacity = 1;
    }

    private void ShowMainFromTray()
    {
        var hideDuringSwitch = Visible && Opacity > 0.01;
        if (hideDuringSwitch)
        {
            Opacity = 0;
        }

        if (_isFloatingMode)
        {
            ExitFloatingMode();
        }

        if (!_normalStartupLayoutReady)
        {
            RefreshNormalStartupLayout();
            _normalStartupLayoutReady = true;
        }

        Show();
        WindowState = FormWindowState.Normal;
        PrepareNormalSurfaceForReveal();
        Opacity = 1;
        Refresh();
        ShowInTaskbar = true;
        Activate();
    }

    private void ShowFloatingFromTray()
    {
        if (!_isFloatingMode)
        {
            Opacity = 0;
            Show();
            EnterFloatingMode();
            ShowInTaskbar = false;
        }
        else
        {
            Show();
            Opacity = 1;
            ShowInTaskbar = false;
        }
    }

    private void RunTrayAction(Action action)
    {
        if (!_isFloatingMode)
        {
            ShowFloatingFromTray();
        }

        action();
    }

    private void ShowMainThenStartGuide()
    {
        ShowMainFromTray();
        ShowStartGuide();
    }

    private void ShowMainThenImport()
    {
        ShowMainFromTray();
        ImportNames();
    }

    private void ShowMainThenReloadCurrentFile()
    {
        ShowMainFromTray();
        ReloadCurrentRosterFile();
    }

    private void ShowMainThenOpenCurrentFile()
    {
        ShowMainFromTray();
        OpenCurrentRosterFile();
    }

    private void ShowMainThenLoadRecentRosterFile(string fileName)
    {
        ShowMainFromTray();
        LoadRecentRosterFile(fileName);
    }

    private void ShowMainThenPasteNames()
    {
        ShowMainFromTray();
        ImportNamesFromClipboard();
    }

    private void ShowMainThenLoadDemoRoster()
    {
        ShowMainFromTray();
        LoadDemoRoster();
    }

    private void ShowMainThenRestorePreviousRoster()
    {
        ShowMainFromTray();
        RestorePreviousRoster();
    }

    private void ShowMainThenStartNewLesson()
    {
        ShowMainFromTray();
        StartNewLesson();
    }

    private void ShowMainThenViewHistory()
    {
        ShowMainFromTray();
        ShowHistoryOverview();
    }

    private void ShowMainThenExportHistory()
    {
        ShowMainFromTray();
        ExportDrawHistory();
    }

    private void ShowMainThenExportLessonSummary()
    {
        ShowMainFromTray();
        ExportLessonSummary();
    }

    private void ShowMainThenExportLessonPackage()
    {
        ShowMainFromTray();
        ExportLessonPackage();
    }

    private void ShowMainThenOpenLessonPackageDirectory()
    {
        ShowMainFromTray();
        OpenLastLessonPackageDirectory();
    }

    private void ShowMainThenClearHistory()
    {
        ShowMainFromTray();
        ClearDrawHistory();
    }

    private void ShowMainThenListOverview()
    {
        ShowMainFromTray();
        ShowListOverview();
    }

    private void ShowMainThenUndrawnOverview()
    {
        ShowMainFromTray();
        ShowListOverview(OverviewStatusFilter.Undrawn);
    }

    private void ShowMainThenEditAbsences()
    {
        ShowMainFromTray();
        EditLessonAbsences();
    }

    private void ShowMainThenTogglePresentationMode()
    {
        ShowMainFromTray();
        TogglePresentationMode();
    }

    private void ShowMainThenExportRoster()
    {
        ShowMainFromTray();
        ExportCurrentRoster();
    }

    private void ShowMainThenExportPresentRoster()
    {
        ShowMainFromTray();
        ExportPresentRoster();
    }

    private void ShowMainThenExportAbsentRoster()
    {
        ShowMainFromTray();
        ExportAbsentRoster();
    }

    private void ShowMainThenQuickGuide()
    {
        ShowMainFromTray();
        ShowQuickGuide();
    }

    private void ConfirmExitApplication()
    {
        if (MessageBox.Show(
                this,
                "确定要退出抽号机吗？\n\n如果正在授课，建议只最小化到隐藏图标区。",
                "退出确认",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) == DialogResult.Yes)
        {
            ExitApplication();
        }
    }

    private void ExitApplication()
    {
        _exitRequested = true;
        Close();
    }

    private void StartSingleInstanceSignalListener()
    {
        if (_showMainEvent is null)
        {
            return;
        }

        Task.Run(() =>
        {
            var handles = new WaitHandle[] { _showMainEvent, _singleInstanceSignalCts.Token.WaitHandle };
            while (!_singleInstanceSignalCts.IsCancellationRequested)
            {
                var index = WaitHandle.WaitAny(handles);
                if (index != 0 || _singleInstanceSignalCts.IsCancellationRequested)
                {
                    return;
                }

                if (!IsDisposed && IsHandleCreated)
                {
                    BeginInvoke(new Action(ShowMainFromTray));
                }
            }
        }, _singleInstanceSignalCts.Token);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _singleInstanceSignalCts.Cancel();
        _singleInstanceSignalCts.Dispose();
        DisposeTrayIcon();
        base.OnFormClosed(e);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_exitRequested && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        DisposeTrayIcon();
        base.OnFormClosing(e);
    }

    private void DisposeTrayIcon()
    {
        if (_trayIconDisposed)
        {
            return;
        }

        _trayIconDisposed = true;
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if (_isFloatingMode)
        {
            using var brush = new SolidBrush(FloatingTransparentKey);
            e.Graphics.FillRectangle(brush, ClientRectangle);
            return;
        }

        using var normalBrush = new LinearGradientBrush(
            ClientRectangle,
            Color.FromArgb(234, 244, 255),
            Color.FromArgb(248, 251, 255),
            90f);
        e.Graphics.FillRectangle(normalBrush, ClientRectangle);
    }

    protected override bool ShowWithoutActivation => _showWithoutActivation || base.ShowWithoutActivation;

    private void BuildLayout()
    {
        BuildNormalLayout();
        BuildFloatingLayout();

        Controls.Add(_floatingHost);
        Controls.Add(_normalHost);
    }

    private void BuildNormalLayout()
    {
        _normalHost.Dock = DockStyle.Fill;
        _normalHost.AutoScroll = true;
        _normalHost.Padding = new Padding(0);
        _normalHost.BackColor = Color.Transparent;

        _cardPanel.Location = new Point(NormalHostPadding, NormalHostPadding);
        _cardPanel.MinimumSize = new Size(NormalCardMinimumWidth, NormalCardMinimumHeight);
        _cardPanel.Padding = new Padding(32);
        _cardPanel.BackColor = Color.FromArgb(245, 250, 255);
        _cardPanel.Paint += (_, e) =>
        {
            using var borderPen = new Pen(Color.FromArgb(210, 223, 236), 1f);
            var rect = new Rectangle(0, 0, _cardPanel.Width - 1, _cardPanel.Height - 1);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = CreateRoundedRectangle(rect, 28);
            using var fillBrush = new SolidBrush(Color.FromArgb(245, 250, 255));
            e.Graphics.FillPath(fillBrush, path);
            e.Graphics.DrawPath(borderPen, path);
        };
        _normalHost.Controls.Add(_cardPanel);
        LayoutNormalCard();

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 1,
            RowCount = 6
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _cardPanel.Controls.Add(layout);

        layout.Controls.Add(BuildHeaderRow(), 0, 0);
        layout.Controls.Add(BuildStatusRow(), 0, 1);

        _resultPanel.Dock = DockStyle.Fill;
        _resultPanel.Margin = new Padding(0, 22, 0, 0);
        _resultPanel.Padding = new Padding(22);
        _resultPanel.MinimumSize = new Size(0, 200);
        _resultPanel.BackColor = Color.FromArgb(233, 243, 255);
        _resultPanel.Resize += (_, _) => RefreshResultFonts();
        _resultPanel.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(205, 223, 244), 1f);
            using var path = CreateRoundedRectangle(
                new Rectangle(0, 0, _resultPanel.Width - 1, _resultPanel.Height - 1),
                24);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var brush = new LinearGradientBrush(
                _resultPanel.ClientRectangle,
                Color.FromArgb(231, 242, 255),
                Color.White,
                135f);
            e.Graphics.FillPath(brush, path);
            e.Graphics.DrawPath(pen, path);
        };

        _resultLabel.Dock = DockStyle.Fill;
        _resultLabel.TextAlign = ContentAlignment.MiddleCenter;
        _resultLabel.ForeColor = Color.FromArgb(17, 38, 61);
        _resultLabel.BackColor = Color.Transparent;
        _resultLabel.AutoSize = false;
        _resultLabel.EnableTextTransition = true;
        _resultLabel.TextTransitionDurationMs = 180;
        _resultPanel.Controls.Add(_resultLabel);
        layout.Controls.Add(_resultPanel, 0, 2);

        var actionRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoSize = true,
            Margin = new Padding(0, 20, 0, 0),
            BackColor = Color.Transparent
        };
        ConfigureButton(_drawButton, CoreDrawActionText, true);
        _drawButton.Click += (_, _) => StartDraw();
        actionRow.Controls.Add(_drawButton);
        ConfigureButton(_drawMaleButton, "抽男生", false);
        _drawMaleButton.Click += (_, _) => StartGenderDraw("男");
        actionRow.Controls.Add(_drawMaleButton);
        ConfigureButton(_drawFemaleButton, "抽女生", false);
        _drawFemaleButton.Click += (_, _) => StartGenderDraw("女");
        actionRow.Controls.Add(_drawFemaleButton);
        ConfigureButton(_drawGroupButton, "抽小组", false);
        _drawGroupButton.Click += (_, _) => DrawGroup();
        actionRow.Controls.Add(_drawGroupButton);

        layout.Controls.Add(actionRow, 0, 3);

        layout.Controls.Add(BuildFooterRow(), 0, 4);

        _hintLabel.Dock = DockStyle.Fill;
        _hintLabel.AutoSize = true;
        _hintLabel.Margin = new Padding(0, 14, 0, 0);
        _hintLabel.ForeColor = Color.FromArgb(88, 113, 140);
        _hintLabel.Font = new Font("Microsoft YaHei UI", 10);
        layout.Controls.Add(_hintLabel, 0, 5);
    }

    private Control BuildHeaderRow()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            AutoSize = true,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var titlePanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            Dock = DockStyle.Fill,
            WrapContents = false,
            AutoSize = true,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        titlePanel.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "抽号机",
            ForeColor = Color.FromArgb(17, 38, 61),
            Font = new Font("Microsoft YaHei UI", 28, FontStyle.Bold)
        });
        titlePanel.Controls.Add(new Label
        {
            AutoSize = true,
            MaximumSize = new Size(720, 0),
            Text = "粘贴或导入名单，然后开始抽号。",
            ForeColor = Color.FromArgb(88, 113, 140),
            Font = new Font("Microsoft YaHei UI", 11)
        });

        var headerButtons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            WrapContents = false,
            Anchor = AnchorStyles.Right | AnchorStyles.Top,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };

        ConfigureButton(_pasteListButton, "粘贴名单", false);
        _pasteListButton.Click += (_, _) => ImportNamesFromClipboard();
        headerButtons.Controls.Add(_pasteListButton);

        ConfigureButton(_loadListButton, "导入文件", false);
        _loadListButton.Click += (_, _) => ImportNames();
        headerButtons.Controls.Add(_loadListButton);

        ConfigureButton(_absenceButton, "本节缺席", false);
        _absenceButton.Click += (_, _) => EditLessonAbsences();
        headerButtons.Controls.Add(_absenceButton);

        ConfigureButton(_floatingModeButton, "悬浮球", false);
        _floatingModeButton.Click += (_, _) => EnterFloatingMode();
        headerButtons.Controls.Add(_floatingModeButton);

        ConfigureButton(_moreActionsButton, "设置", false);
        _moreActionsButton.Click += (_, _) => ShowSettingsDialog();
        headerButtons.Controls.Add(_moreActionsButton);

        header.Controls.Add(titlePanel, 0, 0);
        header.Controls.Add(headerButtons, 1, 0);
        return header;
    }

    private Control BuildStatusRow()
    {
        var statusRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            AutoSize = true,
            MinimumSize = new Size(0, 160),
            Margin = new Padding(0, 24, 0, 0),
            BackColor = Color.Transparent
        };
        statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
        statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 26));
        statusRow.RowStyles.Add(new RowStyle(SizeType.Absolute, 160));

        _fileNameValue.AutoEllipsis = true;
        _fileNameValue.TextAlign = ContentAlignment.MiddleLeft;
        var classroomStatusCard = CreateStatusCard("课堂状态", _fileNameValue);
        _fileNameValue.Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold);
        statusRow.Controls.Add(classroomStatusCard, 0, 0);

        _countdownValue.TextAlign = ContentAlignment.MiddleCenter;
        _countdownValue.AutoSize = false;
        _countdownValue.AutoEllipsis = false;
        _countdownValue.UseMnemonic = false;
        _countdownValue.MinimumSize = new Size(0, MainCountdownMinimumHeight);
        var countdownStatusCard = CreateStatusCard("倒计时", _countdownValue);
        countdownStatusCard.Padding = new Padding(14, 14, 14, 10);
        _countdownValue.Font = CreateMainCountdownFont(12f);
        _countdownValue.Resize += (_, _) => ApplyMainCountdownTypography(_countdownValue.Text);
        statusRow.Controls.Add(countdownStatusCard, 1, 0);

        _roundProgressValue.TextAlign = ContentAlignment.MiddleLeft;
        statusRow.Controls.Add(CreateStatusCard("本轮进度", _roundProgressValue), 2, 0);

        _drawDurationInput.DecimalPlaces = 2;
        _drawDurationInput.Increment = 0.10m;
        _drawDurationInput.Minimum = 0.10m;
        _drawDurationInput.Maximum = 30m;
        _drawDurationInput.Width = 82;
        _drawDurationInput.MinimumSize = new Size(82, 30);
        _drawDurationInput.Font = new Font("Microsoft YaHei UI", 12, FontStyle.Bold);
        _drawDurationInput.TabStop = true;
        _drawDurationInput.ValueChanged += (_, _) => SaveDrawDuration();

        _floatingShadeInput.Minimum = 0;
        _floatingShadeInput.Maximum = 100;
        _floatingShadeInput.TickFrequency = 25;
        _floatingShadeInput.Width = 128;
        _floatingShadeInput.Height = 34;
        _floatingShadeInput.TabStop = true;
        _floatingShadeInput.ValueChanged += (_, _) => SaveFloatingShade();

        return statusRow;
    }

    private Control BuildFooterRow()
    {
        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            AutoSize = true,
            Margin = new Padding(0, 18, 0, 0),
            BackColor = Color.Transparent
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var lastWinnerPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            Dock = DockStyle.Fill,
            WrapContents = false,
            AutoSize = true,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        lastWinnerPanel.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "抽到的人",
            ForeColor = Color.FromArgb(88, 113, 140),
            Font = new Font("Microsoft YaHei UI", 10)
        });

        _lastWinnerValue.Multiline = true;
        _lastWinnerValue.ReadOnly = true;
        _lastWinnerValue.ScrollBars = ScrollBars.None;
        _lastWinnerValue.WordWrap = false;
        _lastWinnerValue.BorderStyle = BorderStyle.None;
        _lastWinnerValue.Width = 520;
        _lastWinnerValue.Height = 52;
        _lastWinnerValue.BackColor = Color.FromArgb(245, 250, 255);
        _lastWinnerValue.ForeColor = Color.FromArgb(17, 38, 61);
        _lastWinnerValue.Font = new Font("Microsoft YaHei UI", 16, FontStyle.Bold);
        lastWinnerPanel.Controls.Add(_lastWinnerValue);
        footer.Controls.Add(lastWinnerPanel, 0, 0);

        return footer;
    }

    private Control BuildAboutRow()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 8, 0, 0)
        };

        ConfigureFooterLinkButton(_aboutButton, "关于");
        _aboutButton.Click += (_, _) => ShowAbout();
        panel.Controls.Add(_aboutButton);

        ConfigureFooterLinkButton(_quickGuideButton, "操作速查");
        _quickGuideButton.Click += (_, _) => ShowQuickGuide();
        panel.Controls.Add(_quickGuideButton);

        return panel;
    }

    private static void ConfigureFooterLinkButton(Button button, string text)
    {
        button.Text = text;
        button.AutoSize = true;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(244, 249, 253);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(235, 243, 249);
        button.BackColor = Color.Transparent;
        button.ForeColor = Color.FromArgb(226, 236, 245);
        button.Font = new Font("Microsoft YaHei UI", 9);
        button.Cursor = Cursors.Hand;
    }
    private void BuildFloatingLayout()
    {
        _floatingHost.Dock = DockStyle.Fill;
        _floatingHost.Visible = false;
        _floatingHost.BackColor = Color.Transparent;
        _floatingHost.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
            if (ShouldShowFloatingToolbar())
            {
                var toolbarBounds = GetFloatingToolbarBounds();
                if (toolbarBounds.Width <= 2)
                {
                    return;
                }

                using var toolbarBrush = new SolidBrush(GetFloatingToolbarColor());
                using var toolbarPath = CreateRoundedRectangle(toolbarBounds, GetToolbarCornerRadius(toolbarBounds));
                using var toolbarPen = new Pen(GetFloatingBorderColor(), 1.6f);
                e.Graphics.FillPath(toolbarBrush, toolbarPath);
                e.Graphics.DrawPath(toolbarPen, toolbarPath);

                if (ShouldShowFloatingGenderShell())
                {
                    var submenuBounds = GetFloatingAnimatedGenderMenuBounds();
                    using var submenuPath = CreateRoundedRectangle(submenuBounds, Math.Min(24, submenuBounds.Width / 2));
                    e.Graphics.FillPath(toolbarBrush, submenuPath);
                    e.Graphics.DrawPath(toolbarPen, submenuPath);

                }

                var statusBounds = GetFloatingStatusBounds(toolbarBounds);
                using var statusPath = CreateRoundedRectangle(InflateRectangle(statusBounds, -1, -1), 9);
                e.Graphics.FillPath(toolbarBrush, statusPath);
            }
        };

        _floatingTitleLabel.AutoSize = false;
        _floatingTitleLabel.Text = "抽号机";
        _floatingTitleLabel.ForeColor = GetFloatingTextColor();
        _floatingTitleLabel.BackColor = Color.Transparent;
        _floatingTitleLabel.TextAlign = ContentAlignment.MiddleCenter;
        _floatingTitleLabel.Font = new Font("Microsoft YaHei UI", 10, FontStyle.Bold);
        _floatingTitleLabel.BorderColor = GetFloatingBorderColor();
        _floatingTitleLabel.UnderlayColor = FloatingTransparentKey;
        _floatingTitleLabel.Click += (_, _) =>
        {
            if (ConsumeFloatingDragClick())
            {
                return;
            }

            if (_floatingIsExpanded)
            {
                CollapseFloatingDock();
            }
            else
            {
                ExpandFloatingDock();
            }
        };
        _floatingHost.Controls.Add(_floatingTitleLabel);

        ConfigureFloatingIconButton(_floatingImportButton, "性别");
        _floatingImportButton.Click += (_, _) =>
        {
            if (!ConsumeFloatingDragClick())
            {
                ToggleFloatingGenderMenu();
            }
        };
        _floatingHost.Controls.Add(_floatingImportButton);

        ConfigureFloatingIconButton(_floatingMaleButton, "男");
        _floatingMaleButton.Visible = false;
        _floatingMaleButton.Click += (_, _) =>
        {
            if (!ConsumeFloatingDragClick())
            {
                SetFloatingGenderMenuVisible(false, animate: false);
                StartGenderDraw("男");
            }
        };
        _floatingHost.Controls.Add(_floatingMaleButton);

        ConfigureFloatingIconButton(_floatingFemaleButton, "女");
        _floatingFemaleButton.Visible = false;
        _floatingFemaleButton.Click += (_, _) =>
        {
            if (!ConsumeFloatingDragClick())
            {
                SetFloatingGenderMenuVisible(false, animate: false);
                StartGenderDraw("女");
            }
        };
        _floatingHost.Controls.Add(_floatingFemaleButton);

        ConfigureFloatingIconButton(_floatingRestoreButton, "小组");
        _floatingRestoreButton.Click += (_, _) =>
        {
            if (!ConsumeFloatingDragClick())
            {
                DrawGroup();
            }
        };
        _floatingHost.Controls.Add(_floatingRestoreButton);

        ConfigureFloatingIconButton(_floatingExitButton, "主界面");
        _floatingExitButton.Click += (_, _) =>
        {
            if (!ConsumeFloatingDragClick())
            {
                ExitFloatingMode();
            }
        };
        _floatingHost.Controls.Add(_floatingExitButton);

        _floatingDrawSurface.Cursor = Cursors.Hand;
        _floatingDrawSurface.BackColor = Color.Transparent;
        _floatingDrawSurface.Resize += (_, _) => RefreshResultFonts();
        _floatingDrawSurface.Paint += (_, e) =>
        {
            using var underlay = new SolidBrush(GetFloatingToolbarColor());
            e.Graphics.FillRectangle(underlay, _floatingDrawSurface.ClientRectangle);
            e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
            e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
            using var brush = new LinearGradientBrush(
                _floatingDrawSurface.ClientRectangle,
                GetFloatingButtonColor(),
                GetFloatingButtonHighlightColor(),
                135f);
            var rect = new RectangleF(2.1f, 2.1f, _floatingDrawSurface.Width - 5.2f, _floatingDrawSurface.Height - 5.2f);
            e.Graphics.FillEllipse(brush, rect);
            using var pen = new Pen(GetFloatingBorderColor(), 1.25f);
            e.Graphics.DrawEllipse(pen, rect);
        };
        _floatingDrawSurface.Click += (_, _) =>
        {
            if (!ConsumeFloatingDragClick())
            {
                StartDraw();
            }
        };
        _floatingHost.Controls.Add(_floatingDrawSurface);

        _floatingResultLabel.Dock = DockStyle.None;
        _floatingResultLabel.TextAlign = ContentAlignment.MiddleCenter;
        _floatingResultLabel.ForeColor = Color.FromArgb(45, 55, 68);
        _floatingResultLabel.BackColor = Color.Transparent;
        _floatingResultLabel.AutoSize = false;
        _floatingResultLabel.Text = "抽号";
        _floatingResultLabel.Click += (_, _) =>
        {
            if (!ConsumeFloatingDragClick())
            {
                StartDraw();
            }
        };
        _floatingDrawSurface.Controls.Add(_floatingResultLabel);

        _floatingCountdownLabel.AutoSize = false;
        _floatingCountdownLabel.TextAlign = ContentAlignment.TopCenter;
        _floatingCountdownLabel.ForeColor = Color.FromArgb(70, 78, 92);
        _floatingCountdownLabel.BackColor = Color.Transparent;
        _floatingCountdownLabel.Font = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Bold);
        _floatingCountdownLabel.EnableTextTransition = true;
        _floatingCountdownLabel.TextTransitionDurationMs = 170;
        _floatingHost.Controls.Add(_floatingCountdownLabel);

        _restoreMenuItem.Click += (_, _) => ExitFloatingMode();
        _floatingStartGuideMenuItem.Click += (_, _) => ShowStartGuide();
        _floatingDrawMenuItem.Click += (_, _) => StartDraw();
        _floatingDrawMaleMenuItem.Click += (_, _) => StartGenderDraw("男");
        _floatingDrawFemaleMenuItem.Click += (_, _) => StartGenderDraw("女");
        _floatingDrawGroupMenuItem.Click += (_, _) => DrawGroup();
        _floatingUndoDrawMenuItem.Click += (_, _) => UndoLastDraw();
        _floatingRedrawMenuItem.Click += (_, _) => RedrawLastDraw();
        ConfigureDurationPresetMenu(_floatingDurationMenuItem, _floatingDuration3MenuItem, _floatingDuration5MenuItem, _floatingDuration8MenuItem);
        _floatingCopyResultMenuItem.Click += (_, _) => CopyCurrentResult();
        _floatingCopyLessonSummaryMenuItem.Click += (_, _) => CopyLessonSummary();
        _floatingExportLessonSummaryMenuItem.Click += (_, _) => ExportLessonSummary();
        _floatingExportLessonPackageMenuItem.Click += (_, _) => ExportLessonPackage();
        _floatingOpenLessonPackageDirectoryMenuItem.Click += (_, _) => OpenLastLessonPackageDirectory();
        _floatingAutoCopyMenuItem.Click += (_, _) => ToggleAutoCopyResult();
        _floatingAvoidRepeatMenuItem.Click += (_, _) => ToggleAvoidRepeat();
        _floatingResetRoundMenuItem.Click += (_, _) => ResetAvoidRepeatRound();
        _floatingNewLessonMenuItem.Click += (_, _) => ShowMainThenStartNewLesson();
        _floatingViewHistoryMenuItem.Click += (_, _) => ShowMainThenViewHistory();
        _floatingExportHistoryMenuItem.Click += (_, _) => ExportDrawHistory();
        _floatingClearHistoryMenuItem.Click += (_, _) => ClearDrawHistory();
        _floatingListOverviewMenuItem.Click += (_, _) => ShowListOverview();
        _floatingUndrawnOverviewMenuItem.Click += (_, _) => ShowMainThenUndrawnOverview();
        _floatingCopyUndrawnRosterMenuItem.Click += (_, _) => CopyUndrawnRoster();
        _floatingExportUndrawnRosterMenuItem.Click += (_, _) => ExportUndrawnRoster();
        _floatingCopyDrawnRosterMenuItem.Click += (_, _) => CopyDrawnRoster();
        _floatingExportDrawnRosterMenuItem.Click += (_, _) => ExportDrawnRoster();
        _floatingAbsenceMenuItem.Click += (_, _) => ShowMainThenEditAbsences();
        _floatingCopyAbsentRosterMenuItem.Click += (_, _) => CopyAbsentRoster();
        _floatingExportAbsentRosterMenuItem.Click += (_, _) => ExportAbsentRoster();
        _floatingCopyPresentRosterMenuItem.Click += (_, _) => CopyPresentRoster();
        _floatingExportPresentRosterMenuItem.Click += (_, _) => ExportPresentRoster();
        _floatingCopyRosterMenuItem.Click += (_, _) => CopyCurrentRoster();
        _floatingExportRosterMenuItem.Click += (_, _) => ExportCurrentRoster();
        _floatingPresentationModeMenuItem.Click += (_, _) => ShowMainThenTogglePresentationMode();
        _importMenuItem.Click += (_, _) => ShowMainThenImport();
        _floatingReloadFileMenuItem.Click += (_, _) => ShowMainThenReloadCurrentFile();
        _floatingOpenSourceFileMenuItem.Click += (_, _) => ShowMainThenOpenCurrentFile();
        _floatingPasteListMenuItem.Click += (_, _) => ShowMainThenPasteNames();
        _floatingDemoListMenuItem.Click += (_, _) => ShowMainThenLoadDemoRoster();
        _floatingRestoreRosterMenuItem.Click += (_, _) => ShowMainThenRestorePreviousRoster();
        _floatingQuickGuideMenuItem.Click += (_, _) => ShowQuickGuide();
        _exitMenuItem.Click += (_, _) => ConfirmExitApplication();
        var floatingDrawActionsGroup = CreateMenuGroup(
            "抽号操作",
            _floatingDrawMaleMenuItem,
            _floatingDrawFemaleMenuItem,
            _floatingDrawGroupMenuItem,
            new ToolStripSeparator(),
            _floatingUndoDrawMenuItem,
            _floatingRedrawMenuItem,
            _floatingResetRoundMenuItem,
            _floatingNewLessonMenuItem,
            new ToolStripSeparator(),
            _floatingDurationMenuItem,
            _floatingAvoidRepeatMenuItem,
            _floatingAutoCopyMenuItem);
        var floatingPrepareRosterGroup = CreateMenuGroup(
            "准备名单",
            _importMenuItem,
            _floatingPasteListMenuItem,
            _floatingDemoListMenuItem,
            _floatingRestoreRosterMenuItem,
            _floatingRecentRosterMenuItem,
            new ToolStripSeparator(),
            _floatingReloadFileMenuItem,
            _floatingOpenSourceFileMenuItem);
        var floatingRoundRosterGroup = CreateMenuGroup(
            "本节名单",
            _floatingAbsenceMenuItem,
            _floatingListOverviewMenuItem,
            _floatingUndrawnOverviewMenuItem,
            new ToolStripSeparator(),
            _floatingCopyUndrawnRosterMenuItem,
            _floatingExportUndrawnRosterMenuItem,
            _floatingCopyDrawnRosterMenuItem,
            _floatingExportDrawnRosterMenuItem,
            new ToolStripSeparator(),
            _floatingCopyAbsentRosterMenuItem,
            _floatingExportAbsentRosterMenuItem,
            _floatingCopyPresentRosterMenuItem,
            _floatingExportPresentRosterMenuItem,
            new ToolStripSeparator(),
            _floatingCopyRosterMenuItem,
            _floatingExportRosterMenuItem);
        var floatingRosterGroup = CreateMenuGroup(
            "名单",
            _floatingStartGuideMenuItem,
            floatingPrepareRosterGroup,
            floatingRoundRosterGroup);
        var floatingLessonFilesGroup = CreateMenuGroup(
            "课堂文件",
            _floatingCopyLessonSummaryMenuItem,
            _floatingExportLessonSummaryMenuItem,
            _floatingExportLessonPackageMenuItem,
            _floatingOpenLessonPackageDirectoryMenuItem);
        var floatingHistoryManagementGroup = CreateMenuGroup(
            "历史管理",
            _floatingExportHistoryMenuItem,
            _floatingClearHistoryMenuItem);
        var floatingRecordsGroup = CreateMenuGroup(
            "记录",
            _floatingCopyResultMenuItem,
            _floatingViewHistoryMenuItem,
            floatingLessonFilesGroup,
            floatingHistoryManagementGroup);
        var floatingDisplayGroup = CreateMenuGroup(
            "显示",
            _floatingPresentationModeMenuItem);
        var floatingHelpGroup = CreateMenuGroup(
            "帮助",
            _floatingQuickGuideMenuItem);

        _floatingMenu.Items.AddRange(new ToolStripItem[]
        {
            _restoreMenuItem,
            new ToolStripSeparator(),
            _floatingDrawMenuItem,
            floatingDrawActionsGroup,
            floatingRosterGroup,
            floatingRecordsGroup,
            floatingDisplayGroup,
            floatingHelpGroup,
            new ToolStripSeparator(),
            _exitMenuItem
        });

        foreach (var control in new Control[] { _floatingHost, _floatingDrawSurface, _floatingResultLabel, _floatingCountdownLabel, _floatingTitleLabel, _floatingImportButton, _floatingMaleButton, _floatingFemaleButton, _floatingRestoreButton, _floatingExitButton })
        {
            control.ContextMenuStrip = _floatingMenu;
        }

        foreach (var control in new Control[]
        {
            _floatingHost,
            _floatingTitleLabel,
            _floatingDrawSurface,
            _floatingResultLabel,
            _floatingCountdownLabel,
            _floatingImportButton,
            _floatingMaleButton,
            _floatingFemaleButton,
            _floatingRestoreButton,
            _floatingExitButton
        })
        {
            WireFloatingDrag(control);
        }
        LayoutFloatingControls();
    }

    private void LayoutNormalCard()
    {
        if (_normalHost.ClientSize.Width <= 0 || _normalHost.ClientSize.Height <= 0)
        {
            return;
        }

        var width = Math.Max(NormalCardMinimumWidth, _normalHost.ClientSize.Width - NormalHostPadding * 2);
        var height = Math.Max(NormalCardMinimumHeight, _normalHost.ClientSize.Height - NormalHostPadding * 2);

        _cardPanel.Size = new Size(width, height);
        _normalHost.AutoScrollMinSize = new Size(width + NormalHostPadding * 2, height + NormalHostPadding * 2);
    }

    private void LayoutFloatingControls()
    {
        if (_floatingHost.Width <= 0 || _floatingHost.Height <= 0)
        {
            return;
        }

        var iconSize = ScaleFloating(FloatingIconSize);
        var logoSize = ScaleFloating(FloatingLogoSize);
        var top = ScaleFloating(7) + GetFloatingTopOffset();
        var actionVisible = ShouldShowFloatingActions();
        var logoX = GetFloatingLogoX();
        var toolbarBounds = GetFloatingFullToolbarBounds();
        var statusBounds = GetFloatingStatusBounds(toolbarBounds);
        var gap = ScaleFloating(8);
        var totalButtonWidth = iconSize * 4 + gap * 3;
        var buttonX = toolbarBounds.X + Math.Max(0, (toolbarBounds.Width - totalButtonWidth) / 2);
        var iconTop = toolbarBounds.Y + (toolbarBounds.Height - iconSize) / 2;

        _floatingTitleLabel.SetBounds(logoX, top, logoSize, logoSize);
        _floatingDrawSurface.SetBounds(buttonX, iconTop, iconSize, iconSize);
        _floatingImportButton.SetBounds(buttonX + (iconSize + gap), iconTop, iconSize, iconSize);
        _floatingRestoreButton.SetBounds(buttonX + (iconSize + gap) * 2, iconTop, iconSize, iconSize);
        _floatingExitButton.SetBounds(buttonX + (iconSize + gap) * 3, iconTop, iconSize, iconSize);
        var genderTop = Math.Max(ScaleFloating(4), toolbarBounds.Top - iconSize - ScaleFloating(12));
        var genderLeft = _floatingImportButton.Left + _floatingImportButton.Width / 2 - iconSize - gap / 2;
        _floatingMaleButton.SetBounds(genderLeft, genderTop, iconSize, iconSize);
        _floatingFemaleButton.SetBounds(genderLeft + iconSize + gap, genderTop, iconSize, iconSize);
        _floatingCountdownLabel.SetBounds(statusBounds.X + ScaleFloating(8), statusBounds.Y + ScaleFloating(3), Math.Max(0, statusBounds.Width - ScaleFloating(16)), Math.Max(0, statusBounds.Height - ScaleFloating(6)));

        SetFloatingActionVisibility(actionVisible);
        LayoutFloatingResultLabel();
        UpdateFloatingRegion();
    }

    private void SetFloatingActionVisibility(bool visible)
    {
        _floatingActionsVisible = visible;
        _floatingDrawSurface.Visible = visible;
        _floatingImportButton.Visible = visible;
        var genderButtonsVisible = visible
            && _floatingGenderMenuVisible
            && _floatingGenderMenuTargetVisible
            && !_floatingGenderAnimationActive
            && _floatingGenderProgress >= 0.98f;
        _floatingMaleButton.Visible = genderButtonsVisible;
        _floatingFemaleButton.Visible = genderButtonsVisible;
        _floatingRestoreButton.Visible = visible;
        _floatingExitButton.Visible = visible;
        _floatingCountdownLabel.Visible = visible;
    }

    private bool ShouldShowFloatingToolbar()
    {
        return _floatingToolbarProgress > 0.02f;
    }

    private bool ShouldShowFloatingActions()
    {
        return _floatingIsExpanded && !_floatingAnimationActive && _floatingToolbarProgress >= 0.98f;
    }

    private bool ShouldShowFloatingGenderShell()
    {
        return _floatingGenderMenuVisible
            && _floatingGenderProgress > 0.02f
            && _floatingToolbarProgress > 0.08f
            && (_floatingIsExpanded || _floatingAnimationActive);
    }

    private int GetFloatingLogoX()
    {
        var margin = ScaleFloating(7);
        return _floatingExpandLeft
            ? Math.Max(margin, GetFloatingDockWidth() - GetFloatingLogoSize() - margin)
            : margin;
    }

    private Rectangle GetFloatingToolbarBounds()
    {
        var fullBounds = GetFloatingFullToolbarBounds();
        var visibleWidth = Math.Max(0, Math.Min(fullBounds.Width, (int)Math.Round(fullBounds.Width * _floatingToolbarProgress)));
        if (_floatingExpandLeft)
        {
            return new Rectangle(fullBounds.Right - visibleWidth, fullBounds.Y, visibleWidth, fullBounds.Height);
        }

        return new Rectangle(fullBounds.X, fullBounds.Y, visibleWidth, fullBounds.Height);
    }

    private Rectangle GetFloatingFullToolbarBounds()
    {
        var top = ScaleFloating(7) + GetFloatingTopOffset();
        return _floatingExpandLeft
            ? new Rectangle(ScaleFloating(8), top, GetFloatingDockWidth() - GetFloatingLogoSize() - ScaleFloating(22), GetFloatingToolbarHeight())
            : new Rectangle(ScaleFloating(78), top, GetFloatingDockWidth() - ScaleFloating(88), GetFloatingToolbarHeight());
    }

    private static int GetToolbarCornerRadius(Rectangle bounds)
    {
        return Math.Max(1, Math.Min(bounds.Height / 2, bounds.Width / 2));
    }

    private Rectangle GetFloatingStatusBounds(Rectangle toolbarBounds)
    {
        return new Rectangle(
            toolbarBounds.X + ScaleFloating(10),
            toolbarBounds.Bottom + ScaleFloating(1),
            Math.Max(1, toolbarBounds.Width - ScaleFloating(20)),
            Math.Max(1, ScaleFloating(_floatingStatusHeight)));
    }

    private int GetFloatingTopOffset()
    {
        return _floatingReserveGenderSpace ? ScaleFloating(FloatingGenderTopOffset) : 0;
    }

    private Rectangle GetFloatingGenderMenuBounds()
    {
        if (!_floatingGenderMenuVisible)
        {
            return Rectangle.Empty;
        }

        var left = Math.Min(_floatingMaleButton.Left, _floatingFemaleButton.Left) - ScaleFloating(8);
        var top = Math.Min(_floatingMaleButton.Top, _floatingFemaleButton.Top) - ScaleFloating(7);
        var right = Math.Max(_floatingMaleButton.Right, _floatingFemaleButton.Right) + ScaleFloating(8);
        var bottom = Math.Max(_floatingMaleButton.Bottom, _floatingFemaleButton.Bottom) + ScaleFloating(7);
        return new Rectangle(left, top, right - left, bottom - top);
    }

    private Rectangle GetFloatingAnimatedGenderMenuBounds()
    {
        var fullBounds = GetFloatingGenderMenuBounds();
        if (fullBounds.IsEmpty)
        {
            return Rectangle.Empty;
        }

        var eased = EaseOutCubic(_floatingGenderProgress);
        var width = Math.Max(2, (int)Math.Round(fullBounds.Width * eased));
        var left = fullBounds.X + (fullBounds.Width - width) / 2;
        if (_floatingAnimationActive && !_floatingIsExpanded)
        {
            left = _floatingExpandLeft ? fullBounds.Right - width : fullBounds.X;
        }

        return new Rectangle(
            left,
            fullBounds.Y,
            width,
            fullBounds.Height);
    }

    private static float EaseOutCubic(float value)
    {
        value = Math.Max(0f, Math.Min(1f, value));
        var inverse = 1f - value;
        return 1f - inverse * inverse * inverse;
    }

    private static Rectangle InflateRectangle(Rectangle rectangle, int dx, int dy)
    {
        rectangle.Inflate(dx, dy);
        return rectangle;
    }

    private void LayoutFloatingResultLabel()
    {
        _floatingResultLabel.SetBounds(ScaleFloating(3), 0, Math.Max(0, _floatingDrawSurface.Width - ScaleFloating(6)), _floatingDrawSurface.Height);
    }

    private float GetFloatingScale()
    {
        var scale = Math.Max(1f, DeviceDpi / 96f);
        if (IsHandleCreated)
        {
            using var graphics = CreateGraphics();
            scale = Math.Max(scale, graphics.DpiX / 96f);
        }

        return Math.Min(FloatingScaleMax, scale);
    }

    private int ScaleFloating(int value)
    {
        return Math.Max(1, (int)Math.Round(value * GetFloatingScale()));
    }

    private int ScaleFloating(float value)
    {
        return Math.Max(1, (int)Math.Round(value * GetFloatingScale()));
    }

    private int GetFloatingDockWidth()
    {
        return ScaleFloating(FloatingDockWidth);
    }

    private int GetFloatingCollapsedWidth()
    {
        return ScaleFloating(FloatingCollapsedWidth);
    }

    private int GetFloatingToolbarHeight()
    {
        return ScaleFloating(FloatingToolbarHeight);
    }

    private int GetFloatingLogoSize()
    {
        return ScaleFloating(FloatingLogoSize);
    }

    private int GetFloatingIconSize()
    {
        return ScaleFloating(FloatingIconSize);
    }

    internal static GraphicsPath CreateRoundedRectangleForButton(Rectangle rect, int radius)
    {
        return CreateRoundedRectangle(rect, radius);
    }

    private static GraphicsPath CreateRoundedRectangle(Rectangle rect, int radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static void ApplyCircleRegion(Control control)
    {
        using var path = new GraphicsPath();
        path.AddEllipse(new Rectangle(0, 0, control.Width, control.Height));
        control.Region = new Region(path);
    }

    private static void ApplyRoundedRegion(Control control, int radius)
    {
        using var path = CreateRoundedRectangle(new Rectangle(0, 0, control.Width, control.Height), radius);
        control.Region = new Region(path);
    }

    private Panel CreateStatusCard(string title, Control valueControl)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            Margin = new Padding(0, 0, 16, 0),
            MinimumSize = new Size(0, 96),
            BackColor = Color.FromArgb(241, 247, 252)
        };
        panel.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(214, 225, 236), 1f);
            using var path = CreateRoundedRectangle(new Rectangle(0, 0, panel.Width - 1, panel.Height - 1), 18);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(Color.FromArgb(241, 247, 252));
            e.Graphics.FillPath(brush, path);
            e.Graphics.DrawPath(pen, path);
        };

        var contentLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 1,
            RowCount = 2
        };
        contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        contentLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        contentLayout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Text = title,
            ForeColor = Color.FromArgb(88, 113, 140),
            Font = new Font("Microsoft YaHei UI", 10),
            Margin = new Padding(0, 0, 0, 8)
        }, 0, 0);

        valueControl.Dock = DockStyle.Fill;
        valueControl.Margin = new Padding(0);
        valueControl.Font = new Font("Microsoft YaHei UI", 13, FontStyle.Bold);
        valueControl.ForeColor = Color.FromArgb(17, 38, 61);
        contentLayout.Controls.Add(valueControl, 0, 1);

        panel.Controls.Add(contentLayout);

        return panel;
    }

    private void ConfigureButton(Button button, string text, bool primary)
    {
        button.AutoSize = true;
        button.Text = text;
        button.AccessibleName = text;
        button.AccessibleDescription = text;
        button.FlatStyle = FlatStyle.Flat;
        button.Font = new Font("Microsoft YaHei UI", 11, FontStyle.Bold);
        button.Padding = new Padding(18, 10, 18, 10);
        button.BackColor = primary ? Color.FromArgb(13, 99, 201) : Color.FromArgb(231, 237, 245);
        button.ForeColor = primary ? Color.White : Color.FromArgb(17, 38, 61);
        button.UseVisualStyleBackColor = false;
        button.Cursor = Cursors.Hand;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = button.BackColor;
        button.FlatAppearance.MouseDownBackColor = button.BackColor;
        if (button is RoundedButton roundedButton)
        {
            roundedButton.Radius = 14;
            roundedButton.HoverBackColor = primary ? Color.FromArgb(10, 84, 176) : Color.FromArgb(218, 226, 238);
            roundedButton.PressedBackColor = primary ? Color.FromArgb(8, 68, 148) : Color.FromArgb(204, 214, 228);
        }
    }

    private void ConfigureSquareButton(Button button, string text)
    {
        button.Text = text;
        button.Width = 44;
        button.Height = 44;
        button.FlatStyle = FlatStyle.Flat;
        button.Font = new Font("Microsoft YaHei UI", 14, FontStyle.Bold);
        button.BackColor = Color.FromArgb(237, 243, 249);
        button.ForeColor = Color.FromArgb(17, 38, 61);
        button.UseVisualStyleBackColor = false;
        button.Cursor = Cursors.Hand;
        button.Margin = new Padding(8, 0, 8, 0);
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = button.BackColor;
        button.FlatAppearance.MouseDownBackColor = button.BackColor;
        if (button is RoundedButton roundedButton)
        {
            roundedButton.Radius = 12;
            roundedButton.HoverBackColor = Color.FromArgb(224, 232, 242);
            roundedButton.PressedBackColor = Color.FromArgb(207, 218, 232);
        }
    }

    private void ConfigureDurationPresetMenu(
        ToolStripMenuItem menuItem,
        ToolStripMenuItem threeSecondItem,
        ToolStripMenuItem fiveSecondItem,
        ToolStripMenuItem eightSecondItem)
    {
        threeSecondItem.Click += (_, _) => SetDrawDurationPreset(3m);
        fiveSecondItem.Click += (_, _) => SetDrawDurationPreset(5m);
        eightSecondItem.Click += (_, _) => SetDrawDurationPreset(8m);
        menuItem.DropDownItems.AddRange(new ToolStripItem[]
        {
            threeSecondItem,
            fiveSecondItem,
            eightSecondItem
        });
    }

    private void ConfigureDurationPresetButton(Button button, string text, decimal seconds)
    {
        button.Text = text;
        button.Width = 38;
        button.Height = 30;
        button.FlatStyle = FlatStyle.Flat;
        button.Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold);
        button.BackColor = Color.FromArgb(226, 235, 245);
        button.ForeColor = Color.FromArgb(17, 38, 61);
        button.UseVisualStyleBackColor = false;
        button.Cursor = Cursors.Hand;
        button.Margin = new Padding(5, 0, 0, 2);
        button.TabStop = false;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = button.BackColor;
        button.FlatAppearance.MouseDownBackColor = button.BackColor;
        if (button is RoundedButton roundedButton)
        {
            roundedButton.Radius = 9;
            roundedButton.HoverBackColor = Color.FromArgb(211, 224, 239);
            roundedButton.PressedBackColor = Color.FromArgb(193, 211, 231);
        }

        button.Click += (_, _) => SetDrawDurationPreset(seconds);
    }

    private static ToolStripMenuItem CreateMenuGroup(string text, params ToolStripItem[] items)
    {
        var group = new ToolStripMenuItem(text);
        group.DropDownItems.AddRange(items);
        return group;
    }

    private void ConfigureFloatingIconButton(FloatingIconButton button, string text)
    {
        button.Text = text;
        button.Width = GetFloatingIconSize();
        button.Height = GetFloatingIconSize();
        button.Font = new Font("Microsoft YaHei UI", 8f, FontStyle.Bold);
        button.BackColor = GetFloatingButtonColor();
        button.ForeColor = GetFloatingTextColor();
        button.UnderlayColor = GetFloatingToolbarColor();
        button.Cursor = Cursors.Hand;
        button.TextAlign = ContentAlignment.MiddleCenter;
        button.HoverBackColor = GetFloatingButtonHoverColor();
        button.PressedBackColor = GetFloatingButtonPressedColor();
        button.BorderColor = GetFloatingBorderColor();
        button.Margin = new Padding(0);
    }

    private void ApplyFloatingFonts()
    {
        SetControlFont(_floatingTitleLabel, 9.2f, FontStyle.Bold);
        SetControlFont(_floatingResultLabel, _isDrawing ? 8f : 9f, FontStyle.Bold);
        SetControlFont(_floatingCountdownLabel, 8.2f, FontStyle.Bold);

        foreach (var button in new[] { _floatingImportButton, _floatingMaleButton, _floatingFemaleButton, _floatingRestoreButton, _floatingExitButton })
        {
            SetControlFont(button, 7.4f, FontStyle.Bold);
        }
    }

    private static void SetControlFont(Control control, float size, FontStyle style)
    {
        const string fontFamily = "Microsoft YaHei UI";
        if (control.Font.FontFamily.Name == fontFamily
            && Math.Abs(control.Font.Size - size) < 0.05f
            && control.Font.Style == style)
        {
            return;
        }

        control.Font = new Font(fontFamily, size, style);
    }

    private void WireFontButton(Button button, bool increase)
    {
        button.Click += (_, _) => AdjustFontSize(increase);
        button.MouseDown += (_, _) =>
        {
            _fontIncreaseDirection = increase;
            _fontRepeatTimer.Start();
        };
        button.MouseUp += (_, _) => _fontRepeatTimer.Stop();
        button.MouseLeave += (_, _) => _fontRepeatTimer.Stop();
    }

    private void WireFloatingDrag(Control control)
    {
        control.MouseDown += FloatingDrag_MouseDown;
        control.MouseMove += FloatingDrag_MouseMove;
        control.MouseUp += FloatingDrag_MouseUp;
    }

    private void FloatingAnimationTimer_Tick(object? sender, EventArgs e)
    {
        var diff = _floatingToolbarTargetProgress - _floatingToolbarProgress;
        if (Math.Abs(diff) <= 0.025f)
        {
            _floatingAnimationTimer.Stop();
            _floatingToolbarProgress = _floatingToolbarTargetProgress;
            _floatingAnimationActive = false;
            LayoutFloatingControls();
            UpdateFloatingRegion();
            SetFloatingActionVisibility(_floatingIsExpanded);
            _floatingHost.Invalidate();
            CompleteFloatingDirectionSwitchIfNeeded();
            return;
        }

        var step = Math.Max(0.055f, Math.Abs(diff) * 0.28f);
        _floatingToolbarProgress += Math.Sign(diff) * step;
        _floatingToolbarProgress = Math.Max(0f, Math.Min(1f, _floatingToolbarProgress));
        LayoutFloatingControls();
        UpdateFloatingRegion();
        _floatingHost.Invalidate();
    }

    private void FloatingStatusAnimationTimer_Tick(object? sender, EventArgs e)
    {
        var diff = _floatingTargetStatusHeight - _floatingStatusHeight;
        if (Math.Abs(diff) <= 1.2f)
        {
            _floatingStatusAnimationTimer.Stop();
            _floatingStatusHeight = _floatingTargetStatusHeight;
            ApplyFloatingWindowHeight();
            LayoutFloatingControls();
            UpdateFloatingRegion();
            _floatingHost.Invalidate();
            return;
        }

        var step = Math.Max(1.8f, Math.Abs(diff) * 0.22f);
        _floatingStatusHeight += Math.Sign(diff) * step;
        ApplyFloatingWindowHeight();
        LayoutFloatingControls();
        UpdateFloatingRegion();
        _floatingHost.Invalidate();
    }

    private void FloatingGenderAnimationTimer_Tick(object? sender, EventArgs e)
    {
        var target = _floatingGenderMenuTargetVisible ? 1f : 0f;
        var diff = target - _floatingGenderProgress;
        if (Math.Abs(diff) <= 0.025f)
        {
            _floatingGenderAnimationTimer.Stop();
            _floatingGenderProgress = target;
            _floatingGenderAnimationActive = false;
            if (!_floatingGenderMenuTargetVisible)
            {
                _floatingGenderMenuVisible = false;
            }

            LayoutFloatingControls();
            UpdateFloatingRegion();
            SetFloatingActionVisibility(ShouldShowFloatingActions());
            _floatingHost.Invalidate();
            return;
        }

        var step = Math.Max(0.06f, Math.Abs(diff) * 0.28f);
        _floatingGenderProgress += Math.Sign(diff) * step;
        _floatingGenderProgress = Math.Max(0f, Math.Min(1f, _floatingGenderProgress));
        LayoutFloatingControls();
        UpdateFloatingRegion();
        _floatingHost.Invalidate();
    }

    private void ExpandFloatingDock()
    {
        var logoScreenX = Left + GetFloatingLogoX();
        _floatingExpandLeft = ShouldExpandFloatingLeft();
        AnchorFloatingWindowToLogo(logoScreenX);
        _floatingIsExpanded = true;
        StartFloatingAnimation(1f);
    }

    private void CollapseFloatingDock()
    {
        SetFloatingGenderMenuVisible(false);
        _floatingIsExpanded = false;
        SetFloatingActionVisibility(false);
        StartFloatingAnimation(0f);
    }

    private void StartFloatingAnimation(float targetProgress)
    {
        _floatingToolbarTargetProgress = Math.Max(0f, Math.Min(1f, targetProgress));
        _floatingAnimationActive = true;
        SetFloatingActionVisibility(false);
        ApplyFloatingWindowHeight();
        LayoutFloatingControls();
        UpdateFloatingRegion();
        _floatingHost.Invalidate();
        _floatingAnimationTimer.Start();
    }

    private void CompleteFloatingDirectionSwitchIfNeeded()
    {
        if (!_floatingDirectionSwitchPending || _floatingToolbarTargetProgress > 0.01f)
        {
            return;
        }

        _floatingDirectionSwitchPending = false;
        var logoScreenX = Left + GetFloatingLogoX();
        _floatingExpandLeft = _floatingDirectionSwitchTargetLeft;
        AnchorFloatingWindowToLogo(logoScreenX);
        SyncFloatingDragAnchor();
        _floatingIsExpanded = true;
        StartFloatingAnimation(1f);
    }

    private void SyncFloatingDragAnchor()
    {
        if (!_floatingDragActive)
        {
            return;
        }

        _floatingDragStartCursor = Cursor.Position;
        _floatingDragStartLocation = Location;
    }

    private void AnchorFloatingWindowToLogo(int logoScreenX)
    {
        var workingArea = Screen.FromControl(this).WorkingArea;
        var targetX = _floatingExpandLeft
            ? logoScreenX - (GetFloatingDockWidth() - GetFloatingLogoSize() - ScaleFloating(7))
            : logoScreenX - ScaleFloating(7);
        targetX = Math.Max(workingArea.Left, Math.Min(workingArea.Right - GetFloatingDockWidth(), targetX));
        Bounds = new Rectangle(targetX, Location.Y, GetFloatingDockWidth(), GetFloatingWindowHeight());
    }

    private bool ShouldExpandFloatingLeft()
    {
        var workingArea = Screen.FromControl(this).WorkingArea;
        var centerX = Left + GetFloatingLogoX() + GetFloatingLogoSize() / 2;
        return centerX >= workingArea.Left + workingArea.Width / 2;
    }

    private void UpdateFloatingRegion()
    {
        if (!_isFloatingMode || Width <= 0 || Height <= 0)
        {
            return;
        }

        const int pad = 0;
        using var path = new GraphicsPath();
        path.AddEllipse(new Rectangle(
            GetFloatingLogoX() - pad,
            ScaleFloating(5) + GetFloatingTopOffset(),
            GetFloatingLogoSize() + pad * 2,
            GetFloatingLogoSize() + pad * 2));

        if (ShouldShowFloatingToolbar())
        {
            var toolbarBounds = GetFloatingToolbarBounds();
            if (toolbarBounds.Width > 2)
            {
                var toolbarRegionBounds = InflateRectangle(toolbarBounds, pad, pad);
                using var toolbarPath = CreateRoundedRectangle(
                    toolbarRegionBounds,
                    Math.Max(1, Math.Min(GetToolbarCornerRadius(toolbarBounds) + pad, Math.Min(toolbarRegionBounds.Width, toolbarRegionBounds.Height) / 2)));
                path.AddPath(toolbarPath, false);
                if (ShouldShowFloatingGenderShell())
                {
                    var genderBounds = GetFloatingAnimatedGenderMenuBounds();
                    if (!genderBounds.IsEmpty)
                    {
                        var genderRegionBounds = InflateRectangle(genderBounds, pad, pad);
                        using var genderPath = CreateRoundedRectangle(
                            genderRegionBounds,
                            Math.Max(1, Math.Min(Math.Min(genderBounds.Height / 2 + pad, genderBounds.Width / 2 + pad), Math.Min(genderRegionBounds.Width, genderRegionBounds.Height) / 2)));
                        path.AddPath(genderPath, false);
                    }
                }

                var statusBounds = GetFloatingStatusBounds(toolbarBounds);
                var statusRegionBounds = InflateRectangle(statusBounds, pad, pad);
                using var statusPath = CreateRoundedRectangle(
                    statusRegionBounds,
                    Math.Max(1, Math.Min(ScaleFloating(10), Math.Min(statusRegionBounds.Width, statusRegionBounds.Height) / 2)));
                path.AddPath(statusPath, false);
            }
        }

        var oldRegion = Region;
        Region = new Region(path);
        oldRegion?.Dispose();
    }

    private void FloatingDrag_MouseDown(object? sender, MouseEventArgs e)
    {
        if (!_isFloatingMode || e.Button != MouseButtons.Left)
        {
            return;
        }

        _floatingDragActive = true;
        _floatingDragMoved = false;
        _floatingDragStartCursor = Cursor.Position;
        _floatingDragStartLocation = Location;
    }

    private void FloatingDrag_MouseMove(object? sender, MouseEventArgs e)
    {
        if (!_floatingDragActive)
        {
            return;
        }

        var cursorPosition = Cursor.Position;
        var deltaX = cursorPosition.X - _floatingDragStartCursor.X;
        var deltaY = cursorPosition.Y - _floatingDragStartCursor.Y;
        if (Math.Abs(deltaX) > 3 || Math.Abs(deltaY) > 3)
        {
            _floatingDragMoved = true;
        }

        Location = ClampFloatingLocation(new Point(_floatingDragStartLocation.X + deltaX, _floatingDragStartLocation.Y + deltaY));
        if (UpdateFloatingExpandDirectionWhileDragging())
        {
            _floatingDragStartCursor = cursorPosition;
            _floatingDragStartLocation = Location;
        }
    }

    private bool UpdateFloatingExpandDirectionWhileDragging()
    {
        if (!_floatingIsExpanded || _floatingAnimationActive || _floatingToolbarProgress < 0.98f)
        {
            return false;
        }

        var nextExpandLeft = ShouldExpandFloatingLeft();
        if (nextExpandLeft == _floatingExpandLeft || (_floatingDirectionSwitchPending && nextExpandLeft == _floatingDirectionSwitchTargetLeft))
        {
            return false;
        }

        _floatingDirectionSwitchPending = true;
        _floatingDirectionSwitchTargetLeft = nextExpandLeft;
        _floatingIsExpanded = false;
        SetFloatingActionVisibility(false);
        StartFloatingAnimation(0f);
        return true;
    }

    private void FloatingDrag_MouseUp(object? sender, MouseEventArgs e)
    {
        _floatingDragActive = false;
        Location = ClampFloatingLocation(Location);
    }

    private Point ClampFloatingLocation(Point location)
    {
        var workingArea = Screen.FromPoint(Cursor.Position).WorkingArea;
        var height = GetFloatingWindowHeight();
        return new Point(
            Math.Max(workingArea.Left, Math.Min(workingArea.Right - GetFloatingDockWidth(), location.X)),
            Math.Max(workingArea.Top, Math.Min(workingArea.Bottom - height, location.Y)));
    }

    private int GetFloatingWindowHeight()
    {
        return ScaleFloating(7)
            + GetFloatingTopOffset()
            + GetFloatingToolbarHeight()
            + ScaleFloating(1)
            + ScaleFloating(Math.Max(FloatingStatusMinHeight, _floatingStatusHeight))
            + ScaleFloating(4);
    }

    private void ApplyFloatingWindowHeight()
    {
        if (!_isFloatingMode)
        {
            return;
        }

        var height = GetFloatingWindowHeight();
        MinimumSize = new Size(GetFloatingDockWidth(), height);
        MaximumSize = new Size(GetFloatingDockWidth(), height);
        if (Height != height)
        {
            Bounds = new Rectangle(Location.X, Location.Y, GetFloatingDockWidth(), height);
            Location = ClampFloatingLocation(Location);
        }
    }

    private bool ConsumeFloatingDragClick()
    {
        if (!_floatingDragMoved)
        {
            return false;
        }

        _floatingDragMoved = false;
        return true;
    }

    private void ApplyStateToUi()
    {
        MigrateLegacyNames();
        NormalizeRecentRosterFiles();
        if (PruneExcludedStudentKeys(saveChanges: false))
        {
            _state.Save();
        }

        UpdateClassroomStatusDisplay();
        _lastWinnerValue.Text = string.IsNullOrWhiteSpace(_state.LastWinner) ? "暂无记录" : _state.LastWinner;
        _drawDurationInput.Value = ClampDrawDuration((decimal)_state.DrawDurationSeconds);
        _floatingShadeInput.Value = ClampFloatingShade(_state.FloatingShade);
        _floatingShadeValue.Text = $"{_floatingShadeInput.Value}%";
        _fontSizeValue.Text = $"{_state.ResultFontSize:0}px";
        UpdateHistoryDisplay();
        TopMost = _state.AlwaysOnTop;
        UpdateTopMostButton();
        UpdatePresentationModeButton();
        UpdateStartupButton();
        UpdateSilentStartupButton();
        UpdateAvoidRepeatButton();
        UpdateAutoCopyButton();
        UpdateDurationPresetMenuState();
        UpdateResetRoundButton();
        UpdateUndoDrawButton();
        UpdateHistoryActionState();
        UpdateRosterActionState();
        ApplyFloatingPalette();
        UpdateFontButtons();
        SetIdleResultText();
        SetCountdownDisplay("等待开始", Color.FromArgb(88, 113, 140));
    }

    private void ImportNames()
    {
        if (_isDrawing)
        {
            MessageBox.Show(this, "抽号进行中，结束后再导入名单。", "暂不能导入", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new OpenFileDialog
        {
            Filter = "名单文件 (*.txt;*.csv;*.xlsx)|*.txt;*.csv;*.xlsx|文本文件 (*.txt)|*.txt|CSV 文件 (*.csv)|*.csv|Excel 工作簿 (*.xlsx)|*.xlsx",
            Title = "选择名单文件",
            InitialDirectory = GetPreferredRosterDirectory()
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        ImportNamesFromFile(dialog.FileName);
    }

    private void ImportNamesFromFile(string fileName, bool isReload = false)
    {
        if (_isDrawing)
        {
            _hintLabel.Text = "抽号进行中，结束后再导入名单。";
            return;
        }

        if (!File.Exists(fileName))
        {
            MessageBox.Show(this, "导入失败：文件不存在。", "导入失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!IsSupportedImportFile(fileName))
        {
            MessageBox.Show(this, "导入失败：请使用 .txt、.csv 或 .xlsx 名单文件。\n\n可点“名单模板”保存标准 Excel 模板，把学生复制进去后再导入。", "导入失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string[] lines;
        try
        {
            lines = ReadImportLines(fileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"导入失败：无法读取文件。\n\n{ex.Message}", "导入失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        ApplyImportedStudents(
            ParseStudents(lines),
            Path.GetFileName(fileName),
            isReload ? "重载文件" : GetImportFormatName(fileName),
            Path.GetFullPath(fileName),
            preserveLessonAbsences: isReload);
    }

    private void ReloadCurrentRosterFile()
    {
        if (_isDrawing)
        {
            _hintLabel.Text = "抽号进行中，结束后再重载名单文件。";
            MessageBox.Show(this, "抽号进行中，结束后再重载当前名单文件。", "暂不能重载", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!CanReloadCurrentRosterFile())
        {
            _hintLabel.Text = "暂无可重载的当前名单文件。";
            MessageBox.Show(this, "暂无可重载的当前名单文件。请先从 .txt、.csv 或 .xlsx 文件导入名单。", "重载文件", MessageBoxButtons.OK, MessageBoxIcon.Information);
            UpdateRosterActionState();
            return;
        }

        ImportNamesFromFile(_state.SourceFilePath, isReload: true);
    }

    private void OpenCurrentRosterFile()
    {
        if (CanOpenCurrentRosterFile())
        {
            OpenLinkedRosterFile();
            return;
        }

        if (CanExportRosterForEditing())
        {
            ExportCurrentRosterForEditing();
            return;
        }

        _hintLabel.Text = "暂无可打开的当前名单文件。";
        MessageBox.Show(this, "暂无可打开的当前名单文件。请先从 .txt、.csv 或 .xlsx 文件导入名单。", "打开文件", MessageBoxButtons.OK, MessageBoxIcon.Information);
        UpdateRosterActionState();
    }

    private void OpenLinkedRosterFile()
    {
        if (!TryOpenRosterFile(_state.SourceFilePath, out var error))
        {
            MessageBox.Show(this, $"打开名单文件失败：{error}", "打开文件", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _hintLabel.Text = $"已打开名单文件：{Path.GetFileName(_state.SourceFilePath)}";
    }

    private bool TryOpenRosterFile(string fileName, out string error)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
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

    private void ExportCurrentRosterForEditing()
    {
        if (_isDrawing)
        {
            _hintLabel.Text = "抽号进行中，结束后再导出编辑名单。";
            MessageBox.Show(this, "抽号进行中，结束后再导出编辑名单。", "导出编辑", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var students = GetStudents();
        if (students.Count == 0)
        {
            _hintLabel.Text = "暂无可导出编辑的名单。";
            MessageBox.Show(this, "暂无可导出编辑的名单。可以先试用、粘贴或导入名单。", "导出编辑", MessageBoxButtons.OK, MessageBoxIcon.Information);
            UpdateRosterActionState();
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "Excel 工作簿 (*.xlsx)|*.xlsx|CSV 文件 (*.csv)|*.csv|文本文件 (*.txt)|*.txt",
            Title = "导出并打开当前名单",
            FileName = $"抽号机名单-{DateTime.Now:yyyyMMdd-HHmmss}.xlsx",
            DefaultExt = "xlsx",
            AddExtension = true,
            InitialDirectory = GetPreferredExportDirectory()
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        RememberExportDirectory(dialog.FileName);

        try
        {
            SaveRosterExport(dialog.FileName, students);
            if (!TryLinkCurrentRosterExport(_state, dialog.FileName))
            {
                throw new InvalidOperationException("导出的文件未能设为当前名单文件。请确认保存位置可写，且扩展名为 .xlsx、.csv 或 .txt。");
            }

            _state.Save();
            UpdateClassroomStatusDisplay();
            UpdateRosterActionState();

            if (TryOpenRosterFile(_state.SourceFilePath, out var openError))
            {
                _hintLabel.Text = $"名单已导出并打开：{Path.GetFileName(dialog.FileName)}。保存修改后点“重载文件”刷新。";
                return;
            }

            _hintLabel.Text = $"名单已导出并设为当前文件：{Path.GetFileName(dialog.FileName)}。";
            MessageBox.Show(
                this,
                $"名单已导出并设为当前文件，但自动打开失败：{openError}\n\n文件位置：{dialog.FileName}\n\n可以手动打开文件编辑，保存后回到抽号机点“重载文件”。",
                "导出编辑",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"导出编辑失败：{ex.Message}", "导出编辑", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ShowSettingsDialog()
    {
        UpdateTopMostButton();
        UpdateStartupButton();
        UpdateSilentStartupButton();
        UpdateAvoidRepeatButton();
        UpdateAutoCopyButton();
        UpdateResetRoundButton();
        UpdateUndoDrawButton();
        UpdateHistoryActionState();
        UpdateRosterActionState();

        using var dialog = CreateSettingsDialog();
        _hintLabel.Text = "设置已打开。";
        dialog.ShowDialog(this);
        UpdateTopMostButton();
        UpdateStartupButton();
        UpdateSilentStartupButton();
        UpdateAvoidRepeatButton();
        UpdateAutoCopyButton();
        UpdateDurationPresetMenuState();
    }

    private Form CreateSettingsDialog()
    {
        var dialog = new Form
        {
            Text = "设置",
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            ClientSize = new Size(760, 520),
            Font = new Font("Microsoft YaHei UI", 10),
            BackColor = Color.FromArgb(248, 251, 255)
        };

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var tabs = new TabControl
        {
            Name = "SettingsTabs",
            Dock = DockStyle.Fill
        };
        tabs.TabPages.Add(BuildDrawSettingsPage());
        tabs.TabPages.Add(BuildDisplaySettingsPage());
        tabs.TabPages.Add(BuildStartupSettingsPage());
        tabs.TabPages.Add(BuildRosterSettingsPage());
        tabs.TabPages.Add(BuildRecordsSettingsPage());
        tabs.TabPages.Add(BuildHelpSettingsPage());
        root.Controls.Add(tabs, 0, 0);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Margin = new Padding(0, 14, 0, 0),
            BackColor = Color.Transparent
        };
        var closeButton = new Button
        {
            Text = "关闭",
            DialogResult = DialogResult.OK,
            AutoSize = true,
            Padding = new Padding(20, 7, 20, 7)
        };
        footer.Controls.Add(closeButton);
        root.Controls.Add(footer, 0, 1);

        dialog.Controls.Add(root);
        dialog.AcceptButton = closeButton;
        dialog.CancelButton = closeButton;
        return dialog;
    }

    private TabPage BuildDrawSettingsPage()
    {
        var page = CreateSettingsTabPage("抽号");
        var content = CreateSettingsFlow();
        page.Controls.Add(content);

        var durationInput = new NumericUpDown
        {
            DecimalPlaces = 2,
            Increment = 0.10m,
            Minimum = _drawDurationInput.Minimum,
            Maximum = _drawDurationInput.Maximum,
            Value = ClampDrawDuration((decimal)_state.DrawDurationSeconds),
            Width = 90,
            Font = new Font("Microsoft YaHei UI", 11, FontStyle.Bold)
        };
        durationInput.ValueChanged += (_, _) => ApplySettingsDuration(durationInput.Value);

        var durationRow = CreateSettingsRow("抽号时长", durationInput);
        durationRow.Name = "SettingsDrawControls";
        durationRow.Controls.Add(CreateSettingsButton("3秒", () => { SetDrawDurationPreset(3m); durationInput.Value = ClampDrawDuration(3m); }));
        durationRow.Controls.Add(CreateSettingsButton("5秒", () => { SetDrawDurationPreset(5m); durationInput.Value = ClampDrawDuration(5m); }));
        durationRow.Controls.Add(CreateSettingsButton("8秒", () => { SetDrawDurationPreset(8m); durationInput.Value = ClampDrawDuration(8m); }));
        content.Controls.Add(durationRow);

        var avoidRepeatBox = CreateSettingsCheckBox("避免重复", _state.AvoidRepeatDraw);
        avoidRepeatBox.CheckedChanged += (_, _) =>
        {
            if (_state.AvoidRepeatDraw != avoidRepeatBox.Checked)
            {
                ToggleAvoidRepeat();
            }
        };
        var autoCopyBox = CreateSettingsCheckBox("自动复制结果", _state.AutoCopyResult);
        autoCopyBox.CheckedChanged += (_, _) =>
        {
            if (_state.AutoCopyResult != autoCopyBox.Checked)
            {
                ToggleAutoCopyResult();
            }
        };
        content.Controls.Add(CreateSettingsRow("抽号行为", avoidRepeatBox, autoCopyBox));
        content.Controls.Add(CreateSettingsRow(
            "本轮操作",
            CreateSettingsButton("撤销上次", UndoLastDraw, CanUndoLastDraw()),
            CreateSettingsButton("重抽一次", RedrawLastDraw, CanRedrawLastDraw()),
            CreateSettingsButton("重置本轮", ResetAvoidRepeatRound, _state.DrawnStudentKeys.Count > 0 || _state.DrawnGroupKeys.Count > 0),
            CreateSettingsButton("新一节课", StartNewLesson, CanUseNewLessonAction())));

        return page;
    }

    private TabPage BuildDisplaySettingsPage()
    {
        var page = CreateSettingsTabPage("显示");
        var content = CreateSettingsFlow();
        page.Controls.Add(content);

        var shadeValue = new Label
        {
            AutoSize = true,
            Text = $"{ClampFloatingShade(_state.FloatingShade)}%",
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Microsoft YaHei UI", 11, FontStyle.Bold),
            ForeColor = Color.FromArgb(17, 38, 61),
            Margin = new Padding(8, 9, 0, 0)
        };
        var shadeInput = new TrackBar
        {
            Minimum = 0,
            Maximum = 100,
            TickFrequency = 25,
            Value = ClampFloatingShade(_state.FloatingShade),
            Width = 180,
            Height = 42
        };
        shadeInput.ValueChanged += (_, _) =>
        {
            ApplySettingsFloatingShade(shadeInput.Value);
            shadeValue.Text = $"{shadeInput.Value}%";
        };
        content.Controls.Add(CreateSettingsRow("悬浮球深浅", shadeInput, shadeValue));
        content.Controls.Add(CreateSettingsRow(
            "窗口",
            CreateSettingsButton("悬浮球模式", EnterFloatingMode),
            CreateSettingsButton(_presentationModeButton.Text, TogglePresentationMode)));
        content.Controls.Add(CreateSettingsRow(
            "结果字号",
            CreateSettingsButton("缩小", () => AdjustFontSize(false), _state.ResultFontSize > MinFontSize),
            CreateSettingsButton("放大", () => AdjustFontSize(true), _state.ResultFontSize < MaxFontSize)));

        var topMostBox = CreateSettingsCheckBox("始终置顶", _state.AlwaysOnTop);
        topMostBox.CheckedChanged += (_, _) =>
        {
            if (_state.AlwaysOnTop != topMostBox.Checked)
            {
                ToggleAlwaysOnTop();
            }
        };
        content.Controls.Add(CreateSettingsRow("置顶", topMostBox));

        return page;
    }

    private TabPage BuildStartupSettingsPage()
    {
        var page = CreateSettingsTabPage("启动");
        var content = CreateSettingsFlow();
        page.Controls.Add(content);

        var startupBox = CreateSettingsCheckBox("开机启动", IsStartupEnabled());
        startupBox.CheckedChanged += (_, _) =>
        {
            if (IsStartupEnabled() != startupBox.Checked)
            {
                ToggleStartup();
                startupBox.Checked = IsStartupEnabled();
            }
        };
        var silentBox = CreateSettingsCheckBox("静默启动", _state.SilentStartup);
        silentBox.CheckedChanged += (_, _) =>
        {
            if (_state.SilentStartup != silentBox.Checked)
            {
                ToggleSilentStartup();
                silentBox.Checked = _state.SilentStartup;
            }
        };
        content.Controls.Add(CreateSettingsRow("启动方式", startupBox, silentBox));
        return page;
    }

    private TabPage BuildRosterSettingsPage()
    {
        var page = CreateSettingsTabPage("名单");
        var content = CreateSettingsFlow();
        page.Controls.Add(content);
        var canUseRoster = GetStudents().Count > 0 && !_isDrawing;
        content.Controls.Add(CreateSettingsRow(
            "准备名单",
            CreateSettingsButton("上课向导", ShowStartGuide, !_isDrawing),
            CreateSettingsButton("名单模板", ShowRosterTemplateOptions, !_isDrawing),
            CreateSettingsButton("试用名单", LoadDemoRoster, !_isDrawing),
            CreateSettingsButton("恢复名单", RestorePreviousRoster, CanRestorePreviousRoster())));
        content.Controls.Add(CreateSettingsRow(
            "本节名单",
            CreateSettingsButton("本节缺席", EditLessonAbsences, canUseRoster),
            CreateSettingsButton("名单概览", () => ShowListOverview(), canUseRoster),
            CreateSettingsButton("重载文件", ReloadCurrentRosterFile, CanReloadCurrentRosterFile())));
        content.Controls.Add(CreateSettingsRow(
            "名单文件",
            CreateSettingsButton(_openSourceFileButton.Text, OpenCurrentRosterFile, CanOpenCurrentRosterFile() || CanExportRosterForEditing()),
            CreateSettingsButton("复制名单", CopyCurrentRoster, canUseRoster),
            CreateSettingsButton("导出名单", ExportCurrentRoster, canUseRoster)));
        return page;
    }

    private TabPage BuildRecordsSettingsPage()
    {
        var page = CreateSettingsTabPage("记录");
        var content = CreateSettingsFlow();
        page.Controls.Add(content);
        var hasHistory = GetVisibleHistoryLines().Count > 0;
        content.Controls.Add(CreateSettingsRow(
            "最近结果",
            CreateSettingsButton("复制结果", CopyCurrentResult, hasHistory),
            CreateSettingsButton("查看历史", ShowHistoryOverview, hasHistory),
            CreateSettingsButton("导出历史", ExportDrawHistory, hasHistory),
            CreateSettingsButton("清空历史", ClearDrawHistory, hasHistory)));
        content.Controls.Add(CreateSettingsRow(
            "课堂文件",
            CreateSettingsButton("复制摘要", CopyLessonSummary, CanCopyLessonSummary()),
            CreateSettingsButton("导出摘要", ExportLessonSummary, CanCopyLessonSummary()),
            CreateSettingsButton("导出课堂包", ExportLessonPackage, CanExportLessonPackage()),
            CreateSettingsButton("打开包位置", OpenLastLessonPackageDirectory, CanOpenLastLessonPackageDirectory())));
        return page;
    }

    private TabPage BuildHelpSettingsPage()
    {
        var page = CreateSettingsTabPage("帮助");
        var content = CreateSettingsFlow();
        page.Controls.Add(content);
        content.Controls.Add(CreateSettingsRow("帮助", CreateSettingsButton("操作速查", ShowQuickGuide), CreateSettingsButton("检查更新", CheckForUpdates), CreateSettingsButton("关于", ShowAbout)));
        return page;
    }

    private static TabPage CreateSettingsTabPage(string text)
    {
        return new TabPage(text)
        {
            BackColor = Color.FromArgb(248, 251, 255),
            Padding = new Padding(14)
        };
    }

    private static FlowLayoutPanel CreateSettingsFlow()
    {
        return new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Color.Transparent
        };
    }

    private FlowLayoutPanel CreateSettingsRow(string title, params Control[] controls)
    {
        var row = new FlowLayoutPanel
        {
            AutoSize = true,
            Width = 680,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Margin = new Padding(0, 0, 0, 14),
            BackColor = Color.Transparent
        };
        row.Controls.Add(new Label
        {
            Text = title,
            AutoSize = false,
            Width = 110,
            Height = 38,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(88, 113, 140),
            Font = new Font("Microsoft YaHei UI", 10, FontStyle.Bold),
            Margin = new Padding(0, 0, 12, 0)
        });

        foreach (var control in controls)
        {
            row.Controls.Add(control);
        }

        return row;
    }

    private Button CreateSettingsButton(string text, Action action, bool enabled = true)
    {
        var button = new RoundedButton();
        ConfigureButton(button, text, false);
        button.Enabled = enabled;
        button.Margin = new Padding(0, 0, 10, 8);
        button.Click += (_, _) => action();
        return button;
    }

    private static CheckBox CreateSettingsCheckBox(string text, bool isChecked)
    {
        return new CheckBox
        {
            Text = text,
            Checked = isChecked,
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(17, 38, 61),
            Margin = new Padding(0, 9, 18, 8)
        };
    }

    private void ApplySettingsDuration(decimal duration)
    {
        var clamped = ClampDrawDuration(duration);
        if (_drawDurationInput.Value != clamped)
        {
            _drawDurationInput.Value = clamped;
        }
        else
        {
            SaveDrawDuration();
        }
    }

    private void ApplySettingsFloatingShade(int shade)
    {
        var clamped = ClampFloatingShade(shade);
        if (_floatingShadeInput.Value != clamped)
        {
            _floatingShadeInput.Value = clamped;
        }
        else
        {
            SaveFloatingShade();
        }
    }

    private (bool Ok, string TabTexts, string DrawControlTexts) RunSettingsDialogSelfCheck()
    {
        using var dialog = CreateSettingsDialog();
        var tabs = dialog.Controls.Find("SettingsTabs", true).OfType<TabControl>().FirstOrDefault();
        if (tabs is null)
        {
            return (Ok: false, TabTexts: string.Empty, DrawControlTexts: string.Empty);
        }

        var tabTexts = tabs.TabPages.Cast<TabPage>().Select(page => page.Text).ToArray();
        var drawControls = dialog.Controls.Find("SettingsDrawControls", true)
            .FirstOrDefault()?
            .Controls
            .Cast<Control>()
            .Where(control => control is Button or NumericUpDown)
            .Select(control => control is Button button ? button.Text : "抽号时长")
            .ToArray() ?? Array.Empty<string>();
        var ok = string.Equals(dialog.Text, "设置", StringComparison.Ordinal)
            && tabTexts.SequenceEqual(new[] { "抽号", "显示", "启动", "名单", "记录", "帮助" }, StringComparer.Ordinal)
            && drawControls.Contains("抽号时长", StringComparer.Ordinal)
            && drawControls.Contains("3秒", StringComparer.Ordinal)
            && drawControls.Contains("5秒", StringComparer.Ordinal)
            && drawControls.Contains("8秒", StringComparer.Ordinal);

        return (
            Ok: ok,
            TabTexts: string.Join(" / ", tabTexts),
            DrawControlTexts: string.Join(" / ", drawControls));
    }

    private void ShowMainMoreMenu()
    {
        UpdateTopMostButton();
        UpdateStartupButton();
        UpdateSilentStartupButton();
        UpdateAvoidRepeatButton();
        UpdateAutoCopyButton();
        UpdateResetRoundButton();
        UpdateUndoDrawButton();
        UpdateHistoryActionState();
        UpdateRosterActionState();

        _mainMoreMenu.Items.Clear();

        ToolStripMenuItem AddGroup(string text)
        {
            var group = new ToolStripMenuItem(text);
            _mainMoreMenu.Items.Add(group);
            return group;
        }

        ToolStripMenuItem AddItem(ToolStripItemCollection items, string text, Action action, bool enabled = true)
        {
            var item = new ToolStripMenuItem(text) { Enabled = enabled };
            item.Click += (_, _) => action();
            items.Add(item);
            return item;
        }

        void AddSeparator(ToolStripItemCollection items)
        {
            if (items.Count == 0)
            {
                return;
            }

            if (items[items.Count - 1] is ToolStripSeparator)
            {
                return;
            }

            items.Add(new ToolStripSeparator());
        }

        var canImport = !_isDrawing;
        var canUseRoster = GetStudents().Count > 0 && !_isDrawing;
        var activeStudents = GetActiveStudents();
        var hasMale = activeStudents.Any(student => IsGender(student.Gender, "男"));
        var hasFemale = activeStudents.Any(student => IsGender(student.Gender, "女"));
        var hasGroups = activeStudents.Any(student => !string.IsNullOrWhiteSpace(student.Group));
        var canReloadFile = CanReloadCurrentRosterFile();
        var canOpenSourceFile = CanOpenCurrentRosterFile();
        var canExportRosterForEditing = CanExportRosterForEditing();
        var hasRecentRosterFiles = HasRecentRosterFiles();
        var canRestoreRoster = CanRestorePreviousRoster();
        var hasHistory = GetVisibleHistoryLines().Count > 0;
        var canCopyLessonSummary = CanCopyLessonSummary();
        var canExportLessonPackage = CanExportLessonPackage();
        var canOpenLessonPackageDirectory = CanOpenLastLessonPackageDirectory();
        var hasRoundRecords = _state.DrawnStudentKeys.Count > 0 || _state.DrawnGroupKeys.Count > 0;
        var duration = ClampDrawDuration((decimal)_state.DrawDurationSeconds);
        var canUseNewLessonAction = CanUseNewLessonAction();
        var canUndo = CanUndoLastDraw();
        var canRedraw = CanRedrawLastDraw();

        var rosterGroup = AddGroup("名单");
        AddItem(rosterGroup.DropDownItems, "上课向导", ShowStartGuide, !_isDrawing);
        AddItem(rosterGroup.DropDownItems, _absenceButton.Text, EditLessonAbsences, canUseRoster);
        AddItem(rosterGroup.DropDownItems, "名单概览", () => ShowListOverview(), canUseRoster);

        var prepareRosterItem = new ToolStripMenuItem("准备名单")
        {
            Enabled = canImport || canRestoreRoster || hasRecentRosterFiles
        };
        rosterGroup.DropDownItems.Add(prepareRosterItem);
        AddItem(prepareRosterItem.DropDownItems, "名单模板", ShowRosterTemplateOptions, canImport);
        AddItem(prepareRosterItem.DropDownItems, "试用名单", LoadDemoRoster, canImport);
        AddItem(prepareRosterItem.DropDownItems, "恢复名单", RestorePreviousRoster, canRestoreRoster);
        var recentItem = new ToolStripMenuItem("最近名单") { Enabled = canImport && hasRecentRosterFiles };
        BuildRecentRosterItems(recentItem.DropDownItems, LoadRecentRosterFile);
        prepareRosterItem.DropDownItems.Add(recentItem);

        var rosterFilesItem = new ToolStripMenuItem("文件和导出")
        {
            Enabled = canOpenSourceFile || canExportRosterForEditing || canReloadFile || canUseRoster
        };
        rosterGroup.DropDownItems.Add(rosterFilesItem);
        AddItem(rosterFilesItem.DropDownItems, _openSourceFileButton.Text, OpenCurrentRosterFile, canOpenSourceFile || canExportRosterForEditing);
        AddItem(rosterFilesItem.DropDownItems, "重载文件", ReloadCurrentRosterFile, canReloadFile);
        AddSeparator(rosterFilesItem.DropDownItems);
        AddItem(rosterFilesItem.DropDownItems, "复制名单", CopyCurrentRoster, canUseRoster);
        AddItem(rosterFilesItem.DropDownItems, "导出名单", ExportCurrentRoster, canUseRoster);

        var drawGroup = AddGroup("抽号");
        AddItem(drawGroup.DropDownItems, "试用抽号", StartDemoDraw, !_isDrawing);
        AddItem(drawGroup.DropDownItems, "新一节课", StartNewLesson, canUseNewLessonAction);

        var rangeDrawItem = new ToolStripMenuItem("按范围抽号")
        {
            Enabled = (hasMale || hasFemale || hasGroups) && !_isDrawing
        };
        drawGroup.DropDownItems.Add(rangeDrawItem);
        AddItem(rangeDrawItem.DropDownItems, "抽男生", () => StartGenderDraw("男"), hasMale && !_isDrawing);
        AddItem(rangeDrawItem.DropDownItems, "抽女生", () => StartGenderDraw("女"), hasFemale && !_isDrawing);
        AddItem(rangeDrawItem.DropDownItems, "抽小组", DrawGroup, hasGroups && !_isDrawing);

        var roundActionsItem = new ToolStripMenuItem("本轮操作")
        {
            Enabled = canUndo || canRedraw || (hasRoundRecords && !_isDrawing)
        };
        drawGroup.DropDownItems.Add(roundActionsItem);
        AddItem(roundActionsItem.DropDownItems, "撤销上次", UndoLastDraw, canUndo);
        AddItem(roundActionsItem.DropDownItems, "重抽一次", RedrawLastDraw, canRedraw);
        AddItem(roundActionsItem.DropDownItems, "重置本轮", ResetAvoidRepeatRound, hasRoundRecords && !_isDrawing);

        var drawSettingsItem = new ToolStripMenuItem("抽号设置");
        drawGroup.DropDownItems.Add(drawSettingsItem);
        var durationItem = new ToolStripMenuItem($"抽号时长：{duration:0.##}秒");
        drawSettingsItem.DropDownItems.Add(durationItem);
        AddDurationPresetItem(durationItem.DropDownItems, "3秒", 3m, duration);
        AddDurationPresetItem(durationItem.DropDownItems, "5秒", 5m, duration);
        AddDurationPresetItem(durationItem.DropDownItems, "8秒", 8m, duration);
        AddSeparator(drawSettingsItem.DropDownItems);
        AddItem(drawSettingsItem.DropDownItems, _avoidRepeatButton.Text, ToggleAvoidRepeat);
        AddItem(drawSettingsItem.DropDownItems, _autoCopyButton.Text, ToggleAutoCopyResult);

        var recordsItem = new ToolStripMenuItem("记录和导出")
        {
            Enabled = hasHistory || canCopyLessonSummary || canExportLessonPackage || canOpenLessonPackageDirectory
        };
        drawGroup.DropDownItems.Add(recordsItem);
        AddItem(recordsItem.DropDownItems, "复制结果", CopyCurrentResult, hasHistory);
        AddItem(recordsItem.DropDownItems, "查看历史", ShowHistoryOverview, hasHistory);
        var lessonFilesItem = new ToolStripMenuItem("课堂文件")
        {
            Enabled = canCopyLessonSummary || canExportLessonPackage || canOpenLessonPackageDirectory
        };
        recordsItem.DropDownItems.Add(lessonFilesItem);
        AddItem(lessonFilesItem.DropDownItems, "复制摘要", CopyLessonSummary, canCopyLessonSummary);
        AddItem(lessonFilesItem.DropDownItems, "导出摘要", ExportLessonSummary, canCopyLessonSummary);
        AddItem(lessonFilesItem.DropDownItems, "导出课堂包", ExportLessonPackage, canExportLessonPackage);
        AddItem(lessonFilesItem.DropDownItems, "打开包位置", OpenLastLessonPackageDirectory, canOpenLessonPackageDirectory);
        var historyManagementItem = new ToolStripMenuItem("历史管理")
        {
            Enabled = hasHistory
        };
        recordsItem.DropDownItems.Add(historyManagementItem);
        AddItem(historyManagementItem.DropDownItems, "导出历史", ExportDrawHistory, hasHistory);
        AddItem(historyManagementItem.DropDownItems, "清空历史", ClearDrawHistory, hasHistory);

        var displayGroup = AddGroup("显示");
        AddItem(displayGroup.DropDownItems, _presentationModeButton.Text, TogglePresentationMode);

        var floatingDisplayItem = new ToolStripMenuItem("悬浮球");
        displayGroup.DropDownItems.Add(floatingDisplayItem);
        AddItem(floatingDisplayItem.DropDownItems, "悬浮球模式", EnterFloatingMode);
        AddItem(floatingDisplayItem.DropDownItems, "悬浮球深浅", ShowFloatingShadeDialog);

        var resultFontItem = new ToolStripMenuItem("结果字号");
        displayGroup.DropDownItems.Add(resultFontItem);
        AddItem(resultFontItem.DropDownItems, "放大结果字号", () => AdjustFontSize(true), _state.ResultFontSize < MaxFontSize);
        AddItem(resultFontItem.DropDownItems, "缩小结果字号", () => AdjustFontSize(false), _state.ResultFontSize > MinFontSize);

        var windowStartupItem = new ToolStripMenuItem("窗口和启动");
        displayGroup.DropDownItems.Add(windowStartupItem);
        AddItem(windowStartupItem.DropDownItems, _topMostButton.Text, ToggleAlwaysOnTop);
        AddItem(windowStartupItem.DropDownItems, _startupButton.Text, ToggleStartup);
        AddItem(windowStartupItem.DropDownItems, _silentStartupButton.Text, ToggleSilentStartup);

        var helpGroup = AddGroup("帮助");
        AddItem(helpGroup.DropDownItems, "操作速查", ShowQuickGuide);
        AddItem(helpGroup.DropDownItems, "检查更新", CheckForUpdates);
        AddItem(helpGroup.DropDownItems, "关于", ShowAbout);

        _mainMoreMenu.Items.Remove(drawGroup);
        _mainMoreMenu.Items.Insert(0, drawGroup);

        _mainMoreMenu.Show(_moreActionsButton, new Point(0, _moreActionsButton.Height + 2));

        void AddDurationPresetItem(ToolStripItemCollection items, string text, decimal seconds, decimal currentDuration)
        {
            var item = AddItem(items, text, () => SetDrawDurationPreset(seconds));
            item.Checked = Math.Abs(currentDuration - seconds) < 0.005m;
        }
    }

    private void ShowRecentRosterMenu()
    {
        UpdateRecentRosterMenus();
        if (_recentRosterMenu.Items.Count == 0)
        {
            return;
        }

        _recentRosterMenu.Show(_recentRosterButton, new Point(0, _recentRosterButton.Height + 2));
    }

    private void LoadRecentRosterFile(string fileName)
    {
        if (_isDrawing)
        {
            _hintLabel.Text = "抽号进行中，结束后再切换最近名单。";
            MessageBox.Show(this, "抽号进行中，结束后再切换最近名单。", "暂不能切换", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(fileName);
        }
        catch (Exception ex)
        {
            RemoveRecentRosterFile(fileName);
            MessageBox.Show(this, $"最近名单路径无效，已移除。\n\n{ex.Message}", "最近名单", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!File.Exists(fullPath))
        {
            RemoveRecentRosterFile(fullPath);
            _hintLabel.Text = "最近名单文件不存在，已从列表移除。";
            MessageBox.Show(this, "这个最近名单文件不存在，已从列表移除。", "最近名单", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!IsSupportedImportFile(fullPath))
        {
            RemoveRecentRosterFile(fullPath);
            MessageBox.Show(this, "这个最近名单不是 .txt、.csv 或 .xlsx 文件，已从列表移除。", "最近名单", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ImportNamesFromFile(fullPath);
    }

    private bool CanLoadRecentRosterFile(string fileName)
    {
        return !_isDrawing
            && !string.IsNullOrWhiteSpace(fileName)
            && IsSupportedImportFile(fileName)
            && File.Exists(fileName);
    }

    private bool HasRecentRosterFiles()
    {
        NormalizeRecentRosterFiles();
        return _state.RecentRosterFiles.Count > 0;
    }

    private void RememberRecentRosterFile(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(fileName);
        }
        catch
        {
            return;
        }

        if (!IsSupportedImportFile(fullPath))
        {
            return;
        }

        var updated = new List<string> { fullPath };
        updated.AddRange(_state.RecentRosterFiles);
        _state.RecentRosterFiles = updated;
        NormalizeRecentRosterFiles();
    }

    private void RemoveRecentRosterFile(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(fileName);
        }
        catch
        {
            fullPath = fileName;
        }

        _state.RecentRosterFiles = _state.RecentRosterFiles
            .Where(path => !string.Equals(NormalizeRosterPathForCompare(path), NormalizeRosterPathForCompare(fullPath), StringComparison.OrdinalIgnoreCase))
            .ToList();
        _state.Save();
        UpdateRosterActionState();
    }

    private void PruneMissingRecentRosterFiles()
    {
        var beforeCount = _state.RecentRosterFiles.Count;
        _state.RecentRosterFiles = _state.RecentRosterFiles
            .Where(path => IsSupportedImportFile(path) && File.Exists(path))
            .ToList();
        NormalizeRecentRosterFiles();
        _state.Save();
        UpdateRosterActionState();

        var removedCount = Math.Max(0, beforeCount - _state.RecentRosterFiles.Count);
        _hintLabel.Text = removedCount > 0
            ? $"已清理 {removedCount} 个失效的最近名单。"
            : "最近名单里没有失效文件。";
    }

    private bool NormalizeRecentRosterFiles()
    {
        var normalized = BuildNormalizedRecentRosterFiles(_state.RecentRosterFiles);
        if (_state.RecentRosterFiles.SequenceEqual(normalized, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        _state.RecentRosterFiles = normalized;
        return true;
    }

    private static List<string> BuildNormalizedRecentRosterFiles(IEnumerable<string> fileNames)
    {
        var normalized = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var fileName in fileNames ?? Enumerable.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                continue;
            }

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(fileName);
            }
            catch
            {
                continue;
            }

            if (!IsSupportedImportFile(fullPath))
            {
                continue;
            }

            var comparePath = NormalizeRosterPathForCompare(fullPath);
            if (!seen.Add(comparePath))
            {
                continue;
            }

            normalized.Add(fullPath);
            if (normalized.Count >= MaxRecentRosterFiles)
            {
                break;
            }
        }

        return normalized;
    }

    private void UpdateRecentRosterMenus()
    {
        NormalizeRecentRosterFiles();
        BuildRecentRosterItems(_recentRosterMenu.Items, LoadRecentRosterFile);
        BuildRecentRosterItems(_floatingRecentRosterMenuItem.DropDownItems, ShowMainThenLoadRecentRosterFile);
        BuildRecentRosterItems(_trayRecentRosterMenuItem.DropDownItems, ShowMainThenLoadRecentRosterFile);
    }

    private void BuildRecentRosterItems(ToolStripItemCollection items, Action<string> loadAction)
    {
        items.Clear();
        if (_state.RecentRosterFiles.Count == 0)
        {
            items.Add(new ToolStripMenuItem("暂无最近名单") { Enabled = false });
            return;
        }

        var missingCount = 0;
        for (var index = 0; index < _state.RecentRosterFiles.Count; index++)
        {
            var fileName = _state.RecentRosterFiles[index];
            if (!File.Exists(fileName))
            {
                missingCount++;
            }

            var item = new ToolStripMenuItem(BuildRecentRosterMenuText(fileName, index + 1))
            {
                Enabled = CanLoadRecentRosterFile(fileName),
                ToolTipText = fileName
            };
            item.Click += (_, _) => loadAction(fileName);
            items.Add(item);
        }

        if (missingCount > 0)
        {
            items.Add(new ToolStripSeparator());
            var cleanItem = new ToolStripMenuItem($"清理失效名单（{missingCount}）");
            cleanItem.Click += (_, _) => PruneMissingRecentRosterFiles();
            items.Add(cleanItem);
        }
    }

    private string GetPreferredRosterDirectory()
    {
        var candidates = new[] { _state.SourceFilePath }.Concat(_state.RecentRosterFiles);
        foreach (var fileName in candidates)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                continue;
            }

            try
            {
                var directory = Path.GetDirectoryName(Path.GetFullPath(fileName));
                if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                {
                    return directory;
                }
            }
            catch
            {
                // Ignore invalid saved paths and fall back to the default dialog directory.
            }
        }

        if (!string.IsNullOrWhiteSpace(_state.LastExportDirectory) && Directory.Exists(_state.LastExportDirectory))
        {
            return _state.LastExportDirectory;
        }

        return string.Empty;
    }

    private static string BuildRecentRosterMenuText(string fileName, int index)
    {
        var displayName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = fileName;
        }

        var directoryName = Path.GetFileName(Path.GetDirectoryName(fileName) ?? string.Empty);
        var label = string.IsNullOrWhiteSpace(directoryName)
            ? displayName
            : $"{displayName}（{directoryName}）";

        if (!File.Exists(fileName))
        {
            label += " - 文件不存在";
        }

        return $"{index}. {EscapeMenuText(label)}";
    }

    private static string EscapeMenuText(string text)
    {
        return text.Replace("&", "&&", StringComparison.Ordinal);
    }

    private static string NormalizeRosterPathForCompare(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFullPath(fileName);
        }
        catch
        {
            return fileName;
        }
    }

    private bool CanReloadCurrentRosterFile()
    {
        return !_isDrawing
            && !string.IsNullOrWhiteSpace(_state.SourceFilePath)
            && IsSupportedImportFile(_state.SourceFilePath)
            && File.Exists(_state.SourceFilePath);
    }

    private bool CanOpenCurrentRosterFile()
    {
        return !string.IsNullOrWhiteSpace(_state.SourceFilePath)
            && IsSupportedImportFile(_state.SourceFilePath)
            && File.Exists(_state.SourceFilePath);
    }

    private bool CanExportRosterForEditing()
    {
        return !_isDrawing
            && GetStudents().Count > 0
            && !CanOpenCurrentRosterFile();
    }

    private string BuildRosterSourceStatusShort()
    {
        if (GetStudents().Count == 0 || CanOpenCurrentRosterFile())
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(_state.SourceFilePath))
        {
            return "内置保存";
        }

        return "原文件不可用";
    }

    private string BuildRosterSourceStatusHint()
    {
        if (GetStudents().Count == 0 || CanOpenCurrentRosterFile())
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(_state.SourceFilePath))
        {
            return "名单已保存在程序内";
        }

        if (!IsSupportedImportFile(_state.SourceFilePath))
        {
            return "原名单文件格式不可用";
        }

        return "原名单文件找不到";
    }

    private static bool IsSupportedImportFile(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return string.Equals(extension, ".txt", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetImportFormatName(string fileName)
    {
        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".csv" => "CSV 格式",
            ".xlsx" => "Excel 格式",
            _ => "TXT 格式"
        };
    }

    private static string[] ReadImportLines(string fileName)
    {
        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".csv" => ReadDelimitedTable(File.ReadAllText(fileName, Encoding.UTF8), ',').Select(BuildImportLineFromFields).ToArray(),
            ".xlsx" => ReadExcelWorksheetLines(fileName),
            _ => File.ReadAllLines(fileName)
        };
    }

    private static List<string[]> ReadDelimitedTable(string text, char delimiter)
    {
        var rows = new List<string[]>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < text.Length; index++)
        {
            var ch = text[index];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (index + 1 < text.Length && text[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(ch);
                }

                continue;
            }

            if (ch == '"')
            {
                inQuotes = true;
                continue;
            }

            if (ch == delimiter)
            {
                row.Add(field.ToString());
                field.Clear();
                continue;
            }

            if (ch is '\r' or '\n')
            {
                row.Add(field.ToString());
                field.Clear();
                rows.Add(row.ToArray());
                row.Clear();
                if (ch == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                {
                    index++;
                }

                continue;
            }

            field.Append(ch);
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row.ToArray());
        }

        return rows;
    }

    private static string[] ReadExcelWorksheetLines(string fileName)
    {
        using var archive = ZipFile.OpenRead(fileName);
        var sharedStrings = ReadExcelSharedStrings(archive);
        var worksheetPath = GetFirstExcelWorksheetPath(archive) ?? "xl/worksheets/sheet1.xml";
        var worksheetEntry = archive.GetEntry(worksheetPath)
            ?? throw new InvalidDataException("未找到 Excel 工作表。");

        using var stream = worksheetEntry.Open();
        var document = XDocument.Load(stream);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var lines = new List<string>();

        foreach (var row in document.Descendants(ns + "row"))
        {
            var values = new SortedDictionary<int, string>();
            var nextColumn = 0;
            foreach (var cell in row.Elements(ns + "c"))
            {
                var columnIndex = GetExcelColumnIndex((string?)cell.Attribute("r")) ?? nextColumn;
                values[columnIndex] = GetExcelCellText(cell, sharedStrings, ns);
                nextColumn = columnIndex + 1;
            }

            if (values.Count == 0)
            {
                lines.Add(string.Empty);
                continue;
            }

            var maxColumn = values.Keys.Max();
            var fields = Enumerable.Range(0, maxColumn + 1)
                .Select(index => values.TryGetValue(index, out var value) ? value : string.Empty);
            lines.Add(BuildImportLineFromFields(fields));
        }

        return lines.ToArray();
    }

    private static List<string> ReadExcelSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null)
        {
            return new List<string>();
        }

        using var stream = entry.Open();
        var document = XDocument.Load(stream);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        return document
            .Descendants(ns + "si")
            .Select(item => string.Concat(item.Descendants(ns + "t").Select(text => text.Value)))
            .ToList();
    }

    private static string? GetFirstExcelWorksheetPath(ZipArchive archive)
    {
        var workbookEntry = archive.GetEntry("xl/workbook.xml");
        var relationshipsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels");
        if (workbookEntry is null || relationshipsEntry is null)
        {
            return null;
        }

        XNamespace workbookNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace relationshipAttrNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        using var workbookStream = workbookEntry.Open();
        var workbook = XDocument.Load(workbookStream);
        var firstSheet = workbook.Descendants(workbookNs + "sheet").FirstOrDefault();
        var relationshipId = (string?)firstSheet?.Attribute(relationshipAttrNs + "id");
        if (string.IsNullOrWhiteSpace(relationshipId))
        {
            return null;
        }

        XNamespace packageRelationshipNs = "http://schemas.openxmlformats.org/package/2006/relationships";
        using var relationshipsStream = relationshipsEntry.Open();
        var relationships = XDocument.Load(relationshipsStream);
        var target = relationships
            .Descendants(packageRelationshipNs + "Relationship")
            .FirstOrDefault(relationship => string.Equals((string?)relationship.Attribute("Id"), relationshipId, StringComparison.Ordinal))
            ?.Attribute("Target")
            ?.Value;

        return string.IsNullOrWhiteSpace(target) ? null : ResolveExcelRelationshipPath("xl/workbook.xml", target);
    }

    private static string ResolveExcelRelationshipPath(string sourcePath, string target)
    {
        if (target.StartsWith("/", StringComparison.Ordinal))
        {
            return target.TrimStart('/').Replace('\\', '/');
        }

        var sourceDirectory = Path.GetDirectoryName(sourcePath)?.Replace('\\', '/') ?? string.Empty;
        var combined = string.IsNullOrWhiteSpace(sourceDirectory) ? target : $"{sourceDirectory}/{target}";
        var parts = new List<string>();
        foreach (var part in combined.Replace('\\', '/').Split('/'))
        {
            if (string.IsNullOrWhiteSpace(part) || part == ".")
            {
                continue;
            }

            if (part == "..")
            {
                if (parts.Count > 0)
                {
                    parts.RemoveAt(parts.Count - 1);
                }

                continue;
            }

            parts.Add(part);
        }

        return string.Join("/", parts);
    }

    private static int? GetExcelColumnIndex(string? cellReference)
    {
        if (string.IsNullOrWhiteSpace(cellReference))
        {
            return null;
        }

        var columnNumber = 0;
        var foundColumn = false;
        foreach (var ch in cellReference)
        {
            if (!char.IsLetter(ch))
            {
                break;
            }

            foundColumn = true;
            columnNumber = columnNumber * 26 + (char.ToUpperInvariant(ch) - 'A' + 1);
        }

        return foundColumn ? columnNumber - 1 : null;
    }

    private static string GetExcelCellText(XElement cell, IReadOnlyList<string> sharedStrings, XNamespace ns)
    {
        var type = (string?)cell.Attribute("t");
        if (string.Equals(type, "inlineStr", StringComparison.Ordinal))
        {
            return string.Concat(cell.Descendants(ns + "t").Select(text => text.Value));
        }

        var value = cell.Element(ns + "v")?.Value ?? string.Empty;
        if (string.Equals(type, "s", StringComparison.Ordinal)
            && int.TryParse(value, out var sharedStringIndex)
            && sharedStringIndex >= 0
            && sharedStringIndex < sharedStrings.Count)
        {
            return sharedStrings[sharedStringIndex];
        }

        return value;
    }

    private static string BuildImportLineFromFields(IEnumerable<string> fields)
    {
        return string.Join("\t", fields.Select(field => field.Trim()));
    }

    private void ImportNamesFromClipboard()
    {
        if (_isDrawing)
        {
            MessageBox.Show(this, "抽号进行中，结束后再导入名单。", "暂不能导入", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var initialText = TryGetClipboardText(out var clipboardWarning);
        var pastedText = ShowPasteRosterDialog(initialText, clipboardWarning);
        if (pastedText is null)
        {
            return;
        }

        var lines = pastedText.ReplaceLineEndings("\n").Split('\n');
        ApplyImportedStudents(ParseStudents(lines), $"粘贴名单 {DateTime.Now:yyyyMMdd-HHmm}", "粘贴名单格式");
    }

    private static string TryGetClipboardText(out string warning)
    {
        warning = string.Empty;
        try
        {
            return Clipboard.ContainsText() ? Clipboard.GetText() : string.Empty;
        }
        catch (Exception ex)
        {
            warning = $"读取剪贴板失败：{ex.Message}";
            return string.Empty;
        }
    }

    private string? ShowPasteRosterDialog(string initialText, string clipboardWarning)
    {
        using var dialog = new Form
        {
            Text = "粘贴名单",
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            ClientSize = new Size(720, 560),
            Font = new Font("Microsoft YaHei UI", 10),
            BackColor = Color.FromArgb(248, 251, 255)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            RowCount = 4,
            ColumnCount = 1
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var introText = "把名单粘贴或输入到下面，然后点“导入名单”。支持 Excel/Word/微信复制内容，也支持“序号 姓名 性别 小组”。";
        if (!string.IsNullOrWhiteSpace(clipboardWarning))
        {
            introText += Environment.NewLine + clipboardWarning + "；也可以在这里手动粘贴或输入。";
        }

        var introLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Text = introText,
            ForeColor = Color.FromArgb(17, 38, 61),
            Margin = new Padding(0, 0, 0, 10)
        };

        var inputBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            AcceptsTab = true,
            AcceptsReturn = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Text = initialText ?? string.Empty,
            Font = new Font("Microsoft YaHei UI", 10)
        };

        var validationLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Visible = false,
            ForeColor = Color.FromArgb(181, 44, 44),
            Margin = new Padding(0, 10, 0, 0)
        };

        inputBox.TextChanged += (_, _) =>
        {
            validationLabel.Visible = false;
            validationLabel.Text = string.Empty;
        };

        var buttonPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Dock = DockStyle.Fill,
            WrapContents = false,
            Margin = new Padding(0, 14, 0, 0)
        };

        var importButton = new Button { Text = "导入名单", AutoSize = true, Padding = new Padding(18, 6, 18, 6) };
        var cancelButton = new Button { Text = "取消", DialogResult = DialogResult.Cancel, AutoSize = true, Padding = new Padding(18, 6, 18, 6) };
        var templateButton = new Button { Text = "名单模板", AutoSize = true, Padding = new Padding(18, 6, 18, 6) };

        importButton.Click += (_, _) =>
        {
            var candidateText = inputBox.Text;
            if (string.IsNullOrWhiteSpace(candidateText))
            {
                ShowPasteRosterValidationError(validationLabel, "请先粘贴或输入名单内容。可每行一人，也可直接粘贴 Excel/Word 表格内容。");
                inputBox.Focus();
                return;
            }

            var previewLines = candidateText.ReplaceLineEndings("\n").Split('\n');
            var preview = ParseStudents(previewLines);
            if (preview.Errors.Count > 0)
            {
                ShowPasteRosterValidationError(validationLabel, BuildPasteRosterInlineError(preview.Errors));
                inputBox.Focus();
                return;
            }

            if (preview.Students.Count == 0)
            {
                ShowPasteRosterValidationError(validationLabel, "没有识别到学生姓名。请确认名单内容已粘贴进来，或按“序号 姓名 性别 小组”的格式输入。");
                inputBox.Focus();
                return;
            }

            if (NormalizeImportedStudents(preview.Students).Students.Count == 0)
            {
                ShowPasteRosterValidationError(validationLabel, "名单内容都被识别为重复或无效项，请检查后再导入。");
                inputBox.Focus();
                return;
            }

            dialog.DialogResult = DialogResult.OK;
            dialog.Close();
        };
        templateButton.Click += (_, _) =>
        {
            dialog.Close();
            BeginInvoke(new MethodInvoker(ShowRosterTemplateOptions));
        };

        buttonPanel.Controls.Add(cancelButton);
        buttonPanel.Controls.Add(importButton);
        buttonPanel.Controls.Add(templateButton);
        layout.Controls.Add(introLabel, 0, 0);
        layout.Controls.Add(inputBox, 0, 1);
        layout.Controls.Add(validationLabel, 0, 2);
        layout.Controls.Add(buttonPanel, 0, 3);
        dialog.Controls.Add(layout);
        dialog.AcceptButton = importButton;
        dialog.CancelButton = cancelButton;

        dialog.Shown += (_, _) => inputBox.Focus();
        return dialog.ShowDialog(this) == DialogResult.OK ? inputBox.Text : null;
    }

    private static void ShowPasteRosterValidationError(Label validationLabel, string text)
    {
        validationLabel.Text = text;
        validationLabel.Visible = true;
    }

    private static string BuildPasteRosterInlineError(IReadOnlyList<string> errors)
    {
        var text = new StringBuilder();
        text.AppendLine("名单格式还不能导入，请在上方直接修改后再点“导入名单”。");
        foreach (var error in errors.Take(4))
        {
            text.AppendLine(error);
        }

        if (errors.Count > 4)
        {
            text.AppendLine($"其余 {errors.Count - 4} 个问题修正后会继续检查。");
        }

        return text.ToString().TrimEnd();
    }

    private bool ApplyImportedStudents(
        (List<StudentRecord> Students, List<string> Errors) parseResult,
        string sourceName,
        string formatName,
        string sourceFilePath = "",
        bool preserveLessonAbsences = false,
        bool showSuccessMessage = true)
    {
        if (parseResult.Errors.Count > 0)
        {
            MessageBox.Show(
                this,
                BuildImportFailureText(formatName, parseResult.Errors),
                "导入失败",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }

        if (parseResult.Students.Count == 0)
        {
            ShowEmptyImportRecoveryPrompt(formatName);
            return false;
        }

        var importReport = NormalizeImportedStudents(parseResult.Students);
        if (importReport.Students.Count == 0)
        {
            ShowEmptyImportRecoveryPrompt(formatName);
            return false;
        }

        RememberPreviousRosterSnapshot();
        _state.Students = importReport.Students;
        _state.Names = importReport.Students.Select(student => student.DisplayName).ToList();
        _state.FileName = sourceName;
        _state.SourceFilePath = sourceFilePath;
        RememberRecentRosterFile(sourceFilePath);
        if (preserveLessonAbsences)
        {
            PruneExcludedStudentKeys(saveChanges: false);
        }
        else
        {
            _state.ExcludedStudentKeys.Clear();
        }

        _state.DrawnStudentKeys.Clear();
        _state.DrawnGroupKeys.Clear();
        ClearUndoSnapshot();
        _state.Save();
        UpdateResetRoundButton();
        UpdateUndoDrawButton();
        UpdateHistoryActionState();
        UpdateRosterActionState();

        UpdateClassroomStatusDisplay();
        _hintLabel.Text = string.Empty;
        SetIdleResultText();
        var qualityWarnings = BuildImportQualityWarnings(importReport.Students);
        if (importReport.RemovedDuplicateCount > 0)
        {
            _hintLabel.Text = $"已自动跳过 {importReport.RemovedDuplicateCount} 条重复名单。";
        }
        else if (importReport.SameNameWarnings.Count > 0)
        {
            _hintLabel.Text = "检测到同名学生，请在名单概览中确认。";
        }
        else if (qualityWarnings.Count > 0)
        {
            _hintLabel.Text = qualityWarnings[0];
        }
        else if (CanRestorePreviousRoster())
        {
            _hintLabel.Text = "如导入错名单，可点“恢复名单”切回上次名单。";
        }

        if (showSuccessMessage)
        {
            var nextAction = ShowImportSuccessDialog(importReport, qualityWarnings);
            if (nextAction != ImportSuccessAction.None && !IsDisposed && !Disposing)
            {
                BeginInvoke(new MethodInvoker(() => RunImportSuccessAction(nextAction)));
            }
        }

        return true;
    }

    private void ShowEmptyImportRecoveryPrompt(string formatName)
    {
        var nextAction = ShowEmptyImportRecoveryDialog(formatName);
        if (nextAction != ImportRecoveryAction.None && !IsDisposed && !Disposing)
        {
            BeginInvoke(new MethodInvoker(() => RunImportRecoveryAction(nextAction)));
        }
    }

    private ImportRecoveryAction ShowEmptyImportRecoveryDialog(string formatName)
    {
        var selectedAction = ImportRecoveryAction.None;
        using var dialog = new Form
        {
            Text = "名单没有导入",
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            ClientSize = new Size(660, 340),
            Font = new Font("Microsoft YaHei UI", 10),
            BackColor = Color.FromArgb(248, 251, 255)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            RowCount = 3,
            ColumnCount = 1
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var titleLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Text = "没有找到可导入的学生名单",
            ForeColor = Color.FromArgb(17, 38, 61),
            Font = new Font("Microsoft YaHei UI", 15, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 10)
        };

        var details = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(17, 38, 61),
            Text = BuildEmptyImportRecoveryText(formatName)
        };

        var buttonPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Dock = DockStyle.Fill,
            WrapContents = true,
            Margin = new Padding(0, 12, 0, 0)
        };

        var closeButton = new Button { Text = "关闭", DialogResult = DialogResult.OK, AutoSize = true, Padding = new Padding(18, 6, 18, 6) };
        var importButton = new Button { Text = "重新选择文件", AutoSize = true, Padding = new Padding(18, 6, 18, 6), Enabled = !_isDrawing };
        var templateButton = new Button { Text = "名单模板", AutoSize = true, Padding = new Padding(18, 6, 18, 6), Enabled = !_isDrawing };
        var pasteButton = new Button { Text = "粘贴/输入名单", AutoSize = true, Padding = new Padding(18, 6, 18, 6), Enabled = !_isDrawing };

        void SelectAction(ImportRecoveryAction action)
        {
            selectedAction = action;
            dialog.DialogResult = DialogResult.OK;
            dialog.Close();
        }

        foreach (var button in new[] { closeButton, importButton, templateButton, pasteButton })
        {
            button.AccessibleName = button.Text;
            button.AccessibleDescription = button.Text;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = ReferenceEquals(button, pasteButton) ? Color.FromArgb(13, 99, 201) : Color.FromArgb(196, 210, 224);
            button.BackColor = ReferenceEquals(button, pasteButton) ? Color.FromArgb(13, 99, 201) : Color.White;
            button.ForeColor = ReferenceEquals(button, pasteButton) ? Color.White : Color.FromArgb(17, 38, 61);
            button.Font = new Font("Microsoft YaHei UI", 10, ReferenceEquals(button, pasteButton) ? FontStyle.Bold : FontStyle.Regular);
        }

        pasteButton.Click += (_, _) => SelectAction(ImportRecoveryAction.PasteRoster);
        templateButton.Click += (_, _) => SelectAction(ImportRecoveryAction.SaveTemplate);
        importButton.Click += (_, _) => SelectAction(ImportRecoveryAction.ImportFile);

        buttonPanel.Controls.Add(closeButton);
        buttonPanel.Controls.Add(importButton);
        buttonPanel.Controls.Add(templateButton);
        buttonPanel.Controls.Add(pasteButton);
        layout.Controls.Add(titleLabel, 0, 0);
        layout.Controls.Add(details, 0, 1);
        layout.Controls.Add(buttonPanel, 0, 2);
        dialog.Controls.Add(layout);
        dialog.AcceptButton = pasteButton.Enabled ? pasteButton : closeButton;
        dialog.CancelButton = closeButton;
        _hintLabel.Text = "名单没有导入：可粘贴名单、使用模板，或重新选择文件。";
        dialog.ShowDialog(this);
        return selectedAction;
    }

    private static string BuildEmptyImportRecoveryText(string formatName)
    {
        var sourceText = string.IsNullOrWhiteSpace(formatName) ? "当前文件" : formatName;
        return string.Join(
            Environment.NewLine,
            new[]
            {
                $"已读取 {sourceText}，但没有找到可用的学生行。",
                string.Empty,
                "可以继续这样处理：",
                "1. 直接点“粘贴/输入名单”，把名单粘贴进输入框后导入。",
                "2. 点“名单模板”，保存标准模板，按“序号、姓名、性别、小组”填写后再导入。",
                "3. 点“重新选择文件”，换一个 .txt、.csv 或 .xlsx 名单文件。",
                string.Empty,
                "常见原因：文件只有标题行、学生姓名列为空、选错文件，或 Excel 表格里学生数据不在可读取区域。"
            });
    }

    private void RunImportRecoveryAction(ImportRecoveryAction action)
    {
        switch (action)
        {
            case ImportRecoveryAction.PasteRoster:
                ImportNamesFromClipboard();
                break;
            case ImportRecoveryAction.SaveTemplate:
                ShowRosterTemplateOptions();
                break;
            case ImportRecoveryAction.ImportFile:
                ImportNames();
                break;
        }
    }

    private ImportSuccessAction ShowImportSuccessDialog(
        (List<StudentRecord> Students, int RemovedDuplicateCount, List<string> SameNameWarnings) importReport,
        IReadOnlyList<string> qualityWarnings)
    {
        var selectedAction = ImportSuccessAction.None;
        var hasImportWarnings = HasImportWarnings(importReport, qualityWarnings);
        using var dialog = new Form
        {
            Text = "导入成功",
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            ClientSize = new Size(700, 460),
            Font = new Font("Microsoft YaHei UI", 10),
            BackColor = Color.FromArgb(248, 251, 255)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            RowCount = 4,
            ColumnCount = 1
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var titleLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Text = $"名单已导入：{importReport.Students.Count} 人",
            ForeColor = Color.FromArgb(17, 38, 61),
            Font = new Font("Microsoft YaHei UI", 15, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 10)
        };

        var details = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(17, 38, 61),
            Text = BuildImportSuccessText(importReport, qualityWarnings)
        };

        var nextStepLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Text = BuildImportSuccessNextStepText(hasImportWarnings),
            ForeColor = Color.FromArgb(49, 68, 88),
            Margin = new Padding(0, 12, 0, 0)
        };

        var buttonPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Dock = DockStyle.Fill,
            WrapContents = true,
            Margin = new Padding(0, 12, 0, 0)
        };

        var canStartDraw = GetPresentStudentsForLesson().Count > 0 && !_isDrawing;
        var defaultActionText = GetImportSuccessDefaultActionText(canStartDraw, hasImportWarnings);
        var closeButton = new Button { Text = ImportSuccessDismissActionText, DialogResult = DialogResult.Cancel, AutoSize = true, Padding = new Padding(18, 6, 18, 6) };
        var startDrawButton = new Button { Text = ImportSuccessPrimaryActionText, AutoSize = true, Padding = new Padding(18, 6, 18, 6), Enabled = canStartDraw };
        var absenceButton = new Button { Text = ImportSuccessAbsenceActionText, AutoSize = true, Padding = new Padding(18, 6, 18, 6), Enabled = importReport.Students.Count > 0 && !_isDrawing };

        foreach (var button in new[] { closeButton, startDrawButton, absenceButton })
        {
            button.AccessibleName = button.Text;
            button.AccessibleDescription = button.Text;
        }

        startDrawButton.Click += (_, _) =>
        {
            selectedAction = ImportSuccessAction.StartDraw;
            dialog.DialogResult = DialogResult.OK;
            dialog.Close();
        };
        absenceButton.Click += (_, _) =>
        {
            selectedAction = ImportSuccessAction.EditAbsences;
            dialog.DialogResult = DialogResult.OK;
            dialog.Close();
        };

        buttonPanel.Controls.Add(startDrawButton);
        buttonPanel.Controls.Add(absenceButton);
        buttonPanel.Controls.Add(closeButton);
        layout.Controls.Add(titleLabel, 0, 0);
        layout.Controls.Add(details, 0, 1);
        layout.Controls.Add(nextStepLabel, 0, 2);
        layout.Controls.Add(buttonPanel, 0, 3);
        dialog.Controls.Add(layout);
        dialog.AcceptButton = string.Equals(defaultActionText, ImportSuccessPrimaryActionText, StringComparison.Ordinal)
            ? startDrawButton
            : closeButton;
        dialog.CancelButton = closeButton;

        dialog.ShowDialog(this);
        return selectedAction;
    }

    private static string BuildImportSuccessNextStepText(bool hasImportWarnings)
    {
        if (hasImportWarnings)
        {
            return $"下一步：已发现导入提醒，建议先确认名单；确认无误后可手动点“{ImportSuccessPrimaryActionText}”。";
        }

        return $"下一步：名单确认无误可按 Enter 直接开始抽号；如果今天有人缺席，先点“{ImportSuccessAbsenceActionText}”。";
    }

    private static string GetImportSuccessDefaultActionText(bool canStartDraw, bool hasImportWarnings)
    {
        return canStartDraw && !hasImportWarnings ? ImportSuccessPrimaryActionText : ImportSuccessDismissActionText;
    }

    private static bool HasImportWarnings(
        (List<StudentRecord> Students, int RemovedDuplicateCount, List<string> SameNameWarnings) importReport,
        IReadOnlyList<string> qualityWarnings)
    {
        return importReport.RemovedDuplicateCount > 0
            || importReport.SameNameWarnings.Count > 0
            || qualityWarnings.Count > 0;
    }

    private void RunImportSuccessAction(ImportSuccessAction action)
    {
        if (IsDisposed || Disposing)
        {
            return;
        }

        if (action == ImportSuccessAction.EditAbsences)
        {
            EditLessonAbsences();
            return;
        }

        if (action == ImportSuccessAction.StartDraw)
        {
            StartDraw();
        }
    }

    private static string BuildImportFailureText(string formatName, IReadOnlyList<string> errors)
    {
        var text = new StringBuilder();
        text.AppendLine($"导入失败，请检查 {formatName}。");
        text.AppendLine();
        foreach (var error in errors.Take(8))
        {
            text.AppendLine(error);
        }

        text.AppendLine();
        text.AppendLine("可点“名单模板”保存标准 Excel 模板，把学生复制进去后再导入。");
        return text.ToString();
    }

    private void RememberPreviousRosterSnapshot()
    {
        var students = GetStudents();
        if (students.Count == 0)
        {
            return;
        }

        _state.PreviousStudents = CloneStudents(students);
        _state.PreviousFileName = string.IsNullOrWhiteSpace(_state.FileName) ? "上次名单" : _state.FileName;
        _state.PreviousSourceFilePath = _state.SourceFilePath;
    }

    private void RestorePreviousRoster()
    {
        if (_isDrawing)
        {
            _hintLabel.Text = "抽号进行中，结束后再恢复名单。";
            MessageBox.Show(this, "抽号进行中，结束后再恢复上次名单。", "暂不能恢复", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!CanRestorePreviousRoster())
        {
            _hintLabel.Text = "暂无可恢复的上次名单。";
            MessageBox.Show(this, "暂无可恢复的上次名单。导入或载入试用名单后，原名单会自动保留一次。", "恢复名单", MessageBoxButtons.OK, MessageBoxIcon.Information);
            UpdateRosterActionState();
            return;
        }

        var previousFileName = string.IsNullOrWhiteSpace(_state.PreviousFileName) ? "上次名单" : _state.PreviousFileName;
        var result = MessageBox.Show(
            this,
            $"将当前名单替换为“{previousFileName}”。当前名单会保留为新的上次名单，方便再次切回。\n\n要恢复吗？",
            "恢复上次名单",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Question);
        if (result != DialogResult.OK)
        {
            return;
        }

        var currentStudents = CloneStudents(_state.Students);
        var currentFileName = _state.FileName;
        var currentSourceFilePath = _state.SourceFilePath;
        var restoredStudents = CloneStudents(_state.PreviousStudents);

        _state.Students = restoredStudents;
        _state.Names = restoredStudents.Select(student => student.DisplayName).ToList();
        _state.FileName = previousFileName;
        _state.SourceFilePath = _state.PreviousSourceFilePath;
        RememberRecentRosterFile(_state.SourceFilePath);
        _state.PreviousStudents = currentStudents;
        _state.PreviousFileName = string.IsNullOrWhiteSpace(currentFileName) ? "恢复前名单" : currentFileName;
        _state.PreviousSourceFilePath = currentSourceFilePath;
        _state.ExcludedStudentKeys.Clear();
        _state.DrawnStudentKeys.Clear();
        _state.DrawnGroupKeys.Clear();
        ClearUndoSnapshot();
        _state.Save();

        UpdateResetRoundButton();
        UpdateUndoDrawButton();
        UpdateHistoryActionState();
        UpdateRosterActionState();

        UpdateClassroomStatusDisplay();
        _hintLabel.Text = $"已恢复上次名单：{_state.FileName}。";
        SetIdleResultText();

        MessageBox.Show(
            this,
            $"已恢复“{_state.FileName}”，共 {_state.Students.Count} 人。\n\n当前名单已保留为新的上次名单，可再次恢复切回。",
            "恢复完成",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private bool CanRestorePreviousRoster()
    {
        return _state.PreviousStudents.Count > 0 && !_isDrawing;
    }

    private static List<StudentRecord> CloneStudents(IEnumerable<StudentRecord> students)
    {
        return students
            .Select(student => new StudentRecord
            {
                Sequence = student.Sequence,
                Name = student.Name,
                Gender = student.Gender,
                Group = student.Group
            })
            .ToList();
    }

    private static (List<StudentRecord> Students, int RemovedDuplicateCount, List<string> SameNameWarnings) NormalizeImportedStudents(IReadOnlyList<StudentRecord> students)
    {
        var cleaned = new List<StudentRecord>();
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        var removedDuplicateCount = 0;

        foreach (var student in students)
        {
            var key = GetStudentKey(student);
            if (seenKeys.Contains(key))
            {
                removedDuplicateCount++;
                continue;
            }

            seenKeys.Add(key);
            cleaned.Add(student);
        }

        var sameNameWarnings = cleaned
            .GroupBy(student => student.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() > 1)
            .Select(group => $"{group.Key}：{string.Join("、", group.Select(student => student.Sequence.Trim()).Where(sequence => !string.IsNullOrWhiteSpace(sequence)))}")
            .Take(6)
            .ToList();

        return (cleaned, removedDuplicateCount, sameNameWarnings);
    }

    private string BuildImportSuccessText((List<StudentRecord> Students, int RemovedDuplicateCount, List<string> SameNameWarnings) importReport, IReadOnlyList<string> qualityWarnings)
    {
        var text = new StringBuilder();
        text.AppendLine($"名单已导入，共 {importReport.Students.Count} 项。");
        if (importReport.RemovedDuplicateCount > 0)
        {
            text.AppendLine($"已自动跳过重复名单：{importReport.RemovedDuplicateCount} 条。");
        }

        if (importReport.SameNameWarnings.Count > 0)
        {
            text.AppendLine("检测到同名学生，请按序号确认：");
            foreach (var warning in importReport.SameNameWarnings)
            {
                text.AppendLine($"- {warning}");
            }
        }

        if (qualityWarnings.Count > 0)
        {
            text.AppendLine("导入提醒：");
            foreach (var warning in qualityWarnings)
            {
                text.AppendLine($"- {warning}");
            }
        }

        if (CanRestorePreviousRoster())
        {
            var previousFileName = string.IsNullOrWhiteSpace(_state.PreviousFileName) ? "上次名单" : _state.PreviousFileName;
            text.AppendLine($"已保留导入前名单：{previousFileName}。可点“恢复名单”切回。");
        }

        if (CanOpenCurrentRosterFile())
        {
            text.AppendLine("可点“打开文件”编辑名单，保存后点“重载文件”刷新。");
            text.AppendLine("此文件已加入“最近名单”，下次可快速切换。");
        }

        text.AppendLine();
        text.Append(BuildCompactListSummary(importReport.Students));
        return text.ToString();
    }

    private static List<string> BuildImportQualityWarnings(IReadOnlyList<StudentRecord> students)
    {
        var warnings = new List<string>();
        if (students.Count == 0)
        {
            return warnings;
        }

        var missingGenderCount = students.Count(student => string.IsNullOrWhiteSpace(student.Gender));
        var unknownGenderGroups = students
            .Where(student => !string.IsNullOrWhiteSpace(student.Gender) && !IsGender(student.Gender, "男") && !IsGender(student.Gender, "女"))
            .GroupBy(student => student.Gender.Trim(), StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var unknownGenderCount = unknownGenderGroups.Sum(group => group.Count());
        if (missingGenderCount + unknownGenderCount > 0)
        {
            warnings.Add($"有 {missingGenderCount + unknownGenderCount} 人性别未填或未识别，男生/女生抽取只会使用识别为“男/女”的学生。");
        }

        if (unknownGenderGroups.Count > 0)
        {
            var preview = string.Join("、", unknownGenderGroups.Take(4).Select(group => $"{group.Key} {group.Count()} 人"));
            if (unknownGenderGroups.Count > 4)
            {
                preview += $"等 {unknownGenderGroups.Count} 类";
            }

            warnings.Add($"未识别性别值：{preview}。");
        }

        var missingGroupCount = students.Count(student => string.IsNullOrWhiteSpace(student.Group));
        if (missingGroupCount == students.Count)
        {
            warnings.Add("未设置小组，抽小组按钮将不可用。");
        }
        else if (missingGroupCount > 0)
        {
            warnings.Add($"有 {missingGroupCount} 人未分组，抽小组时不会计入这些学生。");
        }

        return warnings;
    }

    private void ShowEmptyRosterDrawPrompt()
    {
        if (_isDrawing)
        {
            return;
        }

        StartDemoDraw();
    }

    private void StartDemoDraw()
    {
        if (_isDrawing)
        {
            MessageBox.Show(this, "抽号进行中，结束后再体验抽号。", "暂不能体验", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (GetStudents().Count > 0)
        {
            StartDraw();
            return;
        }

        if (ApplyImportedStudents((BuildDemoRoster(), new List<string>()), "试用名单", "试用名单", showSuccessMessage: false))
        {
            _hintLabel.Text = "已载入试用名单，正在体验抽号。";
            StartDraw();
        }
    }

    private void LoadDemoRoster()
    {
        if (_isDrawing)
        {
            MessageBox.Show(this, "抽号进行中，结束后再载入试用名单。", "暂不能载入", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (GetStudents().Count > 0)
        {
            var result = MessageBox.Show(
                this,
                "载入试用名单会替换当前名单，仅用于熟悉功能。要继续吗？",
                "试用名单",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Question);
            if (result != DialogResult.OK)
            {
                return;
            }
        }

        ApplyImportedStudents((BuildDemoRoster(), new List<string>()), "试用名单", "试用名单");
    }

    private static List<StudentRecord> BuildDemoRoster()
    {
        return new List<StudentRecord>
        {
            new() { Sequence = "1", Name = "张三", Gender = "男", Group = "1" },
            new() { Sequence = "2", Name = "李四", Gender = "女", Group = "1" },
            new() { Sequence = "3", Name = "王五", Gender = "男", Group = "1" },
            new() { Sequence = "4", Name = "赵六", Gender = "女", Group = "1" },
            new() { Sequence = "5", Name = "陈晨", Gender = "男", Group = "2" },
            new() { Sequence = "6", Name = "刘洋", Gender = "女", Group = "2" },
            new() { Sequence = "7", Name = "周宁", Gender = "男", Group = "2" },
            new() { Sequence = "8", Name = "吴雨", Gender = "女", Group = "2" },
            new() { Sequence = "9", Name = "孙浩", Gender = "男", Group = "3" },
            new() { Sequence = "10", Name = "郑洁", Gender = "女", Group = "3" },
            new() { Sequence = "11", Name = "冯磊", Gender = "男", Group = "3" },
            new() { Sequence = "12", Name = "朱琳", Gender = "女", Group = "3" },
            new() { Sequence = "13", Name = "秦川", Gender = "男", Group = "4" },
            new() { Sequence = "14", Name = "杨柳", Gender = "女", Group = "4" },
            new() { Sequence = "15", Name = "何安", Gender = "男", Group = "4" },
            new() { Sequence = "16", Name = "高晴", Gender = "女", Group = "4" }
        };
    }

    private void WireDragImport(Control control)
    {
        control.AllowDrop = true;
        control.DragEnter += DragImport_DragEnter;
        control.DragDrop += DragImport_DragDrop;
    }

    private void WireDragImportRecursive(Control control)
    {
        WireDragImport(control);
        foreach (Control child in control.Controls)
        {
            WireDragImportRecursive(child);
        }
    }

    private void DragImport_DragEnter(object? sender, DragEventArgs e)
    {
        e.Effect = (!_isDrawing && TryGetDraggedImportFile(e, out _))
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private void DragImport_DragDrop(object? sender, DragEventArgs e)
    {
        if (!TryGetDraggedImportFile(e, out var fileName))
        {
            MessageBox.Show(this, "请拖入一个 .txt、.csv 或 .xlsx 名单文件。", "拖拽导入", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ImportNamesFromFile(fileName);
    }

    private static bool TryGetDraggedImportFile(DragEventArgs e, out string fileName)
    {
        fileName = string.Empty;
        if (e.Data is null || !e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return false;
        }

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files)
        {
            return false;
        }

        fileName = files.FirstOrDefault(IsSupportedImportFile) ?? string.Empty;
        return !string.IsNullOrWhiteSpace(fileName);
    }

    private (List<StudentRecord> Students, List<string> Errors) ParseStudents(IEnumerable<string> lines)
    {
        var students = new List<StudentRecord>();
        var errors = new List<string>();
        var lineNumber = 0;
        RosterColumnMap? columnMap = null;

        foreach (var rawLine in ExpandImportLines(lines))
        {
            lineNumber++;
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var parts = SplitImportLine(line);
            if (students.Count == 0 && columnMap is null && TryBuildRosterColumnMap(parts, out var detectedMap))
            {
                columnMap = detectedMap;
                continue;
            }

            if (students.Count == 0 && IsLikelyImportHeader(parts))
            {
                continue;
            }

            var student = columnMap.HasValue
                ? BuildStudentFromHeaderMap(parts, columnMap.Value, students.Count + 1)
                : BuildStudentFromParts(CompactImportParts(parts), students.Count + 1);
            if (string.IsNullOrWhiteSpace(student.Name))
            {
                errors.Add($"第 {lineNumber} 行格式错误：未识别到学生姓名。");
                continue;
            }

            students.Add(student);
        }

        return (students, errors);
    }

    private static IEnumerable<string> ExpandImportLines(IEnumerable<string> lines)
    {
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                yield return rawLine;
                continue;
            }

            foreach (var expandedLine in ExpandPackedImportLine(line))
            {
                yield return expandedLine;
            }
        }
    }

    private static IEnumerable<string> ExpandPackedImportLine(string line)
    {
        var numberedMatches = Regex.Matches(
            line,
            @"(?<!\S)\d+\s*(?:号\s*)?[\.．。、\)）\]】:：\-－—]\s*",
            RegexOptions.CultureInvariant);
        if (numberedMatches.Count > 1)
        {
            var prefix = line[..numberedMatches[0].Index].Trim();
            if (string.IsNullOrWhiteSpace(prefix))
            {
                for (var index = 0; index < numberedMatches.Count; index++)
                {
                    var start = numberedMatches[index].Index;
                    var end = index + 1 < numberedMatches.Count ? numberedMatches[index + 1].Index : line.Length;
                    var item = line[start..end].Trim();
                    if (!string.IsNullOrWhiteSpace(item))
                    {
                        yield return item;
                    }
                }

                yield break;
            }
        }

        var compactNumberedMatches = Regex.Matches(
            line,
            @"(?<!\S)\d{1,3}\s*(?:号\s*)?(?=\p{L})",
            RegexOptions.CultureInvariant);
        if (compactNumberedMatches.Count > 1)
        {
            var prefix = line[..compactNumberedMatches[0].Index].Trim();
            if (string.IsNullOrWhiteSpace(prefix))
            {
                for (var index = 0; index < compactNumberedMatches.Count; index++)
                {
                    var start = compactNumberedMatches[index].Index;
                    var end = index + 1 < compactNumberedMatches.Count ? compactNumberedMatches[index + 1].Index : line.Length;
                    var item = line[start..end].Trim();
                    if (!string.IsNullOrWhiteSpace(item))
                    {
                        yield return item;
                    }
                }

                yield break;
            }
        }

        if (ShouldSplitPlainNameList(line))
        {
            foreach (var name in line.Split(new[] { '、', '；', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(part => part.Trim()))
            {
                if (!string.IsNullOrWhiteSpace(name))
                {
                    yield return name;
                }
            }

            yield break;
        }

        yield return line;
    }

    private static bool ShouldSplitPlainNameList(string line)
    {
        if (!line.Contains('、') && !line.Contains('；') && !line.Contains(';'))
        {
            return false;
        }

        if (line.Contains('\t') || line.Contains(',') || line.Contains('，'))
        {
            return false;
        }

        if (Regex.IsMatch(line, @"^\s*\d+\s*(?:号\s*)?[\.．。、\)）\]】:：\-－—]", RegexOptions.CultureInvariant))
        {
            return false;
        }

        var parts = line.Split(new[] { '、', '；', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(part => part.Trim()).ToList();
        return parts.Count > 1 && parts.All(part => part.Length > 0 && !part.Any(char.IsWhiteSpace));
    }
    private static string[] SplitImportLine(string line)
    {
        string[] parts;
        if (line.Contains('\t'))
        {
            parts = line.Split('\t', StringSplitOptions.None).Select(part => part.Trim()).ToArray();
        }
        else if (line.Contains(',') || line.Contains('，'))
        {
            parts = line.Split(new[] { ',', '，' }, StringSplitOptions.None).Select(part => part.Trim()).ToArray();
        }
        else
        {
            parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(part => part.Trim()).ToArray();
        }

        return NormalizeLooseNumberedImportParts(parts);
    }

    private static string[] NormalizeLooseNumberedImportParts(string[] parts)
    {
        if (parts.Length == 0 || LooksLikeSequence(parts[0]))
        {
            return parts;
        }

        if (!TrySplitLooseNumberedToken(parts[0], out var sequence, out var inlineName))
        {
            return parts;
        }

        var normalized = new List<string> { sequence };
        if (!string.IsNullOrWhiteSpace(inlineName))
        {
            normalized.Add(inlineName);
            normalized.AddRange(parts.Skip(1));
        }
        else if (parts.Length >= 2)
        {
            normalized.Add(parts[1]);
            normalized.AddRange(parts.Skip(2));
        }
        else
        {
            normalized.Add(string.Empty);
        }

        return normalized.ToArray();
    }

    private static bool TrySplitLooseNumberedToken(string value, out string sequence, out string inlineName)
    {
        sequence = string.Empty;
        inlineName = string.Empty;
        var token = value.Trim();
        var digitCount = 0;
        while (digitCount < token.Length && char.IsDigit(token[digitCount]))
        {
            digitCount++;
        }

        if (digitCount == 0 || digitCount >= token.Length)
        {
            return false;
        }

        sequence = token[..digitCount];
        var rest = token[digitCount..].TrimStart();
        if (rest.StartsWith("号", StringComparison.Ordinal))
        {
            rest = rest[1..].TrimStart();
            if (rest.Length > 0 && IsLooseNumberDelimiter(rest[0]))
            {
                rest = rest[1..].TrimStart();
            }

            inlineName = rest;
            return true;
        }

        if (!IsLooseNumberDelimiter(rest[0]))
        {
            if (digitCount <= 3 && char.IsLetter(rest[0]))
            {
                inlineName = rest;
                return true;
            }

            sequence = string.Empty;
            return false;
        }

        inlineName = rest[1..].TrimStart();
        return true;
    }

    private static bool IsLooseNumberDelimiter(char value)
    {
        return value is '.' or '．' or '。' or '、' or ')' or '）' or ']' or '】' or ':' or '：' or '-' or '－' or '—';
    }
    private static string[] CompactImportParts(IEnumerable<string> parts)
    {
        return parts.Where(part => !string.IsNullOrWhiteSpace(part)).ToArray();
    }

    private readonly record struct RosterColumnMap(int SequenceIndex, int NameIndex, int GenderIndex, int GroupIndex);

    private enum RosterColumnKind
    {
        Unknown,
        Sequence,
        Name,
        Gender,
        Group
    }

    private static StudentRecord BuildStudentFromParts(string[] parts, int fallbackSequence)
    {
        var student = new StudentRecord();
        if (parts.Length == 0)
        {
            return student;
        }

        var fieldStartIndex = 1;
        if (parts.Length >= 2 && LooksLikeSequence(parts[0]))
        {
            student.Sequence = parts[0].Trim();
            student.Name = parts[1].Trim();
            fieldStartIndex = 2;
        }
        else
        {
            student.Sequence = fallbackSequence.ToString();
            student.Name = parts[0].Trim();
        }

        ApplyOptionalStudentFields(student, parts.Skip(fieldStartIndex));
        return student;
    }

    private static StudentRecord BuildStudentFromHeaderMap(string[] parts, RosterColumnMap columnMap, int fallbackSequence)
    {
        var student = new StudentRecord
        {
            Sequence = GetMappedImportField(parts, columnMap.SequenceIndex)
        };

        if (string.IsNullOrWhiteSpace(student.Sequence))
        {
            student.Sequence = fallbackSequence.ToString();
        }

        student.Name = GetMappedImportField(parts, columnMap.NameIndex);

        var gender = GetMappedImportField(parts, columnMap.GenderIndex);
        if (!string.IsNullOrWhiteSpace(gender))
        {
            student.Gender = TryNormalizeImportedGender(gender, out var normalizedGender)
                ? normalizedGender
                : gender.Trim();
        }

        var group = GetMappedImportField(parts, columnMap.GroupIndex);
        if (!string.IsNullOrWhiteSpace(group))
        {
            student.Group = NormalizeImportedGroup(group);
        }

        return student;
    }

    private static string GetMappedImportField(string[] parts, int index)
    {
        return index >= 0 && index < parts.Length ? parts[index].Trim() : string.Empty;
    }

    private static void ApplyOptionalStudentFields(StudentRecord student, IEnumerable<string> fields)
    {
        foreach (var rawField in fields)
        {
            var field = rawField.Trim();
            if (string.IsNullOrWhiteSpace(field))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(student.Gender) && TryNormalizeImportedGender(field, out var gender))
            {
                student.Gender = gender;
                continue;
            }

            if (string.IsNullOrWhiteSpace(student.Group))
            {
                student.Group = NormalizeImportedGroup(field);
            }
        }
    }

    private static bool LooksLikeSequence(string value)
    {
        var token = value.Trim().TrimEnd('号');
        return token.Length > 0 && token.All(char.IsDigit);
    }

    private static bool TryNormalizeImportedGender(string value, out string gender)
    {
        var token = value.Trim();
        if (string.Equals(token, "男", StringComparison.OrdinalIgnoreCase)
            || string.Equals(token, "男生", StringComparison.OrdinalIgnoreCase)
            || string.Equals(token, "M", StringComparison.OrdinalIgnoreCase)
            || string.Equals(token, "Male", StringComparison.OrdinalIgnoreCase))
        {
            gender = "男";
            return true;
        }

        if (string.Equals(token, "女", StringComparison.OrdinalIgnoreCase)
            || string.Equals(token, "女生", StringComparison.OrdinalIgnoreCase)
            || string.Equals(token, "F", StringComparison.OrdinalIgnoreCase)
            || string.Equals(token, "Female", StringComparison.OrdinalIgnoreCase))
        {
            gender = "女";
            return true;
        }

        gender = string.Empty;
        return false;
    }

    private static string NormalizeImportedGroup(string value)
    {
        var group = value.Trim();
        if (string.Equals(group, "未分组", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        if (group.StartsWith("第", StringComparison.Ordinal) && group.EndsWith("组", StringComparison.Ordinal) && group.Length > 2)
        {
            return group[1..^1].Trim();
        }

        return group.EndsWith("组", StringComparison.Ordinal) && group.Length > 1
            ? group[..^1].Trim()
            : group;
    }

    private static bool TryBuildRosterColumnMap(string[] parts, out RosterColumnMap columnMap)
    {
        var sequenceIndex = -1;
        var nameIndex = -1;
        var genderIndex = -1;
        var groupIndex = -1;

        for (var index = 0; index < parts.Length; index++)
        {
            switch (GetRosterColumnKind(parts[index]))
            {
                case RosterColumnKind.Sequence when sequenceIndex < 0:
                    sequenceIndex = index;
                    break;
                case RosterColumnKind.Name when nameIndex < 0:
                    nameIndex = index;
                    break;
                case RosterColumnKind.Gender when genderIndex < 0:
                    genderIndex = index;
                    break;
                case RosterColumnKind.Group when groupIndex < 0:
                    groupIndex = index;
                    break;
            }
        }

        columnMap = new RosterColumnMap(sequenceIndex, nameIndex, genderIndex, groupIndex);
        return nameIndex >= 0;
    }

    private static RosterColumnKind GetRosterColumnKind(string value)
    {
        var header = NormalizeRosterHeader(value);
        if (string.IsNullOrWhiteSpace(header))
        {
            return RosterColumnKind.Unknown;
        }

        if (header is "序号" or "编号" or "学号" or "号")
        {
            return RosterColumnKind.Sequence;
        }

        if (header is "姓名" or "名字" or "学生" or "学生姓名" or "名称")
        {
            return RosterColumnKind.Name;
        }

        if (header is "性别" or "男女")
        {
            return RosterColumnKind.Gender;
        }

        if (header is "小组" or "组别" or "组" or "分组")
        {
            return RosterColumnKind.Group;
        }

        return RosterColumnKind.Unknown;
    }

    private static string NormalizeRosterHeader(string value)
    {
        return value.Trim().TrimEnd(':', '：');
    }

    private static bool IsLikelyImportHeader(string[] parts)
    {
        if (parts.Length == 0)
        {
            return false;
        }

        var header = string.Concat(parts);
        var hasName = header.Contains("姓名") || header.Contains("名字") || header.Contains("学生");
        var hasSequence = header.Contains("序号") || header.Contains("编号") || header.Contains("学号");
        var hasGender = header.Contains("性别");
        var hasGroup = header.Contains("组别") || header.Contains("小组") || header.EndsWith("组", StringComparison.Ordinal);
        return hasName && (parts.Length == 1 || hasSequence || hasGender || hasGroup);
    }

    private void MigrateLegacyNames()
    {
        if (_state.Students.Count > 0 || _state.Names.Count == 0)
        {
            return;
        }

        _state.Students = _state.Names
            .Select((name, index) => new StudentRecord
            {
                Sequence = (index + 1).ToString(),
                Name = name.Trim()
            })
            .Where(student => !string.IsNullOrWhiteSpace(student.Name))
            .ToList();
    }

    private List<StudentRecord> GetStudents()
    {
        return _state.Students;
    }

    private List<StudentRecord> GetActiveStudents()
    {
        var excludedKeys = GetExcludedStudentKeySet();
        if (excludedKeys.Count == 0)
        {
            return GetStudents().ToList();
        }

        return GetStudents()
            .Where(student => !excludedKeys.Contains(GetStudentKey(student)))
            .ToList();
    }

    private bool IsStudentExcluded(StudentRecord student)
    {
        return _state.ExcludedStudentKeys.Contains(GetStudentKey(student), StringComparer.Ordinal);
    }

    private string GetStudentLessonStatus(StudentRecord student)
    {
        return IsStudentExcluded(student) ? "本节缺席" : "在场";
    }

    private bool IsStudentDrawnThisRound(StudentRecord student)
    {
        return _state.DrawnStudentKeys.Contains(GetStudentKey(student), StringComparer.Ordinal);
    }

    private string GetStudentRoundStatus(StudentRecord student)
    {
        if (!_state.AvoidRepeatDraw)
        {
            return "本轮未记录";
        }

        return IsStudentDrawnThisRound(student) ? "本轮已抽" : "本轮未抽";
    }

    private HashSet<string> GetExcludedStudentKeySet()
    {
        return _state.ExcludedStudentKeys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .ToHashSet(StringComparer.Ordinal);
    }

    private int GetExcludedStudentCount()
    {
        var currentKeys = GetStudents().Select(GetStudentKey).ToHashSet(StringComparer.Ordinal);
        return _state.ExcludedStudentKeys
            .Where(currentKeys.Contains)
            .Distinct(StringComparer.Ordinal)
            .Count();
    }

    private void SaveDrawDuration()
    {
        _state.DrawDurationSeconds = (double)_drawDurationInput.Value;
        _state.Save();
        UpdateFloatingCountdownIdleText();
        UpdateDurationPresetMenuState();
    }

    private void SetDrawDurationPreset(decimal seconds)
    {
        var duration = ClampDrawDuration(seconds);
        if (_drawDurationInput.Value != duration)
        {
            _drawDurationInput.Value = duration;
        }
        else
        {
            SaveDrawDuration();
        }

        _hintLabel.Text = $"抽号时长已设为 {duration:0.##} 秒。";
    }

    private void SaveFloatingShade()
    {
        _state.FloatingShade = _floatingShadeInput.Value;
        _floatingShadeValue.Text = $"{_state.FloatingShade}%";
        _state.Save();
        ApplyFloatingPalette();
    }

    private void ShowFloatingShadeDialog()
    {
        using var dialog = new Form
        {
            Text = "悬浮球深浅",
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            ClientSize = new Size(420, 160),
            Font = new Font("Microsoft YaHei UI", 10),
            BackColor = Color.FromArgb(248, 251, 255)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            RowCount = 3,
            ColumnCount = 2
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var titleLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Text = "悬浮球深浅",
            ForeColor = Color.FromArgb(17, 38, 61),
            Font = new Font("Microsoft YaHei UI", 13, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 12)
        };

        var valueLabel = new Label
        {
            AutoSize = true,
            Text = $"{ClampFloatingShade(_state.FloatingShade)}%",
            ForeColor = Color.FromArgb(17, 38, 61),
            Font = new Font("Microsoft YaHei UI", 11, FontStyle.Bold),
            Margin = new Padding(12, 10, 0, 0)
        };

        var shadeInput = new TrackBar
        {
            Minimum = 0,
            Maximum = 100,
            TickFrequency = 25,
            Dock = DockStyle.Fill,
            Value = ClampFloatingShade(_state.FloatingShade)
        };
        shadeInput.ValueChanged += (_, _) => valueLabel.Text = $"{shadeInput.Value}%";

        var buttonPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Dock = DockStyle.Fill,
            WrapContents = false,
            Margin = new Padding(0, 14, 0, 0)
        };
        var cancelButton = new Button { Text = "取消", DialogResult = DialogResult.Cancel, AutoSize = true, Padding = new Padding(18, 6, 18, 6) };
        var okButton = new Button { Text = "保存", AutoSize = true, Padding = new Padding(18, 6, 18, 6) };
        okButton.Click += (_, _) =>
        {
            var shade = ClampFloatingShade(shadeInput.Value);
            if (_floatingShadeInput.Value != shade)
            {
                _floatingShadeInput.Value = shade;
            }
            else
            {
                _state.FloatingShade = shade;
                _floatingShadeValue.Text = $"{shade}%";
                _state.Save();
                ApplyFloatingPalette();
            }

            _hintLabel.Text = $"悬浮球深浅已设为 {shade}%。";
            dialog.DialogResult = DialogResult.OK;
            dialog.Close();
        };

        buttonPanel.Controls.Add(cancelButton);
        buttonPanel.Controls.Add(okButton);
        layout.Controls.Add(titleLabel, 0, 0);
        layout.SetColumnSpan(titleLabel, 2);
        layout.Controls.Add(shadeInput, 0, 1);
        layout.Controls.Add(valueLabel, 1, 1);
        layout.Controls.Add(buttonPanel, 0, 2);
        layout.SetColumnSpan(buttonPanel, 2);
        dialog.Controls.Add(layout);
        dialog.AcceptButton = okButton;
        dialog.CancelButton = cancelButton;

        _hintLabel.Text = "悬浮球深浅设置已打开。";
        dialog.ShowDialog(this);
    }

    private void ShowRosterTemplateOptions()
    {
        using var dialog = new Form
        {
            Text = "名单模板",
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            ClientSize = new Size(680, 380),
            Font = new Font("Microsoft YaHei UI", 10),
            BackColor = Color.FromArgb(248, 251, 255)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            RowCount = 3,
            ColumnCount = 1
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var titleLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Text = "名单模板",
            ForeColor = Color.FromArgb(17, 38, 61),
            Font = new Font("Microsoft YaHei UI", 16, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 12)
        };

        var detailBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(17, 38, 61),
            Font = new Font("Microsoft YaHei UI", 10),
            Text = BuildRosterTemplateGuideText()
        };

        var buttonPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Dock = DockStyle.Fill,
            WrapContents = true,
            Margin = new Padding(0, 14, 0, 0)
        };

        var closeButton = new Button { Text = "关闭", DialogResult = DialogResult.Cancel, AutoSize = true, Padding = new Padding(18, 6, 18, 6) };
        var copyButton = new Button { Text = "复制模板", AutoSize = true, Padding = new Padding(18, 6, 18, 6) };
        var saveButton = new Button { Text = "保存 Excel 模板", AutoSize = true, Padding = new Padding(18, 6, 18, 6) };

        copyButton.Click += (_, _) =>
        {
            if (CopyRosterTemplateToClipboard())
            {
                dialog.Close();
            }
        };
        saveButton.Click += (_, _) =>
        {
            if (SaveRosterTemplateToFile())
            {
                dialog.Close();
            }
        };

        buttonPanel.Controls.Add(closeButton);
        buttonPanel.Controls.Add(copyButton);
        buttonPanel.Controls.Add(saveButton);
        layout.Controls.Add(titleLabel, 0, 0);
        layout.Controls.Add(detailBox, 0, 1);
        layout.Controls.Add(buttonPanel, 0, 2);
        dialog.Controls.Add(layout);
        dialog.AcceptButton = saveButton;
        dialog.CancelButton = closeButton;

        _hintLabel.Text = "名单模板已打开。";
        dialog.ShowDialog(this);
    }

    private static string BuildRosterTemplateGuideText()
    {
        return string.Join(
            Environment.NewLine,
            "建议保存 Excel 模板后，把本班学生复制进去，再从“导入文件”选择这个文件。",
            string.Empty,
            "支持列：序号、姓名、性别、小组。其中“姓名”必须有，其他列可留空。",
            "性别可写：男、女、男生、女生、M、F。",
            "小组可写：1、2、第一组、第 1 组等常见写法。",
            string.Empty,
            "也支持直接粘贴：从 Excel、微信、记事本复制学生名单后，点“粘贴名单”。",
            "文件导入支持：.xlsx、.csv、.txt，也可以把文件直接拖到主界面。",
            string.Empty,
            "模板预览：",
            BuildRosterTemplateText());
    }

    private bool CopyRosterTemplateToClipboard()
    {
        try
        {
            Clipboard.SetText(BuildRosterTemplateText());
            _hintLabel.Text = "名单模板已复制，可粘贴到 Excel、微信、记事本后修改。";
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"复制模板失败：{ex.Message}", "名单模板", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
    }

    private bool SaveRosterTemplateToFile()
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "Excel 工作簿 (*.xlsx)|*.xlsx|CSV 文件 (*.csv)|*.csv|文本文件 (*.txt)|*.txt",
            Title = "保存名单模板",
            FileName = "抽号机名单模板.xlsx",
            DefaultExt = "xlsx",
            AddExtension = true,
            InitialDirectory = GetPreferredExportDirectory()
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return false;
        }

        RememberExportDirectory(dialog.FileName);

        try
        {
            SaveRosterTemplate(dialog.FileName);
            _hintLabel.Text = $"名单模板已保存：{Path.GetFileName(dialog.FileName)}";
            OfferOpenRosterTemplate(dialog.FileName);
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"保存模板失败：{ex.Message}", "名单模板", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
    }

    private void OfferOpenRosterTemplate(string fileName)
    {
        var result = MessageBox.Show(
            this,
            $"名单模板已保存：{fileName}\n\n是否现在打开模板填写学生？填写并保存后，回到抽号机点“导入文件”选择这个模板即可。",
            "名单模板",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button1);
        if (result != DialogResult.Yes)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = true
            });
            _hintLabel.Text = "名单模板已打开。填写保存后，点“导入文件”选择这个模板。";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"打开模板失败：{ex.Message}\n\n模板位置：{fileName}", "名单模板", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static void SaveRosterTemplate(string fileName)
    {
        SaveTableRows(fileName, BuildRosterTemplateRows(), "名单模板");
    }

    private static string BuildRosterTemplateText()
    {
        return BuildTabDelimitedText(BuildRosterTemplateRows());
    }

    private static string BuildRosterTemplateCsv()
    {
        return BuildCsvText(BuildRosterTemplateRows());
    }

    private static string[][] BuildRosterTemplateRows()
    {
        return new[]
        {
            new[] { "序号", "姓名", "性别", "小组" },
            new[] { "1", "张三", "男", "1" },
            new[] { "2", "李四", "女", "1" },
            new[] { "3", "王五", "男", "2" },
            new[] { "4", "赵六", "女", "2" }
        };
    }

    private static string EscapeCsvField(string value)
    {
        return value.Any(ch => ch is ',' or '"' or '\r' or '\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }

    private static void SaveTableRows(string fileName, IReadOnlyList<string[]> rows, string sheetName)
    {
        switch (Path.GetExtension(fileName).ToLowerInvariant())
        {
            case ".xlsx":
                SaveTableRowsAsXlsx(fileName, rows, sheetName);
                break;
            case ".csv":
                File.WriteAllText(fileName, BuildCsvText(rows), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                break;
            default:
                File.WriteAllText(fileName, BuildTabDelimitedText(rows), Encoding.UTF8);
                break;
        }
    }

    private static string BuildTabDelimitedText(IEnumerable<string[]> rows)
    {
        return string.Join(Environment.NewLine, rows.Select(row => string.Join("\t", row)));
    }

    private static string BuildCsvText(IEnumerable<string[]> rows)
    {
        return string.Join(Environment.NewLine, rows.Select(row => string.Join(",", row.Select(EscapeCsvField))));
    }

    private static void SaveTableRowsAsXlsx(string fileName, IReadOnlyList<string[]> rows, string sheetName)
    {
        using var stream = new FileStream(fileName, FileMode.Create, FileAccess.ReadWrite);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
        WriteXmlEntry(archive, "[Content_Types].xml", BuildExcelContentTypesDocument());
        WriteXmlEntry(archive, "_rels/.rels", BuildExcelPackageRelationshipsDocument());
        WriteXmlEntry(archive, "xl/workbook.xml", BuildExcelWorkbookDocument(sheetName));
        WriteXmlEntry(archive, "xl/_rels/workbook.xml.rels", BuildExcelWorkbookRelationshipsDocument());
        WriteXmlEntry(archive, "xl/styles.xml", BuildExcelStylesDocument());
        WriteXmlEntry(archive, "xl/worksheets/sheet1.xml", BuildWorksheetDocument(rows));
    }

    private static void WriteXmlEntry(ZipArchive archive, string entryName, XDocument document)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        document.Save(writer);
    }

    private static XDocument BuildExcelContentTypesDocument()
    {
        XNamespace ns = "http://schemas.openxmlformats.org/package/2006/content-types";
        return new XDocument(
            new XElement(ns + "Types",
                new XElement(ns + "Default",
                    new XAttribute("Extension", "rels"),
                    new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                new XElement(ns + "Default",
                    new XAttribute("Extension", "xml"),
                    new XAttribute("ContentType", "application/xml")),
                new XElement(ns + "Override",
                    new XAttribute("PartName", "/xl/workbook.xml"),
                    new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")),
                new XElement(ns + "Override",
                    new XAttribute("PartName", "/xl/worksheets/sheet1.xml"),
                    new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")),
                new XElement(ns + "Override",
                    new XAttribute("PartName", "/xl/styles.xml"),
                    new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"))));
    }

    private static XDocument BuildExcelPackageRelationshipsDocument()
    {
        XNamespace ns = "http://schemas.openxmlformats.org/package/2006/relationships";
        return new XDocument(
            new XElement(ns + "Relationships",
                new XElement(ns + "Relationship",
                    new XAttribute("Id", "rId1"),
                    new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"),
                    new XAttribute("Target", "xl/workbook.xml"))));
    }

    private static XDocument BuildExcelWorkbookDocument(string sheetName)
    {
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace relNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        return new XDocument(
            new XElement(ns + "workbook",
                new XAttribute(XNamespace.Xmlns + "r", relNs),
                new XElement(ns + "sheets",
                    new XElement(ns + "sheet",
                        new XAttribute("name", NormalizeExcelSheetName(sheetName)),
                        new XAttribute("sheetId", "1"),
                        new XAttribute(relNs + "id", "rId1")))));
    }

    private static XDocument BuildExcelWorkbookRelationshipsDocument()
    {
        XNamespace ns = "http://schemas.openxmlformats.org/package/2006/relationships";
        return new XDocument(
            new XElement(ns + "Relationships",
                new XElement(ns + "Relationship",
                    new XAttribute("Id", "rId1"),
                    new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"),
                    new XAttribute("Target", "worksheets/sheet1.xml")),
                new XElement(ns + "Relationship",
                    new XAttribute("Id", "rId2"),
                    new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles"),
                    new XAttribute("Target", "styles.xml"))));
    }

    private static XDocument BuildExcelStylesDocument()
    {
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        return new XDocument(
            new XElement(ns + "styleSheet",
                new XElement(ns + "fonts",
                    new XAttribute("count", "2"),
                    new XElement(ns + "font",
                        new XElement(ns + "sz", new XAttribute("val", "11")),
                        new XElement(ns + "name", new XAttribute("val", "Microsoft YaHei UI"))),
                    new XElement(ns + "font",
                        new XElement(ns + "b"),
                        new XElement(ns + "sz", new XAttribute("val", "11")),
                        new XElement(ns + "name", new XAttribute("val", "Microsoft YaHei UI")))),
                new XElement(ns + "fills",
                    new XAttribute("count", "2"),
                    new XElement(ns + "fill", new XElement(ns + "patternFill", new XAttribute("patternType", "none"))),
                    new XElement(ns + "fill", new XElement(ns + "patternFill", new XAttribute("patternType", "gray125")))),
                new XElement(ns + "borders",
                    new XAttribute("count", "1"),
                    new XElement(ns + "border",
                        new XElement(ns + "left"),
                        new XElement(ns + "right"),
                        new XElement(ns + "top"),
                        new XElement(ns + "bottom"),
                        new XElement(ns + "diagonal"))),
                new XElement(ns + "cellStyleXfs",
                    new XAttribute("count", "1"),
                    new XElement(ns + "xf",
                        new XAttribute("numFmtId", "0"),
                        new XAttribute("fontId", "0"),
                        new XAttribute("fillId", "0"),
                        new XAttribute("borderId", "0"))),
                new XElement(ns + "cellXfs",
                    new XAttribute("count", "2"),
                    new XElement(ns + "xf",
                        new XAttribute("numFmtId", "0"),
                        new XAttribute("fontId", "0"),
                        new XAttribute("fillId", "0"),
                        new XAttribute("borderId", "0"),
                        new XAttribute("xfId", "0")),
                    new XElement(ns + "xf",
                        new XAttribute("numFmtId", "0"),
                        new XAttribute("fontId", "1"),
                        new XAttribute("fillId", "0"),
                        new XAttribute("borderId", "0"),
                        new XAttribute("xfId", "0"),
                        new XAttribute("applyFont", "1"))),
                new XElement(ns + "cellStyles",
                    new XAttribute("count", "1"),
                    new XElement(ns + "cellStyle",
                        new XAttribute("name", "Normal"),
                        new XAttribute("xfId", "0"),
                        new XAttribute("builtinId", "0")))));
    }

    private static XDocument BuildWorksheetDocument(IReadOnlyList<string[]> tableRows)
    {
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var rows = tableRows
            .Select((cells, rowIndex) =>
                new XElement(ns + "row",
                    new XAttribute("r", rowIndex + 1),
                    cells.Select((value, columnIndex) =>
                        new XElement(ns + "c",
                            new XAttribute("r", $"{GetExcelColumnName(columnIndex)}{rowIndex + 1}"),
                            new XAttribute("t", "inlineStr"),
                            rowIndex == 0 ? new XAttribute("s", "1") : null,
                            new XElement(ns + "is",
                                new XElement(ns + "t", value))))));

        return new XDocument(
            new XElement(ns + "worksheet",
                new XElement(ns + "sheetViews",
                    new XElement(ns + "sheetView",
                        new XAttribute("workbookViewId", "0"))),
                new XElement(ns + "cols",
                    new XElement(ns + "col", new XAttribute("min", "1"), new XAttribute("max", "1"), new XAttribute("width", "8"), new XAttribute("customWidth", "1")),
                    new XElement(ns + "col", new XAttribute("min", "2"), new XAttribute("max", "2"), new XAttribute("width", "14"), new XAttribute("customWidth", "1")),
                    new XElement(ns + "col", new XAttribute("min", "3"), new XAttribute("max", "4"), new XAttribute("width", "10"), new XAttribute("customWidth", "1"))),
                new XElement(ns + "sheetData", rows)));
    }

    private static string NormalizeExcelSheetName(string sheetName)
    {
        var cleaned = new string(sheetName
            .Where(ch => ch is not '[' and not ']' and not ':' and not '*' and not '?' and not '/' and not '\\')
            .ToArray())
            .Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            cleaned = "名单";
        }

        return cleaned.Length > 31 ? cleaned[..31] : cleaned;
    }

    private static string GetExcelColumnName(int zeroBasedIndex)
    {
        var dividend = zeroBasedIndex + 1;
        var columnName = new StringBuilder();
        while (dividend > 0)
        {
            var modulo = (dividend - 1) % 26;
            columnName.Insert(0, (char)('A' + modulo));
            dividend = (dividend - modulo) / 26;
        }

        return columnName.ToString();
    }

    private void ShowListOverview(OverviewStatusFilter initialFilter = OverviewStatusFilter.All)
    {
        var students = GetStudents()
            .OrderBy(student => ParseSequenceSortKey(student.Sequence))
            .ThenBy(student => student.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (students.Count == 0)
        {
            _hintLabel.Text = "暂无名单：可以先点“试用名单”，或粘贴/导入本班名单。";
            MessageBox.Show(this, "暂无名单。可以先点“试用名单”体验，或粘贴/导入本班名单。", "名单概览", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var visibleStudents = new List<StudentRecord>();
        using var dialog = new Form
        {
            Text = "名单概览",
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            ShowInTaskbar = false,
            FormBorderStyle = FormBorderStyle.Sizable,
            ClientSize = new Size(640, 680),
            MinimumSize = new Size(520, 520),
            Font = new Font("Microsoft YaHei UI", 10)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            RowCount = 5,
            ColumnCount = 1
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var summaryLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ForeColor = Color.FromArgb(17, 38, 61),
            Font = new Font("Microsoft YaHei UI", 10, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 10)
        };

        var searchBox = new TextBox
        {
            Dock = DockStyle.Fill,
            PlaceholderText = "搜索姓名、序号、小组、性别或状态",
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 0, 0, 10)
        };

        var list = new ListBox
        {
            Dock = DockStyle.Fill,
            IntegralHeight = false,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(248, 251, 255),
            ForeColor = Color.FromArgb(17, 38, 61),
            HorizontalScrollbar = true
        };

        var filterPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 10)
        };

        const string overviewFilterAll = "全部学生";
        const string overviewFilterPresent = "只看在场";
        const string overviewFilterAbsent = "只看缺席";
        const string overviewFilterUndrawn = "只看未抽";
        const string overviewFilterDrawn = "只看已抽";
        var statusFilterBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 150,
            Margin = new Padding(0, 0, 18, 0),
            Font = new Font("Microsoft YaHei UI", 10)
        };
        statusFilterBox.Items.AddRange(_state.AvoidRepeatDraw
            ? new object[] { overviewFilterAll, overviewFilterPresent, overviewFilterAbsent, overviewFilterUndrawn, overviewFilterDrawn }
            : new object[] { overviewFilterAll, overviewFilterPresent, overviewFilterAbsent });
        if (!_state.AvoidRepeatDraw && (initialFilter == OverviewStatusFilter.Undrawn || initialFilter == OverviewStatusFilter.Drawn))
        {
            initialFilter = OverviewStatusFilter.All;
        }

        statusFilterBox.SelectedItem = initialFilter switch
        {
            OverviewStatusFilter.Present => overviewFilterPresent,
            OverviewStatusFilter.Absent => overviewFilterAbsent,
            OverviewStatusFilter.Undrawn => overviewFilterUndrawn,
            OverviewStatusFilter.Drawn => overviewFilterDrawn,
            _ => overviewFilterAll
        };
        if (statusFilterBox.SelectedIndex < 0)
        {
            statusFilterBox.SelectedIndex = 0;
        }
        filterPanel.Controls.Add(new Label
        {
            Text = "状态：",
            AutoSize = true,
            ForeColor = Color.FromArgb(17, 38, 61),
            Font = new Font("Microsoft YaHei UI", 10, FontStyle.Bold),
            Padding = new Padding(0, 4, 4, 0),
            Margin = new Padding(0)
        });
        filterPanel.Controls.Add(statusFilterBox);

        bool MatchesOverviewSearch(StudentRecord student)
        {
            var selectedFilter = statusFilterBox.SelectedItem as string ?? overviewFilterAll;
            if (string.Equals(selectedFilter, overviewFilterPresent, StringComparison.Ordinal) && IsStudentExcluded(student))
            {
                return false;
            }

            if (string.Equals(selectedFilter, overviewFilterAbsent, StringComparison.Ordinal) && !IsStudentExcluded(student))
            {
                return false;
            }

            if (string.Equals(selectedFilter, overviewFilterUndrawn, StringComparison.Ordinal)
                && (!_state.AvoidRepeatDraw || IsStudentExcluded(student) || IsStudentDrawnThisRound(student)))
            {
                return false;
            }

            if (string.Equals(selectedFilter, overviewFilterDrawn, StringComparison.Ordinal)
                && (!_state.AvoidRepeatDraw || IsStudentExcluded(student) || !IsStudentDrawnThisRound(student)))
            {
                return false;
            }

            var query = searchBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(query))
            {
                return true;
            }

            var lessonStatus = GetStudentLessonStatus(student);
            var roundStatus = GetStudentRoundStatus(student);
            return student.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || student.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || student.Sequence.Contains(query, StringComparison.OrdinalIgnoreCase)
                || student.Gender.Contains(query, StringComparison.OrdinalIgnoreCase)
                || student.Group.Contains(query, StringComparison.OrdinalIgnoreCase)
                || lessonStatus.Contains(query, StringComparison.OrdinalIgnoreCase)
                || roundStatus.Contains(query, StringComparison.OrdinalIgnoreCase);
        }

        void RefreshOverviewList()
        {
            visibleStudents = students.Where(MatchesOverviewSearch).ToList();
            list.BeginUpdate();
            list.Items.Clear();
            foreach (var student in visibleStudents)
            {
                list.Items.Add(FormatRosterPreviewLine(student));
            }

            list.EndUpdate();
            var visibleText = visibleStudents.Count == students.Count
                ? string.Empty
                : $"\n当前显示：{visibleStudents.Count} 人";
            summaryLabel.Text = $"{BuildCompactListSummary(students)}{visibleText}";
        }

        void CopyRosterFromDialog(IReadOnlyList<StudentRecord> rows, string emptyMessage, string copiedMessage)
        {
            if (rows.Count == 0)
            {
                _hintLabel.Text = emptyMessage;
                return;
            }

            try
            {
                Clipboard.SetText(BuildRosterExportText(rows));
                _hintLabel.Text = copiedMessage;
            }
            catch (Exception ex)
            {
                MessageBox.Show(dialog, $"复制名单失败：{ex.Message}", "名单概览", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void ExportRosterFromDialog(IReadOnlyList<StudentRecord> rows, string emptyMessage)
        {
            if (rows.Count == 0)
            {
                _hintLabel.Text = emptyMessage;
                return;
            }

            using var exportDialog = new SaveFileDialog
            {
                Filter = "Excel 工作簿 (*.xlsx)|*.xlsx|CSV 文件 (*.csv)|*.csv|文本文件 (*.txt)|*.txt",
                Title = "导出筛选名单",
                FileName = $"抽号机名单筛选-{DateTime.Now:yyyyMMdd-HHmmss}.xlsx",
                DefaultExt = "xlsx",
                AddExtension = true,
                InitialDirectory = GetPreferredExportDirectory()
            };

            if (exportDialog.ShowDialog(dialog) != DialogResult.OK)
            {
                return;
            }

            RememberExportDirectory(exportDialog.FileName);

            try
            {
                SaveRosterExport(exportDialog.FileName, rows);
                _hintLabel.Text = $"筛选名单已导出：{Path.GetFileName(exportDialog.FileName)}";
                MessageBox.Show(dialog, $"筛选名单已导出到：\n{exportDialog.FileName}", "导出完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(dialog, $"导出筛选名单失败：{ex.Message}", "名单概览", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        searchBox.TextChanged += (_, _) => RefreshOverviewList();
        statusFilterBox.SelectedIndexChanged += (_, _) => RefreshOverviewList();
        RefreshOverviewList();

        var buttonPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 14, 0, 0)
        };

        var closeButton = new Button { Text = "关闭", DialogResult = DialogResult.OK, AutoSize = true, Padding = new Padding(18, 6, 18, 6) };
        var copyAllButton = new Button { Text = "复制全部", AutoSize = true, Padding = new Padding(18, 6, 18, 6) };
        var copyVisibleButton = new Button { Text = "复制当前筛选", AutoSize = true, Padding = new Padding(18, 6, 18, 6) };
        var exportVisibleButton = new Button { Text = "导出当前筛选", AutoSize = true, Padding = new Padding(18, 6, 18, 6) };
        copyAllButton.Click += (_, _) => CopyRosterFromDialog(students, "暂无可复制的名单。", $"名单已复制，共 {students.Count} 人。");
        copyVisibleButton.Click += (_, _) => CopyRosterFromDialog(visibleStudents, "当前筛选没有学生。", $"当前筛选名单已复制，共 {visibleStudents.Count} 人。");
        exportVisibleButton.Click += (_, _) => ExportRosterFromDialog(visibleStudents, "当前筛选没有学生。");

        buttonPanel.Controls.Add(closeButton);
        buttonPanel.Controls.Add(copyAllButton);
        buttonPanel.Controls.Add(copyVisibleButton);
        buttonPanel.Controls.Add(exportVisibleButton);

        layout.Controls.Add(summaryLabel, 0, 0);
        layout.Controls.Add(searchBox, 0, 1);
        layout.Controls.Add(filterPanel, 0, 2);
        layout.Controls.Add(list, 0, 3);
        layout.Controls.Add(buttonPanel, 0, 4);
        dialog.Controls.Add(layout);
        dialog.Shown += (_, _) => searchBox.Focus();
        dialog.ShowDialog(this);
    }

    private void EditLessonAbsences()
    {
        if (_isDrawing)
        {
            _hintLabel.Text = "抽号进行中，结束后再设置本节缺席。";
            MessageBox.Show(this, "抽号进行中，结束后再设置本节缺席。", "本节缺席", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var students = GetStudents()
            .OrderBy(student => ParseSequenceSortKey(student.Sequence))
            .ThenBy(student => student.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (students.Count == 0)
        {
            _hintLabel.Text = "暂无名单：可以先点“试用名单”，或粘贴/导入本班名单。";
            MessageBox.Show(this, "暂无名单。可以先试用、粘贴或导入名单，再设置本节缺席。", "本节缺席", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        PruneExcludedStudentKeys();
        var selectedKeys = GetExcludedStudentKeySet();
        var visibleStudents = new List<StudentRecord>();
        var refreshingList = false;

        using var dialog = new Form
        {
            Text = "本节缺席",
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            ClientSize = new Size(540, 660),
            Font = new Font("Microsoft YaHei UI", 10)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            RowCount = 5,
            ColumnCount = 1
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var summaryLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ForeColor = Color.FromArgb(17, 38, 61),
            Font = new Font("Microsoft YaHei UI", 10, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 10)
        };

        var searchBox = new TextBox
        {
            Dock = DockStyle.Fill,
            PlaceholderText = "搜索姓名、序号、小组、性别或状态",
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 0, 0, 8)
        };

        var showAbsentOnlyBox = new CheckBox
        {
            AutoSize = true,
            Text = "只看缺席",
            ForeColor = Color.FromArgb(17, 38, 61),
            Font = new Font("Microsoft YaHei UI", 10, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 10)
        };

        var list = new CheckedListBox
        {
            Dock = DockStyle.Fill,
            CheckOnClick = true,
            IntegralHeight = false,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(248, 251, 255),
            ForeColor = Color.FromArgb(17, 38, 61)
        };

        void RefreshDialogSummary()
        {
            var absentCount = selectedKeys.Count;
            var visibleSuffix = visibleStudents.Count == students.Count
                ? string.Empty
                : $"；当前显示 {visibleStudents.Count} 人";
            summaryLabel.Text = $"勾选缺席学生：{absentCount} 人；本节可抽 {students.Count - absentCount} 人{visibleSuffix}。";
        }

        string FormatAbsenceDialogLine(StudentRecord student)
        {
            var gender = string.IsNullOrWhiteSpace(student.Gender) ? "未填" : student.Gender.Trim();
            var group = string.IsNullOrWhiteSpace(student.Group) ? "未分组" : $"第 {student.Group.Trim()} 组";
            var lessonStatus = selectedKeys.Contains(GetStudentKey(student)) ? "本节缺席" : "在场";
            return $"{student.DisplayName} · {gender} · {group} · {lessonStatus}";
        }

        bool MatchesAbsenceSearch(StudentRecord student)
        {
            if (showAbsentOnlyBox.Checked && !selectedKeys.Contains(GetStudentKey(student)))
            {
                return false;
            }

            var query = searchBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(query))
            {
                return true;
            }

            var lessonStatus = selectedKeys.Contains(GetStudentKey(student)) ? "本节缺席" : "在场";
            return student.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || student.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || student.Sequence.Contains(query, StringComparison.OrdinalIgnoreCase)
                || student.Gender.Contains(query, StringComparison.OrdinalIgnoreCase)
                || student.Group.Contains(query, StringComparison.OrdinalIgnoreCase)
                || lessonStatus.Contains(query, StringComparison.OrdinalIgnoreCase);
        }

        void PopulateStudentList()
        {
            refreshingList = true;
            visibleStudents = students.Where(MatchesAbsenceSearch).ToList();
            list.BeginUpdate();
            list.Items.Clear();
            foreach (var student in visibleStudents)
            {
                list.Items.Add(FormatAbsenceDialogLine(student), selectedKeys.Contains(GetStudentKey(student)));
            }

            list.EndUpdate();
            refreshingList = false;
            RefreshDialogSummary();
        }

        searchBox.TextChanged += (_, _) => PopulateStudentList();
        showAbsentOnlyBox.CheckedChanged += (_, _) => PopulateStudentList();
        list.ItemCheck += (_, e) =>
        {
            if (refreshingList || e.Index < 0 || e.Index >= visibleStudents.Count)
            {
                return;
            }

            var key = GetStudentKey(visibleStudents[e.Index]);
            if (e.NewValue == CheckState.Checked)
            {
                selectedKeys.Add(key);
            }
            else
            {
                selectedKeys.Remove(key);
            }

            dialog.BeginInvoke((Action)PopulateStudentList);
        };
        PopulateStudentList();

        var buttonPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 14, 0, 0)
        };

        var okButton = new Button { Text = "确定", DialogResult = DialogResult.OK, AutoSize = true, Padding = new Padding(18, 6, 18, 6) };
        var cancelButton = new Button { Text = "取消", DialogResult = DialogResult.Cancel, AutoSize = true, Padding = new Padding(18, 6, 18, 6) };
        var clearButton = new Button { Text = "全部到齐", AutoSize = true, Padding = new Padding(18, 6, 18, 6) };
        var markVisibleAbsentButton = new Button { Text = "当前筛选标缺席", AutoSize = true, Padding = new Padding(18, 6, 18, 6) };
        var markVisiblePresentButton = new Button { Text = "当前筛选到齐", AutoSize = true, Padding = new Padding(18, 6, 18, 6) };
        markVisibleAbsentButton.Click += (_, _) =>
        {
            foreach (var student in visibleStudents)
            {
                selectedKeys.Add(GetStudentKey(student));
            }

            PopulateStudentList();
        };
        markVisiblePresentButton.Click += (_, _) =>
        {
            foreach (var student in visibleStudents)
            {
                selectedKeys.Remove(GetStudentKey(student));
            }

            PopulateStudentList();
        };
        clearButton.Click += (_, _) =>
        {
            selectedKeys.Clear();
            PopulateStudentList();
        };

        buttonPanel.Controls.Add(okButton);
        buttonPanel.Controls.Add(cancelButton);
        buttonPanel.Controls.Add(clearButton);
        buttonPanel.Controls.Add(markVisiblePresentButton);
        buttonPanel.Controls.Add(markVisibleAbsentButton);
        dialog.CancelButton = cancelButton;

        layout.Controls.Add(summaryLabel, 0, 0);
        layout.Controls.Add(searchBox, 0, 1);
        layout.Controls.Add(showAbsentOnlyBox, 0, 2);
        layout.Controls.Add(list, 0, 3);
        layout.Controls.Add(buttonPanel, 0, 4);
        dialog.Controls.Add(layout);
        dialog.Shown += (_, _) => searchBox.Focus();

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var currentStudentKeys = students.Select(GetStudentKey).ToHashSet(StringComparer.Ordinal);
        _state.ExcludedStudentKeys = selectedKeys
            .Where(currentStudentKeys.Contains)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        _state.Save();
        UpdateResetRoundButton();
        UpdateRosterActionState();
        SetIdleResultText();

        var excludedCount = _state.ExcludedStudentKeys.Count;
        _hintLabel.Text = excludedCount > 0
            ? $"本节缺席 {excludedCount} 人，抽号将从 {students.Count - excludedCount} 名在场学生中选择。"
            : "本节缺席已清空，全部学生都会参与抽号。";
    }

    private string BuildListOverviewText(IReadOnlyList<StudentRecord> students)
    {
        var text = new StringBuilder();
        text.AppendLine($"当前名单：{(string.IsNullOrWhiteSpace(_state.FileName) ? "未加载" : _state.FileName)}");
        text.AppendLine(BuildCompactListSummary(students));
        var excludedStudents = students.Where(IsStudentExcluded).ToList();
        if (excludedStudents.Count > 0)
        {
            text.AppendLine($"本节缺席：{excludedStudents.Count} 人");
        }

        text.AppendLine();
        text.AppendLine("小组人数：");

        foreach (var group in students
            .GroupBy(student => NormalizeGroupName(student.Group))
            .OrderBy(group => ParseSequenceSortKey(group.Key))
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            var groupTitle = string.Equals(group.Key, "未分组", StringComparison.OrdinalIgnoreCase)
                ? "未分组"
                : $"第 {group.Key} 组";
            text.AppendLine($"{groupTitle}：{group.Count()} 人");
        }

        var otherGenderGroups = students
            .Where(student => !IsGender(student.Gender, "男") && !IsGender(student.Gender, "女"))
            .GroupBy(student => string.IsNullOrWhiteSpace(student.Gender) ? "未填" : student.Gender.Trim())
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (otherGenderGroups.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("未识别性别字段：");
            foreach (var group in otherGenderGroups)
            {
                text.AppendLine($"{group.Key}：{group.Count()} 人");
            }
        }

        text.AppendLine();
        text.AppendLine("名单预览：");
        foreach (var student in students
            .OrderBy(student => ParseSequenceSortKey(student.Sequence))
            .ThenBy(student => student.Name, StringComparer.OrdinalIgnoreCase)
            .Take(30))
        {
            text.AppendLine(FormatRosterPreviewLine(student));
        }

        if (students.Count > 30)
        {
            text.AppendLine($"... 还有 {students.Count - 30} 人");
        }

        return text.ToString();
    }

    private string FormatRosterPreviewLine(StudentRecord student)
    {
        var gender = string.IsNullOrWhiteSpace(student.Gender) ? "未填" : student.Gender.Trim();
        var group = string.IsNullOrWhiteSpace(student.Group) ? "未分组" : $"第 {student.Group.Trim()} 组";
        return $"{student.DisplayName} · {gender} · {group} · {GetStudentLessonStatus(student)} · {GetStudentRoundStatus(student)}";
    }

    private string BuildCompactListSummary(IReadOnlyList<StudentRecord> students)
    {
        var maleCount = students.Count(student => IsGender(student.Gender, "男"));
        var femaleCount = students.Count(student => IsGender(student.Gender, "女"));
        var otherCount = students.Count - maleCount - femaleCount;
        var excludedCount = students.Count(IsStudentExcluded);
        var activeCount = Math.Max(0, students.Count - excludedCount);
        var groupCount = students
            .Select(student => student.Group.Trim())
            .Where(group => !string.IsNullOrWhiteSpace(group))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        var genderText = otherCount > 0
            ? $"男 {maleCount} 人 · 女 {femaleCount} 人 · 其他/未识别 {otherCount} 人"
            : $"男 {maleCount} 人 · 女 {femaleCount} 人";
        var groupText = groupCount > 0 ? groupCount.ToString() : "未分组";
        var summary = $"总人数：{students.Count}\n性别：{genderText}\n小组数：{groupText}\n{BuildStudentRoundOverview(students)}";
        return excludedCount > 0
            ? $"{summary}\n本节可抽：{activeCount} 人 · 缺席 {excludedCount} 人"
            : summary;
    }

    private string BuildStudentRoundOverview(IReadOnlyList<StudentRecord> students)
    {
        if (!_state.AvoidRepeatDraw)
        {
            return "本轮：未记录";
        }

        var activeKeys = students
            .Where(student => !IsStudentExcluded(student))
            .Select(GetStudentKey)
            .ToHashSet(StringComparer.Ordinal);
        if (activeKeys.Count == 0)
        {
            return "本轮：无在场学生";
        }

        var drawnCount = _state.DrawnStudentKeys
            .Where(activeKeys.Contains)
            .Distinct(StringComparer.Ordinal)
            .Count();
        var remainingCount = Math.Max(0, activeKeys.Count - drawnCount);
        return $"本轮：已抽 {drawnCount} 人 · 未抽 {remainingCount} 人";
    }

    private static bool IsGender(string value, string expected)
    {
        return string.Equals(value.Trim(), expected, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeGroupName(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "未分组" : value.Trim();
    }

    private void CopyCurrentRoster()
    {
        var students = GetStudents();
        if (students.Count == 0)
        {
            _hintLabel.Text = "暂无可复制的名单。";
            MessageBox.Show(this, "暂无可复制的名单。可以先试用、粘贴或导入名单。", "复制名单", MessageBoxButtons.OK, MessageBoxIcon.Information);
            UpdateRosterActionState();
            return;
        }

        try
        {
            Clipboard.SetText(BuildRosterExportText(students));
            _hintLabel.Text = $"名单已复制，共 {students.Count} 人。";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"复制名单失败：{ex.Message}", "复制名单", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void CopyUndrawnRoster()
    {
        if (!_state.AvoidRepeatDraw)
        {
            _hintLabel.Text = "未开启避免重复，暂无本轮未抽名单。";
            MessageBox.Show(this, "未开启“避免重复”时不会记录本轮已抽/未抽名单。", "复制未抽名单", MessageBoxButtons.OK, MessageBoxIcon.Information);
            UpdateRosterActionState();
            return;
        }

        var students = GetUndrawnStudentsForCurrentRound();
        if (students.Count == 0)
        {
            _hintLabel.Text = "本轮没有未抽学生。";
            MessageBox.Show(this, "本轮没有未抽学生。可以开始新一节课或重置本轮。", "复制未抽名单", MessageBoxButtons.OK, MessageBoxIcon.Information);
            UpdateRosterActionState();
            return;
        }

        try
        {
            Clipboard.SetText(BuildRosterExportText(students));
            _hintLabel.Text = $"未抽名单已复制，共 {students.Count} 人。";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"复制未抽名单失败：{ex.Message}", "复制未抽名单", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ExportUndrawnRoster()
    {
        if (!_state.AvoidRepeatDraw)
        {
            _hintLabel.Text = "未开启避免重复，暂无可导出的未抽名单。";
            MessageBox.Show(this, "未开启避免重复时不会记录本轮已抽/未抽名单。", "导出未抽名单", MessageBoxButtons.OK, MessageBoxIcon.Information);
            UpdateRosterActionState();
            return;
        }

        var students = GetUndrawnStudentsForCurrentRound();
        if (students.Count == 0)
        {
            _hintLabel.Text = "本轮没有可导出的未抽学生。";
            MessageBox.Show(this, "本轮没有未抽学生。可以开始新一节课或重置本轮。", "导出未抽名单", MessageBoxButtons.OK, MessageBoxIcon.Information);
            UpdateRosterActionState();
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "Excel 工作簿 (*.xlsx)|*.xlsx|CSV 文件 (*.csv)|*.csv|文本文件 (*.txt)|*.txt",
            Title = "导出未抽名单",
            FileName = $"未抽名单-{DateTime.Now:yyyyMMdd-HHmmss}.xlsx",
            DefaultExt = "xlsx",
            AddExtension = true,
            InitialDirectory = GetPreferredExportDirectory()
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        RememberExportDirectory(dialog.FileName);

        try
        {
            SaveRosterExport(dialog.FileName, students);
            _hintLabel.Text = $"未抽名单已导出：{Path.GetFileName(dialog.FileName)}";
            MessageBox.Show(this, $"未抽名单已导出到：\n{dialog.FileName}", "导出完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"导出未抽名单失败：{ex.Message}", "导出未抽名单", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
    private void CopyAbsentRoster()
    {
        var students = GetAbsentStudentsForLesson();
        if (students.Count == 0)
        {
            _hintLabel.Text = "本节没有缺席学生。";
            MessageBox.Show(this, "本节没有缺席学生。", "复制缺席名单", MessageBoxButtons.OK, MessageBoxIcon.Information);
            UpdateRosterActionState();
            return;
        }

        try
        {
            Clipboard.SetText(BuildRosterExportText(students));
            _hintLabel.Text = $"缺席名单已复制，共 {students.Count} 人。";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"复制缺席名单失败：{ex.Message}", "复制缺席名单", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ExportAbsentRoster()
    {
        var students = GetAbsentStudentsForLesson();
        if (students.Count == 0)
        {
            _hintLabel.Text = "本节没有可导出的缺席名单。";
            MessageBox.Show(this, "本节没有缺席学生。", "导出缺席名单", MessageBoxButtons.OK, MessageBoxIcon.Information);
            UpdateRosterActionState();
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "Excel 工作簿 (*.xlsx)|*.xlsx|CSV 文件 (*.csv)|*.csv|文本文件 (*.txt)|*.txt",
            Title = "导出缺席名单",
            FileName = $"缺席名单-{DateTime.Now:yyyyMMdd-HHmmss}.xlsx",
            DefaultExt = "xlsx",
            AddExtension = true,
            InitialDirectory = GetPreferredExportDirectory()
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        RememberExportDirectory(dialog.FileName);

        try
        {
            SaveRosterExport(dialog.FileName, students);
            _hintLabel.Text = $"缺席名单已导出：{Path.GetFileName(dialog.FileName)}";
            MessageBox.Show(this, $"缺席名单已导出到：\n{dialog.FileName}", "导出完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"导出缺席名单失败：{ex.Message}", "导出缺席名单", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
    private List<StudentRecord> GetPresentStudentsForLesson()
    {
        return GetActiveStudents()
            .OrderBy(student => ParseSequenceSortKey(student.Sequence))
            .ThenBy(student => student.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void CopyPresentRoster()
    {
        var students = GetPresentStudentsForLesson();
        if (students.Count == 0)
        {
            _hintLabel.Text = "本节没有在场学生。";
            MessageBox.Show(this, "本节没有在场学生。可以先导入名单，或在“本节缺席”里恢复学生。", "复制在场名单", MessageBoxButtons.OK, MessageBoxIcon.Information);
            UpdateRosterActionState();
            return;
        }

        try
        {
            Clipboard.SetText(BuildRosterExportText(students));
            _hintLabel.Text = $"在场名单已复制，共 {students.Count} 人。";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"复制在场名单失败：{ex.Message}", "复制在场名单", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ExportPresentRoster()
    {
        var students = GetPresentStudentsForLesson();
        if (students.Count == 0)
        {
            _hintLabel.Text = "本节没有可导出的在场名单。";
            MessageBox.Show(this, "本节没有在场学生。可以先导入名单，或在“本节缺席”里恢复学生。", "导出在场名单", MessageBoxButtons.OK, MessageBoxIcon.Information);
            UpdateRosterActionState();
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "Excel 工作簿 (*.xlsx)|*.xlsx|CSV 文件 (*.csv)|*.csv|文本文件 (*.txt)|*.txt",
            Title = "导出在场名单",
            FileName = $"在场名单-{DateTime.Now:yyyyMMdd-HHmmss}.xlsx",
            DefaultExt = "xlsx",
            AddExtension = true,
            InitialDirectory = GetPreferredExportDirectory()
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        RememberExportDirectory(dialog.FileName);

        try
        {
            SaveRosterExport(dialog.FileName, students);
            _hintLabel.Text = $"在场名单已导出：{Path.GetFileName(dialog.FileName)}";
            MessageBox.Show(this, $"在场名单已导出到：\n{dialog.FileName}", "导出完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"导出在场名单失败：{ex.Message}", "导出在场名单", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
    private void CopyDrawnRoster()
    {
        if (!_state.AvoidRepeatDraw)
        {
            _hintLabel.Text = "未开启避免重复，暂无本轮已抽名单。";
            MessageBox.Show(this, "未开启“避免重复”时不会记录本轮已抽/未抽名单。", "复制已抽名单", MessageBoxButtons.OK, MessageBoxIcon.Information);
            UpdateRosterActionState();
            return;
        }

        var students = GetDrawnStudentsForCurrentRound();
        if (students.Count == 0)
        {
            _hintLabel.Text = "本轮还没有已抽学生。";
            MessageBox.Show(this, "本轮还没有已抽学生。", "复制已抽名单", MessageBoxButtons.OK, MessageBoxIcon.Information);
            UpdateRosterActionState();
            return;
        }

        try
        {
            Clipboard.SetText(BuildRosterExportText(students));
            _hintLabel.Text = $"已抽名单已复制，共 {students.Count} 人。";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"复制已抽名单失败：{ex.Message}", "复制已抽名单", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ExportDrawnRoster()
    {
        if (!_state.AvoidRepeatDraw)
        {
            _hintLabel.Text = "未开启避免重复，暂无可导出的已抽名单。";
            MessageBox.Show(this, "未开启避免重复时不会记录本轮已抽/未抽名单。", "导出已抽名单", MessageBoxButtons.OK, MessageBoxIcon.Information);
            UpdateRosterActionState();
            return;
        }

        var students = GetDrawnStudentsForCurrentRound();
        if (students.Count == 0)
        {
            _hintLabel.Text = "本轮没有可导出的已抽学生。";
            MessageBox.Show(this, "本轮还没有已抽学生。", "导出已抽名单", MessageBoxButtons.OK, MessageBoxIcon.Information);
            UpdateRosterActionState();
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "Excel 工作簿 (*.xlsx)|*.xlsx|CSV 文件 (*.csv)|*.csv|文本文件 (*.txt)|*.txt",
            Title = "导出已抽名单",
            FileName = $"已抽名单-{DateTime.Now:yyyyMMdd-HHmmss}.xlsx",
            DefaultExt = "xlsx",
            AddExtension = true,
            InitialDirectory = GetPreferredExportDirectory()
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        RememberExportDirectory(dialog.FileName);

        try
        {
            SaveRosterExport(dialog.FileName, students);
            _hintLabel.Text = $"已抽名单已导出：{Path.GetFileName(dialog.FileName)}";
            MessageBox.Show(this, $"已抽名单已导出到：\n{dialog.FileName}", "导出完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"导出已抽名单失败：{ex.Message}", "导出已抽名单", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
    private List<StudentRecord> GetAbsentStudentsForLesson()
    {
        return GetStudents()
            .Where(IsStudentExcluded)
            .OrderBy(student => ParseSequenceSortKey(student.Sequence))
            .ThenBy(student => student.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private List<StudentRecord> GetUndrawnStudentsForCurrentRound()
    {
        if (!_state.AvoidRepeatDraw)
        {
            return new List<StudentRecord>();
        }

        return GetActiveStudents()
            .Where(student => !IsStudentDrawnThisRound(student))
            .OrderBy(student => ParseSequenceSortKey(student.Sequence))
            .ThenBy(student => student.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private List<StudentRecord> GetDrawnStudentsForCurrentRound()
    {
        if (!_state.AvoidRepeatDraw)
        {
            return new List<StudentRecord>();
        }

        return GetActiveStudents()
            .Where(IsStudentDrawnThisRound)
            .OrderBy(student => ParseSequenceSortKey(student.Sequence))
            .ThenBy(student => student.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void ExportCurrentRoster()
    {
        var students = GetStudents();
        if (students.Count == 0)
        {
            _hintLabel.Text = "暂无可导出的名单。";
            MessageBox.Show(this, "暂无可导出的名单。可以先试用、粘贴或导入名单。", "导出名单", MessageBoxButtons.OK, MessageBoxIcon.Information);
            UpdateRosterActionState();
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "Excel 工作簿 (*.xlsx)|*.xlsx|CSV 文件 (*.csv)|*.csv|文本文件 (*.txt)|*.txt",
            Title = "导出当前名单",
            FileName = $"抽号机名单-{DateTime.Now:yyyyMMdd-HHmmss}.xlsx",
            DefaultExt = "xlsx",
            AddExtension = true,
            InitialDirectory = GetPreferredExportDirectory()
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        RememberExportDirectory(dialog.FileName);

        try
        {
            SaveRosterExport(dialog.FileName, students);
            var linkedAsCurrentFile = TryLinkCurrentRosterExport(_state, dialog.FileName);
            if (linkedAsCurrentFile)
            {
                _state.Save();
                UpdateClassroomStatusDisplay();
                UpdateRosterActionState();
            }

            _hintLabel.Text = linkedAsCurrentFile
                ? $"名单已导出并设为当前文件：{Path.GetFileName(dialog.FileName)}"
                : $"名单已导出：{Path.GetFileName(dialog.FileName)}";
            var message = linkedAsCurrentFile
                ? $"当前名单已导出到：\n{dialog.FileName}\n\n这份文件已设为当前名单文件。之后可点“打开文件”编辑，保存后点“重载文件”刷新。"
                : $"当前名单已导出到：\n{dialog.FileName}";
            MessageBox.Show(this, message, "导出完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"导出失败：{ex.Message}", "导出失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static bool TryLinkCurrentRosterExport(AppState state, string fileName)
    {
        if (state is null || string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(fileName);
        }
        catch
        {
            return false;
        }

        if (!IsSupportedImportFile(fullPath) || !File.Exists(fullPath))
        {
            return false;
        }

        state.SourceFilePath = fullPath;
        state.FileName = Path.GetFileName(fullPath);
        state.RecentRosterFiles ??= new();
        state.RecentRosterFiles = BuildNormalizedRecentRosterFiles(new[] { fullPath }.Concat(state.RecentRosterFiles));
        return true;
    }

    private void SaveRosterExport(string fileName, IReadOnlyList<StudentRecord> students)
    {
        SaveTableRows(fileName, BuildRosterExportRows(students), "当前名单");
    }

    private string BuildRosterExportText(IReadOnlyList<StudentRecord> students)
    {
        return BuildTabDelimitedText(BuildRosterExportRows(students));
    }

    private string[][] BuildRosterExportRows(IReadOnlyList<StudentRecord> students)
    {
        return new[] { new[] { "序号", "姓名", "性别", "小组", "本节状态", "本轮状态" } }
            .Concat(students
            .OrderBy(student => ParseSequenceSortKey(student.Sequence))
            .ThenBy(student => student.Name, StringComparer.OrdinalIgnoreCase)
            .Select(student => new[]
            {
                student.Sequence.Trim(),
                student.Name.Trim(),
                student.Gender.Trim(),
                student.Group.Trim(),
                GetStudentLessonStatus(student),
                GetStudentRoundStatus(student)
            }))
            .ToArray();
    }

    private void ShowHistoryOverview()
    {
        var historyLines = GetVisibleHistoryLines();
        if (historyLines.Count == 0)
        {
            _hintLabel.Text = "暂无可查看的抽号历史。";
            MessageBox.Show(this, "暂无可查看的抽号历史。", "查看历史", MessageBoxButtons.OK, MessageBoxIcon.Information);
            UpdateHistoryActionState();
            return;
        }

        var visibleHistoryLines = new List<string>();
        using var dialog = new Form
        {
            Text = "查看历史",
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            ShowInTaskbar = false,
            FormBorderStyle = FormBorderStyle.Sizable,
            ClientSize = new Size(640, 620),
            MinimumSize = new Size(520, 460),
            Font = new Font("Microsoft YaHei UI", 10)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            RowCount = 4,
            ColumnCount = 1
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var summaryLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ForeColor = Color.FromArgb(17, 38, 61),
            Font = new Font("Microsoft YaHei UI", 10, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 10)
        };

        var searchBox = new TextBox
        {
            Dock = DockStyle.Fill,
            PlaceholderText = "搜索时间或结果",
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 0, 0, 10)
        };

        var list = new ListBox
        {
            Dock = DockStyle.Fill,
            IntegralHeight = false,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(248, 251, 255),
            ForeColor = Color.FromArgb(17, 38, 61),
            HorizontalScrollbar = true
        };

        bool MatchesHistorySearch(string line)
        {
            var query = searchBox.Text.Trim();
            return string.IsNullOrWhiteSpace(query)
                || line.Contains(query, StringComparison.OrdinalIgnoreCase);
        }

        void RefreshHistoryList()
        {
            visibleHistoryLines = historyLines.Where(MatchesHistorySearch).ToList();
            list.BeginUpdate();
            list.Items.Clear();
            foreach (var line in visibleHistoryLines)
            {
                list.Items.Add(line);
            }

            list.EndUpdate();
            var sourceText = string.IsNullOrWhiteSpace(_state.FileName) ? "未加载名单" : Path.GetFileName(_state.FileName);
            var visibleText = visibleHistoryLines.Count == historyLines.Count
                ? string.Empty
                : $"\n当前显示：{visibleHistoryLines.Count} 条";
            summaryLabel.Text = $"历史记录：{historyLines.Count} 条\n当前名单：{sourceText}{visibleText}";
        }

        void CopyHistoryFromDialog(IReadOnlyList<string> rows, string emptyMessage, string copiedMessage)
        {
            if (rows.Count == 0)
            {
                _hintLabel.Text = emptyMessage;
                return;
            }

            try
            {
                Clipboard.SetText(BuildHistoryExportText(rows));
                _hintLabel.Text = copiedMessage;
            }
            catch (Exception ex)
            {
                MessageBox.Show(dialog, $"复制历史失败：{ex.Message}", "查看历史", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void ExportHistoryFromDialog(IReadOnlyList<string> rows, string emptyMessage)
        {
            if (rows.Count == 0)
            {
                _hintLabel.Text = emptyMessage;
                return;
            }

            using var exportDialog = new SaveFileDialog
            {
                Filter = "Excel 工作簿 (*.xlsx)|*.xlsx|CSV 文件 (*.csv)|*.csv|文本文件 (*.txt)|*.txt",
                Title = "导出筛选历史",
                FileName = $"抽号历史筛选-{DateTime.Now:yyyyMMdd-HHmmss}.xlsx",
                DefaultExt = "xlsx",
                AddExtension = true,
                InitialDirectory = GetPreferredExportDirectory()
            };

            if (exportDialog.ShowDialog(dialog) != DialogResult.OK)
            {
                return;
            }

            RememberExportDirectory(exportDialog.FileName);

            try
            {
                SaveHistoryExport(exportDialog.FileName, rows);
                _hintLabel.Text = $"筛选历史已导出：{Path.GetFileName(exportDialog.FileName)}";
                MessageBox.Show(dialog, $"筛选历史已导出到：\n{exportDialog.FileName}", "导出完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(dialog, $"导出筛选历史失败：{ex.Message}", "查看历史", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        searchBox.TextChanged += (_, _) => RefreshHistoryList();
        RefreshHistoryList();

        var buttonPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 14, 0, 0)
        };

        var closeButton = new Button { Text = "关闭", DialogResult = DialogResult.OK, AutoSize = true, Padding = new Padding(18, 6, 18, 6) };
        var copyAllButton = new Button { Text = "复制全部", AutoSize = true, Padding = new Padding(18, 6, 18, 6) };
        var copyVisibleButton = new Button { Text = "复制当前筛选", AutoSize = true, Padding = new Padding(18, 6, 18, 6) };
        var exportVisibleButton = new Button { Text = "导出当前筛选", AutoSize = true, Padding = new Padding(18, 6, 18, 6) };
        copyAllButton.Click += (_, _) => CopyHistoryFromDialog(historyLines, "暂无可复制的抽号历史。", $"抽号历史已复制，共 {historyLines.Count} 条。");
        copyVisibleButton.Click += (_, _) => CopyHistoryFromDialog(visibleHistoryLines, "当前筛选没有历史记录。", $"当前筛选历史已复制，共 {visibleHistoryLines.Count} 条。");
        exportVisibleButton.Click += (_, _) => ExportHistoryFromDialog(visibleHistoryLines, "当前筛选没有历史记录。");

        buttonPanel.Controls.Add(closeButton);
        buttonPanel.Controls.Add(copyAllButton);
        buttonPanel.Controls.Add(copyVisibleButton);
        buttonPanel.Controls.Add(exportVisibleButton);

        layout.Controls.Add(summaryLabel, 0, 0);
        layout.Controls.Add(searchBox, 0, 1);
        layout.Controls.Add(list, 0, 2);
        layout.Controls.Add(buttonPanel, 0, 3);
        dialog.Controls.Add(layout);
        dialog.Shown += (_, _) => searchBox.Focus();
        dialog.ShowDialog(this);
    }

    private void ExportDrawHistory()
    {
        var historyLines = GetVisibleHistoryLines();
        if (historyLines.Count == 0)
        {
            _hintLabel.Text = "暂无可导出的抽号历史。";
            MessageBox.Show(this, "暂无可导出的抽号历史。", "导出历史", MessageBoxButtons.OK, MessageBoxIcon.Information);
            UpdateHistoryActionState();
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "Excel 工作簿 (*.xlsx)|*.xlsx|CSV 文件 (*.csv)|*.csv|文本文件 (*.txt)|*.txt",
            Title = "导出抽号历史",
            FileName = $"抽号历史-{DateTime.Now:yyyyMMdd-HHmmss}.xlsx",
            DefaultExt = "xlsx",
            AddExtension = true,
            InitialDirectory = GetPreferredExportDirectory()
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        RememberExportDirectory(dialog.FileName);

        try
        {
            SaveHistoryExport(dialog.FileName, historyLines);
            _hintLabel.Text = $"抽号历史已导出：{Path.GetFileName(dialog.FileName)}";
            MessageBox.Show(this, $"抽号历史已导出到：\n{dialog.FileName}", "导出完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"导出失败：{ex.Message}", "导出失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void SaveHistoryExport(string fileName, IReadOnlyList<string> historyLines)
    {
        if (string.Equals(Path.GetExtension(fileName), ".txt", StringComparison.OrdinalIgnoreCase))
        {
            File.WriteAllText(fileName, BuildHistoryExportText(historyLines), Encoding.UTF8);
            return;
        }

        SaveTableRows(fileName, BuildHistoryExportRows(historyLines), "抽号历史");
    }

    private string BuildHistoryExportText(IReadOnlyList<string> historyLines)
    {
        var lines = new List<string>
        {
            "抽号机历史记录",
            $"导出时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            $"当前名单：{(string.IsNullOrWhiteSpace(_state.FileName) ? "未加载" : _state.FileName)}",
            $"记录数量：{historyLines.Count}",
            new string('-', 32)
        };
        lines.AddRange(historyLines);
        lines.Add(string.Empty);
        return string.Join(Environment.NewLine, lines);
    }

    private static string[][] BuildHistoryExportRows(IReadOnlyList<string> historyLines)
    {
        return new[] { new[] { "序号", "时间", "结果" } }
            .Concat(historyLines.Select((line, index) =>
            {
                var (time, result) = SplitHistoryLine(line);
                return new[] { (index + 1).ToString(), time, result };
            }))
            .ToArray();
    }

    private static (string Time, string Result) SplitHistoryLine(string line)
    {
        var value = line.Trim();
        if (value.Length > 20
            && value[4] == '-'
            && value[7] == '-'
            && value[10] == ' '
            && value[13] == ':'
            && value[16] == ':')
        {
            return (value[..19], value[20..].Trim());
        }

        return (string.Empty, value);
    }

    private void ClearDrawHistory()
    {
        if (_state.DrawHistory.Count == 0 && string.IsNullOrWhiteSpace(_state.LastWinner))
        {
            _hintLabel.Text = "暂无抽号历史可清空。";
            UpdateHistoryActionState();
            return;
        }

        var result = MessageBox.Show(
            this,
            "确定要清空抽号历史吗？\n\n此操作不会清空名单，也不会重置“避免重复”的本轮记录。",
            "清空抽号历史",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (result != DialogResult.Yes)
        {
            return;
        }

        _state.DrawHistory.Clear();
        _state.LastWinner = string.Empty;
        ClearUndoSnapshot();
        _state.Save();
        UpdateHistoryDisplay();
        UpdateHistoryActionState();
        UpdateUndoDrawButton();
        SetIdleResultText();
        _hintLabel.Text = "抽号历史已清空，名单和避免重复本轮记录不变。";
    }

    private void StartNewLesson()
    {
        if (_isDrawing)
        {
            _hintLabel.Text = "抽号进行中，结束后再开始新一节课。";
            MessageBox.Show(this, "抽号进行中，结束后再开始新一节课。", "暂不能开始新课", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!CanUseNewLessonAction())
        {
            _hintLabel.Text = "暂无名单，先导入或粘贴本班名单后再开始新课。";
            MessageBox.Show(this, "暂无名单。请先导入、粘贴或载入试用名单，再开始新一节课。", "新一节课", MessageBoxButtons.OK, MessageBoxIcon.Information);
            UpdateLessonActionState();
            return;
        }

        if (!CanStartNewLesson())
        {
            _hintLabel.Text = "当前已是新课状态：名单已保留，可以直接抽号；如有缺席，先点“本节缺席”。";
            MessageBox.Show(this, "当前已是新课状态：没有上节历史、上次结果、本节缺席或本轮避免重复记录。\n\n可以直接开始抽号；如果今天有人缺席，先点“本节缺席”。", "新一节课", MessageBoxButtons.OK, MessageBoxIcon.Information);
            UpdateLessonActionState();
            return;
        }

        var result = MessageBox.Show(
            this,
            "开始新一节课吗？\n\n将清空抽号历史、上次结果、撤销记录、本节缺席和“避免重复”的本轮记录。\n名单、最近名单和功能设置都会保留。",
            "新一节课",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button1);
        if (result != DialogResult.Yes)
        {
            return;
        }

        _state.DrawHistory.Clear();
        _state.LastWinner = string.Empty;
        _state.ExcludedStudentKeys.Clear();
        _state.DrawnStudentKeys.Clear();
        _state.DrawnGroupKeys.Clear();
        ClearUndoSnapshot();
        _state.Save();
        UpdateHistoryDisplay();
        UpdateHistoryActionState();
        UpdateResetRoundButton();
        UpdateUndoDrawButton();
        SetIdleResultText();
        _hintLabel.Text = "已开始新一节课：名单保留，历史、本节缺席和本轮避免重复记录已清空。";
    }

    private void CopyCurrentResult()
    {
        var text = GetCopyableResultText();
        if (string.IsNullOrWhiteSpace(text))
        {
            _hintLabel.Text = "暂无可复制的抽号结果。";
            MessageBox.Show(this, "暂无可复制的抽号结果。", "复制结果", MessageBoxButtons.OK, MessageBoxIcon.Information);
            UpdateHistoryActionState();
            return;
        }

        try
        {
            Clipboard.SetText(text);
            _hintLabel.Text = $"已复制结果：{TrimForHint(text)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"复制失败：{ex.Message}", "复制结果", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void CopyLessonSummary()
    {
        if (!CanCopyLessonSummary())
        {
            _hintLabel.Text = "暂无可复制的课堂摘要。";
            MessageBox.Show(this, "暂无可复制的课堂摘要。可以先导入名单或完成一次抽号。", "复制课堂摘要", MessageBoxButtons.OK, MessageBoxIcon.Information);
            UpdateHistoryActionState();
            UpdateRosterActionState();
            return;
        }

        try
        {
            Clipboard.SetText(BuildLessonSummaryText());
            _hintLabel.Text = "课堂摘要已复制。";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"复制课堂摘要失败：{ex.Message}", "复制课堂摘要", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ExportLessonSummary()
    {
        if (!CanCopyLessonSummary())
        {
            _hintLabel.Text = "暂无可导出的课堂摘要。";
            MessageBox.Show(this, "暂无可导出的课堂摘要。可以先导入名单或完成一次抽号。", "导出课堂摘要", MessageBoxButtons.OK, MessageBoxIcon.Information);
            UpdateHistoryActionState();
            UpdateRosterActionState();
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "文本文件 (*.txt)|*.txt",
            Title = "导出课堂摘要",
            FileName = $"课堂摘要-{DateTime.Now:yyyyMMdd-HHmmss}.txt",
            DefaultExt = "txt",
            AddExtension = true,
            InitialDirectory = GetPreferredExportDirectory()
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        RememberExportDirectory(dialog.FileName);

        try
        {
            File.WriteAllText(dialog.FileName, BuildLessonSummaryText(), Encoding.UTF8);
            _hintLabel.Text = $"课堂摘要已导出：{Path.GetFileName(dialog.FileName)}";
            MessageBox.Show(this, $"课堂摘要已导出到：\n{dialog.FileName}", "导出完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"导出课堂摘要失败：{ex.Message}", "导出课堂摘要", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
    private string GetPreferredExportDirectory()
    {
        var directory = _state.LastExportDirectory;
        if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
        {
            return directory;
        }

        return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
    }

    private void RememberExportDirectory(string fileName)
    {
        var directory = Path.GetDirectoryName(fileName);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return;
        }

        _state.LastExportDirectory = directory;
        _state.Save();
    }

    private string GetPreferredLessonPackageDirectory()
    {
        var directory = _state.LastLessonPackageDirectory;
        if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
        {
            return directory;
        }

        return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
    }

    private bool CanOpenLastLessonPackageDirectory()
    {
        var directory = _state.LastLessonPackageDirectory;
        return !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory);
    }

    private void OpenLastLessonPackageDirectory()
    {
        var directory = _state.LastLessonPackageDirectory;
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            _hintLabel.Text = "还没有可打开的课堂包位置。";
            MessageBox.Show(this, "还没有可打开的课堂包位置。先导出一次课堂包后，这里会记住上次选择的位置。", "打开课堂包位置", MessageBoxButtons.OK, MessageBoxIcon.Information);
            UpdateHistoryActionState();
            UpdateRosterActionState();
            return;
        }

        OpenFolderInExplorer(directory);
    }

    private void ExportLessonPackage()
    {
        if (!CanExportLessonPackage())
        {
            _hintLabel.Text = "暂无可导出的课堂包。";
            MessageBox.Show(this, "暂无可导出的课堂包。可以先导入名单或完成一次抽号。", "导出课堂包", MessageBoxButtons.OK, MessageBoxIcon.Information);
            UpdateHistoryActionState();
            UpdateRosterActionState();
            return;
        }

        using var dialog = new FolderBrowserDialog
        {
            Description = "选择课堂包保存位置",
            SelectedPath = GetPreferredLessonPackageDirectory(),
            ShowNewFolderButton = true
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _state.LastLessonPackageDirectory = dialog.SelectedPath;
        _state.Save();

        try
        {
            var packageDirectory = Path.Combine(dialog.SelectedPath, $"抽号机课堂包-{DateTime.Now:yyyyMMdd-HHmmss}");
            Directory.CreateDirectory(packageDirectory);

            var exportedFiles = new List<string>();
            var summaryPath = Path.Combine(packageDirectory, "课堂摘要.txt");
            File.WriteAllText(summaryPath, BuildLessonSummaryText(), Encoding.UTF8);
            exportedFiles.Add(summaryPath);

            var students = GetStudents();
            if (students.Count > 0)
            {
                var rosterPath = Path.Combine(packageDirectory, "当前名单.xlsx");
                SaveRosterExport(rosterPath, students);
                exportedFiles.Add(rosterPath);
            }

            var presentStudents = GetPresentStudentsForLesson();
            if (presentStudents.Count > 0)
            {
                var presentPath = Path.Combine(packageDirectory, "在场名单.xlsx");
                SaveRosterExport(presentPath, presentStudents);
                exportedFiles.Add(presentPath);
            }

            var absentStudents = GetAbsentStudentsForLesson();
            if (absentStudents.Count > 0)
            {
                var absentPath = Path.Combine(packageDirectory, "缺席名单.xlsx");
                SaveRosterExport(absentPath, absentStudents);
                exportedFiles.Add(absentPath);
            }

            var drawnStudents = GetDrawnStudentsForCurrentRound();
            if (drawnStudents.Count > 0)
            {
                var drawnPath = Path.Combine(packageDirectory, "已抽名单.xlsx");
                SaveRosterExport(drawnPath, drawnStudents);
                exportedFiles.Add(drawnPath);
            }

            var undrawnStudents = GetUndrawnStudentsForCurrentRound();
            if (undrawnStudents.Count > 0)
            {
                var undrawnPath = Path.Combine(packageDirectory, "未抽名单.xlsx");
                SaveRosterExport(undrawnPath, undrawnStudents);
                exportedFiles.Add(undrawnPath);
            }

            var historyLines = GetVisibleHistoryLines();
            if (historyLines.Count > 0)
            {
                var historyPath = Path.Combine(packageDirectory, "抽号历史.xlsx");
                SaveHistoryExport(historyPath, historyLines);
                exportedFiles.Add(historyPath);
            }

            var manifestPath = Path.Combine(packageDirectory, "导出清单.txt");
            File.WriteAllText(manifestPath, BuildLessonPackageManifest(packageDirectory, exportedFiles), Encoding.UTF8);
            exportedFiles.Add(manifestPath);

            _hintLabel.Text = $"课堂包已导出：{Path.GetFileName(packageDirectory)}";
            var openResult = MessageBox.Show(this, $"课堂包已导出到：\n{packageDirectory}\n\n共 {exportedFiles.Count} 个文件。\n\n现在打开这个文件夹吗？", "导出完成", MessageBoxButtons.YesNo, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);
            if (openResult == DialogResult.Yes)
            {
                OpenFolderInExplorer(packageDirectory);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"导出课堂包失败：{ex.Message}", "导出课堂包", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private string BuildLessonPackageManifest(string packageDirectory, IReadOnlyList<string> exportedFiles)
    {
        var lines = new List<string>
        {
            "抽号机课堂包导出清单",
            $"生成时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            $"文件夹：{packageDirectory}",
            $"文件数量：{exportedFiles.Count + 1}",
            string.Empty,
            "文件列表"
        };

        foreach (var file in exportedFiles)
        {
            var fileName = Path.GetFileName(file);
            lines.Add($"- {fileName}：{DescribeLessonPackageFile(fileName)}");
        }

        lines.Add("- 导出清单.txt：本文件，方便快速核对课堂包内容。");
        lines.Add(string.Empty);
        lines.Add("建议：需要发给班主任或备课留档时，直接发送整个课堂包文件夹。");
        return string.Join(Environment.NewLine, lines);
    }

    private static string DescribeLessonPackageFile(string fileName)
    {
        if (string.Equals(fileName, "课堂摘要.txt", StringComparison.OrdinalIgnoreCase))
        {
            return "本节课的名单、人数、缺席、抽号和历史摘要。";
        }

        if (string.Equals(fileName, "当前名单.xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return "原始完整名单，适合复核导入内容。";
        }

        if (string.Equals(fileName, "在场名单.xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return "排除本节缺席后的可抽学生名单。";
        }

        if (string.Equals(fileName, "缺席名单.xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return "本节标记为缺席的学生名单。";
        }

        if (string.Equals(fileName, "已抽名单.xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return "开启避免重复后，本轮已经抽到的学生名单。";
        }

        if (string.Equals(fileName, "未抽名单.xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return "开启避免重复后，本轮尚未抽到的学生名单。";
        }

        if (string.Equals(fileName, "抽号历史.xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return "本节课的抽号历史记录。";
        }

        return "课堂相关导出文件。";
    }
    private void OpenFolderInExplorer(string folderPath)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = folderPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"打开文件夹失败：{ex.Message}", "打开文件夹", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
    private bool CanExportLessonPackage()
    {
        return CanCopyLessonSummary();
    }
    private bool CanCopyLessonSummary()
    {
        return GetStudents().Count > 0
            || GetVisibleHistoryLines().Count > 0
            || !string.IsNullOrWhiteSpace(_state.LastWinner);
    }

    private string BuildLessonSummaryText()
    {
        var students = GetStudents();
        var activeStudents = GetPresentStudentsForLesson();
        var absentStudents = GetAbsentStudentsForLesson();
        var drawnStudents = GetDrawnStudentsForCurrentRound();
        var undrawnStudents = GetUndrawnStudentsForCurrentRound();
        var history = GetVisibleHistoryLines();
        var lines = new List<string>
        {
            "抽号机课堂摘要",
            $"生成时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            $"当前名单：{(string.IsNullOrWhiteSpace(_state.FileName) ? "未加载" : _state.FileName)}",
            $"人数：总 {students.Count} · 在场 {activeStudents.Count} · 缺席 {absentStudents.Count}",
            $"历史记录：{history.Count} 条"
        };

        if (_state.AvoidRepeatDraw && students.Count > 0)
        {
            lines.Add($"本轮学生：已抽 {drawnStudents.Count} · 未抽 {undrawnStudents.Count}");
        }
        else
        {
            lines.Add("本轮学生：未启用避免重复记录");
        }

        var lastResult = GetCopyableResultText();
        if (!string.IsNullOrWhiteSpace(lastResult))
        {
            lines.Add($"最近结果：{lastResult}");
        }

        if (absentStudents.Count > 0 && activeStudents.Count > 0)
        {
            lines.Add($"在场名单：{FormatStudentNameList(activeStudents)}");
        }

        if (absentStudents.Count > 0)
        {
            lines.Add($"缺席名单：{FormatStudentNameList(absentStudents)}");
        }

        if (_state.AvoidRepeatDraw && drawnStudents.Count > 0)
        {
            lines.Add($"已抽名单：{FormatStudentNameList(drawnStudents)}");
        }

        if (_state.AvoidRepeatDraw && undrawnStudents.Count > 0)
        {
            lines.Add($"未抽名单：{FormatStudentNameList(undrawnStudents)}");
        }

        lines.Add(string.Empty);
        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatStudentNameList(IReadOnlyList<StudentRecord> students, int limit = 30)
    {
        if (students.Count == 0)
        {
            return "无";
        }

        var names = string.Join("、", students.Take(limit).Select(student => student.DisplayName));
        return students.Count > limit ? $"{names}……另 {students.Count - limit} 人" : names;
    }

    private void TryAutoCopyResult(string historyEntry)
    {
        if (!_state.AutoCopyResult)
        {
            return;
        }

        var text = StripHistoryTimestamp(historyEntry);
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        try
        {
            Clipboard.SetText(text);
            _hintLabel.Text = $"已自动复制结果：{TrimForHint(text)}";
        }
        catch (Exception ex)
        {
            _hintLabel.Text = $"自动复制失败：{ex.Message}";
        }
    }

    private string GetCopyableResultText()
    {
        var history = GetVisibleHistoryLines();
        if (history.Count == 0)
        {
            return string.Empty;
        }

        return StripHistoryTimestamp(history[^1]);
    }

    private static string StripHistoryTimestamp(string value)
    {
        return value.Length > 20 && value[4] == '-' && value[7] == '-' && value[13] == ':' && value[16] == ':'
            ? value[20..].Trim()
            : value.Trim();
    }

    private static string TrimForHint(string value)
    {
        value = value.ReplaceLineEndings(" ");
        return value.Length <= 32 ? value : value[..32] + "...";
    }

    private static string TrimForStatusLine(string value)
    {
        value = value.ReplaceLineEndings(" ").Trim();
        return value.Length <= 18 ? value : value[..18] + "...";
    }

    private void ShowStartGuide()
    {
        MarkStartGuideShown();

        using var dialog = new Form
        {
            Text = "上课向导",
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            ClientSize = new Size(800, 460),
            Font = new Font("Microsoft YaHei UI", 10),
            BackColor = Color.FromArgb(248, 251, 255)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            RowCount = 3,
            ColumnCount = 1
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var titleLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Text = "上课向导",
            ForeColor = Color.FromArgb(17, 38, 61),
            Font = new Font("Microsoft YaHei UI", 16, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 12)
        };

        var statusBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(17, 38, 61),
            Font = new Font("Microsoft YaHei UI", 10),
            Text = BuildStartGuideText()
        };

        var buttonPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Dock = DockStyle.Fill,
            WrapContents = true,
            Margin = new Padding(0, 14, 0, 0)
        };

        void RunGuideAction(Action action)
        {
            dialog.Close();
            BeginInvoke(new MethodInvoker(action));
        }

        var students = GetStudents();
        var presentStudents = GetPresentStudentsForLesson();
        var canUseRoster = students.Count > 0 && !_isDrawing;
        var canDraw = presentStudents.Count > 0 && !_isDrawing;
        var canQuickDemoDraw = students.Count == 0 && !_isDrawing;
        var canUseNewLessonAction = CanUseNewLessonAction();
        var canStartNewLesson = CanStartNewLesson();

        var closeButton = new Button { Text = "关闭", DialogResult = DialogResult.OK, AutoSize = true, Padding = new Padding(18, 6, 18, 6) };
        var quickGuideButton = new Button { Text = "操作速查", AutoSize = true, Padding = new Padding(18, 6, 18, 6) };
        var templateButton = new Button { Text = "名单模板", AutoSize = true, Padding = new Padding(18, 6, 18, 6), Enabled = !_isDrawing };
        var importButton = new Button { Text = "导入文件", AutoSize = true, Padding = new Padding(18, 6, 18, 6), Enabled = !_isDrawing };
        var pasteButton = new Button { Text = "粘贴名单", AutoSize = true, Padding = new Padding(18, 6, 18, 6), Enabled = !_isDrawing };
        var demoButton = new Button { Text = "试用名单", AutoSize = true, Padding = new Padding(18, 6, 18, 6), Enabled = !_isDrawing };
        var quickDemoDrawButton = new Button { Text = DemoDrawActionText, AutoSize = true, Padding = new Padding(18, 6, 18, 6), Enabled = canQuickDemoDraw };
        var newLessonButton = new Button { Text = canStartNewLesson ? "新一节课" : "新课就绪", AutoSize = true, Padding = new Padding(18, 6, 18, 6), Enabled = canUseNewLessonAction };
        var absenceButton = new Button { Text = "本节缺席", AutoSize = true, Padding = new Padding(18, 6, 18, 6), Enabled = canUseRoster };
        var startDrawButton = new Button { Text = CoreDrawActionText, AutoSize = true, Padding = new Padding(18, 6, 18, 6), Enabled = canDraw };
        quickGuideButton.Click += (_, _) => RunGuideAction(ShowQuickGuide);
        templateButton.Click += (_, _) => RunGuideAction(ShowRosterTemplateOptions);
        importButton.Click += (_, _) => RunGuideAction(ImportNames);
        pasteButton.Click += (_, _) => RunGuideAction(ImportNamesFromClipboard);
        demoButton.Click += (_, _) => RunGuideAction(LoadDemoRoster);
        quickDemoDrawButton.Click += (_, _) => RunGuideAction(StartDemoDraw);
        newLessonButton.Click += (_, _) => RunGuideAction(StartNewLesson);
        absenceButton.Click += (_, _) => RunGuideAction(EditLessonAbsences);
        startDrawButton.Click += (_, _) => RunGuideAction(StartDraw);
        static void StyleGuideButton(Button button, bool primary)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = primary ? Color.FromArgb(13, 99, 201) : Color.FromArgb(196, 210, 224);
            button.BackColor = primary ? Color.FromArgb(13, 99, 201) : Color.White;
            button.ForeColor = primary ? Color.White : Color.FromArgb(17, 38, 61);
            button.Font = new Font("Microsoft YaHei UI", 10, primary ? FontStyle.Bold : FontStyle.Regular);
        }

        var primaryGuideButton = canStartNewLesson
            ? newLessonButton
            : canDraw
                ? startDrawButton
                : canQuickDemoDraw
                    ? quickDemoDrawButton
                    : canUseRoster
                        ? absenceButton
                        : pasteButton;

        foreach (var button in new[]
        {
            closeButton,
            quickGuideButton,
            templateButton,
            importButton,
            pasteButton,
            demoButton,
            quickDemoDrawButton,
            newLessonButton,
            absenceButton,
            startDrawButton
        })
        {
            StyleGuideButton(button, ReferenceEquals(button, primaryGuideButton));
        }

        void AddGuideButton(Button button, bool include = true)
        {
            if (include)
            {
                buttonPanel.Controls.Add(button);
            }
        }

        if (students.Count == 0)
        {
            AddGuideButton(quickDemoDrawButton, canQuickDemoDraw);
            AddGuideButton(pasteButton, !_isDrawing);
            AddGuideButton(importButton, !_isDrawing);
            AddGuideButton(templateButton, !_isDrawing);
            AddGuideButton(demoButton, !_isDrawing);
            AddGuideButton(quickGuideButton);
        }
        else
        {
            AddGuideButton(newLessonButton, canUseNewLessonAction);
            AddGuideButton(startDrawButton, canDraw);
            AddGuideButton(absenceButton, canUseRoster);
            AddGuideButton(importButton, !_isDrawing);
            AddGuideButton(pasteButton, !_isDrawing);
            AddGuideButton(templateButton, !_isDrawing);
            AddGuideButton(quickGuideButton);
        }

        AddGuideButton(closeButton);
        layout.Controls.Add(titleLabel, 0, 0);
        layout.Controls.Add(statusBox, 0, 1);
        layout.Controls.Add(buttonPanel, 0, 2);
        dialog.Controls.Add(layout);
        dialog.AcceptButton = primaryGuideButton.Enabled ? primaryGuideButton : closeButton;
        dialog.CancelButton = closeButton;
        _hintLabel.Text = "上课向导已打开。";
        dialog.ShowDialog(this);
    }

    private void MarkStartGuideShown()
    {
        if (_state.StartGuideShown)
        {
            return;
        }

        _state.StartGuideShown = true;
        _state.Save();
    }
    private string BuildStartGuideText()
    {
        var students = GetStudents();
        var presentStudents = GetPresentStudentsForLesson();
        var absentStudents = GetAbsentStudentsForLesson();
        var sourceText = students.Count == 0
            ? "待准备"
            : string.IsNullOrWhiteSpace(_state.FileName) ? "未加载" : _state.FileName;
        var lines = new List<string>
        {
            $"当前名单：{sourceText}",
            $"人数：总 {students.Count} · 在场 {presentStudents.Count} · 缺席 {absentStudents.Count}",
            $"当前状态：{(_isDrawing ? "抽号中" : "可操作")}",
            string.Empty,
            students.Count == 0
                ? "建议：点粘贴名单或导入文件准备本班名单；想先试流程，点“试用抽号”。"
                : CanStartNewLesson()
                    ? "建议：如果这是新一节课，先点“新一节课”清空上节记录；再确认本节缺席并开始抽号。"
                    : "建议：当前已是新课状态，先确认本节缺席；名单无误可以直接开始抽号。"
        };

        return string.Join(Environment.NewLine, lines);
    }
    private void ShowQuickGuide()
    {
        using var dialog = new Form
        {
            Text = "操作速查",
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            ClientSize = new Size(620, 560),
            Font = new Font("Microsoft YaHei UI", 10),
            BackColor = Color.FromArgb(248, 251, 255)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            RowCount = 3,
            ColumnCount = 1
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var titleLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Text = "抽号机操作速查",
            ForeColor = Color.FromArgb(17, 38, 61),
            Font = new Font("Microsoft YaHei UI", 16, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 12)
        };

        var guideText = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(17, 38, 61),
            Font = new Font("Microsoft YaHei UI", 10),
            Text = BuildQuickGuideText()
        };

        var buttonPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 14, 0, 0)
        };
        var closeButton = new Button { Text = "关闭", DialogResult = DialogResult.OK, AutoSize = true, Padding = new Padding(18, 6, 18, 6) };
        buttonPanel.Controls.Add(closeButton);

        layout.Controls.Add(titleLabel, 0, 0);
        layout.Controls.Add(guideText, 0, 1);
        layout.Controls.Add(buttonPanel, 0, 2);
        dialog.Controls.Add(layout);
        dialog.AcceptButton = closeButton;
        dialog.CancelButton = closeButton;
        _hintLabel.Text = "操作速查已打开。";
        dialog.ShowDialog(this);
    }

    private string BuildQuickGuideText()
    {
        var students = GetStudents();
        var presentStudents = GetPresentStudentsForLesson();
        var absentStudents = GetAbsentStudentsForLesson();
        var history = GetVisibleHistoryLines();
        var sourceText = students.Count == 0
            ? "待准备"
            : string.IsNullOrWhiteSpace(_state.FileName) ? "未加载" : _state.FileName;
        var lastResult = GetCopyableResultText();
        var lines = new List<string>
        {
            $"当前名单：{sourceText}",
            $"人数：总 {students.Count} · 在场 {presentStudents.Count} · 缺席 {absentStudents.Count} · 历史 {history.Count} 条",
            string.IsNullOrWhiteSpace(lastResult) ? "最近结果：暂无" : $"最近结果：{lastResult}",
            string.Empty,
            "课堂开始",
            "上课向导    按当前名单给出开始建议",
            "试用名单    先体验抽号流程",
            "空名单点试用抽号会直接载入试用名单",
            "名单模板  保存或复制标准名单模板",
            "导入文件    导入 Excel/TXT/CSV 名单",
            "粘贴名单    直接粘贴或输入名单",
            "本节缺席    设置本节不参与抽号的学生",
            "新一节课    清空上节记录并保留名单",
            string.Empty,
            "抽号与展示",
            "开始抽号    从当前在场学生中抽取",
            "抽男生 / 抽女生 / 抽小组",
            "设置        调整抽号时长、避免重复、自动复制",
            "撤销上次    撤销最近一次抽号记录",
            "重抽一次    按上次范围再抽一次",
            "全屏        进入展示模式",
            "悬浮球      切换为小窗口操作",
            string.Empty,
            "名单与课堂记录",
            "名单概览    查看全班、在场、缺席、已抽和未抽",
            "复制名单    复制当前名单或筛选名单",
            "导出名单    导出当前名单或筛选名单",
            "重载文件    从原名单文件重新读取",
            string.Empty,
            "课后整理",
            "复制结果    复制最近一次抽号结果",
            "查看历史    查看、筛选、复制和导出历史",
            "导出历史    保存抽号历史",
            "课堂摘要    复制或导出本节摘要",
            "课堂包      一次性导出名单、历史和摘要",
            string.Empty,
            "收起与退出",
            "托盘右键  可继续抽号、查看名单、导出记录或退出"
        };

        return string.Join(Environment.NewLine, lines);
    }
    private async void CheckForUpdates()
    {
        try
        {
            UseWaitCursor = true;
            var release = await FetchLatestReleaseAsync();
            if (release is null)
            {
                MessageBox.Show(this, "暂时无法连接 GitHub 或更新镜像，请稍后重试。", "检查更新", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!IsNewerVersion(release.TagName, AppVersion))
            {
                MessageBox.Show(this, $"当前已经是最新版本：{AppVersion}。", "检查更新", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ShowUpdateDialog(release);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"检查更新失败：{ex.Message}", "检查更新", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private static async Task<GitHubReleaseInfo?> FetchLatestReleaseAsync()
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("DrawMachineDesktop-UpdateChecker/1.0");
        foreach (var source in UpdateApiSources)
        {
            try
            {
                using var response = await client.GetAsync(source);
                response.EnsureSuccessStatusCode();
                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var root = document.RootElement;
                var tagName = root.GetProperty("tag_name").GetString() ?? string.Empty;
                var releaseUrl = root.TryGetProperty("html_url", out var releaseUrlElement)
                    ? releaseUrlElement.GetString() ?? $"https://github.com/{UpdateRepository}/releases/latest"
                    : $"https://github.com/{UpdateRepository}/releases/latest";
                var assetUrl = FindInstallerAssetUrl(root) ?? releaseUrl;
                if (!string.IsNullOrWhiteSpace(tagName))
                {
                    return new GitHubReleaseInfo(tagName, releaseUrl, assetUrl);
                }
            }
            catch (HttpRequestException)
            {
            }
            catch (TaskCanceledException)
            {
            }
            catch (JsonException)
            {
            }
        }

        return null;
    }

    private static string? FindInstallerAssetUrl(JsonElement release)
    {
        if (!release.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var installer = assets.EnumerateArray()
            .FirstOrDefault(asset =>
                asset.TryGetProperty("name", out var name)
                && name.GetString()?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true
                && name.GetString()?.Contains("installer", StringComparison.OrdinalIgnoreCase) == true);
        if (installer.ValueKind == JsonValueKind.Object
            && installer.TryGetProperty("browser_download_url", out var installerUrl))
        {
            return installerUrl.GetString();
        }

        var executable = assets.EnumerateArray()
            .FirstOrDefault(asset =>
                asset.TryGetProperty("name", out var name)
                && name.GetString()?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true);
        return executable.ValueKind == JsonValueKind.Object
            && executable.TryGetProperty("browser_download_url", out var executableUrl)
            ? executableUrl.GetString()
            : null;
    }

    private static bool IsNewerVersion(string latestTag, string currentTag)
    {
        return Version.TryParse(latestTag.TrimStart('v', 'V'), out var latest)
            && Version.TryParse(currentTag.TrimStart('v', 'V'), out var current)
            && latest > current;
    }

    private void ShowUpdateDialog(GitHubReleaseInfo release)
    {
        using var dialog = new Form
        {
            Text = "发现新版本",
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false,
            ClientSize = new Size(560, 250),
            Font = SystemFonts.MessageBoxFont
        };
        var message = new Label
        {
            Text = $"发现新版本 {release.TagName}\n当前版本：{AppVersion}\n\n请选择下载源。下载后运行安装程序即可覆盖更新。",
            AutoSize = false,
            Location = new Point(24, 24),
            Size = new Size(500, 90)
        };
        var directButton = new Button { Text = "GitHub 直连", Location = new Point(24, 136), Size = new Size(118, 34) };
        var fastButton = new Button { Text = "ghfast 镜像", Location = new Point(152, 136), Size = new Size(118, 34) };
        var proxyButton = new Button { Text = "gh-proxy 镜像", Location = new Point(280, 136), Size = new Size(128, 34) };
        var closeButton = new Button { Text = "稍后处理", DialogResult = DialogResult.Cancel, Location = new Point(426, 136), Size = new Size(108, 34) };
        directButton.Click += (_, _) => OpenUpdateUrl(release.AssetUrl);
        fastButton.Click += (_, _) => OpenUpdateUrl($"https://ghfast.top/{release.AssetUrl}");
        proxyButton.Click += (_, _) => OpenUpdateUrl($"https://gh-proxy.com/{release.AssetUrl}");
        dialog.Controls.AddRange(new Control[] { message, directButton, fastButton, proxyButton, closeButton });
        dialog.CancelButton = closeButton;
        dialog.ShowDialog(this);
    }

    private static void OpenUpdateUrl(string url)
    {
        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
    }

    private sealed record GitHubReleaseInfo(string TagName, string ReleaseUrl, string AssetUrl);

    private void ShowAbout()
    {
        MessageBox.Show(
            this,
            $"h9647123 使用 OpenAI-Codex 通过 GPT5.5 开发\nVibe Coding\n版本号：{AppVersion}\n构建日期：{AppBuildDate}",
            "关于抽号机",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void ToggleAlwaysOnTop()
    {
        _state.AlwaysOnTop = !_state.AlwaysOnTop;
        _state.Save();
        TopMost = _isPresentationMode || _state.AlwaysOnTop;
        UpdateTopMostButton();
    }

    private void ToggleAvoidRepeat()
    {
        _state.AvoidRepeatDraw = !_state.AvoidRepeatDraw;
        _state.Save();
        UpdateAvoidRepeatButton();
        UpdateResetRoundButton();
        UpdateRosterActionState();
        _hintLabel.Text = _state.AvoidRepeatDraw
            ? "避免重复已开启：本轮抽完后会自动开始新一轮。"
            : string.Empty;
    }

    private void ToggleAutoCopyResult()
    {
        _state.AutoCopyResult = !_state.AutoCopyResult;
        _state.Save();
        UpdateAutoCopyButton();
        _hintLabel.Text = _state.AutoCopyResult
            ? "自动复制已开启：每次抽号结束后会把结果放入剪贴板。"
            : "自动复制已关闭，可继续手动点“复制结果”。";
    }

    private void ResetAvoidRepeatRound()
    {
        if (_isDrawing)
        {
            _hintLabel.Text = "抽号进行中，结束后再重置本轮。";
            return;
        }

        if (_state.DrawnStudentKeys.Count == 0 && _state.DrawnGroupKeys.Count == 0)
        {
            _hintLabel.Text = "本轮暂无已抽记录，无需重置。";
            UpdateResetRoundButton();
            return;
        }

        _state.DrawnStudentKeys.Clear();
        _state.DrawnGroupKeys.Clear();
        _state.Save();
        UpdateResetRoundButton();
        UpdateRosterActionState();
        _hintLabel.Text = "已重置本轮：避免重复记录已清空，名单和抽号历史不变。";
    }

    private void UndoLastDraw()
    {
        if (_isDrawing)
        {
            _hintLabel.Text = "抽号进行中，结束后再撤销。";
            return;
        }

        if (!CanUndoLastDraw())
        {
            _hintLabel.Text = "暂无可撤销的上次抽取。";
            UpdateUndoDrawButton();
            return;
        }

        RemoveLastDrawnKey();
        if (_state.DrawHistory.Count > 0
            && string.Equals(_state.DrawHistory[^1], _state.LastDrawUndoHistoryEntry, StringComparison.Ordinal))
        {
            _state.DrawHistory.RemoveAt(_state.DrawHistory.Count - 1);
        }

        _state.LastWinner = ResolveLastWinnerFromHistory();
        ClearUndoSnapshot();
        _state.Save();
        UpdateHistoryDisplay();
        UpdateResetRoundButton();
        UpdateUndoDrawButton();
        UpdateHistoryActionState();
        SetResultDisplay("已撤销上次抽取");
        _hintLabel.Text = "已撤销上次抽取：历史和避免重复记录已同步回退。";
    }

    private void RedrawLastDraw()
    {
        if (_isDrawing)
        {
            _hintLabel.Text = "抽号进行中，结束后再重抽。";
            return;
        }

        if (!CanUndoLastDraw())
        {
            _hintLabel.Text = "暂无可重抽的上次抽取。";
            UpdateUndoDrawButton();
            return;
        }

        var replayMode = GetLastDrawReplayMode();
        if (!CanStartReplayDraw(replayMode, out var emptyMessage))
        {
            _hintLabel.Text = emptyMessage;
            MessageBox.Show(this, emptyMessage, "无法重抽", MessageBoxButtons.OK, MessageBoxIcon.Information);
            UpdateUndoDrawButton();
            return;
        }

        UndoLastDraw();
        switch (replayMode)
        {
            case "group":
                DrawGroup();
                break;
            case "male":
                StartGenderDraw("男");
                break;
            case "female":
                StartGenderDraw("女");
                break;
            default:
                StartDraw();
                break;
        }
    }

    private bool CanUndoLastDraw()
    {
        return !_isDrawing
            && !string.IsNullOrWhiteSpace(_state.LastDrawUndoKind)
            && !string.IsNullOrWhiteSpace(_state.LastDrawUndoHistoryEntry)
            && _state.DrawHistory.Count > 0
            && string.Equals(_state.DrawHistory[^1], _state.LastDrawUndoHistoryEntry, StringComparison.Ordinal);
    }

    private bool CanRedrawLastDraw()
    {
        return CanUndoLastDraw()
            && CanStartReplayDraw(GetLastDrawReplayMode(), out _);
    }

    private string GetLastDrawReplayMode()
    {
        if (!string.IsNullOrWhiteSpace(_state.LastDrawReplayMode))
        {
            return _state.LastDrawReplayMode;
        }

        return string.Equals(_state.LastDrawUndoKind, "group", StringComparison.Ordinal)
            ? "group"
            : "student";
    }

    private bool CanStartReplayDraw(string replayMode, out string emptyMessage)
    {
        switch (replayMode)
        {
            case "group":
                if (GetActiveStudents().Any(student => !string.IsNullOrWhiteSpace(student.Group)))
                {
                    emptyMessage = string.Empty;
                    return true;
                }

                emptyMessage = "没有可重抽的小组，请检查名单小组字段或“本节缺席”设置。";
                return false;
            case "male":
                if (GetActiveStudents().Any(student => IsGender(student.Gender, "男")))
                {
                    emptyMessage = string.Empty;
                    return true;
                }

                emptyMessage = "没有可重抽的在场男生，请检查性别字段或“本节缺席”设置。";
                return false;
            case "female":
                if (GetActiveStudents().Any(student => IsGender(student.Gender, "女")))
                {
                    emptyMessage = string.Empty;
                    return true;
                }

                emptyMessage = "没有可重抽的在场女生，请检查性别字段或“本节缺席”设置。";
                return false;
            default:
                if (GetActiveStudents().Count > 0)
                {
                    emptyMessage = string.Empty;
                    return true;
                }

                emptyMessage = "没有可重抽的在场学生。可以先导入名单，或在“本节缺席”里恢复学生。";
                return false;
        }
    }

    private void RemoveLastDrawnKey()
    {
        if (string.Equals(_state.LastDrawUndoKind, "student", StringComparison.Ordinal))
        {
            _state.DrawnStudentKeys = _state.DrawnStudentKeys
                .Where(key => !string.Equals(key, _state.LastDrawUndoKey, StringComparison.Ordinal))
                .ToList();
            return;
        }

        if (string.Equals(_state.LastDrawUndoKind, "group", StringComparison.Ordinal))
        {
            _state.DrawnGroupKeys = _state.DrawnGroupKeys
                .Where(key => !string.Equals(key, _state.LastDrawUndoKey, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }

    private string ResolveLastWinnerFromHistory()
    {
        if (_state.DrawHistory.Count == 0)
        {
            return string.Empty;
        }

        var entry = _state.DrawHistory[^1];
        return entry.Length > 20 ? entry[20..].Trim() : entry;
    }

    private void RememberUndoSnapshot(string kind, string key, string historyEntry, string replayMode)
    {
        _state.LastDrawUndoKind = kind;
        _state.LastDrawUndoKey = key;
        _state.LastDrawUndoHistoryEntry = historyEntry;
        _state.LastDrawReplayMode = replayMode;
    }

    private void ClearUndoSnapshot()
    {
        _state.LastDrawUndoKind = string.Empty;
        _state.LastDrawUndoKey = string.Empty;
        _state.LastDrawUndoHistoryEntry = string.Empty;
        _state.LastDrawReplayMode = string.Empty;
    }

    private bool CanStartNewLesson()
    {
        return !_isDrawing
            && (_state.DrawHistory.Count > 0
                || !string.IsNullOrWhiteSpace(_state.LastWinner)
                || GetExcludedStudentCount() > 0
                || _state.DrawnStudentKeys.Count > 0
                || _state.DrawnGroupKeys.Count > 0
                || CanUndoLastDraw());
    }

    private bool CanUseNewLessonAction()
    {
        return !_isDrawing && GetStudents().Count > 0;
    }

    private void UpdateLessonActionState()
    {
        var canUseNewLessonAction = CanUseNewLessonAction();
        _newLessonButton.Enabled = canUseNewLessonAction;
        _floatingNewLessonMenuItem.Enabled = canUseNewLessonAction;
        _trayNewLessonMenuItem.Enabled = canUseNewLessonAction;
    }

    private void UpdateTopMostButton()
    {
        _topMostButton.Text = _state.AlwaysOnTop ? "始终置顶：开" : "始终置顶：关";
    }

    private void UpdatePresentationModeButton()
    {
        var text = _isPresentationMode ? "退出全屏" : "全屏";
        _presentationModeButton.Text = text;
        _floatingPresentationModeMenuItem.Text = _isPresentationMode ? "退出全屏展示" : "全屏展示";
        _trayPresentationModeMenuItem.Text = _isPresentationMode ? "退出全屏展示" : "全屏展示";
    }

    private void UpdateAvoidRepeatButton()
    {
        var text = _state.AvoidRepeatDraw ? "避免重复：开" : "避免重复：关";
        _avoidRepeatButton.Text = text;
        _floatingAvoidRepeatMenuItem.Text = text;
        _floatingAvoidRepeatMenuItem.Checked = _state.AvoidRepeatDraw;
        _trayAvoidRepeatMenuItem.Text = text;
        _trayAvoidRepeatMenuItem.Checked = _state.AvoidRepeatDraw;
    }

    private void UpdateAutoCopyButton()
    {
        var text = _state.AutoCopyResult ? "自动复制：开" : "自动复制：关";
        _autoCopyButton.Text = text;
        _floatingAutoCopyMenuItem.Text = _state.AutoCopyResult ? "自动复制结果：开" : "自动复制结果：关";
        _floatingAutoCopyMenuItem.Checked = _state.AutoCopyResult;
        _trayAutoCopyMenuItem.Text = _state.AutoCopyResult ? "自动复制结果：开" : "自动复制结果：关";
        _trayAutoCopyMenuItem.Checked = _state.AutoCopyResult;
    }

    private void UpdateDurationPresetMenuState()
    {
        var duration = ClampDrawDuration((decimal)_state.DrawDurationSeconds);
        var label = $"抽号时长：{duration:0.##}秒";
        _floatingDurationMenuItem.Text = label;
        _trayDurationMenuItem.Text = label;
        UpdateDurationPresetItem(duration, 3m, _floatingDuration3MenuItem, _trayDuration3MenuItem);
        UpdateDurationPresetItem(duration, 5m, _floatingDuration5MenuItem, _trayDuration5MenuItem);
        UpdateDurationPresetItem(duration, 8m, _floatingDuration8MenuItem, _trayDuration8MenuItem);
    }

    private static void UpdateDurationPresetItem(decimal currentDuration, decimal presetDuration, params ToolStripMenuItem[] menuItems)
    {
        var isSelected = Math.Abs(currentDuration - presetDuration) < 0.005m;
        foreach (var item in menuItems)
        {
            item.Checked = isSelected;
        }
    }

    private void UpdateResetRoundButton()
    {
        var hasRoundRecords = _state.DrawnStudentKeys.Count > 0 || _state.DrawnGroupKeys.Count > 0;
        _resetRoundButton.Enabled = hasRoundRecords && !_isDrawing;
        _floatingResetRoundMenuItem.Enabled = hasRoundRecords && !_isDrawing;
        _trayResetRoundMenuItem.Enabled = hasRoundRecords && !_isDrawing;
        UpdateRoundProgressDisplay();
        UpdateLessonActionState();
    }

    private void UpdateRoundProgressDisplay()
    {
        _roundProgressValue.Text = BuildRoundProgressText();
        UpdateClassroomStatusDisplay();
    }

    private void UpdateClassroomStatusDisplay()
    {
        _fileNameValue.Text = BuildClassroomStatusText();
    }

    private string BuildClassroomStatusText()
    {
        var students = GetStudents();
        var totalCount = students.Count;
        if (totalCount == 0)
        {
            return BuildEmptyClassroomStatusText();
        }

        var presentCount = GetActiveStudents().Count;
        var absentCount = GetExcludedStudentCount();
        var sourceText = string.IsNullOrWhiteSpace(_state.FileName)
            ? "名单：未加载"
            : $"名单：{TrimForStatusLine(_state.FileName)}";
        var stateText = presentCount == 0
                ? $"无人可抽 · 在场 0/{totalCount} · 缺席 {absentCount}"
                : _isDrawing
                    ? $"抽号中 · 在场 {presentCount}/{totalCount} · 缺席 {absentCount}"
                    : $"可抽 · 在场 {presentCount}/{totalCount} · 缺席 {absentCount}";
        var sourceStatus = BuildRosterSourceStatusShort();
        if (!string.IsNullOrWhiteSpace(sourceStatus))
        {
            stateText += $" · {sourceStatus}";
        }

        return $"{sourceText}{Environment.NewLine}{stateText}";
    }

    private static string BuildEmptyClassroomStatusText()
    {
        return $"名单：待准备{Environment.NewLine}粘贴名单 · 导入文件 · {DemoDrawActionText}";
    }

    private string BuildRoundProgressText()
    {
        var excludedCount = GetExcludedStudentCount();
        if (GetStudents().Count == 0)
        {
            return BuildEmptyRoundProgressText();
        }

        if (!_state.AvoidRepeatDraw)
        {
            return excludedCount > 0 ? $"未开启\n缺席 {excludedCount}" : "未开启";
        }

        var students = GetActiveStudents();
        if (students.Count == 0)
        {
            return $"无在场\n缺席 {excludedCount}";
        }

        var currentStudentKeys = students
            .Select(GetStudentKey)
            .ToHashSet(StringComparer.Ordinal);
        var drawnStudentCount = _state.DrawnStudentKeys
            .Where(currentStudentKeys.Contains)
            .Distinct(StringComparer.Ordinal)
            .Count();
        var remainingStudents = Math.Max(0, currentStudentKeys.Count - drawnStudentCount);

        var currentGroupKeys = students
            .Select(student => student.Group.Trim())
            .Where(group => !string.IsNullOrWhiteSpace(group))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (currentGroupKeys.Count == 0)
        {
            return $"学生剩 {remainingStudents}/{currentStudentKeys.Count}\n小组未设";
        }

        var drawnGroupCount = _state.DrawnGroupKeys
            .Where(currentGroupKeys.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var remainingGroups = Math.Max(0, currentGroupKeys.Count - drawnGroupCount);
        return $"学生剩 {remainingStudents}/{currentStudentKeys.Count}\n小组剩 {remainingGroups}/{currentGroupKeys.Count}";
    }

    private static string BuildEmptyRoundProgressText()
    {
        return $"先准备名单\n或点{DemoDrawActionText}";
    }

    private void UpdateUndoDrawButton()
    {
        var canUndo = CanUndoLastDraw();
        var canRedraw = CanRedrawLastDraw();
        _undoDrawButton.Enabled = canUndo;
        _redrawButton.Enabled = canRedraw;
        _floatingUndoDrawMenuItem.Enabled = canUndo;
        _floatingRedrawMenuItem.Enabled = canRedraw;
        _trayUndoDrawMenuItem.Enabled = canUndo;
        _trayRedrawMenuItem.Enabled = canRedraw;
        UpdateLessonActionState();
    }

    private void UpdateHistoryActionState()
    {
        var hasHistory = GetVisibleHistoryLines().Count > 0;
        var canCopyLessonSummary = CanCopyLessonSummary();
        var canExportLessonPackage = CanExportLessonPackage();
        var canOpenLessonPackageDirectory = CanOpenLastLessonPackageDirectory();
        _copyResultButton.Enabled = hasHistory;
        _copyLessonSummaryButton.Enabled = canCopyLessonSummary;
        _exportLessonSummaryButton.Enabled = canCopyLessonSummary;
        _viewHistoryButton.Enabled = hasHistory;
        _exportHistoryButton.Enabled = hasHistory;
        _exportLessonPackageButton.Enabled = canExportLessonPackage;
        _clearHistoryButton.Enabled = hasHistory;
        _floatingCopyResultMenuItem.Enabled = hasHistory;
        _floatingCopyLessonSummaryMenuItem.Enabled = canCopyLessonSummary;
        _floatingExportLessonSummaryMenuItem.Enabled = canCopyLessonSummary;
        _floatingExportLessonPackageMenuItem.Enabled = canExportLessonPackage;
        _floatingOpenLessonPackageDirectoryMenuItem.Enabled = canOpenLessonPackageDirectory;
        _floatingViewHistoryMenuItem.Enabled = hasHistory;
        _floatingExportHistoryMenuItem.Enabled = hasHistory;
        _floatingClearHistoryMenuItem.Enabled = hasHistory;
        _trayCopyResultMenuItem.Enabled = hasHistory;
        _trayCopyLessonSummaryMenuItem.Enabled = canCopyLessonSummary;
        _trayExportLessonSummaryMenuItem.Enabled = canCopyLessonSummary;
        _trayExportLessonPackageMenuItem.Enabled = canExportLessonPackage;
        _trayOpenLessonPackageDirectoryMenuItem.Enabled = canOpenLessonPackageDirectory;
        _trayViewHistoryMenuItem.Enabled = hasHistory;
        _trayExportHistoryMenuItem.Enabled = hasHistory;
        _trayClearHistoryMenuItem.Enabled = hasHistory;
        UpdateLessonActionState();
    }

    private string ResolveMainDrawButtonText()
    {
        return _isDrawing
            ? DrawingActionText
            : GetMainDrawButtonIdleText(GetStudents().Count);
    }

    private static string GetMainDrawButtonIdleText(int studentCount)
    {
        return studentCount == 0 ? DemoDrawActionText : CoreDrawActionText;
    }

    private void UpdateRosterActionState()
    {
        var students = GetStudents();
        var activeStudents = GetPresentStudentsForLesson();
        var hasStudents = students.Count > 0;
        var hasActiveStudents = activeStudents.Count > 0;
        var hasMale = activeStudents.Any(student => IsGender(student.Gender, "男"));
        var hasFemale = activeStudents.Any(student => IsGender(student.Gender, "女"));
        var hasGroups = activeStudents.Any(student => !string.IsNullOrWhiteSpace(student.Group));
        var canDrawRoster = hasActiveStudents && !_isDrawing;
        var canUseRoster = hasStudents && !_isDrawing;
        var canQuickDemoDraw = !hasStudents && !_isDrawing;
        var canStartDrawAction = canDrawRoster || canQuickDemoDraw;
        var canCopyLessonSummary = CanCopyLessonSummary();
        var canExportLessonPackage = CanExportLessonPackage();
        var canOpenLessonPackageDirectory = CanOpenLastLessonPackageDirectory();
        var canImport = !_isDrawing;
        var undrawnStudents = GetUndrawnStudentsForCurrentRound();
        var drawnStudents = GetDrawnStudentsForCurrentRound();
        var absentStudents = GetAbsentStudentsForLesson();
        var canUseUndrawnOverview = canUseRoster && _state.AvoidRepeatDraw;
        var canCopyUndrawnRoster = canUseUndrawnOverview && undrawnStudents.Count > 0;
        var canExportUndrawnRoster = canCopyUndrawnRoster;
        var canCopyDrawnRoster = canUseRoster && _state.AvoidRepeatDraw && drawnStudents.Count > 0;
        var canExportDrawnRoster = canCopyDrawnRoster;
        var canCopyAbsentRoster = canUseRoster && absentStudents.Count > 0;
        var canExportAbsentRoster = canCopyAbsentRoster;
        var canCopyPresentRoster = canUseRoster && activeStudents.Count > 0;
        var canExportPresentRoster = canCopyPresentRoster;
        var undrawnText = _state.AvoidRepeatDraw ? $"未抽名单 ({undrawnStudents.Count})" : "未抽名单";
        var copyUndrawnText = _state.AvoidRepeatDraw ? $"复制未抽名单 ({undrawnStudents.Count})" : "复制未抽名单";
        var exportUndrawnText = _state.AvoidRepeatDraw ? $"导出未抽名单 ({undrawnStudents.Count})" : "导出未抽名单";
        var copyDrawnText = _state.AvoidRepeatDraw ? $"复制已抽名单 ({drawnStudents.Count})" : "复制已抽名单";
        var exportDrawnText = _state.AvoidRepeatDraw ? $"导出已抽名单 ({drawnStudents.Count})" : "导出已抽名单";
        var copyAbsentText = absentStudents.Count > 0 ? $"复制缺席名单 ({absentStudents.Count})" : "复制缺席名单";
        var exportAbsentText = absentStudents.Count > 0 ? $"导出缺席名单 ({absentStudents.Count})" : "导出缺席名单";
        var copyPresentText = activeStudents.Count > 0 ? $"复制在场名单 ({activeStudents.Count})" : "复制在场名单";
        var exportPresentText = activeStudents.Count > 0 ? $"导出在场名单 ({activeStudents.Count})" : "导出在场名单";
        var canReloadFile = CanReloadCurrentRosterFile();
        var canOpenSourceFile = CanOpenCurrentRosterFile();
        var canExportRosterForEditing = CanExportRosterForEditing();
        var hasRecentRosterFiles = HasRecentRosterFiles();
        var canRestoreRoster = CanRestorePreviousRoster();

        _drawButton.Enabled = canStartDrawAction;
        _drawButton.Text = ResolveMainDrawButtonText();
        _drawMaleButton.Enabled = hasMale && !_isDrawing;
        _drawFemaleButton.Enabled = hasFemale && !_isDrawing;
        _drawGroupButton.Enabled = hasGroups && !_isDrawing;
        _listOverviewButton.Enabled = canUseRoster;
        _absenceButton.Enabled = canUseRoster;
        var absenceText = GetExcludedStudentCount() > 0 ? $"本节缺席：{GetExcludedStudentCount()}" : "本节缺席";
        _absenceButton.Text = absenceText;
        _absenceButton.Visible = hasStudents;
        _copyRosterButton.Enabled = canUseRoster;
        _exportRosterButton.Enabled = canUseRoster;
        _copyLessonSummaryButton.Enabled = canCopyLessonSummary;
        _exportLessonSummaryButton.Enabled = canCopyLessonSummary;
        _exportLessonPackageButton.Enabled = canExportLessonPackage;

        _quickDemoDrawButton.Enabled = canQuickDemoDraw;
        _quickDemoDrawButton.Visible = canQuickDemoDraw;
        _loadListButton.Enabled = canImport;
        var rosterSourceStatus = BuildRosterSourceStatusShort();
        _reloadFileButton.Enabled = canReloadFile;
        _reloadFileButton.Visible = canReloadFile;
        _reloadFileButton.Text = canReloadFile
            ? "重载文件"
            : string.IsNullOrWhiteSpace(rosterSourceStatus)
                ? "重载文件"
                : "无原文件";
        _reloadFileButton.AccessibleName = _reloadFileButton.Text;
        _reloadFileButton.AccessibleDescription = canReloadFile
            ? "从当前名单文件重新读取学生名单。"
            : string.IsNullOrWhiteSpace(rosterSourceStatus)
                ? "当前没有可重载的名单文件。"
                : "当前名单已保存在程序内，但没有可重载的原始文件。";
        _openSourceFileButton.Enabled = canOpenSourceFile || canExportRosterForEditing;
        _openSourceFileButton.Text = canOpenSourceFile
            ? "打开文件"
            : canExportRosterForEditing
                ? "导出编辑"
                : string.IsNullOrWhiteSpace(rosterSourceStatus)
                    ? "打开文件"
                    : "文件不可开";
        _openSourceFileButton.AccessibleName = _openSourceFileButton.Text;
        _openSourceFileButton.AccessibleDescription = canOpenSourceFile
            ? "打开当前名单文件。"
            : canExportRosterForEditing
                ? "把当前名单导出为可编辑文件，设为当前名单文件，并立即打开编辑。"
                : string.IsNullOrWhiteSpace(rosterSourceStatus)
                    ? "当前没有可打开的名单文件。"
                    : "当前名单仍可使用，但原始名单文件不可打开。";
        _recentRosterButton.Enabled = canImport && hasRecentRosterFiles;
        _pasteListButton.Enabled = canImport;
        _demoListButton.Enabled = canImport;
        _restoreRosterButton.Enabled = canRestoreRoster;

        _floatingDrawMenuItem.Enabled = canStartDrawAction;
        _floatingDrawMaleMenuItem.Enabled = hasMale && !_isDrawing;
        _floatingDrawFemaleMenuItem.Enabled = hasFemale && !_isDrawing;
        _floatingDrawGroupMenuItem.Enabled = hasGroups && !_isDrawing;
        _floatingCopyLessonSummaryMenuItem.Enabled = canCopyLessonSummary;
        _floatingExportLessonSummaryMenuItem.Enabled = canCopyLessonSummary;
        _floatingExportLessonPackageMenuItem.Enabled = canExportLessonPackage;
        _floatingOpenLessonPackageDirectoryMenuItem.Enabled = canOpenLessonPackageDirectory;
        _floatingListOverviewMenuItem.Enabled = canUseRoster;
        _floatingUndrawnOverviewMenuItem.Enabled = canUseUndrawnOverview;
        _floatingUndrawnOverviewMenuItem.Text = undrawnText;
        _floatingCopyUndrawnRosterMenuItem.Enabled = canCopyUndrawnRoster;
        _floatingCopyUndrawnRosterMenuItem.Text = copyUndrawnText;
        _floatingExportUndrawnRosterMenuItem.Enabled = canExportUndrawnRoster;
        _floatingExportUndrawnRosterMenuItem.Text = exportUndrawnText;
        _floatingCopyDrawnRosterMenuItem.Enabled = canCopyDrawnRoster;
        _floatingCopyDrawnRosterMenuItem.Text = copyDrawnText;
        _floatingExportDrawnRosterMenuItem.Enabled = canExportDrawnRoster;
        _floatingExportDrawnRosterMenuItem.Text = exportDrawnText;
        _floatingAbsenceMenuItem.Enabled = canUseRoster;
        _floatingAbsenceMenuItem.Text = absenceText;
        _floatingCopyAbsentRosterMenuItem.Enabled = canCopyAbsentRoster;
        _floatingCopyAbsentRosterMenuItem.Text = copyAbsentText;
        _floatingExportAbsentRosterMenuItem.Enabled = canExportAbsentRoster;
        _floatingExportAbsentRosterMenuItem.Text = exportAbsentText;
        _floatingCopyPresentRosterMenuItem.Enabled = canCopyPresentRoster;
        _floatingCopyPresentRosterMenuItem.Text = copyPresentText;
        _floatingExportPresentRosterMenuItem.Enabled = canExportPresentRoster;
        _floatingExportPresentRosterMenuItem.Text = exportPresentText;
        _floatingCopyRosterMenuItem.Enabled = canUseRoster;
        _floatingExportRosterMenuItem.Enabled = canUseRoster;
        _importMenuItem.Enabled = canImport;
        _floatingReloadFileMenuItem.Enabled = canReloadFile;
        _floatingOpenSourceFileMenuItem.Enabled = canOpenSourceFile;
        _floatingRecentRosterMenuItem.Enabled = canImport && hasRecentRosterFiles;
        _floatingPasteListMenuItem.Enabled = canImport;
        _floatingDemoListMenuItem.Enabled = canImport;
        _floatingRestoreRosterMenuItem.Enabled = canRestoreRoster;

        _trayDrawMenuItem.Enabled = canStartDrawAction;
        _trayDrawMaleMenuItem.Enabled = hasMale && !_isDrawing;
        _trayDrawFemaleMenuItem.Enabled = hasFemale && !_isDrawing;
        _trayDrawGroupMenuItem.Enabled = hasGroups && !_isDrawing;
        _trayCopyLessonSummaryMenuItem.Enabled = canCopyLessonSummary;
        _trayExportLessonSummaryMenuItem.Enabled = canCopyLessonSummary;
        _trayExportLessonPackageMenuItem.Enabled = canExportLessonPackage;
        _trayOpenLessonPackageDirectoryMenuItem.Enabled = canOpenLessonPackageDirectory;
        _trayListOverviewMenuItem.Enabled = canUseRoster;
        _trayUndrawnOverviewMenuItem.Enabled = canUseUndrawnOverview;
        _trayUndrawnOverviewMenuItem.Text = undrawnText;
        _trayCopyUndrawnRosterMenuItem.Enabled = canCopyUndrawnRoster;
        _trayCopyUndrawnRosterMenuItem.Text = copyUndrawnText;
        _trayExportUndrawnRosterMenuItem.Enabled = canExportUndrawnRoster;
        _trayExportUndrawnRosterMenuItem.Text = exportUndrawnText;
        _trayCopyDrawnRosterMenuItem.Enabled = canCopyDrawnRoster;
        _trayCopyDrawnRosterMenuItem.Text = copyDrawnText;
        _trayExportDrawnRosterMenuItem.Enabled = canExportDrawnRoster;
        _trayExportDrawnRosterMenuItem.Text = exportDrawnText;
        _trayAbsenceMenuItem.Enabled = canUseRoster;
        _trayAbsenceMenuItem.Text = absenceText;
        _trayCopyAbsentRosterMenuItem.Enabled = canCopyAbsentRoster;
        _trayCopyAbsentRosterMenuItem.Text = copyAbsentText;
        _trayExportAbsentRosterMenuItem.Enabled = canExportAbsentRoster;
        _trayExportAbsentRosterMenuItem.Text = exportAbsentText;
        _trayCopyPresentRosterMenuItem.Enabled = canCopyPresentRoster;
        _trayCopyPresentRosterMenuItem.Text = copyPresentText;
        _trayExportPresentRosterMenuItem.Enabled = canExportPresentRoster;
        _trayExportPresentRosterMenuItem.Text = exportPresentText;
        _trayCopyRosterMenuItem.Enabled = canUseRoster;
        _trayExportRosterMenuItem.Enabled = canUseRoster;
        _trayImportMenuItem.Enabled = canImport;
        _trayReloadFileMenuItem.Enabled = canReloadFile;
        _trayOpenSourceFileMenuItem.Enabled = canOpenSourceFile;
        _trayRecentRosterMenuItem.Enabled = canImport && hasRecentRosterFiles;
        _trayPasteListMenuItem.Enabled = canImport;
        _trayDemoListMenuItem.Enabled = canImport;
        _trayRestoreRosterMenuItem.Enabled = canRestoreRoster;

        _floatingImportButton.Enabled = (hasMale || hasFemale) && !_isDrawing;
        _floatingMaleButton.Enabled = hasMale && !_isDrawing;
        _floatingFemaleButton.Enabled = hasFemale && !_isDrawing;
        _floatingRestoreButton.Enabled = hasGroups && !_isDrawing;
        UpdateRecentRosterMenus();
    }

    private void ToggleStartup()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (key is null)
            {
                MessageBox.Show(this, "无法打开开机启动注册表位置。", "设置失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (IsStartupEnabled())
            {
                key.DeleteValue(StartupRegistryName, throwOnMissingValue: false);
            }
            else
            {
                key.SetValue(StartupRegistryName, GetStartupCommand());
            }

            UpdateStartupButton();
            UpdateSilentStartupButton();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"开机启动设置失败：{ex.Message}", "设置失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ToggleSilentStartup()
    {
        _state.SilentStartup = !_state.SilentStartup;
        _state.Save();

        if (_state.SilentStartup || IsStartupEnabled())
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
                key?.SetValue(StartupRegistryName, GetStartupCommand());
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"静默启动设置已保存，但更新开机启动命令失败：{ex.Message}", "设置提醒", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        UpdateStartupButton();
        UpdateSilentStartupButton();
    }

    private void UpdateStartupButton()
    {
        _startupButton.Text = IsStartupEnabled() ? "开机启动：开" : "开机启动：关";
    }

    private void UpdateSilentStartupButton()
    {
        _silentStartupButton.Text = _state.SilentStartup ? "静默启动：开" : "静默启动：关";
    }

    private static bool IsStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: false);
        var value = key?.GetValue(StartupRegistryName)?.ToString();
        return !string.IsNullOrWhiteSpace(value);
    }

    private string GetStartupCommand()
    {
        var command = $"\"{Application.ExecutablePath}\"";
        return _state.SilentStartup ? $"{command} --silent-startup" : command;
    }

    private void ToggleFloatingGenderMenu()
    {
        if (_isDrawing)
        {
            return;
        }

        SetFloatingGenderMenuVisible(!_floatingGenderMenuVisible);
    }

    private void SetFloatingGenderMenuVisible(bool visible, bool animate = true)
    {
        if (_floatingGenderMenuTargetVisible == visible && _floatingGenderMenuVisible == visible && !_floatingGenderAnimationActive)
        {
            return;
        }

        _floatingGenderMenuTargetVisible = visible;
        if (visible)
        {
            _floatingGenderMenuVisible = true;
        }

        _floatingGenderAnimationActive = animate;
        if (!animate)
        {
            _floatingGenderAnimationTimer.Stop();
            _floatingGenderProgress = visible ? 1f : 0f;
            _floatingGenderAnimationActive = false;
            _floatingGenderMenuVisible = visible;
        }
        else
        {
            _floatingGenderAnimationTimer.Start();
        }

        SetFloatingActionVisibility(ShouldShowFloatingActions());
        LayoutFloatingControls();
        UpdateFloatingRegion();
        _floatingHost.Invalidate();
    }

    private List<StudentRecord> BuildStudentDrawPool(IReadOnlyList<StudentRecord> students)
    {
        var pool = students.ToList();
        if (!_state.AvoidRepeatDraw || pool.Count == 0)
        {
            return pool;
        }

        PruneDrawnStudentKeys();
        var drawn = new HashSet<string>(_state.DrawnStudentKeys, StringComparer.Ordinal);
        var available = pool.Where(student => !drawn.Contains(GetStudentKey(student))).ToList();
        if (available.Count > 0)
        {
            return available;
        }

        var eligibleKeys = pool.Select(GetStudentKey).ToHashSet(StringComparer.Ordinal);
        _state.DrawnStudentKeys = _state.DrawnStudentKeys
            .Where(key => !eligibleKeys.Contains(key))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        _state.Save();
        UpdateResetRoundButton();
        _hintLabel.Text = "避免重复：本轮学生已抽完，已自动开始新一轮。";
        return pool;
    }

    private List<StudentGroup> BuildGroupDrawPool(IReadOnlyList<StudentGroup> groups)
    {
        var pool = groups.ToList();
        if (!_state.AvoidRepeatDraw || pool.Count == 0)
        {
            return pool;
        }

        PruneDrawnGroupKeys(pool);
        var drawn = new HashSet<string>(_state.DrawnGroupKeys, StringComparer.OrdinalIgnoreCase);
        var available = pool.Where(group => !drawn.Contains(GetGroupKey(group))).ToList();
        if (available.Count > 0)
        {
            return available;
        }

        var eligibleKeys = pool.Select(GetGroupKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _state.DrawnGroupKeys = _state.DrawnGroupKeys
            .Where(key => !eligibleKeys.Contains(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        _state.Save();
        UpdateResetRoundButton();
        _hintLabel.Text = "避免重复：本轮小组已抽完，已自动开始新一轮。";
        return pool;
    }

    private void MarkStudentDrawn(StudentRecord student)
    {
        if (!_state.AvoidRepeatDraw)
        {
            return;
        }

        var key = GetStudentKey(student);
        if (!_state.DrawnStudentKeys.Contains(key, StringComparer.Ordinal))
        {
            _state.DrawnStudentKeys.Add(key);
        }
    }

    private void MarkGroupDrawn(StudentGroup group)
    {
        if (!_state.AvoidRepeatDraw)
        {
            return;
        }

        var key = GetGroupKey(group);
        if (!_state.DrawnGroupKeys.Contains(key, StringComparer.OrdinalIgnoreCase))
        {
            _state.DrawnGroupKeys.Add(key);
        }
    }

    private void PruneDrawnStudentKeys()
    {
        var currentKeys = GetStudents().Select(GetStudentKey).ToHashSet(StringComparer.Ordinal);
        var cleaned = _state.DrawnStudentKeys
            .Where(currentKeys.Contains)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (cleaned.Count != _state.DrawnStudentKeys.Count)
        {
            _state.DrawnStudentKeys = cleaned;
            _state.Save();
            UpdateResetRoundButton();
        }
    }

    private bool PruneExcludedStudentKeys(bool saveChanges = true)
    {
        var currentKeys = GetStudents().Select(GetStudentKey).ToHashSet(StringComparer.Ordinal);
        var cleaned = _state.ExcludedStudentKeys
            .Where(currentKeys.Contains)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (cleaned.Count == _state.ExcludedStudentKeys.Count)
        {
            return false;
        }

        _state.ExcludedStudentKeys = cleaned;
        if (saveChanges)
        {
            _state.Save();
            UpdateRosterActionState();
            UpdateResetRoundButton();
        }

        return true;
    }

    private void PruneDrawnGroupKeys(IReadOnlyList<StudentGroup> currentGroups)
    {
        var currentKeys = currentGroups.Select(GetGroupKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var cleaned = _state.DrawnGroupKeys
            .Where(currentKeys.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (cleaned.Count != _state.DrawnGroupKeys.Count)
        {
            _state.DrawnGroupKeys = cleaned;
            _state.Save();
            UpdateResetRoundButton();
        }
    }

    private static string GetStudentKey(StudentRecord student)
    {
        return $"{student.Sequence.Trim()}\u001f{student.Name.Trim()}";
    }

    private static string GetGroupKey(StudentGroup group)
    {
        return group.Name.Trim();
    }

    private void StartDraw()
    {
        if (!_isDrawing && GetStudents().Count == 0)
        {
            ShowEmptyRosterDrawPrompt();
            return;
        }

        StartStudentDraw(GetActiveStudents(), "没有可抽取的在场学生。可以先导入名单，或在“本节缺席”里恢复学生。", "student");
    }

    private void StartGenderDraw(string gender)
    {
        var students = GetActiveStudents()
            .Where(student => string.Equals(student.Gender, gender, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var replayMode = string.Equals(gender, "男", StringComparison.OrdinalIgnoreCase) ? "male" : "female";
        StartStudentDraw(students, $"没有可抽取的在场{gender}生，请检查性别字段或“本节缺席”设置。", replayMode);
    }

    private void StartStudentDraw(IReadOnlyList<StudentRecord> students, string emptyMessage, string replayMode)
    {
        if (_isDrawing)
        {
            return;
        }

        if (students.Count == 0)
        {
            MessageBox.Show(this, emptyMessage, "无法抽号", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _drawMode = DrawMode.Student;
        _currentDrawReplayMode = replayMode;
        SetFloatingGenderMenuVisible(false, animate: false);
        _drawPool = BuildStudentDrawPool(students);
        _groupPool.Clear();
        _isDrawing = true;
        UpdateResetRoundButton();
        UpdateUndoDrawButton();
        UpdateRosterActionState();
        _totalSeconds = _state.DrawDurationSeconds;
        _remainingSeconds = _totalSeconds;
        _drawEndTime = DateTime.UtcNow.AddSeconds(_totalSeconds);

        _drawButton.Enabled = false;
        _drawButton.Text = DrawingActionText;
        UpdateFloatingDrawCountdown();
        SetResultDisplay(DrawingActionText, updateFont: false);
        PrepareRollingFonts();
        SetCountdownDisplay($"剩余时间: {_remainingSeconds:0.00}s", Color.FromArgb(18, 161, 80));

        _rollingTimer.Start();
        _countdownTimer.Start();
    }

    private void DrawGroup()
    {
        if (_isDrawing)
        {
            return;
        }

        var groups = GetActiveStudents()
            .Where(student => !string.IsNullOrWhiteSpace(student.Group))
            .GroupBy(student => student.Group)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (groups.Count == 0)
        {
            MessageBox.Show(this, "没有可抽取的小组，请检查名单中的小组字段或“本节缺席”设置。", "无法抽组", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _drawMode = DrawMode.Group;
        _currentDrawReplayMode = "group";
        SetFloatingGenderMenuVisible(false, animate: false);
        _drawPool.Clear();
        var allGroups = groups
            .Select(group => new StudentGroup(
                group.Key,
                group.OrderBy(student => ParseSequenceSortKey(student.Sequence)).ToList()))
            .ToList();
        _groupPool = BuildGroupDrawPool(allGroups);
        ReserveFloatingGroupStatusHeight();
        _isDrawing = true;
        UpdateResetRoundButton();
        UpdateUndoDrawButton();
        UpdateRosterActionState();
        _totalSeconds = _state.DrawDurationSeconds;
        _remainingSeconds = _totalSeconds;
        _drawEndTime = DateTime.UtcNow.AddSeconds(_totalSeconds);

        _drawButton.Enabled = false;
        _drawButton.Text = DrawingActionText;
        UpdateFloatingDrawCountdown();
        SetResultDisplay("抽小组中...", updateFont: false);
        PrepareRollingFonts();
        SetCountdownDisplay($"剩余时间: {_remainingSeconds:0.00}s", Color.FromArgb(18, 161, 80));

        _rollingTimer.Start();
        _countdownTimer.Start();
    }

    private void RollingTimer_Tick(object? sender, EventArgs e)
    {
        SetResultDisplay(
            _drawMode == DrawMode.Group ? FormatGroupResult(PickRandomGroup(), forFloating: false) : PickRandomStudentDisplay(),
            updateFont: false,
            animate: true);
    }

    private void CountdownTimer_Tick(object? sender, EventArgs e)
    {
        _remainingSeconds = Math.Max(0, (_drawEndTime - DateTime.UtcNow).TotalSeconds);

        var countdownText = $"剩余时间: {_remainingSeconds:0.00}s";
        var countdownColor = ResolveCountdownColor();
        SetCountdownDisplay(countdownText, countdownColor);

        if (_remainingSeconds > 0)
        {
            return;
        }

        _countdownTimer.Stop();
        _rollingTimer.Stop();

        if (_drawMode == DrawMode.Group)
        {
            var group = PickRandomGroup();
            SetResultDisplay(FormatGroupResult(group, forFloating: false));
            MarkGroupDrawn(group);
            _state.LastWinner = $"第 {group.Name} 组";
            var historyEntry = AddDrawHistory($"{_state.LastWinner} {string.Join("、", group.Members.Select(student => student.DisplayName))}");
            RememberUndoSnapshot("group", GetGroupKey(group), historyEntry, "group");
            TryAutoCopyResult(historyEntry);
        }
        else
        {
            var winner = PickRandomStudent();
            SetResultDisplay($"最终抽中: {winner.DisplayName}");

            MarkStudentDrawn(winner);
            _state.LastWinner = winner.DisplayName;
            var historyEntry = AddDrawHistory(winner.DisplayName);
            RememberUndoSnapshot("student", GetStudentKey(winner), historyEntry, _currentDrawReplayMode);
            TryAutoCopyResult(historyEntry);
        }

        _isDrawing = false;
        _drawButton.Enabled = true;
        _drawButton.Text = ResolveMainDrawButtonText();
        UpdateResetRoundButton();
        UpdateUndoDrawButton();
        UpdateRosterActionState();
        _state.Save();
        _floatingResultLabel.ForeColor = GetFloatingTextColor();
        _floatingResultLabel.Text = "抽号";
        LayoutFloatingResultLabel();
        UpdateFloatingCountdownIdleText();
    }

    private Color ResolveCountdownColor()
    {
        if (_remainingSeconds > _totalSeconds * (2d / 3d))
        {
            return Color.FromArgb(18, 161, 80);
        }

        if (_remainingSeconds > _totalSeconds * (1d / 3d))
        {
            return Color.FromArgb(217, 154, 0);
        }

        return Color.FromArgb(218, 60, 60);
    }

    private Color ResolveFloatingCountdownColor()
    {
        if (_totalSeconds <= 0)
        {
            return Color.FromArgb(218, 60, 60);
        }

        var elapsedRatio = 1d - Math.Max(0d, Math.Min(1d, _remainingSeconds / _totalSeconds));
        if (elapsedRatio < 0.5d)
        {
            return InterpolateColor(
                Color.FromArgb(18, 161, 80),
                Color.FromArgb(217, 154, 0),
                (float)(elapsedRatio / 0.5d));
        }

        return InterpolateColor(
            Color.FromArgb(217, 154, 0),
            Color.FromArgb(218, 60, 60),
            (float)((elapsedRatio - 0.5d) / 0.5d));
    }

    private StudentRecord PickRandomStudent()
    {
        var pool = _drawPool.Count > 0 ? _drawPool : GetActiveStudents();
        return pool[_random.Next(pool.Count)];
    }

    private string PickRandomStudentDisplay()
    {
        return PickRandomStudent().DisplayName;
    }

    private StudentGroup PickRandomGroup()
    {
        return _groupPool[_random.Next(_groupPool.Count)];
    }

    private string FormatGroupResult(StudentGroup group, bool forFloating)
    {
        var members = group.Members.Select(student => student.DisplayName).ToList();
        if (!forFloating)
        {
            return $"第 {group.Name} 组\n{string.Join("、", members)}";
        }

        return $"第 {group.Name} 组\n{PackFloatingMembers(members)}";
    }

    private string PackFloatingMembers(IReadOnlyList<string> members)
    {
        var lines = new List<string>();
        var maxWidth = Math.Max(100, _floatingCountdownLabel.Width - 8);
        var currentLine = string.Empty;

        foreach (var member in members)
        {
            var candidate = string.IsNullOrWhiteSpace(currentLine) ? member : $"{currentLine}、{member}";
            if (!string.IsNullOrWhiteSpace(currentLine) && TextRenderer.MeasureText(candidate, _floatingCountdownLabel.Font).Width > maxWidth)
            {
                lines.Add(currentLine);
                currentLine = member;
            }
            else
            {
                currentLine = candidate;
            }
        }

        if (!string.IsNullOrWhiteSpace(currentLine))
        {
            lines.Add(currentLine);
        }

        return string.Join("\n", lines);
    }

    private void AdjustFontSize(bool increase)
    {
        var nextSize = increase ? _state.ResultFontSize + 2 : _state.ResultFontSize - 2;
        var clamped = Math.Max(MinFontSize, Math.Min(MaxFontSize, nextSize));
        _state.ResultFontSize = clamped;
        _state.Save();

        _fontSizeValue.Text = $"{clamped:0}px";

        if (clamped >= MaxFontSize)
        {
            _hintLabel.Text = "已经放到最大了。";
        }
        else if (clamped <= MinFontSize)
        {
            _hintLabel.Text = "已经缩到最小了。";
        }
        else
        {
            _hintLabel.Text = string.Empty;
        }

        UpdateFontButtons();
        RefreshResultFonts();
    }

    private void UpdateFontButtons()
    {
        _increaseFontButton.Enabled = _state.ResultFontSize < MaxFontSize;
        _decreaseFontButton.Enabled = _state.ResultFontSize > MinFontSize;
    }

    private string AddDrawHistory(string result)
    {
        var entry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {result}";
        _state.DrawHistory.Add(entry);
        if (_state.DrawHistory.Count > 200)
        {
            _state.DrawHistory = _state.DrawHistory.Skip(_state.DrawHistory.Count - 200).ToList();
        }

        _state.Save();
        UpdateHistoryDisplay();
        UpdateHistoryActionState();
        return entry;
    }

    private void UpdateHistoryDisplay()
    {
        var lastWinner = string.IsNullOrWhiteSpace(_state.LastWinner)
            ? ResolveLastWinnerFromHistory()
            : _state.LastWinner;
        _lastWinnerValue.Text = string.IsNullOrWhiteSpace(lastWinner) ? "暂无记录" : lastWinner;
        _lastWinnerValue.SelectionStart = 0;
        UpdateClassroomStatusDisplay();
    }

    private List<string> GetVisibleHistoryLines()
    {
        if (_state.DrawHistory.Count > 0)
        {
            return _state.DrawHistory.ToList();
        }

        return string.IsNullOrWhiteSpace(_state.LastWinner)
            ? new List<string>()
            : new List<string> { $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {_state.LastWinner}" };
    }

    private void SetIdleResultText()
    {
        var count = GetStudents().Count;
        var activeCount = GetActiveStudents().Count;
        var idleText = count > 0
            ? activeCount == count
                ? $"已加载 {count} 名学生"
                : $"已加载 {count} 名学生\n本节可抽 {activeCount} 名"
            : EmptyRosterResultText;

        if (!_isDrawing)
        {
            SetResultDisplay(idleText);
            if (count == 0)
            {
                _hintLabel.Text = FirstUseHint;
            }
            else if (_hintLabel.Text.StartsWith("首次使用：", StringComparison.Ordinal))
            {
                _hintLabel.Text = string.Empty;
            }
        }
    }

    private void SetResultDisplay(string text, bool updateFont = true, bool animate = false)
    {
        _currentResultText = text;
        SetSmoothLabelText(_resultLabel, text, animate && _isDrawing);
        SetFloatingStatusText(FormatFloatingStatus(text), animate && _isDrawing);
        if (updateFont)
        {
            RefreshResultFonts();
        }
    }

    private static void SetSmoothLabelText(SmoothLabel label, string text, bool animate)
    {
        if (animate)
        {
            label.SetTextAnimated(text);
            return;
        }

        label.Text = text;
    }

    private void SetCountdownDisplay(string text, Color color)
    {
        var displayText = FormatMainCountdownText(text);
        ApplyMainCountdownTypography(displayText);
        _countdownValue.Text = displayText;
        _countdownValue.ForeColor = color;

        if (_isDrawing)
        {
            UpdateFloatingDrawCountdown();
        }
        else
        {
            UpdateFloatingCountdownIdleText();
        }
    }

    private void ApplyMainCountdownTypography(string text)
    {
        var targetSize = ResolveMainCountdownFontSize(text);
        if (Math.Abs(_countdownValue.Font.SizeInPoints - targetSize) > 0.05f)
        {
            _countdownValue.Font = CreateMainCountdownFont(targetSize);
        }
    }

    private float ResolveMainCountdownFontSize(string text)
    {
        const float preferredSize = 13f;
        const float minimumSize = 8.5f;
        if (!text.Contains(Environment.NewLine, StringComparison.Ordinal))
        {
            return preferredSize;
        }

        var availableWidth = Math.Max(1, _countdownValue.ClientSize.Width);
        var availableHeight = Math.Max(1, _countdownValue.ClientSize.Height);
        if (availableWidth <= 1 || availableHeight <= 1)
        {
            return 11f;
        }

        var proposedSize = new Size(availableWidth, availableHeight);
        const TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak;
        for (var size = preferredSize; size >= minimumSize; size -= 0.25f)
        {
            using var font = CreateMainCountdownFont(size);
            var measured = TextRenderer.MeasureText(text, font, proposedSize, flags);
            if (measured.Width <= availableWidth && measured.Height <= availableHeight)
            {
                return size;
            }
        }

        return minimumSize;
    }

    private static Font CreateMainCountdownFont(float sizeInPoints)
    {
        return new Font("Microsoft YaHei UI", sizeInPoints, FontStyle.Bold);
    }

    private static string FormatMainCountdownText(string text)
    {
        const string label = "剩余时间";
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        if (normalized.StartsWith(label + "\n", StringComparison.Ordinal))
        {
            var value = normalized[(label.Length + 1)..].Trim();
            return string.IsNullOrWhiteSpace(value)
                ? label
                : $"{label}{Environment.NewLine}{value}";
        }

        foreach (var prefix in new[] { label + ":", label + "：" })
        {
            if (!normalized.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var value = normalized[prefix.Length..].Trim();
            return string.IsNullOrWhiteSpace(value)
                ? label
                : $"{label}{Environment.NewLine}{value}";
        }

        return text;
    }

    private static int GetMainCountdownLineCount(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        return text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Length;
    }

    private void UpdateFloatingDrawCountdown()
    {
        _floatingResultLabel.Text = $"{_remainingSeconds:0.00}";
        _floatingResultLabel.ForeColor = ResolveFloatingCountdownColor();
        _floatingResultLabel.Invalidate();
        _floatingDrawSurface.Invalidate();
    }

    private void UpdateFloatingCountdownIdleText()
    {
        if (_isDrawing)
        {
            return;
        }

        SetFloatingStatusText(FormatFloatingStatus(_currentResultText));
        _floatingCountdownLabel.ForeColor = Color.FromArgb(70, 78, 92);
    }

    private void SetFloatingStatusText(string text, bool animate = false)
    {
        var floatingText = text;
        if (_drawMode == DrawMode.Group && _groupPool.Count > 0 && text.StartsWith("第 ", StringComparison.Ordinal))
        {
            var group = _groupPool.FirstOrDefault(group => text.StartsWith($"第 {group.Name} 组", StringComparison.Ordinal));
            if (group is not null)
            {
                floatingText = FormatGroupResult(group, forFloating: true);
            }
        }

        SetSmoothLabelText(_floatingCountdownLabel, floatingText, animate);
        UpdateFloatingStatusHeight(floatingText);
    }

    private void UpdateFloatingStatusHeight(string text)
    {
        var target = CalculateFloatingStatusHeight(text);
        if (_isDrawing && _drawMode == DrawMode.Group)
        {
            if (_floatingStatusHeight + 1 >= target)
            {
                return;
            }

            _floatingStatusAnimationTimer.Stop();
            _floatingStatusHeight = target;
            _floatingTargetStatusHeight = target;
            ApplyFloatingWindowHeight();
            LayoutFloatingControls();
            UpdateFloatingRegion();
            _floatingHost.Invalidate();
            return;
        }

        if (Math.Abs(_floatingTargetStatusHeight - target) < 1)
        {
            return;
        }

        _floatingTargetStatusHeight = target;
        _floatingStatusAnimationTimer.Start();
    }

    private int CalculateFloatingStatusHeight(string text)
    {
        var lineCount = Math.Max(1, text.Split('\n').Length);
        return Math.Min(FloatingStatusMaxHeight, Math.Max(FloatingStatusMinHeight, 8 + lineCount * 17));
    }

    private void ReserveFloatingGroupStatusHeight()
    {
        if (!_isFloatingMode || _groupPool.Count == 0)
        {
            return;
        }

        var target = _groupPool
            .Select(group => CalculateFloatingStatusHeight(FormatGroupResult(group, forFloating: true)))
            .DefaultIfEmpty(FloatingStatusMinHeight)
            .Max();
        _floatingStatusAnimationTimer.Stop();
        _floatingStatusHeight = target;
        _floatingTargetStatusHeight = target;
        ApplyFloatingWindowHeight();
        LayoutFloatingControls();
        UpdateFloatingRegion();
        _floatingHost.Invalidate();
    }

    private string FormatFloatingStatus(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text == EmptyRosterResultText)
        {
            return "模板 / 试用 / 粘贴";
        }

        if (text.StartsWith("最终抽中:", StringComparison.Ordinal))
        {
            return text.Replace("最终抽中:", "抽中:");
        }

        if (text.StartsWith("已加载 ", StringComparison.Ordinal))
        {
            return $"单击抽号 · {_state.DrawDurationSeconds:0.00} 秒";
        }

        return text;
    }

    private void RefreshResultFonts()
    {
        ApplyFittedFont(_resultLabel, _resultPanel.ClientSize, GetResultPreferredFontSize());
        ApplyFittedFont(_floatingResultLabel, _floatingDrawSurface.ClientSize, _isDrawing ? 8f : 9f, _isDrawing ? "00.00" : "抽号", 6.2f, 6);
    }

    private void PrepareRollingFonts()
    {
        var students = GetStudents();
        var sampleText = students.Count == 0
            ? DrawingActionText
            : students.Select(student => student.DisplayName).OrderByDescending(name => name.Length).First();

        ApplyFittedFont(_resultLabel, _resultPanel.ClientSize, GetResultPreferredFontSize(), sampleText);
        ApplyFittedFont(_floatingResultLabel, _floatingDrawSurface.ClientSize, 8f, "00.00", 6.2f, 6);
    }

    private float GetResultPreferredFontSize()
    {
        return _isPresentationMode
            ? Math.Max(_state.ResultFontSize, PresentationPreferredFontSize)
            : _state.ResultFontSize;
    }

    private void ApplyFittedFont(Label target, Size bounds, float preferredSize, string? sampleText = null, float minimumSize = MinAutoFitFontSize, int inset = 32)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var text = string.IsNullOrWhiteSpace(sampleText ?? target.Text) ? " " : sampleText ?? target.Text;
        var available = new Size(Math.Max(1, bounds.Width - inset), Math.Max(1, bounds.Height - inset));
        var size = preferredSize;
        var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak;

        while (size > minimumSize)
        {
            using var testFont = new Font("Microsoft YaHei UI", size, FontStyle.Bold, GraphicsUnit.Point);
            var measured = TextRenderer.MeasureText(text, testFont, available, flags);
            if (measured.Width <= available.Width + 4 && measured.Height <= available.Height)
            {
                break;
            }

            size -= 1f;
        }

        target.Font = new Font("Microsoft YaHei UI", Math.Max(size, minimumSize), FontStyle.Bold, GraphicsUnit.Point);
    }

    private void EnterFloatingMode()
    {
        if (_isFloatingMode)
        {
            return;
        }

        if (_isPresentationMode)
        {
            ExitPresentationMode();
        }

        var hideDuringSwitch = Visible && Opacity > 0.01;
        if (hideDuringSwitch)
        {
            Opacity = 0;
        }

        _isFloatingMode = true;
        _normalBounds = Bounds;
        _normalFormBorderStyle = FormBorderStyle;
        _normalTopMost = TopMost;
        _floatingGenderAnimationTimer.Stop();
        _floatingReserveGenderSpace = true;
        _floatingGenderMenuVisible = false;
        _floatingGenderMenuTargetVisible = false;
        _floatingGenderAnimationActive = false;
        _floatingGenderProgress = 0f;

        var workingArea = Screen.FromControl(this).WorkingArea;
        var initialCenterX = _normalBounds.Left + _normalBounds.Width / 2;
        _floatingExpandLeft = initialCenterX >= workingArea.Left + workingArea.Width / 2;
        var collapsedLogoX = Math.Max(workingArea.Left, Math.Min(workingArea.Right - GetFloatingCollapsedWidth(), _normalBounds.Left));
        var targetX = _floatingExpandLeft
            ? collapsedLogoX - (GetFloatingDockWidth() - GetFloatingLogoSize() - ScaleFloating(7))
            : collapsedLogoX;
        targetX = Math.Max(workingArea.Left, Math.Min(workingArea.Right - GetFloatingDockWidth(), targetX));
        var targetY = Math.Max(workingArea.Top, Math.Min(workingArea.Bottom - GetFloatingWindowHeight(), _normalBounds.Top - GetFloatingTopOffset()));

        SuspendLayout();
        _normalHost.Visible = false;
        _floatingHost.Visible = true;
        FormBorderStyle = FormBorderStyle.None;
        TopMost = _state.AlwaysOnTop;
        ApplyFloatingPalette();
        ApplyFloatingFonts();
        BackColor = FloatingTransparentKey;
        TransparencyKey = FloatingTransparentKey;
        _floatingIsExpanded = false;
        _floatingToolbarProgress = 0f;
        _floatingToolbarTargetProgress = 0f;
        MinimumSize = new Size(GetFloatingDockWidth(), GetFloatingWindowHeight());
        MaximumSize = new Size(GetFloatingDockWidth(), GetFloatingWindowHeight());
        Bounds = new Rectangle(targetX, targetY, GetFloatingDockWidth(), GetFloatingWindowHeight());
        _hintLabel.Text = string.Empty;
        LayoutFloatingControls();
        UpdateFloatingRegion();
        UpdateFloatingCountdownIdleText();
        RefreshResultFonts();
        ExpandFloatingDock();
        ResumeLayout(false);
        PerformLayout();
        Opacity = 1;
        Refresh();
    }

    private void TogglePresentationMode()
    {
        if (_isPresentationMode)
        {
            ExitPresentationMode();
        }
        else
        {
            EnterPresentationMode();
        }
    }

    private void EnterPresentationMode()
    {
        if (_isPresentationMode)
        {
            return;
        }

        if (_isFloatingMode)
        {
            ExitFloatingMode();
        }

        var hideDuringSwitch = Visible && Opacity > 0.01;
        if (hideDuringSwitch)
        {
            Opacity = 0;
        }

        _isPresentationMode = true;
        _presentationBounds = Bounds;
        _presentationFormBorderStyle = FormBorderStyle;
        _presentationWindowState = WindowState;
        _presentationMinimumSize = MinimumSize;
        _presentationMaximumSize = MaximumSize;

        SuspendLayout();
        WindowState = FormWindowState.Normal;
        FormBorderStyle = FormBorderStyle.None;
        MinimumSize = Size.Empty;
        MaximumSize = Size.Empty;
        TopMost = true;
        Bounds = Screen.FromControl(this).Bounds;
        ResumeLayout(false);
        PerformLayout();
        PrepareNormalSurfaceForReveal();
        Opacity = 1;
        ShowInTaskbar = true;
        Activate();
        RefreshResultFonts();
        UpdatePresentationModeButton();
        _hintLabel.Text = "已进入全屏展示。";
    }

    private void ExitPresentationMode()
    {
        if (!_isPresentationMode)
        {
            return;
        }

        var hideDuringSwitch = Visible && Opacity > 0.01;
        if (hideDuringSwitch)
        {
            Opacity = 0;
        }

        _isPresentationMode = false;
        SuspendLayout();
        WindowState = FormWindowState.Normal;
        FormBorderStyle = _presentationFormBorderStyle;
        MinimumSize = _presentationMinimumSize == Size.Empty ? new Size(460, 360) : _presentationMinimumSize;
        MaximumSize = _presentationMaximumSize;
        TopMost = _state.AlwaysOnTop;
        Bounds = _presentationBounds;
        if (_presentationWindowState == FormWindowState.Maximized)
        {
            WindowState = FormWindowState.Maximized;
        }

        ResumeLayout(false);
        PerformLayout();
        PrepareNormalSurfaceForReveal();
        Opacity = 1;
        Refresh();
        RefreshResultFonts();
        UpdatePresentationModeButton();
        _hintLabel.Text = "已退出全屏展示。";
    }

    private void ExitFloatingMode()
    {
        if (!_isFloatingMode)
        {
            return;
        }

        var hideDuringSwitch = Visible && Opacity > 0.01;
        if (hideDuringSwitch)
        {
            Opacity = 0;
        }

        _isFloatingMode = false;
        _floatingDragActive = false;
        _floatingAnimationTimer.Stop();
        _floatingStatusAnimationTimer.Stop();
        _floatingGenderAnimationTimer.Stop();
        _floatingAnimationActive = false;
        _floatingDirectionSwitchPending = false;
        _floatingGenderAnimationActive = false;
        _floatingReserveGenderSpace = false;
        _floatingGenderMenuVisible = false;
        _floatingGenderMenuTargetVisible = false;
        _floatingGenderProgress = 0f;
        _floatingToolbarProgress = 1f;
        _floatingToolbarTargetProgress = 1f;
        _floatingResultLabel.Text = "抽号";
        LayoutFloatingResultLabel();
        SuspendLayout();
        Region = null;
        TransparencyKey = Color.Empty;
        BackColor = SystemColors.Control;
        _floatingHost.Visible = false;
        _normalHost.Visible = true;
        FormBorderStyle = _normalFormBorderStyle;
        TopMost = _state.AlwaysOnTop;
        MinimumSize = new Size(460, 360);
        MaximumSize = Size.Empty;
        Bounds = _normalBounds;
        ResumeLayout(false);
        PerformLayout();
        PrepareNormalSurfaceForReveal();
        Opacity = 1;
        Refresh();
    }

    private decimal ClampDrawDuration(decimal duration)
    {
        if (duration < _drawDurationInput.Minimum)
        {
            return _drawDurationInput.Minimum;
        }

        if (duration > _drawDurationInput.Maximum)
        {
            return _drawDurationInput.Maximum;
        }

        return duration;
    }

    private static int ParseSequenceSortKey(string sequence)
    {
        return int.TryParse(sequence, out var value) ? value : int.MaxValue;
    }

    private static int ClampFloatingShade(int value)
    {
        return Math.Max(0, Math.Min(100, value));
    }

    private Color GetFloatingToolbarColor()
    {
        return InterpolateColor(Color.FromArgb(242, 244, 247), Color.FromArgb(150, 156, 168), ClampFloatingShade(_state.FloatingShade) / 100f);
    }

    private Color GetFloatingButtonColor()
    {
        return InterpolateColor(Color.FromArgb(224, 228, 235), Color.FromArgb(119, 127, 143), ClampFloatingShade(_state.FloatingShade) / 100f);
    }

    private Color GetFloatingButtonHighlightColor()
    {
        return InterpolateColor(Color.FromArgb(238, 241, 245), Color.FromArgb(139, 148, 164), ClampFloatingShade(_state.FloatingShade) / 100f);
    }

    private Color GetFloatingButtonHoverColor()
    {
        return InterpolateColor(Color.FromArgb(212, 217, 226), Color.FromArgb(105, 113, 128), ClampFloatingShade(_state.FloatingShade) / 100f);
    }

    private Color GetFloatingButtonPressedColor()
    {
        return InterpolateColor(Color.FromArgb(198, 204, 214), Color.FromArgb(88, 96, 110), ClampFloatingShade(_state.FloatingShade) / 100f);
    }

    private Color GetFloatingBorderColor()
    {
        return InterpolateColor(Color.FromArgb(156, 162, 174), Color.FromArgb(74, 82, 96), ClampFloatingShade(_state.FloatingShade) / 100f);
    }

    private Color GetFloatingTextColor()
    {
        return _state.FloatingShade >= 58
            ? Color.FromArgb(245, 247, 250)
            : Color.FromArgb(45, 55, 68);
    }

    private void ApplyFloatingPalette()
    {
        var toolbarColor = GetFloatingToolbarColor();
        var buttonColor = GetFloatingButtonColor();
        var textColor = GetFloatingTextColor();

        BackColor = _isFloatingMode ? FloatingTransparentKey : SystemColors.Control;
        _floatingTitleLabel.BackColor = toolbarColor;
        _floatingTitleLabel.ForeColor = textColor;
        _floatingTitleLabel.BorderColor = GetFloatingBorderColor();
        _floatingTitleLabel.HoverBackColor = GetFloatingButtonHoverColor();
        _floatingTitleLabel.PressedBackColor = GetFloatingButtonPressedColor();
        _floatingTitleLabel.UnderlayColor = FloatingTransparentKey;
        _floatingTitleLabel.UseTransparentUnderlay = false;
        _floatingResultLabel.ForeColor = _isDrawing ? ResolveFloatingCountdownColor() : textColor;
        _floatingCountdownLabel.ForeColor = textColor;

        foreach (var button in new[] { _floatingImportButton, _floatingMaleButton, _floatingFemaleButton, _floatingRestoreButton, _floatingExitButton })
        {
            button.BackColor = buttonColor;
            button.ForeColor = textColor;
            button.UnderlayColor = toolbarColor;
            button.BorderColor = GetFloatingBorderColor();
            button.HoverBackColor = GetFloatingButtonHoverColor();
            button.PressedBackColor = GetFloatingButtonPressedColor();
            button.Invalidate();
        }

        _floatingHost.Invalidate();
        _floatingDrawSurface.Invalidate();
    }

    private static Color InterpolateColor(Color light, Color dark, float amount)
    {
        amount = Math.Max(0f, Math.Min(1f, amount));
        return Color.FromArgb(
            (int)(light.R + (dark.R - light.R) * amount),
            (int)(light.G + (dark.G - light.G) * amount),
            (int)(light.B + (dark.B - light.B) * amount));
    }
}

internal class BufferedPanel : Panel
{
    public BufferedPanel()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.SupportsTransparentBackColor |
            ControlStyles.UserPaint,
            true);
        BackColor = Color.Transparent;
    }
}

internal class SmoothLabel : Label
{
    private readonly System.Windows.Forms.Timer _textTransitionTimer = new();
    private DateTime _textTransitionStart;
    private string _previousText = string.Empty;
    private float _textTransitionProgress = 1f;
    private bool _settingAnimatedText;

    public bool EnableTextTransition { get; set; }
    public int TextTransitionDurationMs { get; set; } = 120;

    public SmoothLabel()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint,
            true);
        _textTransitionTimer.Interval = 15;
        _textTransitionTimer.Tick += (_, _) => AdvanceTextTransition();
    }

    public void SetTextAnimated(string text)
    {
        if (!EnableTextTransition || Text == text)
        {
            Text = text;
            return;
        }

        _previousText = Text;
        _settingAnimatedText = true;
        Text = text;
        _settingAnimatedText = false;
        _textTransitionProgress = string.IsNullOrWhiteSpace(_previousText) ? 1f : 0f;
        _textTransitionStart = DateTime.UtcNow;
        if (_textTransitionProgress >= 1f)
        {
            _textTransitionTimer.Stop();
        }
        else
        {
            _textTransitionTimer.Start();
        }

        Invalidate();
    }

    protected override void OnTextChanged(EventArgs e)
    {
        if (!_settingAnimatedText && (!EnableTextTransition || !_textTransitionTimer.Enabled))
        {
            _previousText = string.Empty;
            _textTransitionProgress = 1f;
        }

        base.OnTextChanged(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (!EnableTextTransition || _textTransitionProgress >= 1f || string.IsNullOrEmpty(_previousText))
        {
            base.OnPaint(e);
            return;
        }

        e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var eased = EaseOutCubic(_textTransitionProgress);
        DrawTextLayer(e.Graphics, _previousText, (int)Math.Round(255 * (1f - eased)), -2f * eased);
        DrawTextLayer(e.Graphics, Text, (int)Math.Round(255 * eased), 2f * (1f - eased));
    }

    private void AdvanceTextTransition()
    {
        var duration = Math.Max(1, TextTransitionDurationMs);
        _textTransitionProgress = Math.Min(1f, (float)(DateTime.UtcNow - _textTransitionStart).TotalMilliseconds / duration);
        if (_textTransitionProgress >= 1f)
        {
            _textTransitionTimer.Stop();
            _previousText = string.Empty;
        }

        Invalidate();
    }

    private void DrawTextLayer(Graphics graphics, string text, int alpha, float yOffset)
    {
        if (alpha <= 0 || string.IsNullOrEmpty(text))
        {
            return;
        }

        var bounds = new RectangleF(ClientRectangle.X, ClientRectangle.Y + yOffset, ClientRectangle.Width, ClientRectangle.Height);
        using var format = CreateStringFormat(TextAlign);
        using var brush = CreateGradientTextBrush(bounds, alpha);
        graphics.DrawString(text, Font, brush, bounds, format);
    }

    private Brush CreateGradientTextBrush(RectangleF bounds, int alpha)
    {
        var start = Color.FromArgb(alpha, ForeColor);
        var endBase = BlendColor(ForeColor, Color.FromArgb(13, 99, 201), 0.22f);
        var end = Color.FromArgb(alpha, endBase);
        return new LinearGradientBrush(bounds, start, end, 90f);
    }

    private static StringFormat CreateStringFormat(ContentAlignment alignment)
    {
        var format = new StringFormat
        {
            Trimming = StringTrimming.EllipsisCharacter
        };

        format.Alignment = alignment is ContentAlignment.TopCenter or ContentAlignment.MiddleCenter or ContentAlignment.BottomCenter
            ? StringAlignment.Center
            : alignment is ContentAlignment.TopRight or ContentAlignment.MiddleRight or ContentAlignment.BottomRight
                ? StringAlignment.Far
                : StringAlignment.Near;

        format.LineAlignment = alignment is ContentAlignment.MiddleLeft or ContentAlignment.MiddleCenter or ContentAlignment.MiddleRight
            ? StringAlignment.Center
            : alignment is ContentAlignment.BottomLeft or ContentAlignment.BottomCenter or ContentAlignment.BottomRight
                ? StringAlignment.Far
                : StringAlignment.Near;

        return format;
    }

    private static float EaseOutCubic(float value)
    {
        value = Math.Max(0f, Math.Min(1f, value));
        var inverse = 1f - value;
        return 1f - inverse * inverse * inverse;
    }

    private static Color BlendColor(Color left, Color right, float amount)
    {
        amount = Math.Max(0f, Math.Min(1f, amount));
        return Color.FromArgb(
            (int)(left.R + (right.R - left.R) * amount),
            (int)(left.G + (right.G - left.G) * amount),
            (int)(left.B + (right.B - left.B) * amount));
    }
}

internal class RoundedButton : Button
{
    private bool _isHovering;
    private bool _isPressed;
    private int _radius = 14;

    public int Radius
    {
        get => _radius;
        set
        {
            _radius = Math.Max(1, value);
            Invalidate();
        }
    }
    public Color HoverBackColor { get; set; } = Color.Empty;
    public Color PressedBackColor { get; set; } = Color.Empty;

    public RoundedButton()
    {
        DoubleBuffered = true;
        FlatStyle = FlatStyle.Flat;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.SupportsTransparentBackColor |
            ControlStyles.UserPaint,
            true);
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        using var brush = new SolidBrush(ResolveOpaqueBackColor(Parent));
        pevent.Graphics.FillRectangle(brush, ClientRectangle);
    }

    private static Color ResolveOpaqueBackColor(Control? control)
    {
        while (control is not null)
        {
            if (control.BackColor != Color.Transparent)
            {
                return control.BackColor;
            }

            control = control.Parent;
        }

        return Color.FromArgb(245, 250, 255);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _isHovering = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _isHovering = false;
        _isPressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        _isPressed = true;
        Invalidate();
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _isPressed = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        using (var backgroundBrush = new SolidBrush(ResolveOpaqueBackColor(Parent)))
        {
            e.Graphics.FillRectangle(backgroundBrush, ClientRectangle);
        }

        var fill = _isPressed && PressedBackColor != Color.Empty
            ? PressedBackColor
            : _isHovering && HoverBackColor != Color.Empty
                ? HoverBackColor
                : BackColor;
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = MainForm.CreateRoundedRectangleForButton(rect, Radius);
        using var brush = new SolidBrush(fill);
        e.Graphics.FillPath(brush, path);

        var textRect = _isPressed ? new Rectangle(1, 1, Width, Height) : ClientRectangle;
        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            textRect,
            ForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }
}

internal enum DrawMode
{
    Student,
    Group
}

internal sealed class StudentGroup
{
    public StudentGroup(string name, List<StudentRecord> members)
    {
        Name = name;
        Members = members;
    }

    public string Name { get; }
    public List<StudentRecord> Members { get; }
}

internal sealed record RosterImportParserProbe(
    string Name,
    string[] Lines,
    string[] ExpectedNames,
    string[] ExpectedGenders,
    string[] ExpectedGroups);

internal sealed record AppStateSnapshot(
    List<string> Names,
    List<StudentRecord> Students,
    string FileName,
    string SourceFilePath,
    List<string> RecentRosterFiles,
    List<StudentRecord> PreviousStudents,
    string PreviousFileName,
    string PreviousSourceFilePath,
    string LastWinner,
    List<string> DrawHistory,
    float ResultFontSize,
    double DrawDurationSeconds,
    int FloatingShade,
    bool AlwaysOnTop,
    bool SilentStartup,
    bool StartGuideShown,
    bool AvoidRepeatDraw,
    bool AutoCopyResult,
    List<string> ExcludedStudentKeys,
    List<string> DrawnStudentKeys,
    List<string> DrawnGroupKeys,
    string LastDrawUndoKind,
    string LastDrawUndoKey,
    string LastDrawUndoHistoryEntry,
    string LastDrawReplayMode,
    string LastLessonPackageDirectory,
    string LastExportDirectory);

internal sealed class StartupSelfCheckResult
{
    public bool Ok { get; set; }
    public string Version { get; set; } = string.Empty;
    public string BuildDate { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public int StudentCount { get; set; }
    public int HistoryCount { get; set; }
    public int CardWidth { get; set; }
    public int CardHeight { get; set; }
    public int CountdownWidth { get; set; }
    public int CountdownHeight { get; set; }
    public string CountdownSample { get; set; } = string.Empty;
    public int CountdownLineCount { get; set; }
    public bool StartupSurfaceReady { get; set; }
    public bool StartupCriticalControlsReady { get; set; }
    public int StartupWarmupPasses { get; set; }
    public int StartupRevealMaxWaitMs { get; set; }
    public bool HeadlessTopMost { get; set; }
    public string MainMoreMenuTopLevelTexts { get; set; } = string.Empty;
    public string RosterMenuDirectTexts { get; set; } = string.Empty;
    public string PrepareRosterMenuDirectTexts { get; set; } = string.Empty;
    public string RosterFilesMenuDirectTexts { get; set; } = string.Empty;
    public string DisplayMenuDirectTexts { get; set; } = string.Empty;
    public string FloatingDisplayMenuDirectTexts { get; set; } = string.Empty;
    public string ResultFontMenuDirectTexts { get; set; } = string.Empty;
    public string WindowStartupMenuDirectTexts { get; set; } = string.Empty;
    public string MainMoreDrawMenuDirectTexts { get; set; } = string.Empty;
    public string RangeDrawMenuDirectTexts { get; set; } = string.Empty;
    public string RoundActionsMenuDirectTexts { get; set; } = string.Empty;
    public string DrawSettingsMenuDirectTexts { get; set; } = string.Empty;
    public string RecordsMenuDirectTexts { get; set; } = string.Empty;
    public string RecordsLessonFilesMenuDirectTexts { get; set; } = string.Empty;
    public string RecordsHistoryManagementMenuDirectTexts { get; set; } = string.Empty;
    public string FloatingContextMenuTopLevelTexts { get; set; } = string.Empty;
    public string FloatingContextDrawMenuDirectTexts { get; set; } = string.Empty;
    public string FloatingContextRosterMenuDirectTexts { get; set; } = string.Empty;
    public string FloatingContextRecordsMenuDirectTexts { get; set; } = string.Empty;
    public string NewLessonMenuPath { get; set; } = string.Empty;
    public string EmptyMainActionButton { get; set; } = string.Empty;
    public string ReadyMainActionButton { get; set; } = string.Empty;
    public string EmptyRosterText { get; set; } = string.Empty;
    public string EmptyRosterStatusText { get; set; } = string.Empty;
    public string EmptyRoundProgressText { get; set; } = string.Empty;
    public string FirstUseHint { get; set; } = string.Empty;
    public string HeaderSetupButtonTexts { get; set; } = string.Empty;
    public string EmptyHeaderVisibleButtonTexts { get; set; } = string.Empty;
    public string ReadyHeaderVisibleButtonTexts { get; set; } = string.Empty;
    public string PasteRosterButton { get; set; } = string.Empty;
    public string ImportFileButton { get; set; } = string.Empty;
    public string ImportSuccessDefaultAction { get; set; } = string.Empty;
    public string ImportSuccessWarningDefaultAction { get; set; } = string.Empty;
    public string ImportSuccessDismissAction { get; set; } = string.Empty;
    public string StartGuideText { get; set; } = string.Empty;
    public string MainActionButton { get; set; } = string.Empty;
    public string RosterButton { get; set; } = string.Empty;
    public string MoreButton { get; set; } = string.Empty;
    public bool ShortcutKeysRemoved { get; set; }
    public bool SettingsDialogSelfCheckOk { get; set; }
    public string SettingsDialogTabTexts { get; set; } = string.Empty;
    public string SettingsDialogDrawControlTexts { get; set; } = string.Empty;
    public bool MainFooterDecluttered { get; set; }
    public bool MainDurationControlsHidden { get; set; }
    public bool CorruptStateBackupRecoveryOk { get; set; }
    public bool MissingStateBackupRecoveryOk { get; set; }
    public int StateRecoveryStudentCount { get; set; }
    public int StateRecoveryHistoryCount { get; set; }
    public string StateRecoveryNoticeSample { get; set; } = string.Empty;
    public bool DamagedStatePreserved { get; set; }
    public bool RestoredStateFileReadable { get; set; }
    public bool StateSaveSelfCheckOk { get; set; }
    public bool StateSavePrimaryReadable { get; set; }
    public bool StateSaveBackupReadable { get; set; }
    public bool StateSaveTempFilesCleaned { get; set; }
    public string StateSaveBackupLastWinner { get; set; } = string.Empty;
    public bool FirstUseImportWorkflowSelfCheckOk { get; set; }
    public int FirstUseImportStudentCount { get; set; }
    public string FirstUseImportReadyHeaderTexts { get; set; } = string.Empty;
    public string FirstUseImportMainActionText { get; set; } = string.Empty;
    public string FirstUseImportResultText { get; set; } = string.Empty;
    public string FirstUseImportClassroomStatusText { get; set; } = string.Empty;
    public bool FirstUseImportSavedStateReadable { get; set; }
    public bool CoreDrawWorkflowSelfCheckOk { get; set; }
    public bool CoreDrawWorkflowStarted { get; set; }
    public bool CoreDrawWorkflowCompleted { get; set; }
    public string CoreDrawWorkflowWinner { get; set; } = string.Empty;
    public int CoreDrawWorkflowHistoryCount { get; set; }
    public string CoreDrawWorkflowButtonText { get; set; } = string.Empty;
    public bool CoreDrawWorkflowButtonEnabled { get; set; }
    public int CoreDrawWorkflowDrawnStudentKeyCount { get; set; }
    public string CoreDrawWorkflowResultText { get; set; } = string.Empty;
    public bool RosterImportParserSelfCheckOk { get; set; }
    public int RosterImportParserCaseCount { get; set; }
    public int RosterImportParserStudentCount { get; set; }
    public string RosterImportParserSamples { get; set; } = string.Empty;
    public bool AvoidRepeatRolloverSelfCheckOk { get; set; }
    public int AvoidRepeatRolloverStudentPoolCount { get; set; }
    public int AvoidRepeatRolloverGroupPoolCount { get; set; }
    public string AvoidRepeatRolloverHint { get; set; } = string.Empty;
}

internal sealed class FloatingMenuColorTable : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground => Color.FromArgb(242, 244, 247);
    public override Color MenuItemSelected => Color.FromArgb(212, 217, 226);
    public override Color MenuItemBorder => Color.FromArgb(156, 162, 174);
    public override Color ImageMarginGradientBegin => ToolStripDropDownBackground;
    public override Color ImageMarginGradientMiddle => ToolStripDropDownBackground;
    public override Color ImageMarginGradientEnd => ToolStripDropDownBackground;
}

internal class FloatingIconButton : Control
{
    private bool _isHovering;
    private bool _isPressed;
    public Color BorderColor { get; set; } = Color.FromArgb(156, 162, 174);
    public Color HoverBackColor { get; set; } = Color.FromArgb(184, 190, 202);
    public Color PressedBackColor { get; set; } = Color.FromArgb(169, 176, 190);
    public Color UnderlayColor { get; set; } = Color.Transparent;
    public bool UseTransparentUnderlay { get; set; }
    public ContentAlignment TextAlign { get; set; } = ContentAlignment.MiddleCenter;

    public FloatingIconButton()
    {
        DoubleBuffered = true;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.SupportsTransparentBackColor |
            ControlStyles.UserPaint,
            true);
        BackColor = Color.Transparent;
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _isHovering = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _isHovering = false;
        _isPressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        _isPressed = true;
        Invalidate();
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _isPressed = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        if (UseTransparentUnderlay)
        {
            return;
        }

        using var brush = new SolidBrush(UnderlayColor);
        pevent.Graphics.FillRectangle(brush, ClientRectangle);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
        e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
        e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var fillColor = _isPressed
            ? PressedBackColor
            : _isHovering
                ? HoverBackColor
                : BackColor;

        var inset = _isPressed ? 3.6f : 2.1f;
        var rect = new RectangleF(inset, inset, Width - inset * 2 - 1.2f, Height - inset * 2 - 1.2f);
        using var brush = new SolidBrush(fillColor);
        e.Graphics.FillEllipse(brush, rect);
        using var pen = new Pen(BorderColor, 1.2f);
        e.Graphics.DrawEllipse(pen, rect);

        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            _isPressed ? new Rectangle(1, 1, Width, Height) : ClientRectangle,
            ForeColor,
            ResolveTextFlags(TextAlign) | TextFormatFlags.NoPadding);
    }

    private static TextFormatFlags ResolveTextFlags(ContentAlignment alignment)
    {
        var flags = TextFormatFlags.VerticalCenter;
        if (alignment is ContentAlignment.MiddleCenter or ContentAlignment.TopCenter or ContentAlignment.BottomCenter)
        {
            flags |= TextFormatFlags.HorizontalCenter;
        }
        else if (alignment is ContentAlignment.MiddleRight or ContentAlignment.TopRight or ContentAlignment.BottomRight)
        {
            flags |= TextFormatFlags.Right;
        }
        else
        {
            flags |= TextFormatFlags.Left;
        }

        return flags;
    }
}
