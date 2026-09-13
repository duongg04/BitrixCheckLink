let currentPage = 1;
let currentTab = 'active';
let currentSort = 'subdomain';
let currentSortDir = 'asc';
let currentUser = null;
let searchDebounce = null;
let currentModalLinkId = null;

async function init() {
    const user = await requireAuth();
    if (!user) return;

    currentUser = user;
    const whoami = document.getElementById('whoami');
    if (whoami) {
        whoami.textContent = `${user.displayName || user.userName} (${user.roles.join(', ')})`;
    }

    if (user.roles.includes('Admin')) {
        const scanPanel = document.getElementById('scan-panel');
        if (scanPanel) scanPanel.style.display = 'block';
        const adminActions = document.getElementById('admin-actions');
        if (adminActions) adminActions.style.display = 'flex';
        const adminTools = document.getElementById('admin-tools');
        if (adminTools) adminTools.style.display = 'inline-flex';
    }

// Non-admin UX: ẩn điều hướng admin và các công cụ quản trị.
    // Processed tab vẫn hiện cho User/Sales (R18 — TaiLieu.md: "dành cho Sales Team").
    if (!user.roles.includes('Admin')) {
        const adminTools = document.getElementById('admin-tools');
        if (adminTools) adminTools.remove();
        const searchCol = document.getElementById('search-input')?.closest('.col-md-4');
        if (searchCol) searchCol.className = 'col-md-6';
    }
    // Show notification bell for all authenticated users
    const notifBell = document.getElementById('notification-bell');
    if (notifBell) notifBell.style.display = 'inline-block';
    pollNotificationBadge();

    // Wordlist checkbox toggle
    const wordlistCb = document.getElementById('useWordlist');
    const wordlistInput = document.getElementById('wordlistInput');
    if (wordlistCb && wordlistInput) {
        wordlistCb.addEventListener('change', () => {
            wordlistInput.style.display = wordlistCb.checked ? 'block' : 'none';
        });
    }

    await Promise.all([loadStats(), loadLinks(), loadAssignees()]);
}

function goHome() {
    currentPage = 1;
    currentTab = 'active';
    currentSort = 'subdomain';
    currentSortDir = 'asc';
    const searchInput = document.getElementById('search-input');
    if (searchInput) searchInput.value = '';
    const tabs = document.querySelectorAll('#link-tabs .nav-link');
    tabs.forEach((l, i) => l.classList.toggle('active', i === 0));
    const assignee = document.getElementById('assignee-filter');
    if (assignee) assignee.value = '';
    const psel = document.getElementById('processing-status-filter');
    if (psel) psel.value = '';
    loadLinks();
}

function switchTab(tab) {
    // Inactive tab remains Admin-only (backend returns 403 for User on INACTIVE).
    if (tab === 'inactive' && !currentUser?.roles?.includes('Admin')) {
        showToast('Bạn không có quyền truy cập mục này.', 'error');
        return;
    }
    currentTab = tab;
    currentPage = 1;

    document.querySelectorAll('#link-tabs .nav-link').forEach(l => {
        l.classList.toggle('active', l.dataset.tab === tab);
    });

    const isAdmin = currentUser?.roles?.includes('Admin');
    const assigneeFilter = document.getElementById('assignee-filter');
    const procFilter = document.getElementById('processing-status-filter');

    if (tab === 'processed') {
        if (procFilter) procFilter.style.display = 'block';
        // Assignee filter only for Admin (backend ignores assigneeId for User)
        if (assigneeFilter) assigneeFilter.style.display = isAdmin ? 'block' : 'none';
    } else {
        if (procFilter) procFilter.style.display = 'none';
        if (assigneeFilter) assigneeFilter.style.display = (tab === 'inactive' && isAdmin) ? 'block' : 'none';
    }

    // Date filters — visible on all tabs
    const dateFromCol = document.getElementById('date-from-col');
    const dateToCol = document.getElementById('date-to-col');
    if (dateFromCol) dateFromCol.style.display = 'block';
    if (dateToCol) dateToCol.style.display = 'block';

    loadLinks();
}

function onSearchInput() {
    clearTimeout(searchDebounce);
    searchDebounce = setTimeout(() => {
        currentPage = 1;
        loadLinks();
    }, 400);
}

