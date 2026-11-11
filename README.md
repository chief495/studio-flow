# StudioFlow

Веб-приложение для бронирования репетиционных и звукозаписывающих студий.
Учебный MVP: клиент выбирает комнату и свободное время, а администратор управляет комнатами и почасовыми тарифами.

## Возможности

- Регистрация и вход через JWT.
- Роли Client, Manager и Admin.
- Просмотр комнат, вместимости и стоимости часа.
- Просмотр свободных часовых слотов на выбранную дату.
- Создание и отмена бронирований.
- Добавление и редактирование комнат, назначение тарифа и отключение комнаты.
- Создание и редактирование тарифов.
- Защита от пересечений бронирований, в том числе при параллельных запросах.
- Разделение данных по TenantId.
- Адаптивный интерфейс на TypeScript с отдельными формами входа и регистрации.

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
Seed__AdminPassword='LocalAdmin123!' dotnet run --project StudioFlow --urls http://localhost:5000
```

| Адрес | Назначение |
| --- | --- |
| http://localhost:5000 | Сайт |
| http://localhost:5000/swagger | Документация API |
| http://localhost:5000/health | Проверка работы HTTP-приложения |

При первом запуске создаются комната и тариф «Стандарт» стоимостью 1000 ₽/час.
Если администраторов ещё нет и задан Seed__AdminPassword, создаётся аккаунт
admin@studioflow.local с указанным паролем. Повторный запуск не меняет его пароль.

Зарегистрированный через сайт пользователь получает роль Client и доступ к демонстрационной студии.

## Запуск через Docker

Из корня репозитория:

```bash
cp .env.example .env
docker compose up -d postgres
docker compose exec postgres pg_isready -U studioflow -d studioflow
docker compose up --build -d
```

Сайт: http://localhost:8080. Swagger: http://localhost:8080/swagger.
Dockerfile сам устанавливает зависимости и собирает TypeScript.

Compose читает настройки из .env: API_PORT, POSTGRES_PORT и SEED_ADMIN_PASSWORD.
При использовании примера .env администратор создаётся автоматически с паролем
из SEED_ADMIN_PASSWORD, если в БД ещё нет администратора. Замените пример пароля своим.
Файл .env исключён из Git. Для обычного dotnet run он автоматически не загружается.

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
├── Handlers/               # Обработчики API
├── Security/               # Токены, хеширование, claims
├── Validation/             # Правила валидации
├── Middleware/             # Обработка конфликтов бронирования
├── ClientApp/src/          # Исходники TypeScript
└── wwwroot/                # HTML, CSS, скомпилированный JavaScript
```

## API

Для защищённых маршрутов передавайте заголовок Authorization: Bearer <token>.
Токен возвращается при регистрации и входе.

| Метод | Маршрут | Действие |
| --- | --- | --- |
| POST | /api/auth/register | Регистрация |
| POST | /api/auth/login | Вход |
| GET | /api/studio | Демонстрационная студия |
| GET | /api/rooms | Активные комнаты |
| POST | /api/rooms | Создать комнату |
| PUT | /api/rooms/{id} | Изменить комнату |
| GET | /api/rooms/{id}/slots?date=2026-12-01 | Слоты на дату |
| GET | /api/tariffs | Тарифы |
| POST | /api/tariffs | Создать тариф |
| PUT | /api/tariffs/{id} | Изменить тариф |
| GET | /api/bookings | Бронирования текущего tenant |
| POST | /api/bookings | Создать бронирование |
| POST | /api/bookings/{id}/cancel | Отменить бронирование |

Управлять комнатами и тарифами могут Admin и Manager.
Отменять бронирование может его владелец либо Admin/Manager своего tenant.
Подробные схемы запросов доступны в Swagger.

## Правила бронирования

Интерфейс предлагает часовые слоты с 09:00 до 22:00 UTC.
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
Интеграционные тесты пока не добавлены.

## Текущие ограничения

- Схема БД создаётся через EnsureCreated; миграций EF Core пока нет.
- Модель tenant упрощена: сайт работает с одной демонстрационной студией;
  API регистрации принимает TenantId без приглашения или проверки членства.
- API возвращает все бронирования своего tenant; клиентский интерфейс фильтрует их по пользователю.
  Ограничение выдачи на сервере ещё предстоит реализовать.
- Выдача слотов ограничена часами работы, но API создания бронирования пока не проверяет
  соответствие этим слотам и запрет бронирования в прошлом.
- Нет расчёта и сохранения итоговой стоимости, платежей, уведомлений и восстановления пароля.
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
