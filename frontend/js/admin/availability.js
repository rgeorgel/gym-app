import { api } from '../api.js?v=202603311200';
import { showToast, createModal, openModal, closeModal, emptyState, confirm } from '../ui.js?v=202603311200';
import { t, getWeekdays } from '../i18n.js?v=202603311200';

const WEEKDAYS = ['Dom', 'Seg', 'Ter', 'Qua', 'Qui', 'Sex', 'Sáb'];

export async function renderAvailability(container) {
  container.innerHTML = `
    <div style="display:flex;justify-content:flex-end;margin-bottom:1rem">
      <button class="btn btn-primary" id="btnNewBlock">${t('availability.add')}</button>
    </div>
    <div id="availabilityContent"><div class="loading-center"><span class="spinner"></span></div></div>

    <!-- Vacation Blocks section -->
    <div style="margin-top:2rem">
      <div style="display:flex;align-items:center;justify-content:space-between;margin-bottom:1rem">
        <div>
          <h2 style="margin:0;font-size:1.1rem">🏖️ Férias / Feriados</h2>
          <p class="text-sm text-muted" style="margin:0.25rem 0 0">Bloqueie um período completo. Os clientes não conseguirão agendar nenhum horário nesses dias.</p>
        </div>
        <button class="btn btn-secondary" id="btnNewVacation">+ Novo período</button>
      </div>
      <div id="vacationBlocksContent"><div class="loading-center"><span class="spinner"></span></div></div>
    </div>

    <!-- Time Blocks section -->
    <div style="margin-top:2rem">
      <div style="display:flex;align-items:center;justify-content:space-between;margin-bottom:1rem">
        <div>
          <h2 style="margin:0;font-size:1.1rem">🚫 Bloqueios pontuais</h2>
          <p class="text-sm text-muted" style="margin:0.25rem 0 0">Bloqueie horários específicos em datas avulsas. Ex: "hoje das 14h às 16h não atendo".</p>
        </div>
        <button class="btn btn-secondary" id="btnNewTimeBlock">+ Novo bloqueio</button>
      </div>
      <div id="timeBlocksContent"><div class="loading-center"><span class="spinner"></span></div></div>
    </div>
  `;

  document.getElementById('btnNewBlock').addEventListener('click', () => openBlockModal());
  document.getElementById('btnNewVacation').addEventListener('click', () => openVacationModal());
  document.getElementById('btnNewTimeBlock').addEventListener('click', () => openTimeBlockModal());

  await Promise.all([loadBlocks(), loadVacationBlocks(), loadTimeBlocks()]);
}

// ── Recurring availability ────────────────────────────────────────────────────

async function loadBlocks() {
  const content = document.getElementById('availabilityContent');
  try {
    const [blocks, instructors] = await Promise.all([
      api.get('/availability'),
      api.get('/instructors').catch(() => []),
    ]);

    if (!blocks.length) {
      content.innerHTML = `
        <div class="card" style="padding:2rem">
          ${emptyState('🗓️', t('availability.none'))}
          <p class="text-sm text-muted" style="text-align:center;margin-top:1rem">
            Configure os dias e horários de atendimento. O sistema calculará automaticamente os horários disponíveis para cada serviço.
          </p>
        </div>
      `;
      return;
    }

    const weekdays = getWeekdays ? getWeekdays() : ['Dom','Seg','Ter','Qua','Qui','Sex','Sáb'];

    content.innerHTML = `
      <div class="avail-list">
        ${blocks.map(b => `
          <div class="avail-card">
            <div class="avail-card-day">${weekdays[b.weekday] ?? b.weekday}</div>
            <div class="avail-card-time">
              <span class="avail-time-range">⏰ ${b.startTime?.substring(0,5)} – ${b.endTime?.substring(0,5)}</span>
              ${b.instructorName ? `<span class="avail-instructor">✂️ ${b.instructorName}</span>` : ''}
            </div>
            <button class="btn btn-sm btn-danger btn-del" data-id="${b.id}">${t('btn.delete')}</button>
          </div>
        `).join('')}
      </div>`;

    content.querySelectorAll('.btn-del').forEach(btn => {
      btn.addEventListener('click', async () => {
        if (!await confirm(t('availability.delete.confirm'))) return;
        try {
          await api.delete(`/availability/${btn.dataset.id}`);
          showToast(t('availability.deleted'), 'success');
          await loadBlocks();
        } catch (e) {
          showToast(t('error.prefix') + e.message, 'error');
        }
      });
    });

    window._availInstructors = instructors;
  } catch (e) {
    content.innerHTML = `<div class="empty-state"><div class="empty-state-text">${e.message}</div></div>`;
  }
}

