# Форматы логов и событий

## Каналы доставки
- SIEM: syslog или HTTPS JSON.
- Локально: файлы аудита + ротация.

## События (MVP)
Типы:
- user.login
- access.requested
- access.approved
- access.expired
- session.started
- session.ended
- access.denied
- policy.violation

## Обязательные поля
- `timestamp` (RFC3339)
- `eventType`
- `userId`
- `username`
- `role`
- `targetId`
- `targetName`
- `action`
- `result`
- `requestId`
- `sessionId`
- `sourceIp`

## Пример события
```json
{
  "timestamp": "2026-01-26T10:12:30Z",
  "eventType": "session.started",
  "userId": "u-123",
  "username": "ivan.petrov",
  "role": "System_Admin_Windows",
  "targetId": "SCH-249443",
  "targetName": "Терминальная ферма",
  "action": "connect",
  "result": "success",
  "requestId": "REQ-001",
  "sessionId": "SES-001",
  "sourceIp": "10.10.10.5"
}
```

## Соответствие требованиям
- Хранение и неизменяемость логов по политике ИБ.
- Корреляция событий в SIEM по `requestId`/`sessionId`.
