# Build context is the repo root (services reference shared/observability):
#   docker build -f services/notification/Dockerfile .
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["NuGet.Config", "./"]
COPY ["shared/observability/Fintech.Observability/Fintech.Observability.csproj", "shared/observability/Fintech.Observability/"]
COPY ["services/notification/Notification.Worker/Notification.Worker.csproj", "services/notification/Notification.Worker/"]
RUN dotnet restore "services/notification/Notification.Worker/Notification.Worker.csproj"
COPY ["shared/observability/", "shared/observability/"]
COPY ["services/notification/", "services/notification/"]
WORKDIR "/src/services/notification/Notification.Worker"
RUN dotnet build "Notification.Worker.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Notification.Worker.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Notification.Worker.dll"]
