# Task API

Тестовое задание: ASP.NET Core Minimal API на .NET 9, PostgreSQL / EF Core и RabbitMQ.

Структура основана на [dotnet-backend-template](https://github.com/panixida-templates/dotnet-backend-template/tree/development); зависимости от PANiXiDA.Core не используются.

## Запуск

Требуется только Docker с Compose. SDK .NET на компьютере не нужен.

```sh
docker compose up --build -d
```

API: `http://localhost:8080`. Проверка запуска: `GET /health`.
При занятом порте задайте переменную окружения `TASK_API_PORT`.
Миграции EF Core применяются перед запуском HTTP-сервера. PostgreSQL сохраняет данные в volume.

```sh
docker compose logs -f api
docker compose down
```

Пароли в Compose — демонстрационные значения только для локальных контейнеров. Порты PostgreSQL и RabbitMQ не публикуются, API доступен только через loopback.

## API

| Метод | Путь | Результат |
| --- | --- | --- |
| POST | `/tasks` | `201`, созданная задача; неверный JSON, Title или Priority → `400` |
| GET | `/tasks` | `200`, все задачи в порядке CreatedAt / Id |
| PUT | `/tasks/{id}/complete` | `200`, завершённая задача; отсутствует → `404`; уже завершена или конкурентно изменена → `409` |
| DELETE | `/tasks/{id}` | `204`, физическое удаление; отсутствует → `404` |

Некорректный GUID возвращает `400`. Пустой GUID корректен синтаксически, но такой задачи нет: `404`.
Ошибки оформляются как Problem Details. Пагинации и аутентификации нет, согласно объёму задания.

Создание:

```sh
curl -i http://localhost:8080/tasks \
  -H 'Content-Type: application/json' \
  -d '{"title":"Buy milk","priority":"High"}'
```

`title` обязателен, содержит хотя бы один непробельный символ, максимум 200 символов; исходный текст сохраняется.
`priority` по умолчанию `Medium`; принимаются `Low`, `Medium`, `High` или соответствующие числа `0`, `1`, `2`.
Ответы и события содержат строковое значение приоритета. `IsCompleted=false`, `CompletedAt=null` при создании.
Временные метки записываются в UTC с точностью PostgreSQL до микросекунды, одинаковой для API, БД и события. Повторное завершение не изменяет `CompletedAt` и не публикует событие.

```sh
curl -i -X PUT http://localhost:8080/tasks/<id>/complete
curl http://localhost:8080/tasks
curl -i -X DELETE http://localhost:8080/tasks/<id>
```

В Windows используйте `curl.exe` или `Invoke-RestMethod`.

## Устройство

```text
src/
  PANiXiDA.TaskApi.Host/             # startup, конфигурация, Dockerfile
  Tasks/
    PANiXiDA.TaskApi.Tasks.Domain/         # TaskItem, Priority, инварианты
    PANiXiDA.TaskApi.Tasks.Application/    # сценарии, контракты хранилища и событий
    PANiXiDA.TaskApi.Tasks.Infrastructure/ # EF Core, миграции, RabbitMQ
    PANiXiDA.TaskApi.Tasks.Presentation/   # Minimal API, DTO, HTTP-валидация
tests/Tasks/PANiXiDA.TaskApi.Tasks.FunctionalTests/
```

Host связывает слои через встроенный DI. Application зависит только от Domain; Presentation не обращается к EF или RabbitMQ. Версии NuGet централизованы в `Directory.Packages.props`.

`TaskItem.RowVersion` имеет тип `uint` и настроен через `IsRowVersion()`: Npgsql использует системную колонку PostgreSQL `xmin`. EF добавляет исходную версию в условие UPDATE; `DbUpdateConcurrencyException` преобразуется в `409`. Для двух запросов, прочитавших одну версию строки, сохраняется только один результат. Подробнее: [Npgsql concurrency tokens](https://www.npgsql.org/efcore/modeling/concurrency.html).

## RabbitMQ и остановка

После успешного `SaveChangesAsync` неизменяемое событие добавляется в ограниченную очередь в памяти. HTTP-запрос не ждёт RabbitMQ. Один BackgroundService последовательно публикует события через асинхронный клиент, поэтому канал не используется параллельно.

- Exchange: `task.events`, тип `topic`, durable.
- Routing key: `task.completed`.
- JSON: `taskId`, `title`, `completedAt`, `priority`.
- Сообщение persistent, `Content-Type=application/json`, `MessageId=taskId`.
- Попытка публикации ограничена 2 секундами; ошибка логируется вместе с JSON события. Следующее событие может заново открыть соединение.
- При переполнении очереди или остановке publisher событие также записывается в лог.
- При SIGTERM/Ctrl+C очередь закрывается для новых событий. На завершение текущих и ожидающих публикаций отводится 4 секунды, затем до 1 секунды на отмену и закрытие канала/соединения.

Очередь получателя и binding создаёт получатель; приложение только объявляет exchange и публикует. Для проверки тест создаёт временную очередь с binding `task.completed` и читает реальное сообщение.

Это best-effort доставка, как в ТЗ: нет outbox, повторной доставки и publisher confirms. При падении процесса между сохранением и публикацией или недоступном брокере событие может потеряться. Если у exchange нет подходящей очереди, RabbitMQ удалит сообщение. Завершение задачи при этом сохраняется.

## Тесты

```sh
docker compose -p task-api-tests -f compose.test.yaml up --build --abort-on-container-exit --exit-code-from tests
docker compose -p task-api-tests -f compose.test.yaml down -v
```

Команда сборки и тестов работает полностью в контейнере .NET 9. PostgreSQL и RabbitMQ настоящие, изолированы от обычного запуска; тестовая БД находится в tmpfs.

Проверяются создание/завершение/удаление, значения по умолчанию, валидация, получение события из RabbitMQ, отсутствие повторного события, отсутствие публикации при ошибке сохранения, недоступный брокер, гонка двух запросов с одной версией строки, drain/таймаут остановки, переполнение очереди и логирование потерянных сообщений. Гонка воспроизводится барьером перед UPDATE, чтобы оба запроса гарантированно прочитали одну версию.

## Конфигурация

| Переменная | Назначение |
| --- | --- |
| `ConnectionStrings__Tasks` | Строка подключения PostgreSQL, обязательна |
| `RabbitMq__HostName` | Адрес RabbitMQ |
| `RabbitMq__Port` | AMQP-порт, по умолчанию 5672 |
| `RabbitMq__UserName`, `RabbitMq__Password` | Учётные данные, обязательны |
| `RabbitMq__VirtualHost` | Virtual host, по умолчанию `/` |
| `RabbitMq__QueueCapacity` | Размер очереди публикаций, по умолчанию 1024 |

В production секреты передаются через окружение/секрет-хранилище, а миграции обычно выносятся в отдельный шаг развёртывания. `/health` показывает работоспособность HTTP-процесса, а не доступность внешних зависимостей.
