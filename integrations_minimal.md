# Минимальные интеграции (SIEM/ITSM/CMDB)

## SIEM
### События (MVP)
- user.login
- access.requested
- access.approved
- session.started
- session.ended
- access.denied
- policy.violation

### Формат
- JSON/syslog с обязательными полями: time, user, role, target, action, result.

## ITSM (Jira)
### Минимальный workflow
1) Запрос доступа (JIT)
2) Согласование владельцем системы
3) Выдача доступа на срок
4) Завершение и журналирование

### Интеграция
- REST API Jira для создания/обновления заявок и получения статуса.

### Маппинг статусов (пример)
- pending → Jira To Do
- approved → Jira In Progress
- expired → Jira Done

## CMDB (Jira Insight)
### Цели
- Импорт перечня систем и их критичности.
- Синхронизация атрибутов: owner, env, type.

### Интеграция
- Insight REST API для синхронизации атрибутов.
- IQL фильтр задается в `Cmdb:Iql` (по умолчанию `objectType=System`).

## Выходные артефакты
- Спецификация событий SIEM.
- Схема интеграции ITSM.
- Формат импорта CMDB.
