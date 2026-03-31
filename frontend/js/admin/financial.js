import { api } from '../api.js';
import { showToast, createModal, openModal, closeModal, emptyState, confirm } from '../ui.js';

const PAYMENT_METHODS = [
  { value: 'Cash',       label: 'Dinheiro',      icon: '💵' },
  { value: 'Pix',        label: 'PIX',           icon: '⚡' },
  { value: 'DebitCard',  label: 'Débito',        icon: '💳' },
  { value: 'CreditCard', label: 'Crédito',       icon: '💳' },
];

const PAYMENT_LABEL = Object.fromEntries(PAYMENT_METHODS.map(p => [p.value, `${p.icon} ${p.label}`]));

const EXPENSE_CATEGORIES = [
  'Material e insumos',
  'Aluguel do espaço',
  'Equipamentos e manutenção',
  'Cursos e capacitação',
  'Marketing e divulgação',
  'Taxas e mensalidades',
  'Outras',
];

const FMT_BRL = (v) => 'R$ ' + Number(v).toFixed(2).replace('.', ',').replace(/\B(?=(\d{3})+(?!\d))/g, '.');
const FMT_DATE = (d) => { const [y,m,dd] = d.split('-'); return `${dd}/${m}/${y}`; };
const TODAY = () => new Date().toISOString().split('T')[0];

let currentYear  = new Date().getFullYear();
let currentMonth = new Date().getMonth() + 1;
let currentTab   = 'dashboard';

export async function renderFinancial(container, subRoute = null) {
  if (subRoute) {
    const [tab, monthStr] = subRoute.split('/');
    if (['dashboard', 'transactions', 'expenses', 'fees'].includes(tab)) currentTab = tab;
    if (monthStr) {
      const [y, m] = monthStr.split('-').map(Number);
      if (y && m) { currentYear = y; currentMonth = m; }
    }
  }

  history.replaceState(null, '', `#financial/${currentTab}/${currentYear}-${String(currentMonth).padStart(2, '0')}`);

  const monthLabel = new Date(currentYear, currentMonth - 1, 1)
    .toLocaleDateString('pt-BR', { month: 'long', year: 'numeric' });

  container.innerHTML = `
    <div style="display:flex;align-items:center;justify-content:space-between;flex-wrap:wrap;gap:0.75rem;margin-bottom:1.25rem">
      <div style="display:flex;align-items:center;gap:0.5rem">
        <button class="btn btn-secondary btn-sm" id="btnPrevMonth">‹</button>
        <span style="font-size:1rem;font-weight:600;min-width:160px;text-align:center;text-transform:capitalize">${monthLabel}</span>
        <button class="btn btn-secondary btn-sm" id="btnNextMonth">›</button>
      </div>
    </div>

    <div style="display:flex;gap:0.5rem;margin-bottom:1.25rem;border-bottom:1px solid var(--gray-200);padding-bottom:0">
      ${['dashboard','transactions','expenses','fees'].map(tab => `
        <button class="fin-tab btn btn-secondary btn-sm" data-tab="${tab}" style="border-radius:var(--border-radius) var(--border-radius) 0 0;border-bottom:none;${currentTab===tab?'background:var(--brand-primary);color:var(--brand-text-on-primary)':''}">
          ${{ dashboard: '📊 Dashboard', transactions: '💰 Receitas', expenses: '📋 Despesas', fees: '⚙️ Taxas' }[tab]}
        </button>
      `).join('')}
    </div>

    <div id="finContent"><div class="loading-center"><span class="spinner"></span></div></div>
  `;

  container.querySelectorAll('.fin-tab').forEach(btn => {
    btn.addEventListener('click', () => {
      currentTab = btn.dataset.tab;
      renderFinancial(container);
    });
  });

  container.querySelector('#btnPrevMonth').addEventListener('click', () => {
    if (currentMonth === 1) { currentMonth = 12; currentYear--; }
    else currentMonth--;
    renderFinancial(container);
  });
  container.querySelector('#btnNextMonth').addEventListener('click', () => {
    if (currentMonth === 12) { currentMonth = 1; currentYear++; }
    else currentMonth++;
    renderFinancial(container);
  });

  const content = container.querySelector('#finContent');
  if (currentTab === 'dashboard')    await renderDashboard(content, currentYear, currentMonth);
  if (currentTab === 'transactions') await renderTransactions(content, container, currentYear, currentMonth);
  if (currentTab === 'expenses')     await renderExpenses(content, container, currentYear, currentMonth);
  if (currentTab === 'fees')         await renderFees(content);
}

