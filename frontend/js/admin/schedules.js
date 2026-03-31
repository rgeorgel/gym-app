import { api } from '../api.js?v=202603311200';
import { showToast, createModal, openModal, closeModal, formatTime, emptyState, confirm, getWeekdaysFull } from '../ui.js?v=202603311200';
import { t } from '../i18n.js?v=202603311200';
import { loadLocationsForSelector } from './locations.js?v=202603311200';

export async function renderSchedules(container) {
  container.innerHTML = `
    <div style="display:flex;justify-content:flex-end;margin-bottom:1rem">
      <button class="btn btn-primary" id="btnNewSchedule">${t('schedules.new')}</button>
    </div>
    <div id="schedWeekGrid" class="week-grid">
      <div class="loading-center" style="grid-column:1/-1"><span class="spinner"></span></div>
    </div>
  `;

  document.getElementById('btnNewSchedule').addEventListener('click', () => openScheduleModal());
  await loadSchedules();
}

let classTypes = [], instructors = [], schedules = [], locations = [];

async function loadSchedules() {
  try {
    [schedules, classTypes, instructors, locations] = await Promise.all([
      api.get('/schedules'),
      api.get('/class-types'),
      api.get('/instructors'),
      loadLocationsForSelector(),
    ]);
    renderWeekGrid();
  } catch (e) {
    showToast('Erro: ' + e.message, 'error');
  }
}

function renderWeekGrid() {
  const grid = document.getElementById('schedWeekGrid');
  grid.innerHTML = getWeekdaysFull().map((day, idx) => {
    const daySched = schedules.filter(s => s.weekday === idx);
    return `
      <div class="day-column">
        <div class="day-header">${day}</div>
        ${daySched.length === 0
          ? `<div class="empty-state" style="padding:1rem"><div class="empty-state-text text-xs">${t('schedules.noClasses')}</div></div>`
          : daySched.map(s => {
              const loc = locations.find(l => l.id === s.locationId);
              return `
              <div class="class-card" onclick="window._editSchedule('${s.id}')" style="border-left-color:${s.classTypeColor}">
                <div class="class-card-time">${formatTime(s.startTime?.toString())}</div>
                <div class="class-card-name">${s.classTypeName}</div>
                <div class="class-card-slots text-xs text-muted">👥 ${s.capacity} ${t('schedules.slots')} · ${s.durationMinutes}min</div>
                ${s.instructorName ? `<div class="text-xs text-muted">👤 ${s.instructorName}</div>` : ''}
                ${loc ? `<div class="text-xs text-muted">📍 ${loc.name}</div>` : ''}
              </div>
            `}).join('')}
      </div>
    `;
  }).join('');

  window._editSchedule = (id) => openScheduleModal(schedules.find(s => s.id === id));
}

function openScheduleModal(sched = null) {
  const locationOptions = locations.map(l => 
    `<option value="${l.id}" ${sched?.locationId === l.id ? 'selected' : ''}>${l.name}</option>`
  ).join('');

  createModal({
    id: 'schedModal',
    title: sched ? t('schedules.title.edit') : t('schedules.title.new'),
    body: `
      <div class="form-group">
        <label class="form-label">Local *</label>
        <select class="form-control" id="schedLocation" required>
          <option value="">Selecione o local</option>
          ${locationOptions}
        </select>
      </div>
      <div class="form-group">
        <label class="form-label">${t('nav.classTypes')} *</label>
        <select class="form-control" id="schedCt" required>
          <option value="">${t('field.select')}</option>
          ${classTypes.filter(ct=>ct.isActive).map(ct => `<option value="${ct.id}" ${sched?.classTypeId===ct.id?'selected':''}>${ct.name}</option>`).join('')}
        </select>
      </div>
      <div style="display:grid;grid-template-columns:1fr 1fr;gap:1rem">
        <div class="form-group">
          <label class="form-label">${t('schedules.field.weekday')} *</label>
          <select class="form-control" id="schedDay">
            ${getWeekdaysFull().map((d,i) => `<option value="${i}" ${sched?.weekday===i?'selected':''}>${d}</option>`).join('')}
          </select>
        </div>
        <div class="form-group">
          <label class="form-label">${t('schedules.field.time')} *</label>
          <input class="form-control" id="schedTime" type="time" value="${sched?.startTime?.slice(0,5) ?? '07:00'}" required>
        </div>
      </div>
      <div style="display:grid;grid-template-columns:1fr 1fr;gap:1rem">
        <div class="form-group">
          <label class="form-label">${t('schedules.field.duration')}</label>
          <input class="form-control" id="schedDuration" type="number" min="15" value="${sched?.durationMinutes ?? 60}">
        </div>
        <div class="form-group">
          <label class="form-label">${t('field.capacity')} *</label>
          <input class="form-control" id="schedCap" type="number" min="1" value="${sched?.capacity ?? 20}" required>
        </div>
      </div>
      <div class="form-group">
        <label class="form-label">${t('field.instructor')}</label>
        <select class="form-control" id="schedInstructor">
          <option value="">${t('field.noInstructor')}</option>
          ${instructors.map(i => `<option value="${i.id}" ${sched?.instructorId===i.id?'selected':''}>${i.name}</option>`).join('')}
        </select>
      </div>
      ${sched ? `
      <div class="form-group">
        <label class="form-label">${t('field.status')}</label>
        <select class="form-control" id="schedActive">
          <option value="true" ${sched.isActive?'selected':''}>${t('status.active')}</option>
          <option value="false" ${!sched.isActive?'selected':''}>${t('status.inactive')}</option>
        </select>
      </div>` : ''}
    `,
    footer: `
      ${sched ? `<button class="btn btn-danger" id="btnDeleteSched" style="margin-right:auto">${t('schedules.btn.delete')}</button>` : ''}
      <button class="btn btn-secondary" onclick="closeModal('schedModal')">${t('btn.cancel')}</button>
      <button class="btn btn-primary" id="btnSaveSched">${t('btn.save')}</button>
    `
  });
  openModal('schedModal');

  if (sched) {
    document.getElementById('btnDeleteSched').addEventListener('click', async () => {
      if (!await confirm(t('schedules.delete.confirm'))) return;
      try {
        await api.delete(`/schedules/${sched.id}`);
        showToast(t('schedules.deleted'), 'success');
        closeModal('schedModal');
        await loadSchedules();
      } catch (e) {
        showToast(t('error.prefix') + e.message, 'error');
      }
    });
  }

  document.getElementById('btnSaveSched').addEventListener('click', async () => {
    const locationId = document.getElementById('schedLocation').value;
    if (!locationId) {
      showToast('Local é obrigatório', 'error');
      return;
    }

    const body = {
      classTypeId: document.getElementById('schedCt').value,
      instructorId: document.getElementById('schedInstructor').value || null,
      locationId,
      weekday: parseInt(document.getElementById('schedDay').value),
      startTime: document.getElementById('schedTime').value + ':00',
      durationMinutes: parseInt(document.getElementById('schedDuration').value),
      capacity: parseInt(document.getElementById('schedCap').value),
    };
    if (sched) body.isActive = document.getElementById('schedActive').value === 'true';

    try {
      if (sched) await api.put(`/schedules/${sched.id}`, body);
      else await api.post('/schedules', body);
      showToast(t('schedules.saved'), 'success');
      closeModal('schedModal');
      await loadSchedules();
    } catch (e) {
      showToast(t('error.prefix') + e.message, 'error');
    }
  });
}
