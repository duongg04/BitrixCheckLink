# BitrixChecker

Ứng dụng nội bộ kiểm tra, quản lý và theo dõi các instance Bitrix24 hoạt động trên domain zone Việt Nam (`*.bitrix24.vn`). Hệ thống hỗ trợ quét tự động, lập danh sách link theo dõi, quy trình xử lý khách hàng tiềm năng cho Sales Team và re-check định kỳ.

---

## Công nghệ sử dụng

- **Backend**: ASP.NET Core 8 Web API, C# 12
- **Database**: MySQL 8.0 với Entity Framework Core 8 (Pomelo MySQL provider)
- **Xác thực & Phân quyền**: ASP.NET Core Identity, JWT Bearer tokens
- **Hàng đợi & Tác vụ ngầm**: Hangfire (MySQL Storage) cho background scan & recurring re-check
- **Xuất dữ liệu**: ClosedXML (XLSX) và định dạng CSV chuẩn UTF-8
- **Frontend**: Giao diện web tĩnh (HTML5, Bootstrap 5, Bootstrap Icons, Vanilla JavaScript) trong `wwwroot`
- **Kiểm thử**: xUnit, .NET Test SDK (39 unit tests)

---

## Cấu trúc thư mục

```
BitrixChecker/
├── Configuration/               # Các class Options: JwtOptions, ScanOptions, SeedAdminOptions
├── Controllers/
│   ├── AuthController.cs        # Đăng nhập, thông tin user, quản lý người dùng (Admin)
│   ├── ScanController.cs        # Kích hoạt scan (số lượng, wordlist, độ dài) & tiến độ
│   ├── LinkController.cs        # Quản lý links, workflow Sales, tracking, export CSV/XLSX, re-check
│   └── NotificationController.cs# Thông báo in-app (lấy danh sách, đánh dấu đã đọc)
├── Data/
│   ├── AppDbContext.cs          # DbContext và cấu hình quan hệ Entity Framework Core
│   └── Migrations/              # Lịch sử các migration EF Core
├── Models/
│   ├── ApplicationUser.cs       # Identity user mở rộng DisplayName
│   ├── ApplicationRole.cs       # Identity roles (Admin, User)
│   ├── LinkResult.cs            # Entity CheckedLinks (chứa Subdomain, Status, IsTracked...)
│   ├── LinkProcessing.cs        # Thông tin xử lý Sales (Status, Assignee, Note)
│   ├── ProcessingHistory.cs     # Lịch sử thay đổi trạng thái và ghi chú xử lý
│   ├── Notification.cs          # Thông báo in-app (Message, Type, IsRead, CreatedAt)
│   ├── ScanJob.cs               # Thông tin phiên quét (TotalExpected, TotalScanned, Status)
│   └── AuditLog.cs              # Nhật ký kiểm toán thao tác hệ thống
├── Services/
│   ├── IdentitySeedService.cs   # Khởi tạo role Admin/User và tài khoản Admin mặc định
│   └── ScanEngine/
│       ├── ISubdomainGenerator.cs & SubdomainGenerator.cs # Sinh candidate tuần tự theo từ điển & wordlist
│       ├── ILinkDetector.cs & LinkDetector.cs             # Kiểm tra HTTP/HTTPS & dấu hiệu Bitrix24
│       ├── ILinkResultSaver.cs & LinkResultSaver.cs       # Lưu kết quả scan vào database an toàn
│       ├── INotificationService.cs & NotificationService.cs # Lưu thông báo in-app vào database
│       ├── IScanJobService.cs & ScanJobService.cs         # Cập nhật trạng thái và tiến độ ScanJob
│       ├── ScanJobs.cs                                    # Hangfire jobs (Generate, Batch, Recheck)
│       └── ScanEngineState.cs                             # Quản lý trạng thái tạm dừng quét toàn hệ thống
├── wwwroot/                     # Giao diện web
│   ├── index.html & script.js   # Dashboard chính, danh sách link, scan panel, modal xử lý Sales
│   ├── login.html & auth.js     # Giao diện và hàm helper xác thực JWT
│   ├── users.html & users.js    # Giao diện quản lý người dùng (Admin)
│   ├── manage.html & manage.js  # Bảng hiển thị nhanh các link đang hoạt động
│   └── favicon.ico
├── Dockerfile & docker-compose.yml # Đóng gói và chạy dịch vụ Docker
├── .env.example                 # Mẫu cấu hình biến môi trường
├── appsettings.json
└── BitrixChecker.csproj

BitrixChecker.Tests/             # Project kiểm thử (39 unit tests)
├── DomainTests.cs               # Kiểm thử model, DTO, validation, options, notification
└── ScanEngineTests.cs           # Kiểm thử LinkDetector, SubdomainGenerator, workflow Sales
```

