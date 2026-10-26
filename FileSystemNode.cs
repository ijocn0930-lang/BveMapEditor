namespace BveMapEditor
{
    internal class FileSystemNode
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public bool IsDirectory { get; set; }
        public List<FileSystemNode> Children { get; set; } = new List<FileSystemNode>();

        public FileSystemNode(string name, string fullPath, bool isDirectory)
        {
            Name = name;
            FullPath = fullPath;
            IsDirectory = isDirectory;
        }
    }
}
