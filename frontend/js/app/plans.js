import { api } from '../api.js';
import { showToast, createModal, openModal, closeModal, emptyState } from '../ui.js';
import { t } from '../i18n.js';
import { trackEvent } from '../analytics.js';

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
    title: t('store.payment.title'),
    body: '<div class="loading-center"><span class="spinner"></span></div>',
    footer: `<button class="btn btn-secondary" onclick="closeModal('paymentModal')">${t('btn.close')}</button>`
  });
  openModal('paymentModal');

  let pollInterval = null;

  const stopPolling = () => { if (pollInterval) clearInterval(pollInterval); };

  try {
    const payment = await api.post('/payments/checkout', { packageTemplateId: planId });
    const body = document.querySelector('#paymentModal .modal-body');

    body.innerHTML = `
      <div style="text-align:center">
        <p class="text-sm text-muted" style="margin-bottom:1rem">${t('store.payment.scan')}</p>
        <img src="${payment.qrCodeBase64}" alt="QR Code PIX"
          style="width:220px;height:220px;border:2px solid var(--gray-200);border-radius:8px;display:block;margin:0 auto 1rem">
        <button class="btn btn-secondary" id="btnCopyPix" style="width:100%;margin-bottom:1rem">
          ${t('store.payment.copy')}
        </button>
        <div id="paymentStatus" style="font-size:var(--font-size-sm);color:var(--gray-500)">
          <span class="spinner" style="width:14px;height:14px;margin-right:0.4rem"></span>
          ${t('store.payment.waiting')}
        </div>
      </div>
    `;

    document.getElementById('btnCopyPix').addEventListener('click', async () => {
      try {
        await navigator.clipboard.writeText(payment.pixCopyPaste);
        trackEvent('pix_code_copied', { plan_name: planName });
        showToast(t('store.payment.copied'), 'success');
      } catch {
        // fallback: show the code in an alert
        prompt('Copie o código PIX:', payment.pixCopyPaste);
      }
    });

    // Poll for confirmation every 4 seconds
    pollInterval = setInterval(async () => {
      try {
        const status = await api.get(`/payments/${payment.paymentId}/status`);
        const statusEl = document.getElementById('paymentStatus');

        if (status.status === 'Paid') {
          stopPolling();
          trackEvent('purchase', { plan_name: planName, currency: 'BRL' });
          if (statusEl) statusEl.innerHTML = `<span style="color:var(--color-success);font-weight:600">${t('store.payment.success')}</span>`;
          setTimeout(() => closeModal('paymentModal'), 3000);
        } else if (status.status === 'Expired' || status.status === 'Cancelled') {
          stopPolling();
          if (statusEl) statusEl.innerHTML = `<span style="color:var(--color-danger)">${t('store.payment.expired')}</span>`;
        }
      } catch { /* ignore poll errors */ }
    }, 4000);

    // Stop polling when modal closes
    document.querySelector('#paymentModal .modal-overlay')?.addEventListener('click', stopPolling);
    document.querySelector('#paymentModal .modal-close')?.addEventListener('click', stopPolling);

  } catch (e) {
    const body = document.querySelector('#paymentModal .modal-body');
    if (body) {
      const msg = e.message?.includes('not configured')
        ? t('store.notConfigured')
        : t('store.payment.error') + e.message;
      body.innerHTML = `<p class="text-sm" style="color:var(--color-danger)">${msg}</p>`;
    }
  }
}
