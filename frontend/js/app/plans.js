import { api } from '../api.js?v=202603311200';
import { showToast, createModal, openModal, closeModal, emptyState } from '../ui.js?v=202603311200';
import { t } from '../i18n.js?v=202603311200';
import { trackEvent } from '../analytics.js?v=202603311200';

export async function renderPlans(container) {
  container.innerHTML = '<div class="loading-center"><span class="spinner"></span></div>';

  try {
    const plans = await api.get('/store/plans');

    if (!plans.length) {
      container.innerHTML = emptyState('🛒', t('store.none'));
      return;
    }

    trackEvent('store_view', { plans_count: plans.length });

    container.innerHTML = plans.map(p => {
      const items = p.items.map(i => `
        <div style="display:flex;align-items:center;gap:0.5rem;font-size:var(--font-size-sm)">
          <span style="width:10px;height:10px;border-radius:2px;background:${i.classTypeColor};flex-shrink:0;display:inline-block"></span>
          <span>${i.classTypeName}</span>
          <span class="text-muted">· ${i.totalCredits} ${t('store.credits')} · R$ ${Number(i.pricePerCredit).toFixed(2)}/aula</span>
        </div>
      `).join('');

      return `
        <div class="package-card" style="margin-bottom:1rem">
          <div class="package-header">
            <div>
              <div class="package-name">${p.name}</div>
              <div class="text-sm text-muted" style="margin-top:0.2rem">
                ${p.durationDays ? `${p.durationDays} ${t('store.duration')}` : t('store.noDuration')}
              </div>
            </div>
            <div style="text-align:right">
              <div style="font-size:1.25rem;font-weight:700;color:var(--brand-primary)">
                R$ ${Number(p.totalPrice).toFixed(2)}
              </div>
            </div>
          </div>
          <div style="padding:0.75rem 1.25rem;border-top:1px solid var(--gray-100);display:flex;flex-direction:column;gap:0.4rem">
            ${items}
          </div>
          <div style="padding:0.75rem 1.25rem;border-top:1px solid var(--gray-100)">
            <button class="btn btn-primary" style="width:100%" data-plan-id="${p.id}" data-plan-name="${p.name}">
              ${t('store.buy')} — R$ ${Number(p.totalPrice).toFixed(2)}
            </button>
          </div>
        </div>
      `;
    }).join('');

    container.querySelectorAll('[data-plan-id]').forEach(btn => {
      btn.addEventListener('click', () => openPaymentModal(btn.dataset.planId, btn.dataset.planName));
    });
  } catch (e) {
    container.innerHTML = `<div class="empty-state"><div class="empty-state-text">${t('error.prefix')}${e.message}</div></div>`;
  }
}

async function openPaymentModal(planId, planName) {
  trackEvent('checkout_initiated', { plan_name: planName });

  createModal({
    id: 'paymentModal',
    title: t('store.payment.chooseMethod'),
    body: `
      <div style="display:flex;flex-direction:column;gap:0.75rem;padding:0.25rem 0">
        <button class="btn btn-primary" id="payByPix" style="display:flex;align-items:center;justify-content:center;gap:0.6rem;font-size:1rem;padding:0.85rem">
          <span style="font-size:1.2rem">🏦</span> ${t('store.payment.pix')}
        </button>
        <button class="btn btn-secondary" id="payByCard" style="display:flex;align-items:center;justify-content:center;gap:0.6rem;font-size:1rem;padding:0.85rem">
          <span style="font-size:1.2rem">💳</span> ${t('store.payment.creditCard')}
        </button>
      </div>
    `,
    footer: `<button class="btn btn-secondary" onclick="closeModal('paymentModal')">${t('btn.close')}</button>`
  });
  openModal('paymentModal');

  document.getElementById('payByPix')?.addEventListener('click', () => startCheckout(planId, planName, 'PIX'));
  document.getElementById('payByCard')?.addEventListener('click', () => startCheckout(planId, planName, 'CARD'));
}

async function startCheckout(planId, planName, paymentMethod) {
  const isCreditCard = paymentMethod === 'CARD';
  const modalTitle = document.querySelector('#paymentModal .modal-title');
  if (modalTitle) {
    modalTitle.textContent = isCreditCard ? t('store.payment.title.creditCard') : t('store.payment.title');
  }

  const body = document.querySelector('#paymentModal .modal-body');
  if (body) body.innerHTML = '<div class="loading-center"><span class="spinner"></span></div>';

  let pollInterval = null;
  const stopPolling = () => { if (pollInterval) clearInterval(pollInterval); };

  try {
    const payment = await api.post('/payments/checkout', { packageTemplateId: planId, paymentMethod });

    // Open the AbacatePay payment page in a new tab
    window.open(payment.billingUrl, '_blank');

    const redirectedMsg = isCreditCard ? t('store.payment.redirected.creditCard') : t('store.payment.redirected');

    body.innerHTML = `
      <div style="text-align:center;padding:0.5rem 0">
        <p style="margin-bottom:1rem">${redirectedMsg}</p>
        <a href="${payment.billingUrl}" target="_blank" class="btn btn-primary" style="margin-bottom:1.25rem">
          ${t('store.payment.openLink')}
        </a>
        <div id="paymentStatus" style="font-size:var(--font-size-sm);color:var(--gray-500);margin-top:0.5rem">
          <span class="spinner" style="width:14px;height:14px;margin-right:0.4rem"></span>
          ${t('store.payment.waiting')}
        </div>
      </div>
    `;

    // Poll for confirmation every 5 seconds
    pollInterval = setInterval(async () => {
      try {
        const status = await api.get(`/payments/${payment.paymentId}/status`);
        const statusEl = document.getElementById('paymentStatus');

        if (status.status === 'Paid') {
          stopPolling();
          trackEvent('purchase', { plan_name: planName, currency: 'BRL', payment_method: paymentMethod });
          if (statusEl) statusEl.innerHTML = `<span style="color:var(--color-success);font-weight:600">${t('store.payment.success')}</span>`;
          setTimeout(() => closeModal('paymentModal'), 3000);
        } else if (status.status === 'Expired' || status.status === 'Cancelled') {
          stopPolling();
          if (statusEl) statusEl.innerHTML = `<span style="color:var(--color-danger)">${t('store.payment.expired')}</span>`;
        }
      } catch { /* ignore poll errors */ }
    }, 5000);

    document.querySelector('#paymentModal .modal-overlay')?.addEventListener('click', stopPolling);
    document.querySelector('#paymentModal .modal-close')?.addEventListener('click', stopPolling);

  } catch (e) {
    if (body) {
      const msg = e.message?.includes('not configured')
        ? t('store.notConfigured')
        : t('store.payment.error') + e.message;
      body.innerHTML = `<p class="text-sm" style="color:var(--color-danger)">${msg}</p>`;
    }
  }
}
