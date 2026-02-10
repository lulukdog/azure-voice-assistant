# 系统架构文档

## 项目概述

Azure Voice Assistant 是一个基于 Azure 云服务的语音对话系统。用户通过 Web 浏览器进行语音交互，系统将语音转为文字，经 AI 大模型处理后，再将回复转为语音返回给用户。

## 技术栈

| 层级 | 技术选型 |
|------|----------|
| 语言 | C# .NET 10 |
| Web 框架 | ASP.NET Core 10 |
| 语音识别 (STT) | Azure Cognitive Services Speech SDK |
| 大模型 (LLM) | Azure OpenAI Service (GPT-4o) |
| 语音合成 (TTS) | Azure Cognitive Services Speech SDK |
| 前端 | 原生 HTML/JS + Web Audio API |
| 容器化 | Docker |
| 编排部署 | Azure Kubernetes Service (AKS) |

## 系统架构图

```
┌─────────────────────────────────────────────────────┐
│                    Web Browser                       │
│  ┌──────────┐  ┌──────────┐  ┌──────────────────┐  │
│  │ 录音控件  │  │ 音频播放  │  │ WebSocket Client │  │
│  └────┬─────┘  └─────▲────┘  └────────┬─────────┘  │
│       │              │                 │             │
└───────┼──────────────┼─────────────────┼─────────────┘
        │              │                 │
        ▼              │                 ▼
┌─────────────────────────────────────────────────────┐
│              VoiceAssistant.Api                       │
│  ┌──────────────────────────────────────────────┐   │
│  │         WebSocket / REST Endpoint             │   │
│  └──────────────────┬───────────────────────────┘   │
│                     │                                │
│  ┌──────────────────▼───────────────────────────┐   │
│  │          Conversation Pipeline                 │   │
│  │                                               │   │
│  │  ┌─────────┐  ┌─────────┐  ┌─────────┐      │   │
│  │  │  STT    │─▶│  LLM    │─▶│  TTS    │      │   │
│  │  │ Service │  │ Service │  │ Service │      │   │
│  │  └────┬────┘  └────┬────┘  └────┬────┘      │   │
│  │       │            │            │             │   │
│  └───────┼────────────┼────────────┼─────────────┘   │
│          │            │            │                  │
└──────────┼────────────┼────────────┼──────────────────┘
           │            │            │
           ▼            ▼            ▼
    ┌────────────┐ ┌──────────┐ ┌────────────┐
    │ Azure      │ │ Azure    │ │ Azure      │
    │ Speech     │ │ OpenAI   │ │ Speech     │
    │ Service    │ │ Service  │ │ Service    │
    │ (STT)      │ │ (GPT-4o) │ │ (TTS)      │
    └────────────┘ └──────────┘ └────────────┘
```

## 数据流

### 语音对话完整流程

```
1. 用户在浏览器中按住按钮录音
2. 浏览器通过 WebSocket 将音频流发送到后端
3. 后端将音频流转发给 Azure STT 服务，获取文字
4. 将识别出的文字发送给 Azure OpenAI，获取 AI 回复
5. 将 AI 回复文字发送给 Azure TTS 服务，获取音频流
6. 通过 WebSocket 将音频流返回浏览器
7. 浏览器播放返回的音频
```

### 通信协议

- **浏览器 ↔ 后端**: WebSocket（双向实时音频流）+ REST API（会话管理）
- **后端 ↔ Azure STT**: Azure Speech SDK（gRPC）
- **后端 ↔ Azure OpenAI**: Azure OpenAI SDK（HTTPS）
- **后端 ↔ Azure TTS**: Azure Speech SDK（gRPC）

## 项目结构

