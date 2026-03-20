# Roadmap: PAM Gateway — от MVP до продакшена

> Статус-маркеры: ✅ реализовано, 🔲 предстоит сделать

---

## Этап 0 — Тестовое покрытие существующего кода

> Цель: стабилизировать уже написанный функционал перед дальнейшим развитием.

### 0.1 Инфраструктура тестирования

- ✅ Создать проект `PamGateway.Tests.Unit` (xUnit + FluentAssertions + NSubstitute)
- ✅ Создать проект `PamGateway.Tests.Integration` (xUnit + `WebApplicationFactory`)
- 🔲 Настроить `dotnet test` в CI (GitHub Actions / аналог)
- 🔲 Подключить генерацию отчётов покрытия (coverlet → отчёт в PR)

### 0.2 Unit-тесты доменного слоя (`PamGateway.Core`)

- ✅ Тесты enum-значений и корректности record-моделей (сериализация/десериализация)
- ✅ Тесты `AccessPolicyEvaluator` — все комбинации allow/deny, совпадение ролей, протоколов
- ✅ Тесты `LabelExpressionEvaluator` — парсинг выражений (`&&`, `||`, `!`, `=`, `!=`), граничные случаи, невалидные выражения
- ✅ Тесты `AuditEventFactory` — корректность полей для каждого типа события

### 0.3 Unit-тесты хранилищ

- ✅ Тесты `InMemoryAccessRequestStore` — CRUD, GetByItsmKey, идемпотентность
- ✅ Тесты `InMemorySessionStore` — CRUD, обновление статуса
- ✅ Тесты `InMemoryTargetStore` — AddOrUpdate, AddOrUpdateRange, дедупликация
- ✅ Тесты `InMemoryRecordingStore` — CRUD
- ✅ Тесты `InMemoryAuditStore` — Add, GetAll
- ✅ Тесты `InMemoryRoleStore`, `InMemoryPolicyStore`, `InMemoryApprovalStore`
- ✅ Тесты `InMemoryAgentStore` — Register, UpdateHeartbeat, смена статуса
- ✅ Тесты `InMemoryAgentTicketStore` — Issue, GetByTicket, Revoke, истечение срока

### 0.4 Integration-тесты API-контроллеров

- ✅ `HealthController` — GET `/api/v1/health` возвращает 200
- ✅ `TargetsController` — GET/POST, фильтрация, обновление
- ✅ `AccessRequestsController` — создание заявки, approve, deny, повторные вызовы
- ✅ `ApprovalsController` — создание согласования, связь с заявкой
- ✅ `SessionsController` — создание сессии, terminate, выдача ticket
- ✅ `AgentsController` — register, heartbeat, список
- ✅ `RecordingsController` — CRUD, upload/download контента
- ✅ `AuditController` — фильтрация по user/target/date
- ✅ `RolesController` — CRUD ролей
- ✅ `PoliciesController` — CRUD политик
- ✅ `CmdbController` — sync (Stub-провайдер)
- ✅ `JiraWebhooksController` — обработка webhook-событий

### 0.5 Integration-тесты EF-хранилищ (Postgres через Testcontainers)

- 🔲 `EfAccessRequestStore` — CRUD + фильтрация
- 🔲 `EfSessionStore` — CRUD + обновление статуса
- 🔲 `EfTargetStore` — AddOrUpdate, AddOrUpdateRange, labels JSON
- 🔲 `EfRecordingStore` — CRUD
- 🔲 `EfAuditStore` — запись и чтение событий
- 🔲 `EfRoleStore`, `EfPolicyStore`, `EfApprovalStore`
- 🔲 Тест применения всех миграций на чистую БД

### 0.6 Тесты интеграций

- ✅ `JiraItsmClient` — мок HTTP, создание/обновление заявки, маппинг статусов
- ✅ `JiraInsightClient` — мок HTTP, импорт объектов по IQL
- ✅ `StubCmdbClient` — возвращает фиксированные данные

### 0.7 Тесты Worker

- ✅ `AccessRequestWorker` — истечение TTL переводит заявку в Expired
- ✅ `AccessRequestWorker` — синхронизация статусов с Jira

### 0.8 Тесты Agent

