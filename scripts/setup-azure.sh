#!/bin/bash
# ============================================================
# Azure Voice Assistant - 基础设施创建脚本
# 创建所有 Azure 资源：RG, ACR, AKS, Speech, OpenAI
# ============================================================
set -euo pipefail

# ============================================================
# 配置参数（可通过环境变量覆盖）
# ============================================================
RESOURCE_GROUP="${RESOURCE_GROUP:-voice-assistant-rg}"
LOCATION="${LOCATION:-eastasia}"
OPENAI_LOCATION="${OPENAI_LOCATION:-eastus2}"  # OpenAI 在 eastasia 可能不可用

ACR_NAME="${ACR_NAME:-voiceasstluluk}"
AKS_NAME="${AKS_NAME:-voice-assistant-aks}"
AKS_NODE_SIZE="${AKS_NODE_SIZE:-Standard_B2s_v2}"
AKS_NODE_COUNT="${AKS_NODE_COUNT:-1}"

SPEECH_NAME="${SPEECH_NAME:-voice-assistant-speech}"
OPENAI_NAME="${OPENAI_NAME:-voice-assistant-openai}"
OPENAI_DEPLOYMENT="${OPENAI_DEPLOYMENT:-gpt-4o}"

# ============================================================
# 颜色输出
# ============================================================
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

info()  { echo -e "${BLUE}[INFO]${NC} $1"; }
ok()    { echo -e "${GREEN}[OK]${NC} $1"; }
warn()  { echo -e "${YELLOW}[WARN]${NC} $1"; }
error() { echo -e "${RED}[ERROR]${NC} $1"; }

# ============================================================
# 前置检查
# ============================================================
info "检查 Azure CLI 是否已安装..."
if ! command -v az &> /dev/null; then
    error "Azure CLI 未安装。请访问 https://docs.microsoft.com/cli/azure/install-azure-cli"
    exit 1
fi

info "检查 Azure 登录状态..."
if ! az account show &> /dev/null; then
    warn "未登录 Azure，正在打开登录..."
    az login
fi

SUBSCRIPTION=$(az account show --query name -o tsv)
SUBSCRIPTION_ID=$(az account show --query id -o tsv)
ok "当前订阅: $SUBSCRIPTION ($SUBSCRIPTION_ID)"

echo ""
echo "=========================================="
echo "  Azure Voice Assistant 基础设施部署"
echo "=========================================="
echo "  资源组:        $RESOURCE_GROUP"
echo "  区域:          $LOCATION"
echo "  ACR:           $ACR_NAME"
echo "  AKS:           $AKS_NAME ($AKS_NODE_SIZE x $AKS_NODE_COUNT)"
echo "  Speech:        $SPEECH_NAME"
echo "  OpenAI:        $OPENAI_NAME ($OPENAI_LOCATION)"
echo "=========================================="
echo ""

read -p "确认创建以上资源？(y/N) " -n 1 -r
echo
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    info "已取消"
    exit 0
fi

# ============================================================
# Step 1: 资源组
# ============================================================
info "Step 1/6: 创建资源组 $RESOURCE_GROUP..."
az group create \
    --name "$RESOURCE_GROUP" \
    --location "$LOCATION" \
    --output none
ok "资源组已创建"

# ============================================================
# Step 2: Azure Container Registry
# ============================================================
info "Step 2/6: 创建 Azure Container Registry..."
az acr create \
    --resource-group "$RESOURCE_GROUP" \
    --name "$ACR_NAME" \
    --sku Basic \
    --location "$LOCATION" \
    --output none
ok "ACR 已创建: $ACR_NAME.azurecr.io"

# ============================================================
# Step 3: Azure Kubernetes Service
# ============================================================
info "Step 3/6: 创建 AKS 集群（这一步需要较长时间）..."
az aks create \
    --resource-group "$RESOURCE_GROUP" \
    --name "$AKS_NAME" \
    --node-count "$AKS_NODE_COUNT" \
    --node-vm-size "$AKS_NODE_SIZE" \
    --enable-managed-identity \
    --attach-acr "$ACR_NAME" \
    --network-plugin azure \
    --generate-ssh-keys \
    --location "$LOCATION" \
    --output none
ok "AKS 集群已创建"

# 获取 AKS 凭据
info "获取 AKS 集群凭据..."
az aks get-credentials \
    --resource-group "$RESOURCE_GROUP" \
    --name "$AKS_NAME" \
    --overwrite-existing
