# SLD: Teleport‑подобная архитектура (детализация)

## 1. Сервисы и зоны
- **Control Plane**: Auth/API (PAM), PostgreSQL, Audit/Event storage.
- **Proxy Plane**: Session Broker/Proxy (в плане), Ingress.
- **Agent Plane**: агенты на целевых системах (в плане).
- **Mgmt**: SIEM, мониторинг.

## 2. Сетевые взаимодействия (минимум)
- User → Proxy/API: HTTPS 443.
- Proxy/API → Auth: HTTPS/gRPC (внутренний).
- Proxy → Agent: mTLS/WebSocket (туннель).
- Agent → Target: SSH 22, RDP 3389, HTTPS 443.
- Auth → PostgreSQL: TCP 5432.
- Auth → SIEM: HTTPS/syslog.
- Auth → ITSM/CMDB: HTTPS.

## 3. Диаграмма развертывания
```mermaid
flowchart TB
  subgraph control [Control Plane]
    Auth[PAM Auth/API]
    PG[(PostgreSQL)]
  end
  subgraph proxy [Proxy Plane]
    Ingress[Ingress]
    Broker[Session Broker]
  end
  subgraph agent [Agent Plane]
    Agent[Target Agent]
    Target[Target System]
  end
  subgraph mgmt [Mgmt]
    SIEM[SIEM]
    ITSM[Jira ITSM]
    CMDB[CMDB Stub/Insight]
  end

  Ingress --> Broker --> Auth
  Auth --> PG
  Broker --> Agent --> Target
  Auth --> SIEM
  Auth --> ITSM
  Auth --> CMDB
```

## 4. Контракты между компонентами
- **Auth ↔ Proxy**: выдача session tickets, RBAC, TTL.
- **Proxy ↔ Agent**: создание/завершение сессии, канал трафика.
- **Agent ↔ Auth**: heartbeat, audit, запись/метаданные.

## 5. Масштабирование и HA
- Auth/API: горизонтальное масштабирование + shared DB.
- Proxy/Broker: active‑active, sticky‑sessions по session ID.
- Agent: локальный процесс у цели.
- Recording storage: S3/MinIO для прод, локально для dev.

## 6. Конфигурация (MVP)
- `Auth.Enabled`: false (Minikube), true (prod).
- `Cmdb.Provider`: Stub (dev), Insight (prod).
- `SessionRecording.Mode`: node/proxy + sync/async (в плане).

## 7. Sequence: Access Request / Approval
```mermaid
sequenceDiagram
  participant User
  participant API as PAM Auth/API
  participant ITSM as Jira ITSM
  participant Approver
  participant Worker
  User->>API: POST /api/v1/access-requests
  API->>ITSM: Create issue
  ITSM-->>API: issueKey
  API-->>User: RequestId + issueKey
  Approver->>ITSM: Approve transition
  Worker->>ITSM: Poll/transition status
  Worker->>API: Update request status
  API-->>User: Status Approved/Denied
```
