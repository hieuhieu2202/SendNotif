# RemoteControlApi

Hệ thống cung cấp backend quản lý thông báo và cập nhật ứng dụng cho thiết bị di động. Ứng dụng ASP.NET Core 8.0 sử dụng Entity Framework Core để tự động khởi tạo/ cập nhật schema CSDL khi dịch vụ chạy.

## 1. Kết nối & tự động migration
- **Connection string** được khai báo trong `appsettings*.json` với key `ConnectionStrings:AppDatabase`:
  ```text
  Server=10.220.130.125,1453;Database=SendNoti;User ID=MBD-AIOT;Password=123456ad!;TrustServerCertificate=True
  ```
- Ở `Program.cs`, dịch vụ được cấu hình `UseSqlServer(...)` và luôn gọi `Database.MigrateAsync()` khi khởi động ⇒ mọi migration mới sẽ được áp dụng tự động.
- Migration đầu tiên (`20240717000000_InitialCreate`) tạo bảng; dữ liệu thực tế sẽ do admin hoặc tác vụ nền tự thêm sau khi triển khai.

## 2. Mô hình dữ liệu
Hệ thống gồm hai bảng chính với quan hệ 1-n:

### 2.1 AppVersions
Lưu thông tin mỗi bản phát hành ứng dụng.
```sql
CREATE TABLE AppVersions (
    AppVersionId   INT IDENTITY(1,1) PRIMARY KEY,
    VersionName    NVARCHAR(50)  NOT NULL,
    ReleaseNotes   NVARCHAR(MAX) NULL,
    FileUrl        NVARCHAR(255) NOT NULL,
    FileChecksum   NVARCHAR(128) NULL,
    ReleaseDate    DATETIME2     NOT NULL
);
```

### 2.2 Notifications
Lưu thông báo gửi đến người dùng, có thể gắn với một bản cập nhật cụ thể.
```sql
CREATE TABLE Notifications (
    NotificationId INT IDENTITY(1,1) PRIMARY KEY,
    Title          NVARCHAR(100) NOT NULL,
    Message        NVARCHAR(MAX) NOT NULL,
    Link           NVARCHAR(255) NULL,
    CreatedAt      DATETIME2     NOT NULL,
    AppVersionId   INT           NULL,
    FileUrl        NVARCHAR(255) NULL,
    IsActive       BIT           NOT NULL DEFAULT 1,
    CONSTRAINT FK_Notifications_AppVersions_AppVersionId
        FOREIGN KEY (AppVersionId)
        REFERENCES AppVersions(AppVersionId)
        ON DELETE SET NULL
);
```

### 2.3 Quản lý dữ liệu
Ngay sau khi migration được áp dụng, hệ thống không tự thêm dữ liệu mẫu. Admin chủ động tạo bản ghi `AppVersions` và `Notifications` thông qua dashboard, migration seed riêng hoặc script phù hợp với quy trình vận hành của bạn.

## 3. Luồng chính
1. **Admin phát hành bản mới**
   - Upload gói cài đặt qua `POST /api/control/app-version/upload`.
   - (Tuỳ chọn) tạo thông báo gắn `AppVersionId` tương ứng.
   - Dữ liệu được ghi vào `AppVersions` và `Notifications`.

2. **Admin gửi thông báo thường**
   - Gửi JSON hoặc multipart tới `POST /api/control/send-notification-json` / `send-notification`.
   - Backend lưu bản ghi mới trong `Notifications` (IsActive = 1).

3. **Client lấy danh sách thông báo**
   - Gọi `GET /api/control/get-notifications?page=1&pageSize=20`.
   - Server chỉ trả các bản ghi `IsActive=1`, sắp xếp mới nhất trước và join thông tin phiên bản nếu có.
   - Ví dụ JSON bên dưới chỉ mang tính minh hoạ; dữ liệu thực tế phụ thuộc vào các bản ghi mà admin đã thêm.

4. **Client kiểm tra cập nhật**
   - Gọi `GET /api/control/check-app-version?currentVersion=<phiên bản hiện tại>`.
   - Server đối chiếu với bản phát hành mới nhất trong `AppVersions` để quyết định có update không.

5. **Realtime (tuỳ chọn)**
   - Client mở kết nối SSE tới `GET /api/control/notifications-stream` để nhận thông báo ngay khi admin gửi.

## 4. Tài liệu API
Mọi endpoint đều có sẵn trong Swagger (`/swagger`). Dưới đây là tóm tắt các API chính kèm ví dụ cURL.

### 4.1 API cho Admin/Server

