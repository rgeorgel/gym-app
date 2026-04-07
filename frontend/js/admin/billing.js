import { api } from '../api.js?v=202603311200';
import { showToast, formatDate, confirm, applyPhoneMask, applyTaxIdMask, createModal, openModal, closeModal } from '../ui.js?v=202603311200';

export async function renderBilling(container) {
  container.innerHTML = `<div class="loading-center"><span class="spinner"></span></div>`;

  let status;
  try {
    status = await api.get('/billing/status');
  } catch (e) {
    container.innerHTML = `<div class="empty-state"><div class="empty-state-text">Erro ao carregar assinatura: ${e.message}</div></div>`;
    return;
  }

  container.innerHTML = buildPage(status);
  attachEvents(container);
}

function statusBadge(s) {
  const map = {
    Trial:     ['badge-info',    'Trial'],
    Active:    ['badge-success', 'Ativa'],
    PastDue:   ['badge-warning', 'Inadimplente'],
    Canceled:  ['badge-danger',  'Cancelada'],
    Suspended: ['badge-gray',    'Suspensa'],
  };
  const [cls, label] = map[s] ?? ['badge-gray', s];
  return `<span class="badge ${cls}">${label}</span>`;
}

function buildPage(s) {
  const isActive    = s.status === 'Active';
  const isTrial     = s.status === 'Trial';
  const isPastDue   = s.status === 'PastDue';
  const isCanceled  = s.status === 'Canceled';
  const canSetup    = !isActive;
  const canRenew    = isActive && !s.hasCardSubscription;
  const canCancel   = isActive;

  let infoHtml = '';

  const priceFormatted = s.subscriptionPriceCents
    ? `R$ ${(s.subscriptionPriceCents / 100).toLocaleString('pt-BR', { minimumFractionDigits: 2 })}/mês`
    : '';

  if (isTrial) {
    const endsAt = s.trialEndsAt ? formatDate(s.trialEndsAt) : '—';
    infoHtml = `
      <div style="background:var(--color-info);background:rgba(59,130,246,0.08);border:1px solid rgba(59,130,246,0.25);border-radius:var(--border-radius);padding:1rem 1.25rem;margin-bottom:1.5rem">
        <strong>Período de trial</strong> — ${s.trialDaysRemaining} dia(s) restante(s) (até ${endsAt}).<br>
        <span style="font-size:0.875rem;color:var(--gray-600)">Configure o pagamento antes do fim do trial para não perder o acesso.</span>
      </div>`;
  } else if (isPastDue) {
    infoHtml = `
      <div style="background:rgba(245,158,11,0.08);border:1px solid rgba(245,158,11,0.35);border-radius:var(--border-radius);padding:1rem 1.25rem;margin-bottom:1.5rem">
        <strong>Pagamento pendente</strong> — a assinatura está inadimplente. Configure o pagamento para reativar o acesso dos clientes.
      </div>`;
  } else if (isCanceled) {
    const until = s.currentPeriodEnd ? ` até ${formatDate(s.currentPeriodEnd)}` : '';
    infoHtml = `
      <div style="background:rgba(239,68,68,0.08);border:1px solid rgba(239,68,68,0.25);border-radius:var(--border-radius);padding:1rem 1.25rem;margin-bottom:1.5rem">
        <strong>Assinatura cancelada</strong> — acesso dos clientes mantido${until}. Configure um novo pagamento para reativar.
      </div>`;
  }

  const periodEndRow = s.currentPeriodEnd
    ? `<div class="settings-row"><span class="text-muted">Acesso garantido até</span><strong>${formatDate(s.currentPeriodEnd)}</strong></div>`
    : '';

  const trialRow = isTrial
    ? `<div class="settings-row"><span class="text-muted">Trial expira em</span><strong>${s.trialDaysRemaining} dia(s)</strong></div>`
    : '';

  return `
    <div style="max-width:600px;display:flex;flex-direction:column;gap:1.25rem">

      ${infoHtml}

      <div class="card">
        <div class="card-body" style="padding:1.5rem">
          <h3 style="margin:0 0 1.25rem">Status da assinatura</h3>

          <div style="display:flex;flex-direction:column;gap:0.75rem">
            <div class="settings-row">
              <span class="text-muted">Status</span>
              ${statusBadge(s.status)}
            </div>
            <div class="settings-row">
              <span class="text-muted">Acesso dos clientes</span>
              ${s.hasStudentAccess
                ? '<span class="badge badge-success">Liberado</span>'
                : '<span class="badge badge-danger">Bloqueado</span>'}
            </div>
            ${priceFormatted ? `
            <div class="settings-row">
              <span class="text-muted">Valor da assinatura</span>
              <strong>${priceFormatted}</strong>
            </div>` : ''}
            ${trialRow}
            ${periodEndRow}
          </div>
        </div>
      </div>

      ${canSetup ? `
      <div class="card">
        <div class="card-body" style="padding:1.5rem">
          <h3 style="margin:0 0 0.25rem">Configurar pagamento</h3>
          <p class="text-muted text-sm" style="margin:0 0 1.25rem">
            A assinatura é cobrada mensalmente via PIX ou Cartão de Crédito (AbacatePay).
            Após clicar em continuar, você escolhe a forma de pagamento e é redirecionado.
          </p>
          <button class="btn btn-primary" id="btnSetupBilling">Configurar pagamento →</button>
        </div>
      </div>` : ''}

      ${canRenew ? `
      <div class="card">
        <div class="card-body" style="padding:1.5rem">
          <h3 style="margin:0 0 0.25rem">Renovar assinatura</h3>
          <p class="text-muted text-sm" style="margin:0 0 1.25rem">
            Adiciona mais 30 dias ao vencimento atual. Aceita PIX ou Cartão de Crédito (AbacatePay).
          </p>
          <button class="btn btn-primary" id="btnPayNow">Pagar agora →</button>
        </div>
      </div>` : ''}

      ${canCancel ? `
      <div class="card">
        <div class="card-body" style="padding:1.5rem">
          <h3 style="margin:0 0 0.25rem">Cancelar assinatura</h3>
          <p class="text-muted text-sm" style="margin:0 0 1.25rem">
            O acesso permanece ativo até o fim do período atual já pago. Após essa data os clientes não conseguirão acessar o sistema.
          </p>
          <button class="btn btn-danger btn-sm" id="btnCancelBilling">Cancelar assinatura</button>
        </div>
      </div>` : ''}

    </div>
  `;
}

