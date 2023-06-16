using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace OpenAIAPI.Models.Partial
{
    public enum QAType
    {
        Text,
        BugQA
    }

    public class InputTextModel
    {
        public string text { get; set; }
    }

    public class QAModel
    {
        public QAType qAType { get; set; } = QAType.Text;
        public string question { get; set; }
    }

    public class TokenCountModel
    {
        public int tokens { get; set; }
    }

    public class EmbeddingRequestModel
    {
        public string input { get; set; }
        public string model { get; set; }
    }

    public class ChatGPTRequestModel
    {
        public List<ChatGPTMessageModel> messages { get; set; }
        public string model { get; set; }
        public double temperature { get; set; }
        public int max_tokens { get; set; }
        public int top_p { get; set; }
        public bool stream { get; set; }
    }

    public class ChatGPTFunctionCallRequestModel : ChatGPTRequestModel
    {
        public List<FunctionCallModel> functions { get; set; }
        public dynamic function_call { get; set; }
    }

    public class ChatGPTMessageModel
    {
        public string role { get; set; }
        public string content { get; set; }
    }


    public class ChatGPTStreamModel
    {
        public string text { get; set; }
    }

    public class EmbeddingGenerateModel
    {
        public string prompt { get; set; }

        public string sourcePrompt { get; set; }
    }

    public class UsingTextModel
    {
        public string textGuid { get; set; }

        public string textName { get; set; }
    }

    public class QAGenerateModel
    {
        public string prompt { get; set; }

        public List<UsingTextModel> usingText { get; set; }
    }

    public class FunctionCallModel
    {
        public string name { get; set; }
        public string description { get; set; }
        public ParamterModel parameters { get; set; }
    }

    public class ParamterModel
    {
        public string type { get; set; }
        public Dictionary<string, PropertyModel> properties { get; set; }
        public List<string>? required { get; set; }
    }

    public class PropertyModel
    {
        public string type { get; set; }
        public string? description { get; set; }
        public ParamterModel items { get; set; }
    }

    public class QAFunctionCallResponseModel
    {
        public List<UsingTextModel> data { get; set; }
    }
}
