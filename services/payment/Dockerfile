# Build context is the repo root (services reference shared/observability):
#   docker build -f services/payment/Dockerfile .
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["NuGet.Config", "./"]
COPY ["shared/observability/Fintech.Observability/Fintech.Observability.csproj", "shared/observability/Fintech.Observability/"]
COPY ["services/payment/Payment.Api/Payment.Api.csproj", "services/payment/Payment.Api/"]
COPY ["services/payment/Payment.Application/Payment.Application.csproj", "services/payment/Payment.Application/"]
COPY ["services/payment/Payment.Domain/Payment.Domain.csproj", "services/payment/Payment.Domain/"]
COPY ["services/payment/Payment.Infrastructure/Payment.Infrastructure.csproj", "services/payment/Payment.Infrastructure/"]
RUN dotnet restore "services/payment/Payment.Api/Payment.Api.csproj"
COPY ["shared/observability/", "shared/observability/"]
COPY ["services/payment/", "services/payment/"]
WORKDIR "/src/services/payment/Payment.Api"
RUN dotnet build "Payment.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Payment.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Payment.Api.dll"]
