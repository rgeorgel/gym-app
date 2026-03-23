import { api } from '../api.js';
import { showToast, formatTime, emptyState, statusBadge } from '../ui.js';
import { t } from '../i18n.js';

export async function renderAppointments(container) {
  const toDateStr = d => `${d.getFullYear()}-${String(d.getMonth()+1).padStart(2,'0')}-${String(d.getDate()).padStart(2,'0')}`;
  const today = toDateStr(new Date());

  let currentView = 'day';
  let currentDate = new Date();

  container.innerHTML = `
    <div class="filters-bar">
      <div class="view-toggle" style="display:flex;gap:0;margin-right:1rem">
        <button class="btn btn-sm btn-toggle-view active" data-view="day">${t('appointments.view.day')}</button>
        <button class="btn btn-sm btn-toggle-view" data-view="week">${t('appointments.view.week')}</button>
        <button class="btn btn-sm btn-toggle-view" data-view="month">${t('appointments.view.month')}</button>
      </div>
      <button class="btn btn-sm btn-secondary" id="btnToday" style="margin-right:0.5rem">${t('appointments.today')}</button>
      <button class="btn btn-sm btn-secondary" id="btnPrev" style="padding:0.25rem 0.5rem">‹</button>
      <span id="currentPeriod" style="min-width:160px;text-align:center;font-weight:500;padding:0 0.5rem;align-self:center"></span>
      <button class="btn btn-sm btn-secondary" id="btnNext" style="padding:0.25rem 0.5rem">›</button>
      <label class="form-label" style="margin:0 0 0 1rem">${t('appointments.date')}</label>
      <input class="form-control" id="apptDate" type="date" value="${today}" style="width:160px">
      <button class="btn btn-secondary" id="btnLoadAppts" style="margin-left:0.5rem">${t('btn.load')}</button>
      <button class="btn btn-primary" id="btnNewAppt" style="margin-left:auto">＋ Novo Agendamento</button>
    </div>
    <div id="apptsContent"><div class="loading-center"><span class="spinner"></span></div></div>
  `;

  const renderPeriodLabel = () => {
    const periodEl = document.getElementById('currentPeriod');
    const m = currentDate.getMonth();
    const y = currentDate.getFullYear();
    const d = currentDate.getDate();
    const dayNames = ['Sun','Mon','Tue','Wed','Thu','Fri','Sat'];
    const monthNames = ['Jan','Feb','Mar','Apr','May','Jun','Jul','Aug','Sep','Oct','Nov','Dec'];
    
    if (currentView === 'day') {
      const dow = dayNames[currentDate.getDay()];
      periodEl.textContent = `${dow}, ${monthNames[m]} ${d}, ${y}`;
    } else if (currentView === 'week') {
      const startOfWeek = new Date(currentDate);
      startOfWeek.setDate(currentDate.getDate() - currentDate.getDay());
      const endOfWeek = new Date(startOfWeek);
      endOfWeek.setDate(startOfWeek.getDate() + 6);
      periodEl.textContent = `${monthNames[startOfWeek.getMonth()]} ${startOfWeek.getDate()} - ${monthNames[endOfWeek.getMonth()]} ${endOfWeek.getDate()}, ${y}`;
    } else {
      periodEl.textContent = `${monthNames[m]} ${y}`;
    }
  };

  const load = async () => {
    const content = document.getElementById('apptsContent');
    content.innerHTML = '<div class="loading-center"><span class="spinner"></span></div>';

    try {
      let appts;
      if (currentView === 'day') {
        const date = document.getElementById('apptDate').value;
        appts = await api.get(`/appointments?date=${date}`);
        renderDayView(content, appts, date);
      } else if (currentView === 'week') {
        const startOfWeek = new Date(currentDate);
        startOfWeek.setDate(currentDate.getDate() - currentDate.getDay());
        const from = toDateStr(startOfWeek);
        const endOfWeek = new Date(startOfWeek);
        endOfWeek.setDate(startOfWeek.getDate() + 6);
        const to = toDateStr(endOfWeek);
        appts = await api.get(`/appointments?from=${from}&to=${to}`);
        renderWeekView(content, appts, startOfWeek);
      } else {
        const year = currentDate.getFullYear();
        const month = currentDate.getMonth();
        const firstDay = new Date(year, month, 1);
        const lastDay = new Date(year, month + 1, 0);
        const from = toDateStr(firstDay);
        const to = toDateStr(lastDay);
        appts = await api.get(`/appointments?from=${from}&to=${to}`);
        renderMonthView(content, appts, currentDate);
      }
    } catch (e) {
      content.innerHTML = `<div class="empty-state"><div class="empty-state-text">${e.message}</div></div>`;
    }
  };

  const renderDayView = (container, appts, date) => {
    if (!appts.length) {
      container.innerHTML = emptyState(t('appointments.none'));
      return;
    }
    container.innerHTML = `
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
  };

  const renderWeekView = (container, appts, weekStart) => {
    const dayNames = ['Sun','Mon','Tue','Wed','Thu','Fri','Sat'];
    const hours = [];
    for (let h = 6; h <= 21; h++) hours.push(h);

    const apptsByDay = {};
    for (let i = 0; i < 7; i++) {
      const d = new Date(weekStart);
      d.setDate(weekStart.getDate() + i);
      apptsByDay[toDateStr(d)] = [];
    }

    appts.forEach(a => {
      const sessionDate = a.date;
      if (apptsByDay[sessionDate]) {
        apptsByDay[sessionDate].push(a);
      }
    });

    const todayStr = toDateStr(new Date());

    container.innerHTML = `
      <div class="calendar-week" style="overflow-x:auto">
        <table class="calendar-week-table" style="min-width:700px;width:100%;border-collapse:collapse;font-size:0.85rem">
          <thead>
            <tr>
              <th style="width:50px;background:var(--gray-50)"></th>
              ${dayNames.map((d, i) => {
                const dayDate = new Date(weekStart);
                dayDate.setDate(weekStart.getDate() + i);
                const isToday = toDateStr(dayDate) === todayStr;
                return `<th style="padding:0.5rem;text-align:center;background:${isToday ? 'var(--brand-primary)' : 'var(--gray-50)'};color:${isToday ? 'white' : 'inherit'};font-weight:500">${d}<br><small>${dayDate.getDate()}</small></th>`;
              }).join('')}
            </tr>
          </thead>
          <tbody>
            ${hours.map(h => `
              <tr>
                <td style="padding:0.25rem;text-align:center;color:var(--gray-400);font-size:0.75rem;background:var(--gray-50);border-right:1px solid var(--gray-100)">${String(h).padStart(2,'0')}:00</td>
                ${Array(7).fill(0).map((_, dayIdx) => {
                  const dayDate = new Date(weekStart);
                  dayDate.setDate(weekStart.getDate() + dayIdx);
                  const dateStr = toDateStr(dayDate);
                  const dayAppts = (apptsByDay[dateStr] || []).filter(a => {
                    const hour = parseInt(a.startTime.substring(0, 2));
                    return hour === h;
                  });
                  return `<td style="padding:0.25rem;vertical-align:top;min-height:40px;border:1px solid var(--gray-100);background:var(--gray-50)">
                    ${dayAppts.map(a => `
                      <div class="calendar-event" style="background:${a.serviceColor};color:white;padding:2px 4px;border-radius:3px;font-size:0.7rem;margin-bottom:2px;cursor:pointer;white-space:nowrap;overflow:hidden;text-overflow:ellipsis" title="${a.serviceName} - ${a.clientName}">
                        ${formatTime(a.startTime)} ${a.clientName}
                      </div>
                    `).join('')}
                  </td>`;
                }).join('')}
              </tr>
            `).join('')}
          </tbody>
        </table>
      </div>
    `;

    container.querySelectorAll('.calendar-event').forEach(el => {
      el.addEventListener('click', () => {
        const dateInput = document.getElementById('apptDate');
        const dateStr = el.closest('td').cellIndex > 0 ? toDateStr(new Date(weekStart.getTime() + (el.closest('td').cellIndex - 1) * 86400000)) : toDateStr(weekStart);
        dateInput.value = dateStr;
        currentDate = new Date(weekStart.getTime() + (el.closest('td').cellIndex - 1) * 86400000);
        currentView = 'day';
        updateViewButtons();
        load();
      });
    });
  };

  const renderMonthView = (container, appts, monthDate) => {
    const year = monthDate.getFullYear();
    const month = monthDate.getMonth();
    const firstDay = new Date(year, month, 1);
    const lastDay = new Date(year, month + 1, 0);
    const startDayOfWeek = firstDay.getDay();
    const daysInMonth = lastDay.getDate();

    const apptsByDay = {};
    appts.forEach(a => {
      const sessionDate = a.date;
      if (!apptsByDay[sessionDate]) apptsByDay[sessionDate] = [];
      apptsByDay[sessionDate].push(a);
    });

    const dayNames = ['Sun','Mon','Tue','Wed','Thu','Fri','Sat'];
    const todayStr = toDateStr(new Date());

    let cells = '';
    let day = 1;
    for (let week = 0; week < 6; week++) {
      cells += '<tr>';
      for (let dow = 0; dow < 7; dow++) {
        const cellIdx = week * 7 + dow;
        if (cellIdx < startDayOfWeek || day > daysInMonth) {
          cells += '<td style="background:var(--gray-50)"></td>';
        } else {
          const currentDateStr = toDateStr(new Date(year, month, day));
          const isToday = currentDateStr === todayStr;
          const dayAppts = apptsByDay[currentDateStr] || [];
          const isCurrentMonth = true;
          cells += `
            <td class="calendar-month-cell ${isToday ? 'today' : ''}" style="vertical-align:top;min-height:80px;padding:0.25rem;border:1px solid var(--gray-200);cursor:pointer;background:${isToday ? 'rgba(var(--brand-primary-rgb),0.1)' : 'white'}">
              <div style="font-weight:500;font-size:0.85rem;margin-bottom:0.25rem;color:${isCurrentMonth ? 'inherit' : 'var(--gray-400)'}">${day}</div>
              ${dayAppts.slice(0, 3).map(a => `
                <div class="calendar-event" style="background:${a.serviceColor};color:white;padding:1px 3px;border-radius:2px;font-size:0.65rem;margin-bottom:1px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis">
                  ${formatTime(a.startTime)} ${a.clientName}
                </div>
              `).join('')}
              ${dayAppts.length > 3 ? `<div style="font-size:0.65rem;color:var(--gray-500)">+${dayAppts.length - 3} ${t('appointments.appointments')}</div>` : ''}
            </td>
          `;
          day++;
        }
      }
      cells += '</tr>';
      if (day > daysInMonth && week >= Math.ceil((startDayOfWeek + daysInMonth) / 7) - 1) break;
    }

    container.innerHTML = `
      <div class="calendar-month" style="overflow-x:auto">
        <table class="calendar-month-table" style="width:100%;border-collapse:collapse;font-size:0.85rem">
          <thead>
            <tr>
              ${dayNames.map(d => `<th style="padding:0.5rem;text-align:center;background:var(--gray-50);font-weight:500">${d}</th>`).join('')}
            </tr>
          </thead>
          <tbody>${cells}</tbody>
        </table>
      </div>
    `;

    container.querySelectorAll('.calendar-month-cell').forEach(cell => {
      cell.addEventListener('click', () => {
        const dayNum = cell.querySelector('div')?.textContent;
        if (dayNum && cell.children.length > 1) {
          const dateStr = toDateStr(new Date(year, month, parseInt(dayNum)));
          document.getElementById('apptDate').value = dateStr;
          currentDate = new Date(year, month, parseInt(dayNum));
          currentView = 'day';
          updateViewButtons();
          load();
        }
      });
    });
  };

  const updateViewButtons = () => {
    container.querySelectorAll('.btn-toggle-view').forEach(btn => {
      btn.classList.toggle('active', btn.dataset.view === currentView);
    });
    const dateInput = document.getElementById('apptDate');
    const dateNav = document.getElementById('btnLoadAppts');
    const label = container.querySelector('.form-label');
    if (currentView === 'day') {
      dateInput.style.display = '';
      dateNav.style.display = '';
      label.style.display = '';
    } else {
      dateInput.style.display = 'none';
      dateNav.style.display = 'none';
      label.style.display = 'none';
    }
    renderPeriodLabel();
  };

  container.querySelectorAll('.btn-toggle-view').forEach(btn => {
    btn.addEventListener('click', () => {
      currentView = btn.dataset.view;
      updateViewButtons();
      load();
    });
  });

  document.getElementById('btnToday').addEventListener('click', () => {
    currentDate = new Date();
    updateViewButtons();
    load();
  });

  document.getElementById('btnPrev').addEventListener('click', () => {
    if (currentView === 'day') {
      currentDate.setDate(currentDate.getDate() - 1);
      const dateStr = toDateStr(currentDate);
      document.getElementById('apptDate').value = dateStr;
    } else if (currentView === 'week') {
      currentDate.setDate(currentDate.getDate() - 7);
    } else {
      currentDate.setMonth(currentDate.getMonth() - 1);
    }
    updateViewButtons();
    load();
  });

  document.getElementById('btnNext').addEventListener('click', () => {
    if (currentView === 'day') {
      currentDate.setDate(currentDate.getDate() + 1);
      const dateStr = toDateStr(currentDate);
      document.getElementById('apptDate').value = dateStr;
    } else if (currentView === 'week') {
      currentDate.setDate(currentDate.getDate() + 7);
    } else {
      currentDate.setMonth(currentDate.getMonth() + 1);
    }
    updateViewButtons();
    load();
  });

  document.getElementById('btnLoadAppts').addEventListener('click', () => {
    const dateVal = document.getElementById('apptDate').value;
    if (dateVal) {
      const [y, m, d] = dateVal.split('-').map(Number);
      currentDate = new Date(y, m - 1, d);
    }
    currentView = 'day';
    updateViewButtons();
    load();
  });

  document.getElementById('btnNewAppt').addEventListener('click', () => openNewApptModal(load));

  updateViewButtons();
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