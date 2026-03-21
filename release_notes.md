# Релиз v0.10.0

## Безопасность агента (2.2)
- **Верификация ticket** при создании сессии на агенте — агент проверяет валидность ticket через API перед установлением соединения
- **Ограничение bind-интерфейса** — настраиваемый `BindAddress` позволяет ограничить прослушивание только определённым IP
- Новый API-endpoint `GET /api/v1/agents/{agentId}/sessions/{sessionId}/verify-ticket`

## Управление сессиями (2.3)
- **WebSocket keepalive** — настраиваемый интервал пингов для поддержания соединения
- **Idle timeout** — автоматическое завершение неактивных сессий (по умолчанию 30 минут)
- **Лимит параллельных сессий** — агент отклоняет новые подключения при достижении `MaxParallelSessions` (503 Service Unavailable)

## SLA-эскалация (4.1)
- Автоматическая эскалация Pending-заявок, оставшихся без ответа approver дольше настраиваемого порога (`Sla:EscalationTimeoutMinutes`)
- Аудит-событие `access.sla_escalation` при срабатывании
- Обновление статуса в Jira с retry (до 3 попыток)

## Jira-интеграция (4.2)
- **Конфигурируемый маппинг статусов** через `Jira:TransitionMap` в appsettings — поддержка любых кастомных transition ID
- **Retry при недоступности** Jira — до 3 попыток с прогрессивной задержкой (2с, 4с)
- Приоритет `TransitionMap` над hardcoded fallback-маппингом

## RBAC (5.2)
- **Вложенные скобки** в label-выражениях — `((env=prod && role=db) || env=dev) && tier=1`
- **Аудит решений авторизации** — `PolicyDecisionAudit` фиксирует userId, targetId, protocol, matched/deny policy IDs и причину решения

## Observability (5.5)
- **Кастомные метрики** через `System.Diagnostics.Metrics`:
  - `pam.sessions.started/terminated/active`
  - `pam.requests.created/approved/denied/expired`
  - `pam.integration.errors`, `pam.policy.denials`
  - `pam.agents.online`
- Метрики автоматически экспортируются через OpenTelemetry OTLP

## Тесты
- **30 новых unit-тестов** (всего 300: 226 unit + 74 integration):
  - SLA-эскалация (6 тестов): срабатывание, пропуск свежих, disabled, ITSM retry
  - Аудит решений авторизации (6 тестов): allow/deny audit, matched IDs, protocol
  - Вложенные скобки (7 тестов): двойные/тройные уровни, NOT + скобки, complex
  - SessionTracker (4 теста): инкремент, декремент, потокобезопасность
  - Jira TransitionMap (4 теста): приоритет, fallback, unknown status
  - PamMetrics (3 теста): создание, инкременты, MeterListener
