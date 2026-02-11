# 开发指南

## 环境准备

### 必需工具

| 工具 | 版本 | 说明 |
|------|------|------|
| .NET SDK | 10.0+ | [下载地址](https://dotnet.microsoft.com/download/dotnet/10.0) |
| Docker | 24.0+ | 用于容器化构建和本地运行 |
| kubectl | 1.28+ | 用于 AKS 部署（后续阶段） |
| Azure CLI | 2.50+ | 用于 Azure 资源管理 |
| IDE | VS Code / Rider / VS 2022 | 推荐 VS Code + C# Dev Kit 扩展 |

### Azure 资源配置

在开始开发之前，需要在 Azure 上创建以下资源：

1. **Azure Speech Service**
   ```bash
   az cognitiveservices account create \
     --name voice-assistant-speech \
     --resource-group voice-assistant-rg \
     --kind SpeechServices \
     --sku S0 \
     --location eastasia
   ```

2. **Azure OpenAI Service**
   ```bash
   az cognitiveservices account create \
     --name voice-assistant-openai \
     --resource-group voice-assistant-rg \
     --kind OpenAI \
     --sku S0 \
     --location eastus2
   ```
   然后部署 GPT-4o 模型：
   ```bash
   az cognitiveservices account deployment create \
     --name voice-assistant-openai \
     --resource-group voice-assistant-rg \
     --deployment-name gpt-4o \
     --model-name gpt-4o \
     --model-version "2024-05-13" \
     --model-format OpenAI \
     --sku-capacity 10 \
     --sku-name Standard
   ```

### 环境变量

在项目根目录创建 `.env` 文件（已加入 `.gitignore`）：

```env
AZURE_SPEECH_SUBSCRIPTION_KEY=your-speech-key
AZURE_SPEECH_REGION=eastasia
AZURE_OPENAI_ENDPOINT=https://your-openai.openai.azure.com/
AZURE_OPENAI_API_KEY=your-openai-key
AZURE_OPENAI_DEPLOYMENT_NAME=gpt-4o
```

或者配置 `src/VoiceAssistant.Api/appsettings.Development.json`：

```json
{
  "AzureSpeech": {
    "SubscriptionKey": "your-speech-key",
    "Region": "eastasia"
  },
  "AzureOpenAI": {
    "Endpoint": "https://your-openai.openai.azure.com/",
    "ApiKey": "your-openai-key",
    "DeploymentName": "gpt-4o"
  }
}
```

> **注意**: `appsettings.Development.json` 已加入 `.gitignore`，不会提交到仓库。

---

## 本地运行

### 启动后端 API

```bash
cd src/VoiceAssistant.Api
dotnet run
```

默认启动地址：`https://localhost:7096` / `http://localhost:5039`

### 访问前端

后端启动后，直接访问 `http://localhost:5039` 即可打开 Web 界面。
（前端静态文件通过 ASP.NET Core 的 StaticFiles 中间件提供）

### Docker 本地运行

```bash
# 构建镜像
docker build -f deploy/docker/Dockerfile -t voice-assistant:dev .

# 运行容器
docker run -p 5000:8080 \
  -e AZURE_SPEECH_SUBSCRIPTION_KEY=your-key \
  -e AZURE_SPEECH_REGION=eastasia \
  -e AZURE_OPENAI_ENDPOINT=https://your-openai.openai.azure.com/ \
  -e AZURE_OPENAI_API_KEY=your-key \
  -e AZURE_OPENAI_DEPLOYMENT_NAME=gpt-4o \
  voice-assistant:dev
```

> 完整的线上部署流程（Azure 资源创建、AKS 部署、域名配置等）请参考 [DEPLOYMENT.md](./DEPLOYMENT.md)。

---

## 运行测试

```bash
# 运行所有测试（4 个项目，106 个测试用例）
dotnet test

# 运行指定项目的测试
dotnet test tests/VoiceAssistant.Core.Tests           # 22 个单元测试
dotnet test tests/VoiceAssistant.Api.Tests             # 13 个单元测试
dotnet test tests/VoiceAssistant.Infrastructure.Tests  # 48 个单元测试
dotnet test tests/VoiceAssistant.IntegrationTests      # 23 个集成测试

# 带详细输出
dotnet test --verbosity normal

# 生成代码覆盖报告
dotnet test --collect:"XPlat Code Coverage"
```

### 测试项目说明

| 测试项目 | 测试类型 | 测试数量 | 说明 |
|----------|----------|----------|------|
| `VoiceAssistant.Core.Tests` | 单元测试 | 22 | ConversationPipeline、InMemorySessionManager |
| `VoiceAssistant.Api.Tests` | 单元测试 | 13 | ConversationsController、ExceptionHandlingMiddleware |
| `VoiceAssistant.Infrastructure.Tests` | 单元测试 | 48 | Azure 服务实现、健康检查、DI 注册 |
| `VoiceAssistant.IntegrationTests` | 集成测试 | 23 | REST API、SignalR Hub、健康检查、异常中间件 |

### 集成测试架构

集成测试使用 `WebApplicationFactory<Program>` 启动完整的 HTTP/SignalR 管道，mock 掉 Azure 外部服务，保留真实的 `InMemorySessionManager` 和 `ConversationPipeline`：

```
VoiceAssistant.IntegrationTests/
├── Fixtures/
│   ├── MockServiceDefaults.cs              # 默认 happy-path mock 配置
│   └── VoiceAssistantWebApplicationFactory.cs  # 自定义 WebApplicationFactory
├── RestApi/
│   └── ConversationsApiTests.cs            # 12 个 REST API 测试
├── SignalR/
│   └── VoiceHubTests.cs                    # 6 个 WebSocket Hub 测试
├── HealthCheck/
│   └── HealthCheckTests.cs                 # 1 个健康检查测试
└── Middleware/
    └── ExceptionMiddlewareIntegrationTests.cs  # 4 个异常中间件测试
```

---

## 编码规范

### 通用规范

- 使用 C# 14 语法特性（primary constructors, collection expressions 等）
- 使用 `file-scoped namespace`
- 所有公开接口和类都要有 XML 文档注释
- 使用 `nullable reference types`（项目级开启 `<Nullable>enable</Nullable>`）
- 异步方法统一以 `Async` 结尾

### 项目结构规范

```
每个项目目录结构：
├── Interfaces/      # 接口定义（仅 Core 层）
├── Models/          # 数据模型
├── Options/         # 配置选项类
├── Exceptions/      # 自定义异常（仅 Core 层）
├── Services/        # 服务实现
├── Pipeline/        # 管道实现（仅 Core 层）
├── Extensions/      # 扩展方法
└── Middleware/       # 中间件（仅 Api 层）
```

### 依赖注入规范

每个项目层提供一个 `DependencyInjection.cs` 扩展方法：

```csharp
// VoiceAssistant.Core/DependencyInjection.cs
public static class DependencyInjection
{
    public static IServiceCollection AddCore(this IServiceCollection services)
    {
        services.AddSingleton<ISessionManager, InMemorySessionManager>();
        services.AddScoped<IConversationPipeline, ConversationPipeline>();
        return services;
    }
}

// VoiceAssistant.Infrastructure/DependencyInjection.cs
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Options 使用 ValidateOnStart 确保启动时校验配置
        services.AddOptionsWithValidateOnStart<AzureSpeechOptions>()
            .BindConfiguration(AzureSpeechOptions.SectionName)
            .ValidateDataAnnotations();
        services.AddOptionsWithValidateOnStart<AzureOpenAIOptions>()
            .BindConfiguration(AzureOpenAIOptions.SectionName)
            .ValidateDataAnnotations();

        services.AddScoped<ISpeechToTextService, AzureSpeechToTextService>();
        services.AddScoped<ITextToSpeechService, AzureTextToSpeechService>();
        services.AddScoped<IChatService, AzureOpenAIChatService>();

        services.AddHealthChecks()
            .AddCheck<AzureServicesHealthCheck>("azure-services");

        return services;
    }
}
```

在 `Program.cs` 中统一注册：

```csharp
builder.Services.AddCore();
builder.Services.AddInfrastructure(builder.Configuration);
```

### 错误处理规范

- 不要吞掉异常，使用全局异常处理中间件 `ExceptionHandlingMiddleware` 统一处理
- 业务异常使用自定义异常类型（继承 `VoiceAssistantException`）
- Pipeline 中 Azure SDK 调用失败时，通过 `when (ex is not VoiceAssistantException)` 过滤后包装为对应的业务异常
- 所有异常记录到结构化日志
- Controller 的 `Speak` 方法包含独立的 try-catch 进行精细化错误处理

### 日志规范

使用 `ILogger<T>` 进行日志记录：

```csharp
_logger.LogInformation("Processing speech recognition for session {SessionId}", sessionId);
_logger.LogError(ex, "STT failed for session {SessionId}", sessionId);
```

---

## NuGet 包依赖

### VoiceAssistant.Core

| 包名 | 版本 | 用途 |
|------|------|------|
| `Microsoft.Extensions.Options` | 10.0.x | 配置绑定（Options 模型） |
| `Microsoft.Extensions.Logging.Abstractions` | 10.0.x | 日志抽象 |

> Core 层仅依赖 Microsoft.Extensions 抽象包，不依赖任何 Azure SDK。

### VoiceAssistant.Infrastructure

| 包名 | 版本 | 用途 |
|------|------|------|
| `Microsoft.CognitiveServices.Speech` | 最新稳定版 | Azure Speech SDK |
| `Azure.AI.OpenAI` | 最新稳定版 | Azure OpenAI SDK |
| `Microsoft.Extensions.Options` | 10.0.x | 配置绑定 |
| `Microsoft.Extensions.Logging.Abstractions` | 10.0.x | 日志抽象 |
| `Microsoft.Extensions.Diagnostics.HealthChecks` | 10.0.x | 健康检查 |

### VoiceAssistant.Api

| 包名 | 版本 | 用途 |
|------|------|------|
| `Microsoft.AspNetCore.OpenApi` | 10.0.x | OpenAPI 支持 |

> SignalR、HealthChecks、Controllers 等均为 ASP.NET Core 内置功能，无需额外 NuGet 包。

### 测试项目

| 包名 | 版本 | 用途 |
|------|------|------|
| `xunit` | 2.9.3 | 测试框架 |
| `xunit.runner.visualstudio` | 3.1.4 | 测试运行器 |
| `Microsoft.NET.Test.Sdk` | 17.14.1 | 测试 SDK |
| `Moq` | 4.20.72 | Mock 框架 |
| `FluentAssertions` | 8.0.1 | 断言库 |
| `coverlet.collector` | 6.0.4 | 代码覆盖率 |
| `Microsoft.AspNetCore.Mvc.Testing` | 10.0.2 | 集成测试（WebApplicationFactory） |
| `Microsoft.AspNetCore.SignalR.Client` | 10.0.2 | SignalR 客户端（集成测试） |

---

## 项目引用关系

```
VoiceAssistant.Api
  ├── ProjectReference: VoiceAssistant.Core
  └── ProjectReference: VoiceAssistant.Infrastructure
  └── InternalsVisibleTo: VoiceAssistant.IntegrationTests

VoiceAssistant.Infrastructure
  └── ProjectReference: VoiceAssistant.Core

VoiceAssistant.Core
  └── (无项目引用)

VoiceAssistant.Api.Tests
  ├── ProjectReference: VoiceAssistant.Api
  └── ProjectReference: VoiceAssistant.Core

VoiceAssistant.Core.Tests
  └── ProjectReference: VoiceAssistant.Core

VoiceAssistant.Infrastructure.Tests
  ├── ProjectReference: VoiceAssistant.Infrastructure
  └── ProjectReference: VoiceAssistant.Core

VoiceAssistant.IntegrationTests
  ├── ProjectReference: VoiceAssistant.Api
  ├── ProjectReference: VoiceAssistant.Core
  └── ProjectReference: VoiceAssistant.Infrastructure
```

---

## 调试技巧

### 测试 Azure Speech 连接

```bash
# 使用 curl 测试 Speech Service 可用性
curl -X POST \
  "https://eastasia.api.cognitive.microsoft.com/sts/v1.0/issueToken" \
  -H "Ocp-Apim-Subscription-Key: YOUR_KEY" \
  -H "Content-Length: 0"
```

### 测试 Azure OpenAI 连接

```bash
curl -X POST \
  "https://YOUR_ENDPOINT.openai.azure.com/openai/deployments/gpt-4o/chat/completions?api-version=2024-02-01" \
  -H "Content-Type: application/json" \
  -H "api-key: YOUR_KEY" \
  -d '{"messages":[{"role":"user","content":"Hello"}],"max_tokens":100}'
```

### 测试健康检查端点

```bash
curl http://localhost:5039/health
# 期望返回: Healthy
```

### 浏览器音频调试

在 Chrome DevTools 中：
1. 打开 `chrome://flags/#unsafely-treat-insecure-origin-as-secure`
2. 添加 `http://localhost:5039` 以允许非 HTTPS 下使用麦克风
3. 或直接使用 `https://localhost:7096`
