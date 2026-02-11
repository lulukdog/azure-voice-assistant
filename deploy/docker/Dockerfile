# ========================================
# Stage 1: Build
# ========================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /build

# 复制项目文件（利用 Docker 缓存层）
COPY src/VoiceAssistant.Core/VoiceAssistant.Core.csproj src/VoiceAssistant.Core/
COPY src/VoiceAssistant.Infrastructure/VoiceAssistant.Infrastructure.csproj src/VoiceAssistant.Infrastructure/
COPY src/VoiceAssistant.Api/VoiceAssistant.Api.csproj src/VoiceAssistant.Api/

# 还原 NuGet 包
RUN dotnet restore src/VoiceAssistant.Api/VoiceAssistant.Api.csproj

# 复制源码并构建
COPY src/ src/
RUN dotnet publish src/VoiceAssistant.Api/VoiceAssistant.Api.csproj \
    -c Release \
    -o /app/publish

# ========================================
# Stage 2: Runtime
# ========================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Azure Speech SDK 运行时依赖（Linux/Ubuntu Noble）
RUN apt-get update && apt-get install -y --no-install-recommends \
    libasound2t64 \
    libssl3 \
    ca-certificates \
    curl \
    && rm -rf /var/lib/apt/lists/*

# 非 root 用户（安全加固）
RUN groupadd -r appuser && useradd -r -g appuser -d /app appuser

# 复制编译产物
COPY --from=build /app/publish .

# 复制静态前端文件
COPY src/VoiceAssistant.Web/ /app/wwwroot/

# 环境配置
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

# K8s 健康检查
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

USER appuser
ENTRYPOINT ["dotnet", "VoiceAssistant.Api.dll"]
