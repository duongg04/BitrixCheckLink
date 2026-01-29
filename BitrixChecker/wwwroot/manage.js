// Biến toàn cục theo dõi trạng thái
let currentPage = 1;
let currentStatus = 'ACTIVE'; 
let isSystemPaused = false;
let pendingCount = 0; // Theo dõi số lượng pending để xử lý nút Export

// Khởi chạy khi load trang
document.addEventListener('DOMContentLoaded', () => {
    loadStats();
    loadList('ACTIVE', 1);
});

// Tự động làm mới dữ liệu mỗi 3 giây
setInterval(() => {
    loadStats();
    const isModalOpen = document.getElementById('editModal').style.display === 'block';
    if (currentPage === 1 && !isModalOpen) {
        loadListSilent(currentStatus, 1);
    }
}, 3000);

async function loadListSilent(status, page) {
    try {
        const res = await fetch(`/api/link/list?status=${status}&page=${page}`);
        if (!res.ok) return;
        
        const responseData = await res.json(); 
        const data = responseData.data;
        const totalRecord = responseData.total;

        const currentTotalText = document.getElementById('pageInfo').innerText;
        if (!currentTotalText.includes(totalRecord)) {
             loadList(status, page); 
        }
    } catch (e) { }
}

// Hàm 1: Load thống kê & Cập nhật trạng thái nút bấm
async function loadStats() {
    try {
        const res = await fetch('/api/link/stats');
        const data = await res.json();
        
        if(document.getElementById('st-total')) document.getElementById('st-total').innerText = data.total;
        if(document.getElementById('st-active')) document.getElementById('st-active').innerText = data.active;
        
        pendingCount = data.pending !== undefined ? data.pending : 0;
        if(document.getElementById('st-pending')) document.getElementById('st-pending').innerText = pendingCount;
        if(document.getElementById('st-processed')) document.getElementById('st-processed').innerText = data.processed;

        isSystemPaused = data.isPaused;
        
        // --- CẬP NHẬT TRẠNG THÁI CÁC NÚT DỰA TRÊN TÌNH TRẠNG HỆ THỐNG ---
        updateButtonStates();

    } catch (e) { console.error("Lỗi load stats:", e); }
}

function updateButtonStates() {
    const btnPause = document.getElementById('btnPauseToggle');
    const btnClearPending = document.getElementById('btnClearPending');
    const btnClearActive = document.getElementById('btnClearActive');
    const btnExport = document.getElementById('btnExport');

    // 1. Nút Tạm Dừng
    if (btnPause) {
        if (isSystemPaused) {
            btnPause.innerText = "▶️ TIẾP TỤC";
            btnPause.style.background = "#fd7e14"; 
            btnPause.style.border = "1px solid #e86b02";
            btnPause.style.animation = "blink 1s infinite";
        } else {
            btnPause.innerText = "⏸️ TẠM DỪNG";
            btnPause.style.background = "#17a2b8"; 
            btnPause.style.border = "1px solid #117a8b";
            btnPause.style.animation = "none";
        }
    }

    // 2. Nút Xóa Pending & Xóa Active & Export
    // Quy tắc: Chỉ được ấn khi ĐANG TẠM DỪNG (isSystemPaused = true)
    // Riêng nút Export: Có thể ấn nếu Pending = 0 (quét xong) kể cả khi chưa pause.
    
    if (isSystemPaused) {
        // Đang tạm dừng -> Mở khóa các nút
        if(btnClearPending) { btnClearPending.disabled = false; btnClearPending.style.opacity = "1"; btnClearPending.style.cursor = "pointer"; }
        if(btnClearActive) { btnClearActive.disabled = false; btnClearActive.style.opacity = "1"; btnClearActive.style.cursor = "pointer"; }
        if(btnExport) { btnExport.classList.remove('btn-disabled'); }
    } else {
        // Đang chạy -> Khóa các nút nguy hiểm
        if(btnClearPending) { btnClearPending.disabled = true; btnClearPending.style.opacity = "0.5"; btnClearPending.style.cursor = "not-allowed"; }
        if(btnClearActive) { btnClearActive.disabled = true; btnClearActive.style.opacity = "0.5"; btnClearActive.style.cursor = "not-allowed"; }
        
        // Nút Export đặc biệt: Nếu chưa pause nhưng đã quét xong (pending=0) thì vẫn cho export
        if (pendingCount === 0 && btnExport) {
             btnExport.classList.remove('btn-disabled');
        } else if (btnExport) {
             btnExport.classList.add('btn-disabled');
        }
    }
}

async function togglePause() {
    const newState = !isSystemPaused;
    try {
        const res = await fetch(`/api/link/pause?pause=${newState}`, { method: 'POST' });
        const data = await res.json();
        // Không alert để trải nghiệm mượt hơn, nút sẽ tự đổi màu
        loadStats(); 
    } catch(e) {
        alert("Lỗi kết nối!");
    }
}

const style = document.createElement('style');
style.innerHTML = `
  @keyframes blink { 0% { opacity: 1; } 50% { opacity: 0.5; } 100% { opacity: 1; } }
  a.btn-disabled { pointer-events: none; opacity: 0.6; cursor: not-allowed; background: #ccc !important; border-color: #aaa !important; }
`;
document.head.appendChild(style);

