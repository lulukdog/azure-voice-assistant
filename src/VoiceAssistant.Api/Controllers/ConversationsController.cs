using Microsoft.AspNetCore.Mvc;
using VoiceAssistant.Core.Interfaces;

namespace VoiceAssistant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConversationsController(
    IConversationPipeline pipeline,
    ILogger<ConversationsController> logger) : ControllerBase
{
    /// <summary>
    /// 创建新会话
    /// </summary>
    [HttpPost]
    public IActionResult CreateSession()
    {
        var sessionId = Guid.NewGuid().ToString("N");
        logger.LogInformation("Created new session: {SessionId}", sessionId);

        return Ok(new
        {
            SessionId = sessionId,
            CreatedAt = DateTimeOffset.UtcNow
        });
    }

    /// <summary>
    /// 上传音频进行对话（REST 备选方式）
    /// </summary>
    [HttpPost("{sessionId}/speak")]
    public async Task<IActionResult> Speak(string sessionId, IFormFile audio, CancellationToken cancellationToken)
    {
        if (audio == null || audio.Length == 0)
        {
            return BadRequest(new { Message = "请上传音频文件" });
        }

        try
        {
            using var stream = audio.OpenReadStream();
            var result = await pipeline.ProcessAsync(sessionId, stream, cancellationToken);

            return Ok(new
            {
                result.UserText,
                result.AssistantText,
                AudioBase64 = Convert.ToBase64String(result.Audio.Data),
                result.Audio.ContentType
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing speak for session {SessionId}", sessionId);
            return StatusCode(500, new { Message = "处理失败", Error = ex.Message });
        }
    }
}
