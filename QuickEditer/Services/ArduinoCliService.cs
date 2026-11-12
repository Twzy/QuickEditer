// 文件路径：Services/ArduinoCliService.cs
using Microsoft.AspNetCore.Http;
using QuickEditer.Core;
using QuickEditer.Models;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace QuickEditer.Services;

public class ArduinoCliService
{
    private readonly ILogger<ArduinoCliService> _logger;
    private static readonly SemaphoreSlim _compileLock = new(1, 1);

    public ArduinoCliService(ILogger<ArduinoCliService> logger)
    {
        _logger = logger;
    }

    private string CliExePath
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Config.Cfg.ArduinoCliPath))
                return Config.Cfg.ArduinoCliPath;

            var fileName = OperatingSystem.IsWindows() ? "arduino-cli.exe" : "arduino-cli";
            return Path.Combine(Config.ArduinoCliDir, fileName);
        }
    }

    private string CliConfigPath => Path.Combine(Config.ArduinoCliDir, "arduino-cli.yaml");
    private string CliDataDir => Path.Combine(Config.ArduinoCliDir, "data");
    private string CliDownloadsDir => Path.Combine(Config.ArduinoCliDir, "staging");
    private string CliUserDir => Path.Combine(Config.ArduinoCliDir, "user");

    public void EnsureEnvironment()
    {
        Directory.CreateDirectory(Config.ArduinoCliDir);
        Directory.CreateDirectory(Path.Combine(Config.ArduinoCliDir, ".arduino15"));
        Directory.CreateDirectory(CliDataDir);
        Directory.CreateDirectory(CliDownloadsDir);
        Directory.CreateDirectory(CliUserDir);

        EnsureArduinoCliBinary();

        var yaml = BuildDefaultConfig();
        File.WriteAllText(CliConfigPath, yaml, Encoding.UTF8);
    }

    private void EnsureArduinoCliBinary()
    {
        var target = CliExePath;
        if (File.Exists(target)) return;

        var platformDir = GetPlatformFolder();
        if (string.IsNullOrEmpty(platformDir))
        {
            _logger.LogError("Unsupported platform for arduino-cli");
            return;
        }

        var fileName = OperatingSystem.IsWindows() ? "arduino-cli.exe" : "arduino-cli";
        var source = Path.Combine(Config.RawArduinoCliDir, platformDir, fileName);

        if (!File.Exists(source))
        {
            _logger.LogError("arduino-cli source not found: {Source}", source);
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target, true);

            if (!OperatingSystem.IsWindows())
            {
                try
                {
                    File.SetUnixFileMode(target,
                        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                        UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                        UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to set executable bit on {Path}", target);
                }
            }

            _logger.LogInformation("Copied arduino-cli from {Source} to {Target}", source, target);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy arduino-cli from {Source} to {Target}", source, target);
        }
    }

    private static string? GetPlatformFolder()
    {
        if (OperatingSystem.IsWindows())
            return Environment.Is64BitOperatingSystem ? "win_64" : "win_32";

        if (OperatingSystem.IsLinux())
            return Environment.Is64BitOperatingSystem ? "linux_64" : "linux_32";

        return null;
    }

    private string BuildDefaultConfig()
    {
        var sb = new StringBuilder();
        sb.AppendLine("board_manager:");
        sb.AppendLine("  additional_urls:");

        if (!string.IsNullOrWhiteSpace(Config.Cfg.AdditionalUrls))
        {
            foreach (var url in Config.Cfg.AdditionalUrls.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                sb.AppendLine($"    - {url.Trim()}");
            }
        }

        sb.AppendLine("directories:");
        sb.AppendLine($"  data: {EscapeYaml(CliDataDir)}");
        sb.AppendLine($"  downloads: {EscapeYaml(CliDownloadsDir)}");
        sb.AppendLine($"  user: {EscapeYaml(CliUserDir)}");
        sb.AppendLine("build_cache:");
        sb.AppendLine($"  path: {EscapeYaml(Path.Combine(Config.ArduinoCliDir, "build-cache"))}");
        sb.AppendLine("library:");
        sb.AppendLine("  enable_unsafe_install: false");
        sb.AppendLine("logging:");
        sb.AppendLine("  level: info");

        return sb.ToString();
    }

    private static string EscapeYaml(string path) =>
        "\"" + path.Replace("\\", "/") + "\"";

    private async Task<CliResult> RunAsync(string arguments, CancellationToken ct = default)
    {
        EnsureEnvironment();

        var exe = CliExePath;
        if (!File.Exists(exe))
        {
            _logger.LogError("arduino-cli not found at {Path}", exe);
            return new CliResult
            {
                ExitCode = -1,
                Error = $"未找到 arduino-cli：{exe}。请将 arduino-cli 可执行文件放入 ArduinoCore 目录，或在设置中指定路径。"
            };
        }

        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = $"--config-file \"{CliConfigPath}\" {arguments}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Config.ArduinoCliDir
        };
        if (!OperatingSystem.IsWindows())
        {
            psi.Environment["HOME"] = Config.ArduinoCliDir;
            psi.Environment["USERPROFILE"] = Config.ArduinoCliDir;
        }
  

        if (!OperatingSystem.IsWindows())
        {
            var currentPath = psi.Environment.TryGetValue("PATH", out var p) ? p : "";
            if (!currentPath.Contains("/usr/bin", StringComparison.Ordinal))
            {
                psi.Environment["PATH"] = string.IsNullOrEmpty(currentPath)
                    ? "/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin"
                    : "/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin:" + currentPath;
            }
        }


        using var process = new Process { StartInfo = psi };
        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start arduino-cli");
            return new CliResult { ExitCode = -1, Error = ex.Message };
        }

        var output = await process.StandardOutput.ReadToEndAsync(ct);
        var error = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        return new CliResult { ExitCode = process.ExitCode, Output = output, Error = error };
    }

    private async Task<CliResult> RunStreamAsync(
        string arguments,
        Action<string>? onOutput,
        Action<string>? onError,
        CancellationToken ct = default)
    {
        EnsureEnvironment();

        var exe = CliExePath;
        if (!File.Exists(exe))
        {
            var errMsg = $"未找到 arduino-cli：{exe}。请将 arduino-cli 可执行文件放入 ArduinoCore 目录，或在设置中指定路径。";
            onError?.Invoke(errMsg);
            return new CliResult { ExitCode = -1, Error = errMsg };
        }

        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = $"--config-file \"{CliConfigPath}\" {arguments}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Config.ArduinoCliDir
        };

        if (!OperatingSystem.IsWindows())
        {
            psi.Environment["HOME"] = Config.ArduinoCliDir;
            psi.Environment["USERPROFILE"] = Config.ArduinoCliDir;
        }
    

        if (!OperatingSystem.IsWindows())
        {
            var currentPath = psi.Environment.TryGetValue("PATH", out var p) ? p : "";
            if (!currentPath.Contains("/usr/bin", StringComparison.Ordinal))
            {
                psi.Environment["PATH"] = string.IsNullOrEmpty(currentPath)
                    ? "/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin"
                    : "/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin:" + currentPath;
            }
        }

        using var process = new Process { StartInfo = psi };
        var output = new StringBuilder();
        var error = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            output.AppendLine(e.Data);
            onOutput?.Invoke(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            error.AppendLine(e.Data);
            onError?.Invoke(e.Data);
        };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start arduino-cli");
            onError?.Invoke(ex.Message);
            return new CliResult { ExitCode = -1, Error = ex.Message };
        }

        try
        {
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            try { if (!process.HasExited) process.Kill(true); } catch { }
            return new CliResult
            {
                ExitCode = -1,
                Output = output.ToString(),
                Error = "已取消"
            };
        }
        return new CliResult
        {
            ExitCode = process.ExitCode,
            Output = output.ToString(),
            Error = error.ToString()
        };
    }

    // ===== 基础命令 =====

    public async Task<bool> CheckCliAsync(CancellationToken ct = default)
    {
        var result = await RunAsync("version --format json", ct);
        return result.Success;
    }

    public async Task<CliResult> UpdateIndexAsync(CancellationToken ct = default)
        => await RunAsync("core update-index --format json", ct);

    public async Task<CliResult> ListCoresAsync(CancellationToken ct = default)
        => await RunAsync("core list --format json", ct);

    public async Task<CliResult> SearchCoresAsync(CancellationToken ct = default)
        => await RunAsync("core search --format json", ct);

    public async Task<CliResult> InstallCoreAsync(string coreId, string? version = null, CancellationToken ct = default)
    {
        var safeId = coreId.Replace("\"", "");
        var args = string.IsNullOrEmpty(version)
            ? $"core install {safeId}"
            : $"core install {safeId}@{version}";
        return await RunAsync(args, ct);
    }

    public async Task<CliResult> UninstallCoreAsync(string coreId, CancellationToken ct = default)
    {
        var safe = coreId.Replace("\"", "");
        return await RunAsync($"core uninstall {safe}", ct);
    }

    public async Task<CliResult> ListBoardsAsync(CancellationToken ct = default)
        => await RunAsync("board listall --format json", ct);

    public async Task<CliResult> GetBoardDetailsAsync(string fqbn, CancellationToken ct = default)
    {
        var safe = fqbn.Replace("\"", "");
        return await RunAsync($"board details --fqbn {safe} --format json", ct);
    }

    // ===== 库命令 =====

    public async Task<CliResult> UpdateLibraryIndexAsync(CancellationToken ct = default)
        => await RunAsync("lib update-index", ct);

    public async Task<CliResult> ListInstalledLibrariesAsync(CancellationToken ct = default)
        => await RunAsync("lib list --format json", ct);

    public async Task<CliResult> SearchLibrariesAsync(string keyword, CancellationToken ct = default)
    {
        var safe = keyword.Replace("\"", "");
        return await RunAsync($"lib search \"{safe}\" --format json", ct);
    }

    public async Task<CliResult> ListAllAvailableLibrariesAsync(CancellationToken ct = default)
    {
        var r = await RunAsync("lib search --format json", ct);
        if (r.Success && !string.IsNullOrWhiteSpace(r.Output) && r.Output.Trim() != "{}" && r.Output.Trim() != "[]")
            return r;

        return await RunAsync("lib search \"\" --format json", ct);
    }

    public async Task<CliResult> InstallLibraryAsync(string libName, string? version = null, CancellationToken ct = default)
    {
        var safe = libName.Replace("\"", "");
        var args = string.IsNullOrEmpty(version)
            ? $"lib install \"{safe}\""
            : $"lib install \"{safe}@{version}\"";
        return await RunAsync(args, ct);
    }

    public async Task<CliResult> UninstallLibraryAsync(string libName, CancellationToken ct = default)
    {
        var safe = libName.Replace("\"", "");
        return await RunAsync($"lib uninstall \"{safe}\"", ct);
    }

    public async Task<CliResult> UpgradeLibraryAsync(string libName, CancellationToken ct = default)
    {
        var safe = libName.Replace("\"", "");
        return await RunAsync($"lib upgrade \"{safe}\"", ct);
    }

    public async Task<CliResult> SyncLibrariesAsync(CancellationToken ct = default)
    {
        var update = await RunAsync("lib update-index", ct);
        if (!update.Success)
            return update;

        return await RunAsync("lib list --format json", ct);
    }

    // ===== 编译 =====

    public async Task<CompileResult> CompileAsync(
        string projectName,
        string fqbn,
        Dictionary<string, string>? boardConfig = null,
        CancellationToken ct = default)
    {
        await _compileLock.WaitAsync(ct);
        try
        {
            return await CompileInternalAsync(projectName, fqbn, boardConfig, null, null, ct);
        }
        finally
        {
            _compileLock.Release();
        }
    }

    public async Task<CompileResult> CompileAsync(
        string projectName,
        string fqbn,
        Dictionary<string, string>? boardConfig,
        Action<string>? onOutput,
        Action<string>? onError,
        CancellationToken ct = default)
    {
        await _compileLock.WaitAsync(ct);
        try
        {
            return await CompileInternalAsync(projectName, fqbn, boardConfig, onOutput, onError, ct);
        }
        finally
        {
            _compileLock.Release();
        }
    }

    private async Task<CompileResult> CompileInternalAsync(
        string projectName,
        string fqbn,
        Dictionary<string, string>? boardConfig,
        Action<string>? onOutput,
        Action<string>? onError,
        CancellationToken ct)
    {
        var projectDir = Path.Combine(Config.ProjectsDir, projectName);
        var buildDir = Path.Combine(projectDir, "build");

        if (!Directory.Exists(projectDir))
            return new CompileResult { Success = false, Error = $"项目目录不存在：{projectDir}" };

        var mainIno = Path.Combine(projectDir, $"{projectName}.ino");
        if (!File.Exists(mainIno))
            return new CompileResult { Success = false, Error = $"未找到主 sketch 文件：{mainIno}" };

        var buildTmpDir = Path.Combine(buildDir, "tmp");
        Directory.CreateDirectory(buildDir);
        Directory.CreateDirectory(buildTmpDir);
        Directory.CreateDirectory(Path.Combine(projectDir, "logs"));

        var cacheDir = Path.Combine(Config.ArduinoCliDir, "build-cache");
        Directory.CreateDirectory(cacheDir);

        var fqbnWithOptions = fqbn;
        if (boardConfig != null && boardConfig.Count > 0)
        {
            var pairs = boardConfig
                .Where(kv => !string.IsNullOrWhiteSpace(kv.Key) && !string.IsNullOrWhiteSpace(kv.Value))
                .Select(kv => $"{kv.Key}={kv.Value}")
                .ToList();
            if (pairs.Count > 0)
                fqbnWithOptions = $"{fqbn}:{string.Join(",", pairs)}";
        }

        var args = $"compile --fqbn {fqbnWithOptions} " +
                   $"--build-path \"{buildTmpDir}\" " +
                   $"--build-cache-path \"{cacheDir}\" " +
                   $"--output-dir \"{buildDir}\" " +
                   $"\"{projectDir}\" --format json";

        CliResult result;
        if (onOutput != null || onError != null)
        {
            result = await RunStreamAsync(args, onOutput, onError, ct);
        }
        else
        {
            result = await RunAsync(args, ct);
        }

        if (!result.Success)
        {
            _logger.LogError("Compile failed: {Error}", result.Error);
            return new CompileResult
            {
                Success = false,
                RawOutput = result.Output,
                Error = result.Error
            };
        }

        return new CompileResult
        {
            Success = true,
            BuildDir = buildDir,
            OutputFiles = ListBuildFiles(buildDir),
            RawOutput = result.Output
        };
    }

    private List<BuildFile> ListBuildFiles(string buildDir)
    {
        var files = new List<BuildFile>();
        if (!Directory.Exists(buildDir)) return files;

        foreach (var path in Directory.GetFiles(buildDir))
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext is ".bin" or ".hex" or ".uf2" or ".eep")
            {
                files.Add(new BuildFile
                {
                    FileName = Path.GetFileName(path),
                    FullPath = path,
                    Size = new FileInfo(path).Length
                });
            }
        }

        return files;
    }

    // ===== 解析方法 =====

    public List<ArduinoCoreInfo> ParseCores(CliResult result)
    {
        var list = new List<ArduinoCoreInfo>();
        if (!result.Success || string.IsNullOrWhiteSpace(result.Output)) return list;

        try
        {
            using var doc = JsonDocument.Parse(result.Output);
            if (!doc.RootElement.TryGetProperty("platforms", out var platforms) || platforms.ValueKind != JsonValueKind.Array)
                return list;

            foreach (var p in platforms.EnumerateArray())
            {
                var id = p.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";

                var core = new ArduinoCoreInfo
                {
                    Id = id,
                    Name = p.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
                    Maintainer = p.TryGetProperty("maintainer", out var mt) ? mt.GetString() ?? "" : "",
                    Website = p.TryGetProperty("website", out var wb) ? wb.GetString() ?? "" : "",
                    Email = p.TryGetProperty("email", out var em) ? em.GetString() ?? "" : "",
                    Source = id.StartsWith("arduino:", StringComparison.OrdinalIgnoreCase) ? "official" : "additional"
                };

                core.Installed = GetStringProperty(p, "installed_version")
                                 ?? GetStringProperty(p, "installed") ?? "";
                core.Latest = GetStringProperty(p, "latest_version")
                              ?? GetStringProperty(p, "latest") ?? "";

                if (p.TryGetProperty("releases", out var rel) && rel.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in rel.EnumerateObject())
                    {
                        var ver = prop.Name;
                        core.Releases.Add(ver);

                        var isInstalled = false;
                        if (prop.Value.TryGetProperty("installed", out var insFlag) && insFlag.ValueKind == JsonValueKind.True)
                            isInstalled = true;
                        if (!string.IsNullOrEmpty(core.Installed) &&
                            string.Equals(ver, core.Installed, StringComparison.OrdinalIgnoreCase))
                            isInstalled = true;

                        if (isInstalled && prop.Value.TryGetProperty("boards", out var boards) &&
                            boards.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var b in boards.EnumerateArray())
                            {
                                var boardName = b.TryGetProperty("name", out var bn) ? bn.GetString() ?? "" : "";
                                var fqbn = b.TryGetProperty("fqbn", out var fq) ? fq.GetString() ?? "" : "";

                                if (!string.IsNullOrEmpty(fqbn))
                                {
                                    core.Boards.Add(new ArduinoBoardInfo
                                    {
                                        Name = boardName,
                                        Fqbn = fqbn,
                                        Platform = id
                                    });
                                }
                            }
                        }
                    }
                }

                core.Releases = core.Releases
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Distinct()
                    .OrderByDescending(v => ParseVersion(v))
                    .ToList();

                list.Add(core);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Parse cores failed");
        }
        return list;
    }

    private static string? GetStringProperty(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Null => null,
            _ => v.ToString()
        };
    }

    private static Version ParseVersion(string v)
    {
        var cleaned = v.Split('-')[0];
        return Version.TryParse(cleaned, out var ver) ? ver : new Version(0, 0);
    }

    public List<ArduinoBoardInfo> ParseBoards(CliResult result)
    {
        var list = new List<ArduinoBoardInfo>();
        if (!result.Success || string.IsNullOrWhiteSpace(result.Output)) return list;

        try
        {
            using var doc = JsonDocument.Parse(result.Output);
            if (doc.RootElement.TryGetProperty("boards", out var boards) && boards.ValueKind == JsonValueKind.Array)
            {
                foreach (var b in boards.EnumerateArray())
                {
                    list.Add(new ArduinoBoardInfo
                    {
                        Name = b.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                        Fqbn = b.TryGetProperty("fqbn", out var f) ? f.GetString() ?? "" : "",
                        Platform = b.TryGetProperty("platform", out var pl) && pl.TryGetProperty("metadata", out var md) && md.TryGetProperty("id", out var pid)
                            ? pid.GetString() ?? "" : ""
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Parse boards failed");
        }
        return list;
    }

    public async Task<List<ArduinoBoardInfo>> GetAvailableBoardsAsync(CancellationToken ct = default)
    {
        var cliResult = await ListBoardsAsync(ct);
        var boards = ParseBoards(cliResult);
        foreach (var b in boards)
            b.IsBuiltin = false;
        return boards;
    }

    public List<ArduinoLibraryInfo> ParseInstalledLibraries(CliResult result)
    {
        var list = new List<ArduinoLibraryInfo>();
        if (!result.Success || string.IsNullOrWhiteSpace(result.Output)) return list;

        try
        {
            using var doc = JsonDocument.Parse(result.Output);

            JsonElement arr;
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
                arr = doc.RootElement;
            else if (doc.RootElement.TryGetProperty("installed_libraries", out var il))
                arr = il;
            else if (doc.RootElement.TryGetProperty("libraries", out var libs))
                arr = libs;
            else
                return list;

            foreach (var item in arr.EnumerateArray())
            {
                var lib = item.TryGetProperty("library", out var libObj) ? libObj : item;

                var info = new ArduinoLibraryInfo
                {
                    Name = GetStringProperty(lib, "name") ?? "",
                    Author = GetStringProperty(lib, "author") ?? "",
                    Maintainer = GetStringProperty(lib, "maintainer") ?? "",
                    Version = GetStringProperty(lib, "version") ?? "",
                    Sentence = GetStringProperty(lib, "sentence") ?? "",
                    Paragraph = GetStringProperty(lib, "paragraph") ?? "",
                    Website = GetStringProperty(lib, "website") ?? "",
                    Category = GetStringProperty(lib, "category") ?? "",
                    License = GetStringProperty(lib, "license") ?? "",
                    InstallDir = GetStringProperty(lib, "install_dir") ?? "",
                    Installed = true
                };

                info.Latest = info.Version;

                list.Add(info);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Parse installed libraries failed");
        }
        return list;
    }

    public List<ArduinoLibraryInfo> ParseSearchLibraries(CliResult result)
    {
        var list = new List<ArduinoLibraryInfo>();
        if (!result.Success || string.IsNullOrWhiteSpace(result.Output)) return list;

        try
        {
            using var doc = JsonDocument.Parse(result.Output);

            JsonElement arr;
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
                arr = doc.RootElement;
            else if (doc.RootElement.TryGetProperty("libraries", out var libs))
                arr = libs;
            else
                return list;

            foreach (var item in arr.EnumerateArray())
            {
                var info = new ArduinoLibraryInfo
                {
                    Name = GetStringProperty(item, "name") ?? "",
                    Author = GetStringProperty(item, "author") ?? "",
                    Maintainer = GetStringProperty(item, "maintainer") ?? "",
                    Sentence = GetStringProperty(item, "sentence") ?? "",
                    Paragraph = GetStringProperty(item, "paragraph") ?? "",
                    Website = GetStringProperty(item, "website") ?? "",
                    Category = GetStringProperty(item, "category") ?? "",
                    License = GetStringProperty(item, "license") ?? "",
                    Installed = false
                };

                if (item.TryGetProperty("latest", out var latest) && latest.ValueKind == JsonValueKind.Object)
                {
                    info.Latest = GetStringProperty(latest, "version") ?? "";
                    if (latest.TryGetProperty("releases", out var rel) && rel.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in rel.EnumerateObject())
                            info.Releases.Add(prop.Name);
                    }
                }

                if (info.Releases.Count == 0 && item.TryGetProperty("releases", out var rel2) && rel2.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in rel2.EnumerateObject())
                        info.Releases.Add(prop.Name);
                }

                info.Releases = info.Releases
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Distinct()
                    .OrderByDescending(v => ParseVersion(v))
                    .ToList();

                if (string.IsNullOrEmpty(info.Version))
                    info.Version = info.Latest;

                list.Add(info);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Parse search libraries failed");
        }
        return list;
    }

    public List<ArduinoLibraryInfo> LoadAllLibrariesFromIndex()
    {
        var list = new List<ArduinoLibraryInfo>();
        var indexPath = Path.Combine(Config.ArduinoCliDir, "data", "library_index.json");
        if (!File.Exists(indexPath)) return list;

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(indexPath));
            if (!doc.RootElement.TryGetProperty("libraries", out var libs) || libs.ValueKind != JsonValueKind.Array)
                return list;

            foreach (var item in libs.EnumerateArray())
            {
                var info = new ArduinoLibraryInfo
                {
                    Name = GetStringProperty(item, "name") ?? "",
                    Author = GetStringProperty(item, "author") ?? "",
                    Maintainer = GetStringProperty(item, "maintainer") ?? "",
                    Sentence = GetStringProperty(item, "sentence") ?? "",
                    Paragraph = GetStringProperty(item, "paragraph") ?? "",
                    Website = GetStringProperty(item, "website") ?? "",
                    Category = GetStringProperty(item, "category") ?? "",
                    License = GetStringProperty(item, "license") ?? "",
                    Installed = false
                };

                if (item.TryGetProperty("latest", out var latest) && latest.ValueKind == JsonValueKind.Object)
                {
                    info.Latest = GetStringProperty(latest, "version") ?? "";
                    info.Version = info.Latest;
                }

                if (item.TryGetProperty("releases", out var rel) && rel.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in rel.EnumerateObject())
                        info.Releases.Add(prop.Name);

                    info.Releases = info.Releases
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .Distinct()
                        .OrderByDescending(v => ParseVersion(v))
                        .ToList();
                }

                list.Add(info);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Load all libraries from index failed");
        }

        list = list
            .GroupBy(l => l.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(l => ParseVersion(l.Latest)).First())
            .ToList();

        return list;
    }

    public List<BoardConfigItem> ParseBoardConfigOptions(CliResult result)
    {
        var list = new List<BoardConfigItem>();
        if (!result.Success || string.IsNullOrWhiteSpace(result.Output)) return list;

        try
        {
            using var doc = JsonDocument.Parse(result.Output);
            if (!doc.RootElement.TryGetProperty("config_options", out var opts) || opts.ValueKind != JsonValueKind.Array)
                return list;

            foreach (var o in opts.EnumerateArray())
            {
                var item = new BoardConfigItem
                {
                    Key = o.TryGetProperty("option", out var k) ? k.GetString() ?? "" : "",
                    Label = o.TryGetProperty("option_label", out var l) ? l.GetString() ?? "" : ""
                };

                if (o.TryGetProperty("values", out var vals) && vals.ValueKind == JsonValueKind.Array)
                {
                    foreach (var v in vals.EnumerateArray())
                    {
                        item.Options.Add(new BoardConfigOption
                        {
                            Value = v.TryGetProperty("value", out var vv) ? vv.GetString() ?? "" : "",
                            Label = v.TryGetProperty("value_label", out var vl) ? vl.GetString() ?? "" : ""
                        });
                    }
                }

                var current = GetStringProperty(o, "value");
                if (!string.IsNullOrEmpty(current) && item.Options.Any(x => x.Value == current))
                    item.DefaultValue = current;
                else if (item.Options.Count > 0)
                    item.DefaultValue = item.Options[0].Value;

                if (item.Options.Count > 0)
                    list.Add(item);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Parse board config options failed");
        }
        return list;
    }

    public string? FindPackageUrl(string coreId, string version)
    {
        var parts = coreId.Split(':');
        if (parts.Length < 2) return null;

        var packageName = parts[0];
        var coreArch = parts[1];

        var dataDir = Path.Combine(Config.ArduinoCliDir, "data");
        if (!Directory.Exists(dataDir)) return null;

        foreach (var jsonFile in Directory.GetFiles(dataDir, "package_*_index.json"))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(jsonFile));
                if (!doc.RootElement.TryGetProperty("packages", out var packages) ||
                    packages.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var pkg in packages.EnumerateArray())
                {
                    var name = pkg.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    if (!string.Equals(name, packageName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!pkg.TryGetProperty("platforms", out var platforms) ||
                        platforms.ValueKind != JsonValueKind.Array)
                        continue;

                    foreach (var plat in platforms.EnumerateArray())
                    {
                        var arch = plat.TryGetProperty("architecture", out var a) ? a.GetString() ?? "" : "";
                        if (!string.Equals(arch, coreArch, StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (plat.TryGetProperty("releases", out var releases) &&
                            releases.ValueKind == JsonValueKind.Object)
                        {
                            if (releases.TryGetProperty(version, out var relUrl))
                            {
                                var u = relUrl.GetString();
                                if (!string.IsNullOrEmpty(u)) return u;
                            }
                        }

                        if (plat.TryGetProperty("releases", out var relArr) &&
                            relArr.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in relArr.EnumerateArray())
                            {
                                var ver = item.TryGetProperty("version", out var v) ? v.GetString() : null;
                                var url = item.TryGetProperty("url", out var u) ? u.GetString() : null;
                                if (ver == version && !string.IsNullOrEmpty(url))
                                    return url;
                            }
                        }

                        var curVer = plat.TryGetProperty("version", out var cv) ? cv.GetString() : null;
                        var curUrl = plat.TryGetProperty("url", out var cu) ? cu.GetString() : null;
                        if (curVer == version && !string.IsNullOrEmpty(curUrl))
                            return curUrl;
                    }
                }
            }
            catch
            {
            }
        }

        return null;
    }

    #region 合并 ESP32/ESP8266 固件为单个 bin

    public async Task<BuildFile?> MergeEspFirmwareAsync(
        string projectName,
        string fqbn,
        Dictionary<string, string>? boardConfig = null,
        CancellationToken ct = default)
    {
        var projectDir = Path.Combine(Config.ProjectsDir, projectName);
        var buildDir = Path.Combine(projectDir, "build");

        if (!Directory.Exists(buildDir)) return null;

        var isEsp32 = fqbn.StartsWith("esp32:", StringComparison.OrdinalIgnoreCase);
        var isEsp8266 = fqbn.StartsWith("esp8266:", StringComparison.OrdinalIgnoreCase);
        if (!isEsp32 && !isEsp8266) return null;

        var bootloader = Directory.GetFiles(buildDir, "*.ino.bootloader.bin").FirstOrDefault();
        var partitions = Directory.GetFiles(buildDir, "*.ino.partitions.bin").FirstOrDefault();
        var app = Directory.GetFiles(buildDir, "*.ino.bin")
            .FirstOrDefault(f => !f.EndsWith(".bootloader.bin", StringComparison.OrdinalIgnoreCase)
                              && !f.EndsWith(".partitions.bin", StringComparison.OrdinalIgnoreCase));

        if (bootloader == null || partitions == null || app == null)
        {
            _logger.LogWarning("Merge skipped: missing bootloader/partitions/app for {Project}", projectName);
            return null;
        }

        var bootApp0 = FindBootApp0(fqbn);
        if (bootApp0 == null)
        {
            _logger.LogWarning("Merge skipped: boot_app0.bin not found for {Fqbn}", fqbn);
            return null;
        }

        var esptool = FindEsptool(fqbn);
        if (esptool == null)
        {
            _logger.LogWarning("Merge skipped: esptool not found for {Fqbn}", fqbn);
            return null;
        }

        var mergedPath = Path.Combine(buildDir, $"{projectName}.merged.bin");
        if (File.Exists(mergedPath)) File.Delete(mergedPath);

        string GetCfg(string key, string def) =>
            boardConfig != null && boardConfig.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : def;

        var flashMode = GetCfg("FlashMode", "dio");
        var flashFreq = GetCfg("FlashFreq", "40");
        var flashSize = GetCfg("FlashSize", "4MB");

        var args = new StringBuilder();
        if (isEsp32)
            args.Append("--chip esp32 ");
        else
            args.Append("--chip esp8266 ");

        args.Append("merge_bin -o \"");
        args.Append(mergedPath);
        args.Append("\" --flash_mode ");
        args.Append(flashMode);
        args.Append(" --flash_freq ");
        args.Append(flashFreq);
        args.Append("m --flash_size ");
        args.Append(flashSize);
        args.Append(" ");

        if (isEsp32)
        {
            args.Append($"0x1000 \"{bootloader}\" ");
            args.Append($"0x8000 \"{partitions}\" ");
            args.Append($"0xe000 \"{bootApp0}\" ");
            args.Append($"0x10000 \"{app}\"");
        }
        else
        {
            args.Append($"0x0 \"{bootloader}\" ");
            args.Append($"0x1000 \"{partitions}\" ");
            args.Append($"0x2000 \"{app}\"");
        }

        var psi = new ProcessStartInfo
        {
            FileName = esptool,
            Arguments = args.ToString(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = buildDir
        };

        if (!OperatingSystem.IsWindows())
        {
            psi.Environment["HOME"] = Config.ArduinoCliDir;
            psi.Environment["USERPROFILE"] = Config.ArduinoCliDir;
        }

        try
        {
            using var process = new Process { StartInfo = psi };
            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(ct);
            var error = await process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0 || !File.Exists(mergedPath))
            {
                _logger.LogError("Merge failed: {Error}", error);
                return null;
            }

            return new BuildFile
            {
                FileName = Path.GetFileName(mergedPath),
                FullPath = mergedPath,
                Size = new FileInfo(mergedPath).Length
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Merge esp firmware failed");
            return null;
        }
    }

    private string? FindEsptool(string fqbn)
    {
        var dataDir = Path.Combine(Config.ArduinoCliDir, "data", "packages");
        if (!Directory.Exists(dataDir)) return null;

        var exeName = OperatingSystem.IsWindows() ? "esptool.exe" : "esptool.py";
        var candidates = Directory.GetFiles(dataDir, exeName, SearchOption.AllDirectories);
        return candidates.FirstOrDefault();
    }

    private string? FindBootApp0(string fqbn)
    {
        var parts = fqbn.Split(':');
        if (parts.Length < 2) return null;

        var packageName = parts[0];
        var dataDir = Path.Combine(Config.ArduinoCliDir, "data", "packages", packageName);
        if (!Directory.Exists(dataDir)) return null;

        var candidates = Directory.GetFiles(dataDir, "boot_app0.bin", SearchOption.AllDirectories);
        return candidates.FirstOrDefault();
    }

    #endregion

    public string GetLibraryInstallDir(string libName)
    {
        var libDir = Path.Combine(Config.ArduinoCliDir, "user", "libraries", libName);
        if (Directory.Exists(libDir)) return libDir;

        var dataLib = Path.Combine(Config.ArduinoCliDir, "data", "libraries", libName);
        if (Directory.Exists(dataLib)) return dataLib;

        return "";
    }

    public string GetCoreInstallDir(string fqbn)
    {
        var parts = fqbn.Split(':');
        if (parts.Length < 2) return "";

        var packageName = parts[0];
        var arch = parts[1];
        var hardwareDir = Path.Combine(Config.ArduinoCliDir, "data", "packages", packageName, "hardware", arch);

        if (!Directory.Exists(hardwareDir)) return "";

        var versionDir = Directory.GetDirectories(hardwareDir).OrderByDescending(d => d).FirstOrDefault();
        return versionDir ?? "";
    }

    public async Task<CliResult> GetCliVersionAsync(CancellationToken ct = default)
    => await RunAsync("version --format json", ct);



    public async Task<CliResult> UpdateIndexStreamAsync(
    Action<string>? onOutput,
    Action<string>? onError,
    CancellationToken ct = default)
    => await RunStreamAsync("core update-index", onOutput, onError, ct);

    public async Task<CliResult> InstallCoreStreamAsync(
        string coreId,
        string? version,
        Action<string>? onOutput,
        Action<string>? onError,
        CancellationToken ct = default)
    {
        var safeId = coreId.Replace("\"", "");
        var args = string.IsNullOrEmpty(version)
            ? $"core install {safeId}"
            : $"core install {safeId}@{version}";
        return await RunStreamAsync(args, onOutput, onError, ct);
    }

    public async Task<CliResult> UninstallCoreStreamAsync(
        string coreId,
        Action<string>? onOutput,
        Action<string>? onError,
        CancellationToken ct = default)
    {
        var safe = coreId.Replace("\"", "");
        return await RunStreamAsync($"core uninstall {safe}", onOutput, onError, ct);
    }

    public async Task<CliResult> SyncLibrariesStreamAsync(
    Action<string>? onOutput,
    Action<string>? onError,
    CancellationToken ct = default)
    {
        var update = await RunStreamAsync("lib update-index", onOutput, onError, ct);
        if (!update.Success) return update;

        return await RunStreamAsync("lib list --format json", onOutput, onError, ct);
    }

    public async Task<CliResult> InstallLibraryStreamAsync(
        string libName,
        string? version,
        Action<string>? onOutput,
        Action<string>? onError,
        CancellationToken ct = default)
    {
        var safe = libName.Replace("\"", "");
        var args = string.IsNullOrEmpty(version)
            ? $"lib install \"{safe}\""
            : $"lib install \"{safe}@{version}\"";
        return await RunStreamAsync(args, onOutput, onError, ct);
    }

    public async Task<CliResult> UninstallLibraryStreamAsync(
        string libName,
        Action<string>? onOutput,
        Action<string>? onError,
        CancellationToken ct = default)
    {
        var safe = libName.Replace("\"", "");
        return await RunStreamAsync($"lib uninstall \"{safe}\"", onOutput, onError, ct);
    }

    public async Task<CliResult> SearchLibrariesStreamAsync(
        string keyword,
        Action<string>? onOutput,
        Action<string>? onError,
        CancellationToken ct = default)
    {
        var safe = keyword.Replace("\"", "");
        return await RunStreamAsync($"lib search \"{safe}\" --format json", onOutput, onError, ct);
    }
}