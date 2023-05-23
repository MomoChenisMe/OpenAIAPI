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
        double CalculateCosineSimilarity(float[] vectorA, float[] vectorB);
        //Task<EmbeddingGenerateModel> UseEmbeddingGenerateText(Guid[] vectors, string inputText);
        Task<EmbeddingGenerateModel> GetTextSimilarWordsByEmbedding(string inputText);
        Task<QAGenerateModel> GetTop5TextsByEmbedding(string inputText);
        bool CheckTextNullOrEmpty(string inputText);
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

        public bool CheckTextNullOrEmpty(string inputText)
        {
            if (inputText == null || string.IsNullOrWhiteSpace(inputText))
            {
                return true;
            }
            return false;
        }

        public bool CheckTextTokenizer(string inputText)
        {
            var tokenizer = _openAIGPTToken.GetGPT3Tokenizer(inputText);
            if (tokenizer.tokens > totalTokenSize)
            {
                return true;
            }
            return false;
        }

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

        private async Task<QAGenerateModel> UseTop5TextGeneratePrompt(Guid[] vectors, string inputText)
        {
            var guids = await GetTextGuidsAsync(vectors, inputText);
            return await GenerateQAPromptAsync(guids, inputText);
        }

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
                    var tempPrompt = $"TextGuid:\"{tbTextData.Id}\"\nTextName:{tbTextData.Name}\nTextContent:{textContent}\n\n";
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
