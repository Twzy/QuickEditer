using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using QuickEditer.Core;
using QuickEditer.Models;

namespace QuickEditer.Services
{
    public class ProjectService
    {
        private readonly ILogger<ProjectService> _logger;

        public ProjectService(ILogger<ProjectService> logger)
        {
            _logger = logger;
        }

        public List<string> ListProjects()
        {
            if (!Directory.Exists(Config.ProjectsDir))
                return new List<string>();

            return Directory.GetDirectories(Config.ProjectsDir)
                .Select(Path.GetFileName)
                .Where(n => n != null)
                .Cast<string>()
                .OrderBy(n => n)
                .ToList();
        }

        public (bool Ok, string Error) ValidateProjectName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return (false, "项目名称不能为空");

            name = name.Trim();

            if (name.Length > 64)
                return (false, "项目名称不能超过 64 个字符");

            var invalid = Path.GetInvalidFileNameChars();
            if (name.Any(c => invalid.Contains(c)))
                return (false, "项目名称包含非法字符");

            if (name.StartsWith("."))
                return (false, "项目名称不能以点开头");

            var dir = Path.Combine(Config.ProjectsDir, name);
            if (Directory.Exists(dir))
                return (false, $"项目「{name}」已存在");

            return (true, "");
        }

        public ProjectConfig? CreateProject(string name, string boardFqbn, string description, string templateId, out string error)
        {
            error = "";
            var (ok, err) = ValidateProjectName(name);
            if (!ok)
            {
                error = err;
                return null;
            }

            name = name.Trim();
            var projectDir = Path.Combine(Config.ProjectsDir, name);
            var buildDir = Path.Combine(projectDir, "build");
            var logsDir = Path.Combine(projectDir, "logs");

            try
            {
                Directory.CreateDirectory(projectDir);
                Directory.CreateDirectory(buildDir);
                Directory.CreateDirectory(logsDir);

                var config = new ProjectConfig
                {
                    Name = name,
                    BoardFqbn = string.IsNullOrWhiteSpace(boardFqbn) ? Config.Cfg.DefaultFqbn : boardFqbn,
                    MainSketchFile = $"{name}.ino",
                    Description = description ?? "",
                    CreatedAt = DateTime.Now,
                    LastModified = DateTime.Now
                };

                var sketchPath = Path.Combine(projectDir, config.MainSketchFile);
                File.WriteAllText(sketchPath, BuildSketchFromTemplate(name, templateId), Encoding.UTF8);

                Tools.WriteByXML(config, GetConfigPath(name));

                Config.Cfg.LastProject = name;
                Config.AddRecentProject(name);

                _logger.LogInformation("Created project {Name} with FQBN {Fqbn}", name, config.BoardFqbn);
                return config;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Create project {Name} failed", name);
                error = "创建项目失败：" + ex.Message;
                try
                {
                    if (Directory.Exists(projectDir))
                        Directory.Delete(projectDir, true);
                }
                catch { }
                return null;
            }
        }

        public ProjectConfig? LoadProject(string name)
        {
            var cfgPath = GetConfigPath(name);
            if (!File.Exists(cfgPath)) return null;
            try
            {
                return Tools.ReadByXML<ProjectConfig>(cfgPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Load project {Name} failed", name);
                return null;
            }
        }

        public void SaveConfig(ProjectConfig config)
        {
            config.LastModified = DateTime.Now;
            Tools.WriteByXML(config, GetConfigPath(config.Name));
        }

        public bool DeleteProject(string name, out string error)
        {
            error = "";
            var dir = Path.Combine(Config.ProjectsDir, name);
            if (!Directory.Exists(dir))
            {
                error = "项目不存在";
                return false;
            }
            try
            {
                Directory.Delete(dir, true);
                return true;
            }
            catch (Exception ex)
            {
                error = "删除失败：" + ex.Message;
                return false;
            }
        }

        public string ReadSketch(string projectName, string fileName)
        {
            var path = Path.Combine(Config.ProjectsDir, projectName, fileName);
            return File.Exists(path) ? File.ReadAllText(path) : "";
        }

        public void SaveSketch(string projectName, string fileName, string content)
        {
            var dir = Path.Combine(Config.ProjectsDir, projectName);
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, fileName);
            File.WriteAllText(path, content, Encoding.UTF8);
        }

        public string GetProjectDir(string name) => Path.Combine(Config.ProjectsDir, name);

        public string GetConfigPath(string name) =>
            Path.Combine(Config.ProjectsDir, name, "project.xml");

