# Backlog по этапам A–E (Teleport‑подобная реализация)

## Подготовка
- [P1] Создать репозиторий на GitHub (предлагаемое имя: `pam-gateway`).

## Этап A — Control Plane v1 (MVP, уже в разработке)
**Цель:** базовый Auth/API, заявки, аудит, CMDB‑заглушка.
- [A1] Авторизация/Keycloak переключаемая (`Auth.Enabled`).
- [A2] CMDB провайдер `Stub/Insight`.
- [A3] Базовые сущности: Requests, Approvals, Roles, Policies, Targets, Sessions.
- [A4] Аудит событий API (минимум: request/session/audit).
- [A5] Helm/Minikube: deploy, values, ingress.

**Acceptance:** API работает, `cmdb/sync` импортирует Targets из stub, основные CRUD доступны.

## Этап B — Agents v1 (Teleport Agents)
**Цель:** агент на стороне цели и протокол управления сессией.
- [B1] Агент‑демон (Linux/Windows).
- [B2] Регистрация/heartbeat агента в Auth.
- [B3] Session ticket: выдача и проверка.
- [B4] SSH/RDP прокси через агент (минимум: TCP‑туннель).
- [B5] Метки ресурсов (labels) для RBAC.

**Acceptance:** сессия создается через агент, трафик идет через Proxy → Agent → Target.

## Этап C — Session Recording (node/proxy, sync/async)
**Цель:** запись сессий и хранение по режимам Teleport.
- [C1] Модель записи (метаданные в Postgres).
- [C2] Режимы записи: `node`, `node-sync`, `proxy`, `proxy-sync`.
- [C3] Хранилища: local / S3 / MinIO.
- [C4] Шифрование записей (опционально).
- [C5] Реплей сессий (минимум: API загрузки/просмотра).

**Acceptance:** запись и воспроизведение доступны для SSH/RDP, режимы переключаются конфигом.

## Этап D — Access Requests + ITSM
**Цель:** JIT доступы через approvals, синхронизация с Jira.
- [D1] ITSM адаптеры (create/update/transition).
- [D2] Политики request/approve по ролям.
- [D3] SLA/TTL enforcement (Worker).
- [D4] Маппинг статусов Jira → внутренние статусы.

**Acceptance:** заявка → approval → выдача доступа с TTL.

## Этап E — Identity & Prod Hardening
**Цель:** включить полноценный IdP, MFA, security hardening.
- [E1] Keycloak OIDC: роли/claims mapping.
- [E2] Session MFA / step‑up (опционально).
- [E3] RBAC allow/deny по label‑expression.
- [E4] HA: Auth/Proxy/DB/Storage.
- [E5] Observability: metrics, traces, dashboards.

**Acceptance:** prod‑готовая схема, доступы через Keycloak и политики Teleport‑подобного уровня.