// ── Dashboard ──────────────────────────────────────────────────────────────────

async function renderDashboard(el, year, month) {
  try {
    const data = await api.get(`/financial/dashboard?year=${year}&month=${month}`);
    const kpi = (label, value, sub, color='var(--gray-800)') => `
      <div class="card" style="padding:1.25rem">
        <div style="font-size:var(--font-size-sm);color:var(--gray-500);margin-bottom:0.25rem">${label}</div>
        <div style="font-size:1.5rem;font-weight:700;color:${color}">${value}</div>
        ${sub ? `<div style="font-size:var(--font-size-xs);color:var(--gray-400);margin-top:0.2rem">${sub}</div>` : ''}
      </div>`;
    const diff = (cur, prev) => {
      if (!prev) return '';
      const d = cur - prev;
      const pct = Math.abs(Math.round(d / prev * 100));
      return d >= 0
        ? `<span style="color:var(--color-success)">▲ ${pct}% vs mês ant.</span>`
        : `<span style="color:var(--color-danger)">▼ ${pct}% vs mês ant.</span>`;
    };
    el.innerHTML = `
      <div style="display:grid;grid-template-columns:repeat(auto-fit,minmax(min(100%,200px),1fr));gap:1rem">
        ${kpi('Receita Bruta', FMT_BRL(data.grossRevenue), diff(data.grossRevenue, data.prevGrossRevenue), 'var(--color-success)')}
        ${kpi('Taxas de Cartão', FMT_BRL(data.cardFees), '', 'var(--color-danger)')}
        ${kpi('Receita Líquida', FMT_BRL(data.netRevenue), diff(data.netRevenue, data.prevNetRevenue))}
        ${kpi('Despesas', FMT_BRL(data.totalExpenses), diff(data.totalExpenses, data.prevTotalExpenses), 'var(--color-warning)')}
        ${kpi('Lucro Líquido', FMT_BRL(data.profit), diff(data.profit, data.prevProfit), data.profit >= 0 ? 'var(--color-success)' : 'var(--color-danger)')}
        ${kpi('Ticket Médio', FMT_BRL(data.ticketAverage), '')}
        ${kpi('Atendimentos', data.appointmentsCount, '')}
      </div>`;
  } catch (e) {
    el.innerHTML = `<div class="empty-state"><div class="empty-state-text">${e.message}</div></div>`;
  }
}

// ── Transactions ───────────────────────────────────────────────────────────────

