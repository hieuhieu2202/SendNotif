RemoteControlApi
=================

This project exposes a notification API for sending notifications with optional
links, file attachments, and a target version. Notifications are stored in a SQL Server database so
clients can retrieve missed messages even after server restarts. The connection
string lives under `ConnectionStrings:Notifications` in `appsettings.json` and
defaults to LocalDB.

> **Note:** LocalDB is available only on Windows. On Linux or in production, set
> `ConnectionStrings:Notifications` to a reachable SQL Server instance (for
> example: `Server=localhost;Database=NotificationDb;User Id=sa;Password=Pass@word;TrustServerCertificate=True`).

## Run

```bash
dotnet run --project RemoteControlApi
```

The application listens on `http://localhost:5067` by default.

On startup, the API applies any pending Entity Framework Core migrations so the
necessary tables are created automatically in the configured SQL Server
database.

## Web Interface

- `http://localhost:5067/send.html` – send a notification with an optional link, target version, and file.
- `http://localhost:5067/receive.html` – view sent notifications, follow links, and preview/download attachments.

## Swagger

Open `http://localhost:5067/swagger` for interactive documentation.
Use the `POST /api/notifications/form` operation to send a notification with
`multipart/form-data` (including an optional `file` field). To send a JSON payload
with Base64 data, call `POST /api/notifications`. `GET /api/notifications` lists the
latest notifications (the server retains only the most recent 20).

Device endpoints:

- `POST /api/devices/{id}/version` – report a device's current app version and optional card/user information.
- `GET /api/devices/{id}/notifications` – fetch notifications the device hasn't
  received yet.
- `POST /api/devices/{id}/notifications/{notificationId}/read` – mark a notification as read; updates the device's `LastSeen` and returns stored card/user details.

## API Endpoints

| Method | Path | Description |
| ------ | ---- | ----------- |
| `POST` | `/api/notifications` | Send a notification as JSON with optional Base64 file data and `targetVersion`. |
| `POST` | `/api/notifications/form` | Send a notification via multipart form-data with optional `targetVersion` and file. |
| `GET` | `/api/notifications` | List stored notifications (most recent first, capped at 20). |
| `POST` | `/api/notifications/clear` | Remove all stored notifications and delivery records. |
| `GET` | `/api/notifications/stream` | Stream new notifications in real time using Server-Sent Events. |
| `POST` | `/api/devices/{id}/version` | Report the current version of a device along with optional `cardCode`/`userName`. |
| `GET` | `/api/devices/{id}/notifications` | Fetch notifications a device has not yet received. |
| `POST` | `/api/devices/{id}/notifications/{notificationId}/read` | Mark a notification as read and update the device's last-seen timestamp. |
| `GET` | `/uploads/{filename}` | Download an uploaded file (URL returned in notification). |

## Postman / cURL

Send a multipart request (form-data) with fields `id`, `title`, `body`, optional `link`, optional `targetVersion`, and an optional `file`:

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
  "targetVersion": "1.2.3",
  "fileName": "note.txt",
  "fileBase64": "SGVsbG8gd29ybGQ="
}
```

Retrieve messages:

```
GET http://localhost:5067/api/notifications
```

Responses include a `fileUrl` you can open to download or preview the uploaded file.

Report device details:

```
POST http://localhost:5067/api/devices/device123/version
Content-Type: application/json

{
  "version": "1.0.0",
  "cardCode": "CARD123",
  "userName": "alice"
}

POST http://localhost:5067/api/devices/device123/notifications/{notificationId}/read
```

SQL Server DDL for the backing tables is available in `sql/create_tables.sql`.

