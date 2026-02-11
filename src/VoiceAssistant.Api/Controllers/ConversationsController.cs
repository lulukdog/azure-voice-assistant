using Microsoft.AspNetCore.Mvc;
using VoiceAssistant.Core.Exceptions;
using VoiceAssistant.Core.Interfaces;
using VoiceAssistant.Core.Models;

namespace VoiceAssistant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConversationsController(
    IConversationPipeline pipeline,
    ISessionManager sessionManager,
    ILogger<ConversationsController> logger) : ControllerBase
{
    /// <summary>
    /// 创建新会话
    /// </summary>
    [HttpPost]
    public IActionResult CreateSession()
    {
        var session = sessionManager.CreateSession();
        logger.LogInformation("Created new session: {SessionId}", session.SessionId);

        return Ok(new
        {
            session.SessionId,
            session.CreatedAt
        });
    }

    /// <summary>
    /// 获取会话详情
    /// </summary>
    [HttpGet("{sessionId}")]
    public IActionResult GetSession(string sessionId)
    {
        var session = sessionManager.GetSession(sessionId);
        if (session is null)
            return NotFound(new { Code = "SESSION_NOT_FOUND", Message = $"会话 {sessionId} 不存在" });

        return Ok(new
        {
            session.SessionId,
            session.CreatedAt,
            session.LastActiveAt,
            MessageCount = session.Messages.Count
        });
    }

    /// <summary>
    /// 删除会话
    /// </summary>
    [HttpDelete("{sessionId}")]
    public IActionResult DeleteSession(string sessionId)
    {
        var removed = sessionManager.RemoveSession(sessionId);
        if (!removed)
            return NotFound(new { Code = "SESSION_NOT_FOUND", Message = $"会话 {sessionId} 不存在" });

        logger.LogInformation("Deleted session: {SessionId}", sessionId);
        return NoContent();
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
        catch (SessionNotFoundException ex)
        {
            logger.LogWarning(ex, "Session not found: {SessionId}", sessionId);
            return NotFound(new { ex.ErrorCode, ex.Message });
        }
        catch (AudioTooLongException ex)
        {
            logger.LogWarning(ex, "Audio too long for session {SessionId}", sessionId);
            return BadRequest(new { ex.ErrorCode, ex.Message });
        }
        catch (VoiceAssistantException ex)
        {
            logger.LogError(ex, "Upstream service error for session {SessionId}: {ErrorCode}", sessionId, ex.ErrorCode);
            return StatusCode(502, new { ex.ErrorCode, ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing speak for session {SessionId}", sessionId);
            return StatusCode(500, new { Code = "INTERNAL_ERROR", Message = "处理失败" });
        }
    }
}
