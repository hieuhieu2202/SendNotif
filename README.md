# RemoteControlApi

Hệ thống backend gửi thông báo và quản lý cập nhật ứng dụng cho nhiều sản phẩm khác nhau (đa ứng dụng tương tự GoTyfi). Ứng dụng được phát triển với ASP.NET Core 8, Entity Framework Core và tự động áp dụng migration ngay khi dịch vụ khởi động.

## 1. Kết nối CSDL & Migration tự động
- Chuỗi kết nối SQL Server được cấu hình trong `appsettings*.json` với key `ConnectionStrings:AppDatabase`. Ví dụ:
  ```text
  Server=10.220.130.125,1453;Database=SendNoti;User ID=MBD-AIOT;Password=123456ad!;TrustServerCertificate=True
  ```
- `Program.cs` đăng ký `AppDbContext` sử dụng `UseSqlServer(...)` và gọi `Database.MigrateAsync()` khi ứng dụng khởi động ⇒ mọi thay đổi schema (migration) sẽ được áp dụng tự động. Ứng dụng không seed dữ liệu mẫu: bạn cần tạo ứng dụng, phiên bản và thông báo thật sau khi triển khai.

## 2. Mô hình dữ liệu đa ứng dụng
Hệ thống hỗ trợ nhiều ứng dụng độc lập. Mỗi ứng dụng có bộ bản phát hành và thông báo riêng.

### 2.1 Bảng `Applications`
Lưu thông tin định danh của từng ứng dụng.
```sql
CREATE TABLE Applications (
    ApplicationId INT IDENTITY(1,1) PRIMARY KEY,
    AppKey        NVARCHAR(100) NOT NULL UNIQUE,   -- định danh dạng "gotyfi", "myapp"
    DisplayName   NVARCHAR(150) NOT NULL,
    Description   NVARCHAR(500) NULL,
    CreatedAt     DATETIME2     NOT NULL,
    IsActive      BIT           NOT NULL DEFAULT 1
);
```

### 2.2 Bảng `AppVersions`
Quản lý các bản phát hành cho từng ứng dụng/ nền tảng.
```sql
CREATE TABLE AppVersions (
    AppVersionId INT IDENTITY(1,1) PRIMARY KEY,
    ApplicationId INT NOT NULL FOREIGN KEY REFERENCES Applications(ApplicationId) ON DELETE CASCADE,
    VersionName  NVARCHAR(50)  NOT NULL,
    Platform     NVARCHAR(30)  NULL,             -- ví dụ: android, ios
    ReleaseNotes NVARCHAR(MAX) NULL,
    FileUrl      NVARCHAR(255) NOT NULL,         -- link tải gói cài đặt
    FileChecksum NVARCHAR(128) NULL,             -- SHA256 để kiểm tra
    ReleaseDate  DATETIME2     NOT NULL,
    CONSTRAINT UK_AppVersion UNIQUE (ApplicationId, VersionName, Platform)
);
```

### 2.3 Bảng `Notifications`
Lưu thông báo gửi tới người dùng của từng ứng dụng.
```sql
CREATE TABLE Notifications (
    NotificationId INT IDENTITY(1,1) PRIMARY KEY,
    ApplicationId  INT NOT NULL FOREIGN KEY REFERENCES Applications(ApplicationId) ON DELETE CASCADE,
    AppVersionId   INT NULL FOREIGN KEY REFERENCES AppVersions(AppVersionId) ON DELETE NO ACTION,
    Title          NVARCHAR(100) NOT NULL,
    Message        NVARCHAR(MAX) NOT NULL,
    Link           NVARCHAR(255) NULL,
    FileUrl        NVARCHAR(255) NULL,
    CreatedAt      DATETIME2 NOT NULL,
    IsActive       BIT NOT NULL DEFAULT 1
);
```
Mỗi thông báo gắn với đúng một ứng dụng. Nếu cần gửi cùng nội dung cho nhiều app, API sẽ nhân bản và ghi nhiều bản ghi vào bảng `Notifications` (mỗi bản ghi ứng với một `AppKey`). Trên SQL Server, ràng buộc `AppVersionId` sử dụng `ON DELETE NO ACTION` để tránh lỗi "multiple cascade paths", nên hãy xoá hoặc gỡ liên kết thông báo trước khi xoá bản phát hành liên quan.

## 3. Luồng nghiệp vụ chính
1. **Tạo ứng dụng mới**
   - Gọi `POST /api/control/applications` để đăng ký `AppKey` và tên hiển thị.
   - Sau khi tạo thành công, tất cả các API còn lại đều yêu cầu tham số `appKey` để định danh ứng dụng.

2. **Phát hành phiên bản ứng dụng**
   - Gọi `POST /api/control/app-versions` với `appKey`, `versionName`, `platform`, `fileUrl`, `releaseDate`,… để lưu bản phát hành.
   - API đảm bảo không trùng `versionName` trong cùng một ứng dụng + nền tảng.

