using QuickEditer.Core;
using QuickEditer.Models;

namespace QuickEditer.Components.Pages
{
    public partial class Index
    {
        private void AppendLog(OutputLogLevel level, string text)
        {
            _logs.Add(new OutputLogEntry { Level = level, Text = text });
            if (_logs.Count > 500)
                _logs.RemoveRange(0, _logs.Count - 500);
            StateHasChanged();
        }


        private async Task CompileAsync()
        {
            if (_currentConfig == null) return;

            _compileCts?.Cancel();
            _compileCts = new CancellationTokenSource();
            var ct = _compileCts.Token;

            SaveAllTabs();

            if (_currentConfig.Libraries.Count > 0)
            {
                var installedResult = await ArduinoCliSvc.ListInstalledLibrariesAsync();
                var installed = ArduinoCliSvc.ParseInstalledLibraries(installedResult)
                    .Select(l => l.Name)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var missing = _currentConfig.Libraries
                    .Where(l => !installed.Contains(l))
                    .ToList();

                if (missing.Count > 0)
                {
                    var msg = "以下库依赖未安装：\n" + string.Join("\n", missing) +
                              "\n\n是否立即安装？";
                    if (await Dialog.ConfirmAsync(msg))
                    {
                        EnsureBottomTab("输出");
                        foreach (var lib in missing)
                        {
                            AppendLog(OutputLogLevel.Info, $"安装库：{lib}");
                            var r = await ArduinoCliSvc.InstallLibraryAsync(lib);
                            AppendLog(r.Success ? OutputLogLevel.Success : OutputLogLevel.Error,
                                r.Success ? $"库 {lib} 安装成功" : $"库 {lib} 安装失败：{r.Error}");
                        }
                    }
                }
            }

            EnsureBottomTab("输出");

            var logStartIndex = _logs.Count;
            AppendLog(OutputLogLevel.Info, $"开始编译：{_currentConfig.Name} @ {_currentConfig.BoardFqbn}");

            var boardConfig = GetBoardConfigValues(_currentConfig.BoardFqbn);
            var sw = System.Diagnostics.Stopwatch.StartNew();

            var result = await ArduinoCliSvc.CompileAsync(
                _currentConfig.Name,
                _currentConfig.BoardFqbn,
                boardConfig,
    line => InvokeAsync(() => AppendLog(OutputLogLevel.Info, CleanPath(line))),
line => InvokeAsync(() => AppendLog(OutputLogLevel.Error, CleanPath(line))),
                ct);
            sw.Stop();

            if (ct.IsCancellationRequested)
            {
                AppendLog(OutputLogLevel.Warning, "编译已取消");
                _lastArtifacts.Clear();
                _compileCts?.Dispose();
                _compileCts = null;
                return;
            }

            if (!result.Success)
            {
                AppendLog(OutputLogLevel.Error, $"编译失败，耗时 {sw.Elapsed.TotalSeconds:F1}s");
                _lastArtifacts.Clear();
            }
            else if (result.OutputFiles.Count == 0)
            {
                AppendLog(OutputLogLevel.Success, $"编译完成，耗时 {sw.Elapsed.TotalSeconds:F1}s");
                AppendLog(OutputLogLevel.Warning, "未生成 .bin/.hex/.uf2/.eep 产物");
                _lastArtifacts.Clear();
            }
            else
            {
                AppendLog(OutputLogLevel.Success, $"编译成功，耗时 {sw.Elapsed.TotalSeconds:F1}s");
                var artifactsEntry = new OutputLogEntry
                {
                    Level = OutputLogLevel.Success,
                    Text = $"产物：{result.OutputFiles.Count} 个",
                    Artifacts = result.OutputFiles
                };
                _logs.Add(artifactsEntry);
                if (_logs.Count > 500)
                    _logs.RemoveRange(0, _logs.Count - 500);

                _lastArtifacts = result.OutputFiles;
            }

            try
            {
                var logsDir = Path.Combine(ProjectSvc.GetProjectDir(_currentConfig.Name), "logs");
                Directory.CreateDirectory(logsDir);
                var logFile = Path.Combine(logsDir, $"{DateTime.Now:yyyyMMdd_HHmmss}.log");

                var sb = new System.Text.StringBuilder();
                foreach (var log in _logs.Skip(logStartIndex))
                {
                    sb.AppendLine($"[{log.DisplayTime}] [{log.LevelLabel}] {log.Text}");
                    if (log.Artifacts != null)
                    {
                        foreach (var f in log.Artifacts)
                            sb.AppendLine($"    产物：{f.FileName}（{f.Size} 字节）");
                    }
                }

                await File.WriteAllTextAsync(logFile, sb.ToString(), System.Text.Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Compile] 写入日志失败：{ex.Message}");
            }

            _compileCts?.Dispose();
            _compileCts = null;
        }

        private static string CleanPath(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            var baseDir = Config.BaseDir;
            if (string.IsNullOrEmpty(baseDir)) return text;

            return text
                .Replace(baseDir, "..", StringComparison.OrdinalIgnoreCase)
                .Replace(baseDir.Replace("\\","\\\\"), "..", StringComparison.OrdinalIgnoreCase);
        }

        private void CancelCompile()
        {
            _compileCts?.Cancel();
            AppendLog(OutputLogLevel.Warning, "正在取消编译...");
        }

        private async Task CleanBuildAsync()
        {
            if (_currentConfig == null) return;
            if (!await Dialog.ConfirmAsync("确定清空 build 目录吗？")) return;

            var (ok, err) = ProjectSvc.CleanBuild(_currentConfig.Name);
            if (!ok)
            {
                await Dialog.AlertAsync(err);
                return;
            }

            AppendLog(OutputLogLevel.Success, "已清理 build 目录");
            await RefreshTree();
        }

        private async Task CleanLogsAsync()
        {
            if (_currentConfig == null)
            {
                AppendLog(OutputLogLevel.Warning, "未打开项目，无法清理日志");
                return;
            }
            var removed = ProjectSvc.CleanLogs(_currentConfig.Name);
            AppendLog(OutputLogLevel.Success, $"已清理 {removed} 个日志文件");
            await RefreshTree();
        }
    }
}