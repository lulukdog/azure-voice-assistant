# API 接口规范

## 概述

本文档定义了 Azure Voice Assistant 的所有对外接口和内部服务接口契约，是多 Agent 协作的核心共享文档。

---

## 一、C# 服务接口定义

### 1.1 ISpeechToTextService

```csharp
namespace VoiceAssistant.Core.Interfaces;

/// <summary>
/// 语音转文字服务接口
/// </summary>
public interface ISpeechToTextService
{
    /// <summary>
    /// 从音频流识别语音并返回文字
    /// </summary>
    /// <param name="audioStream">PCM 16kHz 16bit 音频流</param>
    /// <param name="language">识别语言，如 "zh-CN"</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>语音识别结果</returns>
    Task<SpeechRecognitionResult> RecognizeAsync(
        Stream audioStream,
        string language = "zh-CN",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 从音频字节数组识别语音
    /// </summary>
    Task<SpeechRecognitionResult> RecognizeAsync(
        byte[] audioData,
        string language = "zh-CN",
        CancellationToken cancellationToken = default);
}
```

### 1.2 IChatService

```csharp
namespace VoiceAssistant.Core.Interfaces;

/// <summary>
/// AI 对话服务接口
/// </summary>
public interface IChatService
{
    /// <summary>
    /// 发送消息并获取 AI 回复
    /// </summary>
    /// <param name="messages">对话历史</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>AI 回复文本</returns>
    Task<string> ChatAsync(
        IReadOnlyList<ConversationMessage> messages,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 发送消息并获取流式 AI 回复
    /// </summary>
    /// <param name="messages">对话历史</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>逐块返回的回复文本流</returns>
    IAsyncEnumerable<string> ChatStreamAsync(
        IReadOnlyList<ConversationMessage> messages,
        CancellationToken cancellationToken = default);
}
```

### 1.3 ITextToSpeechService

```csharp
namespace VoiceAssistant.Core.Interfaces;

/// <summary>
/// 文字转语音服务接口
/// </summary>
public interface ITextToSpeechService
{
    /// <summary>
    /// 将文本合成为语音音频
    /// </summary>
    /// <param name="text">要合成的文本</param>
    /// <param name="voiceName">语音角色名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>音频数据</returns>
    Task<AudioData> SynthesizeAsync(
        string text,
        string? voiceName = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 将文本合成为语音并写入流（流式）
    /// </summary>
    /// <param name="text">要合成的文本</param>
    /// <param name="outputStream">输出音频流</param>
    /// <param name="voiceName">语音角色名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SynthesizeToStreamAsync(
        string text,
        Stream outputStream,
        string? voiceName = null,
        CancellationToken cancellationToken = default);
}
```

### 1.4 IConversationPipeline

```csharp
namespace VoiceAssistant.Core.Interfaces;

/// <summary>
/// 对话处理管道：STT → LLM → TTS
/// </summary>
public interface IConversationPipeline
{
    /// <summary>
    /// 处理一轮完整对话：语音输入 → 文字 → AI 回复 → 语音输出
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="audioInput">用户语音输入</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>对话处理结果，包含识别文本、AI 回复和音频</returns>
    Task<ConversationTurnResult> ProcessAsync(
        string sessionId,
        Stream audioInput,
        CancellationToken cancellationToken = default);
}
```

### 1.5 ISessionManager

```csharp
namespace VoiceAssistant.Core.Interfaces;

/// <summary>
/// 会话管理服务接口
/// </summary>
public interface ISessionManager
{
    /// <summary>
    /// 创建新会话
    /// </summary>
    ConversationSession CreateSession(string? systemPrompt = null);

    /// <summary>
    /// 获取会话，不存在返回 null
    /// </summary>
    ConversationSession? GetSession(string sessionId);

    /// <summary>
    /// 获取会话，不存在则抛出 SessionNotFoundException
    /// </summary>
    ConversationSession GetSessionOrThrow(string sessionId);

    /// <summary>
    /// 添加消息到会话
    /// </summary>
    void AddMessage(string sessionId, ConversationMessage message);

    /// <summary>
    /// 删除会话
    /// </summary>
    bool RemoveSession(string sessionId);

    /// <summary>
    /// 获取所有活跃会话 ID
    /// </summary>
    IReadOnlyList<string> GetActiveSessionIds();
}
```

当前实现：`InMemorySessionManager`（基于 `ConcurrentDictionary`，Singleton 生命周期）。

---

## 二、领域模型

### 2.1 ConversationMessage

```csharp
namespace VoiceAssistant.Core.Models;

public class ConversationMessage
{
    /// <summary>
    /// 角色: "system", "user", "assistant"
    /// </summary>
    public required string Role { get; set; }

    /// <summary>
    /// 消息内容
    /// </summary>
    public required string Content { get; set; }

    /// <summary>
    /// 消息时间戳
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}
```

### 2.2 AudioData

