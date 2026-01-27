FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/PamGateway.Worker/PamGateway.Worker.csproj", "src/PamGateway.Worker/"]
COPY ["src/PamGateway.Core/PamGateway.Core.csproj", "src/PamGateway.Core/"]
RUN dotnet restore "src/PamGateway.Worker/PamGateway.Worker.csproj"
COPY . .
RUN dotnet publish "src/PamGateway.Worker/PamGateway.Worker.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PamGateway.Worker.dll"]
