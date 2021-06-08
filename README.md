# [ICRS-NBKI-Request]

[![Build status]][appveyor]
[![GitHub Release]][releases]

Получение XML данных при обращении к API НБКИ для расчета показателя долговой
нагрузки (ПДН).

## Usage

1. Приготовить соответствующие требованиям НБКИ файлы XML с запросами,
их расширение должно быть `.req` (см. обезличенный [пример]).
2. Запустить программу `ICRS-NBKI-Request`.
3. Она отправит каждый из запросов и получит в ответ подписанные данные
формата `.xml.p7s` (PKCS#7), которые сохранит в файлы `.xml` с теми же
именами.

По умолчанию программа использует текущую папку `.`, но это можно
(раздельно и вход, и выход) настроить в `.exe.config`.

## License

Licensed under the [Apache License, Version 2.0].

[ICRS-NBKI-Request]: https://diev.github.io/ICRS-NBKI-Request/
[пример]: request.xml
[Apache License, Version 2.0]: LICENSE

[appveyor]: https://ci.appveyor.com/project/diev/icrs-nbki-request
[releases]: https://github.com/diev/ICRS-NBKI-Request/releases/latest

[Build status]: https://ci.appveyor.com/api/projects/status/q83mpd646lprhc42?svg=true
[GitHub Release]: https://img.shields.io/github/release/diev/ICRS-NBKI-Request.svg
