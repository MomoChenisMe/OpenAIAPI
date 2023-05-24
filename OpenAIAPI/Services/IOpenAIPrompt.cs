using Azure;
using System.Buffers.Text;
using System.Numerics;
using System.Reflection.Metadata;
using OpenAIAPI.Models.Partial;

namespace OpenAIAPI.Services
{
    public interface IOpenAIPrompt
    {
        /// <summary>
        /// 取得問答生成中系統的提示。
        /// </summary>
        /// <returns>包含使用者的提示</returns>
        string QASystemPrompt();
        /// <summary>
        /// 取得問答生成中使用者的提示。
        /// </summary>
        /// <returns>包含使用者的提示</returns>
        string QATextUserPrompt();
        /// <summary>
        /// 取得問答生成中使用文本來源的提示。
        /// </summary>
        /// <returns>包含使用文本來源的提示</returns>
        string QATextUseSourcePrompt();
    }

    public class OpenAIPrompt : IOpenAIPrompt
    {
        /// <summary>
        /// 取得問答生成中系統的提示。
        /// </summary>
        /// <returns>包含使用者的提示</returns>
        public string QASystemPrompt()
        {
            return "Use Traditional Chinese, You'r name is MomoChenIsMe QA 助理 and an AI assistant developed by the R&D department. Your responsibility is to answer QA questions for MomoChenIsMe";
        }

        /// <summary>
        /// 取得問答生成中使用者的提示。
        /// </summary>
        /// <returns>包含使用者的提示</returns>
        public string QATextUserPrompt()
        {
            return "Answer the question as truthfully as possible using the provided context. " +
                "If there is code, please wrap it in markdown syntax. If the answer is not contained within the text below or the text is empty " +
                "say \"抱歉,您的提問未納入QA問題集,因此我無法回答您的問題\".\n\nContext:\n";
        }

        /// <summary>
        /// 取得問答生成中使用文本來源的提示。
        /// </summary>
        /// <returns>包含使用文本來源的提示</returns>
        public string QATextUseSourcePrompt()
        {
            return "Each context has a TextGuid and TextName and TextContent followed by the actual message. " +
                "Before providing an answer as accurately as possible, you must select multiple sources that can be used to answer the question and keep the TextGuid and TextName of the selected sources then output the result in the following format, " +
                "e.g. [{textGuid: \"xxx\", textName: \"zzz\"}], only output the format that I have specified. " +
                "If no sources are selected, please output an empty array.\n\nSources:\n";
        }
    }
}
