import { api } from '../api.js';
import { showToast, createModal, openModal, closeModal, formatTime, emptyState } from '../ui.js';
import { getUser } from '../auth.js';

let currentDate = new Date();
let sessions = [];
let packages = [];
const user = getUser();

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
  container.innerHTML = days.map((d, i) => {
    const isActive = d.toDateString() === currentDate.toDateString();
    const dayNames = ['Dom','Seg','Ter','Qua','Qui','Sex','Sáb'];
    return `
      <button class="day-btn ${isActive ? 'active' : ''}" data-date="${d.toISOString().split('T')[0]}">
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

  const dateStr = currentDate.toISOString().split('T')[0];

  try {
    sessions = await api.get(`/sessions?from=${dateStr}&to=${dateStr}`);

    if (!sessions.length) {
      list.innerHTML = emptyState('📅', 'Nenhuma aula hoje');
      return;
    }

    // Check which sessions the student is already booked for
    const myBookings = await api.get('/bookings?mine=true').catch(() => []);
    const bookedSessionIds = new Set(myBookings.filter(b => b.status === 'Confirmed' || b.status === 'CheckedIn').map(b => b.sessionId));

    list.innerHTML = sessions.map(s => {
      const isBooked = bookedSessionIds.has(s.id);
      const isFull = s.slotsAvailable <= 0;
      const isCancelled = s.status === 'Cancelled';
      return `
        <div class="session-item ${isFull && !isBooked ? 'full' : ''} ${isBooked ? 'booked' : ''} ${isCancelled ? 'cancelled' : ''}"
             onclick="window._openSession('${s.id}', ${isBooked}, ${isFull})">
          <div class="session-time-block">
            <div class="session-time">${formatTime(s.startTime?.toString())}</div>
            <div class="session-duration">${s.durationMinutes}min</div>
          </div>
          <div class="session-color-dot" style="background:${s.classTypeColor}"></div>
          <div class="session-info">
            <div class="session-name">${s.classTypeName}</div>
            <div class="session-meta">
              ${s.instructorName ? `<span>👤 ${s.instructorName}</span>` : ''}
              <span>{{ modalityLabel }}</span>
            </div>
          </div>
          <div class="session-slots">
            ${isCancelled
              ? '<span class="badge badge-danger">Cancelada</span>'
              : isBooked
                ? '<span class="badge badge-success">✓ Agendado</span>'
                : `<div class="slots-count">Vagas</div>
                   <div class="slots-available ${s.slotsAvailable <= 3 ? 'low' : ''} ${isFull ? 'full' : ''}">${isFull ? '0' : s.slotsAvailable}</div>`}
          </div>
        </div>
      `.replace('{{ modalityLabel }}', { Group: 'Grupo', Individual: 'Individual', Pair: 'Dupla' }[s.modalityType] ?? '');
    }).join('');

    window._openSession = (id, isBooked, isFull) => openSessionModal(id, isBooked, isFull);
  } catch (e) {
    list.innerHTML = `<div class="empty-state"><div class="empty-state-text">Erro ao carregar: ${e.message}</div></div>`;
  }
}

async function openSessionModal(sessionId, isBooked, isFull) {
  const session = sessions.find(s => s.id === sessionId);
  if (!session) return;

  // Get available package items for this class type
  const items = packages.flatMap(p => p.items.filter(i => i.classTypeId === session.classTypeId && i.remainingCredits > 0))
    .filter(Boolean);

  createModal({
    id: 'sessionModal',
    title: session.classTypeName,
    body: `
      <div style="margin-bottom:1rem">
        <div style="font-size:var(--font-size-sm);color:var(--gray-500)">
          🕐 ${formatTime(session.startTime?.toString())} · ${session.durationMinutes}min
          ${session.instructorName ? `· 👤 ${session.instructorName}` : ''}
        </div>
        <div style="font-size:var(--font-size-sm);color:var(--gray-500);margin-top:0.25rem">
          ${session.slotsAvailable} vagas disponíveis
        </div>
      </div>

      ${isBooked
        ? `<div class="badge badge-success" style="font-size:var(--font-size-sm);padding:0.5rem 1rem">✓ Você já está agendado nesta aula</div>`
        : isFull
          ? `<p class="text-sm">Esta aula está cheia. Deseja entrar na lista de espera?</p>`
          : items.length === 0
            ? `<p class="text-sm text-muted">Você não tem créditos disponíveis para esta modalidade.</p>`
            : `
              <div class="form-group">
                <label class="form-label">Usar créditos de:</label>
                <select class="form-control" id="pkgItemSelect">
                  ${items.map(i => `<option value="${i.id}">${i.classTypeName} — ${i.remainingCredits} créditos (${i.classTypeName})</option>`).join('')}
                </select>
              </div>
            `
      }
    `,
    footer: `
      <button class="btn btn-secondary" onclick="closeModal('sessionModal')">Fechar</button>
      ${!isBooked && !isFull && items.length > 0
        ? `<button class="btn btn-primary" id="btnBook">Agendar</button>`
        : !isBooked && isFull
          ? `<button class="btn btn-secondary" id="btnWaitlist">Entrar na fila</button>`
          : ''}
      ${isBooked ? `<button class="btn btn-danger" id="btnCancel">Cancelar agendamento</button>` : ''}
    `
  });
  openModal('sessionModal');

  document.getElementById('btnBook')?.addEventListener('click', async () => {
    const pkgItemId = document.getElementById('pkgItemSelect').value;
    try {
      await api.post('/bookings', { sessionId, studentId: user.id, packageItemId: pkgItemId });
      showToast('Agendamento confirmado!', 'success');
      closeModal('sessionModal');
      await loadPackages();
      await loadSessions();
    } catch (e) {
      showToast('Erro: ' + e.message, 'error');
    }
  });
}
