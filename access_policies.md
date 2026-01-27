# Политики доступа и управления учетными записями

## Общие принципы
- Доступ только через PAM и шлюз, прямые подключения запрещены
- Модель минимальных привилегий (Least Privilege)
- Все привилегированные сессии записываются
- Обязательная MFA для удаленного доступа

## Политики доступа (RBAC)
- Роли определяются по функциям (админ ОС, админ БД, админ сети)
- Доступы выдаются по заявке с согласованием владельца системы
- Регулярный пересмотр доступов не реже 1 раза в квартал

### Типовая матрица ролей (заполняется по реестру систем)
| Роль | AD | Серверы | БД | 1C | Web-приложения | Сеть/Инфра | RDP фермы | Мониторинг |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| PAM_Administrator | Admin | Admin | Admin | Admin | Admin | Admin | Admin | View |
| Security_Auditor | View | View | View | View | View | View | View | View |
| System_Admin_Windows | Operate | Admin | None | None | None | None | Admin | View |
| System_Admin_Linux | Operate | Admin | None | None | None | None | Admin | View |
| DB_Admin | None | None | Admin | None | None | None | None | View |
| Network_Admin | None | None | None | None | None | Admin | None | View |
| OneC_Admin | None | None | None | Admin | None | None | None | View |
| App_Support | None | None | None | Operate | Operate | None | None | View |
| DevOps | Operate | Admin | Operate | None | Admin | Operate | Admin | View |
| ServiceDesk | None | None | None | None | None | None | Operate | View |

Легенда: Admin — полный доступ, Operate — ограниченный по JEA, View — аудит/просмотр, None — нет доступа.

## Политики JIT/JEA
- Доступ выдаётся на ограниченный срок (обычно 2–8 часов)
- Временные доступы автоматически отзываются
- JEA применяется для критичных операций с ограничением команд

## Политики паролей и ротации
- Ротация при каждом использовании или по расписанию (24–72 часа)
- Длина и сложность паролей соответствуют требованиям ИБ
- Хранение только в PAM Vault

## Break‑glass
- Отдельные учетные записи для аварийного доступа
- Обязательная запись и последующий аудит
- Регулярная проверка работоспособности

## Политики логирования
- Логи доступа и действий направляются в SIEM
- Хранение логов в неизменяемом хранилище
- Настроены алерты по ключевым событиям (эскалации, неудачные попытки)
