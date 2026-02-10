#!/bin/bash
# ============================================================
# Azure Voice Assistant - 构建 & 部署脚本
# 构建 Docker 镜像，推送到 ACR，部署到 AKS
# ============================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
ENV_FILE="$PROJECT_ROOT/.env"

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
# 加载环境变量
# ============================================================
if [ -f "$ENV_FILE" ]; then
    info "加载 .env 配置..."
    set -a
    source "$ENV_FILE"
    set +a
    ok "配置已加载"
else
    error ".env 文件不存在。请先运行: bash scripts/setup-azure.sh"
    exit 1
fi

# 参数
IMAGE_TAG="${1:-latest}"
ACR_LOGIN_SERVER="${ACR_NAME}.azurecr.io"
IMAGE_NAME="voice-assistant"
FULL_IMAGE="${ACR_LOGIN_SERVER}/${IMAGE_NAME}:${IMAGE_TAG}"

echo ""
echo "=========================================="
echo "  Azure Voice Assistant 应用部署"
echo "=========================================="
echo "  镜像:  $FULL_IMAGE"
echo "  AKS:   $AKS_NAME"
echo "=========================================="
echo ""

# ============================================================
# Step 1: 登录 ACR
# ============================================================
info "Step 1/5: 登录 Azure Container Registry..."
az acr login --name "$ACR_NAME"
ok "ACR 登录成功"

# ============================================================
# Step 2: 构建 Docker 镜像
# ============================================================
info "Step 2/5: 构建 Docker 镜像..."
docker build \
    -f "$PROJECT_ROOT/deploy/docker/Dockerfile" \
    -t "$FULL_IMAGE" \
    "$PROJECT_ROOT"
ok "镜像构建完成: $FULL_IMAGE"

# ============================================================
# Step 3: 推送镜像到 ACR
# ============================================================
info "Step 3/5: 推送镜像到 ACR..."
docker push "$FULL_IMAGE"

# 同时推送 latest 标签
if [ "$IMAGE_TAG" != "latest" ]; then
    docker tag "$FULL_IMAGE" "${ACR_LOGIN_SERVER}/${IMAGE_NAME}:latest"
    docker push "${ACR_LOGIN_SERVER}/${IMAGE_NAME}:latest"
fi
ok "镜像已推送"

# ============================================================
# Step 4: 确保 AKS 凭据可用
# ============================================================
info "Step 4/5: 配置 kubectl..."
az aks get-credentials \
    --resource-group "$RESOURCE_GROUP" \
    --name "$AKS_NAME" \
    --overwrite-existing
ok "kubectl 已配置"

# ============================================================
# Step 5: 部署到 AKS
# ============================================================
info "Step 5/5: 部署到 AKS..."

# 应用 ConfigMap
kubectl apply -f "$PROJECT_ROOT/deploy/k8s/configmap.yaml"

# 更新 ConfigMap 中的 OpenAI Endpoint（不放在 Secret 中的非敏感配置部分）
kubectl patch configmap voice-assistant-config \
    -n voice-assistant \
    --type merge \
    -p "{\"data\":{\"AzureOpenAI__Endpoint\":\"$AZURE_OPENAI_ENDPOINT\"}}" \
    2>/dev/null || true

# 替换 deployment.yaml 中的镜像地址并部署
sed "s|\${ACR_NAME}|${ACR_NAME}|g" "$PROJECT_ROOT/deploy/k8s/deployment.yaml" | \
    sed "s|:latest|:${IMAGE_TAG}|g" | \
    kubectl apply -f -

ok "部署完成"

# ============================================================
# 等待部署就绪
# ============================================================
info "等待 Pod 就绪..."
kubectl rollout status deployment/voice-assistant \
    -n voice-assistant \
    --timeout=300s

echo ""
echo "=========================================="
echo -e "${GREEN}  部署成功!${NC}"
echo "=========================================="
echo ""

# 获取 Ingress IP
INGRESS_IP=$(kubectl get svc -n ingress-nginx ingress-nginx-controller \
    -o jsonpath='{.status.loadBalancer.ingress[0].ip}' 2>/dev/null || echo "Pending")

echo "Pod 状态:"
kubectl get pods -n voice-assistant
echo ""
echo "Service 状态:"
kubectl get svc -n voice-assistant
echo ""

if [ "$INGRESS_IP" != "Pending" ] && [ -n "$INGRESS_IP" ]; then
    echo "Ingress IP: $INGRESS_IP"
    echo ""
    echo "请将以下 DNS 记录指向此 IP:"
    echo "  voice-assistant.example.com  →  $INGRESS_IP"
    echo ""
    echo "或直接通过 IP 访问（需修改 hosts）:"
    echo "  echo '$INGRESS_IP voice-assistant.example.com' >> /etc/hosts"
else
    warn "Ingress IP 尚未分配，请稍后运行以下命令检查:"
    echo "  kubectl get svc -n ingress-nginx ingress-nginx-controller"
fi

echo ""
echo "有用的命令:"
echo "  查看日志:   kubectl logs -f deployment/voice-assistant -n voice-assistant"
echo "  查看 Pod:   kubectl get pods -n voice-assistant"
echo "  端口转发:   kubectl port-forward svc/voice-assistant 8080:80 -n voice-assistant"
echo "  重新部署:   bash scripts/deploy.sh \$(date +%s)"