function openBlockModal() {
  const instructors = window._availInstructors ?? [];
  const weekdays = getWeekdays ? getWeekdays() : ['Dom','Seg','Ter','Qua','Qui','Sex','Sáb'];

  createModal({
    id: 'availModal',
    title: t('availability.modal.title'),
    body: `
      <div class="form-group">
        <label class="form-label">${t('availability.weekday')} *</label>
        <select class="form-control" id="availWeekday">
          ${weekdays.map((d, i) => `<option value="${i}">${d}</option>`).join('')}
        </select>
      </div>
      <div style="display:grid;grid-template-columns:1fr 1fr;gap:1rem">
        <div class="form-group">
          <label class="form-label">${t('availability.from')} *</label>
          <input class="form-control" id="availStart" type="time" value="09:00">
        </div>
        <div class="form-group">
          <label class="form-label">${t('availability.to')} *</label>
          <input class="form-control" id="availEnd" type="time" value="18:00">
        </div>
      </div>
      ${instructors.length > 0 ? `
      <div class="form-group">
        <label class="form-label">${t('availability.instructor')}</label>
        <select class="form-control" id="availInstructor">
          <option value="">Sem preferência</option>
          ${instructors.map(i => `<option value="${i.id}">${i.name}</option>`).join('')}
        </select>
      </div>` : ''}
    `,
    footer: `
      <button class="btn btn-secondary" onclick="closeModal('availModal')">${t('btn.cancel')}</button>
      <button class="btn btn-primary" id="btnSaveAvail">${t('btn.save')}</button>
    `
  });
  openModal('availModal');

  document.getElementById('btnSaveAvail').addEventListener('click', async () => {
    const instructorId = document.getElementById('availInstructor')?.value || null;
    const body = {
      weekday: parseInt(document.getElementById('availWeekday').value),
      startTime: document.getElementById('availStart').value + ':00',
      endTime: document.getElementById('availEnd').value + ':00',
      instructorId: instructorId || null,
    };
    try {
      await api.post('/availability', body);
      showToast(t('availability.saved'), 'success');
      closeModal('availModal');
      await loadBlocks();
    } catch (e) {
      showToast(t('error.prefix') + e.message, 'error');
    }
  });
}

// ── Vacation Blocks (multi-day full closures) ─────────────────────────────────

async function loadVacationBlocks() {
  const content = document.getElementById('vacationBlocksContent');
  if (!content) return;
  try {
    const blocks = await api.get('/vacation-blocks');

    if (!blocks.length) {
      content.innerHTML = `
        <div class="card" style="padding:1.5rem">
          ${emptyState('🏖️', 'Nenhum período de férias agendado')}
          <p class="text-sm text-muted" style="text-align:center;margin-top:0.75rem">
            Agende suas férias ou feriados para bloquear todos os horários nesses dias.
          </p>
        </div>
      `;
      return;
    }

    const fmt = (d) => { const [y,m,dd] = d.split('-'); return `${dd}/${m}/${y}`; };

    content.innerHTML = `
      <div class="avail-list">
        ${blocks.map(b => `
          <div class="avail-card" style="border-left:3px solid var(--color-warning)">
            <div class="avail-card-day" style="color:var(--color-warning)">🏖️</div>
            <div class="avail-card-time">
              <span class="avail-time-range" style="font-weight:600">
                ${fmt(b.startDate)} – ${fmt(b.endDate)}
              </span>
              ${b.reason ? `<span class="avail-instructor" style="color:var(--gray-500)">${b.reason}</span>` : ''}
            </div>
            <button class="btn btn-sm btn-danger btn-del-vac" data-id="${b.id}">Remover</button>
          </div>
        `).join('')}
      </div>
    `;

    content.querySelectorAll('.btn-del-vac').forEach(btn => {
      btn.addEventListener('click', async () => {
        if (!await confirm('Remover este período de férias?')) return;
        try {
          await api.delete(`/vacation-blocks/${btn.dataset.id}`);
          showToast('Período removido.', 'success');
          await loadVacationBlocks();
        } catch (e) {
          showToast(t('error.prefix') + e.message, 'error');
        }
      });
    });
  } catch (e) {
    content.innerHTML = `<div class="empty-state"><div class="empty-state-text">${e.message}</div></div>`;
  }
}

