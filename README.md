# StudioFlow

Веб-приложение для бронирования репетиционных и звукозаписывающих студий.
Учебный MVP: клиент выбирает комнату и свободное время, а администратор управляет комнатами и почасовыми тарифами.

## Возможности

- Регистрация клиента и вход через JWT; также доступны заранее созданные демо-аккаунты.
- Роли Client, Manager и Admin.
- Просмотр комнат, вместимости и стоимости часа.
- Просмотр занятых интервалов и выбор начала и окончания на выбранную дату.
- Создание и отмена бронирований.
- Редактирование даты, начала и окончания активного бронирования.
- Добавление и редактирование комнат, назначение тарифа и отключение комнаты.
- Создание и редактирование тарифов.
- Защита от пересечений бронирований, в том числе при параллельных запросах.
- Разделение данных по TenantId.
- Адаптивный интерфейс на TypeScript.

## Стек

| Часть | Технологии |
| --- | --- |
| API | .NET 8, ASP.NET Core Minimal API |
| Данные | PostgreSQL 17, Entity Framework Core, Npgsql |
| Авторизация | JWT Bearer, роли, PBKDF2 для паролей |
| Валидация и логи | FluentValidation, Serilog |
| Интерфейс | TypeScript, HTML, CSS, ES-модули |
| Инструменты | Docker Compose, Swagger, Prettier |

## Локальный запуск

Требования: .NET SDK 8, Node.js 24 с npm, Docker с Compose.
Команды ниже рассчитаны на Bash/Zsh; выполняйте их из корня репозитория.

### 1. Запустить PostgreSQL

```bash
docker compose up -d postgres
```

Перед запуском API дождитесь готовности БД:

```bash
docker compose exec postgres pg_isready -U studioflow -d studioflow
```

### 2. Собрать интерфейс

```bash
cd StudioFlow
npm ci
npm run build
cd ..
```

### 3. Запустить приложение

```bash
Seed__AdminPassword='LocalAdmin123!' Seed__DemoClientPassword='LocalClient123!' dotnet run --project StudioFlow --urls http://localhost:5000
```

| Адрес | Назначение |
| --- | --- |
| http://localhost:5000 | Сайт |
| http://localhost:5000/swagger | Документация API |
| http://localhost:5000/health | Проверка работы HTTP-приложения |

При первом запуске создаются комната и тариф «Стандарт» стоимостью 1000 ₽/час.
Если администраторов ещё нет и задан Seed__AdminPassword, создаётся аккаунт
admin@studioflow.local с указанным паролем. Повторный запуск не меняет его пароль.

При заданном Seed__DemoClientPassword создаётся клиент client@studioflow.local с указанным
паролем в демонстрационной студии. Существующим аккаунтам пароли не меняются.
На сайте доступны отдельные режимы входа и регистрации с подтверждением пароля.
Для регистрации нужны только email и пароль длиной 8–128 символов.
Сервер назначает Client и демонстрационную студию; переданные роль и TenantId игнорируются.
Это публичный клиентский доступ к демо-студии, а не присоединение к произвольной
частной студии. Чужие бронирования остаются недоступны.

## Запуск через Docker

Из корня репозитория:

```bash
test -f .env || cp .env.example .env
docker compose up -d postgres
docker compose exec postgres pg_isready -U studioflow -d studioflow
docker compose up --build -d
```

Сайт: http://localhost:8080. Swagger: http://localhost:8080/swagger.
Dockerfile сам устанавливает зависимости и собирает TypeScript.

Compose читает настройки из .env: API_PORT, POSTGRES_PORT, SEED_ADMIN_PASSWORD
и SEED_DEMO_CLIENT_PASSWORD.
При использовании примера .env администратор создаётся автоматически с паролем
из SEED_ADMIN_PASSWORD, если в БД ещё нет администратора. Замените пример пароля своим.
Файл .env исключён из Git. Для обычного dotnet run он автоматически не загружается.
Если данные уже перенесены в studioflow_migrated, сохраните API_DATABASE=studioflow_migrated
в локальном .env. Не перезаписывайте существующий .env примером: это вернёт API к старой базе.

Чтобы создать первого администратора, когда сервисы уже запущены:

```bash
docker compose run --rm -e Seed__AdminPassword='LocalAdmin123!' api
```

Дождитесь запуска временного экземпляра API и остановите его через Ctrl+C.
Аккаунт сохранится в общей БД; после этого войдите через основной сайт на порту 8080.

Логи и остановка:

```bash
docker compose logs -f api
docker compose down
```

