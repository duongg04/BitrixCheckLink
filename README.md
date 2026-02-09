# Bitrix24 Checker Tool

Công cụ tự động quét và kiểm tra trạng thái hoạt động của các tên miền con (subdomain) Bitrix24 bằng phương pháp vét cạn (Brute-force). Hệ thống chạy trên Docker, đảm bảo tính nhất quán và dễ dàng triển khai.

Tech Stack: .NET 9.0 | Docker | MySQL 8.0 | Hangfire

## Tinh nang chinh

* Quét Vét Cạn: Tạo và kiểm tra hàng loạt subdomain theo độ dài tùy chọn.
* Xử lý Đa luồng: Sử dụng Hangfire để chạy nền hiệu suất cao.
* Lưu trữ: Kết quả (link sống/chết) được lưu tự động vào Database MySQL.
* Dễ dàng triển khai: Chỉ cần 1 lệnh duy nhất với Docker.

---

## Yeu cau cai dat (Prerequisites)

Bạn KHÔNG CẦN cài đặt .NET, MySQL hay Visual Studio. Bạn chỉ cần:
1. Docker Desktop (Đã cài và đang chạy).
2. Git (Để tải code về).

---

## Huong dan chay (Quick Start)

### Buoc 1: Tai du an ve may
Mở Terminal và chạy lệnh:
git clone https://github.com/duongg04/BitrixCheckLink.git
cd BitrixCheckLink

### Buoc 2: Chay ung dung
Chạy lệnh sau để tự động cài đặt và khởi động server:
docker-compose up -d --build

### Buoc 3: Truy cap
Sau khoảng 1-2 phút, bạn có thể truy cập:
* Trang chủ (Web Scan): http://localhost:5000
* Trang quản lý tiến độ (Hangfire): http://localhost:5000/hangfire

---

## Quan ly Database (MySQL)

Hệ thống sử dụng MySQL trong Docker. Thông tin kết nối để xem dữ liệu:
* Host: localhost
* Port: 3307
* Username: root
* Password: root
* Database: checked_link

(Gợi ý: Dùng DBeaver, HeidiSQL hoặc Extension Database Client trong VS Code để kết nối).

---

## Cau truc Project

* Dockerfile: Cấu hình môi trường chạy Web App (.NET 9).
* docker-compose.yml: Cấu hình tổ chức dịch vụ (Web + MySQL).
* Program.cs: Code khởi chạy ứng dụng.
* Services/: Chứa logic xử lý.

---

## Cac loi thuong gap (Troubleshooting)

1. Lỗi "Ports are not available":
   Tắt các phần mềm đang chiếm cổng 5000 hoặc 3307, hoặc đổi cổng trong file docker-compose.yml.

2. Web App báo lỗi kết nối Database khi khởi động:
   Hệ thống sẽ tự thử lại (Retry), chỉ cần đợi 10-20 giây.

3. Lỗi 401 khi vào Hangfire:
   Đã được xử lý sẵn trong code (AllowAllAuthorizationFilter).

---

## Tu chinh Hieu suat & Cau hinh (Advanced Configuration)

Để tối ưu hóa tốc độ quét trên máy cấu hình mạnh hoặc mạng nhanh, bạn có thể thay đổi các thông số sau trong file code `Program.cs` trước khi chạy lệnh build.

### 1. Tang toc do quet (So luong luong chay song song)
File cần sửa: `Program.cs`
Tìm đoạn: `builder.Services.AddHangfireServer`

* Thông số: `options.WorkerCount = 6;`
* Giải thích: Đây là số lượng tác vụ chạy cùng một lúc.
* Gợi ý:
  - Máy yếu (RAM 4GB): Để 4 hoặc 6.
  - Máy mạnh (RAM 16GB+, CPU nhiều nhân): Có thể tăng lên 12, 20 hoặc cao hơn để quét nhanh gấp đôi/gấp ba.

### 2. Cau hinh Mang & Timeout (Xu ly mang cham/nhanh)
File cần sửa: `Program.cs`
Tìm đoạn: `builder.Services.AddHttpClient`

* Thông số: `client.Timeout = TimeSpan.FromSeconds(10);`
  - Nếu mạng của bạn rất chậm hoặc chập chờn: Hãy tăng lên 20 hoặc 30 giây để tránh bị báo lỗi "Time out" sai.
  - Nếu mạng cáp quang xịn: Giữ nguyên 10s hoặc giảm xuống 5s để bỏ qua nhanh các link chết.

* Thông số: `MaxConnectionsPerServer = 1000;`
  - Đây là số lượng kết nối tối đa mở ra cùng lúc. Nếu tăng `WorkerCount` lên cao (ví dụ 50), bạn nên kiểm tra xem số này có đủ lớn không (thường 1000 là dư sức).

### 3. Cau hinh Database (Ket noi MySQL)
File cần sửa: `Program.cs`
Tìm đoạn: `var connectionStringWithPool`

* Thông số: `Max Pool Size=100;`
  - Nếu bạn tăng `WorkerCount` lên rất cao (trên 50), hãy tăng số này lên tương ứng (ví dụ 200) để tránh lỗi Database bị quá tải kết nối.

### 4. Thay doi Port (Neu bi trung cong)
File cần sửa: `docker-compose.yml`

* Dịch vụ Web:
  `ports: - "5000:8080"` -> Đổi số 5000 thành số khác (ví dụ 8000) nếu máy bạn đã cài phần mềm khác dùng cổng 5000.
* Dịch vụ MySQL:
  `ports: - "3307:3306"` -> Đổi số 3307 thành số khác nếu cần.

Lưu ý: Sau khi sửa bất kỳ thông số nào trong code, bạn BẮT BUỘC phải chạy lại lệnh sau để áp dụng thay đổi:
docker-compose up -d --build