#### Gửi thông báo JSON
```
POST /api/control/send-notification-json
Content-Type: application/json
```
```bash
curl -X POST "https://<host>/api/control/send-notification-json" \
  -H "Content-Type: application/json" \
  -d '{
        "title": "🔧 Bảo trì hệ thống",
        "body": "Hệ thống bảo trì lúc 23h ngày 20/09",
        "fileBase64": null,
        "fileName": null
      }'
```
**Phản hồi**
```json
{
  "status": "Notification received",
  "message": {
    "id": "c6f9...",
    "title": "🔧 Bảo trì hệ thống",
    "body": "Hệ thống bảo trì lúc 23h ngày 20/09",
    "timestampUtc": "2025-09-17T12:34:56.789Z",
    "fileUrl": null
  }
}
```

#### Gửi thông báo multipart (đính kèm tệp)
```
POST /api/control/send-notification
Content-Type: multipart/form-data
```
```bash
curl -X POST "https://<host>/api/control/send-notification" \
  -F "title=🎯 Khuyến mãi" \
  -F "body=Giảm 30% cho người dùng mới" \
  -F "file=@banner.png"
```

#### Xoá toàn bộ thông báo
```bash
curl -X POST "https://<host>/api/control/clear-notifications"
```
Kết quả:
```json
{ "status": "Cleared" }
```

#### Upload bản cài đặt mới
```
POST /api/control/app-version/upload
Content-Type: multipart/form-data
```
```bash
curl -X POST "https://<host>/api/control/app-version/upload" \
  -F "latest=1.3.0" \
  -F "minSupported=1.1.0" \
  -F "notesVi=Thêm tính năng A, tối ưu hiệu năng" \
  -F "platform=android" \
  -F "file=@app-release.apk"
```
Phản hồi chứa thông tin file được lưu, checksum SHA256 và build number mới.

### 4.2 API cho Client/App

#### Lấy danh sách thông báo (có phân trang)
```bash
curl "https://<host>/api/control/get-notifications?page=1&pageSize=2"
```
```json
{
  "total": 3,
  "page": 1,
  "pageSize": 2,
  "items": [
    {
      "notificationId": 4,
      "title": "🔧 Bảo trì hệ thống",
      "message": "Hệ thống sẽ bảo trì 23h ngày 20/09",
      "createdAt": "2025-09-17T12:00:00Z",
      "fileUrl": null,
      "appVersion": null
    },
    {
      "notificationId": 3,
      "title": "⚡ Cập nhật 1.2.0",
      "message": "Fix lỗi đăng nhập + UI dark mode",
      "createdAt": "2025-09-17T09:30:00Z",
      "fileUrl": null,
      "appVersion": {
        "appVersionId": 3,
        "versionName": "1.2.0",
        "releaseNotes": "Fix lỗi đăng nhập, UI tối ưu",
        "fileUrl": "https://example.com/v1.2.0.apk",
        "fileChecksum": "c3d4e5",
        "releaseDate": "2025-09-17T09:30:00Z"
      }
    }
  ]
}
```

#### Nhận thông báo realtime (Server-Sent Events)
```bash
curl -N "https://<host>/api/control/notifications-stream"
```
Server sẽ đẩy từng thông báo dạng:
```
data: {"id":"...","title":"...","body":"...","timestampUtc":"..."}
```

#### Kiểm tra bản cập nhật
```bash
curl "https://<host>/api/control/check-app-version?currentVersion=1.1.0"
```
```json
{
  "currentVersion": "1.1.0",
  "serverVersion": "1.2.0",
  "updateAvailable": true,
  "comparisonNote": null,
  "latestRelease": {
    "appVersionId": 3,
    "versionName": "1.2.0",
    "releaseNotes": "Fix lỗi đăng nhập, UI tối ưu",
    "fileUrl": "https://example.com/v1.2.0.apk",
    "fileChecksum": "c3d4e5",
    "releaseDate": "2025-09-17T09:30:00Z"
  }
}
```

#### Lấy manifest phiên bản hiện tại
```bash
curl "https://<host>/api/control/app-version"
```
Trả về thông tin `latest`, `minSupported`, ghi chú và danh sách file được upload gần nhất.

#### Tải gói cài đặt mới nhất
```bash
curl -OJ "https://<host>/api/control/app-version/download?platform=android"
```
- Có thể gửi `HEAD` để kiểm tra kích thước & checksum trước khi tải:
  ```bash
  curl -I "https://<host>/api/control/app-version/download?platform=android"
  ```

## 5. Ghi chú vận hành
- Tất cả endpoint mặc định không bật HTTPS khi chạy local; nếu deploy reverse proxy hãy cấu hình lại theo môi trường thực tế.
- Thư mục `wwwroot/uploads` chứa file đính kèm trong thông báo, còn `Builds/` chứa các gói ứng dụng upload.
- Khi cần bổ sung bảng hoặc quan hệ mới, hãy tạo migration EF Core rồi deploy; dịch vụ sẽ tự động cập nhật schema nhờ `Database.MigrateAsync()`.

