// 文件路径：Models/ArduinoBoardInfo.cs（完整）
using System.Collections.Generic;

namespace QuickEditer.Models
{
    public class ArduinoBoardInfo
    {
        public string Name { get; set; } = "";
        public string Fqbn { get; set; } = "";
        public string Platform { get; set; } = "";
        public bool IsBuiltin { get; set; }
    }

    public class ArduinoCoreInfo
    {
        public string Id { get; set; } = "";
        public string Installed { get; set; } = "";
        public string Latest { get; set; } = "";
        public string Name { get; set; } = "";
        public string Maintainer { get; set; } = "";
        public string Website { get; set; } = "";
        public string Email { get; set; } = "";
        public string Source { get; set; } = "official";
        public List<string> Releases { get; set; } = new();

        /// <summary>该核心已安装版本下的开发板</summary>
        public List<ArduinoBoardInfo> Boards { get; set; } = new();
    }

    public class ArduinoLibraryInfo
    {
        public string Name { get; set; } = "";
        public string Author { get; set; } = "";
        public string Maintainer { get; set; } = "";
        public string Version { get; set; } = "";
        public string Latest { get; set; } = "";
        public string Sentence { get; set; } = "";
        public string Paragraph { get; set; } = "";
        public string Website { get; set; } = "";
        public string Category { get; set; } = "";
        public string License { get; set; } = "";
        public bool Installed { get; set; }
        public List<string> Releases { get; set; } = new();

        /// <summary>已安装时本地路径</summary>
        public string InstallDir { get; set; } = "";
    }
}