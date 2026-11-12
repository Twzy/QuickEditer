namespace QuickEditer.Models
{
    public class AppSettings
    {
        public string DefaultFqbn { get; set; } = "";
        public int EditorFontSize { get; set; } = 14;
        public string AppTheme { get; set; } = "dark";
        public bool EditorMinimap { get; set; } = true;
        public bool EditorWordWrap { get; set; }
        public bool EditorStickyScroll { get; set; }

        public int HttpPort { get; set; }
    }
}