# МДК 03.01 — Climate Control

**Бойцов Даниил Антонович · И13-24-11 · вариант 3**

Учебный модуль АИС «Умный кампус»: приём температуры и влажности от IoT-датчиков, проверка HMAC-SHA256, хранение SQLite и версии API v1/v2.

## Практические работы

| Работа | Материалы | Отчёт |
|---|---|---|
| ПР1. Анализ применимости стандартов | [Требования, код и проверка](docs/pr01/README.md) | [Word](reports/Word/Бойцов_ПР1_МДК0301.docx) · [PDF](reports/PDF/Бойцов_ПР1_МДК0301.pdf) |

ПР2–ПР8 будут добавлены после проверки оформления первой работы.

## Исходный проект

- [Решение .NET 8](projects/ClimateControl/ClimateControl.sln)
- [API](projects/ClimateControl/src/Climate.Api) · [модель и HMAC](projects/ClimateControl/src/Climate.Core)
- [SQLite](projects/ClimateControl/src/Climate.Infrastructure) · [эксплуатационные модули](projects/ClimateControl/src/Climate.Operations)
- [Модульные тесты](projects/ClimateControl/tests/Climate.UnitTests) · [HTTP-тесты](projects/ClimateControl/tests/Climate.IntegrationTests)
- [Инструкция запуска и ограничения стенда](projects/ClimateControl/README.md)

Для запуска нужен .NET 8 SDK. Открывать решение можно в Visual Studio 2022 или Visual Studio Code. Веб-редактор github.dev позволяет читать код; запуск .NET выполняется локально.

## Проверка

```powershell
cd projects/ClimateControl
dotnet restore ClimateControl.sln
dotnet build ClimateControl.sln -c Release
dotnet test tests/Climate.UnitTests -c Release
dotnet test tests/Climate.IntegrationTests -c Release
```

Физическое климатическое оборудование к учебному стенду не подключено. Наличие эксплуатационных заготовок в проекте не означает завершение ПР8 или промышленную приёмку системы.
