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

## 三、配置选项类

### 3.1 AzureSpeechOptions

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

### 3.2 AzureOpenAIOptions

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

## 四、REST API 端点

### 4.1 会话管理

#### POST /api/conversations

创建新会话。

**响应:**
```json
{
  "sessionId": "uuid-string",
  "createdAt": "2024-01-01T00:00:00Z"
}
```

#### GET /api/conversations/{sessionId}

获取会话信息和历史。

**响应:**
```json
{
  "sessionId": "uuid-string",
  "messages": [
    { "role": "user", "content": "你好", "timestamp": "..." },
    { "role": "assistant", "content": "你好！有什么可以帮助你的吗？", "timestamp": "..." }
  ],
  "createdAt": "...",
  "lastActiveAt": "..."
}
```

#### DELETE /api/conversations/{sessionId}

删除会话。

**响应:** `204 No Content`

### 4.2 语音对话（REST 备选方式）

#### POST /api/conversations/{sessionId}/speak

上传音频文件进行对话（非实时方式）。

**请求:** `multipart/form-data`
- `audio`: 音频文件（WAV/WebM）

**响应:**
```json
{
  "userText": "识别出的用户文本",
  "assistantText": "AI 回复的文本",
  "audioUrl": "/api/conversations/{sessionId}/audio/{audioId}"
}
```

### 4.3 健康检查

#### GET /health

**响应:**
```json
{
  "status": "Healthy",
  "checks": {
    "azureSpeech": "Healthy",
    "azureOpenAI": "Healthy"
  }
}
```

---

## 五、WebSocket（SignalR）消息协议

### Hub 路径: `/hubs/voice`

### 客户端 → 服务端

#### `StartSession`
```json
{ "language": "zh-CN" }
```
开始一个新的语音对话会话。服务端返回 `SessionStarted` 事件。

#### `SendAudio`
```json
{
  "sessionId": "uuid-string",
  "audioChunk": "<base64-encoded-audio-bytes>"
}
```
发送一段音频数据。可多次调用以流式发送。

#### `EndAudio`
```json
{
  "sessionId": "uuid-string"
}
```
标记音频发送完毕，触发后端处理管道。

#### `CancelProcessing`
```json
{
  "sessionId": "uuid-string"
}
```
取消当前处理（如用户想重新提问）。

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
  "confidence": 0.95,
  "isFinal": true
}
```

#### `AssistantTextChunk`
```json
{
  "sessionId": "uuid-string",
  "textChunk": "AI 回复的一部分文本",
  "isComplete": false
}
```
流式返回 AI 生成的文本。

#### `AudioChunk`
```json
{
  "sessionId": "uuid-string",
  "audioChunk": "<base64-encoded-audio-bytes>",
  "contentType": "audio/mp3",
  "isComplete": false
}
```
流式返回合成的音频数据。

#### `Error`
```json
{
  "sessionId": "uuid-string",
  "code": "STT_FAILED",
  "message": "语音识别失败，请重试"
}
```

### 错误码

| 错误码 | 说明 |
|--------|------|
| `STT_FAILED` | 语音识别失败 |
| `LLM_FAILED` | AI 对话服务失败 |
| `TTS_FAILED` | 语音合成失败 |
| `SESSION_NOT_FOUND` | 会话不存在 |
| `RATE_LIMITED` | 请求频率超限 |
| `AUDIO_TOO_LONG` | 音频超过最大时长限制 |
| `INTERNAL_ERROR` | 内部服务器错误 |