async function renderTransactions(el, container, year, month) {
  const pad = (n) => String(n).padStart(2,'0');
  const from = `${year}-${pad(month)}-01`;
  const lastDay = new Date(year, month, 0).getDate();
  const to = `${year}-${pad(month)}-${lastDay}`;

  try {
    const items = await api.get(`/financial/transactions?from=${from}&to=${to}`);
    el.innerHTML = `
      <div style="display:flex;justify-content:flex-end;margin-bottom:1rem">
        <button class="btn btn-primary" id="btnNewTx">+ Nova receita</button>
      </div>
      ${!items.length ? `<div class="card" style="padding:2rem">${emptyState('💰', 'Nenhuma receita registrada neste mês')}</div>` : `
      <div style="display:flex;flex-direction:column;gap:0.5rem">
        ${items.map(tx => `
          <div class="card" style="padding:0.875rem 1rem">
            <div style="display:flex;align-items:flex-start;gap:0.75rem">
              <div style="min-width:2.75rem;text-align:center;flex-shrink:0">
                <div style="font-size:1.1rem;font-weight:700;line-height:1;color:var(--gray-800)">${tx.date.slice(8)}</div>
                <div style="font-size:0.65rem;color:var(--gray-400);text-transform:uppercase">${['jan','fev','mar','abr','mai','jun','jul','ago','set','out','nov','dez'][parseInt(tx.date.slice(5,7))-1]}</div>
              </div>
              <div style="flex:1;min-width:0">
                <div style="font-weight:500;color:var(--gray-900);white-space:nowrap;overflow:hidden;text-overflow:ellipsis">${tx.serviceName}</div>
                <div style="font-size:var(--font-size-xs);color:var(--gray-500);margin-top:0.1rem">
                  ${tx.studentName ? `${tx.studentName} · ` : ''}${PAYMENT_LABEL[tx.paymentMethod] ?? tx.paymentMethod}${tx.paymentMethod === 'CreditCard' && tx.installments > 1 ? ` ${tx.installments}x` : ''}
                </div>
                ${tx.cardFeePercentage > 0 ? `<div style="font-size:var(--font-size-xs);color:var(--color-danger);margin-top:0.1rem">Taxa: -${FMT_BRL(tx.cardFeeAmount)} (${tx.cardFeePercentage}%)</div>` : ''}
              </div>
              <div style="flex-shrink:0;text-align:right">
                <div style="font-weight:700;color:var(--color-success)">${FMT_BRL(tx.netAmount)}</div>
                ${tx.cardFeePercentage > 0 ? `<div style="font-size:var(--font-size-xs);color:var(--gray-400)">${FMT_BRL(tx.grossAmount)} bruto</div>` : ''}
              </div>
            </div>
            <div style="display:flex;justify-content:flex-end;gap:0.375rem;margin-top:0.625rem;padding-top:0.625rem;border-top:1px solid var(--gray-100)">
              <button class="btn btn-sm btn-secondary btn-edit-tx" data-tx='${JSON.stringify(tx)}'>Editar</button>
              <button class="btn btn-sm btn-danger btn-del-tx" data-id="${tx.id}">Excluir</button>
            </div>
          </div>
        `).join('')}
      </div>`}
    `;

    el.querySelector('#btnNewTx').addEventListener('click', () => openTransactionModal(el, container, year, month));
    el.querySelectorAll('.btn-edit-tx').forEach(btn => {
      btn.addEventListener('click', () => {
        const tx = JSON.parse(btn.dataset.tx);
        openTransactionModal(el, container, year, month, {
          id: tx.id,
          date: tx.date,
          serviceName: tx.serviceName,
          grossAmount: tx.grossAmount,
          paymentMethod: tx.paymentMethod,
          installments: tx.installments,
          studentName: tx.studentName,
          studentId: tx.studentId,
          notes: tx.notes,
        });
      });
    });
    el.querySelectorAll('.btn-del-tx').forEach(btn => {
      btn.addEventListener('click', async () => {
        if (!await confirm('Excluir esta receita?')) return;
        try {
          await api.delete(`/financial/transactions/${btn.dataset.id}`);
          showToast('Receita excluída.', 'success');
          await renderTransactions(el, container, year, month);
        } catch (e) { showToast(e.message, 'error'); }
      });
    });
  } catch (e) {
    el.innerHTML = `<div class="empty-state"><div class="empty-state-text">${e.message}</div></div>`;
  }
}