- 🔲 Регистрация и heartbeat (мок API)
- 🔲 TCP→WebSocket прокси — установка и закрытие соединения

### 0.9 Тесты WebSocket-прокси (API)

- 🔲 `/ws/sessions/{sessionId}` — корректный двунаправленный проброс сообщений
- 🔲 Обработка ошибок: сессия не найдена, агент оффлайн, невалидный target

### 0.10 Тесты UI (`PamGateway.Ui`)

- 🔲 `ApiClient` — мок HTTP, проверка всех методов
- 🔲 Razor Pages — интеграционные тесты рендеринга страниц (200 OK, наличие ключевых элементов)

---

## Этап 1 — Стабилизация Control Plane (этап A по бэклогу)

> Цель: довести базовый API до продакшен-качества.

### 1.1 Аутентификация и авторизация

- ✅ JWT Bearer + Keycloak OIDC (переключаемый `Auth:Enabled`)
- ✅ `KeycloakRoleClaimsTransformation` — маппинг групп → роли
- 🔲 Валидация `audience`, `issuer`, HTTPS metadata в продакшене
- 🔲 Rate limiting на эндпоинтах аутентификации
- 🔲 Refresh-token flow (если UI работает напрямую с Keycloak)
- 🔲 **Тесты:** авторизация — доступ с валидным/невалидным токеном, проверка ролей

### 1.2 RBAC и политики

- ✅ Роли: PAM_Administrator, Security_Auditor, System_Admin_Windows, System_Admin_Linux
- ✅ Политики с `TargetLabelSelector` и `AllowedProtocols`
- ✅ `AccessPolicyEvaluator` + `LabelExpressionEvaluator`
- ✅ Добавить роли: DB_Admin, Network_Admin, OneC_Admin, App_Support, DevOps, ServiceDesk
- ✅ Реализовать `Effect = Deny` (сейчас только Allow)
- 🔲 Кэширование результатов оценки политик
- 🔲 **Тесты:** все комбинации ролей и политик, deny overrides allow

### 1.3 CMDB-интеграция

- ✅ `Cmdb:Provider` Stub / Insight
- ✅ `StubCmdbClient`, `JiraInsightClient`
- 🔲 Инкрементальная синхронизация (delta sync по дате изменения)
- ✅ Периодическая автосинхронизация (Background Service)
- ✅ Логирование конфликтов при sync (дубликаты, удалённые системы)
- ✅ **Тесты:** sync с пустой CMDB, с конфликтами, с большим набором данных

### 1.4 Аудит

- ✅ `AuditController` с фильтрацией по user/target/date
- ✅ `AuditEventFactory` для генерации событий
- ✅ Пагинация результатов аудита (offset/limit или cursor)
- 🔲 Экспорт в SIEM (syslog / HTTP webhook)
- 🔲 Неизменяемость записей аудита (append-only, запрет UPDATE/DELETE)
- 🔲 Ротация и архивация старых записей
- 🔲 **Тесты:** фильтрация, пагинация, формат событий SIEM

### 1.5 Валидация и обработка ошибок API

- ✅ Единообразная модель ошибок (RFC 7807 Problem Details)
- ✅ Валидация входных данных на всех эндпоинтах (DataAnnotations)
- ✅ Global exception handler / middleware
- ✅ **Тесты:** невалидные запросы возвращают 400 с корректным телом

### 1.6 Хранение данных

- ✅ InMemory / Postgres / SQL Server провайдеры
- ✅ EF Core миграции (5 штук)
- 🔲 Перевести `IAgentStore` и `IAgentTicketStore` из InMemory в Postgres
- 🔲 Индексы в БД для часто используемых запросов (status, createdAt, targetId)
- 🔲 Soft delete для сущностей (вместо физического удаления)
- 🔲 **Тесты:** миграции на чистую БД, rollback миграций

### 1.7 Демо-данные и Seed

- ✅ `DemoDataSeeder`
- ✅ Флаг `DemoData:Enabled` = false по умолчанию в prod
- ✅ Seed начальных ролей и политик при первом запуске (отдельно от демо — `SystemDataSeeder`)
- ✅ **Тесты:** seed на пустую БД, повторный seed не дублирует данные

---

