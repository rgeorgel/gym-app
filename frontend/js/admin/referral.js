import { api } from '../api.js';
import { showToast } from '../ui.js';

export async function renderReferral(container) {
  container.innerHTML = `<div class="loading-center"><span class="spinner"></span></div>`;

  let referral;
  try {
    referral = await api.get('/referral/stats');
  } catch (e) {
    container.innerHTML = `<div class="empty-state"><div class="empty-state-text">Erro: ${e.message}</div></div>`;
    return;
  }

  const referralUrl = buildReferralUrl(referral.referralCode);

  container.innerHTML = `
    <div style="max-width:680px;display:flex;flex-direction:column;gap:1.25rem">

      <div class="card">
        <div class="card-body" style="padding:1.5rem">
          <h3 style="margin:0 0 0.25rem">Como funciona</h3>
          <p class="text-muted text-sm" style="margin:0 0 1.25rem">
            Indique o Agendofy para outros negócios e ganhe recompensas automáticas:
          </p>
          <div style="display:grid;grid-template-columns:1fr 1fr;gap:1rem">
            <div style="background:var(--gray-50);border-radius:var(--border-radius);padding:1rem;border:1px solid var(--border)">
              <div style="font-size:1.5rem;margin-bottom:0.5rem">🎁</div>
              <div style="font-weight:600;margin-bottom:0.25rem">Para quem você indica</div>
              <div class="text-sm text-muted">Ganha 30 dias extras de trial (44 dias no total, em vez de 14)</div>
            </div>
            <div style="background:var(--gray-50);border-radius:var(--border-radius);padding:1rem;border:1px solid var(--border)">
              <div style="font-size:1.5rem;margin-bottom:0.5rem">⭐</div>
              <div style="font-weight:600;margin-bottom:0.25rem">Para você</div>
              <div class="text-sm text-muted">Ganha +30 dias na sua assinatura quando o indicado pagar o primeiro mês</div>
            </div>
          </div>
        </div>
      </div>

      <div class="card">
        <div class="card-body" style="padding:1.5rem">
          <h3 style="margin:0 0 0.25rem">Seu link de indicação</h3>
          <p class="text-muted text-sm" style="margin:0 0 1rem">
            Compartilhe este link com donos de salão ou academia. O bônus é aplicado automaticamente quando eles criarem a conta.
          </p>
          <div style="display:flex;gap:0.5rem;align-items:center">
            <input class="form-control" id="inputReferralUrl" value="${referralUrl}" readonly
              style="font-size:0.85rem;flex:1;font-family:monospace;background:var(--gray-50)">
            <button class="btn btn-primary btn-sm" id="btnCopyReferral" style="white-space:nowrap">Copiar link</button>
          </div>
          <p class="text-sm text-muted" style="margin-top:0.35rem">
            Seu código: <strong>${referral.referralCode}</strong>
          </p>
        </div>
      </div>

      <div class="card">
        <div class="card-body" style="padding:1.5rem">
          <h3 style="margin:0 0 1rem">Suas indicações</h3>
          <div style="display:flex;gap:2rem">
            <div style="text-align:center">
              <div style="font-size:2.5rem;font-weight:800;color:var(--color-primary);line-height:1">${referral.totalReferrals}</div>
              <div class="text-sm text-muted" style="margin-top:0.25rem">indicações feitas</div>
            </div>
            <div style="text-align:center">
              <div style="font-size:2.5rem;font-weight:800;color:var(--color-success);line-height:1">${referral.convertedReferrals}</div>
              <div class="text-sm text-muted" style="margin-top:0.25rem">convertidas</div>
              <div class="text-sm text-muted">(bônus recebido)</div>
            </div>
            <div style="text-align:center">
              <div style="font-size:2.5rem;font-weight:800;color:var(--gray-400);line-height:1">${referral.totalReferrals - referral.convertedReferrals}</div>
              <div class="text-sm text-muted" style="margin-top:0.25rem">em trial</div>
              <div class="text-sm text-muted">(aguardando pagamento)</div>
            </div>
          </div>
        </div>
      </div>

    </div>
  `;

  document.getElementById('btnCopyReferral').addEventListener('click', () => {
    navigator.clipboard.writeText(referralUrl).then(() => {
      const btn = document.getElementById('btnCopyReferral');
      btn.textContent = 'Copiado!';
      setTimeout(() => { btn.textContent = 'Copiar link'; }, 2000);
    });
  });
}

function buildReferralUrl(referralCode) {
  const host = location.hostname;
  const parts = host.split('.');
  const baseDomain = parts.length >= 3
    ? `${location.protocol}//${parts.slice(1).join('.')}`
    : `${location.protocol}//${host}`;
  return `${baseDomain}/landing-salao.html?ref=${referralCode}`;
}
