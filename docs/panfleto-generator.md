# Gerador de Panfletos

## Visão Geral

Ferramenta pública em `/panfleto/` que permite criar panfletos de serviços e valores para divulgação em redes sociais (Instagram, WhatsApp, TikTok). O usuário escolhe um template, preenche os dados e exporta em PNG 1080×1080px pronto para publicar.

**Público-alvo:** manicures, salões de beleza, estúdios de estética  
**Acesso:** público — sem necessidade de login  
**Import automático:** usuários do Agendofy têm seus dados preenchidos automaticamente  
**Exportação:** PNG 1080×1080px via `html2canvas`  
**Backend necessário:** nenhum — 100% client-side

---

## Arquitetura

### Frontend

| Arquivo | Responsabilidade |
|---|---|
| `frontend/panfleto/index.html` | Página principal: painel de edição + preview + modal de import |
| `frontend/js/panfleto/editor.js` | Estado, preview ao vivo, importação Agendofy, exportação PNG |
| `frontend/css/panfleto.css` | Layout do editor + estilos dos 6 templates |

### Sem backend próprio

A ferramenta reutiliza endpoints já existentes apenas para o fluxo de importação:

| Endpoint | Uso |
|---|---|
| `POST /api/auth/login` | Autenticar usuário no modal de import manual |
| `GET /api/settings` | Importar nome, logo, cores e redes sociais do tenant |
| `GET /api/class-types` | Importar serviços e preços ativos |

---

## Funcionalidades

### Editor

O painel esquerdo contém todos os campos editáveis:

| Campo | Detalhes |
|---|---|
| Logo | Upload local via `FileReader` → base64. Sem servidor. |
| Nome do negócio | Texto livre, máx. 50 chars |
| Slogan / especialidade | Opcional, máx. 60 chars |
| Telefone / WhatsApp | Máscara automática: `(XX) X XXXX-XXXX` (celular) ou `(XX) XXXX-XXXX` (fixo) |
| Serviços e valores | Lista dinâmica — adicionar / remover / editar inline |
| Instagram | Campo `@handle` — sem URL completa |
| TikTok | Campo `@handle` — sem URL completa |
| Site / catálogo | URL livre ou preenchido automaticamente com `[slug].agendofy.com/catalogo` |

Qualquer alteração re-renderiza o canvas de preview imediatamente (sem debounce — a renderização é DOM puro, sem canvas nativo).

### Templates

Seis templates em pure CSS, sem imagens externas:

| ID | Nome | Paleta | Público-alvo |
|---|---|---|---|
| `rosa-delicado` | Rosa Delicado | Rosa/lilás com círculos decorativos | Manicures, estética |
| `nude-elegante` | Nude Elegante | Bege/nude + dourado itálico | Salão premium |
| `moderno-escuro` | Moderno Escuro | Preto/azul escuro + acento vermelho | Universal |
| `minimalista` | Minimalista | Branco puro + tipografia preta | Todos |
| `verde-natural` | Verde Natural | Verde sage + menta | Spa, bem-estar |
| `vibrante` | Vibrante | Gradiente roxo-pink-laranja | Jovem, criativo |

Cada template é aplicado como classe CSS `tpl--{id}` no elemento `#panfletoCanvas`. Os tokens de cor (texto, preço, divisor) são definidos por seletor, sem JavaScript.

### Importação de dados do Agendofy

**Fluxo silencioso (auto-import):**  
Ao carregar a página, o editor verifica `localStorage` por `access_token` e `tenant_slug`. Se encontrar, chama os endpoints em paralelo e preenche todos os campos sem interação do usuário. O banner CTA muda para confirmação de sucesso.

**Fluxo manual (modal de login):**  
Caso o usuário não esteja logado, clicar em "Importar meus dados" abre um modal com e-mail e senha. O login chama `POST /api/auth/login`, armazena o token em `localStorage` (mesma chave usada pelo painel admin) e executa o import.

**Dados importados:**

| Campo no editor | Origem na API |
|---|---|
| Nome do negócio | `settings.name` |
| Logo | `settings.logoUrl` |
| Instagram | `settings.socialInstagram` (handle extraído via regex) |
| TikTok | `settings.socialTikTok` (handle extraído via regex) |
| Telefone | `settings.socialWhatsApp` + máscara aplicada |
| Site / catálogo | `${settings.slug}.agendofy.com/catalogo` |
| Serviços e preços | `classTypes[]` onde `isActive=true` e `price != null` (máx. 8) |

O helper `extractHandle(value)` normaliza handles — aceita `@handle`, `https://instagram.com/handle` ou só `handle` e retorna sempre o handle limpo.

### Exportação PNG

Usa [html2canvas](https://html2canvas.hertzen.com/) carregado via CDN:

```js
html2canvas(canvas, {
  scale: 1080 / canvas.offsetWidth, // escala para 1080px independente do display
  useCORS: true,
  allowTaint: true,
  backgroundColor: null,
})
```

O arquivo é baixado como `panfleto-{nome-do-negocio}.png`.

**Limitação conhecida:** `html2canvas` pode ter dificuldades com logos carregadas de URLs externas por restrições de CORS. Logos enviadas via upload local (base64) funcionam sempre.

---

## Estrutura do Estado

```js
const state = {
  templateId: 'rosa-delicado', // ID do template selecionado
  logoDataUrl: null,           // base64 ou URL externa
  name: '',
  tagline: '',
  phone: '',                   // já com máscara aplicada
  services: [                  // lista dinâmica, máx. 8 renderizados
    { name: 'Esmaltação simples', price: 'R$ 25' },
  ],
  instagram: '',               // só o handle, sem @
  tiktok: '',                  // só o handle, sem @
  website: '',                 // URL completa ou slug.agendofy.com/catalogo
};
```

---

## Links de Acesso Público

A ferramenta é linkada nos seguintes pontos:

| Página | Local |
|---|---|
| `/funcionalidades.html` | Seção dedicada antes do CTA + link no footer (coluna Produto) |
| `/landing.html` | Footer, coluna Produto |
| `/admin/index.html` | Sidebar, seção Crescimento — abre em nova aba |

---

## Analytics

A página `/panfleto/index.html` inclui Google Analytics (`G-NWZTF6F3M6`) e Microsoft Clarity (`vxxeenq2oz`), os mesmos IDs usados em todo o sistema.

---

## Considerações de Design

- **Preview 1:1** — o canvas é mantido em proporção quadrada com `aspect-ratio: 1/1` e `max-width: 520px`. A exportação escala para 1080px.
- **Máx. 8 serviços no canvas** — a lista do painel permite mais, mas apenas os 8 primeiros são renderizados para evitar overflow no panfleto.
- **Responsivo** — em mobile (`< 768px`) o layout passa de duas colunas para coluna única, com o painel na parte superior e o preview abaixo.
- **Sem dependência de servidor** — todo o fluxo anônimo roda 100% no browser. O import apenas utiliza a API já existente.
