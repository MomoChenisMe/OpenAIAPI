using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using OpenAIAPI.Models.Partial;
using OpenAIAPI.Services;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using OpenAIAPI.Models;


namespace OpenAIAPI.Controllers
{
    //[Authorize]
    [ApiController]
    [Route("/[controller]")]
    public class OpenAIController : ControllerBase
    {
        private readonly IOpenAIHttpService _openAIHttpService;
        private readonly IOpenAIEmbeddings _openAIEmbeddings;
        private readonly IOpenAIGPTToken _openAIGPTToken;

        public OpenAIController(IOpenAIHttpService openAIHttpService, IOpenAIEmbeddings openAIEmbeddings, IOpenAIGPTToken openAIGPTToken)
        {
            _openAIHttpService = openAIHttpService;
            _openAIEmbeddings = openAIEmbeddings;
            _openAIGPTToken = openAIGPTToken;
        }

        /// <summary>
        /// QA系統API, 可以詢問添加過Embedding的相關問題
        /// </summary> 
        [HttpPost("QA", Name = nameof(GetQA))]
        public async Task<ActionResult<string>> GetQA([FromBody] QAModel qaData)
        {
            var embeddingText = await _openAIEmbeddings.GetTextSimilarWordsByEmbedding(qaData.question);
            var responseData = await _openAIHttpService.GetQAGPTResponse(embeddingText.prompt);
            return Ok(responseData);
        }

        /// <summary>
        /// QA系統API, 串流版本
        /// </summary> 
        [HttpPost("QAStream", Name = nameof(GetQAStream))]
        public async Task<ActionResult<EmbeddingGenerateModel>> GetQAStream([FromBody] QAModel qaData)
        {
            Response.Headers.Add("Content-Type", "text/event-stream");
            Response.Headers.Add("Cache-Control", "no-cache");
            var qaEmbeddingData = await _openAIEmbeddings.GetTop5TextsByEmbedding(qaData.question);
            try
            {
                await foreach (string message in _openAIHttpService.GetQAGPTResponseAsStreamAsync(qaEmbeddingData.prompt))
                {
                    // Create the SSE data
                    if (message != null)
                    {
                        var outPutMessageObject = new ChatGPTStreamModel()
                        {
                            text = message
                        };
                        await Response.BodyWriter.WriteAsync(Encoding.UTF8.GetBytes($"data: {JsonConvert.SerializeObject(outPutMessageObject)}\n\n"));
                    }
                    if (HttpContext.RequestAborted.IsCancellationRequested)
                    {
                        break;
                    }
                };
                var doneMessageObject = new ChatGPTStreamModel()
                {
                    text = "[DONE]"
                };
                await Response.BodyWriter.WriteAsync(Encoding.UTF8.GetBytes($"data: {JsonConvert.SerializeObject(doneMessageObject)}\n\n"));
                await Response.BodyWriter.WriteAsync(Encoding.UTF8.GetBytes($"data: {JsonConvert.SerializeObject(new { qaEmbeddingData.usingText })}\n\n"));

                await Response.BodyWriter.FlushAsync();
            }
            catch (Exception e)
            {
                return BadRequest();
            }

            await Response.CompleteAsync();
            return new EmptyResult();
        }

        /// <summary>
        /// Chat GPT 3.5 API
        /// </summary> 
        [HttpPost("ChatGPT", Name = nameof(GetChatGPT))]
        [Produces("application/json")]
        public async Task<ActionResult<ChatGPTMessageModel>> GetChatGPT([FromBody] List<ChatGPTMessageModel> messages)
        {
            var responseData = await _openAIHttpService.GetChatGPTResponse(messages);
            return Ok(responseData);
        }

        /// <summary>
        /// Chat GPT 3.5 API, 串流版本
        /// </summary> 
        [HttpPost("ChatGPTStream", Name = nameof(GetChatGPTStream))]
        public async Task<IActionResult> GetChatGPTStream([FromBody] List<ChatGPTMessageModel> messages)
        {
            // Set the content type to "text/event-stream"
            Response.Headers.Add("Content-Type", "text/event-stream");

            // Set cache control to "no-cache"
            Response.Headers.Add("Cache-Control", "no-cache");

            try
            {
                // Create a SSE message for each response from the GPT API
                await foreach (string message in _openAIHttpService.GetChatGPTResponseAsStreamAsync(messages))
                {
                    // Create the SSE data
                    if (message != null)
                    {
                        var outPutMessageObject = new ChatGPTStreamModel()
                        {
                            text = message
                        };

                        // Write the SSE message to the response stream
                        await Response.BodyWriter.WriteAsync(Encoding.UTF8.GetBytes($"data: {JsonConvert.SerializeObject(outPutMessageObject)}\n\n"));
                        //await Task.Delay(1);
                    }

                    // Check if the SSE connection is still open
                    if (HttpContext.RequestAborted.IsCancellationRequested)
                    {
                        break;
                    }
                }
                await Response.BodyWriter.FlushAsync();
            }
            catch (Exception e)
            {
                return BadRequest();
            }

            await Response.CompleteAsync();
            return new EmptyResult();
        }

        /// <summary>
        /// 用來計算Token數量
        /// </summary> 
        [HttpPost("GPT3Tokenizer", Name = nameof(GetGPT3Tokenizer))]
        [Produces("application/json")]
        public ActionResult<TokenCountModel> GetGPT3Tokenizer([FromBody] InputTextModel inputText)
        {
            var responseData = _openAIGPTToken.GetGPT3Tokenizer(inputText.text);
            return Ok(responseData);
        }

        /// <summary>
        /// 添加Embedding文本到資料庫
        /// </summary> 
        //[HttpPost("AddQAEmbedding", Name = nameof(AddQAEmbedding))]
        //public async Task<IActionResult> AddQAEmbedding([FromBody] InputTextModel inputText)
        //{
        //    if (_openAIEmbeddings.CheckTextNullOrEmpty(inputText.text))
        //    {
        //        return BadRequest("文本不得為空的");
        //    }

        //    if (_openAIEmbeddings.CheckTextTokenizer(inputText.text))
        //    {
        //        return BadRequest("文本超過大小");
        //    }

        //    if (await _openAIEmbeddings.CheckTextIsAssigned(inputText.text))
        //    {
        //        return BadRequest("文本已存在");
        //    }

        //    await _openAIEmbeddings.AddQAEmbedding(inputText.text);

        //    return Ok(new { message = "QAEmbedding資料寫入完成" });
        //}
    }
}
