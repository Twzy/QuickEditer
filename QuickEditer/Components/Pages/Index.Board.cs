using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using QuickEditer.Components.Share;
using QuickEditer.Core;
using QuickEditer.Models;
using QuickEditer.Services;

namespace QuickEditer.Components.Pages
{
    public partial class Index
    {

        [Inject]
        AuthService Auth { get; set; } = default!;

        private void OpenSettings()
        {
            _dialogHost?.PrepareSettings(new AppSettings
            {
                DefaultFqbn = string.IsNullOrEmpty(_currentConfig?.BoardFqbn) ? "" : Config.Cfg.DefaultFqbn,
                EditorFontSize = Config.Cfg.EditorFontSize,
                AppTheme = Config.Cfg.AppTheme,
                EditorMinimap = Config.Cfg.EditorMinimap,
                EditorWordWrap = Config.Cfg.EditorWordWrap,
                EditorStickyScroll = Config.Cfg.EditorStickyScroll,
                HttpPort = Config.Cfg.HttpPort,
            });
            _showSettingsDialog = true;
        }

        private async Task SaveSettings(AppSettings settings)
        {
            Config.Cfg.DefaultFqbn = settings.DefaultFqbn;
            Config.Cfg.EditorFontSize = settings.EditorFontSize;
            Config.Cfg.AppTheme = settings.AppTheme;
            Config.Cfg.EditorMinimap = settings.EditorMinimap;
            Config.Cfg.EditorWordWrap = settings.EditorWordWrap;
            Config.Cfg.EditorStickyScroll = settings.EditorStickyScroll;
            Config.Cfg.HttpPort = settings.HttpPort;
            Config.ConfigAll();

            await JS.InvokeVoidAsync("quickEditer.setTheme", settings.AppTheme);

            if (_editorPanel != null)
                await _editorPanel.RefreshThemeAsync();

            _showSettingsDialog = false;
            StateHasChanged();
        }

        private void OpenProjectSettings() => _showProjectSettings = true;

        private async Task SaveProjectSettings(ProjectConfig config)
        {
            if (_currentConfig != null && !string.Equals(_currentConfig.Name, config.Name, StringComparison.OrdinalIgnoreCase))
            {
                var (ok, err) = ProjectSvc.RenameProject(_currentConfig.Name, config.Name);
                if (!ok)
                {
                    await Dialog.AlertAsync( err);
                    return;
                }

                _currentConfig = ProjectSvc.LoadProject(config.Name);
                _projects = ProjectSvc.ListProjects();
                _projectTree = ProjectTreeSvc.BuildProjectTree(config.Name);

                _tabs.Clear();
                _activeTabPath = null;
                _currentContent = "";

                if (_currentConfig != null)
                {
                    var sketch = Path.Combine(ProjectSvc.GetProjectDir(_currentConfig.Name), _currentConfig.MainSketchFile);
                    _selectedPath = sketch;
                    OpenTab(sketch);
                }
            }

            ProjectSvc.SaveConfig(config);
            _currentConfig = config;
            _showProjectSettings = false;
            AppendLog(OutputLogLevel.Info, $"项目设置已保存：{config.Name}");
        }

        private void OnGlobalSearchResult(GlobalSearchDialog.SearchResult result)
        {
            if (!File.Exists(result.FilePath)) return;
            OpenTab(result.FilePath);
            _showGlobalSearch = false;
        }

        private async Task ExportProject()
        {
            if (_currentConfig == null) return;
            await JS.InvokeVoidAsync("open", $"/api/project/export?name={Uri.EscapeDataString(_currentConfig.Name)}", "_blank");
        }

        private void OnBoardSelected(string fqbn)
        {
            if (_currentConfig != null)
            {
                _currentConfig.BoardFqbn = fqbn;
                _currentConfig.BoardConfigs.Clear();
                ProjectSvc.SaveConfig(_currentConfig);
            }
            else
            {
                Config.Cfg.DefaultFqbn = fqbn;
                Config.ConfigAll();
            }
            _showBoardDialog = false;
        }

        private void OpenBoardConfig()
        {
            var fqbn = _currentConfig?.BoardFqbn ?? Config.Cfg.DefaultFqbn;
            if (string.IsNullOrEmpty(fqbn)) return;

            _boardConfigValues = GetBoardConfigValues(fqbn);
            _showBoardConfigDialog = true;
        }

        private Dictionary<string, string> GetBoardConfigValues(string fqbn)
        {
            if (_currentConfig != null && _currentConfig.BoardConfigs.Count > 0)
                return _currentConfig.BoardConfigs.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);

            var entry = Config.Cfg.BoardConfigs.FirstOrDefault(b => b.Fqbn == fqbn);
            if (entry != null)
                return entry.Items.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);

            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        private string GetCurrentBoardLabel()
        {
            var fqbn = _currentConfig?.BoardFqbn ?? Config.Cfg.DefaultFqbn;
            return string.IsNullOrEmpty(fqbn) ? "" : fqbn;
        }

        private void SaveBoardConfig(Dictionary<string, string> values)
        {
            var fqbn = _currentConfig?.BoardFqbn ?? Config.Cfg.DefaultFqbn;
            if (string.IsNullOrEmpty(fqbn)) return;

            if (_currentConfig != null)
            {
                _currentConfig.BoardConfigs = values
                    .Select(kv => new BoardConfigPair { Key = kv.Key, Value = kv.Value })
                    .ToList();
                ProjectSvc.SaveConfig(_currentConfig);
            }
            else
            {
                var entry = Config.Cfg.BoardConfigs.FirstOrDefault(b => b.Fqbn == fqbn);
                if (entry == null)
                {
                    entry = new BoardConfigEntry { Fqbn = fqbn };
                    Config.Cfg.BoardConfigs.Add(entry);
                }
                entry.Items = values
                    .Select(kv => new BoardConfigValue { Key = kv.Key, Value = kv.Value })
                    .ToList();
                Config.ConfigAll();
            }

            _showBoardConfigDialog = false;
        }

        private void OpenAdditionalUrlsDialog()
        {
            _dialogHost?.ResetAdditionalUrlsDialog();
            _showAdditionalUrlsDialog = true;
        }

        private void SaveAdditionalUrls(string urls)
        {
            Config.Cfg.AdditionalUrls = urls;
            Config.ConfigAll();
            ArduinoCliSvc.EnsureEnvironment();
            _showAdditionalUrlsDialog = false;
        }
    }
}