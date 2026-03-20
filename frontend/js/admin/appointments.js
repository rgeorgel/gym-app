import { api } from '../api.js';
import { showToast, formatTime, emptyState, statusBadge } from '../ui.js';
import { t } from '../i18n.js';

export async function renderAppointments(container) {
  const toDateStr = d => `${d.getFullYear()}-${String(d.getMonth()+1).padStart(2,'0')}-${String(d.getDate()).padStart(2,'0')}`;
  const today = toDateStr(new Date());

  container.innerHTML = `
    <div class="filters-bar">
      <label class="form-label" style="margin:0">${t('appointments.date')}</label>
      <input class="form-control" id="apptDate" type="date" value="${today}" style="width:160px">
      <button class="btn btn-secondary" id="btnLoadAppts">${t('btn.load')}</button>
      <button class="btn btn-primary" id="btnNewAppt" style="margin-left:auto">＋ Novo Agendamento</button>
    </div>
    <div id="apptsContent"><div class="loading-center"><span class="spinner"></span></div></div>
  `;

  const load = async () => {
    const date = document.getElementById('apptDate').value;
    const content = document.getElementById('apptsContent');
    content.innerHTML = '<div class="loading-center"><span class="spinner"></span></div>';
    try {
      const appts = await api.get(`/appointments?date=${date}`);
      if (!appts.length) {
        content.innerHTML = emptyState(t('appointments.none'));
        return;
      }
      content.innerHTML = `
        <table class="data-table">
          <thead><tr>
            <th>${t('appointments.col.time')}</th>
            <th>${t('appointments.col.service')}</th>
            <th>${t('appointments.col.client')}</th>
            <th>${t('appointments.col.phone')}</th>
            <th>${t('appointments.col.price')}</th>
            <th>${t('appointments.col.status')}</th>
            <th></th>
          </tr></thead>
          <tbody id="apptsBody"></tbody>
        </table>
      `;
      const tbody = document.getElementById('apptsBody');
      appts.forEach(a => {
        const priceStr = a.servicePrice != null
          ? `R$ ${Number(a.servicePrice).toLocaleString('pt-BR', { minimumFractionDigits: 2 })}`
          : '—';
        const canCheckin = a.status === 'Confirmed';
        const tr = document.createElement('tr');
        tr.innerHTML = `
          <td>${formatTime(a.startTime)} <small style="color:var(--text-muted)">${a.durationMinutes}min</small></td>
          <td><span class="color-dot" style="background:${a.serviceColor}"></span> ${a.serviceName}</td>
          <td>${a.clientName}</td>
          <td>${a.clientPhone ? `<a href="tel:${a.clientPhone}">${a.clientPhone}</a>` : '—'}</td>
          <td>${priceStr}</td>
          <td>${statusBadge(a.status)}</td>
          <td>${canCheckin ? `<button class="btn btn-sm btn-primary btn-checkin" data-id="${a.bookingId}">${t('appointments.checkin')}</button>` : ''}</td>
        `;
        tbody.appendChild(tr);
      });

      tbody.querySelectorAll('.btn-checkin').forEach(btn => {
        btn.addEventListener('click', async () => {
          btn.disabled = true;
          try {
            await api.post(`/bookings/${btn.dataset.id}/checkin`, {});
            showToast(t('appointments.checkin.success'));
            await load();
          } catch (e) {
            showToast(e.message, 'error');
            btn.disabled = false;
          }
        });
      });
    } catch (e) {
      content.innerHTML = `<div class="empty-state"><div class="empty-state-text">${e.message}</div></div>`;
    }
  };

  document.getElementById('btnLoadAppts').addEventListener('click', load);
  document.getElementById('btnNewAppt').addEventListener('click', () => openNewApptModal(load));
  await load();
}

// ── Modal de novo agendamento ──────────────────────────────────────────────

