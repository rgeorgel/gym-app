import { api } from '../api.js';
import { showToast, createModal, openModal, closeModal, formatDate, statusBadge, emptyState, confirm } from '../ui.js';
import { t } from '../i18n.js';
import { tenantType } from '../tenant.js';
import { openCheckoutModal } from './financial.js';

export async function renderStudentDetail(container, studentId, onBack) {
  container.innerHTML = '<div class="loading-center"><span class="spinner"></span></div>';

  const isGym = tenantType !== 'BeautySalon';

  try {
    const [student, bookings, packages, classTypes, templates] = await Promise.all([
      api.get(`/students/${studentId}`),
      api.get(`/students/${studentId}/bookings`),
      isGym ? api.get(`/students/${studentId}/packages`) : Promise.resolve([]),
      isGym ? api.get('/class-types') : Promise.resolve([]),
      isGym ? api.get('/package-templates').catch(() => []) : Promise.resolve([]),
    ]);

    render(container, student, bookings, packages, classTypes, templates, isGym, onBack);
  } catch (e) {
    showToast('Erro ao carregar dados: ' + e.message, 'error');
    onBack();
  }
}

function render(container, student, bookings, packages, classTypes, templates, isGym, onBack) {
  const confirmedOrCheckedIn = bookings.filter(b => b.status === 'Confirmed' || b.status === 'CheckedIn');
  const cancelled = bookings.filter(b => b.status === 'Cancelled');

  const activePackages = packages.filter(p => p.isActive);
  const totalCredits = activePackages.reduce((sum, p) =>
    sum + p.items.reduce((s, i) => s + i.remainingCredits, 0), 0);

  const birthDate = student.birthDate
    ? (() => {
        const [y, m, d] = student.birthDate.split('-');
        const age = new Date().getFullYear() - parseInt(y);
        return `${d}/${m}/${y} (${age} anos)`;
      })()
    : '—';

  container.innerHTML = `
    <div style="max-width:900px">

      <!-- Header -->
      <div style="margin-bottom:1.5rem">
        <div style="display:flex;align-items:center;gap:0.5rem;flex-wrap:wrap;margin-bottom:0.5rem">
          <button class="btn btn-secondary btn-sm" id="btnBack">← Voltar</button>
          <div style="margin-left:auto;display:flex;align-items:center;gap:0.5rem;flex-wrap:wrap">
            ${statusBadge(student.status)}
            <button class="btn btn-secondary btn-sm" id="btnEditStudent">Editar</button>
            <button class="btn btn-secondary btn-sm" id="btnResetLink">🔑 Link de acesso</button>
          </div>
        </div>
        <h2 style="margin:0;word-break:break-word">${student.name}</h2>
      </div>

      <!-- Info + Stats grid -->
      <div style="display:grid;grid-template-columns:repeat(auto-fit,minmax(min(100%,320px),1fr));gap:1.25rem;margin-bottom:1.25rem">

        <!-- Info card -->
        <div class="card">
          <div class="card-body" style="padding:1.25rem">
            <h3 style="margin:0 0 1rem;font-size:1rem">Dados do cliente</h3>
            <div style="display:flex;flex-direction:column;gap:0.6rem">
              ${infoRow('✉️', student.email)}
              ${student.phone ? infoRow('📱', student.phone) : ''}
              ${infoRow('🎂', birthDate)}
              ${infoRow('📅', 'Cadastro: ' + formatDate(student.createdAt))}
            </div>
          </div>
        </div>

        <!-- Stats card -->
        <div class="card">
          <div class="card-body" style="padding:1.25rem">
            <h3 style="margin:0 0 1rem;font-size:1rem">Resumo</h3>
            <div style="display:grid;grid-template-columns:1fr 1fr;gap:0.75rem">
              ${statBox(confirmedOrCheckedIn.length, 'Agendamentos')}
              ${statBox(cancelled.length, 'Cancelamentos')}
              ${isGym ? statBox(totalCredits, 'Créditos disponíveis') : ''}
              ${statBox(
                student.lastBookingDate ? formatDate(student.lastBookingDate) : 'Nunca',
                'Último agendamento'
              )}
            </div>
          </div>
        </div>

      </div>

      <!-- Notes section -->
      <div class="card" style="margin-bottom:1.25rem" id="notesCard">
        <div class="card-body" style="padding:1.25rem">
          <div style="display:flex;align-items:center;justify-content:space-between;margin-bottom:0.75rem">
            <h3 style="margin:0;font-size:1rem">📝 Observações</h3>
            <button class="btn btn-secondary btn-sm" id="btnEditNotes">Editar</button>
          </div>
          <div id="notesDisplay">
            ${student.healthNotes
              ? `<div style="font-size:var(--font-size-sm);color:var(--gray-700);white-space:pre-wrap;line-height:1.5">${student.healthNotes}</div>`
              : `<div style="font-size:var(--font-size-sm);color:var(--gray-400);font-style:italic">Nenhuma observação. Ex: alérgica a amônia, prefere franja curta…</div>`
            }
          </div>
          <div id="notesEdit" style="display:none">
            <textarea class="form-control" id="notesTextarea" rows="4" placeholder="Ex: alérgica a amônia, prefere franja curta…" style="resize:vertical">${student.healthNotes ?? ''}</textarea>
            <div style="display:flex;gap:0.5rem;margin-top:0.625rem;justify-content:flex-end">
              <button class="btn btn-secondary btn-sm" id="btnCancelNotes">Cancelar</button>
              <button class="btn btn-primary btn-sm" id="btnSaveNotes">Salvar</button>
            </div>
          </div>
        </div>
      </div>

      <!-- Packages section (gym only) -->
      ${isGym ? `
      <div class="card" style="margin-bottom:1.25rem">
        <div class="card-body" style="padding:1.25rem">
          <div style="display:flex;align-items:center;justify-content:space-between;margin-bottom:1rem">
            <h3 style="margin:0;font-size:1rem">Pacotes e créditos</h3>
            <button class="btn btn-primary btn-sm" id="btnAddPackage">+ Novo pacote</button>
          </div>
          <div id="packagesArea">
            ${renderPackagesHtml(packages)}
          </div>
        </div>
      </div>` : ''}

      <!-- Bookings history -->
      <div class="card">
        <div class="card-body" style="padding:1.25rem">
          <h3 style="margin:0 0 1rem;font-size:1rem">Histórico de agendamentos</h3>
          ${renderBookingsHtml(bookings, isGym)}
        </div>
      </div>

      <!-- Payment history -->
      <div class="card" style="margin-top:1.25rem" id="paymentHistoryCard">
        <div class="card-body" style="padding:1.25rem">
          <div style="display:flex;align-items:center;justify-content:space-between;margin-bottom:1rem">
            <h3 style="margin:0;font-size:1rem">💰 Histórico financeiro</h3>
            <button class="btn btn-primary btn-sm" id="btnAddPayment">+ Registrar pagamento</button>
          </div>
          <div id="paymentHistoryArea"><div class="loading-center"><span class="spinner"></span></div></div>
        </div>
      </div>

    </div>
  `;

  document.getElementById('btnBack').addEventListener('click', onBack);

  // Payment history
  const FMT    = (v) => 'R$ ' + Number(v).toFixed(2).replace('.', ',');
  const FMT_D  = (d) => { const [y,m,dd]=d.split('-'); return `${dd}/${m}/${y}`; };

  const loadPaymentHistory = () => {
    const area = container.querySelector('#paymentHistoryArea');
    if (!area) return;
    area.innerHTML = '<div class="loading-center"><span class="spinner"></span></div>';
    api.get(`/financial/transactions?studentId=${student.id}`).then(txs => {
      if (!txs.length) {
        area.innerHTML = `<p style="color:var(--gray-400);font-size:var(--font-size-sm);font-style:italic">Nenhum pagamento registrado.</p>`;
        return;
      }
      area.innerHTML = `
        <div style="display:flex;flex-direction:column;gap:0.5rem">
          ${txs.map(tx => `
            <div style="display:flex;align-items:center;justify-content:space-between;padding:0.5rem 0;border-bottom:1px solid var(--gray-100);font-size:var(--font-size-sm)">
              <div>
                <div style="font-weight:500">${tx.serviceName}</div>
                <div style="color:var(--gray-500)">${FMT_D(tx.date)} · ${tx.paymentMethod}${tx.installments > 1 ? ` ${tx.installments}x` : ''}</div>
              </div>
              <div style="text-align:right">
                <div style="font-weight:700">${FMT(tx.grossAmount)}</div>
                ${tx.cardFeeAmount > 0 ? `<div style="color:var(--gray-400)">${FMT(tx.netAmount)} líquido</div>` : ''}
              </div>
            </div>
          `).join('')}
        </div>
      `;
    }).catch(() => {});
  };

  loadPaymentHistory();

  container.querySelector('#btnAddPayment').addEventListener('click', () => {
    openCheckoutModal(
      { studentId: student.id, studentName: student.name },
      loadPaymentHistory
    );
  });

  // Notes inline edit
  const notesDisplay = document.getElementById('notesDisplay');
  const notesEdit    = document.getElementById('notesEdit');
  document.getElementById('btnEditNotes').addEventListener('click', () => {
    notesDisplay.style.display = 'none';
    notesEdit.style.display = 'block';
    document.getElementById('notesTextarea').focus();
  });
  document.getElementById('btnCancelNotes').addEventListener('click', () => {
    document.getElementById('notesTextarea').value = student.healthNotes ?? '';
    notesEdit.style.display = 'none';
    notesDisplay.style.display = 'block';
  });
  document.getElementById('btnSaveNotes').addEventListener('click', async () => {
    const notes = document.getElementById('notesTextarea').value.trim() || null;
    const btn = document.getElementById('btnSaveNotes');
    btn.disabled = true;
    try {
      await api.patch(`/students/${student.id}/notes`, { notes });
      student.healthNotes = notes;
      notesDisplay.innerHTML = notes
        ? `<div style="font-size:var(--font-size-sm);color:var(--gray-700);white-space:pre-wrap;line-height:1.5">${notes}</div>`
        : `<div style="font-size:var(--font-size-sm);color:var(--gray-400);font-style:italic">Nenhuma observação. Ex: alérgica a amônia, prefere franja curta…</div>`;
      notesEdit.style.display = 'none';
      notesDisplay.style.display = 'block';
      showToast('Observações salvas.', 'success');
    } catch (e) {
      showToast('Erro ao salvar: ' + e.message, 'error');
    } finally {
      btn.disabled = false;
    }
  });

  document.getElementById('btnEditStudent').addEventListener('click', () =>
    openEditModal(student, async (updated) => {
      Object.assign(student, updated);
      render(container, student, bookings, packages, classTypes, templates, isGym, onBack);
    })
  );

  document.getElementById('btnResetLink')?.addEventListener('click', async () => {
    try {
      const data = await api.post(`/students/${student.id}/reset-link`, {});
      const url = `${location.origin}/reset-password.html?token=${data.token}`;
      await navigator.clipboard.writeText(url);
      showToast('Link copiado para a área de transferência!', 'success');
    } catch (e) {
      showToast('Erro ao gerar link: ' + e.message, 'error');
    }
  });

  if (isGym) {
    document.getElementById('btnAddPackage').addEventListener('click', () =>
      openNewPackageModal(student.id, classTypes, templates, async () => {
        const [updatedPkgs, updatedStudent] = await Promise.all([
          api.get(`/students/${student.id}/packages`),
          api.get(`/students/${student.id}`),
        ]);
        render(container, updatedStudent, bookings, updatedPkgs, classTypes, templates, isGym, onBack);
      })
    );

    container.querySelectorAll('[data-delete-pkg]').forEach(btn => {
      btn.addEventListener('click', async () => {
        if (!await confirm('Excluir este pacote?')) return;
        try {
          await api.delete(`/packages/${btn.dataset.deletePkg}`);
          showToast('Pacote excluído.', 'success');
          const [updatedPkgs, updatedStudent] = await Promise.all([
            api.get(`/students/${student.id}/packages`),
            api.get(`/students/${student.id}`),
          ]);
          render(container, updatedStudent, bookings, updatedPkgs, classTypes, templates, isGym, onBack);
        } catch (e) {
          showToast('Erro: ' + e.message, 'error');
        }
      });
    });
  }
}

