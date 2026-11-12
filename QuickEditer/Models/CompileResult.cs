namespace QuickEditer.Models
{
    public class CompileResult
    {
        public bool Success { get; set; }
        public string? BuildDir { get; set; }
        public List<BuildFile> OutputFiles { get; set; } = new();
        public string RawOutput { get; set; } = "";
        public string Error { get; set; } = "";
    }
}
