# Frontend API Guide

Цей файл є практичним контрактом для AI-агента, який реалізує frontend до цього API.
Повний список endpoint-ів і моделей доступний у `README.md` та Swagger (`/swagger`).

## Загальні правила

- Базовий префікс endpoint-ів: `/api`.
- JSON-поля передавай у `camelCase`.
- Для захищених endpoint-ів передавай `Authorization: Bearer <accessToken>`.
- Звичайні DTO надсилай як `application/json`.
- Файли надсилай лише як `multipart/form-data`; не встановлюй `Content-Type` вручну, щоб browser додав boundary.
- `DateTimeOffset` передавай у ISO 8601 з `Z` або явним offset.
- Дату народження передавай як `YYYY-MM-DD`.
- Не вигадуй enum-значення: використовуй значення зі Swagger. Наприклад, visibility: `public`, `followers`, `following`, `mutual`, `only_me`.
- Не роби припущень із HTTP status: для помилок завжди читай RFC 7807 response body.

Типова помилка:

```json
{
  "title": "Invalid request",
  "status": 400,
  "detail": "Location cannot be provided when location removal is requested.",
  "instance": "/api/posts/{id}"
}
```

Для `429 Too Many Requests` враховуй заголовок `Retry-After`, якщо він присутній.

## Partial update

Update endpoint-и постів, профілю та metadata працюють як часткове оновлення:

- поле відсутнє — не змінювати поточне значення;
- поле передане зі значенням — встановити нове значення;
- для видалення metadata використовувати відповідний `remove...: true`;
- не покладатися на `null` як команду видалення;
- не передавати одночасно remove-прапорець і нове значення того самого поля.

Неправильно:

```json
{
  "removeLocation": true,
  "location": {
    "name": "Kyiv"
  }
}
```

Такий payload повертає `400 Bad Request`.

## Оновлення поста

Endpoint: `PUT /api/posts/{id}` (`application/json`).

Підтримувані remove-прапорці:

- `removePoll`;
- `removeLocation`;
- `removeLinkPreview`.

Видалення локації:

```json
{
  "removeLocation": true
}
```

Заміна локації:

```json
{
  "location": {
    "id": "place-id",
    "name": "Kyiv",
    "country": "Ukraine",
    "latitude": 50.45,
    "longitude": 30.52
  }
}
```

Оновлення тексту, очищення медіа та видалення локації одним запитом:

```json
{
  "content": "Updated text",
  "mediaIds": [],
  "removeLocation": true
}
```

Правила `mediaIds`:

- поле відсутнє — не змінювати attachments;
- `mediaIds: []` — відв'язати всі attachments;
- масив ID — встановити точний новий список і порядок attachments.

Якщо користувач прибрав локацію в editor UI, frontend state повинен зберегти цю дію окремо від `location`:

```ts
const payload = {
  content,
  mediaIds,
  ...(location ? { location } : {}),
  ...(hadLocationInitially && !location ? { removeLocation: true } : {}),
};
```

Перед відправленням перевіряй фактичний payload у Network tab. Для видалення локації там обов'язково має бути `"removeLocation": true`.

## Оновлення коментаря

Endpoint: `PUT /api/comments/{id}` (`application/json`).

Контракт metadata такий самий, як для поста:

```json
{
  "content": "Updated comment",
  "removePoll": true,
  "removeLocation": true,
  "removeLinkPreview": true
}
```

Особливості:

- `content` для update comment є обов'язковим;
- `mediaIds` відсутнє — attachments не змінюються;
- `mediaIds: []` — усі attachments відв'язуються;
- remove-прапорець не можна поєднувати з новим значенням відповідного metadata.

## Оновлення профілю

Endpoint: `PUT /api/me` (`multipart/form-data`).

Усі поля необов'язкові. Локація передається не JSON-рядком, а окремими form-полями:

```ts
const form = new FormData();
form.append("DisplayName", displayName);
form.append("Location.Id", location.id);
form.append("Location.Name", location.name);
form.append("Location.Country", location.country);
form.append("Location.Latitude", String(location.latitude));
form.append("Location.Longitude", String(location.longitude));
```

Для видалення локації:

```ts
form.append("RemoveLocation", "true");
```

Інші remove-поля профілю:

- `RemoveBirthDate`;
- `RemoveAvatar`;
- `RemoveBanner`.

Не додавай `Location.*`, коли передаєш `RemoveLocation=true`.

## Media flow

1. Завантаж файл через `POST /api/media/upload` як `multipart/form-data` з полем `File`.
2. Отримай ID медіа з response.
3. Передай цей ID у `mediaIds` під час створення або оновлення поста/коментаря.
4. Не передавай локальні URL, Blob URL або storage key замість media ID.

Приклад:

```ts
const uploadForm = new FormData();
uploadForm.append("File", file);

const uploadedMedia = await api.post("/api/media/upload", uploadForm);

await api.post("/api/posts", {
  content: "Post with media",
  mediaIds: [uploadedMedia.id],
});
```

## Pagination

- Перша сторінка: передай лише `limit` або використай default.
- Наступна сторінка: передай отриманий `nextCursor` як query-параметр `cursor`.
- Cursor є opaque: не парси та не змінюй його.
- Завершуй pagination, коли `hasMore === false` або `nextCursor` відсутній.

```json
{
  "items": [],
  "nextCursor": "opaque-cursor",
  "hasMore": true
}
```

## Checklist для frontend-агента

Перед завершенням будь-якої API-інтеграції:

1. Звір endpoint, HTTP method, auth і content type зі Swagger.
2. Звір точні назви request/response полів.
3. Перевір реальний payload у browser Network tab.
4. Для видалення metadata перевір відповідний `remove...: true`.
5. Переконайся, що payload не містить одночасно remove-прапорець і нове значення.
6. Оброби `400`, `401`, `403`, `404`, `409`, `429` через `ProblemDetails`.
7. Після mutation онови локальний state даними з API response або інвалідуй відповідний query cache.
8. Додай тест мінімум на success, validation error і removal-сценарій.

Для сценарію видалення локації поста тест повинен перевіряти:

1. Початковий пост має `location`.
2. Користувач натискає remove location.
3. Request payload містить `removeLocation: true` і не містить `location`.
4. У response `location === null`.
5. UI не відновлює стару локацію з локального state або stale query cache.
