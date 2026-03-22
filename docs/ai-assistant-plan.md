# Plano: Assistente IA para Admin

## Visão Geral
Um chat flutuante no painel admin onde o admin pode fazer perguntas em linguagem natural
sobre alunos, agendamentos, pacotes, sessões — e o assistente pode executar ações na
aplicação via ferramentas (function calling).

**Modelo:** MiMo-V2-Omni (Xiaomi) via API direta
**Endpoint:** `https://api.xiaomimimo.com/v1/chat/completions` (OpenAI-compatible)

---

## Fase 1 — Backend: Endpoint de Chat

### 1.1 — Novo endpoint `POST /admin/ai/chat`
- Recebe `{ message: string, conversationId: string? }`
- O histórico vem do banco, não do cliente
- Autenticado como admin (JWT)
- `conversationId` cria ou continua uma conversa existente

### 1.2 — Ferramentas disponíveis (function calling)

| Ferramenta | Ação |
|---|---|
| `list_students` | Lista alunos com filtros |
| `get_student` | Detalhes de um aluno |
| `list_schedules` | Agenda de aulas |
| `list_sessions` | Sessões com vagas |
| `get_financial_summary` | Resumo financeiro |
| `list_packages` | Pacotes disponíveis |
| `create_booking` | Criar reserva |
| `cancel_booking` | Cancelar reserva |
| `add_student` | Cadastrar aluno |

### 1.3 — Execução de ferramentas
- Fluxo: LLM → tool_call → backend executa → resultado → LLM → resposta final
- Máx 5 rounds por requisição
- Timeout configurável

### 1.4 — System prompt customizável por tenant
- Nova coluna `AiSystemPrompt` (nullable) na tabela `Tenants`
- Se null, usa prompt padrão genérico:
  > "Você é um assistente do administrador de {TenantName}. Ajude com informações sobre
  > clientes, agendamentos e operações do negócio."
- Admin pode editar o prompt nas configurações do tenant
- Suporte a academia, salão de beleza, studio de dança e qualquer outro negócio

---

## Fase 2 — Banco de Dados: Persistência do Chat

### 2.1 — Novas entidades

**AiConversation**
- `Id`, `TenantId`, `AdminUserId`, `CreatedAt`, `UpdatedAt`
- `Title` (gerado automaticamente da primeira mensagem)

**AiMessage**
- `Id`, `ConversationId`, `Role` (user/assistant/tool), `Content`, `ToolName?`,
  `ToolInput?`, `ToolResult?`, `TokensUsed?`, `CreatedAt`

### 2.2 — Endpoints de histórico
- `GET /admin/ai/conversations` — lista conversas do admin logado
- `GET /admin/ai/conversations/{id}` — mensagens de uma conversa

---

## Fase 3 — Integração com MiMo-V2-Omni

### 3.1 — Provider
- API direta da Xiaomi — OpenAI-compatible
- Base URL: `https://api.xiaomimimo.com/v1`
- Endpoint: `POST /chat/completions`
- Variável de ambiente: `XIAOMI_MIMO_API_KEY`

### 3.2 — Configuração
```
XIAOMI_MIMO_API_KEY=...
AI_MODEL=mimo-v2-omni
AI_BASE_URL=https://api.xiaomimimo.com/v1
AI_MAX_TOKENS=2048
AI_MAX_TOOL_ROUNDS=5
```

---

## Fase 4 — Frontend: Widget de Chat

### 4.1 — Componente flutuante
- Botão fixo canto inferior direito, presente em todas as páginas do admin
- Expande para painel lateral sem recarregar a página

### 4.2 — Interface
- Balões de chat com renderização de Markdown
- Indicador de "pensando..." durante aguardo
- Lista de conversas anteriores (sidebar)
- Sugestões rápidas clicáveis na primeira mensagem

### 4.3 — Arquivo JS
- `js/admin/ai-assistant.js` — toda a lógica do assistente

### 4.4 — Configuração do prompt
- Campo textarea nas Configurações do Admin para editar `AiSystemPrompt`
- `PUT /admin/settings/ai-prompt` para salvar

---

## Fase 5 — Segurança e Limites

- Rate limiting: 20 mensagens/min por tenant
- Ferramentas de escrita exigem confirmação explícita no frontend
- Isolamento de tenant garantido pelo middleware já existente
- Logs de auditoria: toda ação via ferramenta registrada em `AiMessage`

---

## Ordem de Implementação

1. Migration: `AiSystemPrompt` em Tenants, tabelas `AiConversations` e `AiMessages`
2. Configurar `XIAOMI_MIMO_API_KEY` no `.env` e cliente HTTP
3. Endpoint `/admin/ai/chat` com system prompt básico, sem ferramentas
4. Testes de integração com MiMo-V2-Omni
5. Adicionar ferramentas de leitura (list_students, list_schedules, etc.)
6. Persistência completa do histórico no banco
7. Widget de chat no frontend
8. Ferramentas de escrita com confirmação
9. Página de configuração do prompt por tenant
10. Rate limiting e auditoria
