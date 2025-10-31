# 多阶段构建 Dockerfile for .NET 8 Blazor Server
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 复制项目文件并还原依赖
COPY ["SyncEkpToCasdoor.Web.csproj", "./"]
RUN dotnet restore "SyncEkpToCasdoor.Web.csproj"

# 复制所有文件并构建
COPY . .
RUN dotnet build "SyncEkpToCasdoor.Web.csproj" -c Release -o /app/build

# 发布应用
FROM build AS publish
RUN dotnet publish "SyncEkpToCasdoor.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

# 运行时镜像
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 5233

# 设置环境变量
ENV ASPNETCORE_URLS=http://+:5233
ENV ASPNETCORE_ENVIRONMENT=Production
ENV TZ=Asia/Shanghai

# 安装curl用于健康检查
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# 复制发布的文件
COPY --from=publish /app/publish .

# 入口点
ENTRYPOINT ["dotnet", "SyncEkpToCasdoor.Web.dll"]