## Этап 2 — Агенты v1 (этап B по бэклогу)

> Цель: надёжный агент-демон и проксирование сессий.

### 2.1 Агент-демон

- ✅ Базовый агент с регистрацией и heartbeat
- ✅ WebSocket TCP→WS прокси
- 🔲 Автоматический reconnect при потере связи с API
- 🔲 Graceful shutdown с завершением активных сессий
- 🔲 Установка агента как systemd-сервис (Linux) / Windows Service
- 🔲 Автообновление агента (self-update)
- 🔲 **Тесты:** регистрация, heartbeat, reconnect, graceful shutdown

### 2.2 Безопасность агента

- ✅ JoinToken-аутентификация
- 🔲 Ротация agent-token по расписанию
- 🔲 mTLS между API и агентом
- 🔲 Проверка ticket при создании сессии на агенте
- 🔲 Ограничение сетевых интерфейсов (bind только на localhost или определённый IP)
- 🔲 **Тесты:** невалидный joinToken, просроченный ticket, mTLS handshake

### 2.3 Проксирование сессий

- ✅ WebSocket-прокси Client ↔ API ↔ Agent ↔ Target
- 🔲 Поддержка SSH-канала (PTY resize, stdin/stdout/stderr мультиплексирование)
- 🔲 Поддержка RDP-канала (bitmap/h264 фреймы, clipboard, audio)
- 🔲 Keepalive и таймауты неактивных сессий
- 🔲 Ограничение максимального числа параллельных сессий на агенте
- 🔲 **Тесты:** end-to-end прокси SSH/RDP, таймауты, лимиты

### 2.4 Мониторинг агентов

- 🔲 Автоматическое определение Offline по пропущенным heartbeat
- 🔲 Алерт при переходе агента в Offline
- 🔲 Dashboard агентов в UI (статус, число сессий, uptime)
- 🔲 **Тесты:** переход Online→Offline при пропуске heartbeat

---

## Этап 3 — Запись сессий (этап C по бэклогу)

> Цель: полноценная запись и воспроизведение.

### 3.1 Механизм записи

- ✅ Модель `SessionRecording` в БД
- ✅ Режимы: node, node-sync, proxy, proxy-sync
- ✅ Хранилища: Local FS, S3/MinIO
- ✅ Опциональное AES-шифрование
- 🔲 Реализовать захват потока данных на уровне агента (node/node-sync)
- 🔲 Реализовать захват потока данных на уровне прокси (proxy/proxy-sync)
- 🔲 Чанковая загрузка записей (`PUT /recordings/{id}/chunks`)
- 🔲 Retry-механизм для async-режимов при недоступности хранилища
- 🔲 Контроль дискового пространства на агенте (алерт при заполнении)
- 🔲 **Тесты:** запись в каждом режиме, retry при ошибке, шифрование/дешифровка

### 3.2 Хранение записей

- ✅ `LocalRecordingStorage`, `S3RecordingStorage`
- 🔲 Lifecycle-политики S3 (архивация в Glacier / удаление по retention)
- 🔲 Проверка целостности записей (hash verification при скачивании)
- 🔲 Сжатие записей (gzip/zstd)
- 🔲 **Тесты:** upload/download, hash verification, lifecycle

### 3.3 Воспроизведение

- 🔲 API для потокового воспроизведения записи
- 🔲 Веб-плеер в UI для SSH-записей (терминальный replay)
- 🔲 Веб-плеер в UI для RDP-записей (видеопоток)
- 🔲 Поиск по тексту внутри SSH-записей
- 🔲 Таймлайн с навигацией по времени
- 🔲 **Тесты:** воспроизведение записей разных форматов, поиск по тексту

---

## Этап 4 — JIT-доступы и ITSM (этап D по бэклогу)

> Цель: полноценный цикл заявок и синхронизация с Jira.

### 4.1 JIT Access Requests

- ✅ CRUD заявок (`AccessRequestsController`)
- ✅ Approve / Deny
- ✅ TTL (ExpiresAt)
- ✅ Валидация: запретить заявку, если нет подходящей политики для роли пользователя
- ✅ Лимит активных заявок на пользователя (настраиваемый через `Jit:MaxActiveRequestsPerUser`)
- 🔲 Уведомления о статусах заявки (email / webhook)
- 🔲 Эскалация при отсутствии ответа от approver (таймаут SLA)
- 🔲 **Тесты:** полный цикл заявки, SLA-эскалация, лимиты