ok "kubectl 已配置"

# ============================================================
# Step 4: Azure Speech Service
# ============================================================
info "Step 4/6: 创建 Azure Speech Service..."
az cognitiveservices account create \
    --name "$SPEECH_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --kind SpeechServices \
    --sku F0 \
    --location "$LOCATION" \
    --output none
ok "Speech Service 已创建"

SPEECH_KEY=$(az cognitiveservices account keys list \
    --name "$SPEECH_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --query key1 -o tsv)
ok "Speech Key 已获取"

# ============================================================
# Step 5: Azure OpenAI Service
# ============================================================
info "Step 5/6: 创建 Azure OpenAI Service..."
az cognitiveservices account create \
    --name "$OPENAI_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --kind OpenAI \
    --sku S0 \
    --location "$OPENAI_LOCATION" \
    --output none
ok "OpenAI Service 已创建"

info "部署 GPT-4o 模型..."
az cognitiveservices account deployment create \
    --name "$OPENAI_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --deployment-name "$OPENAI_DEPLOYMENT" \
    --model-name gpt-4o \
    --model-version "2024-05-13" \
    --model-format OpenAI \
    --sku-capacity 10 \
    --sku-name Standard \
    --output none
ok "GPT-4o 模型已部署"

OPENAI_ENDPOINT=$(az cognitiveservices account show \
    --name "$OPENAI_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --query properties.endpoint -o tsv)
OPENAI_KEY=$(az cognitiveservices account keys list \
    --name "$OPENAI_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --query key1 -o tsv)
ok "OpenAI Endpoint: $OPENAI_ENDPOINT"

# ============================================================
# Step 6: 创建 Kubernetes Secrets
# ============================================================
info "Step 6/6: 在 AKS 中创建 Namespace 和 Secrets..."

kubectl apply -f "$(dirname "$0")/../deploy/k8s/namespace.yaml"

kubectl create secret generic voice-assistant-secrets \
    --namespace voice-assistant \
    --from-literal=speech-subscription-key="$SPEECH_KEY" \
    --from-literal=openai-endpoint="$OPENAI_ENDPOINT" \
    --from-literal=openai-api-key="$OPENAI_KEY" \
    --dry-run=client -o yaml | kubectl apply -f -

ok "K8s Secrets 已创建"

# ============================================================
# 安装 NGINX Ingress Controller
# ============================================================
info "安装 NGINX Ingress Controller..."
kubectl apply -f https://raw.githubusercontent.com/kubernetes/ingress-nginx/controller-v1.9.4/deploy/static/provider/cloud/deploy.yaml 2>/dev/null || true
ok "NGINX Ingress Controller 已安装"

# ============================================================
# 输出摘要
# ============================================================
echo ""
echo "=========================================="
echo -e "${GREEN}  基础设施创建完成!${NC}"
echo "=========================================="
echo ""
echo "资源摘要:"
echo "  资源组:    $RESOURCE_GROUP"
echo "  ACR:       $ACR_NAME.azurecr.io"
echo "  AKS:       $AKS_NAME"
echo "  Speech:    $SPEECH_NAME (Region: $LOCATION)"
echo "  OpenAI:    $OPENAI_NAME (Region: $OPENAI_LOCATION)"
echo ""
echo "下一步:"
echo "  运行部署脚本: bash scripts/deploy.sh"
echo ""

# 保存配置到 .env 文件（仅本地使用）
ENV_FILE="$(dirname "$0")/../.env"
cat > "$ENV_FILE" <<EOF
# Azure Voice Assistant - 自动生成的环境配置
# 生成时间: $(date -u +"%Y-%m-%dT%H:%M:%SZ")
RESOURCE_GROUP=$RESOURCE_GROUP
LOCATION=$LOCATION
ACR_NAME=$ACR_NAME
AKS_NAME=$AKS_NAME
SPEECH_NAME=$SPEECH_NAME
OPENAI_NAME=$OPENAI_NAME
AZURE_SPEECH_SUBSCRIPTION_KEY=$SPEECH_KEY
AZURE_SPEECH_REGION=$LOCATION
AZURE_OPENAI_ENDPOINT=$OPENAI_ENDPOINT
AZURE_OPENAI_API_KEY=$OPENAI_KEY
AZURE_OPENAI_DEPLOYMENT_NAME=$OPENAI_DEPLOYMENT
EOF

ok "环境配置已保存到 .env 文件（仅本地使用，已在 .gitignore 中）"
