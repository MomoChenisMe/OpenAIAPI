namespace OpenAIAPI.Models.Partial
{
    public class CreateTextModal
    {
        public string Name { get; set; }
        public string? FolderId { get; set; }
        public string TextContent { get; set; }
        public string TextHtml { get; set; }
    }

    public class RenameTextModal
    {
        public string NewName { get; set; }
    }

    public class ViewTextModel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string FolderId { get; set; }
        public string TextContent { get; set; }
        public string TextHtml { get; set; }
        //public DateTime? CreatedAt { get; set; }
        //public DateTime? UpdatedAt { get; set; }
    }

    public class UpdateTextModel
    {
        public string TextContent { get; set; }
        public string TextHtml { get; set; }
    }

}