function openTransactionModal(el, container, year, month, prefill = {}, onSuccess = null) {
  const isEdit = !!prefill.id;
  createModal({
    id: 'txModal',
    title: isEdit ? '✏️ Editar receita' : '💰 Registrar receita',
    body: `
      <div style="display:grid;grid-template-columns:1fr 1fr;gap:1rem">
        <div class="form-group">
          <label class="form-label">Data *</label>
          <input class="form-control" id="txDate" type="date" value="${prefill.date ?? TODAY()}" max="${TODAY()}">
        </div>
        <div class="form-group">
          <label class="form-label">Valor bruto (R$) *</label>
          <input class="form-control" id="txGross" type="number" min="0.01" step="0.01" value="${prefill.grossAmount ?? ''}">
        </div>
      </div>
      <div class="form-group">
        <label class="form-label">Serviço *</label>
        <input class="form-control" id="txService" type="text" value="${prefill.serviceName ?? ''}" placeholder="Nome do serviço">
      </div>
      <div class="form-group">
        <label class="form-label">Cliente</label>
        <input class="form-control" id="txClient" type="text" value="${prefill.studentName ?? ''}" placeholder="Nome da cliente (opcional)">
      </div>
      <div style="display:grid;grid-template-columns:1fr 1fr;gap:1rem">
        <div class="form-group">
          <label class="form-label">Forma de pagamento *</label>
          <select class="form-control" id="txPayment">
            ${PAYMENT_METHODS.map(p => `<option value="${p.value}" ${(prefill.paymentMethod??'Cash')===p.value?'selected':''}>${p.icon} ${p.label}</option>`).join('')}
          </select>
        </div>
        <div class="form-group" id="txInstGroup" style="display:none">
          <label class="form-label">Parcelas</label>
          <select class="form-control" id="txInstallments">
            ${Array.from({length:12},(_,i)=>`<option value="${i+1}" ${(prefill.installments??1)===i+1?'selected':''}>${i+1}x</option>`).join('')}
          </select>
        </div>
      </div>
      <div class="form-group" id="txFeeDisplay" style="padding:0.75rem;background:var(--gray-50);border-radius:var(--border-radius);font-size:var(--font-size-sm);display:none">
        Taxa: <span id="txFeeVal">—</span> → Líquido: <span id="txNetVal" style="font-weight:700;color:var(--color-success)">—</span>
      </div>
      <div class="form-group">
        <label class="form-label">Observação</label>
        <input class="form-control" id="txNotes" type="text" placeholder="Opcional" value="${prefill.notes ?? ''}">
      </div>
    `,
    footer: `
      <button class="btn btn-secondary" onclick="closeModal('txModal')">Cancelar</button>
      <button class="btn btn-primary" id="btnSaveTx">Salvar</button>
    `,
  });
  openModal('txModal');

  let fees = {};
  api.get('/financial/fees').then(data => { fees = Object.fromEntries(data.map(f => [f.feeType, f.feePercentage])); updateFeeDisplay(); });

  const updateFeeDisplay = () => {
    const pm = document.getElementById('txPayment')?.value;
    const inst = parseInt(document.getElementById('txInstallments')?.value ?? '1');
    const gross = parseFloat(document.getElementById('txGross')?.value ?? '0');
    const instGroup = document.getElementById('txInstGroup');
    const feeDisplay = document.getElementById('txFeeDisplay');
    if (instGroup) instGroup.style.display = pm === 'CreditCard' ? '' : 'none';
    if (!feeDisplay) return;
    if (!gross || !pm) { feeDisplay.style.display = 'none'; return; }
    const feeType = pm === 'CreditCard' ? (inst <= 1 ? 'CreditCard1x' : inst <= 6 ? 'CreditCard2to6x' : 'CreditCard7to12x') : pm === 'DebitCard' ? 'DebitCard' : pm;
    const pct = fees[feeType] ?? 0;
    const feeAmt = Math.round(gross * pct / 100 * 100) / 100;
    const net = gross - feeAmt;
    feeDisplay.style.display = '';
    document.getElementById('txFeeVal').textContent = `${FMT_BRL(feeAmt)} (${pct}%)`;
    document.getElementById('txNetVal').textContent = FMT_BRL(net);
  };

  document.getElementById('txPayment').addEventListener('change', updateFeeDisplay);
  document.getElementById('txInstallments').addEventListener('change', updateFeeDisplay);
  document.getElementById('txGross').addEventListener('input', updateFeeDisplay);
  // Trigger initial display if payment is already CreditCard
  updateFeeDisplay();

  document.getElementById('btnSaveTx').addEventListener('click', async () => {
    const date    = document.getElementById('txDate').value;
    const gross   = parseFloat(document.getElementById('txGross').value);
    const service = document.getElementById('txService').value.trim();
    const pm      = document.getElementById('txPayment').value;
    const inst    = parseInt(document.getElementById('txInstallments')?.value ?? '1');
    const client  = document.getElementById('txClient').value.trim();
    const notes   = document.getElementById('txNotes').value.trim();

    if (!date || !gross || !service || !pm) { showToast('Preencha todos os campos obrigatórios.', 'error'); return; }

    const btn = document.getElementById('btnSaveTx');
    btn.disabled = true;
    try {
      if (isEdit) {
        await api.put(`/financial/transactions/${prefill.id}`, {
          date, grossAmount: gross, serviceName: service,
          paymentMethod: pm, installments: inst,
          studentName: client || null, notes: notes || null,
        });
        showToast('Receita atualizada.', 'success');
      } else {
        await api.post('/financial/transactions', {
          date, grossAmount: gross, serviceName: service,
          paymentMethod: pm, installments: inst,
          studentName: client || null, notes: notes || null,
          studentId: prefill.studentId ?? null, bookingId: prefill.bookingId ?? null,
        });
        showToast('Receita registrada.', 'success');
      }
      closeModal('txModal');
      if (onSuccess) onSuccess();
      else await renderTransactions(el, container, year, month);
    } catch (e) { showToast(e.message, 'error'); }
    finally { btn.disabled = false; }
  });
}

