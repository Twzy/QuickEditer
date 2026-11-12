using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QuickEditer.Core;
using QuickEditer.Models;

namespace QuickEditer.Services
{
    public class ExampleService
    {
        private readonly ArduinoCliService _cli;
        private readonly ILogger<ExampleService> _logger;

        public ExampleService(ArduinoCliService cli, ILogger<ExampleService> logger)
        {
            _cli = cli;
            _logger = logger;
        }

        /// <summary>扫描核心/库安装目录下的 examples 文件夹，返回示例根目录列表</summary>
        public List<ExampleInfo> ListExamples(string installDir)
        {
            var list = new List<ExampleInfo>();
            if (string.IsNullOrEmpty(installDir) || !Directory.Exists(installDir)) return list;

            // 常规：installDir/examples
            var examplesDir = Path.Combine(installDir, "examples");
            if (Directory.Exists(examplesDir))
            {
                CollectExamples(examplesDir, "", list);
                return list;
            }

            // 核心：installDir/libraries/*/examples
            var librariesDir = Path.Combine(installDir, "libraries");
            if (Directory.Exists(librariesDir))
            {
                foreach (var libDir in Directory.GetDirectories(librariesDir))
                {
                    var libExamples = Path.Combine(libDir, "examples");
                    if (Directory.Exists(libExamples))
                    {
                        CollectExamples(libExamples, Path.GetFileName(libDir), list);
                    }
                }
            }

            return list;
        }

        private static void CollectExamples(string examplesDir, string prefix, List<ExampleInfo> list)
        {
            foreach (var dir in Directory.GetDirectories(examplesDir))
            {
                var mainIno = FindMainIno(dir);
                if (mainIno == null) continue;

                list.Add(new ExampleInfo
                {
                    Name = string.IsNullOrEmpty(prefix) ? Path.GetFileName(dir) : $"{prefix}/{Path.GetFileName(dir)}",
                    Directory = dir,
                    MainInoPath = mainIno
                });
            }
        }

        /// <summary>递归构建示例文件树，根节点为示例文件夹</summary>
        public ProjectTreeNode BuildExampleTree(ExampleInfo example)
        {
            var root = new ProjectTreeNode
            {
                Name = example.Name,
                FullPath = example.Directory,
                NodeType = ProjectNodeType.Folder
            };

            if (!Directory.Exists(example.Directory))
                return root;

            FillChildren(root);
            return root;
        }

        private void FillChildren(ProjectTreeNode node)
        {
            try
            {
                foreach (var dir in Directory.GetDirectories(node.FullPath).OrderBy(d => d))
                {
                    var child = new ProjectTreeNode
                    {
                        Name = Path.GetFileName(dir),
                        FullPath = dir,
                        NodeType = ProjectNodeType.Folder
                    };
                    FillChildren(child);
                    node.Children.Add(child);
                }

                foreach (var file in Directory.GetFiles(node.FullPath).OrderBy(f => f))
                {
                    node.Children.Add(new ProjectTreeNode
                    {
                        Name = Path.GetFileName(file),
                        FullPath = file,
                        NodeType = ProjectNodeType.File
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Build example tree failed: {Dir}", node.FullPath);
            }
        }

        /// <summary>找示例主 .ino（与所在文件夹同名）</summary>
        private static string? FindMainIno(string dir)
        {
            var expected = Path.Combine(dir, $"{Path.GetFileName(dir)}.ino");
            if (File.Exists(expected)) return expected;

            return Directory.GetFiles(dir, "*.ino").FirstOrDefault();
        }
    }

    public class ExampleInfo
    {
        public string Name { get; set; } = "";
        public string Directory { get; set; } = "";
        public string MainInoPath { get; set; } = "";
    }
}