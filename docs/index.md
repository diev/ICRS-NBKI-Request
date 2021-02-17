# [ICRS-NBKI-Request]

Получение XML данных при обращении к API НБКИ для расчета показателя долговой
нагрузки (ПДН).

## Usage

1. Приготовить соответствующий требованиям НБКИ XML файл с запросом
`request.xml`.
2. Запустить программу `ICRS-NBKI-Request`.
3. Она отправит запрос и получит в ответ подписанный файл `result.xml.p7s`.
4. Параллельно будет создан файл с чистым XML `result.xml` без подписи.

## License

Licensed under the [Apache License, Version 2.0].

[Apache License, Version 2.0]: http://www.apache.org/licenses/LICENSE-2.0 "LICENSE"
