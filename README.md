# Threads API Backend

Backend для соціального застосунку у стилі Threads, побудований на `ASP.NET Core Web API` з `PostgreSQL`, `EF Core`, `JWT`, `AWS S3` і обробкою медіа через `ffmpeg`.

> Останнє оновлення документації: `2026-10-08`

## Зміст

- [Що вміє API](#що-вміє-api)
- [Структура проєкту](#структура-проєкту)
- [Технології](#технології)
- [Запуск через Docker](#запуск-через-docker)
- [Конфігурація](#конфігурація)
- [Документація API](#документація-api)
- [Рольова авторизація](#рольова-авторизація)
- [Обробка помилок](#обробка-помилок)
- [Rate limiting](#rate-limiting)
- [Огляд API](#огляд-api)
- [Логіка CommentService](#логіка-commentservice)
- [Кешування](#кешування)
- [Логування](#логування)
- [Обробка медіа](#обробка-медіа)
- [Тестування](#тестування)
- [Розгортання](#розгортання)

## Що вміє API

- JWT-автентифікація з `access token` + `refresh token`
- реєстрація, логін, logout, `me`, скидання і зміна пароля
- профілі користувачів з `avatar`, `banner`, `bio`, `location` і датою народження
- налаштування видимості дати та року народження (`public`, `followers`, `following`, `mutual`, `only_me`)
- пости з текстом, link preview, цитатами, локацією, медіа, опитуваннями та edit history
- відкладена публікація постів через scheduled posts і background worker
- лайки, репости, bookmarks і перегляди для постів та коментарів
- коментарі з вкладеністю, thread context, edit history, медіа, poll, location і link preview
- підписки: followers / following
- пошук користувачів і постів
- пошук GIF через `Giphy`
- пошук локацій через `Geoapify`
- кешування профілів, постів і зовнішнього пошуку через `HybridCache` і `Redis`
- завантаження зображень і відео в `AWS S3`
- стиснення відео та генерація thumbnail перед upload
- endpoint-specific rate limiting для auth, створення контенту, interactions, upload і зовнішнього пошуку
- OpenAPI-опис і Swagger UI
- структуроване логування auth-подій, помилок, зовнішніх інтеграцій і повільних запитів

## Структура проєкту

```text
BackEndForFinalProject
├── Threads.Api
│   ├── Controllers
│   │   ├── AuthController.cs       # registration, sessions, verification і password recovery
│   │   ├── MeController.cs         # профіль, колекції та пароль поточного користувача
│   │   ├── UsersController.cs      # публічні профілі та їхні колекції
│   │   ├── PostsController.cs      # пости та взаємодії з ними
│   │   ├── CommentsController.cs   # коментарі та відповіді
│   │   ├── FollowsController.cs    # followers і following
│   │   ├── SearchController.cs     # пошук користувачів, постів, GIF і локацій
│   │   ├── LinkPreviewsController.cs # безпечне отримання metadata зовнішнього URL
│   │   ├── MediaController.cs      # upload і доступ до медіа
│   │   ├── ReportsController.cs    # створення скарг на контент і користувачів
│   │   └── Admin*Controller.cs     # moderation, users і dashboard для адміністратора
│   ├── ExceptionHandling           # глобальне перетворення винятків у ProblemDetails
│   ├── Extensions                  # controller helper для явних ProblemDetails-відповідей
│   ├── Middleware                  # логування повільних HTTP-запитів
│   ├── Requests                    # HTTP-моделі для multipart/form-data
│   ├── Responses                   # стабільні title/detail для API-помилок
│   └── Program.cs                  # entrypoint і DI-конфігурація API
├── Threads.Application
│   ├── DTOs
│   │   ├── Posts / Comments        # DTO публікацій і коментарів
│   │   ├── Likes                   # cursor-paged response для лайкнутих targets
│   │   ├── Bookmarks               # cursor-paged response для збережених targets
│   │   ├── Reposts                 # cursor-paged response для repost targets
│   │   ├── Pagination              # спільні cursor request/response DTO
│   │   └── Auth / Users / Media    # інші request/response DTO
│   ├── Interfaces                  # контракти сервісів і репозиторіїв
│   ├── Mapping                     # AutoMapper profiles
│   ├── Services                    # бізнес-логіка застосунку
│   └── Exceptions                  # application exceptions
├── Threads.Domain
│   ├── Common                      # базові domain-моделі
│   ├── Entities                    # EF/domain entities
│   └── Enums                       # domain enums (UserRole, MediaType, VisibilityLevel)
├── Threads.Infrastructure
│   ├── Data
│   │   ├── Configurations          # EF Core і table configurations
│   │   └── Repositories            # реалізації repository interfaces
│   ├── Exceptions                  # технічні Infrastructure exceptions
│   ├── Migrations                  # EF Core migrations і model snapshot
│   ├── Security                    # JWT, password hashing, CORS і policies
│   └── Services                    # S3, Redis, email, GIF, location, ffmpeg та API configurators
├── deploy/nginx                    # nginx reverse proxy configuration
├── tests
│   ├── Threads.Application.UnitTests          # xUnit-тести application-рівня і domain enums
│   └── Threads.Infrastructure.IntegrationTests # repositories, security, transactions і API на PostgreSQL
├── .github/workflows               # GitHub Actions deploy на сервер
├── Dockerfile                      # образ API
├── docker-compose.yml              # API та Redis для локального запуску
└── BackEndForFinalProject.sln      # solution file
```

### Архітектурний потік

1. Запит приходить у контролер з `Threads.Api`.
2. Контролер виконує HTTP binding, дістає auth context і передає дані в application service.
3. Application service виконує бізнес-логіку, включно з ownership-перевірками для команд зміни й видалення ресурсів.
4. Репозиторії та зовнішні інтеграції працюють через `Threads.Infrastructure`.
5. Очікувані негативні результати повертаються через `null`, `bool`, status DTO або application exceptions залежно від сценарію.
6. Контролерні помилки перетворюються на `ProblemDetails` через спільний API helper, а винятки — через глобальний exception handler.
7. API повертає DTO або стандартизовану RFC 7807 помилку з `Content-Type: application/problem+json`.

## Технології

- `.NET 10`
- `ASP.NET Core Web API`
- `Entity Framework Core`
- `PostgreSQL`
- `Redis 7`
- `.NET HybridCache` (L1 memory + L2 Redis)
- `Npgsql`
- `JWT Bearer Authentication`
- `ASP.NET Core Rate Limiting Middleware`
- `OpenAPI` + `Swagger UI`
- `AWS S3`
- `ffmpeg` / `ffprobe`
- `Resend`
- `Giphy API`
- `Geoapify API`
- `Docker`
- `Nginx`
- `xUnit` + `NSubstitute`
- `Testcontainers` (PostgreSQL для integration tests)

## Запуск через Docker

Основний сценарій запуску цього проєкту: через Docker.

> `docker-compose.yml` підіймає API та Redis. PostgreSQL потрібно мати окремо: локально, в іншому compose-стеку або як зовнішню БД.

### 1. Підготуй `.env`

Створи в корені репозиторію файл `.env` і заповни мінімальні змінні:

> Не зберігай реальні паролі, API keys або connection strings у `appsettings*.json`. Для локальної розробки використовуй environment variables або .NET User Secrets, а випадково опубліковані credentials одразу відкликай і замінюй.

```env
ASPNETCORE_ENVIRONMENT=Production
ReverseProxy__KnownProxy=172.18.0.1

ConnectionStrings__DefaultConnection=Host=host.docker.internal;Port=5432;Database=threads_db;Username=postgres;Password=postgres

Jwt__Issuer=threads-api
Jwt__Audience=threads-client
Jwt__Key=your-very-long-secret-key
Jwt__AccessTokenLifetimeMinutes=60

AuthCodes__HashKey=PASTE_BASE64_OUTPUT_HERE

AWS__S3__Region=eu-central-1
AWS__S3__BucketName=your-bucket-name
AWS__S3__ReadUrlExpirationMinutes=60

MediaProcessing__FfmpegPath=ffmpeg
MediaProcessing__FfprobePath=ffprobe
MediaProcessing__VideoCompression__Preset=medium
MediaProcessing__VideoCompression__Crf=28
MediaProcessing__VideoCompression__AudioBitrateKbps=128
MediaProcessing__VideoCompression__MaxWidth=1280

RESEND_APITOKEN=your-resend-token
RESEND_FROM_EMAIL=no-reply@example.com
RESEND_FROM_NAME=Threads API

GIPHY_API_KEY=your-giphy-key
GIPHY_RATING=pg-13
GifApi__BaseURL=https://api.giphy.com/

GEOAPIFY_API_KEY=your-geoapify-key
LocationApi__BaseURL=https://api.geoapify.com/

REDIS_PASSWORD=your-strong-redis-password

Logging__SlowRequestThresholdMilliseconds=1500
```

`AuthCodes__HashKey` має бути Base64-encoded ключем щонайменше на `32` байти. Згенерувати його можна так:

```bash
openssl rand -base64 32
```

`ReverseProxy__KnownProxy` — адреса Docker gateway, з якої Nginx підключається до API-контейнера. Актуальне значення можна отримати після створення контейнера:

```bash
docker inspect threads-api \
  --format '{{range .NetworkSettings.Networks}}{{.Gateway}}{{end}}'
```

Для локального Development-запуску без Docker використовується `127.0.0.1` з `appsettings.Development.json`. У Docker застосунок працює в `Production`, тому адресу proxy отримує з `.env`.

### 2. Підійми контейнер

```bash
docker compose up --build -d
```

### 3. API буде доступне тут

```text
http://127.0.0.1:7001
```

`docker-compose.yml` мапить контейнерний порт `8080` на локальний `7001`.

API стартує після успішного healthcheck Redis. Для Redis автоматично формується connection string `redis:6379` із паролем із `REDIS_PASSWORD`.

## Конфігурація

### Обов'язково для базової роботи API

- `ConnectionStrings__DefaultConnection`
- `Jwt__Issuer`
- `Jwt__Audience`
- `Jwt__Key`
- `AuthCodes__HashKey` для verification/reset/change-password codes
- `Redis__ConnectionString` при запуску без `docker compose`

При запуску через `docker compose` замість ручного `Redis__ConnectionString` достатньо задати `REDIS_PASSWORD` у `.env`.

`ReverseProxy__KnownProxy` за замовчуванням дорівнює `127.0.0.1`. Для Docker + Nginx його потрібно перевизначити адресою Docker gateway, щоб forwarded headers і IP-based rate limiting працювали коректно.

### Обов'язково для медіа

- `AWS__S3__Region`
- `AWS__S3__BucketName`
- `AWS_ACCESS_KEY_ID`
- `AWS_SECRET_ACCESS_KEY`
- `AWS_SESSION_TOKEN` за потреби
- або IAM role / instance profile

### Обов'язково для email/password flows

- `RESEND_APITOKEN`
- `RESEND_FROM_EMAIL`

### Обов'язково для зовнішнього пошуку

- `GIPHY_API_KEY`
- `GEOAPIFY_API_KEY`

### Значення за замовчуванням

- `Jwt__AccessTokenLifetimeMinutes=60`
- `AWS__S3__ReadUrlExpirationMinutes=60`
- `MediaProcessing__FfmpegPath=ffmpeg`
- `MediaProcessing__FfprobePath=ffprobe`
- `MediaProcessing__VideoCompression__Preset=medium`
- `MediaProcessing__VideoCompression__Crf=28`
- `MediaProcessing__VideoCompression__AudioBitrateKbps=128`
- `MediaProcessing__VideoCompression__MaxWidth=1280`
- `GIPHY_RATING=pg-13`
- `GifApi__BaseURL=https://api.giphy.com/`
- `LocationApi__BaseURL=https://api.geoapify.com/`
- `ReverseProxy__KnownProxy=127.0.0.1`
- `Logging__SlowRequestThresholdMilliseconds=1500`

### CORS

Поточна policy `AllowAll` дозволяє будь-які origin, method і header. Секція `Cors:AllowedOrigins` є в `appsettings.json`, але зараз не використовується для обмеження policy, тому змінні `Cors__AllowedOrigins__*` не впливають на поведінку API.

### База даних

EF Core migrations і model snapshot зберігаються в `Threads.Infrastructure/Migrations`. Migration `20261008090458_AddAdministrationAndReports` додає moderation reports, індекси адмін-запитів і soft-delete поля для users/posts/comments.

API не застосовує migrations під час старту, і deploy workflow їх також не запускає. Застосувати актуальні migrations можна командою:

```bash
dotnet ef database update \
  --project Threads.Infrastructure \
  --startup-project Threads.Api
```

> ⚠️ Попередні migrations (`20260922094548_Initial`, `20261002104849_EnforceRepositoryDataIntegrity`) було замінено новою `Initial`. Для порожньої БД це не має значення. Якщо БД вже містить таблиці, створені старими migrations, `database update` спробує створити їх повторно й завершиться помилкою. Таку БД потрібно або перестворити, або відновити старі migrations і додати окрему migration для нових колонок.

## Документація API

OpenAPI і Swagger UI підключені для всіх середовищ:

- OpenAPI JSON: `/openapi/v1.json`
- Swagger UI: `/swagger`

Після запуску через `docker compose` вони доступні за адресами `http://127.0.0.1:7001/openapi/v1.json` і `http://127.0.0.1:7001/swagger`.

## Рольова авторизація

Оновлено: `2026-10-08`

- користувач має роль `User` або `Admin`; нові користувачі за замовчуванням отримують `User`
- роль зберігається в `Users.Role` як ціле число
- роль додається до access token через `ClaimTypes.Role`
- JWT Bearer використовує `ClaimTypes.Role` для перевірки ролі користувача
- policy `Admin` дозволяє доступ користувачам із роллю `Admin`
- усі `/api/admin/*` endpoints захищені атрибутом `[Authorize(Policy = AuthorizationPolicies.Admin)]`
- `GET /api/me` повертає контрактну роль `USER` або `ADMIN`
- blocked/deleted user не проходить login, refresh і перевірку вже виданого access token

## Обробка помилок

API використовує RFC 7807 `ProblemDetails` для контрольованих помилок контролерів, framework-відповідей і винятків application/infrastructure-рівня.

- `ProblemDetailsConfigurator` налаштовує `ProblemDetailsOptions` і автоматично додає `instance` з поточного request path;
- `GlobalExceptionHandler` зареєстрований через `AddExceptionHandler<GlobalExceptionHandler>()` і `UseExceptionHandler()`;
- `UseStatusCodePages()` формує `ProblemDetails` для порожніх framework-відповідей, зокрема `404 Not Found` і `405 Method Not Allowed`;
- JWT challenge/forbidden та rate limiter формують RFC 7807 відповіді для `401`, `403` і `429`;
- явні помилки контролерів формуються через `ControllerProblemDetailsExtensions.ProblemResponse()`.

Контролери документують HTTP-контракт атрибутами `ProducesResponseType`. Типи успішних відповідей вказані біля відповідних action, а повторювані `ProducesResponseType<ProblemDetails>` винесені на рівень контролера. Завдяки цьому OpenAPI явно показує DTO успішної відповіді та `ProblemDetails` для кожного задекларованого помилкового status code.

Усі помилкові відповіді, включно з admin/report endpoints, JWT `401/403`, model validation і rate limiting, повертають `Content-Type: application/problem+json` та єдиний контракт:

- `status` — фактичний HTTP status;
- `title` — стабільна категорія (`Invalid request`, `Unauthorized`, `Forbidden`, `Resource not found`, `Conflict`, `Internal server error`);
- `detail` — конкретне повідомлення;
- `instance` — поточний шлях запиту;
- `type` — необов'язковий URI типу проблеми; якщо поле відсутнє, за RFC 7807 використовується семантика `about:blank`.

| Виняток | HTTP status | Призначення |
|---|---:|---|
| `RequestValidationException` | `400 Bad Request` | Некоректні значення або порушення правил валідації запиту |
| `NotFoundException` | `404 Not Found` | Потрібний ресурс не знайдено |
| `ConflictException` | `409 Conflict` | Конфлікт із поточним станом або дублювання даних |
| `ForbiddenException` | `403 Forbidden` | Користувач не має права використовувати ресурс |
| `ExternalServiceException` | `502 Bad Gateway` | Giphy, Geoapify або інший зовнішній сервіс недоступний чи повернув некоректну відповідь |
| `InfrastructureConfigurationException` | `500 Internal Server Error` | Відсутнє обов'язкове Infrastructure-налаштування |
| `MediaProcessingException` | `500 Internal Server Error` | Помилка обробки зображення або відео |
| Інший `Exception` | `500 Internal Server Error` | Непередбачена внутрішня помилка |

Application exceptions розміщені в `Threads.Application/Exceptions`, а технічні винятки конфігурації — у `Threads.Infrastructure/Exceptions`. Giphy та Geoapify перетворюють мережеві помилки й некоректний JSON на `ExternalServiceException`, не передаючи клієнту внутрішні деталі інтеграції.

Винятки використовуються для переривання сценарію та бізнес-помилок команд. Зокрема, update/delete неіснуючого ресурсу спричиняє `NotFoundException`, а спроба змінити чужий пост, коментар або список followers — `ForbiddenException`. Результати на кшталт неправильних credentials, недійсного refresh token, простроченого verification code або повторної interaction залишаються `null`, `false` чи окремим status і обробляються контролером.

Наприклад, `GET /api/users/unknown-user/replies` повертає `404 Not Found` із `Content-Type: application/problem+json`:

```json
{
  "title": "Resource not found",
  "status": 404,
  "detail": "User was not found.",
  "instance": "/api/users/unknown-user/replies"
}
```

## Rate limiting

API використовує named policies з `Microsoft.AspNetCore.RateLimiting`. Політики реєструються через `RateLimiterConfigurator`, підключаються middleware `UseRateLimiter()` і призначаються endpoint-ам атрибутом `[EnableRateLimiting]`.

IP-based політики використовують `HttpContext.Connection.RemoteIpAddress`. Nginx передає адресу клієнта через `X-Forwarded-For`, а `ForwardedHeadersMiddleware` приймає forwarded headers лише від proxy, вказаного в `ReverseProxy__KnownProxy`.

| Policy | Алгоритм і ліміт | Partition | Застосування |
|---|---|---|---|
| `LoginPolicy` | fixed window: `5 / 1 хв` | IP | login |
| `RegisterPolicy` | fixed window: `3 / 15 хв` | IP | registration |
| `ForgotPasswordPolicy` | fixed window: `3 / 15 хв` | IP | запит reset code |
| `ResendVerificationPolicy` | fixed window: `5 / 10 хв` | IP | повторна відправка verification code |
| `VerificationPolicy` | fixed window: `5 / 10 хв` | IP | verify email, verify reset code і reset password |
| `RefreshPolicy` | fixed window: `20 / 1 хв` | IP | refresh token |
| `ChangePasswordStartPolicy` | fixed window: `3 / 15 хв` | user ID | початок зміни пароля |
| `ChangePasswordConfirmPolicy` | fixed window: `5 / 10 хв` | user ID | підтвердження зміни пароля |
| `PostCreationPolicy` | token bucket: burst `5`, `+1 / 30 с` | user ID | створення постів |
| `CommentCreationPolicy` | token bucket: burst `15`, `+5 / 30 с` | user ID | створення коментарів |
| `InteractionPolicy` | token bucket: burst `60`, `+30 / 30 с` | user ID | views, likes, bookmarks, reposts, follows і poll votes |
| `MediaUploadPolicy` | token bucket: burst `5`, `+1 / 1 хв` | user ID | upload медіа |
| `EditHistoryPolicy` | token bucket: burst `20`, `+5 / 10 с` | user ID або IP | edit history постів і коментарів |
| `ExternalSearchPolicy` | token bucket: burst `10`, `+5 / 10 с` | IP | Giphy, Geoapify і link preview resolve |

Endpoint-и з однаковою named policy використовують спільний bucket у межах одного partition key. Наприклад, усі interactions одного користувача витрачають спільні токени `InteractionPolicy`, а GIF і location search з однієї IP використовують спільні токени `ExternalSearchPolicy`.

При перевищенні ліміту API повертає `429 Too Many Requests`:

```json
{
  "title": "Too many requests",
  "status": 429,
  "detail": "Too many requests. Please try again later.",
  "instance": "/api/posts"
}
```

Якщо limiter надає час відновлення, відповідь також містить HTTP-заголовок `Retry-After` із кількістю секунд до наступної дозволеної спроби.

Поточні limiter-и зберігають стан у пам'яті API-процесу. Це відповідає поточному deployment з одним `api` container. При горизонтальному масштабуванні rate limiting потрібно перенести на reverse proxy/API gateway або реалізувати спільні атомарні лічильники в Redis.

## Огляд API

Базовий префікс: `api/`

У колонці `Auth` значення `Так` означає, що endpoint вимагає Bearer access token. Публічні endpoint-и можуть використовувати переданий token для формування персоналізованих полів відповіді.

Усі часові точки в request/response DTO використовують `DateTimeOffset` і передаються у форматі ISO 8601 з UTC або явним offset, наприклад `2026-10-01T12:00:00Z` чи `2026-10-01T15:00:00+03:00`. Це стосується `CreatedAt`, `UpdatedAt`, `ActionAt`, `EndsAt` і `AccessTokenExpiresAt`. Календарна дата народження залишається `DateOnly` у форматі `YYYY-MM-DD`.

### Auth

| Method | Route | Auth | Призначення |
|---|---|---|---|
| `POST` | `/api/auth/register` | Ні | Створити pending registration і надіслати verification code |
| `POST` | `/api/auth/login` | Ні | Увійти й отримати access та refresh tokens |
| `POST` | `/api/auth/refresh` | Ні | Оновити пару tokens за refresh token |
| `POST` | `/api/auth/logout` | Ні | Відкликати refresh token |
| `POST` | `/api/auth/forgot-password` | Ні | Надіслати password reset code |
| `POST` | `/api/auth/verify-reset-code` | Ні | Перевірити password reset code |
| `POST` | `/api/auth/reset-password` | Ні | Скинути пароль за підтвердженим code |
| `POST` | `/api/auth/verify-email` | Ні | Підтвердити email і створити користувача |
| `POST` | `/api/auth/resend-verification-code` | Ні | Повторно надіслати email verification code |

### Users

Публічні профілі та їхні колекції доступні за `id` або `username`.

| Method | Route | Auth | Призначення |
|---|---|---|---|
| `GET` | `/api/users/by-id/{id}` | Ні | Отримати профіль за `Guid` |
| `GET` | `/api/users/by-username/{username}` | Ні | Отримати профіль за username |
| `GET` | `/api/users/{username}/posts?limit=20&cursor=...` | Ні | Отримати сторінку постів користувача |
| `GET` | `/api/users/{username}/replies?limit=20&cursor=...` | Ні | Отримати сторінку коментарів користувача |
| `GET` | `/api/users/{username}/likes?limit=20&cursor=...` | Ні | Отримати сторінку лайкнутих користувачем постів та коментарів |
| `GET` | `/api/users/{username}/reposts?limit=20&cursor=...` | Ні | Отримати сторінку reposts постів і коментарів користувача |

### Me

Основні операції з авторизованим користувачем згруповані під `/api/me` і потребують Bearer access token. Canonical-маршрут зміни пароля також знаходиться тут.

| Method | Route | Auth | Призначення |
|---|---|---|---|
| `GET` | `/api/me` | Так | Отримати профіль поточного користувача |
| `PUT` | `/api/me` | Так | Оновити профіль, avatar і banner через `multipart/form-data` |
| `DELETE` | `/api/me` | Так | Видалити акаунт поточного користувача |
| `GET` | `/api/me/posts?limit=20&cursor=...` | Так | Отримати сторінку власних постів поточного користувача |
| `GET` | `/api/me/likes?limit=20&cursor=...` | Так | Отримати сторінку лайкнутих постів та коментарів поточного користувача |
| `GET` | `/api/me/bookmarks?limit=20&cursor=...` | Так | Отримати сторінку збережених постів та коментарів поточного користувача |
| `GET` | `/api/me/reposts?limit=20&cursor=...` | Так | Отримати сторінку репостів постів і коментарів поточного користувача |
| `POST` | `/api/me/change-password/start` | Так | Перевірити поточний пароль, зберегти pending password hash і надіслати email code |
| `POST` | `/api/me/change-password/confirm` | Так | Підтвердити зміну пароля шестизначним email code |

Зміна пароля виконується у два етапи. Спочатку клієнт надсилає поточний і новий пароль:

```json
{
  "currentPassword": "current-password",
  "newPassword": "new-password"
}
```

`POST /api/me/change-password/start` перевіряє активність користувача, правильність поточного пароля і те, що новий пароль відрізняється від поточного. Після цього API зберігає pending password hash та надсилає шестизначний код на email користувача.

Для завершення зміни клієнт надсилає отриманий код:

```json
{
  "code": "123456"
}
```

`POST /api/me/change-password/confirm` перевіряє наявність pending-зміни та строк дії коду, застосовує новий password hash і відкликає всі refresh tokens користувача. Обидва endpoint-и потребують Bearer access token.

#### Оновлення профілю

`PUT /api/me` приймає `multipart/form-data`. Усі поля необов'язкові: не передане поле не змінюється.

| Поле | Тип | Опис |
|---|---|---|
| `DisplayName` | `string` | `1–100` символів |
| `Bio` | `string` | `1–500` символів |
| `Location` | `LocationRequest` | `id`, `name`, `country`, `latitude`, `longitude` |
| `RemoveLocation` | `bool` | Очистити локацію |
| `BirthDate` | `YYYY-MM-DD` | Дата народження, не може бути в майбутньому |
| `BirthDateVisibility` | `VisibilityLevel` | Хто бачить дату народження |
| `BirthYearVisibility` | `VisibilityLevel` | Хто бачить рік народження |
| `RemoveBirthDate` | `bool` | Очистити дату народження |
| `Avatar` / `Banner` | `File` | Зображення до `10 MB` |
| `RemoveAvatar` / `RemoveBanner` | `bool` | Видалити avatar / banner |

#### Видимість дати народження

`VisibilityLevel` передається і повертається рядком у snake_case:

| Значення | Хто бачить |
|---|---|
| `public` | усі, включно з анонімними запитами |
| `followers` | користувачі, підписані на власника профілю |
| `following` | користувачі, на яких підписаний власник профілю |
| `mutual` | лише взаємні підписки |
| `only_me` | лише власник (значення за замовчуванням) |

Інші значення, наприклад `OnlyMe` або `4`, повертають `400 Bad Request`. JSON-назви задаються атрибутами `[JsonStringEnumMemberName]` у `VisibilityLevel`, а `VisibilityLevelConverter` використовує ті самі назви для `multipart/form-data`.

Правила перевіряє `BirthDateVisibilityPolicy` в `UserQueryService`:

- власник профілю завжди отримує свою `birthDate`;
- інші користувачі отримують `birthDate` лише тоді, коли їм доступні **і** дата (`birthDateVisibility`), **і** рік (`birthYearVisibility`); інакше `birthDate` дорівнює `null`;
- `birthDateVisibility` і `birthYearVisibility` повертаються завжди, щоб клієнт міг показати, що дата прихована;
- зворотний follow (`owner → viewer`) запитується з БД лише тоді, коли дата існує і хоча б одне з налаштувань дорівнює `following` або `mutual`.

> Формат часткової дати (наприклад, день і місяць без року) ще не узгоджено з frontend, тому зараз прихований рік ховає всю дату.

Приклад відповіді:

```json
{
  "username": "mock_user",
  "bio": "Profile bio",
  "birthDate": "2000-01-01",
  "birthDateVisibility": "followers",
  "birthYearVisibility": "only_me"
}
```

`UserShortResponse` (автор поста чи коментаря, результати пошуку) також містить `bio`.

`UserResponse` використовується і для публічного профілю, і для `/api/me`. Поле `email` є nullable: у відповідях `/api/users/...` воно завжди дорівнює `null`, а `GET /api/me` і успішний `PUT /api/me` повертають email поточного користувача. Приватний профіль завантажується окремо від кешованого публічного профілю, щоб email не потрапляв у public profile cache.

### Reports і administration

Авторизований користувач може створити скаргу з причиною `spam`, `harassment`, `misinformation`, `violence`, `hate` або `other`:

| Method | Route | Auth | Призначення |
|---|---|---|---|
| `POST` | `/api/posts/{id}/report` | Так | Поскаржитися на пост |
| `POST` | `/api/comments/{id}/report` | Так | Поскаржитися на коментар |
| `POST` | `/api/users/{id}/report` | Так | Поскаржитися на користувача |

Адміністративні маршрути потребують ролі `ADMIN`:

| Method | Route | Призначення |
|---|---|---|
| `GET` | `/api/admin/users` | Пошук, фільтрація, сортування та page pagination користувачів |
| `GET` | `/api/admin/users/{id}` | Деталі користувача з `isBlocked` |
| `PUT` | `/api/admin/users/{id}/block` | Ідемпотентно заблокувати користувача |
| `PUT` | `/api/admin/users/{id}/unblock` | Ідемпотентно розблокувати користувача |
| `DELETE` | `/api/admin/users/{id}/delete` | Soft-delete користувача |
| `GET` | `/api/admin/moderation` | Черга скарг із фільтрами та пошуком |
| `GET` | `/api/admin/moderation/{id}` | Деталі скарги та її target |
| `PUT` | `/api/admin/moderation/{id}/status` | Атомарно застосувати `kept`, `deleted` або `blocked` |
| `GET` | `/api/admin/dashboard/metrics` | Поточні метрики |
| `GET` | `/api/admin/dashboard/charts?period=7d` | UTC-серії audience/activity за 7 і 30 днів |
| `GET` | `/api/admin/dashboard/tables?limit=5` | Останні користувачі та згруповані скарги |

#### Cursor pagination

Для paginated endpoints використовується `CursorPageRequest`:

- `limit` — розмір сторінки від `1` до `50`, значення за замовчуванням — `20`;
- `cursor` — необов'язковий opaque cursor із попередньої відповіді;
- для першої сторінки `cursor` не передається;
- некоректний cursor перетворюється на `400 Bad Request`.

Звичайний формат `CursorPageResponse<T>` використовують пости профілю, коментарі поста, followers/following і пошук users/posts:

```json
{
  "items": [],
  "nextCursor": "opaque-cursor",
  "hasMore": true
}
```

`nextCursor` повертається лише тоді, коли `hasMore` дорівнює `true`. Для наступної сторінки його потрібно передати в query-параметрі `cursor`.

#### Формат interaction collections

Публічні profile collections `/api/users/{username}/likes` і `/api/users/{username}/reposts`, а також `/api/me/likes`, `/api/me/bookmarks` і `/api/me/reposts` повертають один об'єкт із двома типізованими колекціями:

```json
{
  "posts": [
    {
      "id": "00000000-0000-0000-0000-000000000000",
      "actionAt": "2026-09-09T12:00:00+00:00"
    }
  ],
  "comments": [
    {
      "id": "00000000-0000-0000-0000-000000000000",
      "actionAt": "2026-09-09T11:30:00+00:00"
    }
  ],
  "nextCursor": "opaque-cursor",
  "hasMore": true
}
```

- `posts` містить об'єкти `PostResponse`;
- `comments` містить об'єкти `CommentResponse`;
- `actionAt` містить UTC-час створення відповідного `Like`, `Bookmark` або `Repost`; у звичайних content endpoints це поле дорівнює `null`;
- `/likes` використовує `UserLikesPageResponse`;
- `/bookmarks` використовує `UserBookmarksPageResponse`;
- `/reposts` використовує `UserRepostsPageResponse`;
- одна сторінка містить сумарно не більше `limit` елементів у `posts` і `comments`;
- сторінка формується за спільним сортуванням `actionAt + id` від новіших взаємодій до старіших, після чого елементи розділяються на два масиви;
- клієнт може об'єднати `posts` і `comments` та відновити спільний порядок сортування за `actionAt`, а при однаковому часі — за `id`, у спадному порядку;
- публічні endpoints не вимагають авторизації, але за наявності Bearer token персоналізовані поля формуються відносно поточного viewer-а;
- bookmarks доступні лише власнику через `/api/me/bookmarks`.

Цей формат використовують `/api/users/{username}/likes`, `/api/users/{username}/reposts`, `/api/me/likes`, `/api/me/bookmarks` і `/api/me/reposts`.

### Posts

| Method | Route | Auth | Призначення |
|---|---|---|---|
| `GET` | `/api/posts/feed` | Ні | Отримати публічну стрічку постів |
| `GET` | `/api/posts/scheduled` | Так | Отримати власні заплановані пости |
| `POST` | `/api/posts/scheduled` | Так | Створити запланований пост |
| `PATCH` | `/api/posts/scheduled/{id}` | Так | Частково оновити власний запланований пост |
| `DELETE` | `/api/posts/scheduled/{id}` | Так | Видалити власний запланований пост |
| `GET` | `/api/posts/{id}` | Ні | Отримати пост за `Guid` |
| `GET` | `/api/posts/{id}/edit-history?limit=20&cursor=...` | Ні | Отримати версії редагування поста |
| `POST` | `/api/posts/{id}/view` | Так | Зареєструвати перегляд поста |
| `POST` | `/api/posts/{id}/like` | Так | Поставити лайк посту |
| `DELETE` | `/api/posts/{id}/like` | Так | Прибрати лайк із поста |
| `POST` | `/api/posts/{id}/repost` | Так | Зробити repost поста |
| `DELETE` | `/api/posts/{id}/repost` | Так | Скасувати repost поста |
| `POST` | `/api/posts/{id}/bookmark` | Так | Додати пост у bookmarks |
| `DELETE` | `/api/posts/{id}/bookmark` | Так | Прибрати пост із bookmarks |
| `POST` | `/api/posts/{id}/poll/vote` | Так | Проголосувати в poll поста |
| `POST` | `/api/posts` | Так | Створити пост |
| `PUT` | `/api/posts/{id}` | Так | Оновити власний пост |
| `DELETE` | `/api/posts/{id}` | Так | Видалити власний пост |

Scheduled post підтримує текст, media attachments і link preview. `ScheduledAt` має бути в майбутньому; background worker кожні `30 секунд` публікує до `20` готових записів за один цикл, переносить media до створеного поста та створює його початкову snapshot-версію.

Звичайний post також може містити quote на post або comment із фіксацією конкретного `QuotedTargetVersionId`. Edit history повертає immutable snapshots, тому старі версії не змінюються разом із поточним контентом.

Повторний конкурентний vote не створює дублікат: `PollRepository` перехоплює лише PostgreSQL `UniqueViolation` для constraint `IX_PollVotes_PollId_UserId` і повертає сервісу `false`. Інші помилки БД не маскуються як повторне голосування.

### Comments

| Method | Route | Auth | Призначення |
|---|---|---|---|
| `GET` | `/api/comments/post/{postId}?limit=20&cursor=...` | Ні | Отримати сторінку коментарів поста |
| `GET` | `/api/comments/{id}` | Ні | Отримати коментар за `Guid` |
| `GET` | `/api/comments/{id}/thread?limit=20&cursor=...` | Ні | Отримати пост, ancestors, target і сторінку прямих відповідей |
| `GET` | `/api/comments/{id}/edit-history?limit=20&cursor=...` | Ні | Отримати версії редагування коментаря |
| `POST` | `/api/comments` | Так | Створити коментар або відповідь |
| `PUT` | `/api/comments/{id}` | Так | Оновити власний коментар |
| `DELETE` | `/api/comments/{id}` | Так | Видалити власний коментар |
| `POST` | `/api/comments/{id}/poll/vote` | Так | Проголосувати в poll коментаря |
| `POST` | `/api/comments/{id}/view` | Так | Зареєструвати перегляд коментаря |
| `POST` | `/api/comments/{id}/like` | Так | Поставити лайк коментарю |
| `DELETE` | `/api/comments/{id}/like` | Так | Прибрати лайк із коментаря |
| `POST` | `/api/comments/{id}/repost` | Так | Зробити repost коментаря |
| `DELETE` | `/api/comments/{id}/repost` | Так | Скасувати repost коментаря |
| `POST` | `/api/comments/{id}/bookmark` | Так | Додати коментар у bookmarks |
| `DELETE` | `/api/comments/{id}/bookmark` | Так | Прибрати коментар із bookmarks |

`POST /api/comments` підтримує `mediaIds`, `poll`, `location` і `linkPreview`. Для update поле `content` залишається обов'язковим, `mediaIds` замінює набір вкладень лише коли передане, а metadata керується явними командами:

```json
{
  "content": "Updated comment",
  "removePoll": true,
  "removeLocation": true,
  "removeLinkPreview": true
}
```

Щоб установити metadata, клієнт передає відповідно `poll`, `location` або `linkPreview`. Одночасна передача значення і відповідного `remove...: true` повертає `400 Invalid request`. Відсутнє поле не змінює поточне значення.

### Логіка CommentService

`CommentService` знаходиться у `Threads.Application/Services/Comments/CommentService.cs` і є фасадом над `CommentQueryService`, `CommentManagementService`, `CommentInteractionService`, `CommentVersionService` та `CommentThreadService`. Читання, команди, interactions, edit history і побудова thread context розділені між цими сервісами, а `CommentResponseFactory` разом із `UserResponseFactory` формують response DTO та read URL для медіа й аватара.

Усі публічні методи асинхронні та приймають `CancellationToken`, щоб запит до БД можна було скасувати, якщо клієнт розірвав HTTP-з'єднання або застосунок завершує роботу.

#### Отримання коментарів

| Метод | Що робить |
|---|---|
| `GetByPostIdAsync(postId, pagination, cancellationToken, currentUserId)` | Повертає `CursorPageResponse<CommentResponse>` із максимум `pagination.limit` коментарів. Результат сортується за `CreatedAt + Id` від старих коментарів до нових і містить як кореневі коментарі, так і відповіді з `ParentCommentId`. |
| `GetByAuthorIdAsync(authorId, pagination, cancellationToken, currentUserId)` | Повертає cursor-сторінку коментарів користувача для `/api/users/{username}/replies`. |
| `GetByIdAsync(id, cancellationToken, currentUserId)` | Повертає один коментар за `Guid`. Якщо коментар не існує, повертає `null`, який контролер перетворює на `404 Not Found`. |
| `GetThreadAsync(id, pagination, cancellationToken, currentUserId)` | Повертає пост, ланцюжок ancestors, target comment і cursor-сторінку його прямих відповідей. |
| `GetEditHistoryAsync(id, pagination, cancellationToken, currentUserId)` | Повертає snapshot-версії коментаря з pagination за `CreatedAt + Id`. |
| `GetLikedByUserIdAsync(userId, limit, cursor, cancellationToken, currentUserId)` | Повертає до `limit + 1` кандидатів-коментарів для спільної сторінки likes. У `ActionAt` записується час створення лайка. |
| `GetBookmarkedByUserIdAsync(userId, limit, cursor, cancellationToken, currentUserId)` | Повертає до `limit + 1` кандидатів-коментарів для спільної сторінки bookmarks. У `ActionAt` записується час створення bookmark. |
| `GetRepostedByUserIdAsync(userId, limit, cursor, cancellationToken, currentUserId)` | Повертає до `limit + 1` кандидатів-коментарів для спільної сторінки reposts. У `ActionAt` записується час створення repost. |

У collection-методах `userId` визначає власника колекції, `limit` і `cursor` — межі вибірки, а `currentUserId` — viewer-а для персональних полів `IsLikedByCurrentUser`, `IsBookmarkedByCurrentUser` та `IsRepostedByCurrentUser`. Фінальну спільну сторінку posts/comments формують відповідно `LikeService`, `BookmarkService` або `RepostService`.

#### Створення, оновлення і видалення

##### `CreateAsync`

Метод створює звичайний коментар або відповідь на інший коментар:

1. Відхиляє порожній текст або рядок лише з пробілів через `RequestValidationException`.
2. Перевіряє існування поста з `request.PostId`.
3. Якщо переданий `ParentCommentId`, перевіряє існування батьківського коментаря та належність до того самого поста.
4. Створює `Comment`, нормалізує текст, location, poll і link preview та прив'язує до `20` попередньо завантажених media items.
5. Створює початкову immutable snapshot-версію коментаря.
6. Зберігає entity та повторно завантажує її з навігаційними властивостями для повної API-відповіді.

База обмежує довжину `Content` до `1000` символів. Сервіс окремо перевіряє тільки те, що текст не порожній.

##### `UpdateAsync`

Метод знаходить коментар, викидає `NotFoundException`, якщо його немає, і `ForbiddenException`, якщо `currentUserId` не збігається з `AuthorId`. Після ownership-перевірки він оновлює текст, а за наявності відповідних полів — media, poll, location і link preview. `RemovePoll`, `RemoveLocation` та `RemoveLinkPreview` явно очищають metadata; передача одночасно значення і remove-прапорця відхиляється через `RequestValidationException`. Кожне успішне оновлення створює нову snapshot-версію та змінює `CurrentVersionId` і `UpdatedAt`. Автор, пост і `ParentCommentId` не змінюються.

##### `DeleteAsync`

Метод викидає `NotFoundException`, якщо коментар не знайдений, і `ForbiddenException`, якщо операцію виконує не автор. Після перевірки `CommentManagementService` видаляє коментар; через cascade delete разом із ним видаляються replies, poll, versions, likes, bookmarks, reposts і views. Очищення media objects у S3 виконується best-effort після успішної DB-операції.

#### Likes, bookmarks і reposts

Методи `AddCommentLikeAsync` / `RemoveCommentLikeAsync`, `AddCommentBookmarkAsync` / `RemoveCommentBookmarkAsync` і `AddCommentRepostAsync` / `RemoveCommentRepostAsync` розміщені відповідно в `LikeService`, `BookmarkService` та `RepostService` і працюють за однаковим принципом:

1. Add-метод перевіряє існування коментаря та повертає `false`, якщо target відсутній.
2. Add-метод викликає repository `TryAddAsync`; унікальний ключ не дозволяє створити дублікат.
3. Remove-метод виконує атомарний `ExecuteDeleteAsync` за парою `userId + commentId` і повертає результат за кількістю змінених рядків.
4. Контролер читає коментар до та після команди, щоб повернути коректний HTTP status, актуальні counters і персональні flags.

Повторний like/bookmark/repost не створює логічний дублікат. Коментарі використовують окремі сутності `CommentLike`, `CommentBookmark` і `CommentRepost` зі складеним primary key `CommentId + UserId`. Якщо два однакові add-запити виконуються одночасно, repository перехоплює лише PostgreSQL `UniqueViolation` відповідного primary key, від'єднує невдалу entity від `DbContext` і повертає `false`; інші DB-помилки поширюються далі.

#### `RecordViewAsync`

Метод реєструє унікальний перегляд коментаря авторизованим користувачем через атомарний `INSERT ... ON CONFLICT DO NOTHING`, після чого отримує актуальний `ViewsCount` і формує `CommentViewResponse`. Один користувач збільшує лічильник конкретного коментаря лише один раз завдяки складеному primary key `CommentId + UserId`.

#### Формування CommentResponse

`CommentResponseFactory` формує фінальний `CommentResponse` із такими даними:

- основні поля: `Id`, `VersionId`, `PostId`, `ParentCommentId`, `Content`, `CreatedAt`, `UpdatedAt`;
- автор: `Id`, `Username`, `DisplayName`, `Bio`, `Location`, `AvatarUrl`, `IsVerified`;
- розширений контент: `Attachments`, `Poll`, `Location`, `LinkPreview`;
- counters: `LikesCount`, `RepliesCount`, `RepostsCount`, `ViewsCount`;
- viewer state: `IsLikedByCurrentUser`, `IsBookmarkedByCurrentUser`, `IsRepostedByCurrentUser`;
- `ActionAt`: час like/bookmark/repost у відповідній interaction collection або `null` у звичайних content endpoints.

URL аватара не зберігається безпосередньо в DTO: якщо в користувача є `AvatarObjectKey`, `UserResponseFactory` формує read URL через `IObjectStorageService`. Якщо локація автора не задана, `Location` дорівнює `null`.

#### Відповідність методів HTTP endpoints

| Service method | HTTP endpoint |
|---|---|
| `GetByPostIdAsync` | `GET /api/comments/post/{postId}?limit=20&cursor=...` |
| `GetByAuthorIdAsync` | `GET /api/users/{username}/replies?limit=20&cursor=...` |
| `GetByIdAsync` | `GET /api/comments/{id}` |
| `GetThreadAsync` | `GET /api/comments/{id}/thread?limit=20&cursor=...` |
| `GetEditHistoryAsync` | `GET /api/comments/{id}/edit-history?limit=20&cursor=...` |
| `CreateAsync` | `POST /api/comments` |
| `UpdateAsync` | `PUT /api/comments/{id}` |
| `DeleteAsync` | `DELETE /api/comments/{id}` |
| `RecordViewAsync` | `POST /api/comments/{id}/view` |
| `VoteCommentAsync` через `PollService` | `POST /api/comments/{id}/poll/vote` |
| `AddCommentLikeAsync` / `RemoveCommentLikeAsync` | `POST` / `DELETE /api/comments/{id}/like` |
| `AddCommentBookmarkAsync` / `RemoveCommentBookmarkAsync` | `POST` / `DELETE /api/comments/{id}/bookmark` |
| `AddCommentRepostAsync` / `RemoveCommentRepostAsync` | `POST` / `DELETE /api/comments/{id}/repost` |
| `GetLikedByUserIdAsync` через `LikeService` | `GET /api/users/{username}/likes`, `GET /api/me/likes` |
| `GetBookmarkedByUserIdAsync` через `BookmarkService` | `GET /api/me/bookmarks` |
| `GetRepostedByUserIdAsync` через `RepostService` | `GET /api/users/{username}/reposts`, `GET /api/me/reposts` |

### Follows

| Method | Route | Auth | Призначення |
|---|---|---|---|
| `POST` | `/api/follows/{userId}` | Так | Підписатися на користувача |
| `DELETE` | `/api/follows/{userId}` | Так | Відписатися від користувача |
| `GET` | `/api/follows/{userId}/followers?limit=20&cursor=...` | Ні | Отримати сторінку followers користувача |
| `GET` | `/api/follows/{userId}/following?limit=20&cursor=...` | Ні | Отримати сторінку користувачів, на яких оформлена підписка |
| `DELETE` | `/api/follows/{userId}/followers/{followId}` | Так | Видалити follower зі свого профілю |

`RemoveFollowerAsync` перевіряє ownership у `FollowService`: спроба змінити чужий список followers спричиняє `ForbiddenException`, а відсутній user, follower або follow — `NotFoundException`. Конкурентне створення однакової підписки обробляється в `FollowRepository.TryAddAsync`: repository повертає `false` лише для `UniqueViolation` constraint `IX_Follows_FollowerId_FollowingId`, а інші помилки БД не приховує.

### Search

| Method | Route | Auth | Призначення |
|---|---|---|---|
| `GET` | `/api/search/users?q=...&limit=20&cursor=...` | Ні | Знайти сторінку користувачів |
| `GET` | `/api/search/posts?q=...&limit=20&cursor=...` | Ні | Знайти сторінку постів |
| `GET` | `/api/search/gifs?q=...` | Ні | Знайти GIF через Giphy |
| `GET` | `/api/search/locations?q=...` | Ні | Знайти локації через Geoapify |

Search users сортується за `Username + Id` у зростаючому порядку, а search posts — за `CreatedAt + Id` у спадному. Для наступної сторінки потрібно повторити той самий `q` і передати `nextCursor` як `cursor`.

User search додатково підтримує `people=following` і `location=near`. Post search підтримує ці самі filters, а також `exactPhrase`, `anyWords`, `excludeWords`, `from`, `minReplies`, `minLikes`, `minReposts`, `fromDate`, `toDate` і `hasMedia`. Невідомі значення enum-like filters і некоректний date range повертають `400 Invalid request`.

### Link previews

| Method | Route | Auth | Призначення |
|---|---|---|---|
| `POST` | `/api/linkpreviews/resolve` | Ні | Безпечно отримати title, image і domain для HTTP/HTTPS URL |

Link preview resolver блокує loopback, private/link-local адреси та redirect-и на приватні hosts, дозволяє лише HTTP/HTTPS без user info, обмежує redirects і обсяг прочитаного HTML. Endpoint використовує спільну `ExternalSearchPolicy`.

### Media

| Method | Route | Auth | Призначення |
|---|---|---|---|
| `GET` | `/api/media/{id}` | Умовно | Отримати presigned URL прикріпленого медіа; неприкріплене доступне лише uploader-у |
| `POST` | `/api/media/upload` | Так | Завантажити файл через `multipart/form-data` |

## Кешування

API використовує `HybridCache` з memory L1 і Redis L2. Redis-ключі мають instance prefix `threads:`.

| Дані | L1 | L2 | Ключ |
|---|---:|---:|---|
| GIF search | `5 хв` | `30 хв` | `giphy:search:{rating}:{query}` |
| Location search | `5 хв` | `30 хв` | `geoapify:search:{query}` |
| Публічний профіль за ID | вимкнено | `5 хв` | `users:profile:v1:{userId}` |
| Username → user ID | вимкнено | `30 хв` | `users:username:v1:{username}` |
| Основний вміст поста за ID | вимкнено | `10 хв` | `posts:core:v1:{postId}` |

Engagement поста не кешується: counters і viewer-specific state читаються окремо. `UpdatedAt` використовується для виявлення застарілого cached content і його оновлення.

Після змін профілю, follow-зв'язків, постів або видалення користувача відповідні entries інвалідуються. Cache update/invalidation є best-effort операцією з timeout `2 секунди`: збій кешу логується, але не скасовує успішну основну операцію.

Загальні значення `HybridCache` для entries без власних options: L1 — `1 хвилина`, L2 — `5 хвилин`.

## Логування

Проєкт використовує структуроване `ILogger`-логування без окремого logging provider. Базові рівні задаються в `Threads.Api/appsettings.json`: application logs — `Information`, `Microsoft.AspNetCore`, EF Core і HTTP clients — `Warning`.

- `SlowRequestLoggingMiddleware` записує `Warning` для запитів, довших за `Logging:SlowRequestThresholdMilliseconds`; значення за замовчуванням — `1500 ms`.
- Глобальний exception handler додає до логів endpoint, status code, `TraceId` і user ID; client cancellation логується як `Debug` без `ProblemDetails` response.
- JWT authentication failures, forbidden responses і rate-limit rejections логуються окремими security categories.
- Auth flows логують значущі події без паролів, access/refresh tokens і verification codes.
- Resend, Giphy, Geoapify, S3 та ffmpeg логують тривалість успішних операцій і деталі технічних збоїв.
- Невдалі best-effort cache invalidation та cleanup тимчасових/S3-файлів логуються як `Warning`.

## Обробка медіа

- зображення проходять валідацію і завантажуються в S3 без перекодування
- відео стискаються у `MP4` перед upload
- метадані для відео знімаються вже з обробленого файлу
- для відео генерується thumbnail
- ліміт upload у застосунку: `100 MB`
- у `nginx` зараз дозволено `512M`, що не конфліктує з API-лімітом

## Тестування

Solution містить два тестові проєкти на `xUnit`:

| Проєкт | Що покриває | Залежності |
|---|---|---|
| `tests/Threads.Application.UnitTests` | application services (auth, posts, scheduled posts, comments, versions, interactions, follows, media, link previews, users), cursor/cache helpers, mapping, валідацію DTO та domain policies | `NSubstitute`, без зовнішніх сервісів |
| `tests/Threads.Infrastructure.IntegrationTests` | repositories, JWT/password/auth-code security, auth transactions, link preview HTTP behavior і API через `WebApplicationFactory` (`Auth`, `Profile`, `Search`, `CommentThread`, `UserReplies`) | `Testcontainers` з `postgres:17-alpine` |

Integration tests самі запускають PostgreSQL-контейнер і застосовують migrations, тому для них потрібен запущений Docker.

Запуск усіх тестів:

```bash
dotnet test BackEndForFinalProject.sln
```

Лише unit tests (без Docker):

```bash
dotnet test tests/Threads.Application.UnitTests
```

## Розгортання

- Docker image вже містить `ffmpeg`
- API слухає `8080` всередині контейнера
- `docker-compose.yml` публікує його на `127.0.0.1:7001`
- Redis працює в окремому контейнері `threads-redis`, захищений паролем і має healthcheck
- API залежить від успішного Redis healthcheck
- конфіг `deploy/nginx/threads.conf` проксіює трафік на `127.0.0.1:7001`
- Nginx передає `X-Forwarded-For` і `X-Forwarded-Proto`; API довіряє лише proxy з `ReverseProxy__KnownProxy`
- forwarded headers обробляються до authentication та rate limiting, тому IP-based policies використовують адресу клієнта, а не Docker gateway
- GitHub Actions workflow `.github/workflows/build-deploy.yaml` запускається на push у `main` (або вручну): підключається до сервера по SSH, робить `git pull`, оновлює nginx, записує `.env` із secret `APP_ENV_FILE` і перезбирає контейнери через `docker compose up -d --build`
- workflow не запускає тести й не застосовує EF Core migrations: перед push у `main` потрібно локально виконати `dotnet test`, а migrations застосувати окремо

## Примітки

- README описує фактичні контролери, маршрути й конфігурацію, які є в коді зараз.
- `Like`, `Bookmark`, `Repost` і `View` розділені на окремі сутності для дописів і коментарів.
- Зміни від `2026-09-22`: додано Swagger UI, unit test project, структуроване логування та best-effort cache/file cleanup; ownership-перевірки update/delete залишаються в application services, а часові поля DTO використовують `DateTimeOffset`.
- Зміни від `2026-10-02`: додано integration tests на Testcontainers, `bio` у `UserShortResponse`, налаштування видимості дати народження; у контракті API `DateOfBirth` → `birthDate`, `RemoveDateOfBirth` → `RemoveBirthDate` (у БД і entity поле досі називається `DateOfBirth`); migrations зведено в одну `Initial`.
- Зміни від `2026-10-08`: додано scheduled posts, content versions/edit history, comment threads і user replies, advanced search filters, quotes, link previews, media/poll/location для comments та уніфікований RFC 7807 error contract. Очищення comment metadata виконується явними `RemovePoll`, `RemoveLocation` і `RemoveLinkPreview`.
