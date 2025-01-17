FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080  
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["EmailAnalyzer.Server/EmailAnalyzer.Server.csproj", "EmailAnalyzer.Server/"]
COPY ["EmailAnalyzer.Shared/EmailAnalyzer.Shared.csproj", "EmailAnalyzer.Shared/"]
RUN dotnet restore "EmailAnalyzer.Server/EmailAnalyzer.Server.csproj"
COPY . .
WORKDIR "/src/EmailAnalyzer.Server"
RUN dotnet build "EmailAnalyzer.Server.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "EmailAnalyzer.Server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://+:8080  
ENTRYPOINT ["dotnet", "EmailAnalyzer.Server.dll"]