// ── helpers ──────────────────────────────────────────────────────────────────

function infoRow(icon, text) {
  return `<div style="display:flex;gap:0.5rem;align-items:baseline;font-size:var(--font-size-sm)">
    <span style="min-width:1.25rem">${icon}</span>
    <span style="color:var(--gray-700)">${text}</span>
  </div>`;
}

function statBox(value, label) {
  return `<div style="background:var(--gray-50);border:1px solid var(--gray-200);border-radius:var(--border-radius);padding:0.75rem;text-align:center">
    <div style="font-size:1.25rem;font-weight:700;color:var(--primary)">${value}</div>
    <div style="font-size:var(--font-size-xs);color:var(--gray-500);margin-top:0.125rem">${label}</div>
  </div>`;
}

function renderPackagesHtml(packages) {
  const active = packages.filter(p => p.isActive);
  const inactive = packages.filter(p => !p.isActive);

  if (!active.length && !inactive.length) {
    return emptyState('📦', 'Nenhum pacote cadastrado');
  }

  const renderPkg = (p, allowDelete) => `
    <div style="border:1px solid var(--gray-200);border-radius:var(--border-radius);padding:0.875rem;margin-bottom:0.75rem;${!p.isActive ? 'opacity:0.55' : ''}">
      <div style="display:flex;align-items:center;justify-content:space-between;margin-bottom:0.625rem">
        <div>
          <span style="font-weight:600">${p.name}</span>
          ${!p.isActive ? '<span class="badge badge-gray" style="margin-left:0.5rem">Inativo</span>' : ''}
        </div>
        <div style="display:flex;align-items:center;gap:0.5rem">
          <span style="font-size:var(--font-size-xs);color:var(--gray-500)">
            ${p.expiresAt ? 'Expira ' + formatDate(p.expiresAt) : 'Sem vencimento'}
          </span>
          ${allowDelete ? `<button class="btn btn-danger btn-sm" data-delete-pkg="${p.id}" style="padding:2px 8px;font-size:0.7rem">✕</button>` : ''}
        </div>
      </div>
      <div style="display:flex;flex-wrap:wrap;gap:0.5rem">
        ${p.items.map(i => `
          <div style="display:flex;align-items:center;gap:0.375rem;background:var(--gray-50);border:1px solid var(--gray-200);border-radius:var(--border-radius);padding:0.3rem 0.625rem;font-size:var(--font-size-sm)">
            <div style="width:8px;height:8px;border-radius:2px;background:${i.classTypeColor};flex-shrink:0"></div>
            <span style="color:var(--gray-600)">${i.classTypeName}</span>
            <span style="font-weight:600;color:${i.remainingCredits > 0 ? 'var(--success)' : 'var(--gray-400)'}">${i.remainingCredits}</span>
            <span style="color:var(--gray-400);font-size:0.65rem">/ ${i.totalCredits}</span>
          </div>
        `).join('')}
      </div>
    </div>
  `;

  return active.map(p => renderPkg(p, true)).join('') +
    (inactive.length ? `
      <details style="margin-top:0.5rem">
        <summary style="font-size:var(--font-size-sm);color:var(--gray-500);cursor:pointer;user-select:none">
          ${inactive.length} pacote${inactive.length > 1 ? 's' : ''} inativo${inactive.length > 1 ? 's' : ''}
        </summary>
        <div style="margin-top:0.5rem">${inactive.map(p => renderPkg(p, false)).join('')}</div>
      </details>
    ` : '');
}