---

## Cơ chế hoạt động & Scan Engine

1. **Sinh subdomain candidate (`SubdomainGenerator`)**:
   - Sử dụng thuật toán **sinh tuần tự theo thứ tự từ điển (deterministic systematic lexicographical generation)** trên tập ký tự `abcdefghijklmnopqrstuvwxyz0123456789`.
   - Sinh lần lượt từ độ dài nhỏ nhất đến lớn nhất (ví dụ: `aaa`, `aab`, `aac`...), không sinh ngẫu nhiên phân tán, đảm bảo phủ kín không gian tìm kiếm và không bị lặp lại candidate.
   - Hỗ trợ tham số giới hạn số lượng (`quantity` / `maxCandidates`, từ 1 đến 100.000).
   - Khi bật tùy chọn `useWordlist`, danh sách từ khóa được chuẩn hóa (chỉ giữ ký tự `a-z0-9` và gạch ngang) và được ưu tiên đưa vào quét trước.
2. **Kiểm tra instance Bitrix24 (`LinkDetector`)**:
   - Gửi yêu cầu HTTPS GET/HEAD với timeout và User-Agent mô phỏng.
   - Phát hiện instance hoạt động qua HTTP 200 hoặc chuyển hướng hợp lệ (301/302).
   - Phân tích phản hồi tìm dấu hiệu Bitrix24: cookie phiên `BITRIX_SM_`, tiêu đề trang Bitrix24, hoặc nội dung giao diện đặc trưng.
   - Loại trừ các trang lỗi mặc định NXDOMAIN, 404, hoặc 403 do tường lửa/load balancer.
3. **Quản lý tác vụ nền (Hangfire)**:
   - `GenerateScanJob`: Khởi tạo phiên quét theo độ dài, số lượng hoặc wordlist, chia các batch nhỏ và đẩy vào hàng đợi.
   - `CheckBatchJob`: Thực thi kiểm tra song song với mức concurrency cấu hình (`Parallelism`), lưu kết quả vào database.
   - `RecheckJob`: Tác vụ định kỳ (mặc định cron `0 2 * * *` - 2h sáng UTC hàng ngày), tự động kiểm tra lại tất cả các link active, đánh dấu inactive nếu không còn phản hồi và tạo thông báo in-app.
   - `CheckSingleLink`: Tác vụ re-check tức thời cho 1 link cụ thể hoặc chạy hàng loạt theo danh sách được chọn.

---

## Phân quyền hệ thống (Authorization)

Hệ thống phân quyền nghiêm ngặt tại Backend (Role-based JWT Authorization):

| Chức năng | Admin | User (Sales Team) | Ghi chú |
|---|:---:|:---:|---|
| Đăng nhập, xem thông tin cá nhân | ✅ | ✅ | Bắt buộc JWT hợp lệ |
| Xem thống kê tổng quan | ✅ | ✅ | `GET /api/link/stats` |
| Xem danh sách **Active Links** | ✅ | ✅ | Có đầy đủ thông tin `CreatedAt`, `LastChecked` |
| Xem danh sách **Processed Links** | ✅ | ✅ | Hỗ trợ lọc theo trạng thái xử lý |
| Xem danh sách **Inactive Links** / **All** | ✅ | ❌ (403) | Non-admin bị chặn truy cập |
| Cập nhật ghi chú xử lý (Sales Note) | ✅ | ✅ | `PUT /api/link/processing/{id}/note` |
| Thay đổi trạng thái xử lý (Status) & Người phụ trách (Assignee) | ✅ | ❌ (403) | Non-admin không được tự ý đổi pipeline hoặc phân công |
| Đánh dấu / Bỏ đánh dấu theo dõi (`IsTracked`) | ✅ | ✅ | `POST /api/link/track/{id}` |
| Khởi tạo phiên quét subdomain (`quantity`, `wordlist`) | ✅ | ❌ (403) | `POST /api/scan/generate` |
| Xóa hàng loạt link inactive | ✅ | ❌ (403) | `DELETE /api/link/inactive` |
| Re-check thủ công (đơn lẻ hoặc hàng loạt) | ✅ | ❌ (403) | `POST /api/link/recheck-bulk` |
| Xuất danh sách CSV / Excel XLSX | ✅ | ❌ (403) | Hỗ trợ lọc theo status, assignee, dateFrom, dateTo |
| Quản lý người dùng (xem, tạo mới User/Admin) | ✅ | ❌ (403) | `GET /api/auth/users`, `POST /api/auth/register` |
| Truy cập Hangfire Dashboard | ✅ | ❌ (403) | `/hangfire` |
| Xem và đánh dấu đã đọc thông báo in-app | ✅ | ✅ | `GET /api/notifications`, `POST /api/notifications/read` |

