let currentUser = null;

async function init() {
    const user = await requireAuth();
    if (!user) return;

    if (!user.roles || !user.roles.includes('Admin')) {
        alert('Chỉ quản trị viên (Admin) mới có quyền truy cập trang quản lý người dùng.');
        location.assign('/index.html');
        return;
    }

    currentUser = user;
    const whoami = document.getElementById('whoami');
    if (whoami) {
        whoami.textContent = `${user.displayName || user.userName} (${user.roles.join(', ')})`;
    }

    await loadUsers();
}

async function loadUsers() {
    const table = document.getElementById('users-table');
    if (!table) return;

    try {
        const r = await api('/api/auth/users');
        if (!r.ok) {
            table.innerHTML = '<tr><td colspan="5" class="text-center text-danger py-4">Không thể tải danh sách người dùng.</td></tr>';
            return;
        }

        const users = await r.json();
        if (!users || !users.length) {
            table.innerHTML = '<tr><td colspan="5" class="text-center text-muted py-4">Chưa có người dùng nào.</td></tr>';
            return;
        }

        table.innerHTML = users.map(u => `
            <tr>
                <td><strong>${escapeHtml(u.userName || '-')}</strong></td>
                <td>${escapeHtml(u.displayName || '-')}</td>
                <td>${escapeHtml(u.email || '-')}</td>
                <td>${(u.roles || []).map(r => `
                    <span class="badge ${r === 'Admin' ? 'bg-primary' : 'bg-secondary'}">${escapeHtml(r)}</span>
                `).join(' ')}</td>
                <td><code class="text-muted small">${escapeHtml(u.id || '-')}</code></td>
            </tr>
        `).join('');
    } catch {
        table.innerHTML = '<tr><td colspan="5" class="text-center text-danger py-4">Lỗi kết nối khi tải danh sách người dùng.</td></tr>';
    }
}

async function createUser(event) {
    event.preventDefault();
    const errorEl = document.getElementById('create-error');
    if (errorEl) errorEl.classList.add('d-none');

    const userName = document.getElementById('new-username')?.value.trim();
    const password = document.getElementById('new-password')?.value;
    const displayName = document.getElementById('new-displayname')?.value.trim();
    const email = document.getElementById('new-email')?.value.trim();
    const role = document.getElementById('new-role')?.value || 'User';

    if (!userName || !password) {
        if (errorEl) {
            errorEl.textContent = 'Vui lòng điền tên đăng nhập và mật khẩu.';
            errorEl.classList.remove('d-none');
        }
        return;
    }

    const btn = document.getElementById('btn-save-user');
    if (btn) {
        btn.disabled = true;
        btn.innerHTML = '<span class="spinner-border spinner-border-sm"></span> Đang tạo...';
    }

    try {
        const r = await api('/api/auth/register', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                userName,
                password,
                displayName: displayName || userName,
                email: email || null,
                role
            })
        });

        const d = await r.json();
        if (r.ok) {
            showToast(d.message || 'Tạo người dùng thành công!', 'success');
            // Close modal
            const modalEl = document.getElementById('create-user-modal');
            const modalInstance = bootstrap.Modal.getInstance(modalEl);
            if (modalInstance) modalInstance.hide();
            document.getElementById('create-user-form')?.reset();
            await loadUsers();
        } else {
            const msg = d.message || 'Lỗi khi tạo người dùng.';
            if (errorEl) {
                errorEl.textContent = msg;
                errorEl.classList.remove('d-none');
            } else {
                showToast(msg, 'error');
            }
        }
    } catch {
        if (errorEl) {
            errorEl.textContent = 'Lỗi kết nối máy chủ.';
            errorEl.classList.remove('d-none');
        } else {
            showToast('Lỗi kết nối máy chủ.', 'error');
        }
    } finally {
        if (btn) {
            btn.disabled = false;
            btn.innerHTML = '<i class="bi bi-check-lg"></i> Tạo người dùng';
        }
    }
}

function showToast(message, type) {
    const container = document.getElementById('toast-container');
    if (!container) return;
    const toast = document.createElement('div');
    toast.className = 'toast show';
    toast.setAttribute('role', 'alert');
    const bgClass = type === 'error' ? 'bg-danger' : type === 'success' ? 'bg-success' : 'bg-info';
    toast.innerHTML = `
        <div class="toast-header ${bgClass} text-white border-0">
            <strong class="me-auto">Thông báo</strong>
            <button type="button" class="btn-close btn-close-white" onclick="this.closest('.toast').remove()"></button>
        </div>
        <div class="toast-body">${escapeHtml(message)}</div>
    `;
    container.appendChild(toast);
    setTimeout(() => toast.remove(), 5000);
}

function escapeHtml(str) {
    if (str === null || str === undefined) return '';
    return String(str)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#039;');
}

document.addEventListener('DOMContentLoaded', () => {
    init();
});