function renderBookingsHtml(bookings, isGym) {
  if (!bookings.length) return emptyState('📅', 'Nenhum agendamento encontrado');

  const statusInfo = {
    Confirmed:  { label: 'Confirmado',  cls: 'badge-success' },
    CheckedIn:  { label: 'Realizado',   cls: 'badge-primary' },
    Cancelled:  { label: 'Cancelado',   cls: 'badge-danger'  },
  };

  return `
    <div style="overflow-x:auto">
      <table style="width:100%;border-collapse:collapse;font-size:var(--font-size-sm)">
        <thead>
          <tr style="border-bottom:2px solid var(--gray-200);text-align:left">
            <th style="padding:0.5rem 0.75rem;color:var(--gray-500);font-weight:500">Data</th>
            <th style="padding:0.5rem 0.75rem;color:var(--gray-500);font-weight:500">Horário</th>
            <th style="padding:0.5rem 0.75rem;color:var(--gray-500);font-weight:500">${isGym ? 'Modalidade' : 'Serviço'}</th>
            <th style="padding:0.5rem 0.75rem;color:var(--gray-500);font-weight:500">Status</th>
            <th style="padding:0.5rem 0.75rem;color:var(--gray-500);font-weight:500">Pagamento</th>
          </tr>
        </thead>
        <tbody>
          ${bookings.map(b => {
            const info = statusInfo[b.status] ?? { label: b.status, cls: 'badge-gray' };
            const [y, m, d] = b.sessionDate.split('-');
            const dateStr = `${d}/${m}/${y}`;
            const time = b.sessionStartTime?.slice(0, 5) ?? '—';
            const pmLabel = { Cash: '💵 Dinheiro', Pix: '⚡ PIX', DebitCard: '💳 Débito', CreditCard: '💳 Crédito' };
            const payCell = b.paymentMethod
              ? `<span style="color:var(--color-success);font-weight:500">${pmLabel[b.paymentMethod] ?? b.paymentMethod}${b.paymentMethod === 'CreditCard' && b.installments > 1 ? ` ${b.installments}x` : ''}</span>
                 <span style="color:var(--gray-500);font-size:0.75rem;display:block">R$ ${Number(b.grossAmount).toFixed(2).replace('.', ',')}</span>`
              : '<span style="color:var(--gray-300)">—</span>';
            return `
              <tr style="border-bottom:1px solid var(--gray-100)">
                <td style="padding:0.625rem 0.75rem">${dateStr}</td>
                <td style="padding:0.625rem 0.75rem">${time}</td>
                <td style="padding:0.625rem 0.75rem">${b.classTypeName || '—'}</td>
                <td style="padding:0.625rem 0.75rem"><span class="badge ${info.cls}">${info.label}</span></td>
                <td style="padding:0.625rem 0.75rem">${payCell}</td>
              </tr>
            `;
          }).join('')}
        </tbody>
      </table>
    </div>
  `;
}

