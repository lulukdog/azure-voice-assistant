using System.Text.Json;
using VoiceAssistant.Core.Exceptions;

namespace VoiceAssistant.Api.Middleware;

public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, errorCode, message) = exception switch
        {
            SessionNotFoundException ex => (StatusCodes.Status404NotFound, ex.ErrorCode, ex.Message),
            AudioTooLongException ex => (StatusCodes.Status400BadRequest, ex.ErrorCode, ex.Message),
            SpeechRecognitionException ex => (StatusCodes.Status502BadGateway, ex.ErrorCode, ex.Message),
            ChatServiceException ex => (StatusCodes.Status502BadGateway, ex.ErrorCode, ex.Message),
            SpeechSynthesisException ex => (StatusCodes.Status502BadGateway, ex.ErrorCode, ex.Message),
            VoiceAssistantException ex => (StatusCodes.Status500InternalServerError, ex.ErrorCode, ex.Message),
            _ => (StatusCodes.Status500InternalServerError, "INTERNAL_ERROR", "An unexpected error occurred.")
        };

        if (exception is VoiceAssistantException)
        {
            logger.LogError(exception, "Voice assistant error: {ErrorCode} - {Message}", errorCode, exception.Message);
        }
        else
        {
            logger.LogError(exception, "Unhandled exception occurred.");
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var response = new { code = errorCode, message };
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
    }
}
