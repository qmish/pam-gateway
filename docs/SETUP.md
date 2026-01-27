# Настройка окружения и запуск

## Установка .NET SDK 8 (Windows)
В PowerShell:
```
winget install Microsoft.DotNet.SDK.8
```
Проверка:
```
dotnet --version
```

## Сборка образов (локально)
```
docker build -t pam-gateway-api:latest -f src/PamGateway.Api/Dockerfile .
docker build -t pam-gateway-worker:latest -f src/PamGateway.Worker/Dockerfile .
```

## Minikube (namespace)
```
kubectl apply -f infra/k8s/namespace.yaml
kubectl apply -f infra/k8s/postgres.yaml
kubectl apply -f infra/k8s/keycloak.yaml
kubectl apply -f infra/k8s/pam-gateway-api.yaml
kubectl apply -f infra/k8s/pam-gateway-worker.yaml
```

## Helm (альтернатива)
```
helm install pam-gateway ./helm/pam-gateway -n pam-gateway -f helm/pam-gateway/values.minikube.yaml
```

## Auth (Keycloak)
В Minikube можно временно отключить авторизацию:
- `values.minikube.yaml` → `api.auth.enabled: false`

В проде/интеграции включить обратно:
- `values.yaml` → `api.auth.enabled: true`

## CMDB (Insight)
Временная заглушка (без реального Insight):
- `values.minikube.yaml` → `api.cmdb.provider: "Stub"`
- Список систем задается в `api.targets`

Для реального Insight:
- `api.cmdb.provider: "Insight"`
- Заполнить `api.cmdb.baseUrl`, `api.cmdb.iql`, `api.cmdb.authType`, `api.cmdb.username`, `api.cmdb.token`

## Ingress (внешний доступ)
```
minikube addons enable ingress
minikube tunnel
```
Добавить в hosts:
`127.0.0.1 pam.local`

Проверка:
`http://pam.local/api/v1/health`

## Jira: токен и transition IDs
1) Получить токен в Jira (Cloud: API token, Server/DC: PAT/Basic).
2) Узнать transition ID для нужных статусов (To Do/In Progress/Done).
   Обычно через REST:
   `GET /rest/api/2/issue/{issueKey}/transitions`
3) Заполнить:
   - `Jira:Token`
   - `Jira:TransitionPending`
   - `Jira:TransitionApproved`
   - `Jira:TransitionExpired`

## PostgreSQL
По умолчанию используется:
`Host=postgres;Port=5432;Database=pam_gateway;Username=pam;Password=pam`

## EF Core миграции
Миграции добавлены в `src/PamGateway.Data/Migrations`.
Применение (после установки .NET SDK 8):
```
dotnet tool install --global dotnet-ef
dotnet ef database update --project src/PamGateway.Data --startup-project src/PamGateway.Api
```
