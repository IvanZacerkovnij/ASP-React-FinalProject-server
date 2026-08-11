# Threads API Backend

Backend для соціального застосунку у стилі Threads, побудований на `ASP.NET Core Web API` з `PostgreSQL`, `EF Core`, `JWT`, `AWS S3` і обробкою медіа через `ffmpeg`.

## Зміст

- [Що вміє API](#що-вміє-api)
- [Структура проєкту](#структура-проєкту)
- [Технології](#технології)
- [Запуск через Docker](#запуск-через-docker)
- [Конфігурація](#конфігурація)
- [Огляд API](#огляд-api)
- [Обробка медіа](#обробка-медіа)
- [Розгортання](#розгортання)

## Що вміє API

- JWT-автентифікація з `access token` + `refresh token`
- реєстрація, логін, logout, `me`, скидання і зміна пароля
- профілі користувачів з `avatar`, `banner`, `bio`, `location`
- пости з текстом, embed, локацією, медіа та опитуваннями
- лайки, репости, перегляди постів
- коментарі з підтримкою вкладеності через `parent comment`
- підписки: followers / following
- пошук користувачів і постів
- пошук GIF через `Giphy`
- пошук локацій через `Geoapify`
- завантаження зображень і відео в `AWS S3`
- стиснення відео та генерація thumbnail перед upload

## Структура проєкту

```text
BackEndForFinalProject
├── Threads.Api               # controllers, entrypoint, HTTP layer
├── Threads.Application       # DTOs, interfaces, business services
├── Threads.Domain            # domain entities
├── Threads.Infrastracture    # EF Core, repositories, integrations, security
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

> `docker-compose.yml` у репозиторії підіймає тільки API-контейнер. PostgreSQL потрібно мати окремо: локально, в іншому compose-стеку або як зовнішню БД.

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

## Конфігурація

### Обов'язково для старту API

- `ConnectionStrings__DefaultConnection`
- `Jwt__Issuer`
- `Jwt__Audience`
- `Jwt__Key`

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

## Огляд API

Базовий префікс: `api/`

### Auth

| Method | Route | Призначення |
|---|---|---|
| `POST` | `/api/auth/register` | Реєстрація |
| `POST` | `/api/auth/login` | Логін |
| `POST` | `/api/auth/refresh` | Оновлення access token |
| `POST` | `/api/auth/logout` | Відкликання refresh token |
| `POST` | `/api/auth/forgot-password` | Надсилання reset code |
| `POST` | `/api/auth/verify-reset-code` | Перевірка reset code |
| `POST` | `/api/auth/reset-password` | Скидання пароля |
| `POST` | `/api/auth/change-password` | Зміна пароля з email confirmation |
| `GET` | `/api/auth/me` | Поточний користувач |

### Users і Follows

| Method | Route | Призначення |
|---|---|---|
| `GET` | `/api/users` | Список користувачів |
| `GET` | `/api/users/{id}` | Користувач за `Guid` |
| `GET` | `/api/users/{username}` | Користувач за username |
| `GET` | `/api/users/by-username/{username}` | Явний route за username |
| `GET` | `/api/users/{id}/posts` | Пости користувача |
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

| Method | Route | Призначення |
|---|---|---|
| `GET` | `/api/posts` | Усі пости |
| `GET` | `/api/posts/feed` | Стрічка |
| `GET` | `/api/posts/liked` | Лайкнуті пости поточного юзера |
| `GET` | `/api/posts/reposted` | Репости поточного юзера |
| `GET` | `/api/posts/user/{username}` | Пости автора |
| `GET` | `/api/posts/{id}` | Пост за `Guid` |
| `POST` | `/api/posts/{id}/view` | Зареєструвати перегляд |
| `POST` | `/api/posts/{id}/like` | Поставити лайк |
| `DELETE` | `/api/posts/{id}/like` | Прибрати лайк |
| `POST` | `/api/posts/{id}/repost` | Репост |
| `DELETE` | `/api/posts/{id}/repost` | Скасувати репост |
| `POST` | `/api/posts/{id}/poll/vote` | Проголосувати в poll |
| `POST` | `/api/posts` | Створити пост |
| `PUT` | `/api/posts/{id}` | Оновити пост |
| `DELETE` | `/api/posts/{id}` | Видалити пост |
| `GET` | `/api/comments/post/{postId}` | Коментарі поста |
| `POST` | `/api/comments` | Створити коментар |
| `PUT` | `/api/comments/{id}` | Оновити коментар |
| `DELETE` | `/api/comments/{id}` | Видалити коментар |

### Search і Media

| Method | Route | Призначення |
|---|---|---|
| `GET` | `/api/search/users?q=...` | Пошук користувачів |
| `GET` | `/api/search/posts?q=...` | Пошук постів |
| `GET` | `/api/search/gifs?q=...` | Пошук GIF |
| `GET` | `/api/search/locations?q=...` | Пошук локацій |
| `GET` | `/api/media/{id}` | Отримати presigned URL медіа |
| `POST` | `/api/media/upload` | Завантажити файл |

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
- конфіг `deploy/nginx/threads.conf` проксіює трафік на `127.0.0.1:7000`

## Примітки

- Swagger у поточному проєкті не підключений.
- README описує фактичні контролери й конфігурацію, які є в коді зараз.
