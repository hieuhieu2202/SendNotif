# RemoteControlApi

Backend ASP.NET Core 8 quản lý thông báo và cập nhật ứng dụng đa nền tảng. Dịch vụ dùng Entity Framework Core để tự chạy
`Database.Migrate()` lúc khởi động và cung cấp bộ API REST cho quản trị viên cũng như ứng dụng di động.

## 1. Cấu hình & khởi động
- Chuỗi kết nối mặc định trỏ tới SQL Server nội bộ: `Server=10.220.130.125,1453;Database=SendNoti;User ID=MBD-AIOT;Password=123456ad!;TrustServerCertificate=True`.
- Có thể override bằng `appsettings.json`, `appsettings.Development.json` hoặc biến môi trường `ConnectionStrings__AppDatabase`.
- `Program.cs` đăng ký `AppDbContext` với `UseSqlServer(...)`, thêm singleton `NotificationStream` để phát realtime và luôn gọi
  `Database.MigrateAsync()` trước khi phục vụ request ⇒ không cần chạy migration thủ công.

## 2. Thiết kế cơ sở dữ liệu

```
Applications (AppKey duy nhất, DisplayName, Description, CreatedAt, IsActive)
    ├─< AppVersions (VersionName, Platform, FileUrl, ReleaseNotes, ReleaseDate, FileChecksum)
    └─< Notifications (Title, Message, Link, FileUrl, IsActive, CreatedAt, AppVersionId?)
```

| Bảng | Mô tả | Ràng buộc chính |
| --- | --- | --- |
| **Applications** | Danh mục ứng dụng bạn quản lý (ví dụ: khách hàng, tài xế). | `AppKey` duy nhất, lưu chữ thường để truy vấn nhanh. |
| **AppVersions** | Bản phát hành của từng ứng dụng. | `(ApplicationId, VersionName)` duy nhất, lưu `Platform`, link tải, checksum. |
| **Notifications** | Thông báo gửi tới người dùng. | Bắt buộc gắn `ApplicationId`, có thể gắn `AppVersionId` (FK `SET NULL`). |

Tất cả các mốc thời gian dùng `DateTime.UtcNow`. Xoá ứng dụng ⇒ cascade xuống phiên bản và thông báo. Xoá bản phát hành ⇒ những
thông báo gắn kèm tự rớt liên kết (`AppVersionId = NULL`).

## 3. Luồng nghiệp vụ chính
1. **Đăng ký ứng dụng** – `POST /api/control/applications` tạo `appKey` mới. Các request khác luôn dùng `appKey` đã đăng ký.
2. **Phát hành phiên bản** – `POST /api/control/app-versions` với `appKey` tương ứng, cung cấp `versionName`, `fileUrl`, `releaseDate`...
3. **Gửi thông báo** – `POST /api/control/send-notification-json` truyền `title`, `body` và mảng `targets` gồm `{ appKey, appVersionId? }`.
   - Có thể đính kèm file qua `fileBase64` hoặc sử dụng endpoint multipart với trường `id=<appKey>`.
   - API lưu bản ghi vào CSDL, phát sự kiện SSE tới mọi client đang nghe và trả về danh sách `notificationId` đã tạo.
4. **Ứng dụng client**
   - Gọi `GET /api/control/get-notifications?appKey=...` để lấy danh sách phân trang kèm block `appVersion` nếu có cập nhật.
   - Gọi `GET /api/control/check-app-version?appKey=...&currentVersion=...` khi khởi động để kiểm tra bản mới nhất.
   - (Tuỳ chọn) mở `EventSource` tới `/api/control/notifications-stream?appKey=...` để nhận realtime.
5. **Dọn dữ liệu** – `POST /api/control/clear-notifications` xoá toàn bộ hoặc truyền `appKey` để xoá theo ứng dụng.

## 4. Danh sách endpoint
Tất cả nằm dưới `/api/control`.

| Endpoint | Phương thức | Mô tả |
| --- | --- | --- |
| `/applications` | GET | Danh sách ứng dụng, kèm tổng số bản phát hành/thông báo. |
| `/applications` | POST | Tạo ứng dụng mới với `appKey`, `displayName`, `description`. |
| `/app-versions` | GET | Liệt kê bản phát hành (lọc bằng `appKey` nếu cần). |
| `/app-versions/{id}` | GET | Lấy chi tiết một bản phát hành. |
| `/app-versions` | POST | Thêm bản phát hành cho một ứng dụng. |
| `/check-app-version` | GET | Client cung cấp `appKey`, `currentVersion` để kiểm tra bản mới nhất. |
| `/send-notification-json` | POST | Gửi thông báo JSON tới nhiều ứng dụng, hỗ trợ đính kèm file Base64. |
| `/send-notification` | POST | Gửi thông báo dạng multipart, trường `id` là `appKey` (có thể phân tách bởi dấu phẩy). |
| `/get-notifications` | GET | Lấy danh sách thông báo theo `appKey`, có phân trang. |
| `/clear-notifications` | POST | Xoá thông báo (toàn bộ hoặc truyền `appKey`). |
| `/notifications-stream` | GET | SSE realtime, trả từng sự kiện JSON khi có thông báo mới. |

Chi tiết payload mẫu xem trong [`USAGE_GUIDE.md`](./USAGE_GUIDE.md).

## 5. Giao diện quản trị tĩnh
Thư mục `wwwroot` chứa hai trang hỗ trợ thao tác nhanh:
- `send.html` – Dashboard gửi thông báo, quản lý `appKey`, thêm target nhiều ứng dụng, tải danh sách phiên bản.
- `receive.html` – Bảng theo dõi thông báo theo `appKey`, hỗ trợ kiểm tra phiên bản và xem log realtime.

Các trang này chỉ dùng cho kiểm thử/manual QA; sản phẩm thực tế nên tích hợp API vào hệ thống quản trị riêng.