Данные PostgreSQL сохраняются в именованном Docker volume после остановки.

## Разработка фронтенда

В отдельном терминале из каталога StudioFlow:

```bash
npm run dev
```

Компилятор следит за TypeScript и обновляет JavaScript в wwwroot/js.
После изменения обновите страницу браузера. Отдельный сервер фронтенда не нужен:
статические файлы и API обслуживает ASP.NET Core.

Исходники редактируются в ClientApp/src, а не в сгенерированных файлах wwwroot/js.

Модули интерфейса:

- app.ts — обработчики событий и отображение экранов.
- api.ts — HTTP-клиент и обработка ошибок API.
- models.ts — типы сущностей и ответов.
- state.ts — состояние приложения и восстановление сессии.
- dom.ts — типизированный доступ к элементам страницы.
- format.ts — форматирование дат, сумм и HTML-экранирование.

Разметка находится в StudioFlow/wwwroot/index.html, стили — в
StudioFlow/wwwroot/css/site.css.

## Конфигурация

Настройки находятся в StudioFlow/appsettings.json и переопределяются переменными окружения.

| Переменная | Назначение |
| --- | --- |
| ConnectionStrings__Default | Строка подключения PostgreSQL |
| Jwt__Key | Ключ подписи JWT, минимум 32 байта |
| Jwt__Issuer | Издатель токена |
| Jwt__Audience | Получатель токена |
| Seed__AdminPassword | Пароль первого администратора |
| Seed__DemoClientPassword | Пароль демо-клиента client@studioflow.local |

В Docker Compose имя базы API можно изменить через API_DATABASE в .env (по умолчанию studioflow).

Локальная БД: localhost:5432, база studioflow, пользователь и пароль studioflow.
В Compose API подключается к хосту postgres.
Значения ключа JWT и пароля БД в репозитории предназначены для разработки.
Для развёртывания задайте собственные секреты через окружение.

## Структура

```text
StudioFlow/
├── Program.cs              # Сборка и запуск приложения
├── Configuration/          # Регистрация сервисов, JWT
├── Contracts/              # DTO запросов
├── Models/                 # Сущности
├── Data/                   # DbContext, создание БД и начальные данные
├── Endpoints/              # Подключение маршрутов
├── Handlers/               # HTTP-обработчики API
├── Services/               # Правила бронирований, доступность и оценка стоимости
├── Security/               # Токены, хеширование, claims
├── Validation/             # Правила валидации
├── Middleware/             # IExceptionHandler и единый формат ошибок
├── ClientApp/src/          # Исходники TypeScript
└── wwwroot/                # HTML, CSS, скомпилированный JavaScript
```

## API

Для защищённых маршрутов передавайте заголовок Authorization: Bearer <token>.
Токен возвращается при регистрации и входе.

| Метод | Маршрут | Действие |
| --- | --- | --- |
| POST | /api/auth/register | Создать Client в демо-студии и получить JWT (201) |
| POST | /api/auth/login | Вход |
| GET | /api/studio | Демонстрационная студия |
| GET | /api/rooms | Активные комнаты |
| POST | /api/rooms | Создать комнату |
| PUT | /api/rooms/{id} | Изменить комнату |
| GET | /api/rooms/{id}/slots?date=2026-12-01 | Слоты на дату |
| GET | /api/rooms/{id}/availability?date=2026-12-01 | Занятые интервалы комнаты |
| GET | /api/tariffs | Тарифы |
| POST | /api/tariffs | Создать тариф |
| PUT | /api/tariffs/{id} | Изменить тариф |
| GET | /api/bookings | Свои брони для Client; брони своей студии для Admin/Manager |
| GET | /api/bookings/{id} | Просмотр доступной брони |
| POST | /api/bookings | Создать бронирование |
| PUT | /api/bookings/{id} | Изменить активное бронирование |
| POST | /api/bookings/{id}/cancel | Отменить бронирование |

Управлять комнатами и тарифами могут Admin и Manager.
Отменять бронирование может его владелец либо Admin/Manager своего tenant.
Одна и та же проверка студии и владельца применяется к списку, отдельной брони,
изменению и отмене. Недоступный ID возвращает 404, без раскрытия существования
чужой брони. Запросы без JWT возвращают 401.
Подробные схемы запросов доступны в Swagger.

Пример регистрации:

```json
{
  "email": "client@example.com",
  "password": "MyPassword123!"
}
```

Email нормализуется: пробелы по краям удаляются, регистр приводится к нижнему.
Повторный email возвращает 409, некорректные данные — 400.
Регистрация не позволяет выбрать Admin/Manager или другую студию.

