using Microsoft.Identity.Client;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;
using static System.Net.Mime.MediaTypeNames;
using Newtonsoft.Json.Linq;
using Microsoft.AspNetCore.Components.Forms;
using System.IO;
using OpenAIAPI.Controllers;
using System.Net.Http;
using System;
using System.Threading;
using Azure;
using OpenAIAPI.Models.Partial;

namespace OpenAIAPI.Services
{
    public interface IOpenAIHttpService
    {
        Task<float[]> GetTextEmbeddingVector(string inputText);
        Task<string> GetQAGPTResponse(string inputText);
        Task<ChatGPTMessageModel> GetChatGPTResponse(List<ChatGPTMessageModel> messages);
        IAsyncEnumerable<string> GetQAGPTResponseAsStreamAsync(string inputText);

        IAsyncEnumerable<string> GetChatGPTResponseAsStreamAsync(List<ChatGPTMessageModel> messages);

        Task<string> GetCustomChatGPTResponse(string inputText, double temperature, int maxTokens);
    }

    public class OpenAIHttpService : IOpenAIHttpService
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly IOpenAIGPTToken _openAIGPTToken;
        private readonly IOpenAIPrompt _openAIPrompt;
        private readonly ILogger _logger;
        private readonly string RequestURL = "https://api.openai.com/v1/";
        private readonly string apiKey = "";
        private readonly string chatGPTModel = "";
        private readonly int chatGPTCompletionMaxTokenSize = 0;
        private readonly int qaGPTCompletionMaxTokenSize = 0;
        private readonly int totalTokenSize = 0;
        private readonly string embeddingModel = "";

        public OpenAIHttpService(HttpClient httpClient, IConfiguration configuration,
            IOpenAIGPTToken openAIGPTToken, IOpenAIPrompt openAIPrompt, ILogger<OpenAIHttpService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _openAIGPTToken = openAIGPTToken;
            _openAIPrompt = openAIPrompt;
            _logger = logger;
            apiKey = _configuration.GetValue<string>("OpenAI:Key");
            chatGPTModel = _configuration.GetValue<string>("OpenAI:ChatGPTModel");
            chatGPTCompletionMaxTokenSize = _configuration.GetValue<int>("OpenAI:ChatGPTCompletionMAXTokenSize");
            qaGPTCompletionMaxTokenSize = _configuration.GetValue<int>("OpenAI:QAGPTCompletionMAXTokenSize");
            totalTokenSize = _configuration.GetValue<int>("OpenAI:TotalTokenSize");
            embeddingModel = _configuration.GetValue<string>("OpenAI:EmbeddingModel");
        }

        public async IAsyncEnumerable<string> GetChatGPTResponseAsStreamAsync(List<ChatGPTMessageModel> messages)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            List<ChatGPTMessageModel> sendMessages = new List<ChatGPTMessageModel>();
            var tokenCount = 0;
            for (int i = messages.Count - 1; i >= 0; i--)
            {
                var message = messages[i];
                var tokenSize = _openAIGPTToken.GetGPT3Tokenizer(message.content).tokens;
                if (tokenCount + tokenSize + chatGPTCompletionMaxTokenSize <= totalTokenSize)
                {
                    tokenCount += tokenSize;
                    sendMessages.Insert(0, message);
                }
                else
                {
                    break;
                }
            }

            var requestData = new ChatGPTRequestModel()
            {
                model = chatGPTModel,
                messages = sendMessages,
                temperature = 0.7,
                max_tokens = chatGPTCompletionMaxTokenSize,
                top_p = 1,
                stream = true
            };

