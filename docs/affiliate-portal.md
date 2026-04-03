# Portal de Afiliados

## Visão Geral

O portal de afiliados permite que parceiros divulguem o Agendofy e recebam comissões sobre os pagamentos de assinatura dos salões que eles indicarem. O super admin gerencia os afiliados, aprova saques e configura as regras do programa.

**Comissão padrão:** 20% de cada pagamento de assinatura do salão indicado  
**Saque mínimo:** R$10,00 (configurável)  
**Pagamento:** manual — admin aprova no sistema e realiza a transferência externamente  
**Trial para indicados:** 15 dias

---

## Arquitetura

### Backend

| Camada | Arquivos |
|---|---|
| Domínio | `GymApp.Domain/Entities/Affiliate.cs` |
| Enums | `GymApp.Domain/Enums/Enums.cs` |
| Contexto EF | `GymApp.Infra/Data/AppDbContext.cs` |
| Migration | `GymApp.Infra/Data/Migrations/*AddAffiliatePortal*` |
| Endpoints afiliado | `GymApp.Api/Endpoints/AffiliateEndpoints.cs` |
| DTOs | `GymApp.Api/DTOs/AffiliateDtos.cs` |
| Gatilho de comissão | `GymApp.Api/Endpoints/BillingEndpoints.cs` |
| Emails | `GymApp.Infra/Services/ResendEmailService.cs` |
| Interface email | `GymApp.Domain/Interfaces/IEmailService.cs` |

### Frontend

| Arquivo | Responsabilidade |
|---|---|
| `frontend/affiliate/index.html` | SPA do portal do afiliado |
| `frontend/js/affiliate/dashboard.js` | Dashboard com saldo e link |
| `frontend/js/affiliate/referrals.js` | Tabela de indicações |
| `frontend/js/affiliate/commissions.js` | Histórico de comissões |
| `frontend/js/affiliate/withdrawals.js` | Solicitações de saque |
| `frontend/css/affiliate.css` | Estilos do portal |
| `frontend/superadmin/index.html` | Itens de nav adicionados |
| `frontend/js/superadmin/affiliates.js` | Gestão de afiliados (SA) |
| `frontend/js/superadmin/affiliate-withdrawals.js` | Fila de saques (SA) |
| `frontend/js/superadmin/affiliate-config.js` | Configurações (SA) |

---

## Modelo de Dados

### Novas entidades

#### `Affiliate`
Liga um `User` (role `Affiliate`) ao programa de afiliados.

| Campo | Tipo | Descrição |
|---|---|---|
| `Id` | `Guid` | PK |
| `UserId` | `Guid` | FK → `User` (unique) |
| `ReferralCode` | `string(50)` | Código único do afiliado (ex: `joao2024`) |
| `CommissionRate` | `decimal(5,4)` | Taxa individual (0.20 = 20%) |
| `CreatedAt` | `DateTime` | Data de cadastro |

#### `AffiliateReferral`
Registra que um salão foi indicado por um afiliado.

| Campo | Tipo | Descrição |
|---|---|---|
| `Id` | `Guid` | PK |
| `AffiliateId` | `Guid` | FK → `Affiliate` |
| `TenantId` | `Guid` | FK → `Tenant` (unique — cada salão só pode ter um afiliado) |
| `RegisteredAt` | `DateTime` | Data do cadastro do salão |

#### `AffiliateCommission`
Gerada a cada pagamento de assinatura do salão.

| Campo | Tipo | Descrição |
|---|---|---|
| `Id` | `Guid` | PK |
| `AffiliateId` | `Guid` | FK → `Affiliate` |
| `TenantId` | `Guid` | FK → `Tenant` |
| `SubscriptionPaymentRef` | `string(200)` | ID do pagamento no AbacatePay |
| `GrossAmount` | `decimal(10,2)` | Valor bruto do pagamento do salão |
| `Rate` | `decimal(5,4)` | Taxa aplicada no momento do pagamento |
| `CommissionAmount` | `decimal(10,2)` | Valor da comissão (GrossAmount × Rate) |
| `Status` | `enum` | `Pending` / `Paid` |
| `CreatedAt` | `DateTime` | Data da geração |

#### `AffiliateWithdrawalRequest`
Solicitação de saque do afiliado.

