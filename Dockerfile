# ─── Build ───────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first so layer caching kicks in when only source changes.
COPY ResumeTailorAI.sln .
COPY src/ResumeTailorAI.Web/ResumeTailorAI.Web.csproj src/ResumeTailorAI.Web/
RUN dotnet restore

COPY . .
RUN dotnet publish src/ResumeTailorAI.Web/ResumeTailorAI.Web.csproj \
    -c Release -o /app/publish /p:UseAppHost=false

# ─── Runtime ─────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "ResumeTailorAI.Web.dll"]
