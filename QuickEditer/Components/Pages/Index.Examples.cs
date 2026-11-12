using QuickEditer.Core;
using QuickEditer.Models;
using Microsoft.JSInterop;

namespace QuickEditer.Components.Pages
{
    public partial class Index
    {
        private async Task RefreshExampleSourcesAsync()
        {
            var sources = new List<string>();

            try
            {
                var coresResult = await ArduinoCliSvc.ListCoresAsync();
                var cores = ArduinoCliSvc.ParseCores(coresResult)
                    .Where(c => !string.IsNullOrWhiteSpace(c.Installed));

                foreach (var core in cores)
                    sources.Add($"核心：{core.Id}");
            }
            catch { }

            try
            {
                var installed = await ArduinoCliSvc.ListInstalledLibrariesAsync();
                var libs = ArduinoCliSvc.ParseInstalledLibraries(installed);
                foreach (var lib in libs)
                    sources.Add($"库：{lib.Name}");
            }
            catch { }

            _exampleSources = sources;
            await InvokeAsync(StateHasChanged);
        }

        private async Task OnOpenExampleSource(string source)
        {
            if (source.StartsWith("核心："))
            {
                var fqbn = source.Substring(3).Trim();
                _exampleInstallDir = ArduinoCliSvc.GetCoreInstallDir(fqbn);
            }
            else if (source.StartsWith("库："))
            {
                var libName = source.Substring(2).Trim();
                _exampleInstallDir = ArduinoCliSvc.GetLibraryInstallDir(libName);
            }
            else
            {
                return;
            }

            if (string.IsNullOrEmpty(_exampleInstallDir))
            {
                await Dialog.AlertAsync("未找到安装目录，无法浏览示例。");
                return;
            }

            _showExamples = true;
        }

        private void OnExampleFileSelected(string filePath)
        {
            if (!File.Exists(filePath)) return;

            var isImage = FileTypeHelper.IsImage(filePath);

            var existing = _tabs.FirstOrDefault(t => t.FilePath == filePath);
            if (existing == null)
            {
                existing = new EditorTabInfo
                {
                    FilePath = filePath,
                    FileName = Path.GetFileName(filePath),
                    Content = isImage ? "" : FileSvc.ReadFile(filePath),
                    IsReadOnly = true
                };
                existing.MarkSaved();
                _tabs.Add(existing);
            }

            _activeTabPath = filePath;
            _currentContent = existing.Content ?? "";
            _selectedPath = null;
            _nonTextFilePath = null;
        }
    }
}