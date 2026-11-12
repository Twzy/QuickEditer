namespace QuickEditer.Models
{
    public class EditorTabInfo
    {
        public string FilePath { get; set; } = "";
        public string FileName { get; set; } = "";
        public string Content { get; set; } = "";
        public string OriginalContent { get; set; } = "";
        public bool IsReadOnly { get; set; }
        public bool IsDirty => !IsReadOnly && Content != OriginalContent;

        public int ScrollTop { get; set; }
        public bool IsImage =>
    !string.IsNullOrEmpty(FilePath) &&
    Path.GetExtension(FilePath).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".svg" or ".webp";
        public void MarkSaved()
        {
            OriginalContent = Content;
        }
    }
}