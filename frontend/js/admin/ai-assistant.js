// AI Assistant widget for admin panel
// Uses MiMo-V2-Omni via /admin/ai endpoints

const API_BASE = '';

function getTenantSlug() {
  const host = location.hostname;
  const parts = host.split('.');
  return parts.length >= 3 ? parts[0] : localStorage.getItem('tenant_slug');
}

async function aiRequest(path, options = {}) {
  const headers = { 'Content-Type': 'application/json', ...(options.headers || {}) };
  const token = localStorage.getItem('access_token');
  const slug = getTenantSlug();
  if (token) headers['Authorization'] = `Bearer ${token}`;
  if (slug) headers['X-Tenant-Slug'] = slug;

  const res = await fetch(`${API_BASE}${path}`, { ...options, headers });
  if (!res.ok) throw new Error(`Erro ${res.status}`);
  if (res.status === 204) return null;
  return res.json();
}

// Simple markdown → HTML (bold, italic, lists, line breaks)
function renderMarkdown(text) {
  if (!text) return '';
  return text
    .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
    .replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>')
    .replace(/\*(.+?)\*/g, '<em>$1</em>')
    .replace(/`(.+?)`/g, '<code>$1</code>')
    .replace(/^- (.+)$/gm, '<li>$1</li>')
    .replace(/(<li>.*<\/li>)/s, '<ul>$1</ul>')
    .replace(/\n/g, '<br>');
}

const QUICK_SUGGESTIONS = [
  'Mostre o resumo do negócio de hoje',
  'Liste os serviços cadastrados',
  'Quais clientes estão inativos há mais de 14 dias?',
  'Mostre meu link de indicação',
];

