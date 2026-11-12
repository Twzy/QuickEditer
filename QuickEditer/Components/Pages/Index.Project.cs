using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using QuickEditer.Core;
using QuickEditer.Models;

namespace QuickEditer.Components.Pages
{
    public partial class Index
    {
        private static void EnsureProjectDirs(string projectName)
        {
            var dir = Path.Combine(Config.ProjectsDir, projectName);
            if (!Directory.Exists(dir)) return;
            Directory.CreateDirectory(Path.Combine(dir, "build"));
            Directory.CreateDirectory(Path.Combine(dir, "logs"));
        }

        private void NewFile()
        {
            if (_currentConfig == null) return;
            _targetDir = _projectTree;
            _dialogHost?.PrepareCreateFile();
            _showCreateFileDialog = true;
        }

        private async Task RefreshTree()
        {
            if (_currentConfig == null) return;
            _projectTree = ProjectTreeSvc.BuildProjectTree(_currentConfig.Name);

            foreach (var t in _tabs.Where(t => !t.IsDirty && !t.IsReadOnly))
            {
                if (File.Exists(t.FilePath))
                {
                    var newContent = FileSvc.ReadFile(t.FilePath);
                    if (newContent != t.Content)
                    {
                        t.Content = newContent;
                        t.MarkSaved();
                        if (t.FilePath == _activeTabPath)
                            _currentContent = newContent;
                    }
                }
            }

            await Task.CompletedTask;
        }

        private async Task OnTreeSelect(ProjectTreeNode node)
        {
            _selectedPath = node.FullPath;

            if (node.NodeType == ProjectNodeType.ProjectRoot)
            {
                if (_currentConfig == null || _currentConfig.Name != node.Name)
                    await LoadProjectAsync(node.Name);
                _nonTextFilePath = null;
                return;
            }

            if (node.NodeType == ProjectNodeType.Folder)
            {
                _nonTextFilePath = null;
                return;
            }

            if (node.NodeType != ProjectNodeType.File) return;

            if (FileTypeHelper.IsImage(node.FullPath))
            {
                _nonTextFilePath = null;
                OpenReadOnlyTab(node.FullPath);
            }
            else if (FileTypeHelper.IsText(node.FullPath))
            {
                _nonTextFilePath = null;
                OpenTab(node.FullPath);
            }
            else
            {
                _nonTextFilePath = node.FullPath;
            }
        }

        private async Task OnTreeContextMenu((ProjectTreeNode Node, string Action) e)
        {
            switch (e.Action)
            {
                case "new-folder":
                    _targetDir = e.Node;
                    _dialogHost?.PrepareCreateFolder();
                    _showCreateFolderDialog = true;
                    break;
                case "new-file":
                    _targetDir = e.Node;
                    _dialogHost?.PrepareCreateFile();
                    _showCreateFileDialog = true;
                    break;
                case "delete":
                    await DeleteNodeAsync(e.Node);
                    break;
                case "refresh":
                    await RefreshTree();
                    break;
                case "upload":
                    await UploadFilesToAsync(e.Node.FullPath);
                    break;
            }
        }

        private async Task UploadFilesToAsync(string targetDir)
        {
            try
            {
                _selfRef ??= DotNetObjectReference.Create(this);
                await JS.InvokeVoidAsync("quickEditer.pickAndUpload", targetDir, _selfRef);
            }
            catch (Exception ex)
            {
                await Dialog.AlertAsync("上传失败：" + ex.Message);
            }
        }

        [JSInvokable]
        public async Task OnUploadCompleted(string dir)
        {
            await RefreshTree();
        }

        private void OnRenameRequest(ProjectTreeNode node)
        {
            _renameTarget = node;
            _dialogHost?.PrepareRename(node.Name);
            _showRenameDialog = true;
        }

        private async Task OnMoveRequest((string SourcePath, string TargetDirPath) e)
        {
            var source = e.SourcePath;
            var targetDir = e.TargetDirPath;

            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(targetDir)) return;
            if (string.Equals(source, targetDir, StringComparison.OrdinalIgnoreCase)) return;
            if (targetDir.StartsWith(source + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                await Dialog.AlertAsync("不能移动到自身或其子目录");
                return;
            }

            var name = Path.GetFileName(source);
            var dest = Path.Combine(targetDir, name);
            if (File.Exists(dest) || Directory.Exists(dest))
            {
                await Dialog.AlertAsync($"目标目录已存在「{name}」");
                return;
            }

            try
            {
                if (Directory.Exists(source))
                    Directory.Move(source, dest);
                else
                    File.Move(source, dest);

                foreach (var t in _tabs.Where(t => t.FilePath == source || t.FilePath.StartsWith(source + Path.DirectorySeparatorChar)))
                {
                    t.FilePath = t.FilePath.Replace(source, dest);
                    t.FileName = Path.GetFileName(t.FilePath);
                }

                if (_activeTabPath == source || (_activeTabPath?.StartsWith(source + Path.DirectorySeparatorChar) ?? false))
                    _activeTabPath = _activeTabPath.Replace(source, dest);

                await RefreshTree();
            }
            catch (Exception ex)
            {
                await Dialog.AlertAsync("移动失败：" + ex.Message);
            }
        }

