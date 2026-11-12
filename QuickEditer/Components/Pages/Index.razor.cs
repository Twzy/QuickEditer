using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using QuickEditer.Components.Share;
using QuickEditer.Core;
using QuickEditer.Models;
using QuickEditer.Services;

namespace QuickEditer.Components.Pages
{
    public partial class Index : IDisposable
    {
        [Inject] public ProjectService ProjectSvc { get; set; } = default!;
        [Inject] public FileTreeService FileSvc { get; set; } = default!;
        [Inject] public ProjectTreeService ProjectTreeSvc { get; set; } = default!;
        [Inject] public ArduinoCliService ArduinoCliSvc { get; set; } = default!;
        [Inject] public IJSRuntime JS { get; set; } = default!;
        [Inject] public DialogService Dialog { get; set; } = default!;

        [Inject] public CookieAuthStateProvider AuthProvider { get; set; } = default!;
        [Inject] public NavigationManager Nav {  get; set; } = default!;


        private List<string> _projects = new();
        private ProjectConfig? _currentConfig;
        private ProjectTreeNode? _projectTree;
        private string? _selectedPath;
        private bool _showAbout;
        private bool _showDonate;
        private async Task OpenHelp()
        {
            await JS.InvokeVoidAsync("open", "/help", "_blank");
        }

        private List<EditorTabInfo> _tabs = new();
        private string? _activeTabPath;
        private string _currentContent = "";
        private string? _nonTextFilePath;
        private string? _draggingTabPath;
        private int _cursorLine;
        private int _cursorColumn;

        private List<OutputLogEntry> _logs = new();

        private List<string> _bottomTabs = new();
        private string _bottomActiveTab = "输出";
        private bool _bottomCollapsed;

        private DialogHost? _dialogHost;
        private EditorPanel? _editorPanel;

        private ProjectTreeNode? _targetDir;
        private ProjectTreeNode? _renameTarget;

        private bool _showNewDialog, _showOpenDialog, _showBoardDialog, _showLibDialog, _showSettingsDialog;
        private bool _showCreateFolderDialog, _showCreateFileDialog, _showRenameDialog;
        private bool _showBoardConfigDialog, _showAdditionalUrlsDialog, _showProjectSettings, _showGlobalSearch, _showShortcuts;
        private bool _showExamples;
        private string _exampleInstallDir = "";
        private List<string> _exampleSources = new();

        private bool _showConfirmCloseTab;
        private string _confirmCloseTabFileName = "";
        private string? _confirmCloseTabPath;
        private TaskCompletionSource<string>? _confirmCloseTabTcs;

        private bool _showChangePassword;
        private Dictionary<string, string> _boardConfigValues = new();

        private DotNetObjectReference<Index>? _selfRef;
        private bool _needRebindResizable;

        private CancellationTokenSource? _compileCts;


        private bool _showFlash;
        private List<BuildFile> _lastArtifacts = new();

        protected override async Task OnInitializedAsync()
        {

            var state = await AuthProvider.GetAuthenticationStateAsync();
            if (state.User.Identity?.IsAuthenticated != true)
            {
                Nav.NavigateTo("/login", forceLoad: true);
                return;
            }


            Config.ReadConfig();

            _bottomTabs.Clear();
            if (Config.Cfg.OutputPanelVisible) _bottomTabs.Add("输出");
            if (Config.Cfg.SerialPanelVisible) _bottomTabs.Add("串口监视器");

            if (_bottomTabs.Count > 0)
                _bottomActiveTab = _bottomTabs[0];
            else
                _bottomActiveTab = "";



            _projects = ProjectSvc.ListProjects();

            var target = Config.Cfg.LastProject;
            if (string.IsNullOrEmpty(target) || !_projects.Contains(target))
                target = _projects.FirstOrDefault();

            if (!string.IsNullOrEmpty(target))
            {
                _currentConfig = ProjectSvc.LoadProject(target);
                if (_currentConfig != null)
                {
                    EnsureProjectDirs(_currentConfig.Name);
                    MigrateLegacySrcLayout(_currentConfig);
                    _projectTree = ProjectTreeSvc.BuildProjectTree(_currentConfig.Name);
                    var sketch = Path.Combine(ProjectSvc.GetProjectDir(_currentConfig.Name), _currentConfig.MainSketchFile);
                    _selectedPath = sketch;
                    OpenTab(sketch);
                }
            }

            _ = RefreshExampleSourcesAsync();
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                var state = await AuthProvider.GetAuthenticationStateAsync();
                if (state.User.Identity?.IsAuthenticated != true)
                {
                    Nav.NavigateTo("/login", forceLoad: true);
                    return;
                }

                _selfRef ??= DotNetObjectReference.Create(this);
            }

