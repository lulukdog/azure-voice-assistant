# 多 Agent 协作指南

## 概述

本项目设计为多个 AI Agent 可以并行协作开发。每个 Agent 负责不同的模块或层级，通过明确的接口契约和文件边界避免冲突。

## 项目上下文（每个 Agent 必读）

在开始任何开发任务之前，Agent 必须阅读以下文档：

1. **本文档** (`docs/AGENT_GUIDE.md`) — 了解协作规则
2. **架构文档** (`docs/ARCHITECTURE.md`) — 理解系统分层和数据流
3. **需求文档** (`docs/REQUIREMENTS.md`) — 理解功能目标
4. **API 规范** (`docs/API_SPEC.md`) — 理解接口契约
5. **开发指南** (`docs/DEVELOPMENT.md`) — 了解编码规范和运行方式
6. **部署指南** (`docs/DEPLOYMENT.md`) — 了解 Docker/AKS 部署流程

## Agent 角色分工

### Agent A: Core 层开发

**负责目录**: `src/VoiceAssistant.Core/`

**职责**:
- 定义所有服务接口（`ISpeechToTextService`, `ITextToSpeechService`, `IChatService`, `IConversationPipeline`, `ISessionManager`）
- 定义领域模型（`ConversationMessage`, `AudioData`, `SpeechRecognitionResult`, `ConversationSession`, `ConversationTurnResult`）
- 定义配置选项类（`AzureSpeechOptions`, `AzureOpenAIOptions`）
- 定义异常类型层次（`VoiceAssistantException` 及其子类）
- 实现 `ConversationPipeline`（编排 STT → LLM → TTS 流程）
- 实现 `InMemorySessionManager`（基于 `ConcurrentDictionary` 的会话管理）

**不可修改**:
- 其他 Agent 负责的目录

**输出契约**:
- 所有接口定义在 `Interfaces/` 目录
- 所有模型定义在 `Models/` 目录
- 所有配置类已定义在 `Options/` 目录
- 其他 Agent 可以直接引用 Core 层的类型

---

### Agent B: Infrastructure 层开发

**负责目录**: `src/VoiceAssistant.Infrastructure/`

**职责**:
- 实现 `ISpeechToTextService` → `AzureSpeechToTextService`
- 实现 `ITextToSpeechService` → `AzureTextToSpeechService`
- 实现 `IChatService` → `AzureOpenAIChatService`
- 实现 `AzureServicesHealthCheck`（健康检查）
- 编写 DI 注册扩展方法 `DependencyInjection.cs`

**前置依赖**:
- 必须等待 Agent A 完成 Core 层接口定义后再开始实现
- 或按照 `API_SPEC.md` 中预定义的接口签名开始实现

**不可修改**:
- `src/VoiceAssistant.Core/` 目录（如需修改接口，提出变更请求）

---

### Agent C: API 层开发

**负责目录**: `src/VoiceAssistant.Api/`

**职责**:
- 实现 REST API 端点（`ConversationsController`）
- 实现 WebSocket Hub（`VoiceHub`）
- 实现中间件（异常处理、请求日志）
- 配置 `Program.cs`（DI 注册、中间件管道、SignalR）

**前置依赖**:
- 依赖 Core 层的接口和模型
- 依赖 Infrastructure 层的 DI 注册

---

### Agent D: 前端开发

**负责目录**: `src/VoiceAssistant.Web/`

**职责**:
- 实现录音界面和音频播放
- 实现 WebSocket 客户端通信
- 实现对话历史界面
- 实现状态管理和 UI 反馈

**前置依赖**:
- 依赖 `API_SPEC.md` 中定义的 WebSocket 消息格式和 REST 端点

---

### Agent E: 测试开发

**负责目录**: `tests/`

