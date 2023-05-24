using AI.Dev.OpenAI.GPT;
using System.Text.RegularExpressions;
using OpenAIAPI.Models.Partial;

namespace OpenAIAPI.Services
{
    public interface IOpenAIGPTToken
    {
        /// <summary>
        /// 過濾特殊字符和空白字符的方法。
        /// </summary>
        /// <param name="input">輸入的字串</param>
        string FilterSpecialCharactersAndWhiteSpace(string input);
        /// <summary>
        /// 過濾所有特殊符號和空白字符的方法。
        /// </summary>
        /// <param name="input">輸入的字串</param>
        string FilterAllSpecialSymbols(string input);
        /// <summary>
        /// 過濾 HTML 標籤的方法。
        /// </summary>
        /// <param name="input">輸入的字串</param>
        string FilterHtmlTag(string input);
        /// <summary>
        /// 使用 GPT-3 Tokenizer 對輸入的文字進行分詞，並返回分詞後的詞彙數量。
        /// </summary>
        /// <param name="input">輸入的文字</param>
        TokenCountModel GetGPT3Tokenizer(string input);
    }

    public class OpenAIGPTToken : IOpenAIGPTToken
    {
        /// <summary>
        /// 過濾所有特殊符號和空白字符的方法。
        /// </summary>
        /// <param name="input">輸入的字串</param>
        public string FilterAllSpecialSymbols(string input)
        {
            // 定義正則表達式，排除指定的符號和空白字符
            string pattern = "[^a-zA-Z0-9\\s\u4e00-\u9fa5]";
            // 使用正則表達式替換匹配的字符
            string result = Regex.Replace(input, pattern, string.Empty);
            return result;
        }

        /// <summary>
        /// 過濾 HTML 標籤的方法。
        /// </summary>
        /// <param name="input">輸入的字串</param>
        public string FilterHtmlTag(string input)
        {
            return Regex.Replace(input, "<.*?>", string.Empty);
        }

        /// <summary>
        /// 過濾特殊字符和空白字符的方法。
        /// </summary>
        /// <param name="input">輸入的字串</param>
        public string FilterSpecialCharactersAndWhiteSpace(string input)
        {
            // 定義正則表達式，排除指定的符號和空白字符
            string pattern = "[^a-zA-Z0-9,.:;`!?\'\"()\\[\\]{}，。、：；！？‘“（）【】｛｝\\s]";
            // 使用正則表達式替換匹配的字符
            string result = Regex.Replace(input, pattern, string.Empty);
            return result;
        }

        /// <summary>
        /// 使用 GPT-3 Tokenizer 對輸入的文字進行分詞，並返回分詞後的詞彙數量。
        /// </summary>
        /// <param name="input">輸入的文字</param>
        public TokenCountModel GetGPT3Tokenizer(string input)
        {
            List<int> tokens = GPT3Tokenizer.Encode(input);
            return new TokenCountModel()
            {
                tokens = tokens.Count()
            };
        }
    }
}