            if (_needRebindResizable || firstRender)
            {
                _needRebindResizable = false;
                try
                {
                    await JS.InvokeVoidAsync("quickEditer.initResizable", _selfRef);
                }
                catch { }
            }
        }

        [JSInvokable]
        public void OnLayoutChanged(int? sidebarWidth, int? outputHeight)
        {
        }

        private void OnCursorPositionChanged((int Line, int Column) pos)
        {
            _cursorLine = pos.Line;
            _cursorColumn = pos.Column;
        }

        private string GetActiveLanguage()
        {
            if (string.IsNullOrEmpty(_activeTabPath)) return "";
            return FileTypeHelper.GetLanguage(_activeTabPath);
        }

        private void OnBottomCollapsedChanged(bool collapsed) => _bottomCollapsed = collapsed;

        private void OnBottomActiveTabChanged(string tab) => _bottomActiveTab = tab;

        private void OnBottomCloseTab(string tab)
        {
            _bottomTabs.Remove(tab);

            if (_bottomTabs.Count == 0)
            {
                _bottomActiveTab = "";
                SaveBottomPanelState();
                _needRebindResizable = true;
                return;
            }

            if (_bottomActiveTab == tab)
                _bottomActiveTab = _bottomTabs[0];

            SaveBottomPanelState();
            _needRebindResizable = true;
        }

        private void EnsureBottomTab(string tab)
        {
            if (!_bottomTabs.Contains(tab))
                _bottomTabs.Add(tab);
            _bottomActiveTab = tab;
            _bottomCollapsed = false;
            SaveBottomPanelState();
            _needRebindResizable = true;
        }

        private void SaveBottomPanelState()
        {
            Config.Cfg.OutputPanelVisible = _bottomTabs.Contains("输出");
            Config.Cfg.SerialPanelVisible = _bottomTabs.Contains("串口监视器");
            Config.ConfigAll();
        }

        private void ToggleOutput()
        {
            if (_bottomTabs.Contains("输出"))
            {
                if (_bottomActiveTab == "输出" && !_bottomCollapsed)
                {
                    OnBottomCloseTab("输出");
                }
                else
                {
                    _bottomActiveTab = "输出";
                    _bottomCollapsed = false;
                    _needRebindResizable = true;
                }
            }
            else
            {
                EnsureBottomTab("输出");
            }
        }

        private void ToggleSerial()
        {
            if (_bottomTabs.Contains("串口监视器"))
            {
                if (_bottomActiveTab == "串口监视器" && !_bottomCollapsed)
                {
                    OnBottomCloseTab("串口监视器");
                }
                else
                {
                    _bottomActiveTab = "串口监视器";
                    _bottomCollapsed = false;
                    _needRebindResizable = true;
                }
            }
            else
            {
                EnsureBottomTab("串口监视器");
            }
        }

        private void HideOutput()
        {
            OnBottomCloseTab("输出");
        }

        private async Task LogoutAsync()
        {
            await JS.InvokeVoidAsync("quickEditer.clearAuthCookie");
            Nav.NavigateTo("/login", forceLoad: true);
        }

        private async Task OnChangePasswordSaved()
        {
            _showChangePassword = false;
            AppendLog(OutputLogLevel.Success, "密码已修改");
            await Task.CompletedTask;
        }
        public void Dispose()
        {
            _selfRef?.Dispose();
            _selfRef = null;
        }
    }
}