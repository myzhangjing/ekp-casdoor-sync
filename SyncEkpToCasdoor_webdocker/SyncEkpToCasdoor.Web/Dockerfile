# 多阶段构建 Dockerfile for .NET 9 Blazor Server
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
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
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 9000

# 设置环境变量
ENV ASPNETCORE_URLS=http://+:9000
ENV ASPNETCORE_ENVIRONMENT=Production

# 复制发布的文件
COPY --from=publish /app/publish .

# 入口点
ENTRYPOINT ["dotnet", "SyncEkpToCasdoor.Web.dll"]
