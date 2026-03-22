import { api, getLocationId, setLocationId } from '../api.js';
import { showToast, createModal, openModal, closeModal, emptyState } from '../ui.js';
import { t } from '../i18n.js';

let locations = [];

export async function renderLocations(container) {
  container.innerHTML = `
    <div style="display:flex;justify-content:flex-end;gap:0.5rem;margin-bottom:1rem">
      <button class="btn btn-primary" id="btnNewLocation">+ Novo Local</button>
    </div>
    <div class="card" id="locationsCard">
      <div class="loading-center"><span class="spinner"></span></div>
    </div>
  `;

  document.getElementById('btnNewLocation').addEventListener('click', () => openLocationModal());
  await loadLocations();
}

async function loadLocations() {
  const card = document.getElementById('locationsCard');
  try {
    locations = await api.get('/locations');
    if (!locations.length) {
      card.innerHTML = emptyState('📍', 'Nenhum local cadastrado');
      return;
    }
    card.innerHTML = `
      <div class="table-wrapper">
        <table>
          <thead><tr>
            <th>Nome</th>
            <th>Endereço</th>
            <th>Telefone</th>
            <th>Tipo</th>
            <th></th>
          </tr></thead>
          <tbody>
            ${locations.map(loc => `
              <tr>
                <td class="font-medium">${loc.name}</td>
                <td style="color:var(--text-muted)">${loc.address ?? '—'}</td>
                <td>${loc.phone ?? '—'}</td>
                <td>
                  ${loc.isMain 
                    ? '<span class="badge badge-success">Matriz</span>' 
                    : '<span class="badge badge-gray">Filial</span>'}
                </td>
                <td>
                  <button class="btn btn-secondary btn-sm" onclick="window._editLocation('${loc.id}')">Editar</button>
                  ${locations.length > 1 ? `
                    <button class="btn btn-danger btn-sm" style="margin-left:0.25rem" onclick="window._deleteLocation('${loc.id}')">Excluir</button>
                  ` : ''}
                </td>
              </tr>
            `).join('')}
          </tbody>
        </table>
      </div>
    `;
    window._editLocation = (id) => openLocationModal(locations.find(l => l.id === id));
    window._deleteLocation = (id) => deleteLocation(id);
  } catch (e) {
    showToast('Erro: ' + e.message, 'error');
  }
}

async function openLocationModal(loc = null) {
  createModal({
    id: 'locationModal',
    title: loc ? 'Editar Local' : 'Novo Local',
    body: `
      <div class="form-group">
        <label class="form-label">Nome *</label>
        <input class="form-control" id="locName" required value="${loc?.name ?? ''}" placeholder="ex: Matriz, Filial Centro">
      </div>
      <div class="form-group">
        <label class="form-label">Endereço</label>
        <input class="form-control" id="locAddress" value="${loc?.address ?? ''}" placeholder="ex: Av. Brasil, 123 - Centro">
      </div>
      <div class="form-group">
        <label class="form-label">Telefone</label>
        <input class="form-control" id="locPhone" value="${loc?.phone ?? ''}" placeholder="ex: (11) 99999-9999">
      </div>
      <div class="form-group">
        <label class="form-label">Tipo</label>
        <select class="form-control" id="locIsMain">
          <option value="false" ${!loc?.isMain ? 'selected' : ''}>Filial</option>
          <option value="true" ${loc?.isMain ? 'selected' : ''}>Matriz (principal)</option>
        </select>
      </div>
      ${loc?.isMain ? '<p style="font-size:0.85rem;color:var(--text-muted);margin-top:0.5rem"><em>Para alterar a matriz, selecione outro local como "Matriz" primeiro.</em></p>' : ''}
    `,
    footer: `
      <button class="btn btn-secondary" onclick="closeModal('locationModal')">Cancelar</button>
      <button class="btn btn-primary" id="btnSaveLoc">Salvar</button>
    `
  });
  openModal('locationModal');

  document.getElementById('btnSaveLoc').addEventListener('click', async () => {
    const name = document.getElementById('locName').value.trim();
    if (!name) {
      showToast('Nome é obrigatório', 'error');
      return;
    }

    const body = {
      name,
      address: document.getElementById('locAddress').value.trim() || null,
      phone: document.getElementById('locPhone').value.trim() || null,
      isMain: document.getElementById('locIsMain').value === 'true'
    };

    try {
      if (loc) {
        await api.put(`/locations/${loc.id}`, body);
        showToast('Local atualizado', 'success');
      } else {
        await api.post('/locations', body);
        showToast('Local criado', 'success');
      }
      closeModal('locationModal');
      await loadLocations();
    } catch (e) {
      showToast('Erro: ' + e.message, 'error');
    }
  });
}

async function deleteLocation(id) {
  const loc = locations.find(l => l.id === id);
  if (!confirm(`Excluir o local "${loc.name}"? Esta ação não pode ser desfeita.`)) return;

  try {
    await api.delete(`/locations/${id}`);
    showToast('Local excluído', 'success');
    await loadLocations();
  } catch (e) {
    showToast('Erro: ' + e.message, 'error');
  }
}

export function renderLocationSelector(container, currentLocationId, onChange) {
  if (locations.length <= 1) {
    container.innerHTML = '';
    return;
  }

  container.innerHTML = `
    <select class="form-control" id="locationSelector" style="min-width:180px">
      <option value="">Todos os Locais</option>
      ${locations.map(l => `
        <option value="${l.id}" ${currentLocationId === l.id ? 'selected' : ''}>${l.name}</option>
      `).join('')}
    </select>
  `;

  document.getElementById('locationSelector').addEventListener('change', async (e) => {
    const newLocationId = e.target.value || null;
    setLocationId(newLocationId);
    onChange(newLocationId);
  });
}

export async function loadLocationsForSelector() {
  try {
    locations = await api.get('/locations');
    return locations;
  } catch {
    return [];
  }
}

export function getLocations() {
  return locations;
}
