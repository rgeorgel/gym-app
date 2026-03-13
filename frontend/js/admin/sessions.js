import { api } from '../api.js';
import { showToast, createModal, openModal, closeModal, formatTime, emptyState, statusBadge, confirm } from '../ui.js';
import { t, getLocale } from '../i18n.js';

export async function renderSessions(container) {
  const today = new Date().toISOString().split('T')[0];
  const nextWeek = new Date(Date.now() + 13 * 86400000).toISOString().split('T')[0];

  container.innerHTML = `
    <div class="filters-bar">
      <label class="form-label" style="margin:0">${t('sessions.from')}</label>
      <input class="form-control" id="sessFrom" type="date" value="${today}" style="width:140px">
      <label class="form-label" style="margin:0">${t('sessions.to')}</label>
      <input class="form-control" id="sessTo" type="date" value="${nextWeek}" style="width:140px">
      <button class="btn btn-secondary" id="btnLoadSess">${t('btn.load')}</button>
    </div>
    <div id="sessionsContent"><div class="loading-center"><span class="spinner"></span></div></div>
  `;

  await loadSessions();

  document.getElementById('btnLoadSess').addEventListener('click', loadSessions);
}

async function loadSessions() {
  const from = document.getElementById('sessFrom').value;
  const to = document.getElementById('sessTo').value;
  const content = document.getElementById('sessionsContent');
  content.innerHTML = '<div class="loading-center"><span class="spinner"></span></div>';

  try {
    const sessions = await api.get(`/sessions?from=${from}&to=${to}`);

    if (!sessions.length) {
      content.innerHTML = emptyState('📅', t('sessions.none'));
      return;
    }

    // Group by date
    const byDate = {};
    sessions.forEach(s => {
      if (!byDate[s.date]) byDate[s.date] = [];
      byDate[s.date].push(s);
    });

    content.innerHTML = Object.entries(byDate).map(([date, list]) => `
      <div style="margin-bottom:1.25rem">
        <div style="font-weight:600;font-size:var(--font-size-sm);color:var(--gray-500);margin-bottom:0.5rem;text-transform:uppercase">
          ${new Date(date + 'T12:00:00').toLocaleDateString('pt-BR', { weekday:'long', day:'2-digit', month:'long' })}
        </div>
        <div class="card">
          <div class="table-wrapper">
            <table>
              <thead><tr><th>${t('sessions.col.time')}</th><th>${t('sessions.col.class')}</th><th>${t('field.instructor')}</th><th>${t('sessions.col.slots')}</th><th>${t('field.status')}</th><th></th></tr></thead>
              <tbody>
                ${list.map(s => `
                  <tr>
                    <td class="font-medium">${formatTime(s.startTime?.toString())}</td>
                    <td>
                      <div style="display:flex;align-items:center;gap:0.5rem">
                        <div style="width:10px;height:10px;border-radius:2px;background:${s.classTypeColor};flex-shrink:0"></div>
                        ${s.classTypeName}
                      </div>
                    </td>
                    <td class="text-sm text-muted">${s.instructorName ?? '—'}</td>
                    <td>
                      <span class="${s.slotsAvailable === 0 ? 'text-danger' : s.slotsAvailable <= 3 ? 'text-warning' : ''}">
                        ${s.bookingsCount}/${s.capacity}
                      </span>
                    </td>
                    <td>${s.status === 'Cancelled' ? `<span class="badge badge-danger">${t('sessions.status.cancelled')}</span>` : `<span class="badge badge-success">${t('sessions.status.active')}</span>`}</td>
                    <td>
                      <div class="flex gap-2">
                        <button class="btn btn-secondary btn-sm" onclick="window._viewCheckin('${s.id}')">${t('sessions.checkin')}</button>
                        ${s.status !== 'Cancelled'
                          ? `<button class="btn btn-danger btn-sm" onclick="window._cancelSession('${s.id}')">${t('btn.cancel')}</button>`
                          : `<button class="btn btn-primary btn-sm" onclick="window._reactivateSession('${s.id}')">${t('sessions.reactivate')}</button>`
                        }
                      </div>
                    </td>
                  </tr>
                `).join('')}
              </tbody>
            </table>
          </div>
        </div>
      </div>
    `).join('');

    window._viewCheckin = (id) => openCheckinModal(id);
    window._cancelSession = (id) => cancelSession(id);
    window._reactivateSession = (id) => reactivateSession(id);
  } catch (e) {
    showToast('Erro: ' + e.message, 'error');
  }
}

async function openCheckinModal(sessionId) {
  createModal({
    id: 'checkinModal',
    title: t('sessions.checkin.title'),
    body: '<div class="loading-center"><span class="spinner"></span></div>',
    footer: `<button class="btn btn-secondary" onclick="closeModal('checkinModal')">${t('btn.close')}</button>`
  });
  openModal('checkinModal');

  try {
    const [sessionData, bookings] = await Promise.all([
      api.get(`/sessions/${sessionId}`),
      api.get(`/sessions/${sessionId}/bookings`),
    ]);

    const body = document.querySelector('#checkinModal .modal-body');
    const title = document.querySelector('#checkinModal .modal-title');
    title.textContent = `Check-in — ${sessionData.classTypeName} ${formatTime(sessionData.startTime?.toString())}`;

    if (!bookings.length) {
      body.innerHTML = emptyState('👤', t('sessions.checkin.none'));
      return;
    }

    const renderList = (list) => {
      body.innerHTML = `
        <div style="margin-bottom:0.75rem;font-size:var(--font-size-xs);color:var(--gray-500)">
          ${list.filter(b=>b.status==='CheckedIn').length} ${t('sessions.checkin.of')} ${list.length}
        </div>
        ${list.map(b => `
          <div class="checkin-row">
            <div>
              <div class="font-medium text-sm">${b.studentName}</div>
              <div class="text-xs text-muted">${statusBadge(b.status)}</div>
            </div>
            ${b.status === 'Confirmed' ? `
              <button class="btn btn-primary btn-sm" onclick="window._doCheckin('${b.id}')">Check-in</button>
            ` : b.status === 'CheckedIn' ? `
              <span class="badge badge-success">✓ ${b.checkedInAt ? new Date(b.checkedInAt).toLocaleTimeString('pt-BR',{hour:'2-digit',minute:'2-digit'}) : ''}</span>
            ` : ''}
          </div>
        `).join('')}
      `;
    };

    renderList(bookings);

    window._doCheckin = async (bookingId) => {
      try {
        await api.post(`/bookings/${bookingId}/checkin`, {});
        const updated = await api.get(`/sessions/${sessionId}/bookings`);
        renderList(updated);
        showToast(t('sessions.checkin.success'), 'success');
      } catch (e) {
        showToast('Erro: ' + e.message, 'error');
      }
    };
  } catch (e) {
    showToast('Erro: ' + e.message, 'error');
  }
}

async function cancelSession(sessionId) {
  if (!await confirm(t('sessions.cancel.confirm'))) return;
  try {
    await api.post(`/sessions/${sessionId}/cancel`, { reason: 'Cancelado pelo admin' });
    showToast(t('sessions.cancel.success'), 'success');
    await loadSessions();
  } catch (e) {
    showToast('Erro: ' + e.message, 'error');
  }
}

async function reactivateSession(sessionId) {
  if (!await confirm(t('sessions.reactivate.confirm'))) return;
  try {
    await api.post(`/sessions/${sessionId}/reactivate`, {});
    showToast(t('sessions.reactivate.success'), 'success');
    await loadSessions();
  } catch (e) {
    showToast('Erro: ' + e.message, 'error');
  }
}