| Campo | Tipo | Descrição |
|---|---|---|
| `Id` | `Guid` | PK |
| `AffiliateId` | `Guid` | FK → `Affiliate` |
| `RequestedAmount` | `decimal(10,2)` | Valor solicitado |
| `Status` | `enum` | `Pending` / `Approved` / `Rejected` |
| `AdminNotes` | `string(500)?` | Observação do admin (ex: comprovante, motivo de rejeição) |
| `CreatedAt` | `DateTime` | Data da solicitação |
| `ResolvedAt` | `DateTime?` | Data da resolução |

#### `SystemConfig`
Configurações globais do sistema (chave-valor).

| Campo | Tipo | Descrição |
|---|---|---|
| `Id` | `Guid` | PK |
| `Key` | `string(100)` | Chave única |
| `Value` | `string(500)` | Valor |
| `UpdatedAt` | `DateTime` | Última atualização |

**Chaves utilizadas:**
- `AffiliateMinWithdrawalCents` — saldo mínimo em centavos (default: `1000` = R$10,00)
- `AffiliateDefaultCommissionRate` — taxa padrão para novos afiliados (default: `0.20`)

### Alterações em entidades existentes

#### `User`
- `UserRole` enum: adicionado `Affiliate`

#### `Tenant`
- Campo `AffiliateReferralCode` (`string(50)?`) — código do afiliado que indicou este salão, gravado no cadastro

---

## Fluxos Principais

### 1. Registro de salão via link de afiliado

```
1. Afiliado compartilha: https://agendofy.com/salao.html?ref=CODIGO
2. Frontend lê ?ref= e envia como AffiliateCode no body do signup
3. POST /api/public/signup { ..., affiliateCode: "CODIGO" }
4. Backend valida o código → Affiliate encontrado
5. Tenant criado com AffiliateReferralCode = "CODIGO" e TrialDays = 15
6. AffiliateReferral criado (AffiliateId → TenantId)
```

### 2. Geração de comissão (automática)

Acionada no webhook `POST /api/webhooks/abacatepay` a cada evento `billing.paid`:

```
1. Webhook recebido com billing.paid
2. Tenant identificado pelo billingId ou customerId
3. SubscriptionStatus → Active; período renovado +30 dias
4. Se tenant.AffiliateReferralCode existe:
   a. AffiliateReferral localizado
   b. GrossAmount = tenant.SubscriptionPriceCents / 100
   c. CommissionAmount = GrossAmount × affiliate.CommissionRate
   d. AffiliateCommission criada com Status = Pending
   e. Email enviado ao afiliado (nova comissão + saldo atualizado)
```

### 3. Cálculo do saldo disponível

```
Saldo disponível = Σ(CommissionAmount de todas comissões)
                 - Σ(CommissionAmount de comissões Paid)
                 - Σ(RequestedAmount de saques Pending ou Approved)
```

### 4. Solicitação de saque (afiliado)

```
1. Afiliado acessa Dashboard ou aba Saques
2. Verifica saldo disponível ≥ mínimo configurado
3. POST /api/affiliate/withdrawal { amount: X }
4. Backend valida:
   - Amount ≥ mínimo
   - Amount ≤ saldo disponível
5. AffiliateWithdrawalRequest criada com Status = Pending
```

### 5. Resolução de saque (super admin)

```
1. Super admin acessa Saques → fila de pendentes
2. PATCH /api/admin/affiliates/withdrawals/{id}
   { status: "Approved" | "Rejected", adminNotes: "..." }
3. Se Approved:
   - Comissões Pending são marcadas como Paid (em ordem cronológica,
     até cobrir o valor do saque)
4. AffiliateWithdrawalRequest.Status e ResolvedAt atualizados
5. Email enviado ao afiliado com status e notas
6. Pagamento externo realizado manualmente pelo admin
```

---

## API Reference

### Endpoints do Afiliado (`/api/affiliate/*`)

Todos requerem JWT com role `Affiliate`.

| Método | Rota | Descrição |
|---|---|---|
| `GET` | `/api/affiliate/me` | Perfil + link de indicação |
| `GET` | `/api/affiliate/referrals` | Salões indicados com status e comissão total |
| `GET` | `/api/affiliate/commissions` | Histórico de comissões (paginado: `?page=1&pageSize=50`) |
| `GET` | `/api/affiliate/balance` | Saldo disponível, total ganho, em processamento |
| `POST` | `/api/affiliate/withdrawal` | Solicitar saque `{ amount: decimal }` |
| `GET` | `/api/affiliate/withdrawals` | Histórico de saques |

