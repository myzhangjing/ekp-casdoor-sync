# 使用官方 .NET 8 SDK 镜像作为构建环境
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# 复制项目文件并还原依赖
COPY ["SyncEkpToCasdoor.Web/SyncEkpToCasdoor.Web.csproj", "SyncEkpToCasdoor.Web/"]
RUN dotnet restore "SyncEkpToCasdoor.Web/SyncEkpToCasdoor.Web.csproj"

# 复制所有源代码并构建
COPY . .
WORKDIR "/src/SyncEkpToCasdoor.Web"
RUN dotnet build "SyncEkpToCasdoor.Web.csproj" -c Release -o /app/build

# 发布应用
FROM build AS publish
RUN dotnet publish "SyncEkpToCasdoor.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

# 使用运行时镜像
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 8080

# 复制发布的文件
COPY --from=publish /app/publish .

# 设置环境变量
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# 启动应用
ENTRYPOINT ["dotnet", "SyncEkpToCasdoor.Web.dll"]
