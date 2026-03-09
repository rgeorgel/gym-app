import { api } from '../api.js';
import { showToast, formatTime, statusBadge, emptyState, confirm } from '../ui.js';
import { getUser } from '../auth.js';

export async function renderMyBookings(container) {
  container.innerHTML = '<div class="loading-center"><span class="spinner"></span></div>';
  const user = getUser();

  try {
    const bookings = await api.get(`/students/${user.id}/bookings`);
    const upcoming = bookings.filter(b => b.status !== 'Cancelled' && new Date(b.sessionDate + 'T' + (b.sessionStartTime ?? '00:00:00')) >= new Date());
    const past = bookings.filter(b => b.status === 'Cancelled' || new Date(b.sessionDate + 'T' + (b.sessionStartTime ?? '00:00:00')) < new Date());

    if (!bookings.length) {
      container.innerHTML = emptyState('📅', 'Nenhum agendamento encontrado');
      return;
    }

    container.innerHTML = `
      ${upcoming.length ? `
        <div style="font-weight:600;color:var(--gray-700);margin-bottom:0.75rem">Próximas aulas</div>
        <div class="card" style="margin-bottom:1.25rem">
          <div class="card-body" style="padding:0">
            ${upcoming.map(b => renderBookingRow(b, true)).join('')}
          </div>
        </div>
      ` : ''}

      ${past.length ? `
        <div style="font-weight:600;color:var(--gray-700);margin-bottom:0.75rem;margin-top:1rem">Histórico</div>
        <div class="card">
          <div class="card-body" style="padding:0">
            ${past.map(b => renderBookingRow(b, false)).join('')}
          </div>
        </div>
      ` : ''}
    `;

    // Event listeners for cancel buttons
    container.querySelectorAll('.btn-cancel-booking').forEach(btn => {
      btn.addEventListener('click', async () => {
        if (!confirm('Cancelar este agendamento?')) return;
        try {
          await api.delete(`/bookings/${btn.dataset.id}`);
          showToast('Agendamento cancelado', 'success');
          renderMyBookings(container);
        } catch (e) {
          showToast('Erro: ' + e.message, 'error');
        }
      });
    });
  } catch (e) {
    container.innerHTML = `<div class="empty-state"><div class="empty-state-text">Erro: ${e.message}</div></div>`;
  }
}

function renderBookingRow(b, showCancel) {
  const d = new Date(b.sessionDate + 'T12:00:00');
  const months = ['Jan','Fev','Mar','Abr','Mai','Jun','Jul','Ago','Set','Out','Nov','Dez'];
  return `
    <div class="booking-item">
      <div class="booking-date-block">
        <div class="booking-day">${d.getDate()}</div>
        <div class="booking-month">${months[d.getMonth()]}</div>
      </div>
      <div class="booking-info">
        <div class="booking-class">${b.classTypeName}</div>
        <div class="booking-time">${formatTime(b.sessionStartTime?.toString())}</div>
      </div>
      <div style="display:flex;align-items:center;gap:0.75rem">
        ${statusBadge(b.status)}
        ${showCancel && b.status === 'Confirmed'
          ? `<button class="btn btn-danger btn-sm btn-cancel-booking" data-id="${b.id}">Cancelar</button>`
          : ''}
      </div>
    </div>
  `;
}
