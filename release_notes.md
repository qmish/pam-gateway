# Релиз v0.9.0 — Хранение данных, мониторинг агентов, delta sync CMDB

## Хранение данных (Этап 1.6)
- **EF-хранилища агентов**: `EfAgentStore` и `EfAgentTicketStore` для Postgres/SqlServer — агенты теперь персистентны в БД
- **Soft delete**: все сущности (кроме аудита и билетов) поддерживают мягкое удаление через `IsDeleted`/`DeletedAt` с глобальными EF query filters
- **Индексы БД**: добавлены индексы на часто используемые поля (status, createdAt, targetId, sessionId, timestamp, eventType) для оптимизации запросов
- **Worker исправлен**: теперь регистрирует `ISessionStore`, `IAgentStore`, `IAgentTicketStore` при использовании Postgres/SqlServer
- **Миграция**: `AddAgentsAndSoftDelete` — таблицы `Agents`, `AgentTickets`, колонки soft delete, индексы

## Аутентификация (Этап 1.1)
- **JWT валидация**: `audience`, `issuer` и HTTPS metadata теперь валидируются в зависимости от конфигурации (строгий режим для продакшена)

## CMDB-интеграция (Этап 1.3)
- **Delta sync**: инкрементальная синхронизация CMDB по дате изменения — после первого полного синхро, последующие выбирают только изменённые объекты
- Полная пересинхронизация каждые N циклов (настраивается через `CmdbSync:FullSyncEveryNth`)
- Конфликты проверяются только при полной синхронизации

## Аудит (Этап 1.4)
- **Ротация аудита**: `AuditRotationService` — автоматическое удаление старых записей по retention-периоду
- Батчевое удаление для минимизации нагрузки на БД
- Настройка: `AuditRotation:Enabled`, `RetentionDays`, `BatchSize`

## Агенты (Этапы 2.1, 2.4)
- **Автоматический reconnect**: агент переподключается к API после 3 последовательных ошибок heartbeat
- **Graceful shutdown**: при остановке агент отправляет offline-статус на API
- **Auto-offline**: `AgentHealthMonitorService` автоматически переводит агентов в Offline при пропуске heartbeat
- Аудит-событие `agent.offline` при переходе агента в офлайн

## Тесты (26 новых)
- `EfAgentStoreTests` — CRUD, Register, UpdateHeartbeat (5 тестов)
- `EfAgentTicketStoreTests` — Issue, GetByTicket, Revoke, GetAll (5 тестов)
- `AgentHealthMonitorTests` — переход Online→Offline, аудит (4 теста)
- `AuditRotationServiceTests` — удаление, retention, batch (4 теста)
- `CmdbDeltaSyncTests` — full/delta sync, конфликты, FullSyncEveryNth (5 тестов)
- `AgentReconnectTests` — reconnect после ошибок, graceful shutdown (2 теста)
- Тестов при выпуске: **270** (196 юнит + 74 интеграционных), все проходят
