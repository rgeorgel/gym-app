import { api } from '../api.js';
import { showToast, createModal, openModal, closeModal, formatDate, statusBadge, emptyState, confirm } from '../ui.js';

let allStudents = [];

export async function renderStudents(container) {
  container.innerHTML = `
    <div class="filters-bar">
      <input type="text" id="studentSearch" class="search-input" placeholder="Buscar por nome ou e-mail...">
      <select id="studentStatus" class="form-control" style="width:auto">
        <option value="">Todos os status</option>
        <option value="Active">Ativo</option>
        <option value="Inactive">Inativo</option>
        <option value="Suspended">Suspenso</option>
      </select>
      <button class="btn btn-primary" id="btnNewStudent">+ Novo Aluno</button>
    </div>
    <div class="card">
      <div class="table-wrapper">
        <table id="studentsTable">
          <thead>
            <tr>
              <th>Nome</th><th>E-mail</th><th>Telefone</th><th>Status</th><th>Cadastro</th><th></th>
            </tr>
          </thead>
          <tbody id="studentsTbody"><tr><td colspan="6"><div class="loading-center"><span class="spinner"></span></div></td></tr></tbody>
        </table>
      </div>
    </div>
  `;

  await loadStudents();

  document.getElementById('btnNewStudent').addEventListener('click', () => openStudentModal());
  document.getElementById('studentSearch').addEventListener('input', filterStudents);
  document.getElementById('studentStatus').addEventListener('change', filterStudents);
}

async function loadStudents() {
  try {
    allStudents = await api.get('/students');
    renderTable(allStudents);
  } catch (e) {
    showToast('Erro ao carregar alunos: ' + e.message, 'error');
  }
}

function filterStudents() {
  const q = document.getElementById('studentSearch').value.toLowerCase();
  const status = document.getElementById('studentStatus').value;
  const filtered = allStudents.filter(s =>
    (!q || s.name.toLowerCase().includes(q) || s.email.toLowerCase().includes(q)) &&
    (!status || s.status === status)
  );
  renderTable(filtered);
}

function renderTable(students) {
  const tbody = document.getElementById('studentsTbody');
  if (!tbody) return;
  if (!students.length) {
    tbody.innerHTML = `<tr><td colspan="6">${emptyState('👤', 'Nenhum aluno encontrado')}</td></tr>`;
    return;
  }
  tbody.innerHTML = students.map(s => `
    <tr>
      <td><div class="font-medium">${s.name}</div></td>
      <td class="text-sm text-muted">${s.email}</td>
      <td class="text-sm">${s.phone ?? '—'}</td>
      <td>${statusBadge(s.status)}</td>
      <td class="text-sm text-muted">${formatDate(s.createdAt)}</td>
      <td>
        <div class="flex gap-2">
          <button class="btn btn-secondary btn-sm" onclick="window._editStudent('${s.id}')">Editar</button>
          <button class="btn btn-secondary btn-sm" onclick="window._viewPackages('${s.id}', '${s.name}')">Pacotes</button>
        </div>
      </td>
    </tr>
  `).join('');

  window._editStudent = (id) => openStudentModal(allStudents.find(s => s.id === id));
  window._viewPackages = (id, name) => openPackagesModal(id, name);
}

function openStudentModal(student = null) {
  const modal = createModal({
    id: 'studentModal',
    title: student ? 'Editar Aluno' : 'Novo Aluno',
    body: `
      <form id="studentForm">
        <div class="form-group">
          <label class="form-label">Nome *</label>
          <input class="form-control" id="sName" required value="${student?.name ?? ''}">
        </div>
        <div class="form-group">
          <label class="form-label">E-mail *</label>
          <input class="form-control" id="sEmail" type="email" required value="${student?.email ?? ''}" ${student ? 'readonly' : ''}>
        </div>
        <div class="form-group">
          <label class="form-label">Telefone</label>
          <input class="form-control" id="sPhone" value="${student?.phone ?? ''}">
        </div>
        <div class="form-group">
          <label class="form-label">Data de nascimento</label>
          <input class="form-control" id="sBirth" type="date" value="${student?.birthDate?.split('T')[0] ?? ''}">
        </div>
        ${student ? `
        <div class="form-group">
          <label class="form-label">Status</label>
          <select class="form-control" id="sStatus">
            <option value="Active" ${student.status==='Active'?'selected':''}>Ativo</option>
            <option value="Inactive" ${student.status==='Inactive'?'selected':''}>Inativo</option>
            <option value="Suspended" ${student.status==='Suspended'?'selected':''}>Suspenso</option>
          </select>
        </div>` : ''}
        <div class="form-group">
          <label class="form-label">Observações de saúde</label>
          <textarea class="form-control" id="sHealth">${student?.healthNotes ?? ''}</textarea>
        </div>
      </form>
    `,
    footer: `
      <button class="btn btn-secondary" onclick="closeModal('studentModal')">Cancelar</button>
      <button class="btn btn-primary" id="btnSaveStudent">Salvar</button>
    `
  });

  openModal('studentModal');

  document.getElementById('btnSaveStudent').addEventListener('click', async () => {
    const body = {
      name: document.getElementById('sName').value.trim(),
      email: document.getElementById('sEmail').value.trim(),
      phone: document.getElementById('sPhone').value.trim() || null,
      birthDate: document.getElementById('sBirth').value || null,
      healthNotes: document.getElementById('sHealth').value.trim() || null,
    };
    if (student) body.status = document.getElementById('sStatus').value;

    try {
      if (student) await api.put(`/students/${student.id}`, body);
      else await api.post('/students', body);
      showToast(student ? 'Aluno atualizado' : 'Aluno criado com sucesso', 'success');
      closeModal('studentModal');
      await loadStudents();
    } catch (e) {
      showToast('Erro: ' + e.message, 'error');
    }
  });
}

