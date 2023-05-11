using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenAIAPI.Models.Partial;
using OpenAIAPI.Services;
using System.Globalization;
using OpenAIAPI.Models;

namespace OpenAIAPI.Controllers
{
    [Authorize]
    [Route("[controller]")]
    [ApiController]
    public class TextManagementController : ControllerBase
    {
        private readonly OpenAIContext _dbContext;
        private readonly IOpenAIHttpService _openAIHttpService;
        private readonly IOpenAIEmbeddings _openAIEmbeddings;
        private readonly IOpenAIGPTToken _openAIGPTToken;

        public TextManagementController(OpenAIContext dbContext, IOpenAIHttpService openAIHttpService, IOpenAIEmbeddings openAIEmbeddings, IOpenAIGPTToken openAIGPTToken)
        {
            _dbContext = dbContext;
            _openAIHttpService = openAIHttpService;
            _openAIEmbeddings = openAIEmbeddings;
            _openAIGPTToken = openAIGPTToken;
        }

        private List<ViewNodeModel> GetFolderHierarchy(List<TB_Folders> allFolders, List<TB_Texts> allTexts, Guid? parentId)
        {
            List<ViewNodeModel> nodes = new List<ViewNodeModel>();

            var childFolders = allFolders.Where(f => f.ParentId == parentId).ToList();
            foreach (var childFolder in childFolders)
            {
                var folderViewModel = new ViewNodeModel
                {
                    Id = childFolder.Id.ToString().ToLower(),
                    Name = childFolder.Name,
                    ParentId = childFolder.ParentId != null ? childFolder.ParentId.ToString().ToLower() : null,
                    NodeType = "folder"
                };

                folderViewModel.Children.AddRange(GetFolderHierarchy(allFolders, allTexts, childFolder.Id));
                nodes.Add(folderViewModel);
            }

            var childTexts = allTexts.Where(t => t.FolderId == parentId).ToList();
            foreach (var text in childTexts)
            {
                nodes.Add(new ViewNodeModel
                {
                    Id = text.Id.ToString().ToLower(),
                    Name = text.Name,
                    ParentId = text.FolderId != null ? text.FolderId.ToString().ToLower() : null,
                    NodeType = "text"
                });
            }

            return nodes;
        }

        /// <summary>
        /// 取得整個資料結構
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetAllFolders", Name = nameof(GetAllFolders))]
        [Produces("application/json")]
        public async Task<ActionResult<List<ViewNodeModel>>> GetAllFolders()
        {
            var allFolders = await _dbContext.TB_Folders.ToListAsync();
            var allTexts = await _dbContext.TB_Texts.ToListAsync();
            var folderHierarchy = GetFolderHierarchy(allFolders, allTexts, null);

            return Ok(folderHierarchy);
        }

        /// <summary>
        /// 建立資料夾
        /// </summary>
        /// <param name="folderData"></param>
        /// <returns></returns>
        [HttpPost("CreateFolder", Name = nameof(CreateFolder))]
        [Produces("application/json")]
        public async Task<ActionResult<TB_Folders>> CreateFolder([FromBody] CreateFolderModal folderData)
        {
            Guid? newfolerParentGuid = null;
            if (!string.IsNullOrEmpty(folderData.ParentId))
            {
                if (!Guid.TryParse(folderData.ParentId, out Guid folderParentGuid))
                {
                    return BadRequest("無效的父資料夾");
                }

                newfolerParentGuid = folderParentGuid;

                var parentFolder = await _dbContext.TB_Folders.FindAsync(newfolerParentGuid);
                if (parentFolder == null)
                {
                    return NotFound("無此父資料夾");
                }
            }

            if (string.IsNullOrEmpty(folderData.Name))
            {
                return BadRequest("新資料夾名稱不能為空");
            }

            var newFolder = new TB_Folders
            {
                Name = folderData.Name,
                ParentId = newfolerParentGuid,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _dbContext.TB_Folders.Add(newFolder);
            await _dbContext.SaveChangesAsync();

            return CreatedAtAction(nameof(CreateFolder), new { id = newFolder.Id }, newFolder);
        }

        /// <summary>
        /// 資料夾重新命名
        /// </summary>
        /// <param name="id"></param>
        /// <param name="renameData"></param>
        /// <returns></returns>
        [HttpPatch("RenameFolder/{id}", Name = nameof(RenameFolder))]
        [Produces("application/json")]
        public async Task<ActionResult<TB_Folders>> RenameFolder([FromRoute] string id, [FromBody] RenameFolderModal renameData)
        {
            if (string.IsNullOrEmpty(id) || !Guid.TryParse(id, out Guid folderId))
            {
                return BadRequest("無效的資料夾");
            }

            var folder = await _dbContext.TB_Folders.FindAsync(folderId);
            if (folder == null)
            {
                return NotFound("無此資料夾");
            }

            if (string.IsNullOrEmpty(renameData.NewName))
            {
                return BadRequest("資料夾名稱不能為空");
            }

            folder.Name = renameData.NewName;
            folder.UpdatedAt = DateTime.Now;
            //_dbContext.TB_Folders.Update(folder);
            await _dbContext.SaveChangesAsync();

            return Ok(folder);
        }

        /// <summary>
        /// 刪除資料夾
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete("DeleteFolder/{id}", Name = nameof(DeleteFolder))]
        [Produces("application/json")]
        public async Task<ActionResult> DeleteFolder([FromRoute] string id)
        {
            if (string.IsNullOrEmpty(id) || !Guid.TryParse(id, out Guid folderId))
            {
                return BadRequest("無效的資料夾");
            }

            var folder = await _dbContext.TB_Folders.FindAsync(folderId);
            if (folder == null)
            {
                return NotFound("無此資料夾");
            }

            await DeleteFolderWithSubFolders(folderId);
            await _dbContext.SaveChangesAsync();

            return NoContent();
        }

