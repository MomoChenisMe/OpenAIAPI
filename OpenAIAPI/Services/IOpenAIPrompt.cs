using Azure;
using System.Buffers.Text;
using System.Numerics;
using System.Reflection.Metadata;
using OpenAIAPI.Models.Partial;

namespace OpenAIAPI.Services
{
    public interface IOpenAIPrompt
    {
        string QASystemPrompt();
        string QATextUserPrompt();
        string QATextUseSourcePrompt();

        string QABugQAUserPrompt();
    }

    public class OpenAIPrompt : IOpenAIPrompt
    {
        public string QABugQAUserPrompt()
        {
            return "Each context has a Bugano followed by the actual message. Briefly explain the content of the source, and if it contains an answer, please include it in the explanation. Do not directly output the content of the sources. You must find the most relevant Bugano from multiple sources for the given question. Please output Bugano in the following format, e.g. \"根據匹配為您找到:\n需求單號:20220303003\n需求說明:xxxx\n\n需求單號:20220303004:\n需求說明:zzzzz...\". If the question is not contained in the select sources or is not related to the select sources, say \"抱歉,您的提問未納入鏵得企業的系統需求QA問題集\". \n\n Sources:\n";
        }

        public string QASystemPrompt()
        {
            return "Use Traditional Chinese, You'r name is MomoChenIsMe QA 助理 and an AI assistant developed by the R&D department. Your responsibility is to answer QA questions for MomoChenIsMe";
        }

        public string QATextUserPrompt()
        {
            return "Answer the question as truthfully as possible using the provided context. " +
                "If there is code, please wrap it in markdown syntax. If the answer is not contained within the text below or the text is empty " +
                "say \"抱歉,您的提問未納入QA問題集,因此我無法回答您的問題\".\n\nContext:\n";
        }

        public string QATextUseSourcePrompt()
        {
            return "Each context has a TextGuid and TextName and TextContent followed by the actual message. " +
                "Before providing an answer as accurately as possible, you must select multiple sources that can be used to answer the question and keep the TextGuid and TextName of the selected sources then output the result in the following format, " +
                "e.g. [{textGuid: \"xxx\", textName: \"zzz\"}], only output the format that I have specified. " +
                "If no sources are selected, please output an empty array.\n\nSources:\n";
        }
    }
}
