using System.Collections.Generic;

namespace QuickEditer.Models
{
    /// <summary>项目资源管理器节点（项目根 + 文件/文件夹）</summary>
    public class ProjectTreeNode
    {
        public string Name { get; set; } = "";
        public string FullPath { get; set; } = "";
        public ProjectNodeType NodeType { get; set; }
        public List<ProjectTreeNode> Children { get; set; } = new();
    }

    public enum ProjectNodeType
    {
        ProjectRoot,
        Folder,
        File
    }
}