// ── Edit modal ────────────────────────────────────────────────────────────────

function openEditModal(student, onSaved) {
  createModal({
    id: 'editStudentModal',
    title: 'Editar cliente',
    body: `
      <form id="editStudentForm">
        <div class="form-group">
          <label class="form-label">${t('field.name')} *</label>
          <input class="form-control" id="esName" required value="${student.name ?? ''}">
        </div>
        <div class="form-group">
          <label class="form-label">${t('field.email')}</label>
          <input class="form-control" value="${student.email ?? ''}" readonly style="opacity:0.6">
        </div>
        <div class="form-group">
          <label class="form-label">${t('field.phone')}</label>
          <input class="form-control" id="esPhone" value="${student.phone ?? ''}">
        </div>
        <div class="form-group">
          <label class="form-label">${t('field.birthDate')}</label>
          <input class="form-control" id="esBirth" type="date" value="${student.birthDate?.split('T')[0] ?? ''}">
        </div>
        <div class="form-group">
          <label class="form-label">${t('field.status')}</label>
          <select class="form-control" id="esStatus">
            <option value="Active"   ${student.status === 'Active'    ? 'selected' : ''}>${t('status.active')}</option>
            <option value="Inactive" ${student.status === 'Inactive'  ? 'selected' : ''}>${t('status.inactive')}</option>
            <option value="Suspended"${student.status === 'Suspended' ? 'selected' : ''}>${t('status.suspended')}</option>
          </select>
        </div>
        <div class="form-group">
          <label class="form-label">${t('students.field.healthNotes')}</label>
          <textarea class="form-control" id="esHealth" rows="3">${student.healthNotes ?? ''}</textarea>
        </div>
      </form>
    `,
    footer: `
      <button class="btn btn-secondary" onclick="closeModal('editStudentModal')">${t('btn.cancel')}</button>
      <button class="btn btn-primary" id="btnSaveEdit">${t('btn.save')}</button>
    `,
  });
  openModal('editStudentModal');

  document.getElementById('btnSaveEdit').addEventListener('click', async () => {
    const body = {
      name:        document.getElementById('esName').value.trim(),
      phone:       document.getElementById('esPhone').value.trim() || null,
      birthDate:   document.getElementById('esBirth').value || null,
      healthNotes: document.getElementById('esHealth').value.trim() || null,
      status:      document.getElementById('esStatus').value,
    };
    try {
      const updated = await api.put(`/students/${student.id}`, body);
      showToast(t('students.saved'), 'success');
      closeModal('editStudentModal');
      onSaved(updated);
    } catch (e) {
      showToast(t('error.prefix') + e.message, 'error');
    }
  });
}

