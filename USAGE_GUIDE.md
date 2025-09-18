# Hướng dẫn sử dụng hệ thống thông báo & cập nhật ứng dụng

Tài liệu này tóm tắt chức năng, danh sách API và payload mẫu để bạn cấu hình cũng như kiểm thử hệ thống nhanh chóng. Tất cả endpo
int đều nằm dưới `/api/control`.

> **Lưu ý:** Ứng dụng tự gọi `Database.Migrate()` khi khởi động ⇒ chỉ cần cấu hình chuỗi kết nối SQL Server là có thể sử dụng.

## 1. Chức năng chính

| Nhóm | Mô tả |
| --- | --- |
| Quản lý phiên bản | Thêm/sửa đổi thông tin bản phát hành (link tải, checksum, ghi chú, ngày phát hành). |
| Gửi thông báo | Gửi thông báo thường hoặc gắn với một `appVersionId`, có thể đính kèm file. |
| Lấy thông báo | Client truy vấn danh sách thông báo đang kích hoạt. |
| Kiểm tra cập nhật | Client cung cấp phiên bản hiện có để so sánh với bản mới nhất trên server. |
| Dọn dữ liệu | Xoá toàn bộ thông báo (tuỳ chọn khi cần reset). |

## 2. API và ví dụ kiểm thử
Giả sử đặt `BASE_URL=https://your-host`.

### 2.1. Phiên bản ứng dụng (AppVersions)
**Thêm bản phát hành mới**
```bash
curl -X POST "$BASE_URL/api/control/app-versions" \
  -H "Content-Type: application/json" \
  -d '{
        "versionName": "1.2.0",
        "releaseNotes": "Fix lỗi đăng nhập, tối ưu UI",
        "fileUrl": "https://cdn.example.com/app/v1.2.0.apk",
        "fileChecksum": "c3d4e5",
        "releaseDate": "2025-09-17T09:30:00Z"
      }'
```

**Liệt kê các bản phát hành**
```bash
curl "$BASE_URL/api/control/app-versions"
```

**Tra cứu bản phát hành theo ID**
```bash
curl "$BASE_URL/api/control/app-versions/3"
```

**Client kiểm tra cập nhật**
```bash
curl "$BASE_URL/api/control/check-app-version?currentVersion=1.1.0"
```
Phản hồi khi có bản mới:
```json
{
  "currentVersion": "1.1.0",
  "serverVersion": "1.2.0",
  "updateAvailable": true,
  "comparisonNote": null,
  "latestRelease": {
    "appVersionId": 3,
    "versionName": "1.2.0",
    "releaseNotes": "Fix lỗi đăng nhập, tối ưu UI",
    "fileUrl": "https://cdn.example.com/app/v1.2.0.apk",
    "fileChecksum": "c3d4e5",
    "releaseDate": "2025-09-17T09:30:00Z"
  }
}
```

### 2.2. Thông báo (Notifications)
**Gửi thông báo JSON**
```bash
curl -X POST "$BASE_URL/api/control/send-notification-json" \
  -H "Content-Type: application/json" \
  -d '{
        "title": "⚡ Cập nhật 1.2.0",
        "body": "Fix lỗi đăng nhập + UI dark mode",
        "link": "https://example.com/changelog",
        "appVersionId": 3
      }'
```

**Gửi thông báo kèm file (multipart/form-data)**
```bash
curl -X POST "$BASE_URL/api/control/send-notification" \
  -F "title=Hướng dẫn sử dụng mới" \
  -F "body=Tệp PDF hướng dẫn sử dụng ứng dụng" \
  -F "appVersionId=" \
  -F "file=@/path/to/guide.pdf"
```

**Client lấy danh sách thông báo**
```bash
curl "$BASE_URL/api/control/get-notifications?page=1&pageSize=20"
```
Phản hồi mẫu:
```json
{
  "total": 2,
  "page": 1,
  "pageSize": 20,
  "items": [
    {
      "notificationId": 3,
      "title": "⚡ Cập nhật 1.2.0",
      "message": "Fix lỗi đăng nhập + UI dark mode",
      "createdAt": "2025-09-17T09:30:00Z",
      "link": "https://example.com/changelog",
      "fileUrl": null,
      "isActive": true,
      "appVersion": {
        "appVersionId": 3,
        "versionName": "1.2.0",
        "releaseNotes": "Fix lỗi đăng nhập, tối ưu UI",
        "fileUrl": "https://cdn.example.com/app/v1.2.0.apk",
        "fileChecksum": "c3d4e5",
        "releaseDate": "2025-09-17T09:30:00Z"
      }
    },
    {
      "notificationId": 4,
      "title": "🔧 Bảo trì hệ thống",
      "message": "Hệ thống sẽ bảo trì 23h ngày 20/09",
      "createdAt": "2025-09-17T12:00:00Z",
      "link": null,
      "fileUrl": null,
      "isActive": true
    }
  ]
}
```

**Xoá toàn bộ thông báo**
```bash
curl -X POST "$BASE_URL/api/control/clear-notifications"
```

## 3. Checklist tích hợp nhanh
1. Cấu hình chuỗi kết nối SQL Server và chạy dịch vụ ⇒ migration tự áp dụng.
2. Thêm bản phát hành đầu tiên bằng `POST /app-versions` (tuỳ chọn, nếu muốn thông báo cập nhật).
3. Gửi thông báo qua `POST /send-notification-json` hoặc multipart.
4. Ứng dụng client gọi `GET /get-notifications` và `GET /check-app-version` khi cần.

> **Mẹo:** Lưu các payload JSON mẫu vào Postman/Insomnia để tái sử dụng khi triển khai các môi trường khác nhau.
