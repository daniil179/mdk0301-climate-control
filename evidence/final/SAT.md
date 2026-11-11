# Протокол программного стенда SAT

Протокол автоматических проверок. Не является актом приёмки оборудования у заказчика.

| ID | Проверка | Статус | Доказательство |
|---|---|---|---|
| CERT | X.509 chain, validity, DNS and Code Signing EKU | PASS | Offline test CA; revocation is not tested. |
| UPDATE | Signed SemVer 1.1.0 package activates | PASS | installation/active.json |
| SHA256 | Corrupt package is rejected; active pointer stays unchanged | PASS | No installation of 1.2.0 |
| MIGRATION | Expand migration preserves legacy row and assigns Priority=0 | PASS | outbox.db / __EFMigrationsHistory |
| OFFLINE | SQLite + Polly: two transient failures, third attempt accepted | PASS | Attempts=3; persisted Delivered state |
| BACKUP | Online backup integrity and restore preserve two rows | PASS | backup.db.sha256 / restored.db |
| BCRYPT | BCrypt accepts correct and rejects wrong demo password | PASS | Only verification booleans are logged. |
| RBAC | Operator cannot change configuration; engineer can | PASS | Closed role/permission matrix. |
| DPAPI | CurrentUser secret round-trip and purpose isolation | PASS | Windows DPAPI executed; raw keys and blobs are not included in the report. |

Статусы отражают только перечисленные программные проверки. Приёмка сети, оборудования и площадки выполняется отдельно.
