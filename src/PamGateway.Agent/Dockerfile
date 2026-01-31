FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/PamGateway.Agent/PamGateway.Agent.csproj", "src/PamGateway.Agent/"]
RUN dotnet restore "src/PamGateway.Agent/PamGateway.Agent.csproj"
COPY . .
RUN dotnet publish "src/PamGateway.Agent/PamGateway.Agent.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PamGateway.Agent.dll"]