function openVacationModal() {
  const today = new Date().toISOString().split('T')[0];

  createModal({
    id: 'vacationModal',
    title: '🏖️ Novo período de férias / feriado',
    body: `
      <p class="text-sm text-muted" style="margin:0 0 1.25rem">
        Durante este período, <strong>nenhum horário</strong> estará disponível para agendamento.
      </p>
      <div style="display:grid;grid-template-columns:1fr 1fr;gap:1rem">
        <div class="form-group">
          <label class="form-label">Data de início *</label>
          <input class="form-control" id="vacStart" type="date" value="${today}" min="${today}">
        </div>
        <div class="form-group">
          <label class="form-label">Data de término *</label>
          <input class="form-control" id="vacEnd" type="date" value="${today}" min="${today}">
        </div>
      </div>
      <div class="form-group">
        <label class="form-label">Motivo <span class="text-muted">(opcional)</span></label>
        <input class="form-control" id="vacReason" type="text" placeholder="Ex: férias, feriado nacional, reforma…" maxlength="200">
      </div>
    `,
    footer: `
      <button class="btn btn-secondary" onclick="closeModal('vacationModal')">Cancelar</button>
      <button class="btn btn-primary" id="btnSaveVacation">Salvar período</button>
    `,
  });
  openModal('vacationModal');

  document.getElementById('vacStart').addEventListener('change', (e) => {
    document.getElementById('vacEnd').min = e.target.value;
    if (document.getElementById('vacEnd').value < e.target.value)
      document.getElementById('vacEnd').value = e.target.value;
  });

  document.getElementById('btnSaveVacation').addEventListener('click', async () => {
    const startDate = document.getElementById('vacStart').value;
    const endDate   = document.getElementById('vacEnd').value;
    const reason    = document.getElementById('vacReason').value.trim() || null;

    if (!startDate || !endDate) { showToast('Preencha as datas.', 'error'); return; }
    if (startDate > endDate)    { showToast('A data de início deve ser anterior ou igual à de término.', 'error'); return; }

    const btn = document.getElementById('btnSaveVacation');
    btn.disabled = true;
    try {
      await api.post('/vacation-blocks', { startDate, endDate, reason });
      showToast('Período de férias salvo.', 'success');
      closeModal('vacationModal');
      await loadVacationBlocks();
    } catch (e) {
      showToast(t('error.prefix') + e.message, 'error');
    } finally {
      btn.disabled = false;
    }
  });
}

// ── Time Blocks (date-specific closures) ─────────────────────────────────────