function sortBy(field) {
    if (currentSort === field) {
        currentSortDir = currentSortDir === 'asc' ? 'desc' : 'asc';
    } else {
        currentSort = field;
        currentSortDir = 'asc';
    }
    updateSortIndicators();
    loadLinks();
}

function updateSortIndicators() {
    ['subdomain', 'status', 'httpCode', 'createdAt', 'lastChecked'].forEach(f => {
        const el = document.getElementById(`sort-${f}`);
        if (el) el.textContent = currentSort === f ? (currentSortDir === 'asc' ? ' ↑' : ' ↓') : '';
    });
}

function prevPage() {
    if (currentPage > 1) {
        currentPage--;
        loadLinks();
    }
}

function nextPage() {
    currentPage++;
    loadLinks();
}

function mapTabToStatus(tab) {
    switch (tab) {
        case 'active': return 'ACTIVE';
        case 'inactive': return 'INACTIVE';
        case 'processed': return 'PROCESSED';
        default: return 'ACTIVE';
    }
}

async function loadStats() {
    try {
        const r = await api('/api/link/stats');
        if (!r.ok) return;
        const d = await r.json();
        const statTotal = document.getElementById('stat-total');
        if (statTotal) statTotal.textContent = d.total ?? '-';
        const statActive = document.getElementById('stat-active');
        if (statActive) statActive.textContent = d.active ?? 0;
        const statInactive = document.getElementById('stat-inactive');
        if (statInactive) statInactive.textContent = d.inactive ?? 0;
        const statProcessed = document.getElementById('stat-processed');
        if (statProcessed) statProcessed.textContent = d.processed ?? 0;

        const pauseBtn = document.getElementById('btn-pause');
        if (pauseBtn) {
            const isPaused = d.isPaused;
            pauseBtn.textContent = isPaused ? 'Bật hệ thống' : 'Tạm dừng hệ thống';
            pauseBtn.className = isPaused ? 'btn btn-outline-success btn-sm' : 'btn btn-outline-warning btn-sm';
            pauseBtn.dataset.paused = isPaused ? 'true' : 'false';
        }
const systemStatus = document.getElementById('system-status');
        if (systemStatus) {
            const statusPaused = !!d.isPaused;
            systemStatus.classList.remove('d-none');
            systemStatus.classList.toggle('badge-status-running', !statusPaused);
            systemStatus.classList.toggle('badge-status-paused', statusPaused);
            const statusText = document.getElementById('system-status-text');
            if (statusText) statusText.textContent = statusPaused ? 'Hệ thống đang tạm dừng' : 'Hệ thống đang hoạt động';
        }
    } catch {
        showToast('Không thể tải thống kê.', 'error');
    }
}

async function loadAssignees() {
    try {
        const r = await api('/api/auth/users');
        if (!r.ok) return;
        const users = await r.json();
        const sel = document.getElementById('assignee-filter');
        if (sel) {
            sel.innerHTML = '<option value="">Tất cả người phụ trách</option>';
            users.forEach(u => {
                const opt = document.createElement('option');
                opt.value = u.id;
                opt.textContent = `${u.displayName || u.userName} (${(u.roles || []).join(',')})`;
                sel.appendChild(opt);
            });
        }

        const psel = document.getElementById('processing-status-filter');
        if (psel) {
            psel.innerHTML = '<option value="">Tất cả trạng thái xử lý</option>';
            ['New', 'Contacted', 'Negotiating', 'Successful', 'Failed', 'NotPotential'].forEach(s => {
                const opt = document.createElement('option');
                opt.value = s;
                opt.textContent = s;
                psel.appendChild(opt);
            });
        }
    } catch { }
}

