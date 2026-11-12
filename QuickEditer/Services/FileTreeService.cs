using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QuickEditer.Core;

namespace QuickEditer.Services
{
    /// <summary>文件树节点</summary>
    public class FileNode
    {
        public string Name { get; set; } = "";
        public string FullPath { get; set; } = "";
        public bool IsDirectory { get; set; }
        public List<FileNode> Children { get; set; } = new();
    }

    public class FileTreeService
    {
        private readonly ILogger<FileTreeService> _logger;

        public FileTreeService(ILogger<FileTreeService> logger)
        {
            _logger = logger;
        }

        /// <summary>获取项目根目录</summary>
        public string GetProjectRoot(string projectName) =>
            Path.Combine(Config.ProjectsDir, projectName);

        /// <summary>递归构建项目文件树（排除 build 目录）</summary>
        public FileNode BuildTree(string projectName, params string[] excludeDirs)
        {
            var root = new FileNode
            {
                Name = projectName,
                FullPath = GetProjectRoot(projectName),
                IsDirectory = true
            };

            if (!Directory.Exists(root.FullPath))
                return root;

            var excludes = new HashSet<string>(excludeDirs ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            FillChildren(root, excludes);
            return root;
        }

        private void FillChildren(FileNode node, HashSet<string> excludeDirs)
        {
            // 目录在前，文件在后，各自按名称排序
            foreach (var dir in Directory.GetDirectories(node.FullPath).OrderBy(d => d))
            {
                var dirName = Path.GetFileName(dir);
                if (excludeDirs.Contains(dirName)) continue;

                var child = new FileNode
                {
                    Name = dirName,
                    FullPath = dir,
                    IsDirectory = true
                };
                FillChildren(child, excludeDirs);
                node.Children.Add(child);
            }

            foreach (var file in Directory.GetFiles(node.FullPath).OrderBy(f => f))
            {
                node.Children.Add(new FileNode
                {
                    Name = Path.GetFileName(file),
                    FullPath = file,
                    IsDirectory = false
                });
            }
        }

        /// <summary>在指定目录下新建文件夹</summary>
        public (bool Ok, string Error) CreateFolder(string parentDir, string folderName)
        {
            if (string.IsNullOrWhiteSpace(folderName))
                return (false, "文件夹名不能为空");

            var invalid = Path.GetInvalidFileNameChars();
            if (folderName.Any(c => invalid.Contains(c)))
                return (false, "文件夹名包含非法字符");

            var target = Path.Combine(parentDir, folderName.Trim());
            if (Directory.Exists(target))
                return (false, "同名文件夹已存在");

            try
            {
                Directory.CreateDirectory(target);
                return (true, "");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Create folder {Dir} failed", target);
                return (false, "创建失败：" + ex.Message);
            }
        }

        /// <summary>在指定目录下新建文件</summary>
        public (bool Ok, string Error) CreateFile(string parentDir, string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return (false, "文件名不能为空");

            var invalid = Path.GetInvalidFileNameChars();
            if (fileName.Any(c => invalid.Contains(c)))
                return (false, "文件名包含非法字符");

            var target = Path.Combine(parentDir, fileName.Trim());
            if (File.Exists(target))
                return (false, "同名文件已存在");

            try
            {
                File.WriteAllText(target, "");
                return (true, "");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Create file {File} failed", target);
                return (false, "创建失败：" + ex.Message);
            }
        }

        /// <summary>读取文件内容</summary>
        public string ReadFile(string fullPath)
        {
            return File.Exists(fullPath) ? File.ReadAllText(fullPath) : "";
        }

        /// <summary>写入文件内容</summary>
        public void SaveFile(string fullPath, string content)
        {
            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(fullPath, content);
        }

        /// <summary>删除文件或目录</summary>
        public (bool Ok, string Error) Delete(string fullPath)
        {
            try
            {
                if (Directory.Exists(fullPath))
                    Directory.Delete(fullPath, true);
                else if (File.Exists(fullPath))
                    File.Delete(fullPath);
                return (true, "");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Delete {Path} failed", fullPath);
                return (false, "删除失败：" + ex.Message);
            }
        }
    }
}