// ── Expenses ────────────────────────────────────────────────────────────────────

async function renderExpenses(el, container, year, month) {
  try {
    const items = await api.get(`/financial/expenses?year=${year}&month=${month}`);
    const total = items.reduce((s, e) => s + e.amount, 0);
    el.innerHTML = `
      <div style="display:flex;align-items:center;justify-content:space-between;margin-bottom:1rem;flex-wrap:wrap;gap:0.5rem">
        <span style="font-size:var(--font-size-sm);color:var(--gray-600)">Total: <strong>${FMT_BRL(total)}</strong></span>
        <button class="btn btn-primary" id="btnNewExp">+ Nova despesa</button>
      </div>
      ${!items.length ? `<div class="card" style="padding:2rem">${emptyState('📋', 'Nenhuma despesa registrada neste mês')}</div>` : `
      <div style="display:flex;flex-direction:column;gap:0.5rem">
        ${items.map(exp => `
          <div class="card" style="padding:1rem">
            <div style="display:flex;align-items:flex-start;gap:0.75rem">
              <div style="flex:1;min-width:0">
                <div style="display:flex;align-items:center;gap:0.5rem;flex-wrap:wrap;margin-bottom:0.25rem">
                  <span style="font-weight:600">${exp.description}</span>
                  <span class="badge badge-gray" style="font-size:0.7rem">${exp.category}</span>
                  ${exp.isRecurring ? '<span class="badge" style="background:rgba(59,130,246,0.1);color:var(--color-info);font-size:0.7rem">🔁 Recorrente</span>' : ''}
                </div>
                <div style="font-size:var(--font-size-sm);color:var(--gray-500)">${FMT_DATE(exp.date)}</div>
              </div>
              <div style="display:flex;align-items:center;gap:0.5rem;flex-shrink:0">
                <span style="font-weight:700;color:var(--color-danger)">${FMT_BRL(exp.amount)}</span>
                <button class="btn btn-sm btn-secondary btn-edit-exp" data-id="${exp.id}" data-json='${JSON.stringify(exp)}'>Editar</button>
                <button class="btn btn-sm btn-danger btn-del-exp" data-id="${exp.id}">Excluir</button>
              </div>
            </div>
          </div>
        `).join('')}
      </div>`}
    `;

    el.querySelector('#btnNewExp').addEventListener('click', () => openExpenseModal(el, container, year, month));
    el.querySelectorAll('.btn-edit-exp').forEach(btn => {
      btn.addEventListener('click', () => openExpenseModal(el, container, year, month, JSON.parse(btn.dataset.json)));
    });
    el.querySelectorAll('.btn-del-exp').forEach(btn => {
      btn.addEventListener('click', async () => {
        if (!await confirm('Excluir esta despesa?')) return;
        try {
          await api.delete(`/financial/expenses/${btn.dataset.id}`);
          showToast('Despesa excluída.', 'success');
          await renderExpenses(el, container, year, month);
        } catch (e) { showToast(e.message, 'error'); }
      });
    });
  } catch (e) {
    el.innerHTML = `<div class="empty-state"><div class="empty-state-text">${e.message}</div></div>`;
  }
}