async function loadList(status, page = 1) {
    if (status !== currentStatus) {
        currentStatus = status;
        currentPage = 1;
    } else {
        currentPage = page;
    }

    document.getElementById('pageIndicator').innerText = currentPage;
    const btnPrev = document.getElementById('btnPrev');
    if(btnPrev) btnPrev.disabled = (currentPage === 1);

    const tbody = document.getElementById('table-body');
    const pageInfo = document.getElementById('pageInfo');
    
    tbody.innerHTML = '<tr><td colspan="6" style="text-align:center">Đang tải dữ liệu...</td></tr>';
    if(pageInfo) pageInfo.innerText = 'Đang tính toán...';

    try {
        const res = await fetch(`/api/link/list?status=${currentStatus}&page=${currentPage}`);
        if (!res.ok) throw new Error("Lỗi API");
        
        const responseData = await res.json(); 
        const data = responseData.data; 
        const totalRecord = responseData.total;

        tbody.innerHTML = '';

        if (data.length === 0) {
            tbody.innerHTML = '<tr><td colspan="6" style="text-align:center">Không có dữ liệu</td></tr>';
            if(pageInfo) pageInfo.innerText = '0 - 0 trên 0 dòng';
            const btnNext = document.getElementById('btnNext');
            if(btnNext) btnNext.disabled = true;
            return;
        }

        data.forEach(item => {
            const badgeClass = item.status === 'ACTIVE' ? 'bg-green' : 'bg-red';
            const row = `
                <tr>
                    <td><b>${item.subdomain}</b></td>
                    <td><a href="${item.fullUrl}" target="_blank">${item.fullUrl}</a></td>
                    <td><span class="badge ${badgeClass}">${item.status}</span></td>
                    <td>${item.saleNote || '-'}</td>
                    <td>${item.saleStatus}</td>
                    <td>
                        <button class="btn-sm" style="background:#17a2b8" onclick="openEdit(${item.id}, '${item.saleNote || ''}', '${item.saleStatus}')">✏️ Edit</button>
                    </td>
                </tr>
            `;
            tbody.innerHTML += row;
        });

        const pageSize = 50;
        const startRecord = (currentPage - 1) * pageSize + 1;
        const endRecord = startRecord + data.length - 1;

        if(pageInfo) {
            pageInfo.innerText = `Hiển thị ${startRecord} - ${endRecord} trên ${totalRecord} dòng`;
        }

        const btnNext = document.getElementById('btnNext');
        if(btnNext) {
            btnNext.disabled = endRecord >= totalRecord; 
        }

    } catch (e) {
        console.error("Lỗi:", e);
        tbody.innerHTML = '<tr><td colspan="6" style="text-align:center; color:red">Lỗi tải dữ liệu</td></tr>';
    }
}

function changePage(step) {
    const newPage = currentPage + step;
    if (newPage < 1) return;
    loadList(currentStatus, newPage);
}

function openEdit(id, note, status) {
    document.getElementById('editId').value = id;
    document.getElementById('editNote').value = note;
    document.getElementById('editStatus').value = status;
    document.getElementById('editModal').style.display = 'block';
}

function closeModal() {
    document.getElementById('editModal').style.display = 'none';
}

async function saveSalesUpdate() {
    const id = document.getElementById('editId').value;
    const body = {
        note: document.getElementById('editNote').value,
        status: document.getElementById('editStatus').value,
        user: 'SalesAdmin'
    };
    
    try {
        await fetch(`/api/link/update-sales/${id}`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body)
        });
        closeModal();
        loadList(currentStatus, currentPage);
        loadStats();
    } catch(e) {
        alert("Lỗi cập nhật!");
    }
}

// Hàm Xóa Dữ Liệu (Đã cập nhật logic kiểm tra)
async function deleteData(status) {
    // Kiểm tra an toàn: Phải Pause trước mới được xóa (trừ khi gọi từ code nội bộ)
    if (!isSystemPaused) {
        alert("Vui lòng ấn 'Tạm Dừng' hệ thống trước khi thực hiện thao tác xóa!");
        return;
    }

    let confirmMsg = "";
    
    if (status === 'PENDING') {
        confirmMsg = "⚠️ BẠN MUỐN XÓA SẠCH PENDING?\n\n- Toàn bộ danh sách chờ sẽ bị xóa.\n- Các link ACTIVE đã tìm được vẫn được GIỮ NGUYÊN.\n\nBạn chắc chắn chứ?";
    } 
    else if (status === 'ACTIVE') {
        // Logic nhắc nhở xuất file Excel
        const exportConfirm = confirm("⚠️ QUAN TRỌNG: Bạn đã xuất file Excel lưu dữ liệu chưa?\n\nNếu xóa Active bây giờ, dữ liệu sẽ mất vĩnh viễn và không thể khôi phục.\n\nNhấn OK nếu bạn ĐÃ XUẤT FILE và muốn xóa.\nNhấn Cancel để quay lại xuất file.");
        if (!exportConfirm) return; // Người dùng chọn Cancel để đi xuất file

        confirmMsg = "🛑 XÁC NHẬN CUỐI CÙNG: Xóa toàn bộ Active?";
    }
    
    if (!confirm(confirmMsg)) return;
    
    document.body.style.cursor = 'wait';

    try {
        const res = await fetch(`/api/link/delete?status=${status}`, { method: 'DELETE' });
        
        if (!res.ok) throw new Error("Lỗi API");
        
        const data = await res.json();
        
        alert(data.message); 
        loadStats(); 
        if (status === 'ACTIVE') loadList('ACTIVE', 1);
        
    } catch(e) {
        alert("Có lỗi xảy ra!");
        console.error(e);
    } finally {
        document.body.style.cursor = 'default';
    }
}

// Hàm Xuất Excel (Có kiểm tra điều kiện)
function exportExcel() {
    if (!isSystemPaused && pendingCount > 0) {
        alert("Hệ thống đang chạy quét! Vui lòng ấn 'Tạm Dừng' hoặc đợi quét xong Pending thì mới được xuất file.");
        return;
    }
    // Chuyển hướng để tải file
    window.location.href = "/api/link/export";
}