// ── New Package modal ─────────────────────────────────────────────────────────

function openNewPackageModal(studentId, classTypes, templates, onSuccess) {
  const activeClassTypes = classTypes.filter(ct => ct.isActive);

  const templateOptions = templates.length
    ? templates.map(tpl => `<option value="${tpl.id}">${tpl.name}${tpl.durationDays ? ` · ${tpl.durationDays}d` : ''}</option>`).join('')
    : `<option disabled>${t('packages.noTemplates')}</option>`;

  const itemsForm = activeClassTypes.map(ct => `
    <div class="form-group" style="background:var(--gray-50);padding:0.75rem;border-radius:var(--border-radius);border:1px solid var(--gray-200)">
      <div style="display:flex;align-items:center;gap:0.5rem;margin-bottom:0.5rem">
        <div style="width:10px;height:10px;border-radius:2px;background:${ct.color}"></div>
        <label class="form-label" style="margin:0">${ct.name}</label>
      </div>
      <div style="display:grid;grid-template-columns:1fr 1fr;gap:0.5rem">
        <div>
          <label class="form-label" style="font-size:0.7rem">${t('packages.field.credits')}</label>
          <input class="form-control" id="credits_${ct.id}" type="number" min="0" value="0">
        </div>
        <div>
          <label class="form-label" style="font-size:0.7rem">${t('packages.field.pricePerClass')}</label>
          <input class="form-control" id="price_${ct.id}" type="number" min="0" step="0.01" value="0">
        </div>
      </div>
    </div>
  `).join('');

  createModal({
    id: 'newPkgModal',
    title: t('packages.modal.new'),
    body: `
      <div style="display:flex;gap:0.5rem;margin-bottom:1.25rem">
        <button class="btn btn-primary" id="tabTpl" style="flex:1">${t('packages.tab.template')}</button>
        <button class="btn btn-secondary" id="tabCustom" style="flex:1">${t('packages.tab.custom')}</button>
      </div>
      <div id="secTpl">
        <div class="form-group">
          <label class="form-label">${t('packages.field.template')}</label>
          <select class="form-control" id="tplSelect">${templateOptions}</select>
        </div>
        <div class="form-group">
          <label class="form-label">${t('packages.field.expiryOverride')}</label>
          <input class="form-control" id="tplExpiry" type="date">
        </div>
      </div>
      <div id="secCustom" class="hidden">
        <div class="form-group">
          <label class="form-label">${t('packages.field.name')}</label>
          <input class="form-control" id="pkgName" placeholder="${t('packages.namePlaceholder')}">
        </div>
        <div class="form-group">
          <label class="form-label">${t('packages.field.expiry')}</label>
          <input class="form-control" id="pkgExpiry" type="date">
        </div>
        ${itemsForm}
      </div>
    `,
    footer: `
      <button class="btn btn-secondary" onclick="closeModal('newPkgModal')">${t('btn.cancel')}</button>
      <button class="btn btn-primary" id="btnSavePkg">${t('packages.modal.new')}</button>
    `,
  });
  openModal('newPkgModal');

  const tabTpl    = document.getElementById('tabTpl');
  const tabCustom = document.getElementById('tabCustom');
  const secTpl    = document.getElementById('secTpl');
  const secCustom = document.getElementById('secCustom');

  tabTpl.addEventListener('click', () => {
    tabTpl.className = 'btn btn-primary'; tabCustom.className = 'btn btn-secondary';
    secTpl.classList.remove('hidden'); secCustom.classList.add('hidden');
  });
  tabCustom.addEventListener('click', () => {
    tabCustom.className = 'btn btn-primary'; tabTpl.className = 'btn btn-secondary';
    secCustom.classList.remove('hidden'); secTpl.classList.add('hidden');
  });

  document.getElementById('btnSavePkg').addEventListener('click', async () => {
    const isTemplate = !secTpl.classList.contains('hidden');
    try {
      if (isTemplate) {
        const templateId = document.getElementById('tplSelect').value;
        if (!templateId) { showToast(t('packages.select.required'), 'error'); return; }
        const expiry = document.getElementById('tplExpiry').value || null;
        await api.post(`/package-templates/${templateId}/assign`, { studentId, expiresAt: expiry });
      } else {
        const items = activeClassTypes.map(ct => ({
          classTypeId:    ct.id,
          totalCredits:   parseInt(document.getElementById(`credits_${ct.id}`).value) || 0,
          pricePerCredit: parseFloat(document.getElementById(`price_${ct.id}`).value) || 0,
        })).filter(i => i.totalCredits > 0);
        if (!items.length) { showToast(t('packages.addCredits'), 'error'); return; }
        await api.post('/packages', {
          studentId,
          name:      document.getElementById('pkgName').value.trim() || 'Pacote',
          expiresAt: document.getElementById('pkgExpiry').value || null,
          items,
        });
      }
      showToast(t('packages.created'), 'success');
      closeModal('newPkgModal');
      await onSuccess();
    } catch (e) {
      showToast(t('error.prefix') + e.message, 'error');
    }
  });
}
