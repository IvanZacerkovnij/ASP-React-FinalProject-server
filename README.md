# Threads API Backend

Backend для соціального застосунку у стилі Threads, побудований на `ASP.NET Core Web API` з `PostgreSQL`, `EF Core`, `JWT`, `AWS S3` і обробкою медіа через `ffmpeg`.

> Останнє оновлення документації: `2026-09-09`

## Зміст

- [Що вміє API](#що-вміє-api)
- [Структура проєкту](#структура-проєкту)
- [Технології](#технології)
- [Запуск через Docker](#запуск-через-docker)
- [Конфігурація](#конфігурація)
- [Рольова авторизація](#рольова-авторизація)
- [Огляд API](#огляд-api)
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
5. API повертає DTO у вигляді JSON-відповіді.

## Технології

- `.NET 10`
- `ASP.NET Core Web API`
- `Entity Framework Core`
- `PostgreSQL`
- `Redis 7`
- `.NET HybridCache` (L1 memory + L2 Redis)
- `Npgsql`
- `JWT Bearer Authentication`
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

GEOAPIFY_API_KEY=your-geoapify-key

REDIS_PASSWORD=your-strong-redis-password

Cors__AllowedOrigins__0=http://localhost:8000
Cors__AllowedOrigins__1=http://127.0.0.1:8000
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
| `POST` | `/api/me/change-password` | Так | Змінити пароль із підтвердженням через email code |

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

## Примітки

- Swagger у поточному проєкті не підключений.
- README описує фактичні контролери, маршрути й конфігурацію, які є в коді зараз.
- Для `Like`, `Bookmark`, `Repost` і `View` тепер використовується єдина сутність на `post` або `comment` target.
- Останні зміни від `2026-09-09`: `MeController` повертає likes, bookmarks і reposts постів та коментарів через єдині combined responses; для comment interactions додано окремі service/repository retrieval-ланцюжки.
