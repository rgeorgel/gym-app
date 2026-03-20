import { api } from '../api.js';
import { showToast, confirm } from '../ui.js';
import { getUser } from '../auth.js';
import { t } from '../i18n.js';
import { trackEvent } from '../analytics.js';

const user = getUser();

function toDateStr(d) {
  return `${d.getFullYear()}-${String(d.getMonth()+1).padStart(2,'0')}-${String(d.getDate()).padStart(2,'0')}`;
}

function formatPrice(p) {
  return p != null ? `R$ ${Number(p).toLocaleString('pt-BR', { minimumFractionDigits: 2 })}` : '';
}

function formatTime(t) {
  return t?.substring(0, 5) ?? '';
}

// Main entry point: renders the service catalog with "Agendar" per service
export async function renderSalonBooking(container) {
  container.innerHTML = '<div class="loading-center"><span class="spinner"></span></div>';
  try {
    const services = await api.get('/class-types');
    const active = services.filter(s => s.isActive);
    if (!active.length) {
      container.innerHTML = `<div class="empty-state"><div class="empty-state-icon">💅</div><div class="empty-state-text">${t('services.none')}</div></div>`;
      return;
    }

    // Resume a booking started anonymously from the catalog
    const raw = sessionStorage.getItem('pendingBooking');
    if (raw) {
      sessionStorage.removeItem('pendingBooking');
      try {
        const pending = JSON.parse(raw);
        const svc = active.find(s => s.id === pending.serviceId);
        if (svc && pending.date && pending.time) {
          renderServiceList(container, active);
          confirmBooking(container, svc, pending.date, pending.time, active);
          return;
        }
      } catch { /* ignore malformed entry */ }
    }

    renderServiceList(container, active);
  } catch (e) {
    container.innerHTML = `<div class="empty-state"><div class="empty-state-text">${e.message}</div></div>`;
  }
}

function renderServiceList(container, services) {
  container.innerHTML = `
    <div style="padding:1rem;display:grid;gap:1rem">
      ${services.map(s => `
        <div class="card" style="padding:1.25rem">
          <div style="display:flex;align-items:center;gap:0.75rem;margin-bottom:0.5rem">
            <span style="width:14px;height:14px;border-radius:50%;background:${s.color};display:inline-block;flex-shrink:0"></span>
            <span style="font-weight:600;font-size:1rem">${s.name}</span>
          </div>
          ${s.description ? `<p style="font-size:0.85rem;color:var(--text-muted);margin:0 0 0.75rem">${s.description}</p>` : ''}
          <div style="display:flex;align-items:center;justify-content:space-between;flex-wrap:wrap;gap:0.5rem">
            <div>
              ${s.price != null ? `<div style="font-weight:700;font-size:1.1rem;color:var(--brand-secondary)">${formatPrice(s.price)}</div>` : ''}
              ${s.durationMinutes ? `<div style="font-size:0.8rem;color:var(--text-muted)">${s.durationMinutes} min</div>` : ''}
              <div style="font-size:0.75rem;color:var(--text-muted)">${t('schedule.payAtLocation')}</div>
            </div>
            <button class="btn btn-primary btn-book-service" data-id="${s.id}" data-name="${s.name}">${t('services.book')}</button>
          </div>
        </div>
      `).join('')}
    </div>
  `;

  container.querySelectorAll('.btn-book-service').forEach(btn => {
    btn.addEventListener('click', () => {
      const svc = services.find(s => s.id === btn.dataset.id);
      renderDatePicker(container, svc, services);
    });
  });
}

