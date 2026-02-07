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
git clone https://gitlab.com/username-cua-ban/bitrix-checker.git
cd bitrix-checker

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