### 4.2 ITSM-интеграция (Jira)

- ✅ `JiraItsmClient` — создание заявок
- ✅ `JiraWebhooksController` — обработка webhook
- ✅ Маппинг статусов Jira → внутренние
- 🔲 Обработка всех transition-статусов Jira (Reopened, Cancelled, и т.д.)
- 🔲 Двунаправленная синхронизация комментариев
- 🔲 Конфигурируемый маппинг статусов (через appsettings)
- 🔲 Обработка ошибок и retry при недоступности Jira
- 🔲 **Тесты:** webhook с разными статусами, retry, невалидный payload

### 4.3 Worker (фоновые задачи)

- ✅ `AccessRequestWorker` — TTL enforcement, синхронизация Jira
- ✅ Отзыв активных сессий при истечении заявки
- ✅ Очистка просроченных agent-ticket
- ✅ Периодическая проверка консистентности (заявка Approved, но сессия Terminated)
- 🔲 Dead letter queue для неотправленных ITSM-обновлений
- ✅ **Тесты:** истечение TTL, отзыв сессий, consistency check

### 4.4 UI для управления доступами

- ✅ Razor Pages: Targets, AccessRequests, Approvals, Sessions, Recordings, Roles, Policies
- 🔲 Форма создания заявки с выбором target и обоснованием
- 🔲 Панель approver'a со списком ожидающих заявок
- 🔲 Статус-бар заявки (таймлайн жизненного цикла)
- 🔲 Фильтрация и поиск во всех таблицах
- 🔲 Адаптивная верстка (мобильные устройства)
- 🔲 **Тесты:** рендеринг страниц, формы, фильтрация

---

## Этап 5 — Identity и Prod Hardening (этап E по бэклогу)

> Цель: промышленная безопасность и отказоустойчивость.

### 5.1 Keycloak / IdP

- ✅ OIDC-интеграция, маппинг ролей
- 🔲 Федерация с AD/LDAP (конфигурация Keycloak realm)
- 🔲 MFA (OTP/WebAuthn) обязательная для всех пользователей
- 🔲 Step-up аутентификация для критичных операций (approve, terminate)
- 🔲 Ограничение по IP/сегменту на уровне Keycloak
- 🔲 Настройка сроков жизни токенов (Access: 5–15 мин, Refresh: 8–12 ч)
- 🔲 **Тесты:** login flow, MFA mock, token expiration, role mapping

### 5.2 RBAC allow/deny по label-expression

- ✅ `LabelExpressionEvaluator` — парсер выражений
- 🔲 Поддержка вложенных скобок в выражениях
- 🔲 Политики Deny с приоритетом над Allow
- 🔲 Наследование политик (иерархия ролей)
- 🔲 Аудит решений авторизации (почему разрешено/запрещено)
- 🔲 **Тесты:** сложные выражения, deny overrides, аудит решений

### 5.3 Высокая доступность (HA)

- 🔲 API: горизонтальное масштабирование (HPA в K8s)
- 🔲 PostgreSQL: primary-replica с автопереключением (Patroni / CloudNativePG)
- 🔲 Keycloak: кластерный режим (Infinispan)
- 🔲 S3/MinIO: репликация бакетов
- 🔲 Session Broker: sticky sessions по sessionId
- 🔲 Health checks: readiness + liveness probes для всех компонентов
- 🔲 **Тесты:** failover DB, failover API-инстанса, восстановление после рестарта

### 5.4 Безопасность

- 🔲 mTLS между всеми внутренними компонентами
- 🔲 Network Policies в K8s (ограничение трафика между подами)
- 🔲 Secrets management (Vault / K8s Secrets + sealed-secrets)
- 🔲 Ротация паролей сервисных учётных записей
- 🔲 Сканирование Docker-образов на уязвимости (Trivy)
- 🔲 OWASP-проверки на API (injection, CSRF, XSS в UI)
- 🔲 **Тесты:** security-сканирование в CI, penetration test checklist