        private void OpenNewProjectDialog()
        {
            _dialogHost?.ResetNewProjectForm();
            _showNewDialog = true;
        }

        private void CloseNewProjectDialog() => _showNewDialog = false;

        private async Task OnNewProjectSubmit((string Name, string Fqbn, string Description, string TemplateId) data)
        {
            var config = ProjectSvc.CreateProject(data.Name, data.Fqbn, data.Description, data.TemplateId, out var error);
            if (config == null)
            {
                await Dialog.AlertAsync(string.IsNullOrEmpty(error) ? "创建项目失败" : error);
                return;
            }

            _showNewDialog = false;
            _currentConfig = config;
            _projects = ProjectSvc.ListProjects();
            EnsureProjectDirs(config.Name);
            _projectTree = ProjectTreeSvc.BuildProjectTree(config.Name);
            _nonTextFilePath = null;

            _tabs.Clear();
            _activeTabPath = null;
            _currentContent = "";

            var sketch = Path.Combine(ProjectSvc.GetProjectDir(config.Name), config.MainSketchFile);
            _selectedPath = sketch;
            OpenTab(sketch);

            Config.Cfg.LastProject = config.Name;
            Config.AddRecentProject(config.Name);

            await RefreshExampleSourcesAsync();
        }

        private async Task LoadProjectAsync(string name)
        {
            _currentConfig = ProjectSvc.LoadProject(name);
            if (_currentConfig != null)
            {
                EnsureProjectDirs(name);
                MigrateLegacySrcLayout(_currentConfig);
                _projectTree = ProjectTreeSvc.BuildProjectTree(name);
                _nonTextFilePath = null;

                _tabs.Clear();
                _activeTabPath = null;
                _currentContent = "";

                var sketch = Path.Combine(ProjectSvc.GetProjectDir(name), _currentConfig.MainSketchFile);
                _selectedPath = sketch;
                OpenTab(sketch);

                Config.Cfg.LastProject = name;
                Config.AddRecentProject(name);

                await RefreshExampleSourcesAsync();
            }
            _showOpenDialog = false;
        }

        private void MigrateLegacySrcLayout(ProjectConfig config)
        {
            var projectDir = ProjectSvc.GetProjectDir(config.Name);
            var legacySrc = Path.Combine(projectDir, "src");

            var expected = Path.Combine(projectDir, $"{config.Name}.ino");
            if (File.Exists(expected))
            {
                if (config.MainSketchFile != $"{config.Name}.ino")
                {
                    config.MainSketchFile = $"{config.Name}.ino";
                    ProjectSvc.SaveConfig(config);
                }
                return;
            }

            if (!Directory.Exists(legacySrc)) return;

            var candidate = Path.Combine(legacySrc, $"{config.Name}.ino");
            if (!File.Exists(candidate))
                candidate = Directory.GetFiles(legacySrc, "*.ino").FirstOrDefault() ?? "";

            if (string.IsNullOrEmpty(candidate) || !File.Exists(candidate)) return;

            try
            {
                var target = Path.Combine(projectDir, $"{config.Name}.ino");
                if (!File.Exists(target))
                    File.Move(candidate, target);

                foreach (var f in Directory.GetFiles(legacySrc))
                {
                    var dest = Path.Combine(projectDir, Path.GetFileName(f));
                    if (!File.Exists(dest))
                        File.Move(f, dest);
                }

                if (Directory.Exists(legacySrc) &&
                    !Directory.EnumerateFileSystemEntries(legacySrc).Any())
                    Directory.Delete(legacySrc);

                config.MainSketchFile = $"{config.Name}.ino";
                ProjectSvc.SaveConfig(config);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Migrate] {config.Name} 迁移失败：{ex.Message}");
            }
        }

        private async Task ConfirmCreateFolder(string name)
        {
            if (_targetDir == null || _currentConfig == null) return;

            var (ok, err) = FileSvc.CreateFolder(_targetDir.FullPath, name);
            if (!ok)
            {
                await Dialog.AlertAsync(err);
                return;
            }

            _showCreateFolderDialog = false;
            await RefreshTree();
        }