function renderDatePicker(container, service, services) {
  // Build next 30 days as selectable dates
  const days = [];
  for (let i = 0; i < 30; i++) {
    const d = new Date();
    d.setDate(d.getDate() + i);
    days.push(d);
  }

  const dayNames = ['Dom', 'Seg', 'Ter', 'Qua', 'Qui', 'Sex', 'Sáb'];

  container.innerHTML = `
    <div style="padding:1rem">
      <button class="btn btn-secondary btn-sm" id="btnBackToServices">${t('salonBook.back')}</button>
      <h3 style="margin:1rem 0 0.5rem">${service.name}</h3>
      <p style="font-size:0.9rem;color:var(--text-muted);margin:0 0 1rem">${t('salonBook.selectDate')}</p>
      <div class="day-selector">
        ${days.map(d => `
          <button class="day-btn" data-date="${toDateStr(d)}">
            <span class="day-btn-name">${dayNames[d.getDay()]}</span>
            <span class="day-btn-num">${d.getDate()}</span>
          </button>
        `).join('')}
      </div>
      <div id="slotsArea" style="margin-top:1.5rem"></div>
    </div>
  `;

  document.getElementById('btnBackToServices').addEventListener('click', () => renderServiceList(container, services));

  container.querySelectorAll('.day-btn').forEach(btn => {
    btn.addEventListener('click', async () => {
      container.querySelectorAll('.day-btn').forEach(b => b.classList.remove('active'));
      btn.classList.add('active');
      await renderSlots(container, service, btn.dataset.date, services);
    });
  });
}

async function renderSlots(container, service, date, services) {
  const area = document.getElementById('slotsArea');
  area.innerHTML = `<div class="loading-center"><span class="spinner"></span></div>`;
  try {
    const slots = await api.get(`/slots?date=${date}&serviceId=${service.id}`);
    if (!slots.length) {
      area.innerHTML = `<p style="color:var(--text-muted);font-size:0.9rem">${t('salonBook.noSlots')}</p>`;
      return;
    }
    area.innerHTML = `
      <p style="font-size:0.9rem;font-weight:600;margin:0 0 0.75rem">${t('salonBook.selectSlot')}</p>
      <div style="display:flex;flex-wrap:wrap;gap:0.5rem">
        ${slots.map(slot => `
          <button class="btn btn-secondary btn-slot" data-slot="${slot}" style="min-width:80px">
            ${formatTime(slot)}
          </button>
        `).join('')}
      </div>
    `;

    area.querySelectorAll('.btn-slot').forEach(btn => {
      btn.addEventListener('click', () => confirmBooking(container, service, date, btn.dataset.slot, services));
    });
  } catch (e) {
    area.innerHTML = `<p style="color:var(--text-danger)">${e.message}</p>`;
  }
}

async function confirmBooking(container, service, date, startTime, services) {
  const dateObj = new Date(date + 'T12:00:00');
  const dateLabel = dateObj.toLocaleDateString('pt-BR', { weekday: 'long', day: 'numeric', month: 'long' });

  const slotArea = document.getElementById('slotsArea');
  slotArea.innerHTML = `
    <div class="card" style="padding:1rem;border:2px solid var(--brand-primary)">
      <p style="font-weight:600;margin:0 0 0.5rem">${t('salonBook.confirm')}</p>
      <p style="margin:0;font-size:0.9rem">
        <strong>${service.name}</strong><br>
        📅 ${dateLabel} às ${formatTime(startTime)}
        ${service.durationMinutes ? ` · ${service.durationMinutes}min` : ''}
        ${service.price != null ? `<br>💰 ${formatPrice(service.price)} — ${t('schedule.payAtLocation')}` : ''}
      </p>
      <div style="display:flex;gap:0.5rem;margin-top:1rem">
        <button class="btn btn-secondary" id="btnCancelConfirm">${t('btn.cancel')}</button>
        <button class="btn btn-primary" id="btnConfirmBook">${t('salonBook.confirm')}</button>
      </div>
    </div>
  `;

  document.getElementById('btnCancelConfirm').addEventListener('click', () =>
    renderSlots(container, service, date, services));

  document.getElementById('btnConfirmBook').addEventListener('click', async () => {
    const btn = document.getElementById('btnConfirmBook');
    btn.disabled = true;
    btn.textContent = '...';
    try {
      await api.post('/bookings/salon', {
        date,
        startTime: startTime,
        serviceId: service.id,
        studentId: user.id,
      });
      trackEvent('booking_created', { class_type: service.name, flow: 'salon' });
      showToast(t('salonBook.success'), 'success');
      renderServiceList(container, await api.get('/class-types').then(r => r.filter(s => s.isActive)));
    } catch (e) {
      showToast(t('error.prefix') + e.message, 'error');
      btn.disabled = false;
      btn.textContent = t('salonBook.confirm');
    }
  });
}