async function loadLinks() {
    const table = document.getElementById('links-table');
    if (!table) return;
    table.innerHTML = '<tr><td colspan="8" class="text-center py-4"><div class="spinner-border spinner-border-sm text-primary" role="status"></div> Đang tải...</td></tr>';

    try {
        const params = new URLSearchParams({
            status: mapTabToStatus(currentTab),
            page: currentPage,
            sortBy: currentSort,
            sortDir: currentSortDir,
            search: document.getElementById('search-input')?.value.trim() || '',
            assigneeId: document.getElementById('assignee-filter')?.value || '',
            processingStatus: document.getElementById('processing-status-filter')?.value || ''
        });
        const dateFrom = document.getElementById('date-from')?.value;
        const dateTo = document.getElementById('date-to')?.value;
        if (dateFrom) params.set('dateFrom', dateFrom);
        if (dateTo) params.set('dateTo', dateTo);

        const r = await api(`/api/link/list?${params.toString()}`);
        if (!r.ok) throw new Error('Request failed');
        const d = await r.json();

        const rows = d.data || [];
        const total = d.total || 0;
        const pageSize = d.pageSize || 50;
        const totalPages = d.totalPages || Math.ceil(total / pageSize) || 1;

        if (!rows.length) {
            table.innerHTML = '<tr><td colspan="8" class="text-center text-muted py-4">Không có link nào phù hợp.</td></tr>';
            if (document.getElementById('pagination')) document.getElementById('pagination').textContent = '';
            if (document.getElementById('page-current')) document.getElementById('page-current').textContent = `Trang ${currentPage}`;
            if (document.getElementById('page-info')) document.getElementById('page-info').textContent = `Tổng: 0`;
            return;
        }

        const isAdmin = currentUser?.roles?.includes('Admin');
        table.innerHTML = rows.map(l => `
            <tr>
                <td><strong>${escapeHtml(l.subdomain || '-')}</strong></td>
                <td class="url-cell"><a href="${escapeHtml(l.fullUrl)}" target="_blank" rel="noopener noreferrer" title="${escapeHtml(l.fullUrl)}">${escapeHtml(l.fullUrl)}</a></td>
                <td><span class="badge link-status-badge ${l.status === 'ACTIVE' ? 'badge-active' : 'badge-inactive'}">${escapeHtml(l.status || '-')}</span></td>
                <td><span class="badge bg-dark">${l.httpCode ?? '-'}</span></td>
                <td>${l.createdAt ? new Date(l.createdAt).toLocaleString('vi-VN') : '-'}</td>
                <td>${l.lastChecked ? new Date(l.lastChecked).toLocaleString('vi-VN') : '-'}</td>
                <td>
                    <span class="badge processing-badge bg-secondary">${escapeHtml(l.processingStatus || 'Chưa xử lý')}</span>
                    ${l.assignedUserName ? `<span class="text-muted small ms-1">(${escapeHtml(l.assignedUserName)})</span>` : ''}
                </td>
                <td class="text-end text-nowrap">
                    <button class="btn btn-sm ${l.isTracked ? 'btn-success' : 'btn-outline-secondary'}" title="${l.isTracked ? 'Đang theo dõi' : 'Theo dõi'}" onclick="toggleTrack(${l.id})"><i class="bi bi-eye${l.isTracked ? '-fill' : ''}"></i></button>
                    <button class="btn btn-sm btn-outline-info ms-1" title="Xem chi tiết ${escapeHtml(l.subdomain || '')}" onclick="viewLink(${l.id})"><i class="bi bi-search"></i></button>
                    ${isAdmin ? `<button class="btn btn-sm btn-outline-warning ms-1" title="Re-check ${escapeHtml(l.subdomain || '')} ngay" onclick="recheckLink(${l.id})"><i class="bi bi-arrow-clockwise"></i></button>` : ''}
                </td>
            </tr>
        `).join('');

        if (document.getElementById('page-info')) document.getElementById('page-info').textContent = `Tổng: ${total.toLocaleString('vi-VN')} link`;
        if (document.getElementById('pagination')) document.getElementById('pagination').textContent = `Trang ${currentPage} / ${totalPages}`;
        if (document.getElementById('page-current')) document.getElementById('page-current').textContent = `Trang ${currentPage}`;
    } catch {
        table.innerHTML = '<tr><td colspan="7" class="text-center text-danger py-4">Không thể tải danh sách link. Vui lòng thử lại.</td></tr>';
    }
}

let scanPollTimer = null;
let isScanPolling = false;

async function extractErrorMessage(response) {
    try {
        const text = await response.text();
        if (!text) return '';
        try {
            const data = JSON.parse(text);
            if (typeof data === 'string') return data;
            if (data && typeof data === 'object') {
                return data.message || data.title || (data.errors ? Object.values(data.errors).flat().join(' ') : text);
            }
        } catch {
            return text;
        }
    } catch {
        return '';
    }
    return '';
}

