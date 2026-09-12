# ==============================================================================
# STAGE 1: Build Backend (.NET SDK)
# ==============================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-backend
WORKDIR /app

COPY TijarahAi.slnx ./
COPY TijarahAi/TijarahAi.csproj ./TijarahAi/
RUN dotnet restore TijarahAi/TijarahAi.csproj

COPY TijarahAi/ ./TijarahAi/
RUN dotnet publish TijarahAi/TijarahAi.csproj -c Release -o /app/publish

# ==============================================================================
# STAGE 2: Production Runtime
# ==============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

COPY --from=build-backend /app/publish ./
COPY TijarahAi/data/ ./data/

ENTRYPOINT ["dotnet", "TijarahAi.dll"]
