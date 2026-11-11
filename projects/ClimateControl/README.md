# Climate Control — МДК 03.01, вариант 3

Бойцов Даниил Антонович, И13-24-11. Учебный модуль АИС «Умный кампус».
Датчики температуры/влажности, HMAC-SHA256, ASP.NET Core 8, EF Core SQLite.

## Статус работ
ПР1–ПР7 выполнены в отчётах. Программные проверки проведены на изолированном стенде.
ПР8 дополнена Windows DPAPI и скриптами PowerShell. Программные проверки выполнены на Windows; реальная приёмка оборудования и площадки не проводилась.

Код основан на модуле МДК03.02 и расширен для API v1/v2, миграции, резервирования и CALS.
src/Climate.Operations содержит вспомогательные разработки для ПР3 и ПР5.
Модули ПР1–ПР8 включены в общее решение; отчёты описывают границы выполненных проверок.
Windows Forms для этого задания не требуется: ПР4 прямо задаёт ASP.NET Core.
Физическим климатическим оборудованием программа не управляет.

## Состав
- src/Climate.Core — DTO, FluentValidation, HMAC.
- src/Climate.Infrastructure — EF Core/SQLite хранилище телеметрии.
- src/Climate.Api — приём телеметрии, авторизация и разные ответы v1/v2.
- src/Climate.Operations — миграции outbox, резервирование, метрики, вспомогательные модули.
- tests/Climate.UnitTests — 63 xUnit-случаев.
- tests/Climate.IntegrationTests — 26 HTTP-сценариев на ASP.NET Core TestServer.
- tools/Climate.TestRunner — официальный xUnit AssemblyRunner с JSON-протоколом.
- tools/Climate.Portfolio — воспроизводимое демо миграции/копии/очереди;
  содержит выполненные проверки портфолио.
- samples/Climate.Before — намеренно дефектный пример из предыдущего аудита;
  в маршрутах API не используется.
- samples/Climate.After и tools/Climate.Metrics — предыдущие вспомогательные материалы.
- docs/electronic-composition.json, device-registry.xml — состав и пример обмена для ПР6.

## Проверка в обычной среде .NET 8 SDK
Открыть ClimateControl.sln в Visual Studio либо выполнить из каталога project:

    dotnet restore ClimateControl.sln
    dotnet build ClimateControl.sln -c Release
    dotnet test tests/Climate.UnitTests -c Release
    dotnet test tests/Climate.IntegrationTests -c Release

Требуется доступ к nuget.org при первом восстановлении пакетов.
Отдельный сервер БД не нужен. SQLite — реальная реляционная БД в тестовом контуре,
но не PostgreSQL и не общий сетевой файл.

В приложенном стенде выполнены те же xUnit-тесты официальным AssemblyRunner:

    dotnet run --project tools/Climate.TestRunner -c Release -- Climate.UnitTests artifacts/unit-results.json
    dotnet run --project tools/Climate.TestRunner -c Release -- Climate.IntegrationTests artifacts/integration-results.json

Итог: 63 + 26 Passed, Failed 0, Skipped 0.
B01-случаи подтверждают известные дефекты намеренно плохого Legacy-образца,
а не пригодность этого образца к эксплуатации.

Дополнительное демо, каждый раз с новым пустым каталогом:

    dotnet run --project tools/Climate.Portfolio -c Release -- demo artifacts/demo-01

Оно создаёт тестовую SQLite БД, копию и восстановленную БД, рассчитывает метрики.
Сбой сети в демо имитируется транспортом; интеграционный V06 отдельно проходит реальный
pipeline API. Демо использует временный учебный центр сертификации и отключает отзыв
только в этой тестовой проверке, не в промышленной политике доверия.

Проверка состава при наличии Python 3:

    python docs/verify-composition.py

## Самостоятельный API
Запуск не требует компьютера пользователя для выполненных тестов.
Для отдельного ручного стенда реестр устройств задаётся переменными среды.
Производственные ключи в архив не включены. В PowerShell:

    $key = New-Object byte[] 32
    [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($key)
    $env:Climate__Devices__0__SensorId = "DVC-001"
    $env:Climate__Devices__0__KeyId = "key-1"
    $env:Climate__Devices__0__RoomId = "A101"
    $env:Climate__Devices__0__KeyBase64 = [Convert]::ToBase64String($key)
    $env:Climate__Devices__0__CanWrite = "true"
    dotnet run --project src/Climate.Api --urls http://127.0.0.1:5080

GET /health открыт. Другие маршруты требуют HMAC-заголовки:
X-Device-Id, X-Key-Id, X-Timestamp (Unix seconds), X-Nonce, X-Signature.
Для GET тело пустое; путь включает номер версии. Пример подписания и работы клиента:
tests/Climate.IntegrationTests/VersioningTests.cs и ClimateFactory.cs.

Маршруты:
- POST /api/v1/telemetry
- GET /api/v1/climate/readings/latest/DVC-001
- GET /api/v2/climate/readings/latest/DVC-001

## Ограничения и эксплуатация
- Ключи TestRegistry и тестовые пароли — исключительно учебные фикстуры.
- Local HTTP/TestServer не подтверждают промышленный TLS или настройку реальной сети.
- Replay-cache в памяти теряется при перезапуске; промышленное общее хранилище не настроено.
- OfflineBuffer рассчитан на одного работника и один DbContext.
- События старше 10 минут могут не пройти текущий API и остаются на разбор, не удаляются.
- 409 не приравнивается к доставке: возможен конфликт содержимого одного номера события.
- Восстановление копии выполняется в новый файл; нельзя переписывать работающую базу.
- Down миграции оставлен для разработки, производственный план использует expand/roll-forward.
- RPO/RTO в отчёте — предложенные цели, а не измеренное SLA.
- DPAPI CurrentUser проверена на Windows. SAT реального оборудования и целевой площадки не проводился.

## Скрипты ПР8

`./scripts/backup.ps1 -Database <source.db> -Backup <new-backup.db>`

`./scripts/new-sat.ps1 -ChecksJson <checks.json> -Output <SAT.md>`

Оба скрипта принимают необязательный `-DotnetPath <dotnet.exe>`. Не перезаписывайте работающую базу. Обновлятор проверяет локальный учебный пакет и подписанный манифест; загрузка через HTTPS в этом проекте не реализована.
