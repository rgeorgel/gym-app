import { api } from '../api.js';
import { showToast, formatTime, statusBadge, emptyState, confirm } from '../ui.js';
import { getUser } from '../auth.js';
import { t, getMonthNames } from '../i18n.js';
import { trackEvent } from '../analytics.js';

let locations = [];

export async function renderMyBookings(container) {
  container.innerHTML = '<div class="loading-center"><span class="spinner"></span></div>';
  const user = getUser();

  try {
    locations = await api.get('/locations').catch(() => []);
    const bookings = await api.get(`/students/${user.id}/bookings`);
    const today = new Date().toISOString().slice(0, 10);
    const upcoming = bookings.filter(b => b.status !== 'Cancelled' && b.sessionDate >= today);
    const past = bookings.filter(b => b.status === 'Cancelled' || b.sessionDate < today);

    if (!bookings.length) {
      container.innerHTML = emptyState('📅', t('bookings.none'));
      return;
    }

    window._cancelBooking = async (id) => {
      if (!await confirm(t('bookings.cancelConfirm'))) return;
      try {
        await api.delete(`/bookings/${id}`);
        trackEvent('booking_cancelled', { origin: 'my_bookings' });
        showToast(t('bookings.cancel.success'), 'success');
        renderMyBookings(container);
      } catch (e) {
        showToast(t('error.prefix') + e.message, 'error');
      }
    };

    container.innerHTML = `
      ${upcoming.length ? `
        <div style="font-weight:600;color:var(--gray-700);margin-bottom:0.75rem">${t('bookings.upcoming')}</div>
        <div class="card" style="margin-bottom:1.25rem">
          <div class="card-body" style="padding:0">
            ${upcoming.map(b => renderBookingRow(b, true)).join('')}
          </div>
        </div>
      ` : ''}

      ${past.length ? `
        <div style="font-weight:600;color:var(--gray-700);margin-bottom:0.75rem;margin-top:1rem">${t('bookings.history')}</div>
        <div class="card">
          <div class="card-body" style="padding:0">
            ${past.map(b => renderBookingRow(b, false)).join('')}
          </div>
        </div>
      ` : ''}
    `;
  } catch (e) {
    container.innerHTML = `<div class="empty-state"><div class="empty-state-text">${t('error.prefix')}${e.message}</div></div>`;
  }
}

function renderBookingRow(b, showCancel) {
  const d = new Date(b.sessionDate + 'T12:00:00');
  const months = getMonthNames();
  const loc = locations.find(l => l.id === b.locationId);
  return `
    <div class="booking-item">
      <div class="booking-date-block">
        <div class="booking-day">${d.getDate()}</div>
        <div class="booking-month">${months[d.getMonth()]}</div>
      </div>
      <div class="booking-info">
        <div class="booking-class">${b.classTypeName}</div>
        <div class="booking-time">${formatTime(b.sessionStartTime?.toString())}${loc ? ` · 📍 ${loc.name}` : ''}</div>
      </div>
      <div style="display:flex;align-items:center;gap:0.75rem">
        ${statusBadge(b.status)}
        ${showCancel && b.status === 'Confirmed'
          ? `<button class="btn btn-danger btn-sm" onclick="window._cancelBooking('${b.id}')">${t('bookings.cancel')}</button>`
          : ''}
      </div>
    </div>
  `;
}