        private async Task DeleteFolderWithSubFolders(Guid folderId)
        {
            var folderToDelete = await _dbContext.TB_Folders.FindAsync(folderId);
            if (folderToDelete == null)
            {
                return;
            }

            // 刪除資料夾中的所有檔案
            var filesToDelete = await _dbContext.TB_Texts.Where(t => t.FolderId == folderId).ToListAsync();
            var embeddingIdsToDelete = filesToDelete.Select(t => t.EmbeddingId).ToList();
            var embeddingsToDelete = await _dbContext.TB_Embeddings.Where(e => embeddingIdsToDelete.Contains(e.Id)).ToListAsync();
            _dbContext.TB_Embeddings.RemoveRange(embeddingsToDelete);
            _dbContext.TB_Texts.RemoveRange(filesToDelete);

            // 刪除子資料夾
            var subFolders = await _dbContext.TB_Folders.Where(f => f.ParentId == folderId).ToListAsync();
            foreach (var subFolder in subFolders)
            {
                await DeleteFolderWithSubFolders(subFolder.Id);
            }

            _dbContext.TB_Folders.Remove(folderToDelete);
        }

        /// <summary>
        /// 建立文件
        /// </summary>
        /// <param name="textData"></param>
        /// <returns></returns>
        [HttpPost("CreateText", Name = nameof(CreateText))]
        [Produces("application/json")]
        public async Task<ActionResult<TB_Texts>> CreateText([FromBody] CreateTextModal textData)
        {
            Guid? newTextFolderGuid = null;
            if (!string.IsNullOrEmpty(textData.FolderId))
            {
                if (!Guid.TryParse(textData.FolderId, out Guid textFolderGuid))
                {
                    return BadRequest("無效的資料夾");
                }

                newTextFolderGuid = textFolderGuid;

                var parentFolder = await _dbContext.TB_Folders.FindAsync(newTextFolderGuid);
                if (parentFolder == null)
                {
                    return NotFound("無此資料夾");
                }
            }

            if (string.IsNullOrEmpty(textData.Name))
            {
                return BadRequest("新文件名稱不能為空");
            }

            var newTextFile = new TB_Texts
            {
                Name = textData.Name,
                FolderId = newTextFolderGuid,
                //EmbeddingId = Guid.NewGuid(),
                TextContent = textData.TextContent,
                TextHtml = textData.TextHtml,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            // 請注意，您需要根據您的需求設置 EmbeddingId，此處暫時將其設置為 Guid.NewGuid()
            //newTextFile.EmbeddingId = Guid.NewGuid();

            _dbContext.TB_Texts.Add(newTextFile);
            await _dbContext.SaveChangesAsync();

            return CreatedAtAction(nameof(CreateText), new { id = newTextFile.Id }, newTextFile);
        }

        /// <summary>
        /// 文件重新命名
        /// </summary>
        /// <param name="id"></param>
        /// <param name="renameData"></param>
        /// <returns></returns>
        [HttpPatch("RenameText/{id}", Name = nameof(RenameText))]
        [Produces("application/json")]
        public async Task<ActionResult> RenameText([FromRoute] string id, [FromBody] RenameTextModal renameData)
        {
            if (string.IsNullOrEmpty(id) || !Guid.TryParse(id, out Guid textId))
            {
                return BadRequest("無效的文件");
            }

            var textFile = await _dbContext.TB_Texts.FindAsync(textId);
            if (textFile == null)
            {
                return NotFound("無此文件");
            }

            if (string.IsNullOrEmpty(renameData.NewName))
            {
                return BadRequest("文件名稱不能為空");
            }

            textFile.Name = renameData.NewName;
            textFile.UpdatedAt = DateTime.Now;
            //_dbContext.TB_Texts.Update(textFile);
            await _dbContext.SaveChangesAsync();

            return NoContent();
        }

        /// <summary>
        /// 刪除文件
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete("DeleteText/{id}", Name = nameof(DeleteText))]
        [Produces("application/json")]
        public async Task<ActionResult> DeleteText([FromRoute] string id)
        {
            if (string.IsNullOrEmpty(id) || !Guid.TryParse(id, out Guid textId))
            {
                return BadRequest("無效的文件");
            }

            var textFile = await _dbContext.TB_Texts.FindAsync(textId);
            if (textFile == null)
            {
                return NotFound("無此文件");
            }
            var embeddingFile = await _dbContext.TB_Embeddings.FindAsync(textFile.EmbeddingId);
            if (embeddingFile != null)
            {
                _dbContext.TB_Embeddings.Remove(embeddingFile);
            }
            _dbContext.TB_Texts.Remove(textFile);
            await _dbContext.SaveChangesAsync();

            return NoContent();
        }

