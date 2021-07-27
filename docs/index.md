# ICRS-NBKI-Request
                   
[![Build status]][appveyor]
[![GitHub Release]][releases]

Получение XML данных при обращении к API НБКИ для расчета показателя долговой
нагрузки (ПДН).

## Usage

1. Приготовить соответствующие требованиям НБКИ файлы XML с запросами,
их расширение должно быть `.req`.
2. Запустить программу `ICRS-NBKI-Request`.
3. Она отправит каждый из запросов и получит в ответ подписанные данные
формата `.xml.p7s` (PKCS#7), которые сохранит в файлы `.xml` с теми же
именами.

По умолчанию программа использует текущую папку `.`, но это можно
(раздельно и вход, и выход) настроить в `.exe.config`.

## HowTo

Как быстро посмотреть параметр Thumbprint (отпечаток сертификата)
с помощью PowerShell - в хранилище Личных (для клиента):

    Get-ChildItem -Path cert:\CurrentUser\My

в хранилище Других пользователей (для удаленного сервера):

    Get-ChildItem -Path cert:\CurrentUser\Addressbook

## License

Licensed under the [Apache License, Version 2.0].

[Apache License, Version 2.0]: http://www.apache.org/licenses/LICENSE-2.0 "LICENSE"

[appveyor]: https://ci.appveyor.com/project/diev/icrs-nbki-request
[releases]: https://github.com/diev/ICRS-NBKI-Request/releases/latest

[Build status]: https://ci.appveyor.com/api/projects/status/q83mpd646lprhc42?svg=true
[GitHub Release]: https://img.shields.io/github/release/diev/ICRS-NBKI-Request.svg
