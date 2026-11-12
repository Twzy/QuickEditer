namespace QuickEditer.Models
{
    public class CliResult
    {
        public int ExitCode { get; set; }
        public string Output { get; set; } = "";
        public string Error { get; set; } = "";
        public bool Success => ExitCode == 0;
    }
}
