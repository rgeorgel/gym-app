import { api } from '../api.js';
import { showToast, formatTime, emptyState, statusBadge } from '../ui.js';
import { t } from '../i18n.js';
import { renderStudentDetail } from './student-detail.js';

export async function renderAppointments(container) {
  const toDateStr = d => `${d.getFullYear()}-${String(d.getMonth()+1).padStart(2,'0')}-${String(d.getDate()).padStart(2,'0')}`;
  const today = toDateStr(new Date());

  let currentView = 'day';
  let currentDate = new Date();

  container.innerHTML = `
    <div class="appts-toolbar">
      <div class="appts-toolbar-row appts-toolbar-row--nav">
        <div class="view-toggle">
          <button class="btn btn-sm btn-toggle-view active" data-view="day">${t('appointments.view.day')}</button>
          <button class="btn btn-sm btn-toggle-view" data-view="week">${t('appointments.view.week')}</button>
          <button class="btn btn-sm btn-toggle-view" data-view="month">${t('appointments.view.month')}</button>
        </div>
        <div class="appts-nav">
          <button class="btn btn-sm btn-secondary" id="btnToday">${t('appointments.today')}</button>
          <button class="btn btn-sm btn-secondary appts-nav-arrow" id="btnPrev">‹</button>
          <span id="currentPeriod" class="appts-period"></span>
          <button class="btn btn-sm btn-secondary appts-nav-arrow" id="btnNext">›</button>
        </div>
      </div>
      <div class="appts-toolbar-row appts-toolbar-row--date" id="dateRow">
        <input class="form-control appts-date-input" id="apptDate" type="date" value="${today}">
        <button class="btn btn-secondary btn-sm" id="btnLoadAppts">${t('btn.load')}</button>
        <button class="btn btn-primary btn-new-appt" id="btnNewAppt">${t('appointments.new')}</button>
      </div>
    </div>
    <div id="apptsContent"><div class="loading-center"><span class="spinner"></span></div></div>
  `;

  const renderPeriodLabel = () => {
    const periodEl = document.getElementById('currentPeriod');
    const m = currentDate.getMonth();
    const y = currentDate.getFullYear();
    const d = currentDate.getDate();
    const dayNames = ['Dom','Seg','Ter','Qua','Qui','Sex','Sáb'];
    const monthNames = ['Jan','Fev','Mar','Abr','Mai','Jun','Jul','Ago','Set','Out','Nov','Dez'];

    if (currentView === 'day') {
      const dow = dayNames[currentDate.getDay()];
      periodEl.textContent = `${dow}, ${d} ${monthNames[m]}`;
    } else if (currentView === 'week') {
      const startOfWeek = new Date(currentDate);
      startOfWeek.setDate(currentDate.getDate() - currentDate.getDay());
      const endOfWeek = new Date(startOfWeek);
      endOfWeek.setDate(startOfWeek.getDate() + 6);
      periodEl.textContent = `${startOfWeek.getDate()} ${monthNames[startOfWeek.getMonth()]} – ${endOfWeek.getDate()} ${monthNames[endOfWeek.getMonth()]}`;
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

    container.innerHTML = `<div class="appt-list"></div>`;
    const list = container.querySelector('.appt-list');

    appts.forEach(a => {
      const priceStr = a.servicePrice != null
        ? `R$ ${Number(a.servicePrice).toLocaleString('pt-BR', { minimumFractionDigits: 2 })}`
        : null;
      const canCheckin = a.status === 'Confirmed';

      const card = document.createElement('div');
      card.className = 'appt-card';
      card.innerHTML = `
        <div class="appt-card-time">
          <div class="appt-card-hour">${formatTime(a.startTime)}</div>
          <div class="appt-card-duration">${a.durationMinutes}min</div>
        </div>
        <div class="appt-card-body">
          <div class="appt-card-client">${a.clientName}</div>
          <div class="appt-card-service">
            <span class="color-dot" style="background:${a.serviceColor ?? '#888'}"></span>
            ${a.serviceName}
          </div>
          ${a.professionalName ? `<div style="font-size:0.8rem;color:var(--text-muted);margin-top:0.15rem">✂️ ${a.professionalName}</div>` : ''}
          ${a.clientPhone || priceStr ? `
          <div class="appt-card-meta">
            ${a.clientPhone ? `<a class="appt-card-phone" href="tel:${a.clientPhone}">📞 ${a.clientPhone}</a>` : ''}
            ${priceStr ? `<span class="appt-card-price">${priceStr}</span>` : ''}
          </div>` : ''}
        </div>
        <div class="appt-card-actions">
          ${statusBadge(a.status)}
          ${canCheckin ? `<button class="btn btn-sm btn-primary btn-checkin" data-id="${a.bookingId}">${t('appointments.checkin')}</button>` : ''}
          <button class="btn btn-sm btn-secondary btn-client-detail" data-id="${a.clientId}" title="Ver detalhes do cliente">👤</button>
        </div>
      `;
      list.appendChild(card);
    });

    container.querySelectorAll('.btn-checkin').forEach(btn => {
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

    const outerEl = container.closest('#contentArea') ?? container.parentElement;
    container.querySelectorAll('.btn-client-detail').forEach(btn => {
      btn.addEventListener('click', () =>
        renderStudentDetail(outerEl, btn.dataset.id, () => renderAppointments(outerEl))
      );
    });
  };

  // ── Agenda view (mobile alternative for week/month) ──────────────────────
  const renderAgendaView = (container, appts, days, skipEmpty = false) => {
    const dayNames = ['Dom','Seg','Ter','Qua','Qui','Sex','Sáb'];
    const monthNames = ['Jan','Fev','Mar','Abr','Mai','Jun','Jul','Ago','Set','Out','Nov','Dez'];
    const todayStr = toDateStr(new Date());

    const apptsByDay = {};
    appts.forEach(a => {
      if (!apptsByDay[a.date]) apptsByDay[a.date] = [];
      apptsByDay[a.date].push(a);
    });

    const visibleDays = skipEmpty ? days.filter(d => (apptsByDay[toDateStr(d)] || []).length > 0) : days;

    if (!visibleDays.length) {
      container.innerHTML = emptyState(t('appointments.none'));
      return;
    }

    container.innerHTML = '<div class="agenda-view"></div>';
    const agenda = container.querySelector('.agenda-view');

    for (const day of visibleDays) {
      const dateStr = toDateStr(day);
      const dayAppts = (apptsByDay[dateStr] || []).slice().sort((a, b) => a.startTime.localeCompare(b.startTime));
      const isToday = dateStr === todayStr;

      const section = document.createElement('div');
      section.className = 'agenda-day';
      section.innerHTML = `
        <div class="agenda-day-header${isToday ? ' agenda-day-header--today' : ''}">
          <span class="agenda-day-name">${dayNames[day.getDay()]}</span>
          <span>${day.getDate()} ${monthNames[day.getMonth()]}</span>
          ${dayAppts.length ? `<span class="agenda-day-count">${dayAppts.length}</span>` : ''}
        </div>
        ${!dayAppts.length ? `<div class="agenda-day-empty">Sem agendamentos</div>` : ''}
        ${dayAppts.map(a => `
          <div class="agenda-appt" data-date="${dateStr}">
            <span class="agenda-appt-time">${formatTime(a.startTime)}</span>
            <span class="agenda-appt-dot" style="background:${a.serviceColor ?? '#888'}"></span>
            <div class="agenda-appt-info">
              <div class="agenda-appt-client">${a.clientName}</div>
              <div class="agenda-appt-service">${a.serviceName}${a.professionalName ? ` · ${a.professionalName}` : ''}</div>
            </div>
            ${statusBadge(a.status)}
          </div>
        `).join('')}
      `;
      agenda.appendChild(section);
    }

    container.querySelectorAll('.agenda-appt').forEach(el => {
      el.addEventListener('click', () => {
        const dateStr = el.dataset.date;
        document.getElementById('apptDate').value = dateStr;
        const [y, m, d] = dateStr.split('-').map(Number);
        currentDate = new Date(y, m - 1, d);
        currentView = 'day';
        updateViewButtons();
        load();
      });
    });
  };

  const renderWeekView = (container, appts, weekStart) => {
    if (window.innerWidth < 640) {
      const days = Array.from({ length: 7 }, (_, i) => {
        const d = new Date(weekStart);
        d.setDate(weekStart.getDate() + i);
        return d;
      });
      renderAgendaView(container, appts, days, false);
      return;
    }
    const dayNames = ['Dom','Seg','Ter','Qua','Qui','Sex','Sáb'];
    const hours = [];
    for (let h = 6; h <= 21; h++) hours.push(h);

    const apptsByDay = {};
    for (let i = 0; i < 7; i++) {
      const d = new Date(weekStart);
      d.setDate(weekStart.getDate() + i);
      apptsByDay[toDateStr(d)] = [];
    }

    appts.forEach(a => {
      if (apptsByDay[a.date]) apptsByDay[a.date].push(a);
    });

    const todayStr = toDateStr(new Date());

    container.innerHTML = `
      <div class="calendar-week" style="overflow-x:auto">
        <table class="calendar-week-table" style="min-width:600px;width:100%;border-collapse:collapse;font-size:0.85rem">
          <thead>
            <tr>
              <th style="width:48px;background:var(--gray-50)"></th>
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
                  const dayAppts = (apptsByDay[dateStr] || []).filter(a => parseInt(a.startTime) === h);
                  return `<td style="padding:0.25rem;vertical-align:top;min-height:40px;border:1px solid var(--gray-100);background:var(--gray-50)" data-date="${dateStr}">
                    ${dayAppts.map(a => `
                      <div class="calendar-event" style="background:${a.serviceColor};color:white;padding:2px 4px;border-radius:3px;font-size:0.7rem;margin-bottom:2px;cursor:pointer;white-space:nowrap;overflow:hidden;text-overflow:ellipsis" data-date="${dateStr}" title="${a.serviceName} - ${a.clientName}">
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
        const dateStr = el.dataset.date;
        if (!dateStr) return;
        document.getElementById('apptDate').value = dateStr;
        const [y, m, d] = dateStr.split('-').map(Number);
        currentDate = new Date(y, m - 1, d);
        currentView = 'day';
        updateViewButtons();
        load();
      });
    });
  };

  const renderMonthView = (container, appts, monthDate) => {
    if (window.innerWidth < 640) {
      const year = monthDate.getFullYear();
      const month = monthDate.getMonth();
      const daysInMonth = new Date(year, month + 1, 0).getDate();
      const days = Array.from({ length: daysInMonth }, (_, i) => new Date(year, month, i + 1));
      renderAgendaView(container, appts, days, true);
      return;
    }
    const year = monthDate.getFullYear();
    const month = monthDate.getMonth();
    const firstDay = new Date(year, month, 1);
    const lastDay = new Date(year, month + 1, 0);
    const startDayOfWeek = firstDay.getDay();
    const daysInMonth = lastDay.getDate();

    const apptsByDay = {};
    appts.forEach(a => {
      if (!apptsByDay[a.date]) apptsByDay[a.date] = [];
      apptsByDay[a.date].push(a);
    });

    const dayNames = ['Dom','Seg','Ter','Qua','Qui','Sex','Sáb'];
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
          cells += `
            <td class="calendar-month-cell ${isToday ? 'today' : ''}" data-date="${currentDateStr}" style="vertical-align:top;min-height:80px;padding:0.25rem;border:1px solid var(--gray-200);cursor:pointer;background:${isToday ? 'rgba(0,123,255,0.08)' : 'white'}">
              <div style="font-weight:500;font-size:0.85rem;margin-bottom:0.25rem">${day}</div>
              ${dayAppts.slice(0, 3).map(a => `
                <div class="calendar-event" style="background:${a.serviceColor};color:white;padding:1px 3px;border-radius:2px;font-size:0.65rem;margin-bottom:1px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis">
                  ${formatTime(a.startTime)} ${a.clientName}
                </div>
              `).join('')}
              ${dayAppts.length > 3 ? `<div style="font-size:0.65rem;color:var(--gray-500)">+${dayAppts.length - 3} mais</div>` : ''}
            </td>
          `;
          day++;
        }
      }
      cells += '</tr>';
      if (day > daysInMonth) break;
    }

    container.innerHTML = `
      <div class="calendar-month" style="overflow-x:auto">
        <table class="calendar-month-table" style="width:100%;border-collapse:collapse;font-size:0.85rem">
          <thead>
            <tr>${dayNames.map(d => `<th style="padding:0.5rem;text-align:center;background:var(--gray-50);font-weight:500">${d}</th>`).join('')}</tr>
          </thead>
          <tbody>${cells}</tbody>
        </table>
      </div>
    `;

    container.querySelectorAll('.calendar-month-cell').forEach(cell => {
      cell.addEventListener('click', () => {
        const dateStr = cell.dataset.date;
        if (!dateStr) return;
        document.getElementById('apptDate').value = dateStr;
        const [y, m, d] = dateStr.split('-').map(Number);
        currentDate = new Date(y, m - 1, d);
        currentView = 'day';
        updateViewButtons();
        load();
      });
    });
  };

  const updateViewButtons = () => {
    container.querySelectorAll('.btn-toggle-view').forEach(btn => {
      btn.classList.toggle('active', btn.dataset.view === currentView);
    });
    const dateRow = document.getElementById('dateRow');
    if (dateRow) dateRow.style.display = currentView === 'day' ? '' : 'none';
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
    document.getElementById('apptDate').value = today;
    updateViewButtons();
    load();
  });

  document.getElementById('btnPrev').addEventListener('click', () => {
    if (currentView === 'day') {
      currentDate.setDate(currentDate.getDate() - 1);
      document.getElementById('apptDate').value = toDateStr(currentDate);
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
      document.getElementById('apptDate').value = toDateStr(currentDate);
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

        <div class="form-group" id="proGroup" style="display:none">
          <label class="form-label">Profissional</label>
          <select class="form-control" id="proSelect"></select>
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

  // Load services and professionals in parallel
  Promise.all([
    api.get('/class-types'),
    api.get('/professionals').catch(() => []),
  ]).then(([services, professionals]) => {
    const sel = overlay.querySelector('#serviceSelect');
    const active = services.filter(s => s.isActive);
    sel.innerHTML = `<option value="">Selecionar serviço…</option>` +
      active.map(s => `<option value="${s.id}" data-duration="${s.durationMinutes ?? ''}">${s.name}${s.durationMinutes ? ` (${s.durationMinutes}min)` : ''}</option>`).join('');

    if (professionals.length > 0) {
      const proSel = overlay.querySelector('#proSelect');
      proSel.innerHTML = `<option value="">Qualquer disponível</option>` +
        professionals.map(p => `<option value="${p.id}">${p.name}</option>`).join('');
      overlay.querySelector('#proGroup').style.display = 'block';
      proSel.addEventListener('change', loadSlots);
    }
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
      const professionalId = overlay.querySelector('#proSelect')?.value || null;
      const query = `/slots?date=${date}&serviceId=${serviceId}${professionalId ? `&professionalId=${professionalId}` : ''}`;
      const slots = await api.get(query);
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
          slotsContainer.querySelectorAll('.btn-slot-pick').forEach(b => { b.classList.remove('btn-primary'); b.classList.add('btn-secondary'); });
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
      const professionalId = overlay.querySelector('#proSelect')?.value || undefined;
      await api.post('/bookings/salon', {
        studentId: overlay.querySelector('#clientId').value,
        serviceId: overlay.querySelector('#serviceSelect').value,
        date: overlay.querySelector('#apptDateNew').value,
        startTime: overlay.querySelector('#selectedSlot').value,
        professionalId,
      });
      showToast('Agendamento criado com sucesso!', 'success');
      close();
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
