@echo off
echo ========================================
echo OAuth 登录快速测试
echo ========================================
echo.
echo 测试配置:
echo   应用地址: http://syn-ekp.fzcsps.com
echo   Casdoor: http://sso.fzcsps.com
echo.
echo [1/3] 测试应用服务...
curl -s -o nul -w "状态码: %%{http_code}\n" http://syn-ekp.fzcsps.com/
echo.
echo [2/3] 测试 Casdoor 服务...
curl -s -o nul -w "状态码: %%{http_code}\n" http://sso.fzcsps.com/
echo.
echo [3/3] 查看服务器日志...
echo.
ssh root@172.16.10.110 "docker logs --tail 20 syncekp-web"
echo.
echo ========================================
echo 测试完成
echo ========================================
echo.
echo 请在浏览器访问: http://syn-ekp.fzcsps.com
echo 使用任意 Casdoor 账号登录测试
echo.
pause