---

## Danh sách API Endpoints

### 1. Xác thực & Quản lý người dùng (`/api/auth`)
- `POST /api/auth/login` — Đăng nhập (username, password), trả về JWT token và thông tin roles.
- `GET /api/auth/me` — Lấy thông tin tài khoản hiện tại (yêu cầu JWT).
- `GET /api/auth/users` — Lấy danh sách người dùng trong hệ thống (chỉ Admin).
- `POST /api/auth/register` — Tạo tài khoản người dùng mới với vai trò `Admin` hoặc `User` (chỉ Admin).

### 2. Quét Subdomain (`/api/scan`)
- `POST /api/scan/generate` — Khởi chạy phiên quét mới (chỉ Admin).
  - Body: `{ minLength, maxLength, quantity, useWordlist, wordlist, parallelism, retryCount, retryDelayMs }`
- `GET /api/scan/progress` — Lấy tiến độ phiên quét gần nhất (số đã quét, số dự kiến, trạng thái).

### 3. Quản lý Link & Workflow Sales (`/api/link`)
- `GET /api/link/stats` — Thống kê số lượng (Tổng số, Active, Inactive, Processed, trạng thái Pause).
- `GET /api/link/list` — Danh sách links có phân trang, tìm kiếm, lọc và sắp xếp:
  - Query: `status` (ACTIVE/PROCESSED, Admin có thêm INACTIVE/ALL), `search`, `assigneeId`, `processingStatus`, `dateFrom`, `dateTo`, `sortBy` (subdomain, status, httpCode, createdAt, lastChecked), `sortDir` (asc/desc), `page`.
- `GET /api/link/{id}` — Chi tiết thông tin một link.
- `GET /api/link/processed` — Danh sách các link đang trong quy trình xử lý.
- `GET /api/link/processing-history/{id}` — Lịch sử thay đổi trạng thái và ghi chú của link.
- `PUT /api/link/processing/{id}/note` — Cập nhật ghi chú xử lý khách hàng (Admin & User).
- `PUT /api/link/processing/{id}` — Cập nhật trạng thái xử lý (`New`, `Contacted`, `Negotiating`, `Successful`, `Failed`, `NotPotential`) và người phụ trách (chỉ Admin).
- `POST /api/link/track/{id}` — Bật/tắt trạng thái theo dõi (`IsTracked`) của link.
- `POST /api/link/recheck/{id}` — Đưa 1 link vào hàng đợi re-check ngay (chỉ Admin).
- `POST /api/link/recheck-bulk` — Đưa danh sách link vào hàng đợi re-check hàng loạt (chỉ Admin).
  - Body: `{ linkIds: [1, 2, 3...] }` (tối đa 200 link/lần).
- `DELETE /api/link/inactive` — Xóa mềm tất cả các link đang ở trạng thái Inactive (chỉ Admin).
- `GET /api/link/export` — Xuất danh sách link ra file CSV theo bộ lọc (chỉ Admin).
- `GET /api/link/export-xlsx` — Xuất danh sách link ra file Excel XLSX theo bộ lọc (chỉ Admin).
- `POST /api/link/pause?pause=true|false` — Tạm dừng hoặc tiếp tục hoạt động của engine quét (chỉ Admin).

### 4. Thông báo In-App (`/api/notifications`)
- `GET /api/notifications?limit=20` — Lấy danh sách các thông báo mới nhất kèm số lượng thông báo chưa đọc.
- `POST /api/notifications/read` — Đánh dấu tất cả thông báo hiện tại là đã đọc.

### 5. Hệ thống & Giám sát
- `GET /health` — Kiểm tra tính sẵn sàng của dịch vụ (`{"status":"healthy"}`).
- `/swagger` — Giao diện tài liệu Swagger UI (hỗ trợ thử nghiệm với nút Authorize Bearer JWT).
- `/hangfire` — Dashboard trực quan quản lý tác vụ ngầm và recurring jobs (chỉ Admin).

---

## Cấu hình

