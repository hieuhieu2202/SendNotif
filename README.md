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

## Postman / cURL

Send a multipart request (form-data) with fields `id`, `title`, `body` and an optional `file`:

```
POST http://localhost:5067/api/control/send-notification
```

Or send JSON when you already have the file as Base64:

```
POST http://localhost:5067/api/control/send-notification
Content-Type: application/json

{
  "id": "123",
  "title": "Hello",
  "body": "Test message",
  "fileName": "note.txt",
  "fileBase64": "SGVsbG8gd29ybGQ="
}
```

The server responds with a `fileUrl` you can open to download or preview the uploaded file.

