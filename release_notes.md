# Релиз v0.13.0 — UI-тесты, Dashboard, безопасность K8s, тёмная тема

## Что нового

### Тестовое покрытие
- **Интеграционные тесты Razor Pages UI** — все 12 страниц проверяются на корректный рендеринг (200 OK), наличие данных, форм и фильтров
- **PamUiFactory** — тестовая фабрика для UI с мок-HttpHandler, имитирующая ответы API
- **Тесты health/readiness/liveness** — полная проверка всех probe-эндпоинтов и валидация store checks
- **Тесты hash verification через API** — upload, chunked upload, finalize, download с верификацией хеша
- **Тесты тёмной темы и локализации** — проверка наличия toggle-кнопок и скриптов в layout

### Dashboard UI (6.5)
- **Сводная панель**: целевые системы, агенты online/offline, активные сессии, ожидающие заявки, одобренные заявки, записи, политики
- **Последние заявки и сессии** — таблицы с 5 последними событиями
- **Статус агентов** — таблица с hostname, OS, статус, последняя активность

### Тёмная тема и локализация (6.5)
- **Переключатель тёмной темы** с сохранением в localStorage (Bootstrap `data-bs-theme`)
- **Локализация RU/EN** — переключение языка интерфейса на лету через `data-i18n` атрибуты
- Обновлённая навигация с русскоязычными подписями

### Мониторинг агентов (2.4)
- **Agent Dashboard** — карточки с метриками (всего, online, offline, сессии), фильтрация, uptime, capabilities

### Запись сессий (3.2)
- **Hash verification** — проверка целостности при скачивании записей (`?verify=true`)
- **GZip-сжатие** — опциональное сжатие записей (`EnableCompression`)
- Тесты round-trip для всех режимов (plain, gzip, encryption+gzip, chunked)

### Инфраструктура K8s
- **HPA** (5.3) — горизонтальное автомасштабирование для API (2-8 реплик) и Worker (1-3 реплики) по CPU/memory
- **Readiness/Liveness probes** (5.3) — `/api/v1/health/ready` и `/api/v1/health/live` с проверкой store
- **Network Policies** (5.4) — default-deny + точечные правила: API↔Postgres, API↔Keycloak, Worker↔Postgres, Worker↔Jira, UI↔API, DNS
- **Resource limits** — CPU/memory requests и limits для API deployment

## Статистика тестов
- **474 теста** (302 unit + 172 integration) — все проходят
- Покрыты: UI-рендеринг, health probes, hash verification, chunked uploads, тема/локализация
