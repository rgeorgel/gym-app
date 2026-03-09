import { api } from '../api.js';
import { showToast, createModal, openModal, closeModal, WEEKDAYS_FULL, formatTime, emptyState, confirm } from '../ui.js';

export async function renderSchedules(container) {
  container.innerHTML = `
    <div style="display:flex;justify-content:flex-end;margin-bottom:1rem">
      <button class="btn btn-primary" id="btnNewSchedule">+ Novo Horário</button>
    </div>
    <div id="schedWeekGrid" class="week-grid">
      <div class="loading-center" style="grid-column:1/-1"><span class="spinner"></span></div>
    </div>
  `;

  document.getElementById('btnNewSchedule').addEventListener('click', () => openScheduleModal());
  await loadSchedules();
}

let classTypes = [], instructors = [], schedules = [];

async function loadSchedules() {
  try {
    [schedules, classTypes, instructors] = await Promise.all([
      api.get('/schedules'),
      api.get('/class-types'),
      api.get('/instructors'),
    ]);
    renderWeekGrid();
  } catch (e) {
    showToast('Erro: ' + e.message, 'error');
  }
}

function renderWeekGrid() {
  const grid = document.getElementById('schedWeekGrid');
  grid.innerHTML = WEEKDAYS_FULL.map((day, idx) => {
    const daySched = schedules.filter(s => s.weekday === idx);
    return `
      <div class="day-column">
        <div class="day-header">${day}</div>
        ${daySched.length === 0
          ? `<div class="empty-state" style="padding:1rem"><div class="empty-state-text text-xs">Sem aulas</div></div>`
          : daySched.map(s => `
            <div class="class-card" onclick="window._editSchedule('${s.id}')" style="border-left-color:${s.classTypeColor}">
              <div class="class-card-time">${formatTime(s.startTime?.toString())}</div>
              <div class="class-card-name">${s.classTypeName}</div>
              <div class="class-card-slots text-xs text-muted">👥 ${s.capacity} vagas · ${s.durationMinutes}min</div>
              ${s.instructorName ? `<div class="text-xs text-muted">👤 ${s.instructorName}</div>` : ''}
            </div>
          `).join('')}
      </div>
    `;
  }).join('');

  window._editSchedule = (id) => openScheduleModal(schedules.find(s => s.id === id));
}

function openScheduleModal(sched = null) {
  createModal({
    id: 'schedModal',
    title: sched ? 'Editar Horário' : 'Novo Horário',
    body: `
      <div class="form-group">
        <label class="form-label">Modalidade *</label>
        <select class="form-control" id="schedCt" required>
          <option value="">Selecione...</option>
          ${classTypes.filter(ct=>ct.isActive).map(ct => `<option value="${ct.id}" ${sched?.classTypeId===ct.id?'selected':''}>${ct.name}</option>`).join('')}
        </select>
      </div>
      <div style="display:grid;grid-template-columns:1fr 1fr;gap:1rem">
        <div class="form-group">
          <label class="form-label">Dia da semana *</label>
          <select class="form-control" id="schedDay">
            ${WEEKDAYS_FULL.map((d,i) => `<option value="${i}" ${sched?.weekday===i?'selected':''}>${d}</option>`).join('')}
          </select>
        </div>
        <div class="form-group">
          <label class="form-label">Horário *</label>
          <input class="form-control" id="schedTime" type="time" value="${sched?.startTime?.slice(0,5) ?? '07:00'}" required>
        </div>
      </div>
      <div style="display:grid;grid-template-columns:1fr 1fr;gap:1rem">
        <div class="form-group">
          <label class="form-label">Duração (min)</label>
          <input class="form-control" id="schedDuration" type="number" min="15" value="${sched?.durationMinutes ?? 60}">
        </div>
        <div class="form-group">
          <label class="form-label">Capacidade *</label>
          <input class="form-control" id="schedCap" type="number" min="1" value="${sched?.capacity ?? 20}" required>
        </div>
      </div>
      <div class="form-group">
        <label class="form-label">Instrutor</label>
        <select class="form-control" id="schedInstructor">
          <option value="">Sem instrutor</option>
          ${instructors.map(i => `<option value="${i.id}" ${sched?.instructorId===i.id?'selected':''}>${i.name}</option>`).join('')}
        </select>
      </div>
      ${sched ? `
      <div class="form-group">
        <label class="form-label">Status</label>
        <select class="form-control" id="schedActive">
          <option value="true" ${sched.isActive?'selected':''}>Ativo</option>
          <option value="false" ${!sched.isActive?'selected':''}>Inativo</option>
        </select>
      </div>` : ''}
    `,
    footer: `
      ${sched ? `<button class="btn btn-danger" id="btnDeleteSched" style="margin-right:auto">Excluir</button>` : ''}
      <button class="btn btn-secondary" onclick="closeModal('schedModal')">Cancelar</button>
      <button class="btn btn-primary" id="btnSaveSched">Salvar</button>
    `
  });
  openModal('schedModal');

  if (sched) {
    document.getElementById('btnDeleteSched').addEventListener('click', async () => {
      if (!await confirm('Excluir este horário da grade? As aulas futuras geradas por ele não serão afetadas.')) return;
      try {
        await api.delete(`/schedules/${sched.id}`);
        showToast('Horário excluído', 'success');
        closeModal('schedModal');
        await loadSchedules();
      } catch (e) {
        showToast('Erro: ' + e.message, 'error');
      }
    });
  }

  document.getElementById('btnSaveSched').addEventListener('click', async () => {
    const body = {
      classTypeId: document.getElementById('schedCt').value,
      instructorId: document.getElementById('schedInstructor').value || null,
      weekday: parseInt(document.getElementById('schedDay').value),
      startTime: document.getElementById('schedTime').value + ':00',
      durationMinutes: parseInt(document.getElementById('schedDuration').value),
      capacity: parseInt(document.getElementById('schedCap').value),
    };
    if (sched) body.isActive = document.getElementById('schedActive').value === 'true';

    try {
      if (sched) await api.put(`/schedules/${sched.id}`, body);
      else await api.post('/schedules', body);
      showToast('Horário salvo', 'success');
      closeModal('schedModal');
      await loadSchedules();
    } catch (e) {
      showToast('Erro: ' + e.message, 'error');
    }
  });
}