### 5.5 Observability

- ✅ OpenTelemetry (трейсы + метрики → OTLP)
- ✅ Prometheus + Grafana (Minikube)
- 🔲 Кастомные метрики: активные сессии, время обработки заявки, ошибки интеграций
- 🔲 Dashboards Grafana: обзор системы, агенты, сессии, ITSM
- 🔲 Алерты: агент offline, ошибка записи, SLA-нарушение, высокая латенция
- 🔲 Structured logging (Serilog → JSON → ELK/Loki)
- 🔲 Distributed tracing: корреляция запросов API → Worker → Agent
- 🔲 **Тесты:** метрики экспортируются, health endpoint отвечает, trace propagation

---

## Этап 6 — Расширенный функционал

> Цель: возможности для полноценного корпоративного PAM.

### 6.1 PAM Vault (управление секретами)

- 🔲 Безопасное хранилище паролей сервисных учёток (Vault / встроенное)
- 🔲 Ротация паролей по расписанию или при каждом использовании (checkout/checkin)
- 🔲 Инъекция credentials при создании сессии (пользователь не видит пароль)
- 🔲 Break-glass: отдельные аварийные учётки с обязательной записью
- 🔲 **Тесты:** ротация, checkout/checkin, break-glass flow

### 6.2 SIEM-интеграция

- 🔲 Экспорт событий в формате CEF/JSON через syslog
- 🔲 Полный набор типов событий (user.login, access.requested, access.approved, session.started, session.ended, access.denied, policy.violation)
- 🔲 Heartbeat-событие для мониторинга доступности PAM со стороны SIEM
- 🔲 Обогащение событий: IP, geolocation, user-agent
- 🔲 **Тесты:** формат событий, доставка, обогащение полей

### 6.3 Расширенные интеграции

- 🔲 AD/LDAP: прямая интеграция для управления группами (без Keycloak)
- 🔲 PKI: выдача клиентских сертификатов для mTLS-сессий
- 🔲 Поддержка нескольких ITSM-провайдеров (ServiceNow, Naumen)
- 🔲 Поддержка нескольких CMDB-провайдеров
- 🔲 **Тесты:** интеграционные тесты с мок-сервисами для каждого провайдера

### 6.4 Расширенные протоколы

- 🔲 Поддержка HTTPS-прокси (веб-приложения)
- 🔲 Поддержка SQL-прокси (PostgreSQL, MySQL, MS SQL)
- 🔲 Поддержка VNC
- 🔲 Поддержка Kubernetes exec/port-forward
- 🔲 **Тесты:** e2e для каждого протокола

### 6.5 Расширенный UI

- 🔲 Дашборд: сводная панель (активные сессии, заявки, агенты, метрики)
- 🔲 Управление пользователями и группами (из Keycloak)
- 🔲 Визуальный редактор политик с preview результата
- 🔲 Карта инфраструктуры (граф таргетов и агентов)
- 🔲 Тёмная тема
- 🔲 Локализация (русский / английский)
- 🔲 **Тесты:** e2e-тесты UI (Playwright)

---

## Этап 7 — Промышленное внедрение

> Цель: подключение реальных систем и переход в эксплуатацию.

### 7.1 Волна 1 — критичные системы (Tier 1)

- 🔲 Подключить AD-системы (SCH-229958, SCH-258508, SCH-258551, SCH-258509, SCH-258505)
- 🔲 Подключить терминальные фермы (SCH-249443, SCH-258513, SCH-258552, SCH-258514, SCH-258504)
- 🔲 Подключить все системы с критичностью `critical` из реестра
- 🔲 Закрыть прямой доступ к системам (только через PAM)
- 🔲 Провести UAT с администраторами

### 7.2 Волна 2 — высокий приоритет (Tier 2)

- 🔲 Подключить системы критичности `non-critical` (класс B)
- 🔲 Миграция сервисных учёток в Vault
- 🔲 Настройка JIT-политик для каждой группы систем

### 7.3 Волна 3 — остальные системы (Tier 3)

- 🔲 Подключить оставшиеся системы
- 🔲 Полная миграция привилегированных учёток
- 🔲 Финальный аудит и закрытие прямых доступов