async function loadTimeBlocks() {
  const content = document.getElementById('timeBlocksContent');
  if (!content) return;
  try {
    const blocks = await api.get('/time-blocks');

    if (!blocks.length) {
      content.innerHTML = `
        <div class="card" style="padding:1.5rem">
          ${emptyState('🚫', 'Nenhum bloqueio agendado')}
          <p class="text-sm text-muted" style="text-align:center;margin-top:0.75rem">
            Use bloqueios para dias ou horários em que você não poderá atender.
          </p>
        </div>
      `;
      return;
    }

    const fmt = (dateStr) => {
      const [y, m, d] = dateStr.split('-');
      return `${d}/${m}/${y}`;
    };

    const weekdayLabel = (dateStr) => {
      const [y, m, d] = dateStr.split('-');
      const day = new Date(parseInt(y), parseInt(m) - 1, parseInt(d)).getDay();
      return WEEKDAYS[day];
    };

    content.innerHTML = `
      <div class="avail-list">
        ${blocks.map(b => `
          <div class="avail-card" style="border-left:3px solid var(--color-danger)">
            <div class="avail-card-day" style="color:var(--color-danger)">
              ${weekdayLabel(b.date)}<br>
              <span style="font-size:0.75rem;font-weight:400">${fmt(b.date)}</span>
            </div>
            <div class="avail-card-time">
              <span class="avail-time-range">🚫 ${b.startTime?.substring(0,5)} – ${b.endTime?.substring(0,5)}</span>
              ${b.reason ? `<span class="avail-instructor" style="color:var(--gray-500)">${b.reason}</span>` : ''}
            </div>
            <button class="btn btn-sm btn-danger btn-del-tb" data-id="${b.id}">Remover</button>
          </div>
        `).join('')}
      </div>
    `;

    content.querySelectorAll('.btn-del-tb').forEach(btn => {
      btn.addEventListener('click', async () => {
        if (!await confirm('Remover este bloqueio?')) return;
        try {
          await api.delete(`/time-blocks/${btn.dataset.id}`);
          showToast('Bloqueio removido.', 'success');
          await loadTimeBlocks();
        } catch (e) {
          showToast(t('error.prefix') + e.message, 'error');
        }
      });
    });
  } catch (e) {
    content.innerHTML = `<div class="empty-state"><div class="empty-state-text">${e.message}</div></div>`;
  }
}

function openTimeBlockModal() {
  const today = new Date().toISOString().split('T')[0];

  createModal({
    id: 'timeBlockModal',
    title: '🚫 Novo bloqueio de horário',
    body: `
      <p class="text-sm text-muted" style="margin:0 0 1.25rem">
        Defina uma data e o intervalo de tempo em que você <strong>não</strong> estará disponível. Os clientes não conseguirão agendar nesse horário.
      </p>
      <div class="form-group">
        <label class="form-label">Data *</label>
        <input class="form-control" id="tbDate" type="date" value="${today}" min="${today}">
      </div>
      <div style="display:grid;grid-template-columns:1fr 1fr;gap:1rem">
        <div class="form-group">
          <label class="form-label">Das *</label>
          <input class="form-control" id="tbStart" type="time" value="14:00">
        </div>
        <div class="form-group">
          <label class="form-label">Até *</label>
          <input class="form-control" id="tbEnd" type="time" value="16:00">
        </div>
      </div>
      <div class="form-group">
        <label class="form-label">Motivo <span class="text-muted">(opcional)</span></label>
        <input class="form-control" id="tbReason" type="text" placeholder="Ex: consulta médica, almoço estendido…" maxlength="200">
      </div>
    `,
    footer: `
      <button class="btn btn-secondary" onclick="closeModal('timeBlockModal')">Cancelar</button>
      <button class="btn btn-primary" id="btnSaveTimeBlock">Salvar bloqueio</button>
    `,
  });
  openModal('timeBlockModal');

  document.getElementById('btnSaveTimeBlock').addEventListener('click', async () => {
    const date    = document.getElementById('tbDate').value;
    const start   = document.getElementById('tbStart').value;
    const end     = document.getElementById('tbEnd').value;
    const reason  = document.getElementById('tbReason').value.trim() || null;

    if (!date || !start || !end) {
      showToast('Preencha data e horários.', 'error');
      return;
    }
    if (start >= end) {
      showToast('O horário de início deve ser anterior ao de término.', 'error');
      return;
    }

    const btn = document.getElementById('btnSaveTimeBlock');
    btn.disabled = true;
    try {
      await api.post('/time-blocks', {
        date,
        startTime: start + ':00',
        endTime:   end   + ':00',
        reason,
      });
      showToast('Bloqueio salvo.', 'success');
      closeModal('timeBlockModal');
      await loadTimeBlocks();
    } catch (e) {
      showToast(t('error.prefix') + e.message, 'error');
    } finally {
      btn.disabled = false;
    }
  });
}
