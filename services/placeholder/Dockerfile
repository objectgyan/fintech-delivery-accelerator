FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Placeholder/Placeholder.csproj", "Placeholder/"]
RUN dotnet restore "Placeholder/Placeholder.csproj"
COPY . .
WORKDIR "/src/Placeholder"
RUN dotnet build "Placeholder.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Placeholder.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Placeholder.dll"]