async function pollScanProgress() {
    if (isScanPolling) return;
    isScanPolling = true;

    const startTime = Date.now();
    const maxPollDuration = 10 * 60 * 1000; // 10 minutes maximum bounded polling
    const statusEl = document.getElementById('scan-status');
    const btn = document.getElementById('btn-scan');

    const tick = async () => {
        try {
            const r = await api('/api/scan/progress');
            if (r.ok) {
                const job = await r.json();
                if (job) {
                    const scanned = job.totalScanned || 0;
                    const expected = job.totalExpected || 0;

                    if (job.status === 'Completed') {
                        if (statusEl) {
                            statusEl.classList.remove('d-none');
                            statusEl.className = 'mt-3 alert alert-success';
                            statusEl.textContent = `Quét hoàn tất! Đã kiểm tra ${scanned.toLocaleString('vi-VN')} / ${expected.toLocaleString('vi-VN')} subdomain.`;
                        }
                        if (btn) {
                            btn.disabled = false;
                            btn.textContent = 'Bắt đầu quét';
                        }
                        isScanPolling = false;
                        await Promise.all([loadStats(), loadLinks()]);
                        setTimeout(() => statusEl?.classList.add('d-none'), 8000);
                        return;
                    }

                    if (job.status === 'Failed') {
                        if (statusEl) {
                            statusEl.classList.remove('d-none');
                            statusEl.className = 'mt-3 alert alert-danger';
                            statusEl.textContent = 'Phiên quét thất bại trên hệ thống.';
                        }
                        if (btn) {
                            btn.disabled = false;
                            btn.textContent = 'Bắt đầu quét';
                        }
                        isScanPolling = false;
                        await Promise.all([loadStats(), loadLinks()]);
                        return;
                    }

                    // Job is Running or Pending
                    if (statusEl) {
                        statusEl.classList.remove('d-none');
                        statusEl.className = 'mt-3 alert alert-info';
                        statusEl.textContent = expected > 0
                            ? `Đang quét... Đã kiểm tra ${scanned.toLocaleString('vi-VN')} / ${expected.toLocaleString('vi-VN')} subdomain. Kết quả đang được cập nhật.`
                            : 'Đang khởi tạo và chuẩn bị các gói quét...';
                    }

                    loadStats();
                    loadLinks();
                }
            }
        } catch {
            // Transient error in polling, ignore and continue until bounded timeout
        }

        if (Date.now() - startTime >= maxPollDuration) {
            if (statusEl) {
                statusEl.classList.remove('d-none');
                statusEl.className = 'mt-3 alert alert-warning';
                statusEl.textContent = 'Phiên quét đang tiếp tục chạy ngầm trên máy chủ. Kết quả sẽ được cập nhật khi tải lại trang.';
            }
            if (btn) {
                btn.disabled = false;
                btn.textContent = 'Bắt đầu quét';
            }
            isScanPolling = false;
            await Promise.all([loadStats(), loadLinks()]);
            return;
        }

        // Strictly sequential polling: next tick scheduled only after this tick completes
        scanPollTimer = setTimeout(tick, 3000);
    };

    scanPollTimer = setTimeout(tick, 1500);
}

