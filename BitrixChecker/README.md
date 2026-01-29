# 🕵️ Bitrix24 Link Checker Tool

Công cụ tự động quét, kiểm tra trạng thái hoạt động của các subdomain Bitrix24 và hỗ trợ quản lý Sales Team.

## 🚀 Tính năng chính

1.  **Quét ngẫu nhiên:** Tạo và kiểm tra hàng loạt subdomain Bitrix24 (Active/Inactive).
2.  **Xử lý đa luồng:** Sử dụng **Hangfire** để xử lý hàng ngàn request mà không làm treo hệ thống.
3.  **Quản lý Sales:** Giao diện Dashboard cho phép Sales update trạng thái, ghi chú khách hàng.
4.  **Tự động hóa:** Job chạy ngầm hàng ngày (00:00) để kiểm tra lại các link đang Active.
5.  **Báo cáo:** Xuất danh sách khách hàng tiềm năng ra file Excel (.csv).

## 🛠 Công nghệ sử dụng

* **Backend:** ASP.NET Core 8.0 Web API
* **Database:** MySQL
* **Background Job:** Hangfire (MySQL Storage)
* **Frontend:** HTML5, CSS3, JavaScript (Vanilla)

## ⚙️ Hướng dẫn Cài đặt & Chạy

### 1. Yêu cầu hệ thống
* .NET SDK 8.0 trở lên
* MySQL Server (XAMPP hoặc MySQL Workbench)

### 2. Cấu hình Database
Mở file `appsettings.json` và cập nhật chuỗi kết nối MySQL của bạn:
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=bitrix_checker;User=root;Password=YOUR_PASSWORD;Allow User Variables=true"
}