```csharp
namespace VoiceAssistant.Core.Models;

public class AudioData
{
    /// <summary>
    /// 音频二进制数据
    /// </summary>
    public required byte[] Data { get; set; }

    /// <summary>
    /// 音频格式 MIME 类型，如 "audio/mp3", "audio/wav"
    /// </summary>
    public required string ContentType { get; set; }

    /// <summary>
    /// 音频时长（秒）
    /// </summary>
    public double? DurationSeconds { get; set; }
}
```

### 2.3 SpeechRecognitionResult

```csharp
namespace VoiceAssistant.Core.Models;

public class SpeechRecognitionResult
{
    /// <summary>
    /// 是否识别成功
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 识别出的文本
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// 识别置信度 (0.0 - 1.0)
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// 识别失败原因（如有）
    /// </summary>
    public string? ErrorMessage { get; set; }
}
```

### 2.4 ConversationSession

```csharp
namespace VoiceAssistant.Core.Models;

public class ConversationSession
{
    public required string SessionId { get; set; }
    public List<ConversationMessage> Messages { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastActiveAt { get; set; } = DateTimeOffset.UtcNow;
}
```

### 2.5 ConversationTurnResult

```csharp
namespace VoiceAssistant.Core.Models;

public class ConversationTurnResult
{
    /// <summary>
    /// 用户语音识别后的文本
    /// </summary>
    public required string UserText { get; set; }

    /// <summary>
    /// AI 回复的文本
    /// </summary>
    public required string AssistantText { get; set; }

    /// <summary>
    /// AI 回复的语音音频
    /// </summary>
    public required AudioData Audio { get; set; }
}
```

---

## 三、异常类型

所有业务异常继承自 `VoiceAssistantException`，包含 `ErrorCode` 属性用于 API 错误码映射：

```csharp
namespace VoiceAssistant.Core.Exceptions;

public class VoiceAssistantException : Exception
{
    public string ErrorCode { get; }
    public VoiceAssistantException(string errorCode, string message) : base(message)
    {
        ErrorCode = errorCode;
    }
    public VoiceAssistantException(string errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}

// 派生异常
public class SpeechRecognitionException   : VoiceAssistantException  // ErrorCode = "STT_FAILED"
public class ChatServiceException         : VoiceAssistantException  // ErrorCode = "LLM_FAILED"
public class SpeechSynthesisException     : VoiceAssistantException  // ErrorCode = "TTS_FAILED"
public class SessionNotFoundException     : VoiceAssistantException  // ErrorCode = "SESSION_NOT_FOUND"
public class AudioTooLongException        : VoiceAssistantException  // ErrorCode = "AUDIO_TOO_LONG"
```

---

## 四、配置选项类

### 4.1 AzureSpeechOptions

```csharp
namespace VoiceAssistant.Core.Options;

public class AzureSpeechOptions
{
    public const string SectionName = "AzureSpeech";

    public required string SubscriptionKey { get; set; }
    public required string Region { get; set; }
    public string RecognitionLanguage { get; set; } = "zh-CN";
    public string SynthesisVoiceName { get; set; } = "zh-CN-XiaoxiaoNeural";
    public string SynthesisOutputFormat { get; set; } = "Audio16Khz32KBitRateMonoMp3";
}
```

### 4.2 AzureOpenAIOptions

```csharp
namespace VoiceAssistant.Core.Options;

public class AzureOpenAIOptions
{
    public const string SectionName = "AzureOpenAI";

    public required string Endpoint { get; set; }
    public required string ApiKey { get; set; }
    public required string DeploymentName { get; set; }
    public int MaxTokens { get; set; } = 800;
    public double Temperature { get; set; } = 0.7;
    public string SystemPrompt { get; set; } = "你是一个友好的 AI 语音助手，请用简洁的中文回答问题。";
}
```

---

## 五、REST API 端点

所有响应均使用 ASP.NET Core 默认 camelCase JSON 序列化。

### 5.1 会话管理

#### POST /api/conversations

创建新会话。

**响应:** `200 OK`
```json
{
  "sessionId": "a1b2c3d4e5f6...",
  "createdAt": "2024-01-01T00:00:00Z"
}
```

#### GET /api/conversations/{sessionId}

获取会话摘要信息。

**响应:** `200 OK`
```json
{
  "sessionId": "a1b2c3d4e5f6...",
  "createdAt": "2024-01-01T00:00:00+00:00",
  "lastActiveAt": "2024-01-01T00:01:00+00:00",
  "messageCount": 4
}
```

**错误响应:** `404 Not Found`
```json
{
  "code": "SESSION_NOT_FOUND",
  "message": "会话 {sessionId} 不存在"
}
```

#### DELETE /api/conversations/{sessionId}

删除会话。

**响应:** `204 No Content`

**错误响应:** `404 Not Found`（同上）

### 5.2 语音对话（REST 备选方式）

#### POST /api/conversations/{sessionId}/speak

上传音频文件进行对话（非实时方式）。

**请求:** `multipart/form-data`
- `audio`: 音频文件（WAV/WebM）

