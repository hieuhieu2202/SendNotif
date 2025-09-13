RemoteControlApi
=================

This project exposes a notification API that accepts optional file attachments.

## Run

```bash
dotnet run --project RemoteControlApi
```

The application listens on `http://localhost:5067` by default.

## Web Interface

- `http://localhost:5067/send.html` – send a notification with an optional file.
- `http://localhost:5067/receive.html` – view sent notifications and preview/download attachments.

## Swagger

Open `http://localhost:5067/swagger` for interactive documentation.
Use the `POST /api/notifications` operation to send a notification. It accepts either
`multipart/form-data` (with a `file` field) or pure JSON that includes `fileBase64` and
`fileName`. `GET /api/notifications` lists the latest notifications.

## Postman / cURL

Send a multipart request (form-data) with fields `id`, `title`, `body` and an optional `file`:

```
POST http://localhost:5067/api/notifications
```

Or send JSON when you already have the file as Base64:

```
POST http://localhost:5067/api/notifications
Content-Type: application/json

{
  "id": "123",
  "title": "Hello",
  "body": "Test message",
  "fileName": "note.txt",
  "fileBase64": "SGVsbG8gd29ybGQ="
}
```

Retrieve messages:

```
GET http://localhost:5067/api/notifications
```

Responses include a `fileUrl` you can open to download or preview the uploaded file.

