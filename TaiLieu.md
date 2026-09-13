Đề bài phát triển ứng dụng: Công cụ kiểm tra và quản lý các instance Bitrix24 trên domain .vn
1. Giới thiệu và mục tiêu dự án
Bạn là đối tác chính thức của Bitrix24 tại Việt Nam. Để hỗ trợ hoạt động kinh doanh, thẩm định tiềm năng khách hàng và đánh giá thị trường, cần phát triển một ứng dụng web nội bộ sử dụng NestJS (backend) nhằm tự động phát hiện và kiểm tra các instance Bitrix24 cloud đang hoạt động trên domain zone Việt Nam (*.bitrix24.vn).
Mục tiêu chính:
•	Tạo ngẫu nhiên các subdomain tiềm năng (ví dụ: abc.bitrix24.vn, companyname.bitrix24.vn...).
•	Kiểm tra xem subdomain đó có tồn tại và đang hoạt động không bằng cách gửi request HTTPS và phân tích response (không cần render full browser, chỉ cần HEAD/GET request để kiểm tra status code và nội dung đặc trưng của Bitrix24).
•	Lưu trữ và quản lý danh sách các link đã kiểm tra.
•	Hỗ trợ re-check định kỳ các link đang hoạt động.
•	Theo dõi quá trình xử lý của Sales Team đối với các instance tiềm năng.
Ứng dụng này là công cụ nội bộ, chỉ dành cho team nội bộ sử dụng.
2. Công nghệ yêu cầu
•	Backend: NestJS (TypeScript) – cấu trúc modular, sử dụng TypeORM hoặc Prisma cho database.
•	Database: PostgreSQL (hoặc MySQL).
•	Frontend: Có thể sử dụng React/Vue/Angular, hoặc đơn giản là NestJS kết hợp với Swagger + simple admin UI (EJS hoặc riêng frontend). Ưu tiên có giao diện web dễ sử dụng.
•	Authentication: Hệ thống đăng nhập đơn giản (JWT) với ít nhất 2 role: Admin (quản lý toàn bộ) và User (Sales Team – chỉ xem và cập nhật ghi chú xử lý).
•	Queue/Background jobs: Sử dụng BullMQ hoặc NestJS Bull để xử lý việc generate và check link hàng loạt mà không block server.
•	HTTP Client: Axios hoặc built-in NestJS HttpService, hỗ trợ timeout, retry.
•	Scheduling: @nestjs/schedule cho các task định kỳ re-check.
3. Các chức năng chính
3.1. Quản lý người dùng & Đăng nhập
•	Trang đăng nhập (username/password).
•	Phân quyền: Admin có đầy đủ quyền, User chỉ xem danh sách active links và cập nhật ghi chú xử lý.
3.2. Tạo và kiểm tra link ngẫu nhiên
•	Giao diện để cấu hình và chạy task generate subdomain:
o	Độ dài subdomain (ví dụ: 5-15 ký tự).
o	Số lượng subdomain cần generate và check mỗi lần (ví dụ: 1000-5000).
o	Phương thức generate: ngẫu nhiên chữ cái + số (a-z, 0-9, có thể thêm gạch ngang).
o	Có thể thêm danh sách từ khóa (wordlist) để generate thông minh hơn (ví dụ: tên công ty phổ biến, từ tiếng Việt/Anh liên quan doanh nghiệp).
•	Chạy background job để:
o	Generate list subdomain.
o	Check từng link bằng HTTPS GET/HEAD request đến https://<subdomain>.bitrix24.vn.
o	Tiêu chí xác định tồn tại và hoạt động:
	Status code 200 OK (hoặc 301/302 redirect hợp lệ).
	Response header hoặc body chứa dấu hiệu của Bitrix24 (ví dụ: có chứa "bitrix" trong header, hoặc title chứa "Bitrix24", hoặc có cookie Bitrix cụ thể).
	Không phải error page mặc định (NXDOMAIN, 404, 403 từ load balancer).
o	Lưu kết quả vào database.
3.3. Quản lý danh sách link
•	Dashboard hiển thị thống kê: Tổng link đã check, số active, số inactive, số đang xử lý.
•	Các tab/table riêng biệt:
o	Active Links: Danh sách các link đang hoạt động tốt.
	Hiển thị: subdomain, ngày phát hiện, ngày check gần nhất, status hiện tại.
	Cho phép đánh dấu "Đang theo dõi" và re-check thủ công.
o	Inactive Links: Danh sách link không tồn tại hoặc không hoạt động.
	Có thể xóa hàng loạt để dọn dẹp.
o	Processed Links (dành cho Sales Team): Danh sách các active link đã được Sales xử lý.
	Mỗi link có fields:
	Ghi chú xử lý (textarea).
	Trạng thái xử lý (dropdown: Đã liên hệ, Đang đàm phán, Thành công, Thất bại, Không tiềm năng...).
	Người xử lý (assign user).
	Ngày cập nhật.
	Filter theo trạng thái, người xử lý, ngày.
3.4. Re-check định kỳ
•	Cấu hình cron job (admin setting):
o	Tự động re-check tất cả active links hàng ngày/tuần.
o	Nếu link không còn hoạt động → chuyển sang inactive và thông báo (email hoặc in-app notification).
•	Re-check thủ công cho từng link hoặc hàng loạt.
3.5. Các tính năng bổ sung
•	Export danh sách active/processed ra CSV/Excel.
•	Logging đầy đủ các hoạt động check.
•	Rate limiting khi check để tránh bị block IP (delay giữa các request, proxy nếu cần sau này).
•	Thông báo (email hoặc dashboard) khi phát hiện link active mới.
4. Yêu cầu phi chức năng
•	Hiệu suất: Có thể xử lý check hàng nghìn link mà không crash (sử dụng queue).
•	Bảo mật: HTTPS cho ứng dụng, hash password (bcrypt), JWT expire.
•	Deploy: Dễ deploy trên VPS/Docker, có Dockerfile.
•	Code quality: Clean code, comment rõ ràng, unit test cho core logic (generate & check).
5. Giao hàng & Milestone gợi ý
•	Milestone 1: Setup NestJS project, auth, database schema, basic UI dashboard.
•	Milestone 2: Implement generate subdomain + background check job.
•	Milestone 3: Quản lý danh sách link (active/inactive/processed).
•	Milestone 4: Re-check định kỳ + export + polish UI.
•	Tài liệu: README chi tiết cách chạy, API docs (Swagger).