function openExpenseModal(el, container, year, month, prefill = null) {
  const isEdit = !!prefill;
  const pad = (n) => String(n).padStart(2,'0');
  const defaultDate = `${year}-${pad(month)}-01`;
  createModal({
    id: 'expModal',
    title: isEdit ? 'Editar despesa' : '📋 Nova despesa',
    body: `
      <div style="display:grid;grid-template-columns:1fr 1fr;gap:1rem">
        <div class="form-group">
          <label class="form-label">Data *</label>
          <input class="form-control" id="expDate" type="date" value="${prefill?.date ?? defaultDate}">
        </div>
        <div class="form-group">
          <label class="form-label">Valor (R$) *</label>
          <input class="form-control" id="expAmount" type="number" min="0.01" step="0.01" value="${prefill?.amount ?? ''}">
        </div>
      </div>
      <div class="form-group">
        <label class="form-label">Categoria *</label>
        <select class="form-control" id="expCategory">
          ${EXPENSE_CATEGORIES.map(c => `<option value="${c}" ${prefill?.category===c?'selected':''}>${c}</option>`).join('')}
        </select>
      </div>
      <div class="form-group">
        <label class="form-label">Descrição *</label>
        <input class="form-control" id="expDesc" type="text" value="${prefill?.description ?? ''}" placeholder="Ex: Aluguel sala dezembro">
      </div>
      <div class="form-group" style="display:flex;align-items:center;gap:0.5rem">
        <input type="checkbox" id="expRecurring" ${prefill?.isRecurring?'checked':''} style="width:16px;height:16px">
        <label for="expRecurring" style="margin:0;cursor:pointer">Despesa recorrente (repete todo mês)</label>
      </div>
    `,
    footer: `
      <button class="btn btn-secondary" onclick="closeModal('expModal')">Cancelar</button>
      <button class="btn btn-primary" id="btnSaveExp">${isEdit ? 'Salvar' : 'Adicionar'}</button>
    `,
  });
  openModal('expModal');

  document.getElementById('btnSaveExp').addEventListener('click', async () => {
    const date     = document.getElementById('expDate').value;
    const amount   = parseFloat(document.getElementById('expAmount').value);
    const category = document.getElementById('expCategory').value;
    const desc     = document.getElementById('expDesc').value.trim();
    const recurring = document.getElementById('expRecurring').checked;

    if (!date || !amount || !category || !desc) { showToast('Preencha todos os campos.', 'error'); return; }

    const btn = document.getElementById('btnSaveExp');
    btn.disabled = true;
    try {
      const body = { date, amount, category, description: desc, isRecurring: recurring };
      if (isEdit) await api.put(`/financial/expenses/${prefill.id}`, body);
      else        await api.post('/financial/expenses', body);
      showToast(isEdit ? 'Despesa atualizada.' : 'Despesa adicionada.', 'success');
      closeModal('expModal');
      await renderExpenses(el, container, year, month);
    } catch (e) { showToast(e.message, 'error'); }
    finally { btn.disabled = false; }
  });
}

