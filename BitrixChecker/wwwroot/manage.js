let currentPage = 1;
let total = 0;

document.addEventListener('DOMContentLoaded', async () => {
    const user = await requireAuth();
    if (!user) return;

    if (user.roles && user.roles.includes('Admin')) {
        const adminActions = document.getElementById('admin-actions');
        if (adminActions) adminActions.classList.remove('d-none');
    }

    const prevBtn = document.getElementById('previous');
    if (prevBtn) prevBtn.onclick = () => load(currentPage - 1);

    const nextBtn = document.getElementById('next');
    if (nextBtn) nextBtn.onclick = () => load(currentPage + 1);

    await load(1);
});

async function load(page) {
    if (page < 1) return;

    const tableBody = document.getElementById('links');
    if (tableBody) tableBody.innerHTML = '<tr><td colspan="5" class="text-center py-4"><div class="spinner-border spinner-border-sm text-primary" role="status"></div> Đang tải...</td></tr>';

    const response = await api(`/api/link/list?status=ACTIVE&page=${page}`);
    if (!response.ok) {
        if (tableBody) tableBody.innerHTML = response.status === 403
            ? '<tr><td colspan="5" class="text-center text-danger py-4">Bạn không có quyền xem trang này.</td></tr>'
            : '<tr><td colspan="5" class="text-center text-danger py-4">Không thể tải danh sách. Vui lòng thử lại.</td></tr>';
        return;
    }

    const result = await response.json();
    currentPage = page;
    total = result.total || 0;

    const data = result.data || [];
    const body = document.getElementById('links');
    if (!body) return;
    body.replaceChildren();

    if (!data.length) {
        body.innerHTML = '<tr><td colspan="5" class="text-center text-muted py-4">Không có link hoạt động nào.</td></tr>';
        const emptySummary = document.getElementById('summary');
        if (emptySummary) emptySummary.textContent = `${total} link đang hoạt động`;
        return;
    }

    for (const item of data) {
        const row = document.createElement('tr');

        const subdomain = document.createElement('td');
        subdomain.textContent = item.subdomain;

        const url = document.createElement('td');
        const link = document.createElement('a');
        link.href = item.fullUrl;
        link.target = '_blank';
        link.rel = 'noopener noreferrer';
        link.className = 'url-cell d-inline-block';
        link.title = item.fullUrl;
        link.textContent = item.fullUrl;
        url.append(link);

        const code = document.createElement('td');
        code.textContent = item.httpCode ?? '-';

        const checked = document.createElement('td');
        checked.textContent = item.lastChecked ? new Date(item.lastChecked).toLocaleString('vi-VN') : '-';

        const noteCell = document.createElement('td');
        const note = document.createElement('textarea');
        note.className = 'form-control form-control-sm mb-1';
        note.maxLength = 1000;
        note.value = item.note || '';

        const save = document.createElement('button');
        save.className = 'btn btn-primary btn-sm';
        save.textContent = 'Lưu ghi chú';
        save.onclick = async () => {
            save.disabled = true;
            const originalText = save.textContent;
            save.textContent = 'Đang lưu...';
            try {
                const r = await api(`/api/link/processing/${item.id}/note`, {
                    method: 'PUT',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ note: note.value })
                });
                if (!r.ok) {
                    alert(r.status === 403 ? 'Bạn không có quyền sửa ghi chú.' : 'Không thể lưu ghi chú.');
                } else {
                    alert('Đã lưu ghi chú.');
                }
            } catch {
                alert('Lỗi kết nối. Không thể lưu ghi chú.');
            } finally {
                save.disabled = false;
                save.textContent = originalText;
            }
        };

        noteCell.append(note, save);
        row.append(subdomain, url, code, checked, noteCell);
        body.append(row);
    }

    const summary = document.getElementById('summary');
    if (summary) summary.textContent = `${total} link đang hoạt động`;

    const pageSpan = document.getElementById('page');
    if (pageSpan) pageSpan.textContent = `Trang ${currentPage}`;

    const prevBtn = document.getElementById('previous');
    if (prevBtn) prevBtn.disabled = currentPage === 1;

    const nextBtn = document.getElementById('next');
    if (nextBtn) nextBtn.disabled = currentPage * (result.pageSize || 50) >= total;
}

// Export CSV with the sessionStorage Bearer token attached (window.open cannot send the header).
async function exportCsv() {
    try {
        const r = await api('/api/link/export?status=ACTIVE');
        if (!r.ok) {
            alert('Không thể xuất CSV.');
            return;
        }
        const blob = await r.blob();
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `bitrix_links_active_${new Date().toISOString().slice(0, 10)}.csv`;
        document.body.appendChild(a);
        a.click();
        a.remove();
        URL.revokeObjectURL(url);
    } catch {
        alert('Lỗi kết nối khi xuất CSV.');
    }
}
