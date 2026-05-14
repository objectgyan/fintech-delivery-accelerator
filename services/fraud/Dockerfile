FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Fraud.Api/Fraud.Api.csproj", "Fraud.Api/"]
COPY ["Fraud.Application/Fraud.Application.csproj", "Fraud.Application/"]
COPY ["Fraud.Domain/Fraud.Domain.csproj", "Fraud.Domain/"]
COPY ["Fraud.Infrastructure/Fraud.Infrastructure.csproj", "Fraud.Infrastructure/"]
RUN dotnet restore "Fraud.Api/Fraud.Api.csproj"
COPY . .
WORKDIR "/src/Fraud.Api"
RUN dotnet build "Fraud.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Fraud.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Fraud.Api.dll"]
