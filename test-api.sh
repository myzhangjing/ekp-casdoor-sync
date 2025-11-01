#!/bin/bash

BASE_URL="http://syncas.fzcsps.com"
ADMIN_USER="admin"
ADMIN_PASS="sosy3080@sohu.com"

echo "=========================================="
echo "测试 1: 特权登录"
echo "=========================================="

# 使用curl测试admin登录
LOGIN_RESPONSE=$(curl -s -c /tmp/cookies.txt -X POST \
  -H "Content-Type: application/json" \
  -d "{\"username\":\"$ADMIN_USER\",\"password\":\"$ADMIN_PASS\"}" \
  $BASE_URL/api/admin-login)

echo "登录响应: $LOGIN_RESPONSE"

if echo "$LOGIN_RESPONSE" | grep -q "\"success\":true"; then
    echo "✅ 特权登录成功"
else
    echo "❌ 特权登录失败"
    exit 1
fi

echo ""
echo "=========================================="
echo "测试 2: 获取配置"
echo "=========================================="

CONFIG_RESPONSE=$(curl -s -b /tmp/cookies.txt \
  $BASE_URL/api/config/settings)

echo "配置响应: $CONFIG_RESPONSE"

if echo "$CONFIG_RESPONSE" | grep -q "\"success\":true"; then
    echo "✅ 获取配置成功"
else
    echo "❌ 获取配置失败"
fi

echo ""
echo "=========================================="
echo "测试 3: 保存配置"
echo "=========================================="

SAVE_RESPONSE=$(curl -s -b /tmp/cookies.txt -X POST \
  -H "Content-Type: application/json" \
  -d "{\"domain\":\"syncas.fzcsps.com\",\"protocol\":\"http\",\"allowedUsers\":\"testuser@example.com\",\"currentPassword\":\"\",\"newPassword\":\"\"}" \
  $BASE_URL/api/config/settings)

echo "保存响应: $SAVE_RESPONSE"

if echo "$SAVE_RESPONSE" | grep -q "\"success\":true"; then
    echo "✅ 保存配置成功"
else
    echo "❌ 保存配置失败"
fi

echo ""
echo "=========================================="
echo "测试 4: 验证配置已持久化"
echo "=========================================="

# 检查配置文件
ssh root@172.16.10.110 "cat /root/ekp-casdoor-sync/SyncEkpToCasdoor_webdocker/config/appsettings.Production.json"

echo ""
echo "=========================================="
echo "测试 5: 登出功能"
echo "=========================================="

LOGOUT_RESPONSE=$(curl -s -b /tmp/cookies.txt \
  -L $BASE_URL/logout)

if echo "$LOGOUT_RESPONSE" | grep -q "登录"; then
    echo "✅ 登出成功,重定向到登录页"
else
    echo "⚠️  登出响应: $(echo $LOGOUT_RESPONSE | head -c 200)"
fi

# 清理
rm -f /tmp/cookies.txt

echo ""
echo "=========================================="
echo "所有测试完成!"
echo "=========================================="
