# Agent‑протокол (API + payloads)

## Транспорт
- Вариант v1: HTTPS + WebSocket (mTLS опционально).
- Аутентификация агента: join‑token → выдача agent‑cert.
- Все вызовы подписаны `Agent-Id` + `Authorization: Bearer <agent_token>`.

## 1) Регистрация агента
**POST** `/api/v1/agents/register`
```json
{
  "joinToken": "JOIN-XXXX",
  "agentId": "agent-001",
  "hostname": "srv-01",
  "os": "linux",
  "labels": { "env": "prod", "role": "db" },
  "capabilities": ["ssh", "rdp"]
}
```
**Response 200**
```json
{
  "agentToken": "eyJhbGci...",
  "agentCert": "-----BEGIN CERTIFICATE-----...",
  "heartbeatIntervalSec": 30
}
```

## 2) Heartbeat
**POST** `/api/v1/agents/heartbeat`
```json
{
  "agentId": "agent-001",
  "status": "ok",
  "activeSessions": 2,
  "labels": { "env": "prod", "role": "db" }
}
```
**Response 200** `{ "ok": true }`

## 3) Создание сессии (Proxy → Agent)
**POST** `/api/v1/agents/{agentId}/sessions`
```json
{
  "sessionId": "SES-123",
  "targetId": "SCH-229958",
  "protocol": "ssh",
  "user": "alice",
  "ticket": "TICKET-XYZ",
  "expiresAt": "2026-01-27T12:00:00Z"
}
```
**Response 201**
```json
{
  "sessionId": "SES-123",
  "status": "active",
  "proxyTunnelUrl": "wss://proxy.example.local/ws/sessions/SES-123"
}
```

## 4) Поток данных (Proxy ↔ Agent)
**WebSocket** `/ws/sessions/{sessionId}`
- Мультиплекс каналов: `control`, `stdin`, `stdout`, `recording`.

**Control message**
```json
{ "type": "resize", "cols": 120, "rows": 40 }
```

### Channel frames (SSH)
**stdin**
```json
{ "channel": "stdin", "seq": 15, "data": "<base64>" }
```
**stdout**
```json
{ "channel": "stdout", "seq": 16, "data": "<base64>" }
```
**stderr**
```json
{ "channel": "stderr", "seq": 17, "data": "<base64>" }
```
**control (SSH keepalive)**
```json
{ "channel": "control", "type": "keepalive", "ts": "2026-01-27T11:00:00Z" }
```

### Channel frames (RDP)
**rdp.input**
```json
{
  "channel": "rdp.input",
  "seq": 21,
  "events": [
    { "type": "mouse", "x": 120, "y": 220, "button": "left", "action": "down" },
    { "type": "mouse", "x": 120, "y": 220, "button": "left", "action": "up" }
  ]
}
```
**rdp.frame**
```json
{
  "channel": "rdp.frame",
  "seq": 22,
  "codec": "h264",
  "keyframe": false,
  "data": "<base64>"
}
```
**rdp.bitmap**
```json
{
  "channel": "rdp.bitmap",
  "seq": 23,
  "encoding": "rle",
  "width": 1280,
  "height": 720,
  "rects": [
    { "x": 0, "y": 0, "w": 640, "h": 360, "data": "<base64>" }
  ]
}
```
**rdp.clipboard**
```json
{ "channel": "rdp.clipboard", "seq": 24, "contentType": "text/plain", "data": "ZGF0YQ==" }
```
**rdp.audio**
```json
{
  "channel": "rdp.audio",
  "seq": 25,
  "codec": "pcm_s16le",
  "sampleRate": 48000,
  "channels": 2,
  "data": "<base64>"
}
```

## 5) Завершение сессии
**POST** `/api/v1/agents/{agentId}/sessions/{sessionId}/terminate`
```json
{ "reason": "request_expired" }
```
**Response 200**
```json
{ "status": "terminated", "endedAt": "2026-01-27T11:30:00Z" }
```

## 6) Запись сессий (upload)
**PUT** `/api/v1/recordings/{sessionId}/chunks`
```json
{
  "seq": 10,
  "timestamp": "2026-01-27T11:00:00Z",
  "payload": "<base64>"
}
```
**Response 200** `{ "ok": true }`

## 7) Метаданные сессии
**POST** `/api/v1/audit/events`
```json
{
  "eventType": "session.started",
  "sessionId": "SES-123",
  "targetId": "SCH-229958",
  "user": "alice",
  "result": "success"
}
```

## Ошибки
- `401` — агент не авторизован.
- `403` — роль/политика запрещает.
- `409` — конфликт (сессия уже активна).