**响应:** `200 OK`
```json
{
  "userText": "识别出的用户文本",
  "assistantText": "AI 回复的文本",
  "audioBase64": "<base64-encoded-audio-bytes>",
  "contentType": "audio/mp3"
}
```

**错误响应:**

| 状态码 | 条件 | 响应体 |
|--------|------|--------|
| `400` | 未上传音频文件 | `{ "message": "请上传音频文件" }` |
| `400` | 音频超时 | `{ "errorCode": "AUDIO_TOO_LONG", "message": "..." }` |
| `404` | 会话不存在 | `{ "errorCode": "SESSION_NOT_FOUND", "message": "..." }` |
| `502` | STT/LLM/TTS 失败 | `{ "errorCode": "STT_FAILED\|LLM_FAILED\|TTS_FAILED", "message": "..." }` |
| `500` | 未知错误 | `{ "code": "INTERNAL_ERROR", "message": "处理失败" }` |

> **注意**: Speak 端点内部 catch 了异常并返回 `{ "errorCode", "message" }`，而全局 ExceptionHandlingMiddleware 返回 `{ "code", "message" }`。两条路径的错误码值相同，但 JSON 字段名不同。

### 5.3 健康检查

#### GET /health

**响应:** `200 OK`

```
Healthy
```

> 返回纯文本（非 JSON），由 ASP.NET Core `MapHealthChecks` 默认行为决定。

---

## 六、WebSocket（SignalR）消息协议

### Hub 路径: `/hubs/voice`

> **重要**: SignalR 默认使用 **camelCase** 进行 JSON 序列化。服务端代码中的 PascalCase 属性名（如 `SessionId`）在客户端接收时变为 camelCase（如 `sessionId`）。

### 客户端 → 服务端

#### `StartSession`

```
参数: language (string, 默认 "zh-CN")
```
开始一个新的语音对话会话。服务端返回 `SessionStarted` 事件。

#### `SendAudio`

```
参数: sessionId (string), audioChunkBase64 (string)
```
发送完整音频数据（Base64 编码）。服务端处理完整 STT → LLM → TTS 管道后依次返回 `RecognitionResult`、`AssistantTextChunk`、`AudioChunk`。

#### `EndSession`

```
参数: sessionId (string)
```
结束会话并移除会话数据。服务端返回 `SessionEnded` 事件。

### 服务端 → 客户端

#### `SessionStarted`
```json
{
  "sessionId": "uuid-string"
}
```

#### `RecognitionResult`
```json
{
  "sessionId": "uuid-string",
  "text": "识别出的文本",
  "confidence": 1.0,
  "isFinal": true
}
```

#### `AssistantTextChunk`
```json
{
  "sessionId": "uuid-string",
  "textChunk": "AI 回复的文本",
  "isComplete": true
}
```

#### `AudioChunk`
```json
{
  "sessionId": "uuid-string",
  "audioChunk": "<base64-encoded-audio-bytes>",
  "contentType": "audio/mp3",
  "isComplete": true
}
```

#### `SessionEnded`
```json
{
  "sessionId": "uuid-string"
}
```

#### `Error`
```json
{
  "sessionId": "uuid-string",
  "code": "STT_FAILED",
  "message": "语音识别失败，请重试"
}
```

### 连接断开行为

当客户端断开 WebSocket 连接时，`OnDisconnectedAsync` 仅清除 ConnectionId → SessionId 的映射关系，**不会删除会话数据**。会话仍可通过 REST API 访问。

### 错误码

| 错误码 | 说明 | HTTP 状态码 |
|--------|------|-------------|
| `STT_FAILED` | 语音识别失败 | 502 |
| `LLM_FAILED` | AI 对话服务失败 | 502 |
| `TTS_FAILED` | 语音合成失败 | 502 |
| `SESSION_NOT_FOUND` | 会话不存在 | 404 |
| `AUDIO_TOO_LONG` | 音频超过最大时长限制（60 秒） | 400 |
| `INTERNAL_ERROR` | 内部服务器错误 | 500 |

---

## 七、DI 注册总览

```csharp
// Program.cs
builder.Services.AddCore();           // ISessionManager (Singleton), IConversationPipeline (Scoped)
builder.Services.AddInfrastructure(    // ISpeechToTextService, IChatService, ITextToSpeechService (均 Scoped)
    builder.Configuration);            // + Options 绑定 (ValidateOnStart) + HealthCheck
```

| 服务 | 生命周期 | 注册位置 |
|------|----------|----------|
| `ISessionManager` → `InMemorySessionManager` | Singleton | Core |
| `IConversationPipeline` → `ConversationPipeline` | Scoped | Core |
| `ISpeechToTextService` → `AzureSpeechToTextService` | Scoped | Infrastructure |
| `IChatService` → `AzureOpenAIChatService` | Scoped | Infrastructure |
| `ITextToSpeechService` → `AzureTextToSpeechService` | Scoped | Infrastructure |
| `AzureSpeechOptions` | Options (ValidateOnStart) | Infrastructure |
| `AzureOpenAIOptions` | Options (ValidateOnStart) | Infrastructure |
