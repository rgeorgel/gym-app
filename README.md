# GymApp

Plataforma SaaS multi-tenant para gestão de academias e agendamento de aulas. Cada academia opera em sua própria subdomain ou domínio personalizado, com identidade visual (cores, logo) própria.

---

## Funcionalidades

### Para o aluno
- Login e auto-cadastro via link da academia
- Visualização da grade de aulas da semana
- Agendamento e cancelamento de aulas
- Acompanhamento de pacotes e créditos disponíveis
- Redefinição de senha por e-mail (self-service)

### Para o admin da academia
- Dashboard com KPIs: ocupação, receita, alunos ativos, churn
- Gestão de alunos (cadastro, status, pacotes, histórico de aulas)
- Geração de link de acesso para alunos
- Gestão de tipos de aula (modalidades), grade de horários e sessões
- Check-in de alunos nas sessões
- Relatórios de frequência e receita

### Para o Super Admin
- Gestão de academias (tenants): criar, editar, ativar/desativar
- Configuração de cores, logo, domínio personalizado por academia
- Criação do usuário admin de cada academia

---

## Tecnologias

| Camada | Tecnologia |
|---|---|
| Backend | .NET 10, ASP.NET Core Minimal API |
| Banco de dados | PostgreSQL 16, EF Core 10 + Npgsql |
| Autenticação | JWT Bearer + Refresh Token |
| Hashing de senha | BCrypt.Net-Next |
| E-mail | SendGrid ou Resend (configurável) |
| Frontend | HTML5 + CSS3 + Vanilla JS (ES Modules) |
| Infraestrutura | Docker + Docker Compose, Nginx |

---

## Estrutura do projeto

```
gym-app/
├── docker-compose.yml
├── .env.example
├── backend/
│   ├── GymApp.Domain/        # Entidades e interfaces (sem dependências externas)
│   ├── GymApp.Infra/         # EF Core, migrations, serviços (email, tenant)
│   └── GymApp.Api/           # Minimal API: endpoints, middleware, DTOs
└── frontend/
    ├── index.html            # Tela de login / cadastro
    ├── reset-password.html   # Redefinição de senha
    ├── admin/                # Painel do admin (SPA)
    ├── app/                  # App do aluno (SPA)
    ├── css/                  # Estilos globais e variáveis CSS
    └── js/
        ├── api.js            # Cliente HTTP com JWT e tenant header
        ├── auth.js           # Login, cadastro, redirect por role
        ├── tenant.js         # Carrega tema e cores do tenant
        ├── ui.js             # Modais, toasts, utilitários
        ├── admin/            # Módulos do painel admin
        └── app/              # Módulos do app do aluno
```

---

## Configuração do servidor

### Pré-requisitos
- Docker e Docker Compose

### 1. Variáveis de ambiente

```bash
cp .env.example .env
```

Edite o `.env` com os valores da sua instalação:

```env
# Banco de dados
POSTGRES_DB=gymapp
POSTGRES_USER=gymapp
POSTGRES_PASSWORD=senha_segura

# JWT (mínimo 32 caracteres em produção)
JWT_SECRET=TROQUE_POR_UMA_CHAVE_SEGURA_32_CHARS

# URL do frontend (usada nos links de e-mail)
FRONTEND_URL=https://seudominio.com

# Provider de e-mail: SendGrid | Resend
EMAIL_PROVIDER=Resend

# SendGrid (se EMAIL_PROVIDER=SendGrid)
SENDGRID_API_KEY=SG.xxxx
SENDGRID_FROM_EMAIL=noreply@seudominio.com
SENDGRID_FROM_NAME=Gym App

# Resend (se EMAIL_PROVIDER=Resend)
RESEND_API_KEY=re_xxxx
RESEND_FROM_EMAIL=noreply@seudominio.com
RESEND_FROM_NAME=Gym App
```

### 2. Subir os containers

```bash
docker compose up --build -d
```

A aplicação sobe em `http://localhost` (porta 80 por padrão, configurável via `WEB_PORT`).

As migrations e o seed inicial são aplicados automaticamente na primeira execução.