function openSetupModal(container) {
  createModal({
    id: 'modalSetupBilling',
    title: 'Configurar pagamento',
    body: `
      <p class="text-muted text-sm" style="margin:0 0 1.25rem">Informe os dados para cadastro no sistema de cobrança.</p>
      <div class="form-group">
        <label class="form-label">CPF / CNPJ <span style="color:var(--color-danger)">*</span></label>
        <input class="form-control" id="inputTaxId" placeholder="000.000.000-00 ou 00.000.000/0001-00">
      </div>
      <div class="form-group">
        <label class="form-label">Telefone (WhatsApp)</label>
        <input class="form-control" id="inputPhone" placeholder="(11) 99999-9999">
      </div>
      <div class="form-group" style="margin-top:1rem">
        <label class="form-label">Forma de pagamento</label>
        <div style="display:flex;gap:0.75rem;margin-top:0.4rem">
          <label style="display:flex;align-items:center;gap:0.5rem;cursor:pointer;flex:1;border:1px solid var(--gray-300);border-radius:var(--border-radius);padding:0.65rem 0.9rem;transition:border-color 0.15s" id="labelPix">
            <input type="radio" name="paymentMethod" value="PIX" checked style="accent-color:var(--brand-secondary)">
            🏦 PIX
          </label>
          <label style="display:flex;align-items:center;gap:0.5rem;cursor:pointer;flex:1;border:1px solid var(--gray-300);border-radius:var(--border-radius);padding:0.65rem 0.9rem;transition:border-color 0.15s" id="labelCard">
            <input type="radio" name="paymentMethod" value="CARD" style="accent-color:var(--brand-secondary)">
            💳 Cartão de Crédito
          </label>
        </div>
      </div>
      <div id="setupError" style="display:none;color:var(--color-danger);font-size:0.875rem;margin-top:0.5rem"></div>
    `,
    footer: `
      <button class="btn btn-secondary" onclick="closeModal('modalSetupBilling')">Cancelar</button>
      <button class="btn btn-primary" id="btnConfirmSetup">Continuar para pagamento</button>
    `
  });
  openModal('modalSetupBilling');

  applyTaxIdMask(document.getElementById('inputTaxId'));
  applyPhoneMask(document.getElementById('inputPhone'));

  // Highlight selected method
  document.querySelectorAll('input[name="paymentMethod"]').forEach(radio => {
    radio.addEventListener('change', () => {
      document.getElementById('labelPix').style.borderColor = '';
      document.getElementById('labelCard').style.borderColor = '';
      radio.closest('label').style.borderColor = 'var(--brand-secondary)';
    });
  });
  document.getElementById('labelPix').style.borderColor = 'var(--brand-secondary)';

  document.getElementById('btnConfirmSetup').addEventListener('click', async () => {
    const taxId = document.getElementById('inputTaxId').value.trim();
    const phone = document.getElementById('inputPhone').value.trim();
    const paymentMethod = document.querySelector('input[name="paymentMethod"]:checked')?.value ?? 'PIX';
    const errEl = document.getElementById('setupError');

    if (!taxId) {
      errEl.textContent = 'Informe o CPF ou CNPJ.';
      errEl.style.display = 'block';
      return;
    }
    errEl.style.display = 'none';

    const btn = document.getElementById('btnConfirmSetup');
    btn.disabled = true;
    btn.textContent = 'Aguarde...';

    try {
      const res = await api.post('/billing/setup', { taxId, phone: phone || null, paymentMethod });
      closeModal('modalSetupBilling');
      if (res.url) {
        showToast('Redirecionando para o pagamento...', 'success');
        window.open(res.url, '_blank');
      }
    } catch (e) {
      errEl.textContent = e.message ?? 'Erro ao configurar pagamento.';
      errEl.style.display = 'block';
      btn.disabled = false;
      btn.textContent = 'Continuar para pagamento';
    }
  });
}

