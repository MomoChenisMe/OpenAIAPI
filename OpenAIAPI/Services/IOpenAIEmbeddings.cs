using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using OpenAIAPI.Models;
using OpenAIAPI.Models.Partial;

namespace OpenAIAPI.Services
{
    public interface IOpenAIEmbeddings
    {
        /// <summary>
        /// 計算兩個向量的餘弦相似度。
        /// </summary>
        /// <param name="vectorA">第一個向量</param>
        /// <param name="vectorB">第二個向量</param>
        /// <returns>兩個向量的餘弦相似度</returns>
        double CalculateCosineSimilarity(float[] vectorA, float[] vectorB);
        /// <summary>
        /// 根據嵌入向量獲取與輸入文字相似的詞彙。
        /// </summary>
        /// <param name="inputText">輸入的文字</param>
        /// <returns>包含相似詞彙的嵌入生成模型</returns>
        Task<EmbeddingGenerateModel> GetTextSimilarWordsByEmbedding(string inputText);
        /// <summary>
        /// 根據嵌入向量獲取與輸入文字相似度最高的五個文本。
        /// </summary>
        /// <param name="inputText">輸入的文字</param>
        /// <returns>包含相似度最高的五個文本的問答生成模型</returns>
        Task<QAGenerateModel> GetTop5TextsByEmbedding(string inputText);
        /// <summary>
        /// 檢查輸入的文字是否為空或為 null。
        /// </summary>
        /// <param name="inputText">輸入的文字</param>
        /// <returns>如果輸入的文字為空或為 null，則為 true；否則為 false</returns>
        bool CheckTextNullOrEmpty(string inputText);
        /// <summary>
        /// 檢查輸入的文字是否超過指定的Token數量限制。
        /// </summary>
        /// <param name="inputText">輸入的文字</param>
        /// <returns>如果輸入的文字詞彙數量超過總詞彙數量限制，則為 true；否則為 false</returns>
        bool CheckTextTokenizer(string inputText);
        Task<Guid> AddQAEmbedding(string inputText);
    }

    public class OpenAIEmbeddings : IOpenAIEmbeddings
    {
        private readonly IConfiguration _configuration;
        private readonly OpenAIContext _dbContext;
        private readonly IOpenAIHttpService _openAIHttpService;
        private readonly IOpenAIGPTToken _openAIGPTToken;
        private readonly IOpenAIPrompt _openAIPrompt;
        private readonly ILogger _logger;
        private readonly int totalTokenSize = 0;
        private readonly int qaGPTCompletionMaxTokenSize = 0;

        public OpenAIEmbeddings(IConfiguration configuration, OpenAIContext dbContext, IOpenAIHttpService openAIHttpService, IOpenAIGPTToken openAIGPTToken, IOpenAIPrompt openAIPrompt,
            ILogger<OpenAIHttpService> logger)
        {
            _configuration = configuration;
            _dbContext = dbContext;
            _openAIHttpService = openAIHttpService;
            _openAIGPTToken = openAIGPTToken;
            _openAIPrompt = openAIPrompt;
            _logger = logger;
            totalTokenSize = _configuration.GetValue<int>("OpenAI:TotalTokenSize");
            qaGPTCompletionMaxTokenSize = _configuration.GetValue<int>("OpenAI:QAGPTCompletionMAXTokenSize");
        }

        public async Task<Guid> AddQAEmbedding(string inputText)
        {
            var filterText = _openAIGPTToken.FilterSpecialCharactersAndWhiteSpace(inputText);
            var inputVector = await _openAIHttpService.GetTextEmbeddingVector(filterText);

            var newTB_Embedding = new TB_Embeddings
            {
                Vector = string.Join(',', inputVector.Select(x => x.ToString(CultureInfo.InvariantCulture)))
            };

            _dbContext.TB_Embeddings.Add(newTB_Embedding);
            await _dbContext.SaveChangesAsync();
            return newTB_Embedding.Id;
        }

