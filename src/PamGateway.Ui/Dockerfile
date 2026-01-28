FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY PamGateway.sln ./
COPY src/PamGateway.Ui/PamGateway.Ui.csproj src/PamGateway.Ui/
RUN dotnet restore "src/PamGateway.Ui/PamGateway.Ui.csproj"

COPY . .
RUN dotnet publish "src/PamGateway.Ui/PamGateway.Ui.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "PamGateway.Ui.dll"]