async function startScan() {
    clearTimeout(scanPollTimer);
    isScanPolling = false;

    const btn = document.getElementById('btn-scan');
    if (btn) {
        btn.disabled = true;
        btn.textContent = 'Đang khởi tạo...';
    }

    const statusEl = document.getElementById('scan-status');
    if (statusEl) {
        statusEl.classList.remove('d-none');
        statusEl.className = 'mt-3 alert alert-info';
        statusEl.textContent = 'Đang khởi tạo phiên quét...';
    }

    try {
        const minLen = parseInt(document.getElementById('minLength')?.value || '3');
        const maxLen = parseInt(document.getElementById('maxLength')?.value || '3');
        const par = parseInt(document.getElementById('parallelism')?.value || '20');
        const retry = parseInt(document.getElementById('retryCount')?.value || '2');
        const qtyVal = document.getElementById('scanQuantity')?.value;
        const qty = qtyVal ? parseInt(qtyVal) : null;
        const useWordlist = document.getElementById('useWordlist')?.checked || false;
        const wordlistRaw = document.getElementById('wordlistInput')?.value || '';
        const wordlist = useWordlist
            ? wordlistRaw.split('\n').map(s => s.trim()).filter(s => s.length > 0)
            : null;

        const r = await api('/api/scan/generate', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                minLength: minLen,
                maxLength: maxLen,
                quantity: qty,
                parallelism: par,
                retryCount: retry,
                useWordlist: useWordlist,
                wordlist: wordlist
            })
        });

        if (r.ok) {
            let successMsg = 'Đã kích hoạt phiên quét thành công!';
            try {
                const text = await r.text();
                const d = JSON.parse(text);
                if (typeof d === 'string') successMsg = d;
                else if (d?.message) successMsg = d.message;
            } catch {}

            if (statusEl) {
                statusEl.className = 'mt-3 alert alert-info';
                statusEl.textContent = `${successMsg} Đang theo dõi tiến độ quét...`;
            }
            if (btn) {
                btn.disabled = true;
                btn.textContent = 'Đang quét...';
            }
            pollScanProgress();
            return;
        }

        // Non-OK response: extract status and message
        const errMsg = await extractErrorMessage(r);
        let displayMsg = '';
        switch (r.status) {
            case 400:
                displayMsg = errMsg || 'Dữ liệu yêu cầu không hợp lệ.';
                break;
            case 401:
                displayMsg = 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.';
                break;
            case 403:
                displayMsg = 'Bạn không có quyền thực hiện chức năng này.';
                break;
            case 404:
                displayMsg = 'Không tìm thấy tài nguyên yêu cầu.';
                break;
            case 429:
                displayMsg = 'Yêu cầu quá giới hạn (rate limit). Vui lòng thử lại sau giây lát.';
                break;
            case 500:
                displayMsg = errMsg || 'Lỗi máy chủ nội bộ (500).';
                break;
            default:
                displayMsg = errMsg || `Yêu cầu thất bại (${r.status}).`;
                break;
        }

        if (statusEl) {
            statusEl.className = 'mt-3 alert alert-danger';
            statusEl.textContent = displayMsg;
            setTimeout(() => statusEl.classList.add('d-none'), 8000);
        }
        if (btn) {
            btn.disabled = false;
            btn.textContent = 'Bắt đầu quét';
        }
    } catch {
        // Only genuine network/fetch exceptions reach here
        if (statusEl) {
            statusEl.className = 'mt-3 alert alert-danger';
            statusEl.textContent = 'Lỗi kết nối máy chủ.';
            setTimeout(() => statusEl.classList.add('d-none'), 8000);
        }
        if (btn) {
            btn.disabled = false;
            btn.textContent = 'Bắt đầu quét';
        }
    }
}

