namespace OpenAIAPI.Models.Partial
{
    public class CreateFolderModal
    {
        public string Name { get; set; }
        public string? ParentId { get; set; }
    }

    public class ViewNodeModel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string? ParentId { get; set; }
        public string NodeType { get; set; }
        public List<ViewNodeModel> Children { get; set; } = new List<ViewNodeModel>();
        //public List<ViewFolderModel> SubFolders { get; set; } = new List<ViewFolderModel>();
        //public List<ViewTextModel> SubTexts { get; set; } = new List<ViewTextModel>();
    }

    public class RenameFolderModal
    {
        public string NewName { get; set; }
    }
}
