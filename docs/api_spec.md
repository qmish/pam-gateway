# API спецификация (MVP)

Базовый URL: `/api/v1`

## Auth
### GET /auth/login
Редирект в Keycloak (OIDC).

### GET /auth/callback
Обмен code → tokens. Возвращает сессию пользователя.

### POST /auth/logout
Завершение сессии пользователя.

## Access Requests (JIT)
### POST /access/requests
Запрос JIT доступа.

**Body:**
```json
{
  "targetId": "SCH-249443",
  "durationMinutes": 120,
  "reason": "incident-123"
}
```

**Response 201:**
```json
{
  "requestId": "REQ-001",
  "status": "pending",
  "expiresAt": "2026-01-26T12:00:00Z"
}
```

### GET /access/requests/{id}
Статус заявки.

### POST /access/requests/{id}/approve
Approval (через ITSM или вручную).

## Sessions
### POST /sessions
Создать сессию (RDP/SSH).

**Body:**
```json
{
  "targetId": "SCH-249443",
  "protocol": "RDP",
  "requestId": "REQ-001"
}
```

**Response 201:**
```json
{
  "sessionId": "SES-001",
  "status": "active",
  "connectUrl": "wss://pam.example/sessions/SES-001"
}
```

### GET /sessions/{id}
Статус сессии.

### POST /sessions/{id}/terminate
Завершить сессию.

## Targets
### GET /targets
Список систем для пользователя.

### GET /targets/{id}
Детали системы.

## Roles
### GET /roles
Список ролей.

### GET /roles/{id}
Детали роли.

### POST /roles
Создание роли.

## Policies
### GET /policies
Список политик.

### GET /policies/{id}
Детали политики.

### POST /policies
Создание политики.

## Approvals
### GET /approvals
Список согласований.

### POST /approvals
Создать согласование вручную.

## CMDB
### POST /cmdb/sync
Импорт систем из Jira Insight (IQL).

## Audit
### GET /audit/events
Фильтрация по пользователю/системе/периоду.

## Ошибки
- 400: некорректный запрос
- 401: неаутентифицирован
- 403: нет прав
- 404: не найдено
- 409: конфликт (например, нет активного JIT)
