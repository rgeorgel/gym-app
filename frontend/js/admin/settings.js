import { api } from '../api.js';
import { showToast } from '../ui.js';

export async function renderSettings(container) {
  container.innerHTML = `<div class="loading-center"><span class="spinner"></span></div>`;

  let settings, templates;
  try {
    [settings, templates] = await Promise.all([
      api.get('/settings'),
      api.get('/package-templates'),
    ]);
  } catch (e) {
    container.innerHTML = `<div class="empty-state"><div class="empty-state-text">Erro: ${e.message}</div></div>`;
    return;
  }

  const options = templates.map(t => {
    const selected = t.id === settings.defaultPackageTemplateId ? 'selected' : '';
    const duration = t.durationDays ? ` · ${t.durationDays} dias` : ' · sem validade';
    const credits = t.items.map(i => `${i.totalCredits}× ${i.classTypeName}`).join(', ');
    return `<option value="${t.id}" ${selected}>${t.name}${duration} — ${credits}</option>`;
  }).join('');

  container.innerHTML = `
    <div class="card" style="max-width:600px">
      <div class="card-body" style="padding:1.5rem">
        <h3 style="margin:0 0 0.25rem">Pacote padrão para novos alunos</h3>
        <p class="text-muted text-sm" style="margin:0 0 1.25rem">
          Se configurado, este pacote será automaticamente atribuído a todo aluno que se cadastrar
          (auto-registro ou criado pelo admin). Ideal para "primeira aula grátis".
        </p>

        <div class="form-group">
          <label class="form-label">Modelo de pacote</label>
          <select class="form-control" id="selectDefaultTemplate">
            <option value="">— Nenhum (não atribuir automaticamente) —</option>
            ${options}
          </select>
        </div>

        <div style="display:flex;align-items:center;gap:0.75rem;margin-top:1rem">
          <button class="btn btn-primary" id="btnSaveSettings">Salvar</button>
          <span id="settingsSavedMsg" class="text-sm text-muted" style="display:none">Salvo!</span>
        </div>
      </div>
    </div>
  `;

  document.getElementById('btnSaveSettings').addEventListener('click', async () => {
    const val = document.getElementById('selectDefaultTemplate').value;
    const btn = document.getElementById('btnSaveSettings');
    btn.disabled = true;
    try {
      await api.put('/settings/default-package-template', {
        templateId: val || null,
      });
      showToast('Configuração salva', 'success');
    } catch (e) {
      showToast('Erro: ' + e.message, 'error');
    } finally {
      btn.disabled = false;
    }
  });
}
