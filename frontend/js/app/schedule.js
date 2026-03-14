import { api } from '../api.js';
import { showToast, createModal, openModal, closeModal, formatTime, emptyState, confirm } from '../ui.js';
import { getUser } from '../auth.js';
import { t, getWeekdays } from '../i18n.js';
import { trackEvent } from '../analytics.js';

let currentDate = new Date();
let sessions = [];
let packages = [];
const user = getUser();

function toDateStr(d) {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

export async function renderSchedule(container) {
  container.innerHTML = `
    <div class="day-selector" id="daySelector"></div>
    <div id="scheduleList" class="sessions-list"><div class="loading-center"><span class="spinner"></span></div></div>
  `;

  await loadPackages();
  renderDaySelector();
  await loadSessions();
}

async function loadPackages() {
  try {
    packages = await api.get(`/students/${user.id}/packages`);
  } catch (e) { packages = []; }
}

function renderDaySelector() {
  const container = document.getElementById('daySelector');
  const days = [];
  for (let i = 0; i < 14; i++) {
    const d = new Date();
    d.setDate(d.getDate() + i);
    days.push(d);
  }
  const dayNames = getWeekdays();
  container.innerHTML = days.map((d, i) => {
    const isActive = d.toDateString() === currentDate.toDateString();
    return `
      <button class="day-btn ${isActive ? 'active' : ''}" data-date="${toDateStr(d)}">
        <span class="day-btn-name">${dayNames[d.getDay()]}</span>
        <span class="day-btn-num">${d.getDate()}</span>
      </button>
    `;
  }).join('');

  container.querySelectorAll('.day-btn').forEach(btn => {
    btn.addEventListener('click', async () => {
      container.querySelectorAll('.day-btn').forEach(b => b.classList.remove('active'));
      btn.classList.add('active');
      currentDate = new Date(btn.dataset.date + 'T12:00:00');
      await loadSessions();
    });
  });
}

async function loadSessions() {
  const list = document.getElementById('scheduleList');
  list.innerHTML = '<div class="loading-center"><span class="spinner"></span></div>';

  const dateStr = toDateStr(currentDate);

  try {
    sessions = await api.get(`/sessions?from=${dateStr}&to=${dateStr}`);

    if (!sessions.length) {
      list.innerHTML = emptyState('📅', t('schedule.noClasses'));
      return;
    }

    // Check which sessions the student is already booked for
    const myBookings = await api.get('/bookings').catch(() => []);
    const activeBookings = myBookings.filter(b => b.status === 'Confirmed' || b.status === 'CheckedIn');
    const bookedSessionIds = new Set(activeBookings.map(b => b.sessionId));
    const bookingBySession = Object.fromEntries(activeBookings.map(b => [b.sessionId, b.id]));

    const modalityLabels = {
      Group: t('schedule.modality.group'),
      Individual: t('schedule.modality.individual'),
      Pair: t('schedule.modality.pair'),
    };

    list.innerHTML = sessions.map(s => {
      const isBooked = bookedSessionIds.has(s.id);
      const bookingId = bookingBySession[s.id] ?? null;
      const isFull = s.slotsAvailable <= 0;
      const isCancelled = s.status === 'Cancelled';
      return `
        <div class="session-item ${isFull && !isBooked ? 'full' : ''} ${isBooked ? 'booked' : ''} ${isCancelled ? 'cancelled' : ''}"
             onclick="window._openSession('${s.id}', ${isBooked}, ${isFull}, '${bookingId}')">
          <div class="session-time-block">
            <div class="session-time">${formatTime(s.startTime?.toString())}</div>
            <div class="session-duration">${s.durationMinutes}${t('schedule.duration')}</div>
          </div>
          <div class="session-color-dot" style="background:${s.classTypeColor}"></div>
          <div class="session-info">
            <div class="session-name">${s.classTypeName}</div>
            <div class="session-meta">
              ${s.instructorName ? `<span>👤 ${s.instructorName}</span>` : ''}
              <span>${modalityLabels[s.modalityType] ?? ''}</span>
            </div>
          </div>
          <div class="session-slots">
            ${isCancelled
              ? `<span class="badge badge-danger">${t('schedule.cancelled')}</span>`
              : isBooked
                ? `<span class="badge badge-success" style="white-space:normal;text-align:center;line-height:1.3">✓ ${t('schedule.booked').replace('✓ ', '').split('.')[0]}</span>`
                : `<div class="slots-count">${t('schedule.slots')}</div>
                   <div class="slots-available ${s.slotsAvailable <= 3 ? 'low' : ''} ${isFull ? 'full' : ''}">${isFull ? '0' : s.slotsAvailable}</div>`}
          </div>
        </div>
      `;
    }).join('');

    window._openSession = (id, isBooked, isFull, bookingId) => openSessionModal(id, isBooked, isFull, bookingId);
  } catch (e) {
    list.innerHTML = `<div class="empty-state"><div class="empty-state-text">${t('schedule.loadError')}${e.message}</div></div>`;
  }
}

async function openSessionModal(sessionId, isBooked, isFull, bookingId) {
  const session = sessions.find(s => s.id === sessionId);
  if (!session) return;
  trackEvent('view_class', { class_type: session.classTypeName });

  // Get available package items for this class type
  const items = packages.flatMap(p => p.items.filter(i => i.classTypeId === session.classTypeId && i.remainingCredits > 0))
    .filter(Boolean);

  createModal({
    id: 'sessionModal',
    title: session.classTypeName,
    body: `
      <div style="margin-bottom:1rem">
        <div style="font-size:var(--font-size-sm);color:var(--gray-500)">
          🕐 ${formatTime(session.startTime?.toString())} · ${session.durationMinutes}${t('schedule.duration')}
          ${session.instructorName ? `· 👤 ${session.instructorName}` : ''}
        </div>
        <div style="font-size:var(--font-size-sm);color:var(--gray-500);margin-top:0.25rem">
          ${session.slotsAvailable} ${t('schedule.slotsAvailable')}
        </div>
      </div>

      ${isBooked
        ? `<div class="badge badge-success" style="font-size:var(--font-size-sm);padding:0.5rem 1rem">${t('schedule.booked')}</div>`
        : isFull
          ? `<p class="text-sm">${t('schedule.full')}</p>`
          : items.length === 0
            ? `<p class="text-sm text-muted">${t('schedule.noCredits')}</p>`
            : `
              <div class="form-group">
                <label class="form-label">${t('schedule.useCreditsFrom')}</label>
                <select class="form-control" id="pkgItemSelect">
                  ${items.map(i => `<option value="${i.id}">${i.classTypeName} — ${i.remainingCredits} ${t('dash.credits')}</option>`).join('')}
                </select>
              </div>
            `
      }
    `,
    footer: `
      <button class="btn btn-secondary" onclick="closeModal('sessionModal')">${t('btn.close')}</button>
      ${!isBooked && !isFull && items.length > 0
        ? `<button class="btn btn-primary" id="btnBook">${t('schedule.book')}</button>`
        : !isBooked && isFull
          ? `<button class="btn btn-secondary" id="btnWaitlist">${t('schedule.joinWaitlist')}</button>`
          : ''}
      ${isBooked ? `<button class="btn btn-danger" id="btnCancel">${t('schedule.cancelBooking')}</button>` : ''}
    `
  });
  openModal('sessionModal');

  document.getElementById('btnBook')?.addEventListener('click', async () => {
    const pkgItemId = document.getElementById('pkgItemSelect').value;
    try {
      await api.post('/bookings', { sessionId, studentId: user.id, packageItemId: pkgItemId });
      trackEvent('booking_created', { class_type: session.classTypeName });
      showToast(t('schedule.book.success'), 'success');
      closeModal('sessionModal');
      await loadPackages();
      await loadSessions();
    } catch (e) {
      showToast(t('error.prefix') + e.message, 'error');
    }
  });

  document.getElementById('btnCancel')?.addEventListener('click', async () => {
    if (!await confirm(t('schedule.cancelBooking.confirm'))) return;
    try {
      await api.delete(`/bookings/${bookingId}`);
      trackEvent('booking_cancelled', { class_type: session.classTypeName, origin: 'schedule' });
      showToast(t('schedule.cancelBooking.success'), 'success');
      closeModal('sessionModal');
      await loadPackages();
      await loadSessions();
    } catch (e) {
      showToast(t('error.prefix') + e.message, 'error');
    }
  });
}
