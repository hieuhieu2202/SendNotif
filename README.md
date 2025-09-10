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
Use the `send-notification` operation for multipart uploads and `send-notification-json`
for JSON. The `get-notifications` endpoint lists the latest notifications.

## Postman / cURL

Send a multipart request (form-data) with fields `id`, `title`, `body` and an optional `file`:

```
POST http://localhost:5067/api/control/send-notification
```

Or send JSON when you already have the file as Base64:

```
POST http://localhost:5067/api/control/send-notification-json
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
GET http://localhost:5067/api/control/get-notifications
```

Responses include a `fileUrl` you can open to download or preview the uploaded file.

