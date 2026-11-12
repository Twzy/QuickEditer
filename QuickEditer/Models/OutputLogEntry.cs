// 文件路径：Models/OutputLogEntry.cs
using System;
using System.Collections.Generic;

namespace QuickEditer.Models
{
    public enum OutputLogLevel
    {
        Info,
        Success,
        Warning,
        Error
    }

    public class OutputLogEntry
    {
        public DateTime Time { get; set; } = DateTime.Now;
        public OutputLogLevel Level { get; set; } = OutputLogLevel.Info;
        public string Text { get; set; } = "";

        public List<BuildFile>? Artifacts { get; set; }

        public string DisplayTime => Time.ToString("HH:mm:ss");

        public string LevelLabel => Level switch
        {
            OutputLogLevel.Success => "OK",
            OutputLogLevel.Warning => "WARN",
            OutputLogLevel.Error => "ERR",
            _ => "INFO"
        };
    }
}