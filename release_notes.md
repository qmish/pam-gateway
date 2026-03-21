# Релиз v0.11.0

## ITSM-интеграция (4.2)
- **Все Jira transitions**: поддержка Reopened, Cancelled/Canceled, In Progress, Open — корректный маппинг обратно во внутренние статусы (Pending/Denied)
- **Двунаправленные комментарии**: `AddCommentAsync` / `GetCommentsAsync` в IItsmClient для синхронизации комментариев между PAM и Jira
- Конфигурируемый маппинг через `StatusMap` в appsettings с приоритетом над hardcoded

## Dead Letter Queue (4.3)
- **InMemoryDeadLetterStore** в Core — хранилище для неотправленных ITSM-операций
- **DeadLetterProcessor** — фоновый сервис (каждые 5 минут), retry до 10 попыток с инкрементальным backoff
- Поддержка операций: `update_status`, `add_comment`
- Автоматическое завершение после исчерпания лимита попыток

## Уведомления о статусах заявки (4.1)
- **WebhookNotificationService** — HTTP POST уведомления при смене статуса заявки
- Конфигурация: `Notifications:Enabled`, `Notifications:WebhookUrl`, `Notifications:WebhookSecret`
- Фильтрация по типу события (`Notifications:Events`)
- Graceful degradation: ошибки webhook не блокируют основной процесс

## RBAC — наследование политик (5.2)
- **Иерархия ролей** через `Access:RoleHierarchy` в appsettings (child → parent)
- Мульти-уровневая иерархия: Leaf → Mid → Root автоматически наследует все политики по цепочке
- Deny из родительской роли корректно блокирует доступ дочерней
- Дедупликация политик при наследовании (каждая политика применяется один раз)

## Observability (5.5)
- **Serilog интеграция** — structured logging в JSON-формате
- Включение через `Logging:UseSerilog=true` в конфигурации
- Автоматическое обогащение: `Service`, `LogContext`
- Подготовка для вывода в ELK/Loki

## Тесты
- **32 новых unit-теста** → итого **332** (258 unit + 74 integration):
  - Jira transitions (7 тестов): Cancelled→Denied, Reopened→Pending, ConfiguredMap, Unknown, Duplicate, MissingKey
  - Jira comments (3 теста): AddComment, GetComments, empty response
  - Dead Letter Queue (8 тестов): CRUD, resolve, retry, limit, processor (resolve/fail/max retries/comments)
  - Notifications (6 тестов): webhook send, event filter, disabled, secret header, error handling, noop
  - Role Hierarchy (5 тестов): child inherits, multi-level, no duplicates, deny propagation, no hierarchy
  - JiraTransitionMap (3 теста, из v0.10.0): priority, fallback
