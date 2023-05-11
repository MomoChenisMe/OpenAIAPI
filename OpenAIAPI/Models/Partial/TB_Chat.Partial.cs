namespace OpenAIAPI.Models.Partial
{
    public class ChatroomModel
    {
        public string ChatroomID { get; set; }
        public string ChatName { get; set; }
        public List<ChatGPTMessageModel> Content { get; set; }
    }

    public class ChatroomCreateModel
    {
        public string AccountEmail { get; set; }
        public string ChatName { get; set; }
        public List<ChatGPTMessageModel> Content { get; set; }
    }

}