            var content = new StringContent(JsonConvert.SerializeObject(requestData), Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, RequestURL + "chat/completions")
            {
                Content = content
            };
            request.Headers.Accept.Clear();
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
            var cancellationToken = new CancellationTokenSource();
            using var response = _httpClient.Send(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken.Token);
            response.EnsureSuccessStatusCode();
            using var streamReader = new StreamReader(await response.Content.ReadAsStreamAsync(cancellationToken.Token), leaveOpen: false);
            while (!cancellationToken.IsCancellationRequested)
            {
                string line = await streamReader.ReadLineAsync();
                if (line == null || line.StartsWith("event:"))
                {
                    continue;
                }

                if (line.StartsWith("data:"))
                {
                    string jsonData = line.Substring("data:".Length).Trim();
                    if (jsonData != "[DONE]")
                    {
                        dynamic streamJSONData = JsonConvert.DeserializeObject(jsonData);

                        yield return streamJSONData.choices[0].delta.content;
                    }
                    else if (jsonData == "[DONE]")
                    {
                        cancellationToken.Cancel();
                        break;
                    }
                }
            }
        }

        public async Task<ChatGPTMessageModel> GetChatGPTResponse(List<ChatGPTMessageModel> messages)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            List<ChatGPTMessageModel> sendMessages = new List<ChatGPTMessageModel>();
            var tokenCount = 0;
            for (int i = messages.Count - 1; i >= 0; i--)
            {
                var message = messages[i];
                var tokenSize = _openAIGPTToken.GetGPT3Tokenizer(message.content).tokens;
                if (tokenCount + tokenSize + chatGPTCompletionMaxTokenSize < totalTokenSize)
                {
                    tokenCount += tokenSize;
                    sendMessages.Insert(0, message);
                }
                else
                {
                    break;
                }
            }

            var requestData = new ChatGPTRequestModel()
            {
                model = chatGPTModel,
                messages = sendMessages,
                temperature = 0.7,
                max_tokens = chatGPTCompletionMaxTokenSize,
                top_p = 1,
                stream = false
            };
            var content = new StringContent(JsonConvert.SerializeObject(requestData), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(RequestURL + "chat/completions", content);

            if (response.IsSuccessStatusCode)
            {
                var jsonResponse = await response.Content.ReadAsStringAsync();
                dynamic data = JsonConvert.DeserializeObject(jsonResponse);
                ChatGPTMessageModel responseData = JsonConvert.DeserializeObject<ChatGPTMessageModel>(data.choices[0].message.ToString());
                return responseData;
            }
            else
            {
                throw new Exception($"GetChatGPTResponse錯誤 : {response.ReasonPhrase}");
            }
        }

        public async IAsyncEnumerable<string> GetQAGPTResponseAsStreamAsync(string inputText)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            List<ChatGPTMessageModel> messages = new List<ChatGPTMessageModel>();
            messages.Add(new ChatGPTMessageModel()
            {
                role = "system",
                content = _openAIPrompt.QASystemPrompt()
            }); ;
            ChatGPTMessageModel message = new ChatGPTMessageModel()
            {
                role = "user",
                content = inputText
            };

            messages.Add(message);
            var requestData = new ChatGPTRequestModel()
            {
                model = chatGPTModel,
                messages = messages,
                temperature = 0.2,
                max_tokens = qaGPTCompletionMaxTokenSize,
                top_p = 1,
                stream = true
            };
            var content = new StringContent(JsonConvert.SerializeObject(requestData), Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, RequestURL + "chat/completions")
            {
                Content = content
            };
            request.Headers.Accept.Clear();
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
            var cancellationToken = new CancellationTokenSource();
            var aaa = JsonConvert.SerializeObject(requestData);
            using var response = _httpClient.Send(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken.Token);
            response.EnsureSuccessStatusCode();
            using var streamReader = new StreamReader(await response.Content.ReadAsStreamAsync(cancellationToken.Token), leaveOpen: false);
            while (!cancellationToken.IsCancellationRequested)
            {
                string line = await streamReader.ReadLineAsync();
                if (line == null || line.StartsWith("event:"))
                {
                    continue;
                }

                if (line.StartsWith("data:"))
                {
                    string jsonData = line.Substring("data:".Length).Trim();

                    if (jsonData != "[DONE]")
                    {
                        dynamic streamJSONData = JsonConvert.DeserializeObject(jsonData);

                        yield return streamJSONData.choices[0].delta.content;
                    }
                    else if (jsonData == "[DONE]")
                    {
                        cancellationToken.Cancel();
                        break;
                    }
                }
            }
        }

        public async Task<string> GetQAGPTResponse(string inputText)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            List<ChatGPTMessageModel> messages = new List<ChatGPTMessageModel>();
            messages.Add(new ChatGPTMessageModel()
            {
                role = "system",
                content = _openAIPrompt.QASystemPrompt()
            });
            ChatGPTMessageModel message = new ChatGPTMessageModel()
            {
                role = "user",
                content = inputText
            };
            messages.Add(message);
            var requestData = new ChatGPTRequestModel()
            {
                model = chatGPTModel,
                messages = messages,
                temperature = 0.2,
                max_tokens = qaGPTCompletionMaxTokenSize,
                top_p = 1,
                stream = false
            };
            var content = new StringContent(JsonConvert.SerializeObject(requestData), Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(RequestURL + "chat/completions", content);

            if (response.IsSuccessStatusCode)
            {
                var jsonResponse = await response.Content.ReadAsStringAsync();
                dynamic data = JsonConvert.DeserializeObject(jsonResponse);
                return data.choices[0].message.content.ToString();
            }
            else
            {
                throw new Exception($"GetQAGPTResponse錯誤 : {response.ReasonPhrase}");
            }
        }

        public async Task<float[]> GetTextEmbeddingVector(string inputText)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var requestData = new EmbeddingRequestModel()
            {
                input = inputText,
                model = embeddingModel
            };
            var content = new StringContent(JsonConvert.SerializeObject(requestData), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(RequestURL + "embeddings", content);

            if (response.IsSuccessStatusCode)
            {
                var httpResponseData = await response.Content.ReadAsStringAsync();
                var jsonResponse = JObject.Parse(httpResponseData);
                var embeddings = jsonResponse["data"].First["embedding"].ToObject<float[]>();
                return embeddings;
            }
            else
            {
                throw new Exception($"GetTextEmbeddingVector錯誤 : {response.ReasonPhrase}");
            }
        }

        public async Task<string> GetCustomChatGPTResponse(string inputText, double temperature, int maxTokens)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            List<ChatGPTMessageModel> messages = new List<ChatGPTMessageModel>();
            ChatGPTMessageModel message = new ChatGPTMessageModel()
            {
                role = "user",
                content = inputText
            };
            messages.Add(message);
            var requestData = new ChatGPTRequestModel()
            {
                model = chatGPTModel,
                messages = messages,
                temperature = temperature,
                max_tokens = maxTokens > 2048 ? 2048 : maxTokens,
                top_p = 1,
                stream = false
            };
            var content = new StringContent(JsonConvert.SerializeObject(requestData), Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(RequestURL + "chat/completions", content);

            if (response.IsSuccessStatusCode)
            {
                var jsonResponse = await response.Content.ReadAsStringAsync();
                dynamic data = JsonConvert.DeserializeObject(jsonResponse);
                return data.choices[0].message.content.ToString();
            }
            else
            {
                throw new Exception($"GetCustomChatGPTResponse錯誤 : {response.ReasonPhrase}");
            }
        }
    }
}