## Правила бронирования

Интерфейс предлагает ручной ввод начала и окончания:
от 30 минут до 8 часов, в пределах 09:00–22:00 UTC одного дня.
Длительность рассчитывается автоматически; занятые интервалы отображаются отдельно.
API проверяет UTC, начало в будущем, длительность, часы работы и пересечения.
Соседние интервалы разрешены: 10:00–11:00 и 11:00–12:00 не пересекаются.
Отменённые бронирования не блокируют время.

Помимо проверки в API, PostgreSQL использует exclusion constraint с btree_gist.
При пересечении активных бронирований одной комнаты API возвращает HTTP 409.

## Проверка изменений

```bash
dotnet build
cd StudioFlow
npm run typecheck
npm run format:check
npm run build
```

Форматирование фронтенда: npm run format.
### Интеграционные тесты PostgreSQL

Из корня проекта:

```bash
docker compose -f docker-compose.test.yml up -d --wait
dotnet test
docker compose -f docker-compose.test.yml stop
```

Тестовая БД работает отдельно от приложения, на порту 55432.
Каждый прогон создаёт собственную БД со случайным именем и удаляет только её.
Для другого PostgreSQL задайте TEST_POSTGRES_CONNECTION со строкой подключения
к служебной базе postgres. Пользователю нужны права создания БД и расширения btree_gist.

Тесты используют WebApplicationFactory, настоящий PostgreSQL и JWT из реального
входа через API. Проверяются два клиента одной студии, клиент другой студии,
Admin/Manager, список, просмотр, изменение, отмена, анонимные запросы,
безопасная регистрация, запрет назначения роли/студии через запрос,
конкурентные регистрации одного email и запрет создания брони в чужой комнате.
Дополнительно проверяются доступность после переноса/отмены, расчёт стоимости,
единый формат ошибок 400/404/409, некорректный JSON, а также 500 без деталей исключения.
Точность классификации PostgreSQL проверяется отдельно:
23P01 для booking_no_overlap даёт 409; другое ограничение — 500 с записью в лог.
В GitHub Actions PostgreSQL запускается как service, результаты dotnet test сохраняются в artifacts.

## Текущие ограничения

- Обновление схемы выполняется миграциями; база старой версии после EnsureCreated
  требует отдельного переноса данных по инструкции ниже.
- Модель tenant упрощена: сайт работает с одной демонстрационной студией.
  Публичная регистрация создаёт клиентов только в демо-студии.
  Присоединение к другим студиям и приглашения сотрудников пока не реализованы.
- Стоимость возвращается как estimatedCost по текущему тарифу, в RUB.
  Это оценка: при изменении тарифа она пересчитывается. Историческая цена не сохраняется,
  платежей, уведомлений и восстановления пароля пока нет.
- Нет refresh-токенов; после истечения JWT требуется повторный вход.
- Скрытые комнаты отсутствуют в текущем списке API; повторное включение через UI пока недоступно.
- Compose проверяет готовность PostgreSQL через healthcheck перед запуском API.

## Конфигурация репозитория

- .gitignore — исключение артефактов сборки, зависимостей и локальных секретов.
- .gitattributes и .editorconfig — окончания строк и правила редактирования.
- global.json — выбор установленного .NET SDK 8.
- .env.example — пример локальных переменных Docker Compose.
- StudioFlow/.nvmrc — Node.js 24.
- StudioFlow/.prettierrc.json и .prettierignore — правила форматирования.
- .github/workflows/ci.yml — проверка форматирования, сборка TypeScript и API при push и pull request.

Скомпилированные файлы wwwroot/js пока хранятся в Git для запуска без Node.js.
После изменений TypeScript выполняйте npm run build перед коммитом.

## Сервис бронирований и ошибки API

HTTP-обработчики бронирований передают DTO и контекст пользователя в BookingService.
Сервис напрямую использует DbContext: проверяет студию и владельца, валидирует время,
проверяет пересечения, создаёт, переносит и отменяет бронь.
Он также рассчитывает занятое время, часовые слоты и оценку стоимости.
Отдельные repository/unit-of-work слои не добавлены.
CRUD комнат и тарифов по-прежнему использует DbContext в обработчиках.

Ошибки возвращаются как application/problem+json:

- 400 — ошибки валидации с полем errors; некорректный JSON имеет ключ request.
- 404 — отсутствующий или недоступный ресурс.
- 409 — пересечение брони или недопустимое изменение отменённой брони.
- 500 — неожиданная ошибка без текста исключения или SQL.

