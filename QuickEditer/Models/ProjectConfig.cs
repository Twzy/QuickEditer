using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace QuickEditer.Models
{
    [XmlRoot("Project")]
    public class ProjectConfig
    {
        [XmlAttribute]
        public string Name { get; set; } = "Untitled";

        public string BoardFqbn { get; set; } = "arduino:avr:uno";

        /// <summary>主 sketch 文件名（相对项目根，Arduino 要求与项目文件夹同名）</summary>
        public string MainSketchFile { get; set; } = "sketch.ino";

        [XmlArray("Libraries")]
        [XmlArrayItem("Library")]
        public List<string> Libraries { get; set; } = new();

        public DateTime LastModified { get; set; } = DateTime.Now;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string Description { get; set; } = "";

        /// <summary>项目级开发板配置（覆盖全局）</summary>
        [XmlArray("BoardConfigs")]
        [XmlArrayItem("Item")]
        public List<BoardConfigPair> BoardConfigs { get; set; } = new();
    }

    public class BoardConfigPair
    {
        [XmlAttribute]
        public string Key { get; set; } = "";

        [XmlText]
        public string Value { get; set; } = "";
    }
}