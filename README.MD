# PAM Gateway

Платформа для контролируемого доступа к корпоративным системам с JIT‑доступами, аудитом и записью сессий. Архитектура разделяет control plane (API/DB) и data plane (proxy/agent).

## Компоненты
- `PamGateway.Api` — REST API, заявки, политики, аудит.
- `PamGateway.Worker` — фоновые задачи (SLA/TTL/ITSM).
- `PamGateway.Agent` — агент на стороне цели (регистрация, heartbeat, проксирование).
- `PamGateway.Ui` — веб‑интерфейс для управления подключениями.
- `PamGateway.Data` — EF Core + PostgreSQL.
- `helm/pam-gateway` — Helm chart для Kubernetes/Minikube.

## Быстрый старт (локально)
1) Поднять PostgreSQL и применить миграции:
```
dotnet ef database update
```
2) Запустить API:
```
dotnet run --project src/PamGateway.Api
```
3) Запустить агент:
```
dotnet run --project src/PamGateway.Agent
```
4) Запустить UI:
```
dotnet run --project src/PamGateway.Ui
```

## Конфигурация
Основные параметры:
- `Auth:Enabled` — переключатель авторизации (false для локальной разработки).
- `Cmdb:Provider` — `Stub` или `Insight`.
- `Storage:Provider` — `Postgres` или `InMemory`.
- `Agent:ApiBaseUrl`, `Agent:PublicUrl`, `Agent:ListenUrl` — параметры агента.
- `Api:BaseUrl` — адрес API для UI.

## Документация
Вся документация находится в `docs/`:
- `docs/hld.md` — High Level Design
- `docs/sld.md` — Low Level Design
- `docs/backlog.md` — план реализации
- `docs/agent_protocol.md` — протокол агента
- `docs/session_recording_architecture.md` — запись сессий
- `docs/SETUP.md` — окружение и запуск