### 3. Credenciais iniciais (seed de desenvolvimento)

| Usuário | E-mail | Senha | Role |
|---|---|---|---|
| Super Admin | admin@gymapp.com | admin123 | SuperAdmin |
| Admin Boxe Elite | admin@boxe-elite.com | admin123 | Admin |
| Aluno demo | joao@example.com | aluno123 | Student |

> **Altere essas senhas antes de ir para produção.**

---

## Desenvolvimento local (sem Docker)

```bash
# Backend
cd backend
dotnet run --project GymApp.Api
# API disponível em http://localhost:5000

# Frontend
# Sirva a pasta frontend/ com qualquer servidor estático (ex: Live Server no VS Code)
```

Em desenvolvimento local, o frontend não tem subdomínio, então o tenant é resolvido via header. Configure o header `X-Tenant-Slug: boxe-elite` nas requisições, ou acesse com `?slug=boxe-elite` na URL de login.

---

## Multi-tenancy

O tenant é resolvido automaticamente a cada requisição, em ordem de prioridade:

1. **Domínio personalizado** — `app.boxeelite.com.br` → tenant com `CustomDomain = "app.boxeelite.com.br"`
2. **Subdomínio** — `boxe-elite.gymapp.com.br` → tenant com `Slug = "boxe-elite"`
3. **Header** — `X-Tenant-Slug: boxe-elite` (usado em desenvolvimento local)

---

## Como configurar um novo tenant

### 1. Criar a academia pelo painel Super Admin

Acesse como Super Admin → menu **Academias** → **+ Nova Academia**.

Preencha:
- **Nome** da academia
- **Slug** — identificador único usado na URL (ex: `boxe-elite` → `boxe-elite.seudominio.com`)
- **Cor primária** e **cor secundária** (hex) — aplicadas automaticamente no tema do aluno
- **Logo URL** — link público para a imagem de logo
- **Plano** — Basic / Pro / Enterprise
- **Nome, e-mail e senha** do primeiro admin da academia

### 2. Configurar DNS (subdomínio padrão)

No seu provedor DNS, adicione um registro **CNAME** ou **A** apontando o subdomínio para o servidor:

```
boxe-elite.seudominio.com  →  IP ou CNAME do servidor
```

O sistema resolve o tenant automaticamente pelo subdomínio.

### 3. Configurar domínio personalizado (opcional)

Se a academia quiser usar o próprio domínio (ex: `app.boxeelite.com.br`):

1. O cliente aponta o DNS do domínio dele para o seu servidor:
   ```
   app.boxeelite.com.br  →  A  →  IP do servidor
   ```
2. No painel Super Admin → **Academias** → editar a academia → campo **Domínio personalizado** → preencha `app.boxeelite.com.br`.
3. Configure o certificado SSL para esse domínio no seu servidor/proxy (ex: Nginx + Certbot).

A partir daí, acessos a `app.boxeelite.com.br` são resolvidos para a academia correta com identidade visual própria.

### 4. Cores e tema

As cores são aplicadas via variáveis CSS injetadas dinamicamente pelo frontend ao carregar o tenant (`tenant.js`). Não é necessário nenhum rebuild — basta editar no painel e recarregar a página.

---

## Fluxo de acesso do aluno

```
Aluno acessa boxe-elite.seudominio.com
  → tenant resolvido pelo subdomínio
  → logo e cores da academia carregados
  → login ou auto-cadastro (se habilitado pelo admin)
  → acesso ao app com pacotes e grade de aulas
```

### Cadastro pelo admin
Admin cria o aluno com uma senha inicial → aluno já consegue fazer login.

O admin também pode gerar um **link de acesso** (válido 48h) e enviar ao aluno via WhatsApp ou e-mail, sem precisar informar a senha.

### Auto-cadastro
Se o tenant estiver resolvido (acesso via subdomínio ou domínio personalizado), o link **Cadastre-se** aparece na tela de login e o aluno pode criar a própria conta.

### Esqueci minha senha
O aluno acessa o link **Esqueci minha senha** na tela de login, informa o e-mail e recebe um link de redefinição por e-mail (válido 2 horas).
