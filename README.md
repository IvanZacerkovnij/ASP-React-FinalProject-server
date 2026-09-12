# Threads API Backend

Backend для соціального застосунку у стилі Threads, побудований на `ASP.NET Core Web API` з `PostgreSQL`, `EF Core`, `JWT`, `AWS S3` і обробкою медіа через `ffmpeg`.

> Останнє оновлення документації: `2026-09-12`

## Зміст

- [Що вміє API](#що-вміє-api)
- [Структура проєкту](#структура-проєкту)
- [Технології](#технології)
- [Запуск через Docker](#запуск-через-docker)
- [Конфігурація](#конфігурація)
- [Рольова авторизація](#рольова-авторизація)
- [Обробка помилок](#обробка-помилок)
- [Rate limiting](#rate-limiting)
- [Огляд API](#огляд-api)
- [Логіка CommentService](#логіка-commentservice)
- [Кешування](#кешування)
- [Обробка медіа](#обробка-медіа)
- [Розгортання](#розгортання)

## Що вміє API

- JWT-автентифікація з `access token` + `refresh token`
- реєстрація, логін, logout, `me`, скидання і зміна пароля
- профілі користувачів з `avatar`, `banner`, `bio`, `location`
- пости з текстом, embed, локацією, медіа та опитуваннями
- лайки, репости, bookmarks і перегляди для постів та коментарів
- коментарі з підтримкою вкладеності через `parent comment` і user-interactions
- підписки: followers / following
- пошук користувачів і постів
- пошук GIF через `Giphy`
- пошук локацій через `Geoapify`
- дворівневе кешування пошуку через `HybridCache` і `Redis`
- завантаження зображень і відео в `AWS S3`
- стиснення відео та генерація thumbnail перед upload
- endpoint-specific rate limiting для auth, створення контенту, interactions, upload і зовнішнього пошуку

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
│   │   └── MediaController.cs      # upload і доступ до медіа
│   ├── ExceptionHandling           # глобальне перетворення винятків у ProblemDetails
│   ├── Requests                    # HTTP-моделі для multipart/form-data
│   └── Program.cs                  # entrypoint і DI-конфігурація API
├── Threads.Application
│   ├── DTOs
│   │   ├── Posts / Comments        # DTO публікацій і коментарів
│   │   ├── Likes                   # combined response для лайкнутих targets
│   │   ├── Bookmarks               # combined response для збережених targets
│   │   ├── Reposts                 # combined response для repost targets
│   │   └── Auth / Users / Media    # інші request/response DTO
│   ├── Interfaces                  # контракти сервісів і репозиторіїв
│   ├── Mapping                     # AutoMapper profiles
│   ├── Services                    # бізнес-логіка застосунку
│   └── Exceptions                  # application exceptions
├── Threads.Domain
│   ├── Common                      # базові domain-моделі
│   ├── Entities                    # EF/domain entities
│   └── Enums                       # domain enums
├── Threads.Infrastracture
│   ├── Data
│   │   ├── Configurations          # EF Core і table configurations
│   │   └── Repositories            # реалізації repository interfaces
│   ├── Exceptions                  # технічні Infrastructure exceptions
│   ├── Migrations                  # EF Core migrations і model snapshot
│   ├── Security                    # JWT, password hashing, CORS і policies
│   └── Services                    # S3, Redis, email, GIF, location і ffmpeg
├── deploy/nginx                    # nginx reverse proxy configuration
├── Dockerfile                      # образ API
├── docker-compose.yml              # API та Redis для локального запуску
└── BackEndForFinalProject.sln      # solution file
```

### Архітектурний потік

1. Запит приходить у контролер з `Threads.Api`.
2. Контролер дістає auth context і валідує route-level умови.
3. Application service виконує бізнес-логіку.
4. Репозиторії та зовнішні інтеграції працюють через `Threads.Infrastracture`.
5. Очікувані негативні результати повертаються через `null`, `bool` або status DTO.
6. Необроблені винятки проходять через глобальний exception handler і перетворюються на `ProblemDetails`.
7. API повертає DTO або стандартизовану помилку у вигляді JSON-відповіді.

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
- `AWS S3`
- `ffmpeg` / `ffprobe`
- `Resend`
- `Giphy API`
- `Geoapify API`
- `Docker`
- `Nginx`

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

Cors__AllowedOrigins__0=http://localhost:8000
Cors__AllowedOrigins__1=http://127.0.0.1:8000
```

`ReverseProxy__KnownProxy` — адреса Docker gateway, з якої Nginx підключається до API-контейнера. Актуальне значення можна отримати після створення контейнера:

```bash
docker inspect threads-api \
  --format '{{range .NetworkSettings.Networks}}{{.Gateway}}{{end}}'
```

### 2. Підійми контейнер

```bash
docker compose up --build -d
```

### 3. API буде доступне тут

```text
http://127.0.0.1:7000
```

`docker-compose.yml` мапить контейнерний порт `8080` на локальний `7000`.

API стартує після успішного healthcheck Redis. Для Redis автоматично формується connection string `redis:6379` із паролем із `REDIS_PASSWORD`.

## Конфігурація

### Обов'язково для старту API

- `ConnectionStrings__DefaultConnection`
- `Jwt__Issuer`
- `Jwt__Audience`
- `Jwt__Key`
- `ReverseProxy__KnownProxy`
- `Redis__ConnectionString` при запуску без `docker compose`

При запуску через `docker compose` замість ручного `Redis__ConnectionString` достатньо задати `REDIS_PASSWORD` у `.env`.

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

### База даних

У репозиторії є початкова EF Core migration `20260909111719_Initial` від `2026-09-09`. Застосувати її можна командою:

```bash
dotnet ef database update \
  --project Threads.Infrastracture \
  --startup-project Threads.Api
```

## Рольова авторизація

Додано: `2026-09-07`

- користувач має роль `User` або `Moderator`; нові користувачі за замовчуванням отримують `User`
- роль зберігається в `Users.Role` як ціле число
- роль додається до access token через `ClaimTypes.Role`
- JWT Bearer використовує `ClaimTypes.Role` для перевірки ролі користувача
- policy `Moderation` дозволяє доступ користувачам із роллю `Moderator`
- майбутні moderation endpoints захищатимуться атрибутом `[Authorize(Policy = AuthorizationPolicies.Moderation)]`
- moderation endpoints у поточній версії API ще не реалізовані

## Обробка помилок

API використовує `GlobalExceptionHandler` із `IExceptionHandler`, зареєстрований через `AddExceptionHandler<GlobalExceptionHandler>()` і `UseExceptionHandler()`. Контролери не дублюють однакові `try/catch`: вони викликають application services, а необроблені винятки централізовано перетворюються на `ProblemDetails`.

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

Application exceptions розміщені в `Threads.Application/Exceptions`, а технічні винятки конфігурації — у `Threads.Infrastracture/Exceptions`. Giphy та Geoapify перетворюють мережеві помилки й некоректний JSON на `ExternalServiceException`, не передаючи клієнту внутрішні деталі інтеграції.

Винятки використовуються лише для переривання сценарію. Очікувані результати, наприклад неправильні credentials, недійсний refresh token, прострочений verification code або повторне видалення interaction, залишаються `null`, `false` чи окремим status і обробляються контролером.

Приклад відповіді глобального handler:

```json
{
  "type": "about:blank",
  "title": "Invalid request",
  "status": 400,
  "detail": "Post must contain content, media, poll, or embed.",
  "instance": "/api/posts"
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
| `ExternalSearchPolicy` | token bucket: burst `10`, `+5 / 10 с` | IP | Giphy і Geoapify search |

Endpoint-и з однаковою named policy використовують спільний bucket у межах одного partition key. Наприклад, усі interactions одного користувача витрачають спільні токени `InteractionPolicy`, а GIF і location search з однієї IP використовують спільні токени `ExternalSearchPolicy`.

При перевищенні ліміту API повертає `429 Too Many Requests`:

```json
{
  "message": "Too many requests. Please try again later."
}
```

Якщо limiter надає час відновлення, відповідь також містить HTTP-заголовок `Retry-After` із кількістю секунд до наступної дозволеної спроби.

Поточні limiter-и зберігають стан у пам'яті API-процесу. Це відповідає поточному deployment з одним `api` container. При горизонтальному масштабуванні rate limiting потрібно перенести на reverse proxy/API gateway або реалізувати спільні атомарні лічильники в Redis.

## Огляд API

Базовий префікс: `api/`

У колонці `Auth` значення `Так` означає, що endpoint вимагає Bearer access token. Публічні endpoint-и можуть використовувати переданий token для формування персоналізованих полів відповіді.

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
| `GET` | `/api/users/{username}/posts` | Ні | Отримати пости користувача |
| `GET` | `/api/users/{username}/likes` | Ні | Отримати лайкнуті користувачем пости та коментарі |
| `GET` | `/api/users/{username}/reposts` | Ні | Отримати reposts постів і коментарів користувача |

### Me

Основні операції з авторизованим користувачем згруповані під `/api/me` і потребують Bearer access token. Canonical-маршрут зміни пароля також знаходиться тут.

| Method | Route | Auth | Призначення |
|---|---|---|---|
| `GET` | `/api/me` | Так | Отримати профіль поточного користувача |
| `PUT` | `/api/me` | Так | Оновити профіль, avatar і banner через `multipart/form-data` |
| `DELETE` | `/api/me` | Так | Видалити акаунт поточного користувача |
| `GET` | `/api/me/posts` | Так | Отримати власні пости поточного користувача |
| `GET` | `/api/me/likes` | Так | Отримати лайкнуті пости та коментарі поточного користувача |
| `GET` | `/api/me/bookmarks` | Так | Отримати збережені пости та коментарі поточного користувача |
| `GET` | `/api/me/reposts` | Так | Отримати репости постів і коментарів поточного користувача |
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

`UserResponse` використовується і для публічного профілю, і для `/api/me`. Поле `email` є nullable: у відповідях `/api/users/...` воно завжди дорівнює `null`, а `GET /api/me` і успішний `PUT /api/me` повертають email поточного користувача. Приватний профіль завантажується окремо від кешованого публічного профілю, щоб email не потрапляв у public profile cache.

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
  ]
}
```

- `posts` містить об'єкти `PostResponse`;
- `comments` містить об'єкти `CommentResponse`;
- `actionAt` містить UTC-час створення відповідного `Like`, `Bookmark` або `Repost`; у звичайних content endpoints це поле дорівнює `null`;
- `/likes` використовує `UserLikesResponse`;
- `/bookmarks` використовує `UserBookmarksResponse`;
- `/reposts` використовує `UserRepostsResponse`;
- кожна колекція окремо відсортована від найновішої взаємодії до найстарішої; спільного сортування між posts і comments немає;
- клієнт може об'єднати `posts` і `comments` та відсортувати спільний список за `actionAt` у спадному порядку;
- публічні endpoints не вимагають авторизації, але за наявності Bearer token персоналізовані поля формуються відносно поточного viewer-а;
- bookmarks доступні лише власнику через `/api/me/bookmarks`.

### Posts

| Method | Route | Auth | Призначення |
|---|---|---|---|
| `GET` | `/api/posts/feed` | Ні | Отримати публічну стрічку постів |
| `GET` | `/api/posts/{id}` | Ні | Отримати пост за `Guid` |
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

### Comments

| Method | Route | Auth | Призначення |
|---|---|---|---|
| `GET` | `/api/comments/post/{postId}` | Ні | Отримати коментарі поста |
| `GET` | `/api/comments/{id}` | Ні | Отримати коментар за `Guid` |
| `POST` | `/api/comments` | Так | Створити коментар або відповідь |
| `PUT` | `/api/comments/{id}` | Так | Оновити власний коментар |
| `DELETE` | `/api/comments/{id}` | Так | Видалити власний коментар |
| `POST` | `/api/comments/{id}/view` | Так | Зареєструвати перегляд коментаря |
| `POST` | `/api/comments/{id}/like` | Так | Поставити лайк коментарю |
| `DELETE` | `/api/comments/{id}/like` | Так | Прибрати лайк із коментаря |
| `POST` | `/api/comments/{id}/repost` | Так | Зробити repost коментаря |
| `DELETE` | `/api/comments/{id}/repost` | Так | Скасувати repost коментаря |
| `POST` | `/api/comments/{id}/bookmark` | Так | Додати коментар у bookmarks |
| `DELETE` | `/api/comments/{id}/bookmark` | Так | Прибрати коментар із bookmarks |

### Логіка CommentService

`CommentService` знаходиться у `Threads.Application/Services/CommentService.cs` і виконує бізнес-логіку між API-контролерами та репозиторіями. Для роботи він використовує `ICommentRepository`, `IPostRepository`, репозиторії likes/bookmarks/reposts, `IObjectStorageService` для URL аватара та `IMapper` для перетворення entities на response DTO.

Усі публічні методи асинхронні та приймають `CancellationToken`, щоб запит до БД можна було скасувати, якщо клієнт розірвав HTTP-з'єднання або застосунок завершує роботу.

#### Отримання коментарів

| Метод | Що робить |
|---|---|
| `GetByPostIdAsync(postId, cancellationToken, currentUserId)` | Завантажує всі коментарі поста разом з авторами, replies, likes, bookmarks, reposts і views. Результат сортується за `CreatedAt` від старих коментарів до нових. Запит фільтрує лише за `PostId`, тому повертає і кореневі коментарі, і відповіді з `ParentCommentId`. |
| `GetByIdAsync(id, cancellationToken, currentUserId)` | Повертає один коментар за `Guid`. Якщо коментар не існує, повертає `null`, який контролер перетворює на `404 Not Found`. |
| `GetLikedByUserIdAsync(userId, cancellationToken, currentUserId)` | Повертає коментарі, які лайкнув користувач `userId`, від найновішого лайка до найстарішого. У `ActionAt` записується час створення лайка. |
| `GetBookmarkedByUserIdAsync(userId, cancellationToken, currentUserId)` | Повертає збережені користувачем коментарі, від найновішої закладки до найстарішої. У `ActionAt` записується час створення bookmark. |
| `GetRepostedByUserIdAsync(userId, cancellationToken, currentUserId)` | Повертає репостнуті користувачем коментарі, від найновішого репосту до найстарішого. У `ActionAt` записується час репосту. |

У collection-методах `userId` визначає, чию колекцію потрібно отримати, а `currentUserId` — відносно якого viewer-а потрібно обчислити персональні поля `IsLikedByCurrentUser`, `IsBookmarkedByCurrentUser` та `IsRepostedByCurrentUser`. Якщо `currentUserId` не переданий, усі ці поля мають значення `false`.

#### Створення, оновлення і видалення

##### `CreateAsync`

Метод створює звичайний коментар або відповідь на інший коментар:

1. Відхиляє порожній текст або рядок лише з пробілів через `RequestValidationException`.
2. Перевіряє існування поста з `request.PostId`.
3. Якщо переданий `ParentCommentId`, перевіряє існування батьківського коментаря та належність до того самого поста.
4. Створює `Comment` через AutoMapper, встановлює `AuthorId` із поточного користувача та обрізає зовнішні пробіли через `Trim()`.
5. Зберігає entity та повторно завантажує її з навігаційними властивостями для повної API-відповіді.

База обмежує довжину `Content` до `1000` символів. Сервіс окремо перевіряє тільки те, що текст не порожній.

##### `UpdateAsync`

Метод перевіряє новий текст, знаходить коментар, оновлює тільки `Content`, виставляє `UpdatedAt` у UTC і зберігає зміни. Якщо коментар не знайдений, повертає `null`. Автор, пост і `ParentCommentId` не змінюються.

##### `DeleteAsync`

Метод повертає `false`, якщо коментар не знайдений, або видаляє його і повертає `true`. Через cascade delete разом із коментарем видаляються його replies, likes, bookmarks, reposts і views.

Перевірка того, що редагувати або видаляти коментар намагається саме його автор, зараз виконується в `CommentsController`, а не всередині `CommentService`.

#### Likes, bookmarks і reposts

Пари методів `LikeAsync` / `UnlikeAsync`, `BookmarkAsync` / `UnbookmarkAsync` і `RepostAsync` / `UnrepostAsync` працюють за однаковим принципом:

1. Перевіряють існування коментаря; якщо його немає — повертають `null`.
2. Шукають interaction за парою `userId + commentId`.
3. Add-метод створює interaction тільки тоді, коли його ще немає.
4. Remove-метод видаляє interaction тільки тоді, коли він існує.
5. Повторно завантажують коментар та повертають актуальні counters і персональні flags.

Операції є ідемпотентними: повторний like/bookmark/repost не створює логічний дублікат, а повторне видалення відсутньої взаємодії не завершується помилкою. На рівні БД також є унікальні індекси для пари користувач-коментар. Якщо два однакові add-запити виконуються одночасно, сервіс перехоплює `DbUpdateException`, після чого повертає актуальний стан коментаря.

Для interaction коментаря завжди встановлюється `CommentId`, а `PostId` залишається `null`, оскільки одна interaction entity може посилатися або на пост, або на коментар.

#### `ViewAsync`

Метод реєструє унікальний перегляд коментаря авторизованим користувачем:

1. Завантажує коментар разом із його `Views`.
2. Перевіряє, чи вже існує перегляд від `userId`.
3. Якщо перегляду немає, додає `View` із `CommentId` і `ViewerId`.
4. Повторно завантажує коментар і повертає актуальний `ViewsCount`.

Один користувач збільшує лічильник конкретного коментаря лише один раз. Повторні та одночасні запити додатково захищені унікальним індексом у БД.

#### Формування CommentResponse

Приватний метод `MapCommentResponse` формує фінальний `CommentResponse` із такими даними:

- основні поля: `Id`, `PostId`, `ParentCommentId`, `Content`, `CreatedAt`, `UpdatedAt`;
- автор: `Id`, `Username`, `DisplayName`, `Location`, `AvatarUrl`, `IsVerified`;
- counters: `LikesCount`, `RepliesCount`, `RepostsCount`, `ViewsCount`;
- viewer state: `IsLikedByCurrentUser`, `IsBookmarkedByCurrentUser`, `IsRepostedByCurrentUser`;
- `ActionAt`: час like/bookmark/repost у відповідній interaction collection або `null` у звичайних content endpoints.

URL аватара не зберігається безпосередньо в DTO: якщо в користувача є `AvatarObjectKey`, `CommentService` формує read URL через `IObjectStorageService`. Якщо локація автора не задана, `Location` дорівнює `null`.

#### Відповідність методів HTTP endpoints

| Service method | HTTP endpoint |
|---|---|
| `GetByPostIdAsync` | `GET /api/comments/post/{postId}` |
| `GetByIdAsync` | `GET /api/comments/{id}` |
| `CreateAsync` | `POST /api/comments` |
| `UpdateAsync` | `PUT /api/comments/{id}` |
| `DeleteAsync` | `DELETE /api/comments/{id}` |
| `ViewAsync` | `POST /api/comments/{id}/view` |
| `LikeAsync` / `UnlikeAsync` | `POST` / `DELETE /api/comments/{id}/like` |
| `BookmarkAsync` / `UnbookmarkAsync` | `POST` / `DELETE /api/comments/{id}/bookmark` |
| `RepostAsync` / `UnrepostAsync` | `POST` / `DELETE /api/comments/{id}/repost` |
| `GetLikedByUserIdAsync` | `/api/users/{username}/likes`, `/api/me/likes` |
| `GetBookmarkedByUserIdAsync` | `/api/me/bookmarks` |
| `GetRepostedByUserIdAsync` | `/api/users/{username}/reposts`, `/api/me/reposts` |

### Follows

| Method | Route | Auth | Призначення |
|---|---|---|---|
| `POST` | `/api/follows/{userId}` | Так | Підписатися на користувача |
| `DELETE` | `/api/follows/{userId}` | Так | Відписатися від користувача |
| `GET` | `/api/follows/{userId}/followers` | Ні | Отримати followers користувача |
| `GET` | `/api/follows/{userId}/following` | Ні | Отримати користувачів, на яких оформлена підписка |
| `DELETE` | `/api/follows/{userId}/followers/{followId}` | Так | Видалити follower зі свого профілю |

### Search

| Method | Route | Auth | Призначення |
|---|---|---|---|
| `GET` | `/api/search/users?q=...` | Ні | Знайти користувачів |
| `GET` | `/api/search/posts?q=...` | Ні | Знайти пости |
| `GET` | `/api/search/gifs?q=...` | Ні | Знайти GIF через Giphy |
| `GET` | `/api/search/locations?q=...` | Ні | Знайти локації через Geoapify |

### Media

| Method | Route | Auth | Призначення |
|---|---|---|---|
| `GET` | `/api/media/{id}` | Умовно | Отримати presigned URL прикріпленого медіа; неприкріплене доступне лише uploader-у |
| `POST` | `/api/media/upload` | Так | Завантажити файл через `multipart/form-data` |

## Кешування

Оновлено: `2026-09-07`

- пошук GIF і локацій використовує `HybridCache`
- L1-кеш зберігається в пам'яті API-контейнера протягом `5 хвилин`
- L2-кеш зберігається в Redis протягом `30 хвилин`
- ключі Redis мають префікс `threads:`
- загальні значення за замовчуванням для інших HybridCache entries: L1 — `1 хвилина`, L2 — `5 хвилин`

## Обробка медіа

- зображення проходять валідацію і завантажуються в S3 без перекодування
- відео стискаються у `MP4` перед upload
- метадані для відео знімаються вже з обробленого файлу
- для відео генерується thumbnail
- ліміт upload у застосунку: `100 MB`
- у `nginx` зараз дозволено `512M`, що не конфліктує з API-лімітом

## Розгортання

- Docker image вже містить `ffmpeg`
- API слухає `8080` всередині контейнера
- `docker-compose.yml` публікує його на `127.0.0.1:7000`
- Redis працює в окремому контейнері `threads-redis`, захищений паролем і має healthcheck
- API залежить від успішного Redis healthcheck
- конфіг `deploy/nginx/threads.conf` проксіює трафік на `127.0.0.1:7000`
- Nginx передає `X-Forwarded-For` і `X-Forwarded-Proto`; API довіряє лише proxy з `ReverseProxy__KnownProxy`
- forwarded headers обробляються до authentication та rate limiting, тому IP-based policies використовують адресу клієнта, а не Docker gateway

## Примітки

- Swagger у поточному проєкті не підключений.
- README описує фактичні контролери, маршрути й конфігурацію, які є в коді зараз.
- Для `Like`, `Bookmark`, `Repost` і `View` тепер використовується єдина сутність на `post` або `comment` target.
- Останні зміни від `2026-09-12`: зміна пароля розділена на start/confirm endpoint-и з окремими request DTO; додано глобальний exception handler і типізовані Application/Infrastructure exceptions; контролери більше не дублюють `try/catch`; помилки Giphy та Geoapify перетворюються на безпечні `502 Bad Gateway` responses; додано endpoint-specific rate limiting і trusted forwarded headers для Nginx.
