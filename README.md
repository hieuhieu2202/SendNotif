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

Ví dụ lấy và xoá thông báo trên Postman:

```
GET http://localhost:5067/api/control/get-notifications?clientId=device1

POST http://localhost:5067/api/control/clear-notifications?clientId=device1
```

## Các endpoint chính
- `POST /api/control/send-notification-json` : gửi thông báo dạng JSON.
- `POST /api/control/send-notification-form` : gửi thông báo kèm tệp (form-data).
- `GET /api/control/get-notifications?clientId=YOUR_ID` : lấy thông báo của từng client.
- `POST /api/control/clear-notifications?clientId=YOUR_ID` : đánh dấu thông báo đã xem cho client đó.
- `GET /api/control/notifications-stream` : stream thông báo (SSE).

Mỗi thiết bị nên dùng một `clientId` riêng, việc xoá của một client sẽ không ảnh hưởng đến client khác. Tham khảo thêm trong Swagger tại `/swagger`.
