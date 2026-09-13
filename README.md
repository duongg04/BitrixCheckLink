# BitrixChecker

Ứng dụng nội bộ kiểm tra và quản lý các instance Bitrix24. Backend dùng ASP.NET Core 8, MySQL 8, ASP.NET Identity, JWT và Hangfire.

## Kiến trúc chương trình

Dự án được chia thành 4 giai đoạn phát triễn:

| Giai đoạn | Nội dung | Trạng thái |
|---|---|---|
| **Giai đoạn 1** | Nền tảng: Domain models (User, ScanJob, LinkResult, LinkProcessing, AuditLog), Authentication/Authorization, Swagger, cấu hình môi trường | ✅ Hoàn thành |
| **Giai đoạn 2** | Engine quét thông minh: SubdomainGenerator, LinkDetector, LinkResultSaver, 4 Hangfire job | ✅ Hoàn thành |
| **Giai đoạn 3** | Quản lý dữ liệu & workflow Sales: 3 nhóm link (Active/Inactive/Processed), filter/search/sort, export CSV, lịch sử thay đổi | ✅ Hoàn thành |
| **Giai đoạn 4** | Re-check định kỳ, thông báo, logging, bảo mật production, unit tests | ✅ Hoàn thành |

## Cấu trúc thư mục

```
BitrixChecker/
├── Configuration/         # ScanOptions, JwtOptions, SeedAdminOptions
├── Controllers/
│   ├── AuthController.cs     # Đăng nhập, lấy token
│   ├── ScanController.cs      # API kích hoạt scan (Admin)
│   ├── LinkController.cs      # Quản lý link & workflow Sales
├── Data/
│   ├── AppDbContext.cs        # DbContext + config EF
│   └── Migrations/            # Migration files
├── Models/
│   ├── ApplicationUser.cs     # User (Identity) + DisplayName
│   ├── ScanJob.cs             # Tracking job
│   ├── LinkResult.cs          # Kết quả scan (map với bảng CheckedLinks)
│   ├── LinkProcessing.cs      # Workflow Sales
│   ├── ProcessingHistory.cs   # Lịch sử thay đổi
│   ├── AuditLog.cs            # Audit log
│   └── LinkStatuses.cs        # Hằng số ACTIVE/INACTIVE
├── Services/
│   ├── BitrixService.cs
│   ├── IdentitySeedService.cs
│   └── ScanEngine/
│       ├── SubdomainGenerator     # Sinh candidate
│       ├── LinkDetector           # HTTP detect active
│       ├── LinkResultSaver        # Lưu kết quả
│       ├── NotificationService    # Thông báo
│       ├── ScanJobService         # Tracking DB
│       └── ScanJobs               # 4 Hangfire job
├── wwwroot/                  # Static files (login.html)
├── .env.example              # Template env vars
├── appsettings*.json
├── Dockerfile + docker-compose.yml
└── BitrixChecker.csproj

BitrixChecker.Tests/        # Unit tests (35 tests)
```

## Domain Models

### LinkResult → bảng `CheckedLinks`
| Field | Type | Mô tả |
|---|---|---|
| Id | int | Primary key |
| Subdomain | string(100) | Tên subdomain (unique) |
| FullUrl | string(2048) | URL đầy đủ |
| Status | string(20) | "ACTIVE" hoặc "INACTIVE" |
| HttpCode | int? | HTTP status code |
| CreatedAt/LastChecked | DateTime | Timestamp |
| ResponseFingerprint | string(2000) | Nội dung response |
| IsDeleted | bool | Soft delete |

### LinkProcessing (workflow Sales)
| Field | Type | Mô tả |
|---|---|---|
| LinkResultId | int | FK → LinkResult |
| AssignedUserId | string | Người phụ trách |
| Note | string(1000) | Ghi chú |
| Status | string | New/Contacted/Negotiating/Successful/Failed/NotPotential |
| UpdatedAt | DateTime | Cập nhật lúc |
| UpdatedByUserId | string | Người cập nhật |

### ProcessingHistory
Lưu lịch sử mọi thay đổi của LinkProcessing (status, note, assignee).

### ScanJob
| Field | Type | Mô tả |
|---|---|---|
| Name/JobType | string | Tên + loại job |
| Status | string | Pending/Running/Completed/Failed |
| TotalExpected/TotalScanned | int | Số lượng |
| CreatedAt/StartedAt/CompletedAt | DateTime | Thời gian |

## Phân quyền

| Vai trò | Quyền |
| --- | --- |
| **Admin** | Tạo user, khởi động scan, xem toàn bộ danh sách/thống kê, quản lý xử lý Sales, archive link, export CSV, truy cập Hangfire dashboard |
| **User** | Đăng nhập, chỉ xem link `ACTIVE`, cập nhật ghi chú xử lý cho link active |

