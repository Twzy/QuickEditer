using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QuickEditer.Core;
using QuickEditer.Models;

namespace QuickEditer.Services
{
    public class ProjectTreeService
    {
        private readonly FileTreeService _fileSvc;
        private readonly ILogger<ProjectTreeService> _logger;

        public ProjectTreeService(FileTreeService fileSvc, ILogger<ProjectTreeService> logger)
        {
            _fileSvc = fileSvc;
            _logger = logger;
        }

        /// <summary>构建当前项目树：项目根 → 文件/文件夹（排除 build、logs、project.xml）</summary>
        public ProjectTreeNode BuildProjectTree(string projectName)
        {
            var rootPath = Path.Combine(Config.ProjectsDir, projectName);

            var root = new ProjectTreeNode
            {
                Name = projectName,
                FullPath = rootPath,
                NodeType = ProjectNodeType.ProjectRoot
            };

            if (!Directory.Exists(rootPath))
                return root;

            var fileTree = _fileSvc.BuildTree(projectName);
            AppendChildren(root, fileTree);
            return root;
        }

        private void AppendChildren(ProjectTreeNode parent, FileNode fileNode)
        {
            foreach (var child in fileNode.Children)
            {
                // 隐藏项目配置文件
                if (!child.IsDirectory &&
                    string.Equals(child.Name, "project.xml", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var node = new ProjectTreeNode
                {
                    Name = child.Name,
                    FullPath = child.FullPath,
                    NodeType = child.IsDirectory ? ProjectNodeType.Folder : ProjectNodeType.File
                };

                if (child.IsDirectory)
                    AppendChildren(node, child);

                parent.Children.Add(node);
            }
        }

        /// <summary>重命名文件或文件夹（项目根不可重命名）</summary>
        public (bool Ok, string Error) Rename(ProjectTreeNode node, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
                return (false, "名称不能为空");

            if (node.NodeType == ProjectNodeType.ProjectRoot)
                return (false, "不能重命名项目根节点");

            var invalid = Path.GetInvalidFileNameChars();
            if (newName.Any(c => invalid.Contains(c)))
                return (false, "名称包含非法字符");

            var parentDir = Path.GetDirectoryName(node.FullPath);
            if (string.IsNullOrEmpty(parentDir))
                return (false, "无法解析父目录");

            var target = Path.Combine(parentDir, newName.Trim());
            if (Directory.Exists(target) || File.Exists(target))
                return (false, "同名项已存在");

            try
            {
                if (node.NodeType == ProjectNodeType.Folder)
                    Directory.Move(node.FullPath, target);
                else
                    File.Move(node.FullPath, target);

                return (true, "");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Rename {Path} failed", node.FullPath);
                return (false, "重命名失败：" + ex.Message);
            }
        }

        public (bool Ok, string Error) Move(string sourcePath, string targetDir)
        {
            try
            {
                var name = Path.GetFileName(sourcePath);
                var dest = Path.Combine(targetDir, name);
                if (Directory.Exists(dest) || File.Exists(dest))
                    return (false, "目标已存在同名项");
                if (Directory.Exists(sourcePath))
                    Directory.Move(sourcePath, dest);
                else
                    File.Move(sourcePath, dest);
                return (true, "");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Move {Src} to {Dst} failed", sourcePath, targetDir);
                return (false, ex.Message);
            }
        }
    }
}