async function viewLink(id) {
    try {
        const [detailRes, historyRes] = await Promise.all([
            api(`/api/link/${id}`),
            api(`/api/link/processing-history/${id}`)
        ]);

        if (detailRes.ok) {
            const detailData = await detailRes.json();
            const link = detailData.data ? detailData.data[0] : detailData;
            if (link) {
                const modalSub = document.getElementById('modal-subdomain');
                if (modalSub) modalSub.textContent = link.subdomain || '';
                const urlEl = document.getElementById('modal-url');
                if (urlEl) {
                    urlEl.href = link.fullUrl;
                    urlEl.textContent = link.fullUrl;
                }
                const modalHttp = document.getElementById('modal-http');
                if (modalHttp) modalHttp.textContent = link.httpCode || 'N/A';
                const modalCreated = document.getElementById('modal-created');
                if (modalCreated) modalCreated.textContent = link.createdAt ? new Date(link.createdAt).toLocaleString('vi-VN') : '-';
                const modalLastChecked = document.getElementById('modal-last-checked');
                if (modalLastChecked) modalLastChecked.textContent = link.lastChecked ? new Date(link.lastChecked).toLocaleString('vi-VN') : '-';

                // Backend GET /api/link/{id} returns flat fields (note, processingStatus,
                // assignedUserId, assignedUserName), not a nested "processing" object.
                // Map both shapes so the modal shows the real pipeline state instead of
                // defaulting to New/(empty) — otherwise saving would overwrite real data.
                const proc = link.processing || {
                    status: link.processingStatus,
                    assignedUserId: link.assignedUserId,
                    note: link.note
                };
                const procStatusEl = document.getElementById('modal-processing-status');
                if (procStatusEl) {
                    procStatusEl.value = proc.status || 'New';
                    procStatusEl.disabled = !currentUser?.roles?.includes('Admin');
                }
                const assigneeEl = document.getElementById('modal-assignee');
                if (assigneeEl) {
                    assigneeEl.value = proc.assignedUserId || '';
                    assigneeEl.disabled = !currentUser?.roles?.includes('Admin');
                }
                const noteEl = document.getElementById('modal-note');
                if (noteEl) noteEl.value = proc.note || '';

                currentModalLinkId = link.id;
            }
        }

        if (historyRes.ok) {
            const histories = await historyRes.json();
            const container = document.getElementById('modal-history');
            if (container) {
                if (!histories || !histories.length) {
                    container.innerHTML = '<div class="history-empty">Chưa có lịch sử thay đổi nào.</div>';
                } else {
                    container.innerHTML = histories.map(h => `
                        <div class="timeline-item">
                            <div class="d-flex justify-content-between">
                                <strong>${escapeHtml(h.status || 'Mới tạo')}</strong>
                                <small class="text-muted">${new Date(h.changedAt).toLocaleString('vi-VN')}</small>
                            </div>
                            ${h.note ? `<div class="small text-muted mt-1">${escapeHtml(h.note)}</div>` : ''}
                            ${h.changedByName ? `<div class="small text-secondary">Bởi: ${escapeHtml(h.changedByName)}</div>` : ''}
                            ${h.changeReason ? `<div class="small text-muted fst-italic">${escapeHtml(h.changeReason)}</div>` : ''}
                        </div>
                    `).join('');
                }
            }
        }

        const modalEl = document.getElementById('detail-modal');
        if (modalEl) {
            const bsModal = new bootstrap.Modal(modalEl);
            bsModal.show();
        }
    } catch {
        showToast('Không thể tải chi tiết link.', 'error');
    }
}

async function saveProcessing() {
    const linkId = currentModalLinkId;
    if (!linkId) return;

    const status = document.getElementById('modal-processing-status')?.value || 'New';
    const assignee = document.getElementById('modal-assignee')?.value || '';
    const note = document.getElementById('modal-note')?.value || '';

    const isAdmin = currentUser?.roles?.includes('Admin');
    const endpoint = isAdmin ? `/api/link/processing/${linkId}` : `/api/link/processing/${linkId}/note`;
    const payload = isAdmin
        ? { note, status, assignedUserId: assignee.trim() || null }
        : { note };

const saveBtn = document.getElementById('btn-save-processing');
    if (saveBtn) {
        saveBtn.disabled = true;
        saveBtn.innerHTML = '<span class="spinner-border spinner-border-sm"></span> Đang lưu...';
    }
    try {
        const r = await api(endpoint, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });
        const d = await r.json();
        showToast(d.message || (r.ok ? 'Đã lưu thông tin xử lý.' : 'Lỗi khi lưu.'), r.ok ? 'success' : 'error');
        if (saveBtn) {
            saveBtn.disabled = false;
            saveBtn.innerHTML = '<i class="bi bi-check-lg"></i> Lưu thay đổi';
        }
        if (r.ok) {
            loadLinks();
            loadStats();
        }
    } catch {
        showToast('Lỗi kết nối máy chủ.', 'error');
        if (saveBtn) {
            saveBtn.disabled = false;
            saveBtn.innerHTML = '<i class="bi bi-check-lg"></i> Lưu thay đổi';
        }
    }
}

async function recheckLink(linkId) {
    if (!confirm('Re-check link này ngay lập tức?')) return;
    try {
        const r = await api(`/api/link/recheck/${linkId}`, { method: 'POST' });
        const d = await r.json();
        showToast(d.message || 'Đã gửi lệnh re-check.', 'success');
        setTimeout(() => loadLinks(), 1000);
    } catch {
        showToast('Lỗi kết nối khi re-check.', 'error');
    }
}