Mọi API, ngoại trừ đăng nhập và health check, đều yêu cầu JWT. API đăng nhập giới hạn 10 lần/phút/IP; ASP.NET Identity khóa tài khoản 15 phút sau 5 lần đăng nhập sai.

## API Endpoints

### Auth
- `POST /api/auth/login` — đăng nhập, nhận JWT

### Scan (Admin)
- `POST /api/scan/generate` — khởi động scan với cấu hình (min/max length, wordlist, parallelism, retry)

### Link (Admin/User)
- `GET /api/link/stats` — thống kê (Admin: full, User: ACTIVE only)
- `GET /api/link/list` — danh sách links (có filter/search/sort/pagination)
  - Params: `status` (ALL/ACTIVE/INACTIVE/PROCESSED), `search`, `assigneeId`, `sortBy`, `sortDir`, `dateFrom`, `dateTo`, `page`
- `GET /api/link/processed` — danh sách links đã qua Sales team xử lý
- `GET /api/link/processing-history/{id}` — lịch sử thay đổi của một link
- `PUT /api/link/processing/{id}/note` — cập nhật ghi chú (Admin/User)
- `PUT /api/link/processing/{id}` — cập nhật status + assignee (Admin)
- `POST /api/link/pause?pause=true` — tạm dừng hệ thống (Admin)
- `DELETE /api/link/inactive` — xóa links inactive (Admin)
- `GET /api/link/export` — export CSV (Admin)

### Other
- `/swagger` — API docs (Bearer JWT)
- `/hangfire` — Hangfire dashboard (Admin only)
- `GET /health` — `200 OK { status: "healthy" }`

## Quy trình Scan Engine (4 Hangfire job)

1. **GenerateScanJob** — sinh candidate subdomains (random + wordlist), tạo ScanJob record, enqueue CheckBatchJob
2. **CheckBatchJob** — kiểm tra HTTP, lưu active links, enqueue NotificationJob
3. **RecheckJob** — cron hàng ngày 2h sáng UTC, re-check active links, chuyển INACTIVE + thông báo
4. **NotificationJob** — log thông báo khi phát hiện link mới hoặc link mất

## Cấu hình

### appsettings.json
```json
{
  "Scan": {
    "TargetBaseDomain": "bitrix24.vn",
    "MinLength": 5,
    "MaxLength": 15,
    "WordlistPath": "",
    "UseWordlist": false,
    "RetryCount": 2,
    "RetryDelayMs": 1500,
    "Parallelism": 20,
    "BatchSize": 500,
    "MaxCandidates": 100000,
    "WorkerCount": 6,
    "RecheckCron": "0 2 * * *"
  }
}
```

### Biến môi trường bắt buộc
| Biến | Mô tả |
|---|---|
| `ConnectionStrings__DefaultConnection` | MySQL connection string |
| `Jwt__Key` | Secret key (≥32 ký tự) |
| `Jwt__Issuer` / `Jwt__Audience` | JWT issuer/audience |
| `SeedAdmin__UserName` / `SeedAdmin__Email` / `SeedAdmin__Password` | Admin seed (≥12 ký tự, có hoa/thường/số/đặc biệt) |

## Chạy Dự án

### Docker
```bash
cp BitrixChecker/.env.example BitrixChecker/.env
# Edit .env with real secrets
cd BitrixChecker && docker compose --env-file .env up --build -d
```
Truy cập: `http://localhost:5000/swagger`, `http://localhost:5000/health`

### Local
```powershell
$env:ConnectionStrings__DefaultConnection = 'Server=localhost;Database=bitrix_checker;User=root;Password=yourpass'
$env:Jwt__Key = 'your-32-char-secret-key'
# ... thiết lập biến môi trường khác
dotnet run --project BitrixChecker
```

## Bảo mật

- Security headers: X-Content-Type-Options, X-Frame-Options, X-XSS-Protection, Referrer-Policy, Permissions-Policy
- CORS: chỉ `https://bitrix24.vn` được phép
- Rate limiting: 10 req/phút/IP cho auth endpoint
- HTTPS bắt buộc ở Production
- Không có secret hay mật khẩu mặc định trong code

## Logging

Console logging với structured logs. Filter: `Microsoft=Warning`, `BitrixChecker=Information`.

## Test

```powershell
dotnet test
```
35 unit tests: ProcessingStatuses, LinkResult defaults, ProcessingHistory, SubdomainGenerator, ScanOptions validation, LinkDetector rules (302/200/404/registration), status transitions, ScanEngineState pause/resume, audit log properties, request DTO mapping.
