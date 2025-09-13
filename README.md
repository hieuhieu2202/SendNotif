RemoteControlApi
=================

This project exposes a notification API for sending notifications with optional
links and file attachments.

## Run

```bash
dotnet run --project RemoteControlApi
```

The application listens on `http://localhost:5067` by default.

## Web Interface

- `http://localhost:5067/send.html` – send a notification with an optional link and file.
- `http://localhost:5067/receive.html` – view sent notifications, follow links, and preview/download attachments.

## Swagger

Open `http://localhost:5067/swagger` for interactive documentation.
Use the `POST /api/notifications/form` operation to send a notification with
`multipart/form-data` (including an optional `file` field). To send a JSON payload
with Base64 data, call `POST /api/notifications`. `GET /api/notifications` lists the
latest notifications.

## Postman / cURL

Send a multipart request (form-data) with fields `id`, `title`, `body`, an optional `link`, and an optional `file`:

```
POST http://localhost:5067/api/notifications/form
```

Or send JSON when you already have the file as Base64:

```
POST http://localhost:5067/api/notifications
Content-Type: application/json

{
  "id": "123",
  "title": "Hello",
  "body": "Test message",
  "link": "https://example.com",
  "fileName": "note.txt",
  "fileBase64": "SGVsbG8gd29ybGQ="
}
```

Retrieve messages:

```
GET http://localhost:5067/api/notifications
```

Responses include a `fileUrl` you can open to download or preview the uploaded file.

