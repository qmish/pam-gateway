# MVP: ключевые системы (critical + AD + терминальные фермы)

## Критерии включения в MVP
- Все системы с критичностью `critical` (класс A).
- Все доменные службы AD/LDAP.
- Все терминальные фермы.

## Источники
- Реестр систем: `systems_registry_abc.md`
- Исходная выгрузка: `0000/data.md`

## Доменные службы AD/LDAP (из реестра)
- SCH-229958 — Active Directory
- SCH-258508 — Active Directory CASHTOYOU
- SCH-258551 — Active Directory CREDIT2DAY
- SCH-258509 — Active Directory ONECLICKMONEY
- SCH-258505 — Active Directory SKORFIN

## Терминальные фермы (из реестра)
- SCH-249443 — Терминальная ферма
- SCH-258513 — Терминальная ферма CASHTOYOU
- SCH-258552 — Терминальная ферма CREDIT2DAY
- SCH-258514 — Терминальная ферма ONECLICKMONEY
- SCH-258504 — Терминальная ферма SKORFIN

## Примечания
- Перечень critical систем берется по фильтру `Критичность = critical` из `systems_registry_abc.md`.
- Включение дополнительных систем в MVP оформляется отдельным списком и утверждением ИБ.