### Endpoints do Super Admin (`/api/admin/*`)

Todos requerem JWT com role `SuperAdmin`.

| Método | Rota | Descrição |
|---|---|---|
| `GET` | `/api/admin/affiliates` | Lista todos afiliados com saldo |
| `POST` | `/api/admin/affiliates` | Criar afiliado |
| `GET` | `/api/admin/affiliates/{id}` | Detalhe: indicações + comissões + saques |
| `PATCH` | `/api/admin/affiliates/{id}/rate` | Ajustar taxa individual `{ commissionRate: decimal }` |
| `GET` | `/api/admin/affiliates/withdrawals` | Lista saques (`?status=Pending\|Approved\|Rejected`) |
| `PATCH` | `/api/admin/affiliates/withdrawals/{id}` | Aprovar/Rejeitar `{ status, adminNotes? }` |
| `GET` | `/api/admin/config` | Ler configurações do sistema |
| `PATCH` | `/api/admin/config` | Atualizar `{ affiliateMinWithdrawalCents?, affiliateDefaultCommissionRate? }` |

### Endpoint público (signup)

| Método | Rota | Campo adicionado |
|---|---|---|
| `POST` | `/api/public/signup` | `affiliateCode?: string` — código do afiliado |

---

## Auth

O afiliado usa o mesmo `POST /api/auth/login` sem header `X-Tenant-Slug` (igual ao Super Admin). O JWT retornado contém `role = "Affiliate"` e não possui `tenant_id`.

**Criação de conta:** exclusivamente pelo Super Admin via `POST /api/admin/affiliates`. Não há auto-cadastro.

---

## Notificações por Email

Dois novos métodos em `IEmailService` implementados em `ResendEmailService`:

### `SendAffiliateCommissionEarnedAsync`
Disparado automaticamente ao confirmar um pagamento de assinatura.

- **Para:** afiliado
- **Assunto:** `💰 Nova comissão: R$X,XX de [Nome do Salão]`
- **Conteúdo:** nome do salão, valor da comissão, novo saldo disponível

### `SendAffiliateWithdrawalStatusAsync`
Disparado ao aprovar ou rejeitar uma solicitação de saque.

- **Para:** afiliado
- **Assunto:** `✅ Solicitação de saque aprovada — R$X,XX` ou `❌ Solicitação de saque rejeitada — R$X,XX`
- **Conteúdo:** valor, status, observação do admin (se houver)

---

## Frontend

### Portal do Afiliado (`/affiliate/index.html`)

SPA com sidebar + hash routing. Acesso restrito a usuários com role `Affiliate`.

| Seção (hash) | Descrição |
|---|---|
| `#dashboard` | Saldo disponível, total ganho, em processamento, taxa, link de indicação (copiar), formulário de saque |
| `#referrals` | Tabela de salões indicados: nome, slug, status de assinatura, data de cadastro, comissão total gerada |
| `#commissions` | Histórico paginado: data, salão, valor bruto, taxa, comissão, status (Pendente/Pago) |
| `#withdrawals` | Formulário de novo saque + histórico: data, valor, status, data de resolução, observação do admin |

### Portal do Super Admin (`/superadmin/index.html`)

Três novas páginas na seção "Afiliados" do menu lateral:

| Página | Descrição |
|---|---|
| **Afiliados** | Lista com saldo, indicações, taxa. Drill-down por afiliado com indicações, comissões e edição de taxa. Modal para criar novo afiliado. |
| **Saques** | Fila de solicitações com filtro por status. Modal de aprovação/rejeição com campo de observação. |
| **Configurações** | Saldo mínimo para saque (R$) e taxa padrão para novos afiliados (%). |

---

## Configuração e Deploy

Nenhuma variável de ambiente nova necessária. O sistema usa as configurações de email (Resend) e banco de dados já existentes.

A migration `AddAffiliatePortal` é aplicada automaticamente no startup via `db.Database.MigrateAsync()`.

Para criar o primeiro afiliado: fazer login como Super Admin → Portal Super Admin → Afiliados → Novo Afiliado.
