async function startScan() {
    const minLen = parseInt(document.getElementById('minLength').value);
    const maxLen = parseInt(document.getElementById('maxLength').value);
    
    const status = document.getElementById('status');
    const btn = document.getElementById('btn-scan');

    if (minLen > maxLen) {
        alert("Độ dài Min không được lớn hơn Max!");
        return;
    }
    
    // Cảnh báo nếu khoảng quá rộng
    if (maxLen >= 5 && !confirm("Cảnh báo: Độ dài từ 5 ký tự trở lên sẽ tạo ra HÀNG CHỤC TRIỆU link. Quá trình này có thể chạy mất nhiều ngày. Bạn chắc chắn muốn quét chứ?")) {
        return;
    }

    status.innerText = "Đang khởi tạo Job...";
    status.className = "";
    btn.disabled = true;

    try {
        const response = await fetch('/api/scan/generate', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ 
                minLength: minLen, 
                maxLength: maxLen
                // Không gửi count nữa
            })
        });

        const data = await response.json();
        
        if (response.ok) {
            status.innerText = "✅ " + data.message;
            status.className = "success";
        } else {
            status.innerText = "❌ Lỗi: " + (data.message || JSON.stringify(data));
            status.className = "error";
        }
    } catch (e) {
        console.error(e);
        status.innerText = "❌ Lỗi kết nối server!";
        status.className = "error";
    } finally {
        setTimeout(() => { btn.disabled = false; }, 2000);
    }
}