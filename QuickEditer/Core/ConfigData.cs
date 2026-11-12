using System.Collections.Generic;
using System.Xml.Serialization;

namespace QuickEditer.Core
{
    public class ConfigData
    {
        public string DefaultFqbn { get; set; } = "arduino:avr:uno";
        public string ArduinoCliPath { get; set; } = "";
        public string LastProject { get; set; } = "";
        public string AdditionalUrls { get; set; } = "";

        public int EditorFontSize { get; set; } = 14;
        public string AppTheme { get; set; } = "dark";
        public bool EditorMinimap { get; set; } = true;
        public bool EditorWordWrap { get; set; } = false;

        public bool EditorStickyScroll { get; set; } = false;
        public string LastExampleName { get; set; } = "";

        [XmlArray("RecentProjects")]
        [XmlArrayItem("Project")]
        public List<string> RecentProjects { get; set; } = new();

        [XmlArray("BoardConfigs")]
        [XmlArrayItem("Board")]
        public List<BoardConfigEntry> BoardConfigs { get; set; } = new();

        public int HttpPort { get; set; } = 58631;


        public bool OutputPanelVisible { get; set; } = true;
        public bool SerialPanelVisible { get; set; } = false;

    }

    public class BoardConfigEntry
    {
        [XmlAttribute]
        public string Fqbn { get; set; } = "";

        [XmlArray("Items")]
        [XmlArrayItem("Item")]
        public List<BoardConfigValue> Items { get; set; } = new();
    }

    public class BoardConfigValue
    {
        [XmlAttribute]
        public string Key { get; set; } = "";

        [XmlText]
        public string Value { get; set; } = "";
    }
}