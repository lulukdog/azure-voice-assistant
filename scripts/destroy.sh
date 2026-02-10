#!/bin/bash
# ============================================================
# Azure Voice Assistant - 资源清理脚本
# 删除所有 Azure 资源（危险操作）
# ============================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ENV_FILE="$SCRIPT_DIR/../.env"

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

if [ -f "$ENV_FILE" ]; then
    set -a
    source "$ENV_FILE"
    set +a
fi

RESOURCE_GROUP="${RESOURCE_GROUP:-voice-assistant-rg}"

echo ""
echo -e "${RED}=========================================="
echo "  ⚠️  危险操作：删除所有 Azure 资源"
echo "==========================================${NC}"
echo ""
echo "将删除资源组: $RESOURCE_GROUP"
echo "这将删除该组下的所有资源（ACR, AKS, Speech, OpenAI 等）"
echo ""
read -p "确认删除？输入资源组名称确认: " CONFIRM

if [ "$CONFIRM" != "$RESOURCE_GROUP" ]; then
    echo "输入不匹配，已取消"
    exit 0
fi

echo ""
echo -e "${YELLOW}正在删除资源组 $RESOURCE_GROUP ...${NC}"
az group delete --name "$RESOURCE_GROUP" --yes --no-wait

echo -e "${GREEN}删除请求已提交（后台执行中）${NC}"
echo "查看状态: az group show --name $RESOURCE_GROUP --query properties.provisioningState -o tsv"