// ── Card Fee Config ─────────────────────────────────────────────────────────────

async function renderFees(el) {
  try {
    const fees = await api.get('/financial/fees');
    const feeMap = Object.fromEntries(fees.map(f => [f.feeType, f.feePercentage]));
    const row = (id, label, key) => `
      <div style="display:flex;align-items:center;justify-content:space-between;padding:0.75rem 0;border-bottom:1px solid var(--gray-100)">
        <span style="font-size:var(--font-size-sm)">${label}</span>
        <div style="display:flex;align-items:center;gap:0.5rem">
          <input class="form-control" id="fee_${id}" type="number" min="0" max="20" step="0.1"
            value="${feeMap[key] ?? 0}" style="width:90px;text-align:right">
          <span style="color:var(--gray-500);font-size:var(--font-size-sm)">%</span>
        </div>
      </div>`;
    el.innerHTML = `
      <div class="card" style="max-width:500px">
        <div class="card-body" style="padding:1.5rem">
          <h3 style="margin:0 0 0.25rem">Taxas da maquininha</h3>
          <p class="text-sm text-muted" style="margin:0 0 1.25rem">Configure as taxas cobradas pela sua maquininha. Serão usadas para calcular o valor líquido automaticamente.</p>
          ${row('cash',     '💵 Dinheiro',         'Cash')}
          ${row('pix',      '⚡ PIX',               'Pix')}
          ${row('debit',    '💳 Débito',            'DebitCard')}
          ${row('c1x',      '💳 Crédito à vista',   'CreditCard1x')}
          ${row('c2_6',     '💳 Crédito 2x – 6x',  'CreditCard2to6x')}
          ${row('c7_12',    '💳 Crédito 7x – 12x', 'CreditCard7to12x')}
          <div style="margin-top:1rem;display:flex;justify-content:flex-end">
            <button class="btn btn-primary" id="btnSaveFees">Salvar taxas</button>
          </div>
        </div>
      </div>`;

    el.querySelector('#btnSaveFees').addEventListener('click', async () => {
      const btn = el.querySelector('#btnSaveFees');
      btn.disabled = true;
      try {
        await api.put('/financial/fees', {
          cash:             parseFloat(el.querySelector('#fee_cash').value)   || 0,
          pix:              parseFloat(el.querySelector('#fee_pix').value)    || 0,
          debitCard:        parseFloat(el.querySelector('#fee_debit').value)  || 0,
          creditCard1x:     parseFloat(el.querySelector('#fee_c1x').value)   || 0,
          creditCard2to6x:  parseFloat(el.querySelector('#fee_c2_6').value)  || 0,
          creditCard7to12x: parseFloat(el.querySelector('#fee_c7_12').value) || 0,
        });
        showToast('Taxas salvas com sucesso.', 'success');
      } catch (e) { showToast(e.message, 'error'); }
      finally { btn.disabled = false; }
    });
  } catch (e) {
    el.innerHTML = `<div class="empty-state"><div class="empty-state-text">${e.message}</div></div>`;
  }
}

// ── Public helper: open checkout from appointments page ────────────────────────

export function openCheckoutModal(prefill, onSuccess = null) {
  // prefill: { date, serviceName, grossAmount, studentName, studentId, bookingId }
  const container = document.getElementById('contentArea');
  const tempEl = document.createElement('div');
  openTransactionModal(tempEl, container, new Date().getFullYear(), new Date().getMonth() + 1, prefill, onSuccess);
}