async function openPackagesModal(studentId, studentName) {
  const modal = createModal({
    id: 'packagesModal',
    title: `Pacotes — ${studentName}`,
    body: '<div class="loading-center"><span class="spinner"></span></div>',
    footer: `
      <button class="btn btn-secondary" onclick="closeModal('packagesModal')">Fechar</button>
      <button class="btn btn-primary" id="btnNewPackage">+ Novo Pacote</button>
    `
  });
  openModal('packagesModal');

  try {
    const [packages, classTypes] = await Promise.all([
      api.get(`/students/${studentId}/packages`),
      api.get('/class-types'),
    ]);

    const body = modal.querySelector('.modal-body');
    if (!packages.length) {
      body.innerHTML = emptyState('📦', 'Nenhum pacote cadastrado');
    } else {
      body.innerHTML = packages.map(p => `
        <div class="package-card" style="margin-bottom:0.75rem">
          <div class="package-header">
            <span class="package-name">${p.name}</span>
            <span class="package-expiry">Vence: ${p.expiresAt ? new Date(p.expiresAt).toLocaleDateString('pt-BR') : 'Sem validade'}</span>
          </div>
          <div class="package-items">
            ${p.items.map(i => `
              <div class="credit-item">
                <div class="credit-color" style="background:${i.classTypeColor}"></div>
                <div class="credit-info">
                  <div class="credit-type">${i.classTypeName}</div>
                  <div class="credit-used">${i.usedCredits}/${i.totalCredits} usados · R$ ${i.pricePerCredit}/aula</div>
                </div>
                <div class="credit-remaining">${i.remainingCredits}</div>
              </div>
            `).join('')}
          </div>
        </div>
      `).join('');
    }

    document.getElementById('btnNewPackage').addEventListener('click', () => openNewPackageModal(studentId, classTypes, async () => {
      const packages2 = await api.get(`/students/${studentId}/packages`);
      body.innerHTML = ''; // re-render handled by reopening
    }));
  } catch (e) {
    showToast('Erro ao carregar pacotes: ' + e.message, 'error');
  }
}

function openNewPackageModal(studentId, classTypes, onSuccess) {
  const modal = createModal({
    id: 'newPackageModal',
    title: 'Novo Pacote',
    body: `
      <div class="form-group">
        <label class="form-label">Nome do pacote</label>
        <input class="form-control" id="pkgName" placeholder="ex: Plano Março 2026" required>
      </div>
      <div class="form-group">
        <label class="form-label">Validade</label>
        <input class="form-control" id="pkgExpiry" type="date">
      </div>
      <div id="pkgItems">
        ${classTypes.filter(ct => ct.isActive).map(ct => `
          <div class="form-group" style="background:var(--gray-50);padding:0.75rem;border-radius:var(--border-radius);border:1px solid var(--gray-200)">
            <div style="display:flex;align-items:center;gap:0.5rem;margin-bottom:0.5rem">
              <div style="width:10px;height:10px;border-radius:2px;background:${ct.color}"></div>
              <label class="form-label" style="margin:0">${ct.name}</label>
            </div>
            <div style="display:grid;grid-template-columns:1fr 1fr;gap:0.5rem">
              <div>
                <label class="form-label" style="font-size:0.7rem">Créditos</label>
                <input class="form-control" id="credits_${ct.id}" type="number" min="0" value="0" placeholder="0">
              </div>
              <div>
                <label class="form-label" style="font-size:0.7rem">R$/aula</label>
                <input class="form-control" id="price_${ct.id}" type="number" min="0" step="0.01" value="0" placeholder="0.00">
              </div>
            </div>
          </div>
        `).join('')}
      </div>
    `,
    footer: `
      <button class="btn btn-secondary" onclick="closeModal('newPackageModal')">Cancelar</button>
      <button class="btn btn-primary" id="btnSavePkg">Criar Pacote</button>
    `
  });
  openModal('newPackageModal');

  document.getElementById('btnSavePkg').addEventListener('click', async () => {
    const items = classTypes.filter(ct => ct.isActive).map(ct => ({
      classTypeId: ct.id,
      totalCredits: parseInt(document.getElementById(`credits_${ct.id}`).value) || 0,
      pricePerCredit: parseFloat(document.getElementById(`price_${ct.id}`).value) || 0,
    })).filter(i => i.totalCredits > 0);

    if (!items.length) { showToast('Adicione pelo menos um tipo de aula', 'error'); return; }

    try {
      await api.post('/packages', {
        studentId,
        name: document.getElementById('pkgName').value.trim() || 'Pacote',
        expiresAt: document.getElementById('pkgExpiry').value || null,
        items
      });
      showToast('Pacote criado com sucesso!', 'success');
      closeModal('newPackageModal');
      await onSuccess();
    } catch (e) {
      showToast('Erro: ' + e.message, 'error');
    }
  });
}
