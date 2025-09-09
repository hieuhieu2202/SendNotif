# RemoteControlApi

API cho phép gửi và nhận thông báo. Bạn có thể thử nghiệm nhanh bằng giao diện tại `wwwroot/send.html`, Swagger hoặc Postman.

## Chạy dự án

```bash
# yêu cầu .NET 8 SDK
cd RemoteControlApi
dotnet run
```

Mặc định API chạy tại `http://localhost:5067` (hoặc port hiển thị trên console).

## Thử nghiệm API

### Swagger
Sau khi chạy ứng dụng, truy cập `http://localhost:5067/swagger` (hoặc port hiển thị trên console) để thử các endpoint trực tiếp trên trình duyệt.

### Postman
Ví dụ request gửi thông báo ở dạng JSON:

```
POST http://localhost:5067/api/control/send-notification-json
Content-Type: application/json

{
  "title": "Test",
  "body": "Hello from Postman"
}
```

Để gửi kèm tệp, gọi endpoint `send-notification-form`, chuyển sang tab *form-data* và thêm các trường `title`, `body` cùng trường tệp `file`.

## Các endpoint chính
- `POST /api/control/send-notification-json` : gửi thông báo dạng JSON.
- `POST /api/control/send-notification-form` : gửi thông báo kèm tệp (form-data).
- `GET /api/control/get-notifications` : lấy danh sách thông báo.
- `GET /api/control/notifications-stream` : stream thông báo (SSE).

Tham khảo thêm trong Swagger tại `/swagger`.