        public string ExportProjectZip(string name)
        {
            var projectDir = GetProjectDir(name);
            if (!Directory.Exists(projectDir)) return "";

            var exportDir = Path.Combine(Config.BaseDir, "exports");
            Directory.CreateDirectory(exportDir);

            var zipPath = Path.Combine(exportDir, $"{name}_{DateTime.Now:yyyyMMddHHmmss}.zip");
            try
            {
                if (File.Exists(zipPath)) File.Delete(zipPath);
                ZipFile.CreateFromDirectory(projectDir, zipPath);
                return zipPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Export project {Name} failed", name);
                return "";
            }
        }

        public (bool Ok, string Error) ImportProjectZip(string zipPath, string newName)
        {
            if (!File.Exists(zipPath)) return (false, "文件不存在");
            var (ok, err) = ValidateProjectName(newName);
            if (!ok) return (false, err);

            var targetDir = Path.Combine(Config.ProjectsDir, newName.Trim());
            try
            {
                Directory.CreateDirectory(targetDir);
                ZipFile.ExtractToDirectory(zipPath, targetDir);

                var cfgPath = Path.Combine(targetDir, "project.xml");
                if (File.Exists(cfgPath))
                {
                    var cfg = Tools.ReadByXML<ProjectConfig>(cfgPath);
                    cfg.Name = newName.Trim();
                    var oldIno = Path.Combine(targetDir, cfg.MainSketchFile);
                    cfg.MainSketchFile = $"{newName.Trim()}.ino";
                    var newIno = Path.Combine(targetDir, cfg.MainSketchFile);
                    if (File.Exists(oldIno) && !File.Exists(newIno))
                        File.Move(oldIno, newIno);
                    Tools.WriteByXML(cfg, cfgPath);
                }

                return (true, "");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Import project {Name} failed", newName);
                return (false, ex.Message);
            }
        }

        public (bool Ok, string Error) RenameProject(string oldName, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName)) return (false, "名称不能为空");
            if (string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
                return (false, "名称未变化");

            var oldDir = GetProjectDir(oldName);
            var newDir = GetProjectDir(newName.Trim());
            if (!Directory.Exists(oldDir)) return (false, "项目不存在");
            if (Directory.Exists(newDir)) return (false, "同名项目已存在");

            try
            {
                var cfg = LoadProject(oldName);
                if (cfg == null) return (false, "项目配置读取失败");

                Directory.Move(oldDir, newDir);

                cfg.Name = newName.Trim();
                var oldIno = Path.Combine(newDir, cfg.MainSketchFile);
                cfg.MainSketchFile = $"{newName.Trim()}.ino";
                var newIno = Path.Combine(newDir, cfg.MainSketchFile);
                if (File.Exists(oldIno) && !File.Exists(newIno))
                    File.Move(oldIno, newIno);

                Tools.WriteByXML(cfg, Path.Combine(newDir, "project.xml"));

                if (Config.Cfg.LastProject == oldName)
                {
                    Config.Cfg.LastProject = newName.Trim();
                    Config.ConfigAll();
                }

                return (true, "");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Rename project {Old} to {New} failed", oldName, newName);
                return (false, ex.Message);
            }
        }

        private static string BuildSketchFromTemplate(string projectName, string templateId)
        {
            var tpl = ProjectTemplate.BuiltIn.FirstOrDefault(t => t.Id == templateId)
                      ?? ProjectTemplate.BuiltIn[0];
            return tpl.Code.Replace("{projectName}", projectName);
        }


        /// <summary>清理项目 logs 目录，保留最近 keepCount 个文件</summary>
        public int CleanLogs(string projectName, int keepCount = 20)
        {
            var logsDir = Path.Combine(GetProjectDir(projectName), "logs");
            if (!Directory.Exists(logsDir)) return 0;

            var files = Directory.GetFiles(logsDir, "*.log");
            var removed = 0;
            foreach (var f in files)
            {
                try
                {
                    File.Delete(f);
                    removed++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "删除日志失败: {Path}", f);
                }
            }
            return removed;
        }

        /// <summary>清空项目 build 目录</summary>
        public (bool Ok, string Error) CleanBuild(string projectName)
        {
            var buildDir = Path.Combine(GetProjectDir(projectName), "build");
            if (!Directory.Exists(buildDir)) return (true, "");

            try
            {
                Directory.Delete(buildDir, true);
                Directory.CreateDirectory(buildDir);
                return (true, "");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }
    }
}