3. **Gửi thông báo**
   - Gửi request JSON tới `POST /api/control/send-notification-json` chứa tiêu đề, nội dung và danh sách ứng dụng nhận (`targets`).
   - Một thông báo có thể gửi cho nhiều ứng dụng trong cùng request; backend tự tạo bản ghi riêng cho từng app và kiểm tra `appVersionId` (nếu có) phải thuộc ứng dụng tương ứng.
   - Tệp đính kèm tuỳ chọn (`fileBase64`, `fileName`). Backend lưu file tại `wwwroot/uploads` và trả về `fileUrl` để client tải.

4. **Ứng dụng client lấy thông báo**
   - Gọi `GET /api/control/get-notifications?appKey=<app>` để nhận danh sách thông báo đang kích hoạt, có phân trang.
   - Nếu thông báo gắn bản cập nhật, phản hồi sẽ chứa block `appVersion` (versionName, releaseNotes, fileUrl, …) để client hiển thị nút cập nhật.

5. **Ứng dụng kiểm tra cập nhật**
   - Gọi `GET /api/control/check-app-version?appKey=<app>&currentVersion=<phiên bản hiện tại>`.
   - API trả về `updateAvailable`, `serverVersion` và thông tin chi tiết bản phát hành mới nhất của ứng dụng đó.

## 4. Tài liệu API
Swagger khả dụng tại `/swagger` sau khi dịch vụ chạy. Bảng dưới tóm tắt các endpoint quan trọng cùng ví dụ sử dụng.

### 4.1 Quản lý ứng dụng
- **Danh sách ứng dụng**
  ```bash
  curl "https://<host>/api/control/applications"
  ```
- **Tạo ứng dụng**
  ```bash
  curl -X POST "https://<host>/api/control/applications" \
    -H "Content-Type: application/json" \
    -d '{
          "appKey": "gotyfi",
          "displayName": "GoTyfi",
          "description": "Ứng dụng đặt xe"
        }'
  ```

### 4.2 Quản lý phiên bản
- **Thêm bản phát hành**
  ```bash
  curl -X POST "https://<host>/api/control/app-versions" \
    -H "Content-Type: application/json" \
    -d '{
          "appKey": "gotyfi",
          "versionName": "1.2.0",
          "platform": "android",
          "fileUrl": "https://cdn.example.com/gotyfi/v1.2.0.apk",
          "releaseNotes": "Sửa lỗi đăng nhập",
          "releaseDate": "2025-09-17T09:30:00Z"
        }'
  ```
- **Liệt kê bản phát hành của một ứng dụng**
  ```bash
  curl "https://<host>/api/control/app-versions?appKey=gotyfi"
  ```
- **Kiểm tra cập nhật trên client**
  ```bash
  curl "https://<host>/api/control/check-app-version?appKey=gotyfi&currentVersion=1.1.0"
  ```

### 4.3 Gửi & nhận thông báo
- **Gửi thông báo tới nhiều ứng dụng**
  ```bash
  curl -X POST "https://<host>/api/control/send-notification-json" \
    -H "Content-Type: application/json" \
    -d '{
          "title": "🚀 Cập nhật mới",
          "body": "Đã có phiên bản 1.2.0",
          "link": "https://example.com/changelog",
          "fileBase64": null,
          "fileName": null,
          "targets": [
            { "appKey": "gotyfi", "appVersionId": 5 },
            { "appKey": "gotyfi-driver" }
          ]
        }'
  ```
  *Lưu ý:* Nếu `targets` chỉ chứa một ứng dụng, bạn có thể cung cấp `appVersionId` để thông báo hiển thị chi tiết bản cập nhật tương ứng.

- **Lấy danh sách thông báo cho một ứng dụng**
  ```bash
  curl "https://<host>/api/control/get-notifications?appKey=gotyfi&page=1&pageSize=20"
  ```
  Phản hồi:
  ```json
  {
    "total": 2,
    "page": 1,
    "pageSize": 20,
    "items": [
      {
        "notificationId": 42,
        "title": "🚀 Cập nhật mới",
        "message": "Đã có phiên bản 1.2.0",
        "createdAt": "2025-09-17T09:35:00Z",
        "link": "https://example.com/changelog",
        "fileUrl": null,
        "appKey": "gotyfi",
        "appName": "GoTyfi",
        "appVersion": {
          "appVersionId": 5,
          "versionName": "1.2.0",
          "platform": "android",
          "releaseNotes": "Sửa lỗi đăng nhập",
          "fileUrl": "https://cdn.example.com/gotyfi/v1.2.0.apk",
          "releaseDate": "2025-09-17T09:30:00Z"
        }
      }
    ]
  }
  ```

- **Xoá thông báo**
  ```bash
  curl -X POST "https://<host>/api/control/clear-notifications?appKey=gotyfi"
  ```

## 5. Giao diện tĩnh (tuỳ chọn)
Thư mục `wwwroot` chứa:
- `send.html`: bảng điều khiển gửi thông báo, cho phép chọn nhiều ứng dụng, tự động tải danh sách phiên bản khi chọn 1 ứng dụng.
- `receive.html`: giao diện xem thông báo và kiểm tra cập nhật theo từng ứng dụng.

Các trang này chỉ là công cụ hỗ trợ quản trị viên, mọi dữ liệu thực tế vẫn do bạn thêm thông qua API hoặc giao diện riêng của hệ thống.
