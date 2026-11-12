using Microsoft.AspNetCore.Components;
using QuickEditer.Core;
using QuickEditer.Models;

namespace QuickEditer.Components.Pages
{
    public partial class Index
    {
        private void OpenTab(string filePath)
        {
            var existing = _tabs.FirstOrDefault(t => t.FilePath == filePath);
            if (existing == null)
            {
                existing = new EditorTabInfo
                {
                    FilePath = filePath,
                    FileName = Path.GetFileName(filePath),
                    Content = FileSvc.ReadFile(filePath)
                };
                existing.MarkSaved();
                _tabs.Add(existing);
            }

            _activeTabPath = filePath;
            _currentContent = existing.Content ?? "";
            _selectedPath = filePath;
            _nonTextFilePath = null;
        }

        private void OpenReadOnlyTab(string filePath)
        {
            var existing = _tabs.FirstOrDefault(t => t.FilePath == filePath);
            if (existing == null)
            {
                existing = new EditorTabInfo
                {
                    FilePath = filePath,
                    FileName = Path.GetFileName(filePath),
                    Content = "",
                    IsReadOnly = true
                };
                existing.MarkSaved();
                _tabs.Add(existing);
            }

            _activeTabPath = filePath;
            _currentContent = "";
            _selectedPath = filePath;
            _nonTextFilePath = null;
        }

        private void SwitchTab(string filePath)
        {
            if (_activeTabPath == filePath) return;

            var tab = _tabs.FirstOrDefault(t => t.FilePath == filePath);
            if (tab == null) return;

            _activeTabPath = filePath;
            _currentContent = tab.Content ?? "";
            _selectedPath = tab.IsReadOnly ? null : filePath;
            _nonTextFilePath = null;

            _ = RestoreScrollAsync(tab.ScrollTop);
        }

        private async Task RestoreScrollAsync(int scrollTop)
        {
            await InvokeAsync(async () =>
            {
                await Task.Yield();
                if (_editorPanel != null)
                    await _editorPanel.RestoreScrollAsync(scrollTop);
            });
        }

        private async Task CloseTab(string filePath)
        {
            var tab = _tabs.FirstOrDefault(t => t.FilePath == filePath);
            if (tab == null) return;

            if (tab.IsDirty && !tab.IsReadOnly)
            {
                _confirmCloseTabPath = filePath;
                _confirmCloseTabFileName = tab.FileName;
                _showConfirmCloseTab = true;
                _confirmCloseTabTcs = new TaskCompletionSource<string>();

                var choice = await _confirmCloseTabTcs.Task;
                _showConfirmCloseTab = false;

                if (choice == "cancel") return;
                if (choice == "yes")
                {
                    await SaveTabAsync(tab);
                }
            }

            _tabs.Remove(tab);

            if (_activeTabPath == filePath)
            {
                var next = _tabs.LastOrDefault();
                _activeTabPath = next?.FilePath;
                _currentContent = next?.Content ?? "";
                _selectedPath = next?.IsReadOnly == true ? null : next?.FilePath;
            }
        }

        private void CloseOtherTabs()
        {
            if (_activeTabPath == null) return;
            var active = _tabs.FirstOrDefault(t => t.FilePath == _activeTabPath);

            foreach (var t in _tabs.Where(t => t.IsDirty && !t.IsReadOnly && t != active))
                FileSvc.SaveFile(t.FilePath, t.Content);

            _tabs.Clear();
            if (active != null) _tabs.Add(active);
        }

        private void CloseAllTabs()
        {
            foreach (var t in _tabs.Where(t => t.IsDirty && !t.IsReadOnly))
                FileSvc.SaveFile(t.FilePath, t.Content);

            _tabs.Clear();
            _activeTabPath = null;
            _currentContent = "";
            _nonTextFilePath = null;
        }

        private void OnTabDrop(string targetPath)
        {
            if (string.IsNullOrEmpty(_draggingTabPath) || _draggingTabPath == targetPath) return;

            var from = _tabs.FirstOrDefault(t => t.FilePath == _draggingTabPath);
            var to = _tabs.FirstOrDefault(t => t.FilePath == targetPath);
            if (from == null || to == null) return;

            var fromIdx = _tabs.IndexOf(from);
            var toIdx = _tabs.IndexOf(to);
            _tabs.RemoveAt(fromIdx);
            _tabs.Insert(toIdx, from);

            _draggingTabPath = null;
        }

        private async Task SaveActiveTab()
        {
            if (_activeTabPath == null) return;
            var tab = _tabs.FirstOrDefault(t => t.FilePath == _activeTabPath);
            if (tab == null) return;

            if (tab.IsReadOnly)
            {
                AppendLog(OutputLogLevel.Warning, $"「{tab.FileName}」是只读文件，无法保存");
                return;
            }

            await SaveTabAsync(tab);
        }

        private async Task SaveTabAsync(EditorTabInfo tab)
        {
            FileSvc.SaveFile(tab.FilePath, tab.Content);
            tab.MarkSaved();
            AppendLog(OutputLogLevel.Success, $"已保存：{tab.FileName}");
        }

        private void SaveAllTabs()
        {
            var saved = 0;
            foreach (var t in _tabs.Where(t => t.IsDirty && !t.IsReadOnly))
            {
                FileSvc.SaveFile(t.FilePath, t.Content);
                t.MarkSaved();
                saved++;
            }
            foreach (var t in _tabs)
                t.MarkSaved();

            AppendLog(OutputLogLevel.Success,
                saved > 0 ? $"已保存 {saved} 个文件" : "没有需要保存的文件");
        }

        private void OnEditorChanged((string FilePath, string Content) e)
        {
            var tab = _tabs.FirstOrDefault(t => t.FilePath == e.FilePath);
            if (tab == null || tab.IsReadOnly) return;

            tab.Content = e.Content;
            if (e.FilePath == _activeTabPath)
                _currentContent = e.Content;
        }

        private void OnEditorScrollChanged((string FilePath, int ScrollTop) e)
        {
            var tab = _tabs.FirstOrDefault(t => t.FilePath == e.FilePath);
            if (tab != null)
                tab.ScrollTop = e.ScrollTop;
        }


        private async Task UndoAsync() { if (_editorPanel != null) await _editorPanel.UndoAsync(); }
        private async Task RedoAsync() { if (_editorPanel != null) await _editorPanel.RedoAsync(); }
        private async Task CopyAsync() { if (_editorPanel != null) await _editorPanel.CopyAsync(); }
        private async Task CutAsync() { if (_editorPanel != null) await _editorPanel.CutAsync(); }
        private async Task PasteAsync() { if (_editorPanel != null) await _editorPanel.PasteAsync(); }
        private async Task SelectAllAsync() { if (_editorPanel != null) await _editorPanel.SelectAllAsync(); }
        private async Task FindAsync() { if (_editorPanel != null) await _editorPanel.FindAsync(); }
        private async Task ReplaceAsync() { if (_editorPanel != null) await _editorPanel.ReplaceAsync(); }

        private async Task ToggleCommentAsync() { if (_editorPanel != null) await _editorPanel.ToggleCommentAsync(); }
    }
}