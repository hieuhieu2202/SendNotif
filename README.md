# RemoteControlApi

Backend ASP.NET Core 8 quản lý thông báo và cập nhật ứng dụng. Ứng dụng sử dụng Entity Framework Core để tự tạo/cập nhật cơ sở
 dữ liệu SQL Server khi khởi động và cung cấp các API thuần tuý để quản trị viên thêm bản phát hành, gửi thông báo cũng như để
 client kiểm tra phiên bản mới.

## 1. Kết nối CSDL & Migration tự động
- Chuỗi kết nối mặc định: `Server=10.220.130.125,1453;Database=SendNoti;User ID=MBD-AIOT;Password=123456ad!;TrustServerCertific
ate=True`.
- Có thể thay đổi trong `appsettings.json`, `appsettings.Development.json` hoặc biến môi trường `ConnectionStrings__AppDatabase`.
- `Program.cs` đăng ký `AppDbContext` với `UseSqlServer(...)` và gọi `Database.MigrateAsync()` ngay khi dịch vụ khởi động ⇒ mọi
migration được áp dụng tự động, không cần chạy tay.

## 2. Mô hình dữ liệu tổng quan
Hệ thống gồm hai bảng chính giống với tài liệu yêu cầu:

### 2.1 Bảng `AppVersions`
Lưu các phiên bản ứng dụng đã phát hành.
```sql
CREATE TABLE AppVersions (
    AppVersionId  INT PRIMARY KEY IDENTITY(1,1),
    VersionName   NVARCHAR(50)  NOT NULL,
    ReleaseNotes  NVARCHAR(MAX) NULL,
    FileUrl       NVARCHAR(255) NOT NULL,
    FileChecksum  NVARCHAR(128) NULL,
    ReleaseDate   DATETIME2     NOT NULL
);
```
- Ràng buộc: `VersionName` là duy nhất để tránh trùng bản phát hành.

### 2.2 Bảng `Notifications`
Quản lý thông báo gửi tới toàn bộ người dùng. Một thông báo có thể liên kết với một bản cập nhật cụ thể hoặc chỉ là thông báo thường.
```sql
CREATE TABLE Notifications (
    NotificationId INT PRIMARY KEY IDENTITY(1,1),
    Title          NVARCHAR(100) NOT NULL,
    Message        NVARCHAR(MAX) NOT NULL,
    Link           NVARCHAR(255) NULL,
    CreatedAt      DATETIME2     NOT NULL,
    AppVersionId   INT           NULL,
    FileUrl        NVARCHAR(255) NULL,
    IsActive       BIT           NOT NULL DEFAULT 1,
    CONSTRAINT FK_Notifications_AppVersions
        FOREIGN KEY (AppVersionId) REFERENCES AppVersions(AppVersionId)
        ON DELETE SET NULL
);
```
- Trường `AppVersionId` có thể để trống. Nếu bản cập nhật bị xoá, thông báo sẽ tự động gỡ liên kết (giá trị về `NULL`).

## 3. Flow nghiệp vụ chính
1. **Admin phát hành ứng dụng mới**
   - Gửi `POST /api/control/app-versions` để thêm bản ghi vào `AppVersions` (VersionName, ReleaseNotes, FileUrl, ReleaseDate...).
   - Gửi tiếp `POST /api/control/send-notification-json` với `appVersionId` để thông báo người dùng về bản cập nhật.

2. **Admin tạo thông báo thường**
   - Gửi `POST /api/control/send-notification-json` chỉ với `title`, `body` (có thể kèm `link`, `fileBase64`).
   - Hệ thống lưu bản ghi vào `Notifications` với `AppVersionId = NULL`.

3. **Client lấy danh sách thông báo**
   - Gọi `GET /api/control/get-notifications` để lấy danh sách đang kích hoạt (`IsActive = 1`) sắp xếp theo thời gian mới nhất.
   - Nếu `AppVersionId` khác `NULL`, phản hồi sẽ chứa block `appVersion` với thông tin bản cập nhật.

4. **Client hiển thị thông báo**
   - Nếu phản hồi có `appVersion` ⇒ hiển thị banner cập nhật + nút tải về.
   - Nếu không có ⇒ hiển thị thông báo thường.

5. **Client kiểm tra phiên bản khi khởi động**
   - Gọi `GET /api/control/check-app-version?currentVersion=...`.
   - API so sánh với bản phát hành mới nhất (`AppVersions`) và trả về `updateAvailable` cùng thông tin bản mới nhất.

## 4. Danh sách API
Tất cả endpoint đều nằm dưới `/api/control`. Ví dụ bên dưới sử dụng `BASE_URL=https://your-host`.

### 4.1 Quản lý phiên bản (`AppVersions`)
| Endpoint | Mô tả |
| --- | --- |
| `GET /app-versions` | Liệt kê toàn bộ bản phát hành, sắp xếp mới nhất trước. |
| `GET /app-versions/{id}` | Lấy chi tiết một bản phát hành. |
| `POST /app-versions` | Thêm bản phát hành mới. |
| `GET /check-app-version?currentVersion=1.1.0` | Client gửi version hiện có để kiểm tra bản mới. |

**Ví dụ tạo bản phát hành**
```bash
curl -X POST "$BASE_URL/api/control/app-versions" \
  -H "Content-Type: application/json" \
  -d '{
        "versionName": "1.2.0",
        "fileUrl": "https://cdn.example.com/app/v1.2.0.apk",
        "fileChecksum": "c3d4e5",
        "releaseNotes": "Fix lỗi đăng nhập, tối ưu UI",
        "releaseDate": "2025-09-17T09:30:00Z"
      }'
```

### 4.2 Thông báo (`Notifications`)
| Endpoint | Mô tả |
| --- | --- |
| `POST /send-notification-json` | Gửi thông báo dạng JSON. Trường `appVersionId` tuỳ chọn. |
| `POST /send-notification` | Gửi thông báo dạng multipart (kèm file nhị phân). |
| `GET /get-notifications?page=1&pageSize=20` | Lấy danh sách thông báo đang kích hoạt. |
| `POST /clear-notifications` | Xoá toàn bộ thông báo. |

**Ví dụ gửi thông báo gắn bản cập nhật**
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

**Ví dụ phản hồi khi client lấy thông báo**
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
      "appVersion": {
        "appVersionId": 3,
        "versionName": "1.2.0",
        "releaseNotes": "Fix lỗi đăng nhập, UI tối ưu",
        "fileUrl": "https://example.com/v1.2.0.apk",
        "fileChecksum": "c3d4e5",
        "releaseDate": "2025-09-17T09:30:00Z"
      }
    },
    {
      "notificationId": 4,
      "title": "🔧 Bảo trì hệ thống",
      "message": "Hệ thống sẽ bảo trì 23h ngày 20/09",
      "createdAt": "2025-09-17T12:00:00Z"
    }
  ]
}
```

## 5. Giao diện hỗ trợ quản trị
Thư mục `wwwroot` cung cấp hai trang tĩnh:
- `send.html`: form gửi thông báo nhanh (nhập tiêu đề, nội dung, tuỳ chọn chọn bản cập nhật và đính kèm file).
- `receive.html`: bảng điều khiển xem thông báo, lọc theo thời gian và kiểm tra phiên bản.

Các trang này chỉ là công cụ tham khảo. Bạn có thể tích hợp trực tiếp các API trên vào hệ thống riêng của mình.
