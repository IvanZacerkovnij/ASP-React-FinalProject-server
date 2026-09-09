# Threads API Backend

Backend для соціального застосунку у стилі Threads, побудований на `ASP.NET Core Web API` з `PostgreSQL`, `EF Core`, `JWT`, `AWS S3` і обробкою медіа через `ffmpeg`.

> Останнє оновлення документації: `2026-09-07`

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
├── Threads.Api               # controllers, entrypoint, HTTP layer
├── Threads.Application       # DTOs, interfaces, business services
├── Threads.Domain            # domain entities
├── Threads.Infrastracture    # EF Core, repositories, integrations, security
│   └── Migrations            # EF Core migrations і model snapshot
├── deploy/nginx              # nginx config for reverse proxy
├── Dockerfile
└── docker-compose.yml
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

У репозиторії є початкова EF Core migration від `2026-09-04`. Застосувати її можна командою:

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

### Auth

| Method | Route | Призначення |
|---|---|---|
| `POST` | `/api/auth/register` | Створення pending registration і надсилання verification code |
| `POST` | `/api/auth/login` | Логін |
| `POST` | `/api/auth/refresh` | Оновлення access token |
| `POST` | `/api/auth/logout` | Відкликання refresh token |
| `POST` | `/api/auth/verify-email` | Підтвердження email і фінальне створення користувача |
| `POST` | `/api/auth/resend-verification-code` | Повторне надсилання verification code |
| `POST` | `/api/auth/forgot-password` | Надсилання reset code |
| `POST` | `/api/auth/verify-reset-code` | Перевірка reset code |
| `POST` | `/api/auth/reset-password` | Скидання пароля |
| `POST` | `/api/auth/change-password` | Зміна пароля з email confirmation |
| `GET` | `/api/auth/me` | Поточний користувач |

### Users і Follows

Оновлено `2026-09-09 11:58 EEST`: endpoint `GET /api/users/by-username/{username}` видалено, оскільки він повністю дублював логіку `GET /api/users/{username}`.

Оновлено `2026-09-09 12:22 EEST`: endpoint `GET /api/users/{id}/posts` видалено, оскільки отримання постів автора вже виконує `GET /api/posts/user/{username}`.

| Method | Route | Призначення |
|---|---|---|
| `GET` | `/api/users` | Список користувачів |
| `GET` | `/api/users/{id}` | Користувач за `Guid` |
| `GET` | `/api/users/{username}` | Користувач за username |
| `GET` | `/api/users/{username}/likes` | Лайкнуті пости користувача |
| `GET` | `/api/users/{username}/reposts` | Репости користувача |
| `PUT` | `/api/users/me` | Оновлення профілю, avatar, banner |
| `DELETE` | `/api/users/me` | Видалення акаунта |
| `POST` | `/api/follows/{userId}` | Підписатися |
| `DELETE` | `/api/follows/{userId}` | Відписатися |
| `GET` | `/api/follows/{userId}/followers` | Followers |
| `GET` | `/api/follows/{userId}/following` | Following |
| `DELETE` | `/api/follows/{userId}/followers/{followId}` | Видалити follower |

### Posts і Comments

Comments interactions and unified target entities updated: `2026-09-04`

| Method | Route                         | Призначення |
|---|-------------------------------|---|
| `GET` | `/api/posts`                  | Усі пости |
| `GET` | `/api/posts/feed`             | Стрічка |
| `GET` | `/api/posts/liked`            | Лайкнуті пости поточного юзера |
| `GET` | `/api/posts/bookmarks`        | Збережені пости поточного юзера |
| `GET` | `/api/posts/reposted`         | Репости поточного юзера |
| `GET` | `/api/posts/user/{username}`  | Пости автора |
| `GET` | `/api/posts/{id}`             | Пост за `Guid` |
| `POST` | `/api/posts/{id}/view`        | Зареєструвати перегляд |
| `POST` | `/api/posts/{id}/like`        | Поставити лайк |
| `DELETE` | `/api/posts/{id}/like`        | Прибрати лайк |
| `POST` | `/api/posts/{id}/repost`      | Репост |
| `DELETE` | `/api/posts/{id}/repost`      | Скасувати репост |
| `POST` | `/api/posts/{id}/bookmark`    | Додати пост у bookmarks |
| `DELETE` | `/api/posts/{id}/bookmark`    | Прибрати пост з bookmarks |
| `POST` | `/api/posts/{id}/poll/vote`   | Проголосувати в poll |
| `POST` | `/api/posts`                  | Створити пост |
| `PUT` | `/api/posts/{id}`             | Оновити пост |
| `DELETE` | `/api/posts/{id}`             | Видалити пост |
| `GET` | `/api/comments/post/{postId}` | Коментарі поста |
| `POST` | `/api/comments`               | Створити коментар |
| `PUT` | `/api/comments/{id}`          | Оновити коментар |
| `DELETE` | `/api/comments/{id}`          | Видалити коментар |
| `POST` | `/api/comments/{id}/view`     | Зареєструвати перегляд коментаря |
| `POST` | `/api/comments/{id}/like`     | Поставити лайк коментарю |
| `DELETE` | `/api/comments/{id}/like`     | Прибрати лайк з коментаря |
| `POST` | `/api/comments/{id}/repost`   | Репост коментаря |
| `DELETE` | `/api/comments/{id}/repost`   | Скасувати репост коментаря |
| `POST` | `/api/comments/{id}/bookmark` | Додати коментар у bookmarks |
| `DELETE` | `/api/comments/{id}/bookmark` | Прибрати коментар з bookmarks |

### Search і Media

| Method | Route | Призначення |
|---|---|---|
| `GET` | `/api/search/users?q=...` | Пошук користувачів |
| `GET` | `/api/search/posts?q=...` | Пошук постів |
| `GET` | `/api/search/gifs?q=...` | Пошук GIF |
| `GET` | `/api/search/locations?q=...` | Пошук локацій |
| `GET` | `/api/media/{id}` | Отримати presigned URL медіа |
| `POST` | `/api/media/upload` | Завантажити файл |

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
- README описує фактичні контролери й конфігурацію, які є в коді зараз.
- Для `Like`, `Bookmark`, `Repost` і `View` тепер використовується єдина сутність на `post` або `comment` target.
- Останні зміни від `2026-09-07`: додано Redis, L1/L2-кешування Giphy та Geoapify, Redis healthcheck і початкову EF Core migration.
