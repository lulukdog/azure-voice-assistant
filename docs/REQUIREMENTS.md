# 需求文档

## 项目名称

Azure Voice Assistant

## 项目目标

构建一个基于 Azure 云服务的语音对话系统，实现用户通过 Web 浏览器与 AI 进行语音交互（语音问，语音答）。

## 功能需求

### FR-001: 语音录制

- 用户在 Web 浏览器中通过麦克风录制语音
- 支持按住按钮录音（Push-to-Talk）和点击切换录音两种模式
- 录音过程中显示实时音量反馈（波形或音量条）
- 录音时长限制：单次最长 60 秒
- 支持的浏览器：Chrome 90+, Edge 90+, Firefox 90+, Safari 15+

### FR-002: 语音识别 (STT)

- 将用户的语音实时传输到后端
- 调用 Azure Speech Service 将语音转换为文字
- 支持中文（zh-CN）和英文（en-US）语音识别
- 将识别结果实时显示在前端界面
- 返回识别置信度分数

### FR-003: AI 对话 (LLM)

- 将识别后的文字发送给 Azure OpenAI（GPT-4o）
- 维护对话上下文（会话历史）
- 支持系统提示词（System Prompt）配置
- 支持流式响应，逐句反馈给用户
- 单次对话 Token 限制可配置

### FR-004: 语音合成 (TTS)

- 将 AI 回复文字通过 Azure TTS 转换为语音
- 支持选择不同的语音角色（Voice Name）
- 支持调节语速和音调
- 生成的音频实时流式传输给前端播放
- 支持 SSML 标记语言以实现更自然的语音

### FR-005: 实时对话界面

- 显示对话历史（用户说的话 + AI 的回复）
- 显示当前状态（录音中 / 识别中 / 思考中 / 播放中）
- 支持中断 AI 回复（用户重新开始录音时停止播放）
- 支持文字输入作为备选输入方式
- 响应式设计，适配桌面和移动端浏览器

### FR-006: 会话管理

- 创建新会话
- 查看会话历史
- 切换/恢复已有会话
- 清除会话历史

## 非功能需求

### NFR-001: 性能

- 语音识别延迟（音频发送到文字返回）：< 2 秒
- LLM 首个 Token 响应延迟：< 1 秒
- 语音合成首个音频块延迟：< 1 秒
- 端到端响应延迟（用户停止说话到开始听到回复）：< 5 秒

### NFR-002: 可靠性

- 服务可用性目标：99.5%
- WebSocket 断线自动重连
- Azure 服务调用失败时的重试策略（指数退避）
- 优雅降级：TTS 失败时返回文字回复

### NFR-003: 安全性

- 所有通信使用 HTTPS/WSS
- Azure 服务凭据通过环境变量或 Azure Key Vault 管理
- 不在前端暴露任何 Azure 密钥
- 输入内容过滤（防止 prompt injection 基础防护）
- 速率限制：单用户每分钟最多 20 次请求

### NFR-004: 可扩展性

- 支持多用户并发对话
- 通过 AKS 水平扩展处理并发负载
- 无状态设计，会话状态不依赖单个实例

### NFR-005: 可观测性

- 结构化日志（Serilog）
- 请求链路追踪
- 关键指标监控（延迟、错误率、并发数）

## Azure 资源依赖

| Azure 服务 | 用途 | SKU 建议 |
|------------|------|----------|
| Azure Speech Service | 语音识别 + 语音合成 | S0（标准层） |
| Azure OpenAI Service | GPT-4o 对话 | 标准部署 |
| Azure Kubernetes Service | 容器编排 | Standard_B2s（开发） |
| Azure Container Registry | 镜像仓库 | Basic |

## 配置项

```json
{
  "AzureSpeech": {
    "SubscriptionKey": "<from-env>",
    "Region": "eastasia",
    "RecognitionLanguage": "zh-CN",
    "SynthesisVoiceName": "zh-CN-XiaoxiaoNeural",
    "SynthesisOutputFormat": "Audio16Khz32KBitRateMonoMp3"
  },
  "AzureOpenAI": {
    "Endpoint": "<from-env>",
    "ApiKey": "<from-env>",
    "DeploymentName": "gpt-4o",
    "MaxTokens": 800,
    "Temperature": 0.7,
    "SystemPrompt": "你是一个友好的 AI 语音助手，请用简洁的中文回答问题。"
  }
}
```

## 里程碑

### M1: 基础骨架

- 项目结构搭建完成
- 所有文档编写完成
- .NET 解决方案和项目创建
- 基本的 REST API 端点可运行

### M2: 核心对话流程

- STT 服务集成
- LLM 服务集成
- TTS 服务集成
- 对话管道 Pipeline 实现
- 基础 WebSocket 通信

### M3: Web 前端

- 录音界面实现
- 实时音频播放
- 对话历史展示
- 状态管理

### M4: 完善与部署

- 错误处理与重试
- Docker 镜像构建
- AKS 部署配置
- 基础监控与日志
