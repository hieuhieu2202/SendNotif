# Hướng dẫn sử dụng hệ thống thông báo & cập nhật ứng dụng

Tài liệu này tổng hợp chức năng, danh sách API và ví dụ payload để bạn có thể cấu hình, kiểm thử hệ thống nhanh chóng.

> **Cơ bản**: tất cả endpoint đều nằm dưới `/api/control`. Server tự chạy `Database.Migrate()` khi khởi động nên chỉ cần cấu hình chuỗi kết nối trong `appsettings*.json` là có thể sử dụng.

## 1. Chức năng chính

| Nhóm | Mô tả |
| --- | --- |
| Quản lý ứng dụng | Đăng ký app mới với `appKey` riêng, xem danh sách ứng dụng đang hoạt động. |
| Quản lý phiên bản | Lưu trữ phiên bản ứng dụng (Android/iOS/khác), ghi nhận link tải, checksum, ngày phát hành. |
| Gửi thông báo đa ứng dụng | Một request có thể gửi thông báo tới nhiều app khác nhau, kèm link, file đính kèm, hoặc liên kết tới một bản cập nhật cụ thể. |
| Lấy thông báo & realtime | Client truy vấn danh sách thông báo theo `appKey` hoặc kết nối SSE để nhận realtime. |
| Kiểm tra cập nhật | Client gửi `currentVersion` để so sánh với bản phát hành mới nhất của ứng dụng trên server. |
| Dọn dữ liệu | API hỗ trợ xoá toàn bộ thông báo (tất cả app hoặc theo từng `appKey`). |

## 2. Danh sách API và payload kiểm thử

Các ví dụ dưới sử dụng biến `BASE_URL=https://your-host` để dễ thay thế. Nếu chạy local có thể đổi thành `http://localhost:5000`.

### 2.1. Ứng dụng (Applications)

**Lấy danh sách ứng dụng**
```bash
curl "$BASE_URL/api/control/applications"
```
Phản hồi mẫu:
```json
[
  {
    "applicationId": 1,
    "appKey": "gotyfi",
    "displayName": "GoTyfi",
    "description": "Ứng dụng gọi xe",
    "isActive": true,
    "createdAt": "2025-07-01T09:00:00Z",
    "versionCount": 3,
    "notificationCount": 12
  }
]
```

**Tạo ứng dụng mới**
```bash
curl -X POST "$BASE_URL/api/control/applications" \
  -H "Content-Type: application/json" \
  -d '{
        "appKey": "gotyfi",
        "displayName": "GoTyfi",
        "description": "Ứng dụng gọi xe"
      }'
```
Body kiểm thử (copy vào Postman):
```json
{
  "appKey": "gotyfi",
  "displayName": "GoTyfi",
  "description": "Ứng dụng gọi xe"
}
```

### 2.2. Phiên bản ứng dụng (App Versions)

**Tạo bản phát hành mới**
```bash
curl -X POST "$BASE_URL/api/control/app-versions" \
  -H "Content-Type: application/json" \
  -d '{
        "appKey": "gotyfi",
        "versionName": "1.2.0",
        "platform": "android",
        "fileUrl": "https://cdn.example.com/gotyfi/v1.2.0.apk",
        "fileChecksum": "c3d4e5f6",
        "releaseNotes": "Fix lỗi đăng nhập, tối ưu UI",
        "releaseDate": "2025-09-17T09:30:00Z"
      }'
```
Body kiểm thử:
```json
{
  "appKey": "gotyfi",
  "versionName": "1.2.0",
  "platform": "android",
  "fileUrl": "https://cdn.example.com/gotyfi/v1.2.0.apk",
  "fileChecksum": "c3d4e5f6",
  "releaseNotes": "Fix lỗi đăng nhập, tối ưu UI",
  "releaseDate": "2025-09-17T09:30:00Z"
}
```

**Liệt kê các bản phát hành**
```bash
curl "$BASE_URL/api/control/app-versions?appKey=gotyfi"
```

**Tra cứu bản phát hành theo ID**
```bash
curl "$BASE_URL/api/control/app-versions/10"
```

