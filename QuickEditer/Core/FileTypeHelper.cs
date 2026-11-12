using System;
using System.Collections.Generic;
using System.IO;

namespace QuickEditer.Core
{
    public static class FileTypeHelper
    {
        private static readonly HashSet<string> TextExts = new(StringComparer.OrdinalIgnoreCase)
        {
            ".ino", ".cpp", ".c", ".cc", ".cxx", ".h", ".hpp", ".hxx",
            ".txt", ".md", ".json", ".xml", ".yaml", ".yml", ".csv",
            ".log", ".ini", ".cfg", ".conf", ".properties",
            ".html", ".htm", ".css", ".js", ".ts", ".jsx", ".tsx",
            ".py", ".java", ".cs", ".vb", ".sh", ".bat", ".ps1",
            ".sql", ".gitignore", ".editorconfig"
        };

        private static readonly HashSet<string> ImageExts = new(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".svg", ".webp", ".ico"
        };

        private static readonly HashSet<string> FirmwareExts = new(StringComparer.OrdinalIgnoreCase)
        {
            ".bin", ".hex", ".uf2", ".eep"
        };

        private static readonly HashSet<string> DataExts = new(StringComparer.OrdinalIgnoreCase)
        {
            ".json", ".xml", ".yaml", ".yml"
        };

        private static readonly HashSet<string> TextDocExts = new(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".md", ".log", ".csv"
        };

        public static string GetExt(string path) =>
            string.IsNullOrEmpty(path) ? "" : Path.GetExtension(path).ToLowerInvariant();

        public static bool IsText(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            var ext = GetExt(path);
            if (!string.IsNullOrEmpty(ext)) return TextExts.Contains(ext);
            return TextExts.Contains(Path.GetFileName(path));
        }

        public static bool IsImage(string path) =>
            !string.IsNullOrEmpty(path) && ImageExts.Contains(GetExt(path));

        public static bool IsFirmware(string path) =>
            !string.IsNullOrEmpty(path) && FirmwareExts.Contains(GetExt(path));

        public static bool IsData(string path) =>
            !string.IsNullOrEmpty(path) && DataExts.Contains(GetExt(path));

        /// <summary>Monaco 语言标识</summary>
        public static string GetLanguage(string path)
        {
            if (string.IsNullOrEmpty(path)) return "plaintext";

            return GetExt(path) switch
            {
                ".ino" or ".cpp" or ".c" or ".h" or ".hpp" or ".cc" or ".cxx" => "cpp",
                ".json" => "json",
                ".xml" => "xml",
                ".html" or ".htm" => "html",
                ".css" => "css",
                ".js" => "javascript",
                ".ts" => "typescript",
                ".md" => "markdown",
                ".yaml" or ".yml" => "yaml",
                ".py" => "python",
                ".cs" => "csharp",
                _ => "plaintext"
            };
        }

        /// <summary>文件树图标类</summary>
        public static string GetIconClass(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return "qe-ico-file";

            var ext = GetExt(fileName);
            if (ext is ".ino" or ".cpp" or ".c" or ".h" or ".hpp" or ".cc" or ".cxx")
                return "qe-ico-file-code";
            if (TextDocExts.Contains(ext))
                return "qe-ico-file-text";
            if (DataExts.Contains(ext))
                return "qe-ico-file-data";
            if (ImageExts.Contains(ext))
                return "qe-ico-file-image";
            if (FirmwareExts.Contains(ext))
                return "qe-ico-firmware";
            return "qe-ico-file";
        }

        /// <summary>判断是否项目主草图（项目根下的 {项目名}.ino）</summary>
        public static bool IsMainSketch(string filePath, string projectName)
        {
            if (string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(projectName))
                return false;

            var parent = Path.GetDirectoryName(filePath);
            if (string.IsNullOrEmpty(parent)) return false;

            // 主草图在项目根，父目录名即项目名
            if (!string.Equals(Path.GetFileName(parent), projectName, StringComparison.OrdinalIgnoreCase))
                return false;

            return string.Equals(Path.GetFileName(filePath), $"{projectName}.ino", StringComparison.OrdinalIgnoreCase);
        }
    }
}