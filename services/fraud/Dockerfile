# Build context is the repo root (services reference shared/observability):
#   docker build -f services/fraud/Dockerfile .
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["NuGet.Config", "./"]
COPY ["shared/observability/Fintech.Observability/Fintech.Observability.csproj", "shared/observability/Fintech.Observability/"]
COPY ["services/fraud/Fraud.Api/Fraud.Api.csproj", "services/fraud/Fraud.Api/"]
COPY ["services/fraud/Fraud.Application/Fraud.Application.csproj", "services/fraud/Fraud.Application/"]
COPY ["services/fraud/Fraud.Domain/Fraud.Domain.csproj", "services/fraud/Fraud.Domain/"]
COPY ["services/fraud/Fraud.Infrastructure/Fraud.Infrastructure.csproj", "services/fraud/Fraud.Infrastructure/"]
RUN dotnet restore "services/fraud/Fraud.Api/Fraud.Api.csproj"
COPY ["shared/observability/", "shared/observability/"]
COPY ["services/fraud/", "services/fraud/"]
WORKDIR "/src/services/fraud/Fraud.Api"
RUN dotnet build "Fraud.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Fraud.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Fraud.Api.dll"]