function openPayMethodModal(onConfirm) {
  createModal({
    id: 'modalPayMethod',
    title: 'Forma de pagamento',
    body: `
      <div style="display:flex;gap:0.75rem;margin-top:0.25rem">
        <label style="display:flex;align-items:center;gap:0.5rem;cursor:pointer;flex:1;border:1px solid var(--brand-secondary);border-radius:var(--border-radius);padding:0.75rem 0.9rem" id="payLabelPix">
          <input type="radio" name="payMethod" value="PIX" checked style="accent-color:var(--brand-secondary)">
          🏦 PIX
        </label>
        <label style="display:flex;align-items:center;gap:0.5rem;cursor:pointer;flex:1;border:1px solid var(--gray-300);border-radius:var(--border-radius);padding:0.75rem 0.9rem" id="payLabelCard">
          <input type="radio" name="payMethod" value="CARD" style="accent-color:var(--brand-secondary)">
          💳 Cartão de Crédito
        </label>
      </div>
    `,
    footer: `
      <button class="btn btn-secondary" onclick="closeModal('modalPayMethod')">Cancelar</button>
      <button class="btn btn-primary" id="btnConfirmPayMethod">Continuar →</button>
    `
  });
  openModal('modalPayMethod');

  document.querySelectorAll('input[name="payMethod"]').forEach(radio => {
    radio.addEventListener('change', () => {
      document.getElementById('payLabelPix').style.borderColor = '';
      document.getElementById('payLabelCard').style.borderColor = '';
      radio.closest('label').style.borderColor = 'var(--brand-secondary)';
    });
  });

  document.getElementById('btnConfirmPayMethod').addEventListener('click', () => {
    const method = document.querySelector('input[name="payMethod"]:checked')?.value ?? 'PIX';
    closeModal('modalPayMethod');
    onConfirm(method);
  });
}

function attachEvents(container) {
  container.querySelector('#btnSetupBilling')?.addEventListener('click', () => {
    openSetupModal(container);
  });

  container.querySelector('#btnPayNow')?.addEventListener('click', () => {
    openPayMethodModal(async (paymentMethod) => {
      const btn = container.querySelector('#btnPayNow');
      btn.disabled = true;
      btn.textContent = 'Aguarde...';
      try {
        const res = await api.post('/billing/pay', { paymentMethod });
        if (res.url) {
          showToast('Redirecionando para o pagamento...', 'success');
          window.open(res.url, '_blank');
        }
      } catch (e) {
        showToast('Erro: ' + e.message, 'error');
      } finally {
        btn.disabled = false;
        btn.textContent = 'Pagar agora →';
      }
    });
  });

  container.querySelector('#btnCancelBilling')?.addEventListener('click', async () => {
    const confirmed = await confirm('Tem certeza que deseja cancelar a assinatura? O acesso será mantido até o fim do período atual.');
    if (!confirmed) return;

    const btn = container.querySelector('#btnCancelBilling');
    btn.disabled = true;

    try {
      const res = await api.post('/billing/cancel', {});
      showToast('Assinatura cancelada. ' + (res.message ?? ''), 'success');
      await renderBilling(container);
    } catch (e) {
      showToast('Erro: ' + e.message, 'error');
      btn.disabled = false;
    }
  });
}