async function toggleTrack(linkId) {
    try {
        const r = await api(`/api/link/track/${linkId}`, { method: 'POST' });
        const d = await r.json();
        showToast(d.message || 'Đã cập nhật trạng thái theo dõi.', r.ok ? 'success' : 'error');
        if (r.ok) loadLinks();
    } catch {
        showToast('Lỗi kết nối khi cập nhật theo dõi.', 'error');
    }
}

async function bulkRecheckAll() {
    const table = document.getElementById('links-table');
    const viewButtons = table ? Array.from(table.querySelectorAll('button[onclick^="viewLink("]')) : [];
    const linkIds = viewButtons.map(btn => {
        const match = btn.getAttribute('onclick')?.match(/\d+/);
        return match ? parseInt(match[0]) : null;
    }).filter(id => id !== null);

    if (!linkIds.length) {
        showToast('Không có link nào trên trang hiện tại để re-check.', 'error');
        return;
    }

    if (!confirm(`Bạn có chắc muốn gửi lệnh re-check cho ${linkIds.length} link trên trang này?`)) return;

    const btn = document.getElementById('btn-bulk-recheck');
    if (btn) btn.disabled = true;
    try {
        const r = await api('/api/link/recheck-bulk', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ linkIds })
        });
        const d = await r.json();
        showToast(d.message || 'Đã gửi lệnh re-check hàng loạt.', r.ok ? 'success' : 'error');
        if (r.ok) setTimeout(() => loadLinks(), 1500);
    } catch {
        showToast('Lỗi kết nối khi gửi lệnh re-check hàng loạt.', 'error');
    } finally {
        if (btn) btn.disabled = false;
    }
}

async function exportCsv() {
    const status = mapTabToStatus(currentTab);
    const assignee = document.getElementById('assignee-filter')?.value || '';
    const dateFrom = document.getElementById('date-from')?.value || '';
    const dateTo = document.getElementById('date-to')?.value || '';
    const params = new URLSearchParams({ status, assigneeId: assignee });
    if (dateFrom) params.set('dateFrom', dateFrom);
    if (dateTo) params.set('dateTo', dateTo);

    try {
        const r = await api(`/api/link/export?${params.toString()}`);
        if (!r.ok) {
            showToast(r.status === 403 ? 'Bạn không có quyền xuất CSV.' : 'Không thể xuất CSV.', 'error');
            return;
        }
        const blob = await r.blob();
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `bitrix_links_${status.toLowerCase()}_${new Date().toISOString().slice(0, 10)}.csv`;
        document.body.appendChild(a);
        a.click();
        a.remove();
        URL.revokeObjectURL(url);
    } catch {
        showToast('Lỗi kết nối khi xuất CSV.', 'error');
    }
}

async function exportXlsx() {
    const status = mapTabToStatus(currentTab);
    const assignee = document.getElementById('assignee-filter')?.value || '';
    const dateFrom = document.getElementById('date-from')?.value || '';
    const dateTo = document.getElementById('date-to')?.value || '';
    const params = new URLSearchParams({ status, assigneeId: assignee });
    if (dateFrom) params.set('dateFrom', dateFrom);
    if (dateTo) params.set('dateTo', dateTo);

    try {
        const r = await api(`/api/link/export-xlsx?${params.toString()}`);
        if (!r.ok) {
            showToast(r.status === 403 ? 'Bạn không có quyền xuất XLSX.' : 'Không thể xuất XLSX.', 'error');
            return;
        }
        const blob = await r.blob();
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `bitrix_links_${status.toLowerCase()}_${new Date().toISOString().slice(0, 10)}.xlsx`;
        document.body.appendChild(a);
        a.click();
        a.remove();
        URL.revokeObjectURL(url);
    } catch {
        showToast('Lỗi kết nối khi xuất XLSX.', 'error');
    }
}

async function pollNotificationBadge() {
    try {
        const r = await api('/api/notifications?limit=5');
        if (!r.ok) return;
        const d = await r.json();
        const badge = document.getElementById('notif-badge');
        if (badge) {
            const count = d.unreadCount || 0;
            badge.textContent = count > 99 ? '99+' : count;
            badge.style.display = count > 0 ? 'inline-block' : 'none';
        }
    } catch {}
}