        /// <summary>
        /// 取得文件
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("GetText/{id}", Name = nameof(GetText))]
        [Produces("application/json")]
        public async Task<ActionResult<ViewTextModel>> GetText([FromRoute] string id)
        {
            if (string.IsNullOrEmpty(id) || !Guid.TryParse(id, out Guid textId))
            {
                return BadRequest("無效的文件");
            }

            var text = await _dbContext.TB_Texts.FindAsync(textId);
            if (text == null)
            {
                return NotFound("無此文件");
            }

            var viewTextModel = new ViewTextModel
            {
                Id = text.Id.ToString().ToLower(),
                Name = text.Name,
                FolderId = text.FolderId != null ? text.FolderId.ToString().ToLower() : null,
                TextContent = text.TextContent,
                TextHtml = text.TextHtml,
                //CreatedAt = text.CreatedAt,
                //UpdatedAt = text.UpdatedAt
            };

            return Ok(viewTextModel);
        }

        /// <summary>
        /// 更新文件
        /// </summary>
        /// <param name="id"></param>
        /// <param name="updateTextModel"></param>
        /// <returns></returns>
        [HttpPatch("UpdateText/{id}", Name = nameof(UpdateText))]
        [Produces("application/json")]
        public async Task<ActionResult> UpdateText([FromRoute] string id, [FromBody] UpdateTextModel updateTextModel)
        {
            if (string.IsNullOrEmpty(id) || !Guid.TryParse(id, out Guid textId))
            {
                return BadRequest("無效的文件");
            }

            var text = await _dbContext.TB_Texts.FindAsync(textId);
            if (text == null)
            {
                return NotFound("無此文件");
            }

            Guid? embeddingGuid = null;

            if (!_openAIEmbeddings.CheckTextNullOrEmpty(updateTextModel.TextContent))
            {
                if (_openAIEmbeddings.CheckTextTokenizer(updateTextModel.TextContent))
                {
                    return BadRequest("文本超過大小");
                }

                var filterText = _openAIGPTToken.FilterSpecialCharactersAndWhiteSpace(updateTextModel.TextContent);
                var inputVector = await _openAIHttpService.GetTextEmbeddingVector(filterText);

                if (text.EmbeddingId != null)
                {
                    var embeddingData = await _dbContext.TB_Embeddings.FirstOrDefaultAsync(embedding => embedding.Id == text.EmbeddingId);
                    if (embeddingData != null)
                    {
                        embeddingData.Vector = string.Join(',', inputVector.Select(x => x.ToString(CultureInfo.InvariantCulture)));
                    }
                    await _dbContext.SaveChangesAsync();
                    embeddingGuid = text.EmbeddingId;
                }
                else
                {
                    var newTB_Embedding = new TB_Embeddings
                    {
                        Vector = string.Join(',', inputVector.Select(x => x.ToString(CultureInfo.InvariantCulture)))
                    };

                    _dbContext.TB_Embeddings.Add(newTB_Embedding);
                    await _dbContext.SaveChangesAsync();
                    embeddingGuid = newTB_Embedding.Id;
                }
            }

            text.EmbeddingId = embeddingGuid;
            text.TextContent = updateTextModel.TextContent;
            text.TextHtml = updateTextModel.TextHtml;
            text.UpdatedAt = DateTime.Now;

            await _dbContext.SaveChangesAsync();

            return NoContent();
        }


    }
}