**职责**:
- 为 Core 层编写单元测试（`VoiceAssistant.Core.Tests`）
- 为 Infrastructure 层编写单元测试（`VoiceAssistant.Infrastructure.Tests`）
- 为 API 层编写单元测试（`VoiceAssistant.Api.Tests`）
- 编写集成测试（`VoiceAssistant.IntegrationTests`）— 使用 `WebApplicationFactory<Program>` 端到端验证 REST API、SignalR Hub、健康检查和异常中间件

---

### Agent F: 部署与运维

**负责目录**: `deploy/`, `scripts/`

**职责**:
- 编写 Dockerfile（多阶段构建）
- 编写 Kubernetes 部署清单
- 编写辅助脚本（构建、部署、本地运行）

## 协作规则

### 规则 1: 文件所有权

每个 Agent 只修改自己负责目录中的文件。如果需要修改其他 Agent 的文件，必须在 `docs/CHANGE_REQUESTS.md` 中记录变更请求。

### 规则 2: 接口优先

- Core 层接口是所有 Agent 的共享契约
- 接口变更必须先更新 `API_SPEC.md`，再通知相关 Agent
- 使用 C# interface 确保编译时类型安全

### 规则 3: 命名规范

| 类别 | 规范 | 示例 |
|------|------|------|
| 命名空间 | `VoiceAssistant.{Layer}` | `VoiceAssistant.Core` |
| 接口 | `I{Name}Service` | `ISpeechToTextService` |
| 实现类 | `Azure{Name}Service` | `AzureSpeechToTextService` |
| 配置类 | `Azure{Service}Options` | `AzureSpeechOptions` |
| 模型类 | PascalCase，无前缀 | `ConversationMessage` |
| 控制器 | `{Resource}Controller` | `ConversationsController` |

### 规则 4: Git 分支策略

```
main
├── feature/core-interfaces        # Agent A
├── feature/azure-stt-service      # Agent B
├── feature/azure-tts-service      # Agent B
├── feature/azure-openai-service   # Agent B
├── feature/api-endpoints          # Agent C
├── feature/web-frontend           # Agent D
├── feature/unit-tests             # Agent E
└── feature/docker-k8s             # Agent F
```

### 规则 5: 依赖方向

```
VoiceAssistant.Api → VoiceAssistant.Core
VoiceAssistant.Api → VoiceAssistant.Infrastructure
VoiceAssistant.Infrastructure → VoiceAssistant.Core
VoiceAssistant.Core → (无外部项目依赖)
```

Core 层不允许引用 Infrastructure 或 Api 层。依赖注入在 Api 层的 `Program.cs` 中完成。

### 规则 6: 共享状态

- 不使用静态变量共享状态
- 所有跨请求状态通过 DI 容器的生命周期管理
- 会话状态使用 `InMemorySessionManager`（基于 `ConcurrentDictionary`，Singleton 生命周期）

## Agent 启动检查清单

Agent 开始工作前，请确认：

- [ ] 已阅读所有 `docs/` 下的文档
- [ ] 清楚自己负责的目录范围
- [ ] 了解前置依赖是否已就绪
- [ ] 了解输出契约（其他 Agent 期望的接口/格式）
- [ ] 遵循命名规范
- [ ] 不修改他人负责的文件

## 冲突解决

如果遇到以下情况：

1. **接口不满足需求**: 在 `docs/CHANGE_REQUESTS.md` 中记录，说明原因和建议变更
2. **文件冲突**: 优先以 Core 层定义为准
3. **设计分歧**: 参考 `ARCHITECTURE.md` 中的设计决策，无法解决则记录到 Change Request

## 沟通文件

| 文件 | 用途 |
|------|------|
| `docs/CHANGE_REQUESTS.md` | 跨 Agent 变更请求 |
| `docs/API_SPEC.md` | 接口契约（所有 Agent 共享） |
| `docs/DEPLOYMENT.md` | 部署指南（Agent F 主要参考） |
| `docs/DECISIONS.md` | 重要设计决策记录（可选，后续创建） |