**Client kiểm tra cập nhật**
```bash
curl "$BASE_URL/api/control/check-app-version?appKey=gotyfi&currentVersion=1.1.0"
```
Phản hồi mẫu khi có bản mới:
```json
{
  "currentVersion": "1.1.0",
  "serverVersion": "1.2.0",
  "updateAvailable": true,
  "comparisonNote": null,
  "latestRelease": {
    "appVersionId": 42,
    "versionName": "1.2.0",
    "platform": "android",
    "releaseNotes": "Fix lỗi đăng nhập, tối ưu UI",
    "fileUrl": "https://cdn.example.com/gotyfi/v1.2.0.apk",
    "fileChecksum": "c3d4e5f6",
    "releaseDate": "2025-09-17T09:30:00Z"
  }
}
```

### 2.3. Thông báo (Notifications)

**Gửi thông báo JSON tới nhiều ứng dụng**
```bash
curl -X POST "$BASE_URL/api/control/send-notification-json" \
  -H "Content-Type: application/json" \
  -d '{
        "title": "🚀 Cập nhật mới",
        "body": "Đã có phiên bản 1.2.0",
        "link": "https://example.com/changelog",
        "fileBase64": null,
        "fileName": null,
        "targets": [
          { "appKey": "gotyfi", "appVersionId": 42 },
          { "appKey": "gotyfi-driver" }
        ]
      }'
```
Body kiểm thử:
```json
{
  "title": "🚀 Cập nhật mới",
  "body": "Đã có phiên bản 1.2.0",
  "link": "https://example.com/changelog",
  "fileBase64": null,
  "fileName": null,
  "targets": [
    { "appKey": "gotyfi", "appVersionId": 42 },
    { "appKey": "gotyfi-driver" }
  ]
}
```
Phản hồi thành công:
```json
{
  "status": "sent",
  "fileUrl": null,
  "notifications": [
    { "appKey": "gotyfi", "notificationId": 105, "appVersionId": 42 },
    { "appKey": "gotyfi-driver", "notificationId": 106, "appVersionId": null }
  ]
}
```

**Gửi thông báo kèm file đính kèm (multipart/form-data)**
```bash
curl -X POST "$BASE_URL/api/control/send-notification" \
  -F "title=Hướng dẫn sử dụng mới" \
  -F "body=File PDF hướng dẫn sử dụng ứng dụng" \
  -F "id=gotyfi" \
  -F "file=@/path/to/guide.pdf"
```
Trường `id` đại diện cho `appKey`. API sẽ tự chuyển file thành đường dẫn tải và lưu trong bảng `Notifications`.

**Client lấy danh sách thông báo**
```bash
curl "$BASE_URL/api/control/get-notifications?appKey=gotyfi&page=1&pageSize=20"
```
Phản hồi mẫu:
```json
{
  "total": 3,
  "page": 1,
  "pageSize": 20,
  "items": [
    {
      "notificationId": 105,
      "title": "🚀 Cập nhật mới",
      "message": "Đã có phiên bản 1.2.0",
      "createdAt": "2025-09-17T09:35:00Z",
      "link": "https://example.com/changelog",
      "fileUrl": null,
      "appKey": "gotyfi",
      "appName": "GoTyfi",
      "appVersion": {
        "appVersionId": 42,
        "versionName": "1.2.0",
        "platform": "android",
        "releaseNotes": "Fix lỗi đăng nhập, tối ưu UI",
        "fileUrl": "https://cdn.example.com/gotyfi/v1.2.0.apk",
        "fileChecksum": "c3d4e5f6",
        "releaseDate": "2025-09-17T09:30:00Z"
      }
    }
  ]
}
```

**Xoá thông báo**
- Xoá toàn bộ: `curl -X POST "$BASE_URL/api/control/clear-notifications"`
- Xoá theo ứng dụng: `curl -X POST "$BASE_URL/api/control/clear-notifications?appKey=gotyfi"`

**Nhận realtime qua SSE**
```bash
curl "$BASE_URL/api/control/notifications-stream"
```
Luồng trả về từng sự kiện JSON mỗi khi có thông báo mới được ghi vào database.

## 3. Checklist tích hợp

1. Tạo ứng dụng bằng `POST /applications`.
2. Thêm tối thiểu một bản phát hành qua `POST /app-versions` nếu muốn gắn thông báo với cập nhật.
3. Dùng `POST /send-notification-json` để gửi thông báo cho một hoặc nhiều app.
4. Client gọi `GET /get-notifications` để hiển thị danh sách và `GET /check-app-version` khi cần kiểm tra cập nhật.
5. (Tuỳ chọn) kết nối `notifications-stream` để cập nhật realtime.

> **Mẹo**: Bạn có thể lưu các JSON mẫu trong Postman/Insomnia để kiểm thử nhanh mỗi khi triển khai bản mới.
