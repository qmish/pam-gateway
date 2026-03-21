# Релиз v0.14.0 — PAM Vault, расширенный SIEM, CI pipeline

## Что нового

### PAM Vault — управление привилегированными учётками (6.1)
- **Хранилище credentials** — API для создания, получения, управления учётными записями целевых систем
- **Checkout/Checkin** — безопасное получение пароля с отслеживанием кто и когда использовал
- **Ротация паролей** — ручная ротация через API, генерация криптостойких паролей (24 символа)
- **Break-glass** — аварийные учётки с обязательной аудит-записью `vault.breakglass.checkout`
- **Полная история** — все checkout/checkin операции сохраняются с причиной и временем
- **Защита** — нельзя checkout уже занятый credential, нельзя ротировать пока checked out

### Расширенная SIEM-интеграция (6.2)
- **16 типов событий** — полный набор: user.login/logout, access.requested/approved/denied/expired, session.started/ended, vault.checkout/checkin/rotated, policy.violation, agent.online/offline, breakglass.checkout, system.heartbeat
- **Heartbeat** — периодическое событие для мониторинга доступности PAM со стороны SIEM (настраиваемый интервал)
- **Обогащение событий** — User-Agent добавлен в AuditEvent и CEF-сообщения (`requestClientApplication`)
- **CEF escaping** — корректное экранирование спецсимволов (`=`, `|`, `\`) в CEF-формате

### CI Pipeline (9.1)
- **dotnet format** — проверка стиля кода в CI
- **Минимальный порог покрытия** — 70% с предупреждением при нарушении
- **Trivy** — автоматическое сканирование Docker-образов на уязвимости (CRITICAL, HIGH)
- **Полный pipeline**: build → lint → test → coverage → Docker build → Trivy scan

### Helm Chart (9.3)
- **Semantic versioning** — Chart.yaml обновлён до v0.14.0 с метаданными

## Статистика тестов
- **501 тест** (318 unit + 183 integration) — все проходят
- Новые тесты: Vault stores (5), SIEM events (7), Vault API integration (9)
