// 文件路径：Models/CoreInstallLogEntry.cs
using System;

namespace QuickEditer.Models
{
    public class CoreInstallLogEntry
    {
        public DateTime Time { get; set; } = DateTime.Now;
        public string CoreId { get; set; } = "";
        public string Version { get; set; } = "";
        public string Action { get; set; } = "install"; // install / uninstall / update / index / refresh / lib-install / lib-uninstall / lib-update / lib-index
        public bool Success { get; set; }
        public bool IsRunning { get; set; }
        public string Stage { get; set; } = "";
        public string Message { get; set; } = "";
        public string RawOutput { get; set; } = "";
        public string RawError { get; set; } = "";
        public string SuggestedUrl { get; set; } = "";

        public string DisplayTime => Time.ToString("HH:mm:ss");

        public string Title => Action switch
        {
            "lib-install" => string.IsNullOrEmpty(Version)
                ? $"安装库 {CoreId}"
                : $"安装库 {CoreId}@{Version}",
            "lib-uninstall" => string.IsNullOrEmpty(Version)
                ? $"卸载库 {CoreId}"
                : $"卸载库 {CoreId}@{Version}",
            "lib-update" => string.IsNullOrEmpty(Version)
                ? $"更新库 {CoreId}"
                : $"更新库 {CoreId} → {Version}",
            "lib-index" => "lib update-index",
            "lib-refresh" => "刷新库列表",
            "uninstall" => string.IsNullOrEmpty(Version)
                ? $"卸载 {CoreId}"
                : $"卸载 {CoreId}@{Version}",
            "update" => string.IsNullOrEmpty(Version)
                ? $"更新 {CoreId}"
                : $"更新 {CoreId} → {Version}",
            _ => string.IsNullOrEmpty(Version)
                ? $"{CoreId}"
                : $"{CoreId}@{Version}"
        };
    }
}