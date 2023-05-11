using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using OpenAIAPI.Models.Partial;
using OpenAIAPI.Models;

namespace OpenAIAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("/[controller]")]
    public class ChatroomController : ControllerBase
    {
        private readonly OpenAIContext _dbContext;

        public ChatroomController(OpenAIContext dbContext)
        {
            _dbContext = dbContext;
        }

        /// <summary>
        /// 取得聊天室資料
        /// </summary>
        /// <param name="accountEmail"></param>
        /// <returns></returns>
        [HttpGet("{accountEmail}", Name = nameof(GetChatroomsByAccountEmail))]
        public async Task<ActionResult<List<ChatroomModel>>> GetChatroomsByAccountEmail(string accountEmail)
        {
            var chatroomList = await _dbContext.TB_Chat.Where(c => _dbContext.TB_UserChatroom.Any(uc => uc.AccountEmail == accountEmail && uc.ChatroomID == c.ChatroomID))
                        .Select(c => new ChatroomModel()
                        {
                            ChatroomID = c.ChatroomID.ToString().ToLower(),
                            ChatName = c.ChatName,
                            Content = JsonConvert.DeserializeObject<List<ChatGPTMessageModel>>(c.Content)
                        }).ToListAsync();
            return Ok(chatroomList);
        }


        /// <summary>
        /// 新增或更新聊天室資料
        /// </summary>
        /// <param name="chatroomData"></param>
        /// <returns></returns>
        [HttpPut("{accountEmail}", Name = nameof(UpsertChatroomsByAccountEmail))]
        public async Task<IActionResult> UpsertChatroomsByAccountEmail(string accountEmail, [FromBody] ChatroomModel chatroomData)
        {
            if (accountEmail == "")
            {
                return BadRequest("無效的Email");
            }

            //var existingEmail = await _dbContext.TB_User.FindAsync(accountEmail);
            //if (existingEmail == null)
            //{
            //    return NotFound();
            //}
            var chatroomID = "";
            try
            {
                if (chatroomData.ChatroomID == "")
                {
                    var newChatroom = new TB_Chat()
                    {
                        ChatName = chatroomData.ChatName == "" ? DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") : chatroomData.ChatName,
                        Content = JsonConvert.SerializeObject(chatroomData.Content)
                    };
                    _dbContext.TB_Chat.Add(newChatroom);
                    await _dbContext.SaveChangesAsync();

                    var newChatroomID = newChatroom.ChatroomID;

                    _dbContext.TB_UserChatroom.Add(new TB_UserChatroom()
                    {
                        AccountEmail = accountEmail,
                        ChatroomID = newChatroomID,
                    });

                    await _dbContext.SaveChangesAsync();

                    chatroomID = newChatroomID.ToString();
                }
                else
                {
                    if (!Guid.TryParse(chatroomData.ChatroomID, out Guid chatroomId))
                    {
                        return BadRequest("無效的聊天室ID");
                    }

                    var existingChatroom = await _dbContext.TB_Chat.FindAsync(chatroomId);
                    if (existingChatroom == null)
                    {
                        return NotFound();
                    }

                    existingChatroom.ChatName = chatroomData.ChatName;
                    existingChatroom.Content = JsonConvert.SerializeObject(chatroomData.Content);
                    await _dbContext.SaveChangesAsync();

                    chatroomID = chatroomData.ChatroomID;
                }
                return Ok(new { chatroomID });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// 刪除聊天室
        /// </summary>
        /// <param name="accountEmail"></param>
        /// <param name="chatroomId"></param>
        /// <returns></returns>
        [HttpDelete("{accountEmail}/{chatroomId}", Name = nameof(DeleteChatroomsByAccountEmail))]
        public async Task<IActionResult> DeleteChatroomsByAccountEmail(string accountEmail, string chatroomId)
        {
            if (!Guid.TryParse(chatroomId, out Guid outChatroomId))
            {
                return BadRequest("無效的聊天室ID");
            }

            var existingUserChatroom = await _dbContext.TB_UserChatroom.FindAsync(accountEmail, outChatroomId);
            if (existingUserChatroom == null)
            {
                return NotFound();
            }

            var existingChatroom = await _dbContext.TB_Chat.FindAsync(outChatroomId);
            if (existingChatroom == null)
            {
                return NotFound();
            }
            try
            {
                _dbContext.TB_UserChatroom.Remove(existingUserChatroom);
                _dbContext.TB_Chat.Remove(existingChatroom);
                await _dbContext.SaveChangesAsync();
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