export function initAiAssistant() {
  // Inject widget HTML
  const widget = document.createElement('div');
  widget.id = 'ai-widget';
  widget.innerHTML = `
    <button class="ai-fab" id="aiFab" title="Assistente IA">
      <span class="ai-fab-icon">✦</span>
    </button>
    <div class="ai-panel" id="aiPanel">
      <div class="ai-panel-header">
        <div class="ai-panel-title">
          <span class="ai-title-icon">✦</span>
          <span>Assistente IA</span>
        </div>
        <div class="ai-panel-actions">
          <button class="ai-btn-icon" id="aiNewChat" title="Nova conversa">✕ Nova</button>
          <button class="ai-btn-icon" id="aiClose" title="Fechar">✕</button>
        </div>
      </div>
      <div class="ai-conversations" id="aiConversations"></div>
      <div class="ai-messages" id="aiMessages"></div>
      <div class="ai-suggestions" id="aiSuggestions"></div>
      <div class="ai-input-area">
        <textarea class="ai-input" id="aiInput" placeholder="Pergunte algo..." rows="1"></textarea>
        <button class="ai-send" id="aiSend">➤</button>
      </div>
    </div>
  `;
  document.body.appendChild(widget);

  const fab = document.getElementById('aiFab');
  const panel = document.getElementById('aiPanel');
  const closeBtn = document.getElementById('aiClose');
  const newChatBtn = document.getElementById('aiNewChat');
  const messagesEl = document.getElementById('aiMessages');
  const inputEl = document.getElementById('aiInput');
  const sendBtn = document.getElementById('aiSend');
  const suggestionsEl = document.getElementById('aiSuggestions');
  const conversationsEl = document.getElementById('aiConversations');

  let currentConversationId = null;
  let isOpen = false;
  let isLoading = false;

  function togglePanel() {
    isOpen = !isOpen;
    panel.classList.toggle('open', isOpen);
    fab.classList.toggle('active', isOpen);
    if (isOpen && messagesEl.children.length === 0) {
      showSuggestions();
      loadConversations();
    }
  }

  function showSuggestions() {
    suggestionsEl.innerHTML = QUICK_SUGGESTIONS.map(s =>
      `<button class="ai-suggestion">${s}</button>`
    ).join('');
    suggestionsEl.querySelectorAll('.ai-suggestion').forEach(btn => {
      btn.addEventListener('click', () => {
        inputEl.value = btn.textContent;
        sendMessage();
      });
    });
  }

  function hideSuggestions() {
    suggestionsEl.innerHTML = '';
  }

  async function loadConversations() {
    try {
      const list = await aiRequest('/api/admin/ai/conversations');
      if (!list || list.length === 0) { conversationsEl.innerHTML = ''; return; }
      conversationsEl.innerHTML = `
        <div class="ai-conv-label">Conversas recentes</div>
        ${list.slice(0, 5).map(c =>
          `<div class="ai-conv-item" data-id="${c.id}">${c.title}</div>`
        ).join('')}
      `;
      conversationsEl.querySelectorAll('.ai-conv-item').forEach(item => {
        item.addEventListener('click', () => loadConversation(item.dataset.id));
      });
    } catch (_) {}
  }

  async function loadConversation(id) {
    try {
      const data = await aiRequest(`/api/admin/ai/conversations/${id}`);
      if (!data) return;
      currentConversationId = id;
      messagesEl.innerHTML = '';
      hideSuggestions();
      conversationsEl.innerHTML = '';
      data.messages.forEach(m => appendMessage(m.role, m.content ?? ''));
      scrollToBottom();
    } catch (_) {}
  }

  function appendMessage(role, content) {
    const div = document.createElement('div');
    div.className = `ai-message ai-message-${role}`;
    div.innerHTML = role === 'assistant' ? renderMarkdown(content) : content.replace(/&/g, '&amp;').replace(/</g, '&lt;');
    messagesEl.appendChild(div);
  }

  function appendThinking() {
    const div = document.createElement('div');
    div.className = 'ai-message ai-message-assistant ai-thinking';
    div.id = 'aiThinking';
    div.innerHTML = '<span class="ai-dots"><span>.</span><span>.</span><span>.</span></span>';
    messagesEl.appendChild(div);
    scrollToBottom();
  }

  function removeThinking() {
    document.getElementById('aiThinking')?.remove();
  }

  function scrollToBottom() {
    messagesEl.scrollTop = messagesEl.scrollHeight;
  }

  async function sendMessage() {
    const message = inputEl.value.trim();
    if (!message || isLoading) return;

    isLoading = true;
    sendBtn.disabled = true;
    inputEl.value = '';
    inputEl.style.height = 'auto';
    hideSuggestions();
    conversationsEl.innerHTML = '';

    appendMessage('user', message);
    appendThinking();
    scrollToBottom();

    try {
      const data = await aiRequest('/api/admin/ai/chat', {
        method: 'POST',
        body: JSON.stringify({ message, conversationId: currentConversationId || undefined })
      });
      removeThinking();
      if (data) {
        currentConversationId = data.conversationId;
        appendMessage('assistant', data.message);
        scrollToBottom();
      }
    } catch (e) {
      removeThinking();
      appendMessage('assistant', `⚠️ Erro ao processar sua mensagem. Tente novamente.`);
    } finally {
      isLoading = false;
      sendBtn.disabled = false;
      inputEl.focus();
    }
  }

  function startNewChat() {
    currentConversationId = null;
    messagesEl.innerHTML = '';
    inputEl.value = '';
    showSuggestions();
    loadConversations();
  }

  // Event listeners
  fab.addEventListener('click', togglePanel);
  closeBtn.addEventListener('click', togglePanel);
  newChatBtn.addEventListener('click', startNewChat);
  sendBtn.addEventListener('click', sendMessage);

  inputEl.addEventListener('keydown', e => {
    if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); sendMessage(); }
  });

  // Auto-resize textarea
  inputEl.addEventListener('input', () => {
    inputEl.style.height = 'auto';
    inputEl.style.height = Math.min(inputEl.scrollHeight, 120) + 'px';
  });
}