### 7.4 Операционная готовность

- 🔲 Runbooks для всех типовых инцидентов утверждены
- 🔲 Мониторинг и алерты настроены для всех компонентов
- 🔲 Регламент еженедельной/ежемесячной/квартальной проверки
- 🔲 Документация для администраторов и пользователей
- 🔲 Тренинг для команды эксплуатации

---

## Этап 8 — Нагрузочное тестирование и соответствие

> Цель: подтверждение SLA и регуляторных требований.

### 8.1 Нагрузочное тестирование

- 🔲 Подготовить сценарии (k6 / NBomber / JMeter)
- 🔲 Тест: 100 одновременных сессий
- 🔲 Тест: 500 одновременных сессий
- 🔲 Тест: 1000 одновременных сессий
- 🔲 Тест: пиковые JIT-запросы (burst)
- 🔲 Тест: устойчивость Session Broker под нагрузкой
- 🔲 Профилирование и устранение bottleneck'ов
- 🔲 Зафиксировать baseline производительности

### 8.2 Соответствие требованиям (Compliance)

- 🔲 152-ФЗ: контроль доступа, журналирование, защита ПДн
- 🔲 PCI DSS (если применимо): учёт привилегированных действий
- 🔲 ISO 27001: процедуры управления доступом
- 🔲 Аудит неизменяемости логов
- 🔲 Отчёт о результатах security review
- 🔲 Пенетрационное тестирование (внешний аудит)

### 8.3 DR и BCP

- 🔲 Тест RTO/RPO для каждого класса критичности (A: 1ч/15мин, B: 4ч/4ч, C: 24ч/24ч)
- 🔲 Тест восстановления БД из бэкапа
- 🔲 Тест переключения на резервный кластер
- 🔲 Тест восстановления записей сессий из S3-реплики
- 🔲 Документирование плана DR

---

## Этап 9 — CI/CD и DevOps

> Цель: автоматизация сборки, тестирования и доставки.

### 9.1 CI Pipeline

- 🔲 `dotnet build` всех проектов
- 🔲 `dotnet test` — unit + integration тесты
- 🔲 Линтинг / code style (dotnet format, editorconfig)
- 🔲 Генерация отчёта покрытия (coverlet → PR comment)
- 🔲 Минимальный порог покрытия (например, 70%)
- 🔲 SAST-сканирование (Security Code Scan / Semgrep)
- 🔲 Сканирование Docker-образов (Trivy)
- 🔲 Сборка и публикация Docker-образов в registry

### 9.2 CD Pipeline

- 🔲 Автодеплой в dev/staging (Helm upgrade)
- 🔲 Smoke-тесты после деплоя (health check + базовый сценарий)
- 🔲 Ручной approve для продакшен-деплоя
- 🔲 Blue/green или canary deployment стратегия
- 🔲 Rollback при неудачном деплое

### 9.3 Инфраструктура как код

- ✅ Helm chart с шаблонами для всех компонентов
- ✅ Kubernetes-манифесты (`infra/k8s/`)
- 🔲 Версионирование Helm chart (semantic versioning)
- 🔲 Terraform/Pulumi для инфраструктуры (если облако)
- 🔲 GitOps (ArgoCD / FluxCD) для управления деплоями

---

## Сводка по тестовому покрытию

| Слой | Тип тестов | Этап |
|------|-----------|------|
| `PamGateway.Core` (модели, evaluators) | Unit | 0 |
| InMemory-хранилища | Unit | 0 |
| EF-хранилища (Postgres) | Integration | 0 |
| API-контроллеры | Integration | 0 |
| Jira/CMDB клиенты | Unit (мок HTTP) | 0 |
| Worker | Unit + Integration | 0 |
| Agent | Unit + Integration | 0, 2 |
| WebSocket-прокси | Integration | 0, 2 |
| UI (ApiClient + Pages) | Integration | 0, 4 |
| Авторизация (JWT/Keycloak) | Integration | 1, 5 |
| Запись сессий | Integration + e2e | 3 |
| JIT полный цикл | e2e | 4 |
| Нагрузочные | Performance | 8 |
| Безопасность | Security scan | 5, 8 |
| UI e2e (Playwright) | e2e | 6 |
