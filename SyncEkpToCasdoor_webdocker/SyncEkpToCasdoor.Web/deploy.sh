#!/bin/bash
# Deploy script for Linux/WSL
# Usage: bash deploy.sh

SERVER_IP="172.16.10.110"
SERVER_USER="root"
SERVER_PASS="fwater@163.com"
DEPLOY_PATH="/opt/syncekp-web"
APP_NAME="syncekp-casdoor-web"

echo "========================================"
echo "Deploy SyncEkpToCasdoor.Web to Server"
echo "Server: $SERVER_IP:9000"
echo "========================================"
echo ""

# Step 1: Create package
echo "[1/5] Creating deployment package..."
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
PACKAGE_NAME="deploy_${TIMESTAMP}.zip"

zip -r "$PACKAGE_NAME" \
    Dockerfile \
    docker-compose.yml \
    .dockerignore \
    appsettings.json \
    appsettings.Production.json \
    SyncEkpToCasdoor.Web.csproj \
    Program.cs \
    Components \
    Controllers \
    Models \
    Services \
    wwwroot \
    -x "*.log" "bin/*" "obj/*"

echo "Package created: $PACKAGE_NAME"
echo ""

# Step 2: Upload to server
echo "[2/5] Uploading to server..."
echo "Password: $SERVER_PASS"

if command -v sshpass &> /dev/null; then
    # Use sshpass if available
    sshpass -p "$SERVER_PASS" scp -o StrictHostKeyChecking=no "$PACKAGE_NAME" ${SERVER_USER}@${SERVER_IP}:/tmp/
else
    # Manual upload
    echo "Please enter password when prompted: $SERVER_PASS"
    scp -o StrictHostKeyChecking=no "$PACKAGE_NAME" ${SERVER_USER}@${SERVER_IP}:/tmp/
fi

if [ $? -ne 0 ]; then
    echo "Upload failed!"
    exit 1
fi

echo "Upload completed"
echo ""

# Step 3-5: Deploy on server
echo "[3/5] Extracting files..."
echo "[4/5] Stopping old container..."
echo "[5/5] Building and starting container..."
echo ""

REMOTE_COMMANDS="
mkdir -p $DEPLOY_PATH
cd $DEPLOY_PATH
echo 'Extracting files...'
unzip -o /tmp/$PACKAGE_NAME
rm /tmp/$PACKAGE_NAME
echo 'Stopping old container...'
docker stop $APP_NAME 2>/dev/null || true
docker rm $APP_NAME 2>/dev/null || true
echo 'Building new image...'
docker-compose build
echo 'Starting container...'
docker-compose up -d
sleep 5
echo 'Checking container status...'
docker ps | grep $APP_NAME
"

if command -v sshpass &> /dev/null; then
    sshpass -p "$SERVER_PASS" ssh -o StrictHostKeyChecking=no ${SERVER_USER}@${SERVER_IP} "$REMOTE_COMMANDS"
else
    echo "Please enter password when prompted: $SERVER_PASS"
    ssh -o StrictHostKeyChecking=no ${SERVER_USER}@${SERVER_IP} "$REMOTE_COMMANDS"
fi

if [ $? -eq 0 ]; then
    echo ""
    echo "========================================"
    echo "Deployment completed successfully!"
    echo "========================================"
    echo ""
    echo "Access URL:"
    echo "  Internal: http://${SERVER_IP}:9000"
    echo "  External: http://syn-ekp.fzcsps.com:9000"
    echo ""
    echo "Check logs:"
    echo "  ssh ${SERVER_USER}@${SERVER_IP}"
    echo "  docker logs -f $APP_NAME"
    echo ""
else
    echo ""
    echo "Deployment failed!"
    echo ""
fi

# Cleanup
rm -f "$PACKAGE_NAME"