В ProblemDetails есть traceId, а в заголовке — тот же X-Request-Id.
Неожиданные исключения логируются вместе с этим идентификатором.
Гонки бронирований защищены PostgreSQL: только нарушение ограничения
booking_no_overlap с кодом 23P01 преобразуется в 409.

Оформление C# проверяется в CI через CSharpier, TypeScript/HTML/CSS — через Prettier:

```bash
dotnet tool restore
dotnet csharpier StudioFlow StudioFlow.IntegrationTests
dotnet csharpier --check StudioFlow StudioFlow.IntegrationTests
cd StudioFlow
npm run format:check
```

## Миграции PostgreSQL

Приложение применяет миграции через MigrateAsync перед начальным заполнением БД.
На пустой базе создаются таблицы и история __EFMigrationsHistory.
SQL изменения схемы при старте больше не выполняются отдельно от миграций.

- InitialSchema — таблицы, внешние ключи и индексы.
- PreventOverlappingBookings — расширение btree_gist и ограничение booking_no_overlap.
  Оно блокирует пересечения активных броней одной комнаты. Отмена освобождает время,
  а соседние интервалы разрешены. При откате этой миграции удаляется только ограничение;
  расширение сохраняется, поскольку может использоваться другими объектами.

Из корня проекта:

```bash
dotnet tool restore
dotnet ef migrations list --project StudioFlow
dotnet ef migrations add DescribeYourChange --project StudioFlow --output-dir Data/Migrations
dotnet ef database update --project StudioFlow
dotnet ef migrations has-pending-model-changes --project StudioFlow
```

Для генерации SQL без применения:

```bash
dotnet ef migrations script --idempotent --project StudioFlow --output migration.sql
```

Design-time factory не запускает веб-приложение и не создаёт демо-аккаунты.
Для выбора базы используйте ConnectionStrings__Default. Пароль через .env
для dotnet ef автоматически не загружается.
Для развёртывания с несколькими экземплярами применяйте миграции одним процессом
до запуска остальных экземпляров.

### Переход с прежней базы после EnsureCreated с сохранением данных

Не запускайте новую версию API на прежней базе напрямую:
в ней уже есть таблицы, но отсутствует история миграций.
Не удаляйте таблицы и не добавляйте фиктивную историю вручную.
Переносите данные в новую базу с миграциями, оставляя исходную для проверки и возврата.

Ниже пример для стандартного локального Compose. Если имена баз, порты или учётные
данные изменены, скорректируйте команды. База studioflow_migrated должна быть новой.

1. Остановите запись в исходную базу и сохраните полный резервный архив:

```bash
docker compose stop api
docker compose up -d postgres
docker compose exec -T postgres pg_dump -U studioflow -d studioflow --format=custom --file=/tmp/studioflow-before-migrations.dump
docker compose cp postgres:/tmp/studioflow-before-migrations.dump ./studioflow-before-migrations.dump
```

Архив содержит данные пользователей. Храните его локально, не добавляйте в Git.

2. Создайте новую базу и примените к ней миграции без запуска API и seed:

```bash
docker compose exec -T postgres createdb -U studioflow studioflow_migrated
dotnet tool restore
ConnectionStrings__Default='Host=localhost;Port=5432;Database=studioflow_migrated;Username=studioflow;Password=studioflow' dotnet ef database update --project StudioFlow
```

3. Перенесите только данные в одной транзакции (Bash/Zsh):

```bash
set -o pipefail
docker compose exec -T postgres pg_dump -U studioflow -d studioflow --data-only --no-owner --no-privileges --exclude-table='public."__EFMigrationsHistory"' | docker compose exec -T postgres psql -U studioflow -d studioflow_migrated --single-transaction -v ON_ERROR_STOP=1
```

При ошибке импорта целевая транзакция откатится, исходная база не изменяется.
Не импортируйте старую схему поверх миграций. Если данные содержат пересекающиеся
активные брони, импорт завершится ошибкой ограничения — сначала разберите эти конфликты на копии.

4. Сравните количество и содержимое Users, Rooms, Tariffs и Bookings,
   проверьте вход и операции с бронями на копии.
5. После успешной проверки задайте API_DATABASE=studioflow_migrated в .env
   и запустите docker compose up --build -d api.
   При обычном dotnet run передайте строку подключения новой базы через окружение.
   Не удаляйте старую базу и архив до завершения проверки.

Автотесты проверяют запуск на пустой БД, повторный запуск, откат и возврат ограничения,
защиту от пересечений и перенос копии данных прежней схемы без изменения исходника.