        private async Task ConfirmCreateFile(string name)
        {
            if (_targetDir == null || _currentConfig == null) return;

            var ext = Path.GetExtension(name).ToLowerInvariant();
            if (ext == ".ino")
            {
                var expected = $"{_currentConfig.Name}.ino";
                if (!string.Equals(name, expected, StringComparison.OrdinalIgnoreCase))
                {
                    await Dialog.AlertAsync(
                        $"项目主草图必须命名为「{expected}」，且只能有一个。\n其他代码请使用 .cpp / .h 文件。");
                    return;
                }
            }

            var (ok, err) = FileSvc.CreateFile(_targetDir.FullPath, name);
            if (!ok)
            {
                await Dialog.AlertAsync(err);
                return;
            }

            _showCreateFileDialog = false;
            await RefreshTree();
        }

        private async Task ConfirmRename(string newName)
        {
            if (_renameTarget == null || _currentConfig == null) return;

            if (FileTypeHelper.IsMainSketch(_renameTarget.FullPath, _currentConfig.Name))
            {
                await Dialog.AlertAsync($"主草图文件「{_currentConfig.MainSketchFile}」与项目同名，不能重命名。");
                return;
            }

            var (ok, err) = ProjectTreeSvc.Rename(_renameTarget, newName);
            if (!ok)
            {
                await Dialog.AlertAsync(err);
                return;
            }

            var oldPath = _renameTarget.FullPath;
            var parent = Path.GetDirectoryName(oldPath);
            var newPath = string.IsNullOrEmpty(parent) ? null : Path.Combine(parent, newName.Trim());

            if (newPath != null)
            {
                foreach (var t in _tabs.Where(t => t.FilePath == oldPath || t.FilePath.StartsWith(oldPath + Path.DirectorySeparatorChar)))
                {
                    t.FilePath = t.FilePath.Replace(oldPath, newPath);
                    t.FileName = Path.GetFileName(t.FilePath);
                }

                if (_activeTabPath == oldPath || (_activeTabPath?.StartsWith(oldPath + Path.DirectorySeparatorChar) ?? false))
                    _activeTabPath = _activeTabPath.Replace(oldPath, newPath);
            }

            _showRenameDialog = false;
            await RefreshTree();
        }

        private async Task DeleteNodeAsync(ProjectTreeNode node)
        {
            if (node.NodeType == ProjectNodeType.ProjectRoot)
            {
                await Dialog.AlertAsync("不能删除项目根节点");
                return;
            }

            if (!await Dialog.ConfirmAsync($"确定删除「{node.Name}」吗？"))
                return;

            var (ok, err) = FileSvc.Delete(node.FullPath);
            if (!ok)
            {
                await Dialog.AlertAsync(err);
                return;
            }

            var toRemove = _tabs.Where(t =>
                t.FilePath == node.FullPath ||
                t.FilePath.StartsWith(node.FullPath + Path.DirectorySeparatorChar)
            ).ToList();

            foreach (var t in toRemove)
            {
                var wasActive = _activeTabPath == t.FilePath;
                _tabs.Remove(t);

                if (wasActive)
                {
                    var next = _tabs.LastOrDefault();
                    _activeTabPath = next?.FilePath;
                    _currentContent = next?.Content ?? "";
                }
            }

            if (_nonTextFilePath != null &&
                (_nonTextFilePath == node.FullPath ||
                 _nonTextFilePath.StartsWith(node.FullPath + Path.DirectorySeparatorChar)))
                _nonTextFilePath = null;

            await RefreshTree();
        }

        private async Task DeleteProjectAsync(string name)
        {
            var ok = ProjectSvc.DeleteProject(name, out var error);
            if (!ok)
            {
                await Dialog.AlertAsync(string.IsNullOrEmpty(error) ? "删除项目失败" : error);
                return;
            }

            if (_currentConfig != null && _currentConfig.Name == name)
            {
                _currentConfig = null;
                _projectTree = null;
                _selectedPath = null;
                _tabs.Clear();
                _activeTabPath = null;
                _currentContent = "";
                _nonTextFilePath = null;
            }

            if (Config.Cfg.LastProject == name)
            {
                Config.Cfg.LastProject = "";
                Config.ConfigAll();
            }

            Config.Cfg.RecentProjects.RemoveAll(p => string.Equals(p, name, StringComparison.OrdinalIgnoreCase));
            Config.ConfigAll();

            _projects = ProjectSvc.ListProjects();
            await RefreshExampleSourcesAsync();
        }
    }
}