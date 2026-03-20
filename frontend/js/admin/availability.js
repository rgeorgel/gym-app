import { api } from '../api.js';
import { showToast, createModal, openModal, closeModal, emptyState, confirm } from '../ui.js';
import { t, getWeekdays } from '../i18n.js';

const WEEKDAYS = ['Dom', 'Seg', 'Ter', 'Qua', 'Qui', 'Sex', 'Sáb'];

export async function renderAvailability(container) {
  container.innerHTML = `
    <div style="display:flex;justify-content:flex-end;margin-bottom:1rem">
      <button class="btn btn-primary" id="btnNewBlock">${t('availability.add')}</button>
    </div>
    <div id="availabilityContent"><div class="loading-center"><span class="spinner"></span></div></div>
  `;

  document.getElementById('btnNewBlock').addEventListener('click', () => openBlockModal());
  await loadBlocks();
}

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

    // Group by weekday
    const byDay = {};
    blocks.forEach(b => {
      if (!byDay[b.weekday]) byDay[b.weekday] = [];
      byDay[b.weekday].push(b);
    });

    content.innerHTML = `<div class="card"><div class="table-wrapper">
      <table class="data-table">
        <thead><tr>
          <th>${t('availability.weekday')}</th>
          <th>${t('availability.from')}</th>
          <th>${t('availability.to')}</th>
          <th>${t('availability.instructor')}</th>
          <th></th>
        </tr></thead>
        <tbody>
          ${blocks.map(b => `
            <tr>
              <td class="font-medium">${weekdays[b.weekday] ?? b.weekday}</td>
              <td>${b.startTime?.substring(0,5)}</td>
              <td>${b.endTime?.substring(0,5)}</td>
              <td>${b.instructorName ?? '—'}</td>
              <td>
                <button class="btn btn-sm btn-danger btn-del" data-id="${b.id}">${t('btn.delete')}</button>
              </td>
            </tr>
          `).join('')}
        </tbody>
      </table>
    </div></div>`;

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

    // Store instructors globally for modal
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
          <option value="">${t('field.noInstructor')}</option>
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
