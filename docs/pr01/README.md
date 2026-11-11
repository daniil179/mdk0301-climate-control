# ПР1 — Анализ применимости стандартов

Вариант 3 — Climate Control АИС «Умный кампус».

Цель: определить нормативную базу и связать требования с подсистемами, исходниками и проверками.

## Трассировка требований

| Требование | Исходный файл | Проверка |
|---|---|---|
| R01. Подлинность и целостность телеметрии | [HmacProtocol.cs](../../projects/ClimateControl/src/Climate.Core/HmacProtocol.cs), [HmacHandler.cs](../../projects/ClimateControl/src/Climate.Api/Security/HmacHandler.cs) | Подмена тела и подписи отклоняется |
| R02. Запрет повторного запроса | [ReplayGuard.cs](../../projects/ClimateControl/src/Climate.Api/Security/ReplayGuard.cs) | Повтор nonce получает отказ |
| R03. Допустимые диапазоны | [TelemetryValidator.cs](../../projects/ClimateControl/src/Climate.Core/TelemetryValidator.cs) | Граничные и ошибочные значения |
| R04. Разграничение доступа | [Program.cs](../../projects/ClimateControl/src/Climate.Api/Program.cs) | Отсутствие telemetry:write не даёт записать показание |
| Совместимость v1/v2 | [ReadingsController.cs](../../projects/ClimateControl/src/Climate.Api/Controllers/ReadingsController.cs) | [VersioningTests.cs](../../projects/ClimateControl/tests/Climate.IntegrationTests/VersioningTests.cs), V01/V02 |

## Документы

- [Требования R01–R08](../../projects/ClimateControl/docs/requirements.md)
- [Техническое задание](../../projects/ClimateControl/docs/technical-specification.md)
- [Исходники](../../projects/ClimateControl/src)
- [Фактический журнал сборки](../../evidence/pr01/build.log)
- [Модульные проверки](../../evidence/pr01/unit-tests.log)
- [HTTP-проверки](../../evidence/pr01/integration-tests.log)
- [Отчёт Word](../../reports/Word/Бойцов_ПР1_МДК0301.docx)
- [Отчёт PDF](../../reports/PDF/Бойцов_ПР1_МДК0301.pdf)

## Нормативный профиль

ГОСТ Р 59793-2021 — стадии создания; ISO/IEC/IEEE 12207:2026 — процессы жизненного цикла; ГОСТ 19.201-78 — ТЗ программного модуля; ГОСТ Р 56939-2024 — безопасная разработка; ISO/IEC 25010:2023 — модель качества; ГОСТ 19.301-79 и ISO/IEC/IEEE 29119-3:2021 — методика и документы испытаний; ГОСТ 34.201-2020 — комплект документации; ГОСТ Р 59792-2021 — виды испытаний АС.

В задании приведён ГОСТ 34.601-89. Уточнено обозначение ГОСТ 34.601-90 и учтена его замена в РФ на ГОСТ Р 59793-2021. Международная редакция 12207:2017 заменена редакцией 2026. Применимость стандартов закрепляется в ТЗ; учебная работа не является сертификацией.

## Скриншоты

Скриншоты в отчёте показывают настоящие файлы репозитория в переносном Visual Studio Code. Журналы получены при локальной сборке и запуске тестов .NET 8; редактор использован для просмотра файлов.