### 1. `appsettings.json`
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=bitrix_checker;User=root;Password=your_password;"
  },
  "Jwt": {
    "Key": "your_secure_random_key_at_least_32_characters_long",
    "Issuer": "BitrixChecker",
    "Audience": "BitrixCheckerUsers",
    "ExpiryMinutes": 60
  },
  "Scan": {
    "TargetBaseDomain": "bitrix24.vn",
    "MinLength": 3,
    "MaxLength": 5,
    "UseWordlist": false,
    "RetryCount": 2,
    "RetryDelayMs": 1500,
    "Parallelism": 20,
    "BatchSize": 500,
    "MaxCandidates": 100000,
    "WorkerCount": 6,
    "RecheckCron": "0 2 * * *"
  },
  "SeedAdmin": {
    "UserName": "admin",
    "Email": "admin@bitrixchecker.local",
    "Password": "Admin@Pass123456"
  }
}
```

### 2. Các biến môi trường bắt buộc (khi triển khai)
| Biến | Ý nghĩa |
|---|---|
| `MYSQL_ROOT_PASSWORD` | Mật khẩu tài khoản root của MySQL container |
| `MYSQL_DATABASE` | Tên database (mặc định: `bitrix_checker`) |
| `JWT_KEY` | Khóa bí mật ký token JWT (tối thiểu 32 ký tự) |
| `SEED_ADMIN_USERNAME` | Tên đăng nhập Admin khởi tạo tự động |
| `SEED_ADMIN_EMAIL` | Email của tài khoản Admin |
| `SEED_ADMIN_PASSWORD` | Mật khẩu Admin (tối thiểu 6 ký tự, có chữ hoa, chữ thường, số, ký tự đặc biệt) |

---

## Hướng dẫn cài đặt & Khởi chạy

### Cách 1: Khởi chạy nhanh bằng Docker Compose (Khuyến nghị)

1. Sao chép file cấu hình môi trường mẫu:
   ```bash
   cd BitrixChecker
   cp .env.example .env
   ```
2. Mở file `.env` và thiết lập mật khẩu MySQL, JWT key và mật khẩu Admin.
3. Khởi động hệ thống:
   ```bash
   docker compose up -d --build
   ```
4. Truy cập các địa chỉ:
   - **Giao diện Dashboard chính**: `http://localhost:5000/index.html` (hoặc tự động chuyển hướng từ `http://localhost:5000/`)
   - **Trang đăng nhập**: `http://localhost:5000/login.html`
   - **Quản lý người dùng**: `http://localhost:5000/users.html` (chỉ tài khoản Admin)
   - **Tài liệu Swagger API**: `http://localhost:5000/swagger`
   - **Hangfire Dashboard**: `http://localhost:5000/hangfire` (chỉ tài khoản Admin)
   - **Kiểm tra trạng thái**: `http://localhost:5000/health`

### Cách 2: Chạy trực tiếp trên máy cục bộ (.NET SDK 8.0)

1. Cần cài đặt sẵn MySQL 8.0 và tạo database `bitrix_checker`.
2. Thiết lập chuỗi kết nối và các biến môi trường:
   ```powershell
   $env:ConnectionStrings__DefaultConnection = "Server=localhost;Port=3306;Database=bitrix_checker;User=root;Password=YourPassword;"
   $env:Jwt__Key = "YourSecretKeyMustBeAtLeast32CharsLong!!"
   $env:SeedAdmin__UserName = "admin"
   $env:SeedAdmin__Email = "admin@example.com"
   $env:SeedAdmin__Password = "Admin@Pass123456"
   ```
3. Chạy ứng dụng (database migrations và seed tài khoản Admin sẽ được tự động áp dụng khi khởi động):
   ```bash
   dotnet run --project BitrixChecker
   ```

---

## Kiểm thử tự động (Unit Tests)

Dự án bao gồm bộ kiểm thử đơn vị với **39 test cases** độc lập xác thực tính chính xác của toàn bộ engine và nghiệp vụ:

```bash
dotnet test "Bitrix24 CheckLink.sln" -c Release
```

Nội dung kiểm thử bao gồm:
- Xác thực tập trạng thái chuẩn của quy trình Sales (`ProcessingStatuses`).
- Trạng thái mặc định và tính nhất quán của `LinkResult`, cờ theo dõi `IsTracked`.
- Kiểm tra tính bất biến và lịch sử thay đổi `ProcessingHistory`.
- Ràng buộc dữ liệu DTO `BulkRecheckRequest` và `ScanRequest` (hỗ trợ `Quantity`).
- Khởi tạo entity `Notification` in-app.
- Thuật toán `SubdomainGenerator` tuân thủ nghiêm ngặt giới hạn `maxCandidates` và loại trừ trùng lặp.
- Ràng buộc cấu hình quét an toàn trong `ScanOptions`.
- Quy tắc phát hiện HTTP của `LinkDetector` (chuyển hướng 302 đến auth, 200 trang đăng ký, loại trừ 404/trang lỗi).
- Kiểm soát trạng thái pause/resume của `ScanEngineState`.
