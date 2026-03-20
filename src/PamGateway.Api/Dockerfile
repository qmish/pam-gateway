FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/PamGateway.Api/PamGateway.Api.csproj", "src/PamGateway.Api/"]
COPY ["src/PamGateway.Core/PamGateway.Core.csproj", "src/PamGateway.Core/"]
COPY ["src/PamGateway.Data/PamGateway.Data.csproj", "src/PamGateway.Data/"]
COPY ["src/PamGateway.Integrations/PamGateway.Integrations.csproj", "src/PamGateway.Integrations/"]
RUN dotnet restore "src/PamGateway.Api/PamGateway.Api.csproj"
COPY . .
RUN dotnet publish "src/PamGateway.Api/PamGateway.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PamGateway.Api.dll"]
