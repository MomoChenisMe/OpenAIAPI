using AI.Dev.OpenAI.GPT;
using System.Text.RegularExpressions;
using OpenAIAPI.Models.Partial;

namespace OpenAIAPI.Services
{
    public interface IOpenAIGPTToken
    {
        string FilterSpecialCharactersAndWhiteSpace(string input);
        string FilterAllSpecialSymbols(string input);

        string FilterHtmlTag(string input);
        TokenCountModel GetGPT3Tokenizer(string input);
    }

    public class OpenAIGPTToken : IOpenAIGPTToken
    {
        public string FilterAllSpecialSymbols(string input)
        {
            // 定義正則表達式，排除指定的符號和空白字符
            string pattern = "[^a-zA-Z0-9\\s\u4e00-\u9fa5]";
            // 使用正則表達式替換匹配的字符
            string result = Regex.Replace(input, pattern, string.Empty);
            return result;
        }

        public string FilterHtmlTag(string input)
        {
            return Regex.Replace(input, "<.*?>", string.Empty);
        }

        public string FilterSpecialCharactersAndWhiteSpace(string input)
        {
            // 定義正則表達式，排除指定的符號和空白字符
            string pattern = "[^a-zA-Z0-9,.:;`!?\'\"()\\[\\]{}，。、：；！？‘“（）【】｛｝\\s]";
            // 使用正則表達式替換匹配的字符
            string result = Regex.Replace(input, pattern, string.Empty);
            return result;
        }

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
