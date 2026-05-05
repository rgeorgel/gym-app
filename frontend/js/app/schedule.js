import { api, getLocationId, setLocationId } from '../api.js?v=202603311200';
import { showToast, createModal, openModal, closeModal, formatTime, emptyState, confirm } from '../ui.js?v=202603311200';
import { getUser } from '../auth.js?v=202603311200';
import { t, getWeekdays } from '../i18n.js?v=202603311200';
import { trackEvent } from '../analytics.js?v=202603311200';
import { tenantType } from '../tenant.js?v=202603311200';

let currentDate = new Date();
let sessions = [];
let packages = [];
let locations = [];
let selectedLocationId = null;
const user = getUser();

function toDateStr(d) {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

export async function renderSchedule(container) {
  container.innerHTML = `
    <div id="locationFilter" style="padding:0.5rem 1rem;border-bottom:1px solid var(--gray-100)"></div>
    <div class="day-selector" id="daySelector"></div>
    <div id="scheduleList" class="sessions-list"><div class="loading-center"><span class="spinner"></span></div></div>
  `;

  const pendingDate = sessionStorage.getItem('pendingGymDate');
  if (pendingDate) {
    sessionStorage.removeItem('pendingGymDate');
    currentDate = new Date(pendingDate + 'T12:00:00');
  }

  await loadLocations();
  if (tenantType !== 'BeautySalon') await loadPackages();
  renderLocationFilter();
  renderDaySelector();
  await loadSessions();
}

async function loadLocations() {
  try {
    locations = await api.get('/locations');
  } catch (e) { 
    locations = []; 
  }
}

async function loadPackages() {
  try {
    packages = await api.get(`/students/${user.id}/packages`);
  } catch (e) { packages = []; }
}

function renderLocationFilter() {
  const container = document.getElementById('locationFilter');
  
  if (locations.length <= 1) {
    container.style.display = 'none';
    return;
  }

  selectedLocationId = getLocationId();
  
  container.innerHTML = `
    <select class="form-control" id="locationSelect" style="font-size:var(--font-size-sm)">
      <option value="">📍 Todos os locais</option>
      ${locations.map(l => `
        <option value="${l.id}" ${selectedLocationId === l.id ? 'selected' : ''}>📍 ${l.name}</option>
      `).join('')}
    </select>
  `;

  document.getElementById('locationSelect').addEventListener('change', async (e) => {
    const newLocationId = e.target.value || null;
    selectedLocationId = newLocationId;
    setLocationId(newLocationId);
    await loadSessions();
  });
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
      const loc = locations.find(l => l.id === s.locationId);
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
              ${loc ? `<span>📍 ${loc.name}</span>` : ''}
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

  // Auto-open pending session from public gym schedule flow
  let pendingGymSession = null;
  try {
    const stored = sessionStorage.getItem('pendingGymSession');
    pendingGymSession = stored ? JSON.parse(stored) : null;
  } catch { }

  const isBeautySalon = tenantType === 'BeautySalon';
  const loc = locations.find(l => l.id === session.locationId);

  // Get available package items for this class type (gym only)
  const items = isBeautySalon ? [] :
    packages.flatMap(p => p.items.filter(i => i.classTypeId === session.classTypeId && i.remainingCredits > 0))
      .filter(Boolean);

  const canBook = !isBooked && !isFull && (isBeautySalon || items.length > 0);

  const bookingBody = isBooked
    ? `<div class="badge badge-success" style="font-size:var(--font-size-sm);padding:0.5rem 1rem">${t('schedule.booked')}</div>`
    : isFull
      ? `<p class="text-sm">${t('schedule.full')}</p>`
      : isBeautySalon
        ? `
          ${session.classTypePrice
            ? `<div style="margin-bottom:0.75rem">
                 <span style="font-size:var(--font-size-sm);color:var(--gray-500)">${t('schedule.servicePrice')}</span>
                 <span style="font-size:1.25rem;font-weight:700;color:var(--brand-secondary);margin-left:0.5rem">R$ ${Number(session.classTypePrice).toFixed(2)}</span>
               </div>`
            : ''}
          <p class="text-sm text-muted">${t('schedule.payAtLocation')}</p>
        `
        : items.length === 0
          ? `<p class="text-sm text-muted">${t('schedule.noCredits')}</p>`
          : `
            <div class="form-group">
              <label class="form-label">${t('schedule.useCreditsFrom')}</label>
              <select class="form-control" id="pkgItemSelect">
                ${items.map(i => `<option value="${i.id}">${i.classTypeName} — ${i.remainingCredits} ${t('dash.credits')}</option>`).join('')}
              </select>
            </div>
          `;

  createModal({
    id: 'sessionModal',
    title: session.classTypeName,
    body: `
      <div style="margin-bottom:1rem">
        <div style="font-size:var(--font-size-sm);color:var(--gray-500)">
          🕐 ${formatTime(session.startTime?.toString())} · ${session.durationMinutes}${t('schedule.duration')}
          ${session.instructorName ? `· 👤 ${session.instructorName}` : ''}
        </div>
        ${loc ? `
        <div style="font-size:var(--font-size-sm);color:var(--gray-500);margin-top:0.25rem">
          📍 ${loc.name}${loc.address ? ` — ${loc.address}` : ''}
        </div>
        ` : ''}
        <div style="font-size:var(--font-size-sm);color:var(--gray-500);margin-top:0.25rem">
          ${session.slotsAvailable} ${t('schedule.slotsAvailable')}
        </div>
      </div>
      ${bookingBody}
    `,
    footer: `
      <button class="btn btn-secondary" onclick="closeModal('sessionModal')">${t('btn.close')}</button>
      ${canBook
        ? `<button class="btn btn-primary" id="btnBook">${isBeautySalon ? t('schedule.bookDirect') : t('schedule.book')}</button>`
        : !isBooked && isFull
          ? `<button class="btn btn-secondary" id="btnWaitlist">${t('schedule.joinWaitlist')}</button>`
          : ''}
      ${isBooked ? `<button class="btn btn-danger" id="btnCancel">${t('schedule.cancelBooking')}</button>` : ''}
    `
  });
  openModal('sessionModal');

  if (pendingGymSession && pendingGymSession.sessionId === sessionId && !isBooked && !isFull) {
    const btnBook = document.getElementById('btnBook');
    if (btnBook) setTimeout(() => btnBook.click(), 100);
  }

  document.getElementById('btnBook')?.addEventListener('click', async () => {
    const body = isBeautySalon
      ? { sessionId, studentId: user.id }
      : { sessionId, studentId: user.id, packageItemId: document.getElementById('pkgItemSelect').value };
    try {
      await api.post('/bookings', body);
      trackEvent('booking_created', { class_type: session.classTypeName });
      showToast(t('schedule.book.success'), 'success');
      closeModal('sessionModal');
      if (!isBeautySalon) await loadPackages();
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