async function loadNotifications() {
    const list = document.getElementById('notif-list');
    if (!list) return;
    try {
        const r = await api('/api/notifications?limit=15');
        if (!r.ok) {
            list.innerHTML = '<li class="dropdown-header">Thông báo</li><li><hr class="dropdown-divider"></li><li class="text-center text-muted small py-2">Không thể tải thông báo.</li>';
            return;
        }
        const d = await r.json();
        const notifs = d.notifications || [];
        if (!notifs.length) {
            list.innerHTML = '<li class="dropdown-header">Thông báo</li><li><hr class="dropdown-divider"></li><li class="text-center text-muted small py-3">Không có thông báo nào.</li>';
            return;
        }

        const items = notifs.map(n => {
            const iconClass = n.type === 'Success' ? 'bi-check-circle-fill text-success'
                : n.type === 'Warning' ? 'bi-exclamation-triangle-fill text-warning'
                : n.type === 'Error' ? 'bi-x-circle-fill text-danger'
                : 'bi-info-circle-fill text-info';
            return `
                <li class="px-3 py-2 border-bottom ${n.isRead ? '' : 'bg-light'}">
                    <div class="d-flex align-items-start gap-2">
                        <i class="bi ${iconClass} mt-1"></i>
                        <div class="flex-grow-1">
                            <div class="small ${n.isRead ? 'text-muted' : 'fw-semibold'}">${escapeHtml(n.message)}</div>
                            <div class="text-muted" style="font-size: 0.7rem;">${new Date(n.createdAt).toLocaleString('vi-VN')}</div>
                        </div>
                    </div>
                </li>
            `;
        }).join('');

        list.innerHTML = `
            <li class="dropdown-header d-flex justify-content-between align-items-center">
                <span>Thông báo (${d.unreadCount || 0} mới)</span>
                ${d.unreadCount > 0 ? '<a href="#" class="small text-primary" onclick="markAllNotificationsRead(event)">Đánh dấu đã đọc</a>' : ''}
            </li>
            <li><hr class="dropdown-divider my-1"></li>
            ${items}
        `;
    } catch {
        list.innerHTML = '<li class="dropdown-header">Thông báo</li><li><hr class="dropdown-divider"></li><li class="text-center text-danger small py-2">Lỗi kết nối.</li>';
    }
}

async function markAllNotificationsRead(e) {
    if (e) e.preventDefault();
    try {
        await api('/api/notifications/read', { method: 'POST' });
        const badge = document.getElementById('notif-badge');
        if (badge) badge.style.display = 'none';
        loadNotifications();
    } catch {}
}

async function bulkDeleteInactive() {
    if (!confirm('Bạn có chắc chắn muốn xóa TẤT CẢ các subdomain Inactive? Hành động này không thể hoàn tác.')) return;
    try {
        const btn = document.getElementById('btn-bulk-delete');
        if (btn) btn.disabled = true;
        const r = await api(`/api/link/inactive`, { method: 'DELETE' });
        if (!r.ok) {
            const msg = await extractErrorMessage(r) || 'Không thể xóa Inactive.';
            showToast(msg, 'error');
        } else {
            const d = await r.json();
            showToast(`Đã xóa ${d.archived || 0} subdomain Inactive.`, 'success');
            loadStats();
            loadLinks();
        }
    } catch {
        showToast('Lỗi kết nối.', 'error');
    } finally {
        const btn = document.getElementById('btn-bulk-delete');
        if (btn) btn.disabled = false;
    }
}

function togglePause() {
    const btn = document.getElementById('btn-pause');
    if (!btn) return;
    const isPaused = btn.dataset.paused === 'true';
    api(`/api/link/pause?pause=${!isPaused}`, { method: 'POST' })
        .then(r => r.json())
        .then(d => {
            btn.dataset.paused = (!isPaused).toString();
            btn.textContent = !isPaused ? 'Bật hệ thống' : 'Tạm dừng hệ thống';
            btn.className = !isPaused ? 'btn btn-outline-success btn-sm' : 'btn btn-outline-warning btn-sm';
            showToast(!isPaused ? 'Hệ thống đã tạm dừng' : 'Hệ thống đã tiếp tục hoạt động', 'success');
        })
        .catch(() => showToast('Lỗi kết nối.', 'error'));
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
