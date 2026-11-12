namespace QuickEditer.Models
{
    public class BoardConfigItem
    {
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
        public string Value { get; set; } = "";
        public string DefaultValue { get; set; } = "";
        public List<BoardConfigOption> Options { get; set; } = new();
    }

    public class BoardConfigOption
    {
        public string Value { get; set; } = "";
        public string Label { get; set; } = "";
    }
}