function openNewApptModal(onSuccess) {
  const toDateStr = d => `${d.getFullYear()}-${String(d.getMonth()+1).padStart(2,'0')}-${String(d.getDate()).padStart(2,'0')}`;
  const today = toDateStr(new Date());

  const overlay = document.createElement('div');
  overlay.className = 'modal-overlay';
  overlay.innerHTML = `
    <div class="modal" style="max-width:480px;width:100%">
      <div class="modal-header">
        <h3 class="modal-title">Novo Agendamento</h3>
        <button class="modal-close" id="btnCloseModal">×</button>
      </div>
      <div class="modal-body" style="display:flex;flex-direction:column;gap:1rem">

        <div class="form-group">
          <label class="form-label">Cliente</label>
          <input class="form-control" id="clientSearch" placeholder="Buscar por nome ou telefone…" autocomplete="off">
          <div id="clientResults" style="display:none;border:1px solid var(--gray-200);border-radius:var(--border-radius);margin-top:2px;background:white;max-height:180px;overflow-y:auto;position:relative;z-index:10"></div>
          <input type="hidden" id="clientId">
          <div id="clientSelected" style="display:none;margin-top:0.4rem;font-size:0.85rem;color:var(--brand-secondary);font-weight:500"></div>
        </div>

        <div class="form-group">
          <label class="form-label">Serviço</label>
          <select class="form-control" id="serviceSelect"><option value="">Carregando…</option></select>
        </div>

        <div class="form-group">
          <label class="form-label">Data</label>
          <input class="form-control" id="apptDateNew" type="date" value="${today}">
        </div>

        <div id="slotsGroup" style="display:none">
          <label class="form-label">Horário</label>
          <div id="slotsContainer" style="display:flex;flex-wrap:wrap;gap:0.5rem;margin-top:0.25rem"></div>
          <input type="hidden" id="selectedSlot">
        </div>

      </div>
      <div class="modal-footer">
        <button class="btn btn-secondary" id="btnCancelModal">Cancelar</button>
        <button class="btn btn-primary" id="btnConfirmAppt" disabled>Confirmar</button>
      </div>
    </div>
  `;
  document.body.appendChild(overlay);

  const close = () => overlay.remove();
  overlay.querySelector('#btnCloseModal').addEventListener('click', close);
  overlay.querySelector('#btnCancelModal').addEventListener('click', close);
  overlay.addEventListener('click', e => { if (e.target === overlay) close(); });

  // Load services
  api.get('/class-types').then(services => {
    const sel = overlay.querySelector('#serviceSelect');
    const active = services.filter(s => s.isActive);
    sel.innerHTML = `<option value="">Selecionar serviço…</option>` +
      active.map(s => `<option value="${s.id}" data-duration="${s.durationMinutes ?? ''}">${s.name}${s.durationMinutes ? ` (${s.durationMinutes}min)` : ''}</option>`).join('');
  });

  // Client search with debounce
  let searchTimer;
  overlay.querySelector('#clientSearch').addEventListener('input', e => {
    clearTimeout(searchTimer);
    const q = e.target.value.trim();
    const results = overlay.querySelector('#clientResults');
    const clientId = overlay.querySelector('#clientId');
    clientId.value = '';
    overlay.querySelector('#clientSelected').style.display = 'none';
    if (q.length < 2) { results.style.display = 'none'; return; }
    searchTimer = setTimeout(async () => {
      try {
        const clients = await api.get(`/students?search=${encodeURIComponent(q)}`);
        if (!clients.length) {
          results.innerHTML = `<div style="padding:0.6rem 1rem;font-size:0.85rem;color:var(--gray-400)">Nenhum cliente encontrado</div>`;
        } else {
          results.innerHTML = clients.slice(0, 8).map(c => `
            <div class="client-result-item" data-id="${c.id}" data-name="${c.name}"
              style="padding:0.6rem 1rem;cursor:pointer;font-size:0.9rem;border-bottom:1px solid var(--gray-100)">
              <strong>${c.name}</strong>
              ${c.phone ? `<span style="color:var(--gray-400);font-size:0.8rem;margin-left:0.5rem">${c.phone}</span>` : ''}
            </div>
          `).join('');
          results.querySelectorAll('.client-result-item').forEach(item => {
            item.addEventListener('mouseenter', () => item.style.background = 'var(--gray-50)');
            item.addEventListener('mouseleave', () => item.style.background = '');
            item.addEventListener('click', () => {
              clientId.value = item.dataset.id;
              overlay.querySelector('#clientSearch').value = item.dataset.name;
              const sel = overlay.querySelector('#clientSelected');
              sel.textContent = `✓ ${item.dataset.name}`;
              sel.style.display = 'block';
              results.style.display = 'none';
              checkConfirmBtn();
            });
          });
        }
        results.style.display = 'block';
      } catch {}
    }, 300);
  });

  // Load slots when service or date changes
  const loadSlots = async () => {
    const serviceId = overlay.querySelector('#serviceSelect').value;
    const date = overlay.querySelector('#apptDateNew').value;
    const slotsGroup = overlay.querySelector('#slotsGroup');
    const slotsContainer = overlay.querySelector('#slotsContainer');
    const selectedSlot = overlay.querySelector('#selectedSlot');
    selectedSlot.value = '';
    checkConfirmBtn();

    if (!serviceId || !date) { slotsGroup.style.display = 'none'; return; }

    slotsGroup.style.display = 'block';
    slotsContainer.innerHTML = `<span class="spinner"></span>`;
    try {
      const slots = await api.get(`/slots?date=${date}&serviceId=${serviceId}`);
      if (!slots.length) {
        slotsContainer.innerHTML = `<span style="font-size:0.85rem;color:var(--gray-400)">Sem horários disponíveis nesta data</span>`;
        return;
      }
      slotsContainer.innerHTML = slots.map(s => `
        <button class="btn btn-secondary btn-slot-pick" data-slot="${s}" style="min-width:72px;font-size:0.85rem">
          ${s.substring(0,5)}
        </button>
      `).join('');
      slotsContainer.querySelectorAll('.btn-slot-pick').forEach(btn => {
        btn.addEventListener('click', () => {
          slotsContainer.querySelectorAll('.btn-slot-pick').forEach(b => b.classList.remove('btn-primary'));
          btn.classList.add('btn-primary');
          btn.classList.remove('btn-secondary');
          selectedSlot.value = btn.dataset.slot;
          checkConfirmBtn();
        });
      });
    } catch (e) {
      slotsContainer.innerHTML = `<span style="font-size:0.85rem;color:var(--color-danger)">${e.message}</span>`;
    }
  };

  overlay.querySelector('#serviceSelect').addEventListener('change', loadSlots);
  overlay.querySelector('#apptDateNew').addEventListener('change', loadSlots);

  const checkConfirmBtn = () => {
    const ok = overlay.querySelector('#clientId').value &&
               overlay.querySelector('#serviceSelect').value &&
               overlay.querySelector('#apptDateNew').value &&
               overlay.querySelector('#selectedSlot').value;
    overlay.querySelector('#btnConfirmAppt').disabled = !ok;
  };

  overlay.querySelector('#btnConfirmAppt').addEventListener('click', async () => {
    const btn = overlay.querySelector('#btnConfirmAppt');
    btn.disabled = true;
    btn.textContent = '…';
    try {
      await api.post('/bookings/salon', {
        studentId: overlay.querySelector('#clientId').value,
        serviceId: overlay.querySelector('#serviceSelect').value,
        date: overlay.querySelector('#apptDateNew').value,
        startTime: overlay.querySelector('#selectedSlot').value,
      });
      showToast('Agendamento criado com sucesso!', 'success');
      close();
      // Atualiza a data do filtro para a data agendada e recarrega
      const apptDate = document.getElementById('apptDate');
      if (apptDate) apptDate.value = overlay.querySelector('#apptDateNew').value;
      await onSuccess();
    } catch (e) {
      showToast(e.message, 'error');
      btn.disabled = false;
      btn.textContent = 'Confirmar';
    }
  });
}
