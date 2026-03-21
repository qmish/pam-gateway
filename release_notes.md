# v0.8.0 — Rate Limiting, SIEM, кэширование политик, тесты Agent/UI

## Новый функционал

### CI/CD (Этап 0.1)
- Генерация отчётов покрытия кода (coverlet → Cobertura → HTML)
- Публикация артефактов покрытия в GitHub Actions
- Вывод сводки покрытия в логах CI

### Rate Limiting (Этап 1.1)
- ASP.NET Core `FixedWindowLimiter` для auth и api эндпоинтов
- Настраиваемые лимиты через `RateLimiting:Auth` и `RateLimiting:Api`
- HTTP 429 при превышении лимита

### Кэширование политик (Этап 1.2)
- `IMemoryCache` для результатов оценки политик (TTL 5 минут)
- Метод `InvalidateCache()` для принудительного обновления
- Снижение нагрузки на store при частых запросах

### SIEM Export (Этап 1.4)
- Фоновый сервис `SiemExportService` для экспорта аудит-событий
- Поддержка транспорта: syslog (UDP) и HTTP webhook
- Формат событий: CEF (Common Event Format)
- Настройка через `SiemExport:Enabled`, `Transport`, `WebhookUrl`, `SyslogHost`

### Иммутабельность аудита (Этап 1.4)
- `AuditImmutabilityMiddleware` запрещает PUT/PATCH/DELETE на `/api/v1/audit`
- HTTP 405 Method Not Allowed с RFC 7807 Problem Details

## Тесты

### Agent тесты (Этап 0.8)
- Регистрация с корректным payload
- Heartbeat после регистрации
- Bearer token в heartbeat
- Retry при ошибке регистрации

### UI ApiClient тесты (Этап 0.10)
- Тесты всех HTTP-методов ApiClient через мок HttpHandler
- Проверка корректных endpoint'ов и payload'ов
- Обработка серверных ошибок

### Дополнительные тесты
- Policy caching: кэширование, invalidation, deny overrides allow
- SIEM CEF: формат, severity для deny/success, session/request IDs
- Rate limiting: нормальная нагрузка
- Audit immutability: GET разрешён, DELETE/PUT/PATCH заблокированы

## Статистика
- **244 теста** (170 unit + 74 integration) — все проходят
- **18 изменённых файлов**, 1041 добавленных строк