        /// <summary>
        /// 計算兩個向量的餘弦相似度。
        /// </summary>
        /// <param name="vectorA">第一個向量</param>
        /// <param name="vectorB">第二個向量</param>
        /// <returns>兩個向量的餘弦相似度</returns>
        public double CalculateCosineSimilarity(float[] vectorA, float[] vectorB)
        {
            double dotProduct = 0.0;
            double normA = 0.0;
            double normB = 0.0;

            for (int i = 0; i < vectorA.Length; i++)
            {
                dotProduct += vectorA[i] * vectorB[i];
                normA += vectorA[i] * vectorA[i];
                normB += vectorB[i] * vectorB[i];
            }

            normA = Math.Sqrt(normA);
            normB = Math.Sqrt(normB);

            return dotProduct / (normA * normB);
        }

        /// <summary>
        /// 檢查輸入的文字是否為空或為 null。
        /// </summary>
        /// <param name="inputText">輸入的文字</param>
        /// <returns>如果輸入的文字為空或為 null，則為 true；否則為 false</returns>
        public bool CheckTextNullOrEmpty(string inputText)
        {
            if (inputText == null || string.IsNullOrWhiteSpace(inputText))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 檢查輸入的文字是否超過指定的總詞彙數量限制。
        /// </summary>
        /// <param name="inputText">輸入的文字</param>
        /// <returns>如果輸入的文字詞彙數量超過總詞彙數量限制，則為 true；否則為 false</returns>
        public bool CheckTextTokenizer(string inputText)
        {
            var tokenizer = _openAIGPTToken.GetGPT3Tokenizer(inputText);
            if (tokenizer.tokens > totalTokenSize)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 根據嵌入向量獲取與輸入文字相似的詞彙。
        /// </summary>
        /// <param name="inputText">輸入的文字</param>
        /// <returns>包含相似詞彙的嵌入生成模型</returns>
        public async Task<EmbeddingGenerateModel> GetTextSimilarWordsByEmbedding(string inputText)
        {
            var inputVector = await _openAIHttpService.GetTextEmbeddingVector(inputText);

            // 1. 使用並行處理
            var allEmbeddings = await _dbContext.TB_Embeddings.ToListAsync();
            var cosineSimilarities = new ConcurrentDictionary<Guid, double>();

            // 2. 將所有嵌入向量保存在內存中，並將其轉換為float[]
            var embeddingVectors = allEmbeddings.Select(e => new
            {
                e.Id,
                Vector = e.Vector.Split(',').Select(float.Parse).ToArray()
            }).ToList();

            // 3. 適當地使用資料分批查詢
            int batchSize = 100;
            int batchCount = (int)Math.Ceiling(embeddingVectors.Count / (double)batchSize);

            Parallel.ForEach(Partitioner.Create(0, batchCount), range =>
            {
                for (int i = range.Item1; i < range.Item2; i++)
                {
                    int from = i * batchSize;
                    int to = Math.Min(from + batchSize, embeddingVectors.Count);

                    for (int j = from; j < to; j++)
                    {
                        var e = embeddingVectors[j];
                        var similarity = CalculateCosineSimilarity(inputVector, e.Vector);
                        cosineSimilarities.TryAdd(e.Id, similarity);
                    }
                }
            });

            var sortedSimilarities = cosineSimilarities.OrderByDescending(x => x.Value).Take(5).ToDictionary(x => x.Key, x => x.Value);
            var top5Vectors = sortedSimilarities.Select(x => x.Key).ToArray();

            return await UseTextEmbeddingGeneratePrompt(top5Vectors, inputText);
        }

        /// <summary>
        /// 使用文字嵌入生成模型的方法，根據提供的嵌入向量和輸入文字生成相應的提示。
        /// </summary>
        /// <param name="vectors">嵌入向量的Guid陣列</param>
        /// <param name="inputText">輸入的文字</param>
        /// <returns>嵌入生成模型，包含生成的提示</returns>
        private async Task<EmbeddingGenerateModel> UseTextEmbeddingGeneratePrompt(Guid[] vectors, string inputText)
        {
            StringBuilder sb = new StringBuilder();
            StringBuilder sb1 = new StringBuilder();
            var systemPrompt = _openAIPrompt.QASystemPrompt();
            var qaPrompt = $"\n\nnQuestion:{inputText}";
            var prompt = _openAIPrompt.QATextUserPrompt();
            sb.Append(prompt);
            sb1.Append(_openAIPrompt.QATextUseSourcePrompt());
            var tokenizer = _openAIGPTToken.GetGPT3Tokenizer(systemPrompt + prompt + qaPrompt);
            for (int i = 0; i < vectors.Length; i++)
            {
                var tbTextData = await _dbContext.TB_Texts.FirstOrDefaultAsync(text => text.EmbeddingId == vectors[i]);
                if (tbTextData != null)
                {
                    var currentTextTokenizer = _openAIGPTToken.GetGPT3Tokenizer(tbTextData.TextContent);
                    if (tokenizer.tokens + currentTextTokenizer.tokens + qaGPTCompletionMaxTokenSize <= totalTokenSize)
                    {
                        tokenizer.tokens += currentTextTokenizer.tokens;
                        sb.Append($"\n{tbTextData.TextContent}\n");
                        sb1.Append($"TextGuid:\"{tbTextData.Id.ToString()}\"\nTextName\":{tbTextData.Name}\"\n{_openAIGPTToken.FilterAllSpecialSymbols(tbTextData.TextContent)}\n");
                    }
                    else
                    {
                        break;
                    }
                }
            }
            sb.Append(qaPrompt);
            sb1.Append($"\n\nQuestion:{inputText}");
            return new EmbeddingGenerateModel
            {
                prompt = sb.ToString(),
                sourcePrompt = sb1.ToString()
            };
        }

        /// <summary>
        /// 根據嵌入向量獲取與輸入文字相似度最高的五個文本。
        /// </summary>
        /// <param name="inputText">輸入的文字</param>
        /// <returns>包含相似度最高的五個文本的問答生成模型</returns>
        public async Task<QAGenerateModel> GetTop5TextsByEmbedding(string inputText)
        {
            var inputVector = await _openAIHttpService.GetTextEmbeddingVector(inputText);

            // 1. 使用並行處理
            var allEmbeddings = await _dbContext.TB_Embeddings.ToListAsync();
            var cosineSimilarities = new ConcurrentDictionary<Guid, double>();

            // 2. 將所有嵌入向量保存在內存中，並將其轉換為float[]
            var embeddingVectors = allEmbeddings.Select(e => new
            {
                e.Id,
                Vector = e.Vector.Split(',').Select(float.Parse).ToArray()
            }).ToList();

            // 3. 適當地使用資料分批查詢
            int batchSize = 100;
            int batchCount = (int)Math.Ceiling(embeddingVectors.Count / (double)batchSize);

            Parallel.ForEach(Partitioner.Create(0, batchCount), range =>
            {
                for (int i = range.Item1; i < range.Item2; i++)
                {
                    int from = i * batchSize;
                    int to = Math.Min(from + batchSize, embeddingVectors.Count);

                    for (int j = from; j < to; j++)
                    {
                        var e = embeddingVectors[j];
                        var similarity = CalculateCosineSimilarity(inputVector, e.Vector);
                        cosineSimilarities.TryAdd(e.Id, similarity);
                    }
                }
            });

            var sortedSimilarities = cosineSimilarities.OrderByDescending(x => x.Value).Take(5).ToDictionary(x => x.Key, x => x.Value);
            var top5Vectors = sortedSimilarities.Select(x => x.Key).ToArray();

            return await UseTop5TextGeneratePrompt(top5Vectors, inputText);
        }

        /// <summary>
        /// 使用頂部五個文本生成提示的方法，根據提供的嵌入向量和輸入文字生成問答生成模型。
        /// </summary>
        /// <param name="vectors">嵌入向量的Guid陣列</param>
        /// <param name="inputText">輸入的文字</param>
        /// <returns>問答生成模型，包含生成的提示和使用的文本清單</returns>
        private async Task<QAGenerateModel> UseTop5TextGeneratePrompt(Guid[] vectors, string inputText)
        {
            var guids = await GetTextGuidsAsync(vectors, inputText);
            return await GenerateQAPromptAsync(guids, inputText);
        }

        /// <summary>
        /// 根據提供的嵌入向量和輸入文字，獲取與嵌入向量相對應的文本的Guid清單。
        /// </summary>
        /// <param name="vectors">嵌入向量的Guid陣列</param>
        /// <param name="inputText">輸入的文字</param>
        /// <returns>與嵌入向量相對應的文本的Guid清單</returns>
        private async Task<List<Guid>> GetTextGuidsAsync(Guid[] vectors, string inputText)
        {
            var guids = new List<Guid>();
            var prompt = _openAIPrompt.QATextUseSourcePrompt();
            var tokenizer = _openAIGPTToken.GetGPT3Tokenizer(prompt);
            var qaPrompt = $"\n\nQuestion:{inputText}";

            foreach (var vector in vectors)
            {
                var tbTextData = await _dbContext.TB_Texts.FirstOrDefaultAsync(text => text.EmbeddingId == vector);
                if (tbTextData != null)
                {
                    var textContent = FilterNewLines(tbTextData.TextContent);
                    var tempPrompt = $"TextGuid:\"{tbTextData.Id}\"\nTextName:\"{tbTextData.Name}\"\nTextContent:\"{textContent}\"\n\n";
                    var currentTextTokenizer = _openAIGPTToken.GetGPT3Tokenizer(tempPrompt);
                    int tokensNeeded = tokenizer.tokens + currentTextTokenizer.tokens + 200;

                    if (tokensNeeded > totalTokenSize)
                    {
                        guids.AddRange(await GetGuidsFromQAResultAsync(prompt + qaPrompt));
                        prompt = _openAIPrompt.QATextUseSourcePrompt();
                        tokenizer = _openAIGPTToken.GetGPT3Tokenizer(prompt + qaPrompt);
                    }

                    tokenizer.tokens += currentTextTokenizer.tokens;
                    prompt += tempPrompt;
                }
            }

            guids.AddRange(await GetGuidsFromQAResultAsync(prompt + qaPrompt));
            return guids;
        }

        /// <summary>
        /// 從ChatGPT回傳結果中獲取包含Guid的文本清單。
        /// </summary>
        /// <param name="prompt">問答生成的提示</param>
        /// <returns>包含Guid的文本清單</returns>
        private async Task<List<Guid>> GetGuidsFromQAResultAsync(string prompt)
        {
            var qaResult = await _openAIHttpService.GetCustomChatGPTResponse(prompt, 0.0, 200);
            var regex = new Regex(@"\[(.*?)\]");
            var match = regex.Match(qaResult);
            var guids = new List<Guid>();

            if (match.Success)
            {
                var idsList = JsonConvert.DeserializeObject<List<UsingTextModel>>(match.Value);
                guids.AddRange(idsList.Select(id => Guid.Parse(id.textGuid)));
            }

            return guids;
        }

        /// <summary>
        /// 根據提供的文本Guid清單和輸入文字生成問答生成模型的提示。
        /// </summary>
        /// <param name="guids">文本的Guid清單</param>
        /// <param name="inputText">輸入的文字</param>
        /// <returns>問答生成模型，包含生成的提示和使用的文本清單</returns>
        private async Task<QAGenerateModel> GenerateQAPromptAsync(List<Guid> guids, string inputText)
        {
            var prompt = _openAIPrompt.QATextUserPrompt();
            var qaPrompt = $"\n\nQuestion:{inputText}";
            var tokenizer = _openAIGPTToken.GetGPT3Tokenizer(prompt + qaPrompt);
            var usingTextList = new List<UsingTextModel>();

            foreach (var guid in guids)
            {
                var tbTextData = await _dbContext.TB_Texts.FirstOrDefaultAsync(text => text.Id == guid);
                if (tbTextData != null)
                {
                    var textContent = FilterNewLines(tbTextData.TextContent);
                    var currentTextTokenizer = _openAIGPTToken.GetGPT3Tokenizer(textContent);
                    int tokensNeeded = tokenizer.tokens + currentTextTokenizer.tokens + qaGPTCompletionMaxTokenSize;

                    if (tokensNeeded > totalTokenSize)
                    {
                        break;
                    }

                    tokenizer.tokens += currentTextTokenizer.tokens;
                    prompt += $"{textContent}\n\n";
                    usingTextList.Add(new UsingTextModel
                    {
                        textGuid = tbTextData.Id.ToString(),
                        textName = tbTextData.Name
                    });
                }
            }

            if (guids.Count == 0)
            {
                prompt += "empty";
            }

            prompt += qaPrompt;

            return new QAGenerateModel
            {
                prompt = prompt,
                usingText = usingTextList
            };
        }

        private string FilterNewLines(string inputText)
        {
            return inputText.Replace("\n", "");
        }
    }
}
