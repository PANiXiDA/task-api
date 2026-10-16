# Task API

Тестовое задание: ASP.NET Core Minimal API на .NET 9, PostgreSQL / EF Core и RabbitMQ.

Структура основана на [dotnet-backend-template](https://github.com/panixida-templates/dotnet-backend-template/tree/development); зависимости от PANiXiDA.Core не используются.

## Проверка для заказчика: без установки .NET и знания кода

Это серверное API без отдельного сайта с кнопками. Самый простой способ проверить задание — запустить готовые автотесты одной командой. Они сами создают и завершают задачи, проверяют БД и получают настоящее сообщение из RabbitMQ.

### 1. Подготовить компьютер и скачать проект

1. Установите [Docker Desktop](https://docs.docker.com/desktop/setup/install/windows-install/) и запустите его. На Windows используйте Linux containers; если установщик попросит настроить WSL или перезагрузиться, выполните его инструкции. Docker Compose входит в Docker Desktop.
2. Дождитесь, когда Docker Desktop сообщит, что Docker Engine запущен. Для первой сборки нужен интернет: Docker скачает необходимые компоненты.
3. Распакуйте полученный от разработчика ZIP-архив проекта. Если скачиваете с GitHub, откройте [ветку с реализацией](https://github.com/PANiXiDA/task-api/tree/feature/implement-task-api), затем **Code → Download ZIP**. Пока [PR №1](https://github.com/PANiXiDA/task-api/pull/1) не слит, скачивать нужно именно эту ветку, а не `development`. Репозиторий приватный: для скачивания через GitHub нужен выданный доступ; переданный ZIP такого доступа не требует.
4. Откройте распакованную папку, в которой лежат `README.md`, `compose.yaml` и `compose.test.yaml`. В Проводнике Windows нажмите на адресную строку, введите `powershell` и нажмите Enter. Все команды ниже выполняйте в открывшемся окне.

Проверьте, что Docker доступен:

```powershell
docker compose version
```

Должна появиться строка с версией Docker Compose. Устанавливать .NET, PostgreSQL, RabbitMQ, Git или Postman отдельно не нужно.

### 2. Запустить автоматическую проверку

Скопируйте в PowerShell всю команду и нажмите Enter:

```powershell
docker compose -p task-api-tests -f compose.test.yaml up --build --abort-on-container-exit --exit-code-from tests --attach tests
```

Первый запуск может занять несколько минут. Дождитесь окончания: команда сама соберёт приложение, запустит отдельные PostgreSQL и RabbitMQ, выполнит проверки и остановит тестовые контейнеры. Обычный сервис для этого запускать не нужно.

**Успешный результат в конце вывода:**

```text
Test Run Successful.
Total tests: 23
     Passed: 23
```

В логах могут встречаться ошибки БД и недоступности RabbitMQ: некоторые тесты специально воспроизводят эти ситуации. Ориентируйтесь на итог `Test Run Successful.` и количество прошедших тестов. Если в конце написано `Test Run Failed`, есть упавшие тесты или сборка оборвалась, проверка не пройдена — передайте разработчику вывод окна.

После проверки удалите её временные контейнеры:

```powershell
docker compose -p task-api-tests -f compose.test.yaml down -v
```

Команда очистки относится только к отдельному тестовому окружению и не удаляет задачи из обычного сервиса.

### 3. Запустить сервис для ручной проверки

В том же окне PowerShell выполните:

```powershell
docker compose up --build -d
```

Команда поднимает всё необходимое: **API + PostgreSQL + RabbitMQ**. Подключения уже настроены, таблицы создаются автоматически миграциями. Проверить контейнеры можно командой `docker compose ps`: у API ожидается состояние `Up`, у PostgreSQL и RabbitMQ — `Up (healthy)`.

Откройте в браузере [http://localhost:8080/health](http://localhost:8080/health). Ожидаемый ответ:

```json
{"status":"ok"}
```

Если страница не открылась сразу, подождите несколько секунд после запуска и обновите её. Список задач доступен по адресу [http://localhost:8080/tasks](http://localhost:8080/tasks); в новой БД он пустой: `[]`.

### 4. Проверить создание, завершение и удаление вручную

Копируйте блоки по порядку в **одно и то же окно PowerShell**. ID задачи сохраняется автоматически: ничего подставлять вместо `<id>` не нужно.

Создайте задачу:

```powershell
$baseUrl = "http://localhost:8080"
$task = Invoke-RestMethod -Method Post -Uri "$baseUrl/tasks" -ContentType "application/json" -Body '{"title":"Buy milk","priority":"High"}'
$task | ConvertTo-Json
```

В ответе должны быть `title: "Buy milk"`, `priority: "High"`, `isCompleted: false` и `completedAt: null`.

Завершите её:

```powershell
Invoke-RestMethod -Method Put -Uri "$baseUrl/tasks/$($task.id)/complete" | ConvertTo-Json
```

Теперь `isCompleted` равен `true`, а `completedAt` содержит дату и время завершения. Если выполнить эту команду повторно, ожидается ошибка **409 Conflict**: повторное завершение запрещено.

Посмотрите список задач:

```powershell
Invoke-RestMethod -Uri "$baseUrl/tasks" | ConvertTo-Json
```

Созданная задача должна присутствовать в списке и быть завершённой. Удалите её и снова откройте список:

```powershell
Invoke-RestMethod -Method Delete -Uri "$baseUrl/tasks/$($task.id)"
Invoke-RestMethod -Uri "$baseUrl/tasks" | ConvertTo-Json
```

Успешное удаление не выводит текст. Задача исчезнет из списка; если других задач нет, список пустой (PowerShell может не вывести ничего).

Получение события из RabbitMQ проверяют автотесты из шага 2. В обычном запуске нет интерфейса брокера и очереди получателя: они не нужны для работы API, а тест создаёт свою очередь автоматически. Успешное ручное завершение задачи само по себе не подтверждает доставку события.

### 5. Остановить сервис

```powershell
docker compose down
```

Созданные вручную задачи сохраняются в Docker volume и будут доступны после следующего запуска.

### Если что-то не получилось

| Что видно | Что сделать |
| --- | --- |
| `docker` не найден | Установите Docker Desktop и заново откройте PowerShell. |
| `Cannot connect` / `error during connect` к Docker | Запустите Docker Desktop и дождитесь запуска Engine. |
| `no configuration file provided` | Откройте PowerShell в папке, где лежит `compose.yaml`. |
| Ошибка скачивания образов или пакетов | Проверьте интернет и доступ Docker к сети, затем повторите команду. |
| `port is already allocated` / занят 8080 | Выполните `$env:TASK_API_PORT = "8081"`, затем повторите `docker compose up --build -d`. В адресах браузера и `$baseUrl` используйте порт 8081. |
| API не отвечает после запуска | Выполните `docker compose ps` и `docker compose logs --tail=100 api`, передайте вывод разработчику. |
| При скачивании с GitHub ошибка 404 | Проверьте доступ к приватному репозиторию или используйте ZIP, полученный от разработчика. |

Команды Docker работают и на macOS/Linux с установленным Docker Compose; блоки `Invoke-RestMethod` выше предназначены для PowerShell.

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
