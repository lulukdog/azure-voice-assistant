# 部署指南（新手友好版）

本文档从零开始，手把手教你把 Azure Voice Assistant 部署到线上。分为三种方式，按难度递增排列：

| 方式 | 适用场景 | 难度 |
|------|----------|------|
| [Docker 本地运行](#一docker-本地运行) | 本地验证镜像是否正常 | ⭐ |
| [Azure 一键部署](#二azure-一键部署脚本方式) | 用脚本自动创建 Azure 资源并部署到 AKS | ⭐⭐ |
| [手动分步部署](#三手动分步部署) | 想理解每一步在做什么 | ⭐⭐⭐ |

**当前线上地址**: https://voice-assistant.eastasia.cloudapp.azure.com

---

## 前置准备

### 1. 安装必需工具

在开始之前，请确认你的机器上已安装以下工具：

```bash
# 检查 .NET SDK（需要 10.0+）
dotnet --version

# 检查 Docker
docker --version

# 检查 Azure CLI（需要 2.50+）
az --version

# 检查 kubectl（需要 1.28+）
kubectl version --client
```

**如果缺少上述工具**：

| 工具 | macOS 安装 | Windows 安装 |
|------|-----------|-------------|
| .NET SDK 10.0 | `brew install dotnet` | [下载安装](https://dotnet.microsoft.com/download/dotnet/10.0) |
| Docker | `brew install --cask docker` | [Docker Desktop](https://www.docker.com/products/docker-desktop/) |
| Azure CLI | `brew install azure-cli` | `winget install Microsoft.AzureCLI` |
| kubectl | `brew install kubectl` | `az aks install-cli` |

### 2. 登录 Azure

```bash
# 登录（会打开浏览器）
az login

# 确认当前订阅
az account show --query "{name:name, id:id}" -o table
```

如果你有多个订阅，先切换到正确的：

```bash
# 查看所有订阅
az account list --query "[].{name:name, id:id}" -o table

# 切换订阅
az account set --subscription "你的订阅名称或ID"
```

### 3. 确保项目能在本地正常构建

```bash
cd /path/to/azure-voice-assistant

# 构建
dotnet build

# 运行测试
dotnet test
```

> 所有 106 个测试必须通过才继续。

---

## 一、Docker 本地运行

这种方式不需要 Azure 账号也能验证镜像构建是否正常（但因为没有真实 Azure 密钥，只能测试健康检查端点）。

### Step 1: 构建镜像

```bash
# 在项目根目录执行
docker build -f deploy/docker/Dockerfile -t voice-assistant:dev .
```

构建过程说明：
- **第一阶段 (build)**：使用 .NET SDK 编译项目
- **第二阶段 (runtime)**：只包含运行时，镜像更小更安全

### Step 2: 运行容器

```bash
docker run -p 8080:8080 \
  -e AZURE_SPEECH_SUBSCRIPTION_KEY=your-speech-key \
  -e AZURE_SPEECH_REGION=eastasia \
  -e AZURE_OPENAI_ENDPOINT=https://your-openai.openai.azure.com/ \
  -e AZURE_OPENAI_API_KEY=your-openai-key \
  -e AZURE_OPENAI_DEPLOYMENT_NAME=gpt-4o \
  voice-assistant:dev
```

> **注意**: 把 `your-speech-key` 等占位符换成真实的 Azure 密钥。如果你还没有 Azure 资源，先跳到[第二节](#二azure-一键部署脚本方式)创建。

### Step 3: 验证

```bash
# 健康检查
curl http://localhost:8080/health
# 期望输出: Healthy

# 打开浏览器访问前端
open http://localhost:8080
```

### Step 4: 停止容器

```bash
# 查看运行中的容器
docker ps

# 停止
docker stop <容器ID>
```

---

## 二、Azure 一键部署（脚本方式）

项目提供了自动化脚本，一键完成：创建所有 Azure 资源 → 构建镜像 → 推送到 ACR → 部署到 AKS。

### 整体流程

```
scripts/setup-azure.sh          scripts/deploy.sh
        │                              │
        ▼                              ▼
┌──────────────────┐          ┌──────────────────┐
│ 创建 Azure 资源   │          │ 构建 + 推送镜像     │
│                  │          │                  │
│ ① 资源组         │   .env   │ ① 登录 ACR       │
│ ② ACR 容器仓库   │ ───────▶ │ ② Docker Build   │
│ ③ AKS k8s集群   │          │ ③ Push to ACR    │
│ ④ Speech 服务    │          │ ④ kubectl 配置    │
│ ⑤ OpenAI 服务   │          │ ⑤ 部署到 AKS     │
│ ⑥ K8s Secrets   │          │                  │
└──────────────────┘          └──────────────────┘
```

### Step 1: 创建 Azure 基础设施

```bash
bash scripts/setup-azure.sh
```

脚本会：
1. 检查 Azure CLI 是否已安装和登录
2. 显示将要创建的资源清单，等你确认
3. 依次创建：资源组 → ACR → AKS → Speech Service → OpenAI Service
4. 部署 GPT-4o 模型
5. 在 AKS 中创建 Namespace 和 Secrets
6. 安装 NGINX Ingress Controller
7. 将所有密钥保存到项目根目录的 `.env` 文件

**默认配置**：

| 资源 | 默认值 | 说明 |
|------|--------|------|
| 资源组 | `voice-assistant-rg` | |
| 区域 | `eastasia` | 离中国用户最近 |
| OpenAI 区域 | `eastus2` | OpenAI 在东亚不一定可用 |
| ACR 名称 | `voiceasstluluk` | **需改为你自己的唯一名称** |
| AKS 节点 | `Standard_B2s_v2 x 1` | 最小规格，开发够用 |
| Speech SKU | `F0`（免费） | 每月 5 小时免费 |

**自定义配置**（通过环境变量覆盖）：

```bash
# 示例：修改 ACR 名称和区域
ACR_NAME=myacr123 LOCATION=japaneast bash scripts/setup-azure.sh
```

> **费用提醒**：AKS 集群会持续产生费用（即使没有流量）。开发完后记得用 `scripts/destroy.sh` 清理。

### Step 2: 构建并部署应用

```bash
bash scripts/deploy.sh
```

脚本会自动读取 Step 1 生成的 `.env` 文件，然后：
1. 登录 ACR
2. 构建 Docker 镜像
3. 推送镜像到 ACR
4. 配置 kubectl
5. 部署 K8s 资源（ConfigMap + Deployment + Service + Ingress + HPA）
6. 等待 Pod 就绪

部署成功后会显示：
- Pod 状态
- Service 状态
- Ingress IP（如果已分配）

### Step 3: 验证部署

```bash
# 查看 Pod 是否 Running
kubectl get pods -n voice-assistant

# 查看 Service
kubectl get svc -n voice-assistant

# 端口转发到本地测试（不需要 Ingress）
kubectl port-forward svc/voice-assistant 8080:80 -n voice-assistant

# 另开终端测试
curl http://localhost:8080/health
# 期望输出: Healthy
```

部署完成后，通过 Ingress 域名直接访问：

```
https://voice-assistant.eastasia.cloudapp.azure.com
```

### Step 4: 配置域名访问（可选）

如果你想通过域名访问：

```bash
# 1. 获取 Ingress IP
kubectl get svc -n ingress-nginx ingress-nginx-controller \
  -o jsonpath='{.status.loadBalancer.ingress[0].ip}'

# 2. 在你的 DNS 服务商添加 A 记录：
#    voice-assistant.yourdomain.com → <上面获取的 IP>

# 3. 安装 cert-manager（自动申请 HTTPS 证书）
kubectl apply -f https://github.com/cert-manager/cert-manager/releases/download/v1.13.3/cert-manager.yaml

# 4. 等待 cert-manager 就绪
kubectl wait --for=condition=Available deployment --all -n cert-manager --timeout=120s

# 5. 创建证书签发者
kubectl apply -f deploy/k8s/cluster-issuer.yaml
```

> 修改 `deploy/k8s/deployment.yaml` 中 Ingress 的 `host` 字段为你自己的域名。

### 更新部署

代码有变更时，只需重新运行 deploy 脚本：

```bash
# 使用时间戳作为镜像标签
bash scripts/deploy.sh $(date +%s)
```

### 回滚

```bash
# 查看部署历史
kubectl rollout history deployment/voice-assistant -n voice-assistant

# 回滚到上一版本
kubectl rollout undo deployment/voice-assistant -n voice-assistant
```

---

## 三、手动分步部署

如果你想理解每一步做了什么，可以手动执行。

### Step 1: 创建资源组

```bash
az group create \
  --name voice-assistant-rg \
  --location eastasia
```

> 资源组是 Azure 中管理资源的逻辑容器，所有资源都放在这个组里，方便一键删除。

### Step 2: 创建 Azure Container Registry (ACR)

```bash
az acr create \
  --resource-group voice-assistant-rg \
  --name <你的ACR名称> \
  --sku Basic \
  --location eastasia
```

> ACR 名称必须全球唯一，只能包含字母和数字。例如 `myvoiceasst123`。

### Step 3: 创建 AKS 集群

```bash
az aks create \
  --resource-group voice-assistant-rg \
  --name voice-assistant-aks \
  --node-count 1 \
  --node-vm-size Standard_B2s_v2 \
  --enable-managed-identity \
  --attach-acr <你的ACR名称> \
  --network-plugin azure \
  --generate-ssh-keys \
  --location eastasia
```

参数说明：
- `--node-count 1`：只创建 1 个节点（省钱）
- `--node-vm-size Standard_B2s_v2`：最小规格的虚拟机
- `--enable-managed-identity`：使用托管身份，无需手动管理密钥
- `--attach-acr`：让 AKS 有权限从你的 ACR 拉取镜像

获取 kubectl 凭据：

```bash
az aks get-credentials \
  --resource-group voice-assistant-rg \
  --name voice-assistant-aks

# 验证连接
kubectl get nodes
```

### Step 4: 创建 Azure Speech Service

```bash
az cognitiveservices account create \
  --name voice-assistant-speech \
  --resource-group voice-assistant-rg \
  --kind SpeechServices \
  --sku F0 \
  --location eastasia

# 获取密钥
az cognitiveservices account keys list \
  --name voice-assistant-speech \
  --resource-group voice-assistant-rg \
  --query key1 -o tsv
```

> `F0` 是免费层，每月 5 小时语音识别 + 50 万字符语音合成。开发测试足够。正式上线用 `S0`。

### Step 5: 创建 Azure OpenAI Service

```bash
# 创建 OpenAI 资源（注意区域可能需要用 eastus2）
az cognitiveservices account create \
  --name voice-assistant-openai \
  --resource-group voice-assistant-rg \
  --kind OpenAI \
  --sku S0 \
  --location eastus2

# 部署 GPT-4o 模型
az cognitiveservices account deployment create \
  --name voice-assistant-openai \
  --resource-group voice-assistant-rg \
  --deployment-name gpt-4o \
  --model-name gpt-4o \
  --model-version "2024-05-13" \
  --model-format OpenAI \
  --sku-capacity 10 \
  --sku-name Standard

# 获取 Endpoint
az cognitiveservices account show \
  --name voice-assistant-openai \
  --resource-group voice-assistant-rg \
  --query properties.endpoint -o tsv

# 获取密钥
az cognitiveservices account keys list \
  --name voice-assistant-openai \
  --resource-group voice-assistant-rg \
  --query key1 -o tsv
```

> **为什么用 `eastus2` 而不是 `eastasia`？** Azure OpenAI 的可用区域有限，GPT-4o 在东亚可能不可用。`eastus2` 是最稳定的可用区域之一。

### Step 6: 构建并推送 Docker 镜像

```bash
# 登录 ACR
az acr login --name <你的ACR名称>

# 构建镜像
docker build \
  -f deploy/docker/Dockerfile \
  -t <你的ACR名称>.azurecr.io/voice-assistant:v1 \
  .

# 推送到 ACR
docker push <你的ACR名称>.azurecr.io/voice-assistant:v1
```

### Step 7: 部署到 AKS

```bash
# 创建 Namespace
kubectl apply -f deploy/k8s/namespace.yaml

# 创建 Secrets（替换为你的真实密钥）
kubectl create secret generic voice-assistant-secrets \
  --namespace voice-assistant \
  --from-literal=speech-subscription-key="<你的Speech密钥>" \
  --from-literal=openai-endpoint="<你的OpenAI Endpoint>" \
  --from-literal=openai-api-key="<你的OpenAI密钥>"

# 应用 ConfigMap
kubectl apply -f deploy/k8s/configmap.yaml

# 修改 deployment.yaml 中的镜像地址后部署
# 把 `voiceasstluluk.azurecr.io` 替换为你的 ACR 地址
kubectl apply -f deploy/k8s/deployment.yaml
```

> **关于 Secrets**：Kubernetes Secrets 存储敏感信息（密钥、密码）。上面 3 个 secret key 的作用：
> - `speech-subscription-key` → 用于调用 Azure Speech（语音识别 + 语音合成）
> - `openai-endpoint` → Azure OpenAI 的 API 地址
> - `openai-api-key` → Azure OpenAI 的访问密钥

### Step 8: 验证部署

```bash
# 查看 Pod 状态（等待 STATUS 变为 Running）
kubectl get pods -n voice-assistant -w

# 查看日志
kubectl logs -f deployment/voice-assistant -n voice-assistant

# 端口转发测试
kubectl port-forward svc/voice-assistant 8080:80 -n voice-assistant

# 测试健康检查
curl http://localhost:8080/health

# 打开浏览器测试
open http://localhost:8080
```

---

## 资源架构总览

部署完成后的 Azure 资源关系：

```
voice-assistant-rg（资源组）
├── ACR（容器镜像仓库）
│   └── voice-assistant:v1（Docker 镜像）
│
├── AKS（Kubernetes 集群）
│   └── voice-assistant namespace
│       ├── Deployment（运行你的应用容器）
│       ├── Service（ClusterIP，集群内部负载均衡）
│       ├── Ingress（对外暴露 HTTP/HTTPS）
│       ├── HPA（自动扩缩容 1~3 个 Pod）
│       ├── ConfigMap（非敏感配置）
│       └── Secret（密钥）
│
├── Speech Service（语音识别 + 语音合成）
│
└── OpenAI Service（GPT-4o 大模型）
```

---

## K8s 资源清单说明

| 文件 | 资源类型 | 作用 |
|------|----------|------|
| `namespace.yaml` | Namespace | 创建 `voice-assistant` 命名空间，隔离资源 |
| `configmap.yaml` | ConfigMap | 存储非敏感配置（区域、语音角色、模型参数等） |
| `deployment.yaml` | Deployment | 定义应用容器的运行规格、探针、资源限制 |
| `deployment.yaml` | Service | ClusterIP 类型，集群内部 80 → 容器 8080 |
| `deployment.yaml` | Ingress | 对外暴露 HTTPS，配置域名和 TLS 证书 |
| `deployment.yaml` | HPA | 根据 CPU/内存自动扩缩容（1~3 副本） |
| `cluster-issuer.yaml` | ClusterIssuer | Let's Encrypt 证书自动签发 |

---

## 常用运维命令

### 日常操作

```bash
# 查看 Pod 状态
kubectl get pods -n voice-assistant

# 实时查看日志
kubectl logs -f deployment/voice-assistant -n voice-assistant

# 端口转发到本地
kubectl port-forward svc/voice-assistant 8080:80 -n voice-assistant

# 进入 Pod 内部调试
kubectl exec -it deployment/voice-assistant -n voice-assistant -- /bin/bash

# 查看 Pod 详细信息（排查启动失败）
kubectl describe pod <pod-name> -n voice-assistant
```

### 扩缩容

```bash
# 手动扩容到 3 个副本
kubectl scale deployment/voice-assistant -n voice-assistant --replicas=3

# 查看 HPA 状态
kubectl get hpa -n voice-assistant
```

### 更新部署

```bash
# 方式 1: 使用 deploy 脚本
bash scripts/deploy.sh $(date +%s)

# 方式 2: 手动更新镜像
kubectl set image deployment/voice-assistant \
  voice-assistant=<ACR>.azurecr.io/voice-assistant:v2 \
  -n voice-assistant
```

### 回滚

```bash
# 查看历史版本
kubectl rollout history deployment/voice-assistant -n voice-assistant

# 回滚到上一版本
kubectl rollout undo deployment/voice-assistant -n voice-assistant

# 回滚到指定版本
kubectl rollout undo deployment/voice-assistant -n voice-assistant --to-revision=2
```

### 查看资源用量

```bash
# 查看节点资源
kubectl top nodes

# 查看 Pod 资源（需要 metrics-server）
kubectl top pods -n voice-assistant
```

---

## 故障排查

### Pod 一直 Pending

```bash
kubectl describe pod <pod-name> -n voice-assistant
```

常见原因：
- **Insufficient cpu/memory**：节点资源不够 → 扩容节点或减小资源请求
- **ImagePullBackOff**：镜像拉取失败 → 检查 ACR 名称是否正确、AKS 是否 attach 了 ACR

### Pod CrashLoopBackOff

```bash
# 查看崩溃日志
kubectl logs <pod-name> -n voice-assistant --previous
```

常见原因：
- **配置缺失**：Secrets 未创建或 key 名称错误
- **Options 校验失败**：`SubscriptionKey`、`Endpoint`、`ApiKey` 等必填项为空

### 健康检查失败

```bash
# 查看 Pod 事件
kubectl describe pod <pod-name> -n voice-assistant | grep -A 10 Events
```

常见原因：
- 应用启动慢，探针 `initialDelaySeconds` 太短
- 端口不匹配（应用监听 8080，探针也应检查 8080）

### SignalR WebSocket 连接失败

如果前端 WebSocket 无法连接：

1. 确认 Ingress 超时设置够大（已配置 3600s）
2. 如果用了 CDN 或代理，确认支持 WebSocket
3. 确认 CORS 设置允许前端域名

---

## 费用预估

| Azure 资源 | SKU | 月费用（估算） |
|------------|-----|---------------|
| AKS（1 x Standard_B2s_v2） | — | ~$30 |
| Speech Service | F0（免费层） | $0 |
| Speech Service | S0（标准层） | ~$1/小时语音 |
| OpenAI (GPT-4o) | Standard | ~$5/100万输入token |
| ACR | Basic | ~$5 |
| **开发环境合计** | | **~$35/月** |

> 开发阶段最大的开销是 AKS 集群。**测试完后务必清理资源**。

---

## 资源清理

### 删除所有 Azure 资源

```bash
bash scripts/destroy.sh
```

脚本会要求你输入资源组名称进行二次确认，然后后台删除整个资源组及其下所有资源。

### 手动清理

```bash
# 删除资源组（会删除组下所有资源）
az group delete --name voice-assistant-rg --yes --no-wait

# 查看删除状态
az group show --name voice-assistant-rg --query properties.provisioningState -o tsv
```

### 只清理 AKS 中的应用（保留集群）

```bash
kubectl delete namespace voice-assistant
```

---

## 附录

### Docker 镜像结构

```
voice-assistant:dev
├── /app/                          # ASP.NET Core 应用
│   ├── VoiceAssistant.Api.dll     # 主入口
│   └── ...                        # 依赖 DLL
├── /app/wwwroot/                  # 静态前端文件
│   ├── index.html
│   ├── css/styles.css
│   └── js/app.js, audio-*.js, websocket-client.js
└── 非 root 用户运行 (appuser)
```

### 配置注入方式

应用通过 ASP.NET Core 的配置系统读取配置，优先级从低到高：

```
appsettings.json           ← 默认值
appsettings.{Env}.json     ← 环境特定配置
环境变量                    ← K8s ConfigMap / Secret 注入
```

K8s 中环境变量使用 `__`（双下划线）分隔嵌套配置。例如：

| 环境变量 | 对应 appsettings.json |
|----------|-----------------------|
| `AzureSpeech__SubscriptionKey` | `AzureSpeech.SubscriptionKey` |
| `AzureSpeech__Region` | `AzureSpeech.Region` |
| `AzureOpenAI__Endpoint` | `AzureOpenAI.Endpoint` |
| `AzureOpenAI__ApiKey` | `AzureOpenAI.ApiKey` |
| `AzureOpenAI__DeploymentName` | `AzureOpenAI.DeploymentName` |

### 端口映射一览

| 场景 | 监听端口 | 访问方式 |
|------|----------|----------|
| `dotnet run`（本地开发） | 5039 (HTTP) / 7096 (HTTPS) | `http://localhost:5039` |
| Docker 容器 | 8080 | `docker run -p 8080:8080 ...` |
| K8s Service | 80 → 8080 | `kubectl port-forward svc/... 8080:80` |
| K8s Ingress | 443 (HTTPS) → 80 → 8080 | `https://voice-assistant.eastasia.cloudapp.azure.com` |
