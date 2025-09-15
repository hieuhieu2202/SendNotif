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

Open `http://localhost:5067/swagger` for interactive documentation. Use the
`POST /api/Control/send-notification` operation to send a notification with
`multipart/form-data` (including an optional `file` field). To send a JSON payload
with Base64 data, call `POST /api/Control/send-notification-json`. `GET
/api/Control/get-notifications` lists the latest notifications (the server
retains only the most recent 20).

## API Endpoints

| # | API | Chức năng chính |
| - | --- | ---------------- |
| 1 | `POST /api/Control/send-notification-json` | Gửi thông báo dạng JSON (đính kèm file qua base64). |
| 2 | `POST /api/Control/send-notification` | Gửi thông báo với file upload dạng `multipart/form-data`. |
| 3 | `GET /api/Control/get-notifications` | Lấy danh sách thông báo hiện có (có phân trang). |
| 4 | `POST /api/Control/clear-notifications` | Xóa toàn bộ thông báo khỏi hàng đợi. |
| 5 | `GET /api/Control/notifications-stream` | Stream SSE để nhận thông báo thời gian thực. |
| 6 | `GET /api/Control/app-version` | Trả về thông tin phiên bản ứng dụng mới nhất. |
| 7 | `POST /api/Control/app-version/upload` | Upload bản build mới (APK/IPA) và cập nhật manifest. |
| 8 | `GET /api/Control/app-version/download` | Tải xuống bản build mới cho thiết bị (Android/iOS). |
| 9 | `HEAD /api/Control/app-version/download` | Kiểm tra metadata (kích thước, SHA256, content type) của bản build mới. |

| Method | Path | Description |
| ------ | ---- | ----------- |
| `GET` | `/uploads/{filename}` | Download an uploaded file (URL returned in notification). |

## Postman / cURL

Send a multipart request (form-data) with fields `id`, `title`, `body`, optional `link`, optional `targetVersion`, and an optional `file`:

```
POST http://localhost:5067/api/Control/send-notification
```

Or send JSON when you already have the file as Base64:

```
POST http://localhost:5067/api/Control/send-notification-json
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
GET http://localhost:5067/api/Control/get-notifications
```

Responses include a `fileUrl` you can open to download or preview the uploaded file.

SQL Server DDL for the backing tables is available in `sql/create_tables.sql`.

