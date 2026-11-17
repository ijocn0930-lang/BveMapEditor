namespace BveMapEditor
{
    internal class FileSystemNode
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public bool IsDirectory { get; set; }
        public List<FileSystemNode> Children { get; set; } = new List<FileSystemNode>();
        // ★ (2026-11-17) フォルダの中（Children）を読み込んだか。false のフォルダは、ツリーで開いたときに中を読む
        public bool IsLoaded { get; set; }

        public FileSystemNode(string name, string fullPath, bool isDirectory)
        {
            Name = name;
            FullPath = fullPath;
            IsDirectory = isDirectory;
        }
    }
}