```
azure-voice-assistant/
├── docs/                          # 项目文档
│   ├── ARCHITECTURE.md            # 系统架构文档（本文件）
│   ├── REQUIREMENTS.md            # 需求文档
│   ├── AGENT_GUIDE.md             # 多 Agent 协作指南
│   ├── API_SPEC.md                # API 接口规范
│   └── DEVELOPMENT.md             # 开发指南
├── src/
│   ├── VoiceAssistant.Api/        # Web API 层 - 端点、中间件、WebSocket
│   ├── VoiceAssistant.Core/       # 核心业务层 - 接口定义、领域模型、Pipeline
│   ├── VoiceAssistant.Infrastructure/ # 基础设施层 - Azure 服务集成实现
│   └── VoiceAssistant.Web/        # 静态前端 - HTML/JS/CSS
├── tests/
│   ├── VoiceAssistant.Api.Tests/
│   ├── VoiceAssistant.Core.Tests/
│   └── VoiceAssistant.Infrastructure.Tests/
├── deploy/
│   ├── docker/                    # Dockerfile
│   └── k8s/                       # Kubernetes 部署清单
├── scripts/                       # 辅助脚本
└── VoiceAssistant.sln             # 解决方案文件
```

## 分层设计

### VoiceAssistant.Core（核心层）

职责：定义业务接口和领域模型，不依赖任何外部实现。

```
VoiceAssistant.Core/
├── Interfaces/
│   ├── ISpeechToTextService.cs    # STT 服务接口
│   ├── ITextToSpeechService.cs    # TTS 服务接口
│   ├── IChatService.cs            # LLM 对话服务接口
│   └── IConversationPipeline.cs   # 对话管道接口
├── Models/
│   ├── ConversationMessage.cs     # 对话消息模型
│   ├── AudioData.cs               # 音频数据模型
│   ├── SpeechRecognitionResult.cs # 语音识别结果
│   └── ConversationSession.cs     # 会话模型
├── Options/
│   ├── AzureSpeechOptions.cs      # Azure Speech 配置
│   └── AzureOpenAIOptions.cs      # Azure OpenAI 配置
└── Pipeline/
    └── ConversationPipeline.cs    # 对话管道实现（STT → LLM → TTS）
```

### VoiceAssistant.Infrastructure（基础设施层）

职责：实现 Core 层定义的接口，封装 Azure SDK 调用。

```
VoiceAssistant.Infrastructure/
├── Azure/
│   ├── AzureSpeechToTextService.cs   # Azure STT 实现
│   ├── AzureTextToSpeechService.cs   # Azure TTS 实现
│   └── AzureOpenAIChatService.cs     # Azure OpenAI 实现
└── DependencyInjection.cs            # DI 注册扩展方法
```

### VoiceAssistant.Api（API 层）

职责：暴露 HTTP/WebSocket 端点，处理请求路由和中间件。

```
VoiceAssistant.Api/
├── Controllers/
│   └── ConversationController.cs  # REST API 端点
├── Hubs/
│   └── VoiceHub.cs                # WebSocket/SignalR Hub
├── Middleware/
│   └── ExceptionHandlingMiddleware.cs
├── Program.cs
├── appsettings.json
└── appsettings.Development.json
```

### VoiceAssistant.Web（前端）

职责：提供 Web 界面，处理浏览器端音频录制和播放。

```
VoiceAssistant.Web/
├── index.html
├── css/
│   └── styles.css
└── js/
    ├── app.js                     # 主应用逻辑
    ├── audio-recorder.js          # 音频录制模块
    ├── audio-player.js            # 音频播放模块
    └── websocket-client.js        # WebSocket 通信模块
```

## 关键设计决策

### 1. 使用 WebSocket（SignalR）而非纯 REST

语音对话需要实时双向通信，WebSocket 比 REST 轮询更适合音频流传输。采用 SignalR 可以获得自动重连、分组管理等开箱即用的功能。

### 2. 管道模式处理对话

将 STT → LLM → TTS 的处理流程抽象为 `IConversationPipeline`，便于：
- 独立测试每个环节
- 灵活替换实现（如切换不同 LLM 提供商）
- 增加中间处理步骤（如内容审核、日志记录）

### 3. 音频格式约定

- 浏览器 → 服务端：WebM/Opus 或 PCM 16kHz 16bit
- 服务端 → Azure STT：PCM 16kHz 16bit（SDK 要求）
- Azure TTS → 服务端 → 浏览器：MP3 或 PCM

### 4. 部署策略

- 本地开发：`dotnet run` 直接启动
- Docker：多阶段构建镜像
- 生产环境：AKS 部署，支持水平扩展
