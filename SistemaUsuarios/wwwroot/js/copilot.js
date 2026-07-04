/**
 * copilot.js — AgentTools Copiloto da Proposta
 *
 * Copiloto consultivo com memória por proposta (localStorage).
 * Cada proposta tem seu próprio histórico de conversa — persiste entre sessões.
 */

(function () {
    'use strict';

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    function init() {
        const panel = document.getElementById('copilotPanel');
        if (!panel) return;

        const toggleBtn = document.getElementById('copilotToggle');
        const closeBtn  = document.getElementById('copilotClose');
        const messages  = document.getElementById('copilotMessages');
        const inputEl   = document.getElementById('copilotInput');
        const sendBtn   = document.getElementById('copilotSend');
        const chips     = document.getElementById('cpChips');

        let apiHistory  = [];   // [{role, content}] — enviado à API
        let displayLog  = [];   // [{type, ...}]     — persistido no localStorage
        let isLoading   = false;
        let opened      = false;

        const isEditMode = !!(panel.dataset.propostaId && panel.dataset.propostaId.length > 0);

        /* ─── Abrir / fechar ─────────────────────────────────────────────────── */
        toggleBtn.addEventListener('click', () => {
            panel.classList.add('open');
            toggleBtn.style.display = 'none';
            if (!opened) {
                opened = true;
                const hadHistory = isEditMode && loadState();
                if (!hadHistory) showWelcome();
            }
        });

        closeBtn.addEventListener('click', () => {
            panel.classList.remove('open');
            toggleBtn.style.display = '';
        });

        /* ─── Chips rápidos ──────────────────────────────────────────────────── */
        if (chips) {
            chips.querySelectorAll('button[data-msg]').forEach(btn => {
                btn.addEventListener('click', () => sendMessage(btn.dataset.msg));
            });
        }

        /* ─── Memória por proposta (localStorage) ────────────────────────────── */
        function storageKey() { return 'cp_' + panel.dataset.propostaId; }

        function saveState() {
            if (!isEditMode) return;
            try {
                localStorage.setItem(storageKey(), JSON.stringify({
                    apiHistory: apiHistory.slice(-24),
                    displayLog: displayLog.slice(-50),
                    ts: Date.now()
                }));
            } catch (e) { /* quota exceeded — ignore */ }
        }

        function loadState() {
            try {
                const raw = localStorage.getItem(storageKey());
                if (!raw) return false;
                const state = JSON.parse(raw);
                if (!state.displayLog?.length) return false;

                apiHistory = state.apiHistory || [];
                state.displayLog.forEach(item => renderItem(item));

                // Separador visual entre sessões
                const sep = document.createElement('div');
                sep.className = 'cp-session-sep';
                sep.textContent = '— continuando análise —';
                messages.appendChild(sep);
                scrollBottom();

                return true;
            } catch (e) { return false; }
        }

        /* ─── Boas-vindas (primeira abertura) ───────────────────────────────── */
        function showWelcome() {
            if (isEditMode) {
                const welcome = {
                    type: 'ai',
                    data: {
                        mensagem: 'Analisei a proposta e estou pronto para ajudar. Posso sugerir pontos que vão encantar o cliente, fortalecer a argumentação comercial ou identificar lacunas antes de enviar. Por onde quer começar?',
                        pontosFort: [],
                        gaps: [],
                        proximaPergunta: null
                    }
                };
                renderItem(welcome);
                displayLog.push(welcome);
                apiHistory.push({ role: 'assistant', content: welcome.data.mensagem });
                saveState();
            } else {
                const welcome = {
                    type: 'ai',
                    data: { mensagem: 'Olá! Vou te ajudar a construir essa proposta. Me conte: para onde é a viagem, quando e quem vai?', pontosFort: [], gaps: [], proximaPergunta: null }
                };
                renderItem(welcome);
                apiHistory.push({ role: 'assistant', content: welcome.data.mensagem });
            }
        }

        /* ─── Enviar mensagem ────────────────────────────────────────────────── */
        async function sendMessage(text) {
            text = text.trim();
            if (!text || isLoading) return;

            // Registra e renderiza mensagem do usuário
            const userItem = { type: 'user', text };
            displayLog.push(userItem);
            renderItem(userItem);
            apiHistory.push({ role: 'user', content: text });
            saveState();

            inputEl.value = '';
            inputEl.style.height = '';

            const pid = panel.dataset.propostaId || null;
            const payload = {
                propostaId:  pid || null,
                message:     text,
                history:     apiHistory.slice(-14),
                abaAtual:    getAba(),
                formContext: pid ? null : getFormCtx(),
            };

            const loadingEl = appendLoading();
            setLoading(true);

            try {
                const res = await fetch('/AiCopilot/Chat', {
                    method:  'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body:    JSON.stringify(payload),
                });
                if (!res.ok) throw new Error(`HTTP ${res.status}`);
                const data = await res.json();

                loadingEl.remove();

                // Salva resposta no histórico
                apiHistory.push({ role: 'assistant', content: data.mensagem || '' });

                const aiItem = {
                    type: 'ai',
                    data: {
                        mensagem:       data.mensagem || '',
                        pontosFort:     data.pontosFort || [],
                        gaps:           data.gaps || [],
                        proximaPergunta: data.proximaPergunta || null,
                    }
                };
                displayLog.push(aiItem);
                renderItem(aiItem);
                saveState();

            } catch (e) {
                loadingEl.remove();
                const errItem = { type: 'ai', data: { mensagem: 'Não consegui processar a mensagem. Tente novamente.', pontosFort: [], gaps: [], proximaPergunta: null } };
                renderItem(errItem);
                console.warn('[Copiloto]', e);
            } finally {
                setLoading(false);
            }
        }

        function setLoading(on) {
            isLoading = on;
            sendBtn.disabled = on;
            sendBtn.style.opacity = on ? '.5' : '';
        }

        /* ─── Renderização de itens ──────────────────────────────────────────── */
        function renderItem(item) {
            if (item.type === 'user') {
                const el = document.createElement('div');
                el.className = 'msg-user';
                el.innerHTML = `<div class="msg-bubble">${esc(item.text)}</div>`;
                messages.appendChild(el);
                scrollBottom();
                return;
            }

            if (item.type === 'ai') {
                const d = item.data;

                // Mensagem principal + pontos fortes
                if (d.mensagem || (d.pontosFort && d.pontosFort.length)) {
                    const el = document.createElement('div');
                    el.className = 'msg-ai';
                    let html = '<div class="msg-bubble">';

                    if (d.mensagem) html += `<p style="margin:0 0 6px;">${esc(d.mensagem)}</p>`;

                    if (d.pontosFort && d.pontosFort.length) {
                        html += '<div class="cp-pontos">';
                        d.pontosFort.forEach(p => {
                            html += `<div class="cp-ponto"><i class="fas fa-check-circle"></i>${esc(p)}</div>`;
                        });
                        html += '</div>';
                    }

                    html += '</div>';
                    el.innerHTML = html;
                    messages.appendChild(el);
                }

                // Gaps
                if (d.gaps && d.gaps.length) {
                    const el = document.createElement('div');
                    el.className = 'msg-ai';
                    let html = '<div class="msg-bubble cp-gaps-bubble">';
                    html += '<div class="cp-gaps-title"><i class="fas fa-exclamation-triangle me-1"></i>Oportunidades de melhoria</div>';
                    d.gaps.forEach(g => {
                        html += `<div class="cp-gap"><i class="fas fa-circle"></i>${esc(g)}</div>`;
                    });
                    html += '</div>';
                    el.innerHTML = html;
                    messages.appendChild(el);
                }

                // Pergunta provocativa
                if (d.proximaPergunta) {
                    const el = document.createElement('div');
                    el.className = 'msg-ai';
                    el.innerHTML = `<div class="msg-bubble cp-question"><i class="fas fa-question-circle me-1"></i>${esc(d.proximaPergunta)}</div>`;
                    messages.appendChild(el);
                }

                scrollBottom();
            }
        }

        function appendLoading() {
            const el = document.createElement('div');
            el.className = 'msg-ai';
            el.innerHTML = '<div class="msg-bubble cp-typing"><span></span><span></span><span></span></div>';
            messages.appendChild(el);
            scrollBottom();
            return el;
        }

        /* ─── Helpers ────────────────────────────────────────────────────────── */
        function getAba() {
            const a = document.querySelector('#editarTabs .nav-link.active');
            return a ? (a.getAttribute('href') || '').replace('#tab-', '') || 'dados' : 'dados';
        }

        function getFormCtx() {
            return {
                isNovaProposta:    true,
                titulo:            val('Titulo'),
                dataInicio:        val('DataInicio'),
                dataFim:           val('DataFim'),
                numeroPassageiros: intVal('NumeroPassageiros', 1),
                numeroCriancas:    intVal('NumeroCriancas', 0),
            };
        }

        function val(id)       { return (document.getElementById(id) || {}).value || ''; }
        function intVal(id, d) { return parseInt((document.getElementById(id) || {}).value, 10) || d; }
        function esc(t)        { const d = document.createElement('div'); d.textContent = t; return d.innerHTML; }
        function scrollBottom() { messages.scrollTop = messages.scrollHeight; }

        /* ─── Input ──────────────────────────────────────────────────────────── */
        sendBtn.addEventListener('click', () => sendMessage(inputEl.value));

        inputEl.addEventListener('keydown', e => {
            if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); sendMessage(inputEl.value); }
        });

        inputEl.addEventListener('input', () => {
            inputEl.style.height = 'auto';
            inputEl.style.height = Math.min(inputEl.scrollHeight, 120) + 'px';
        });
    }

})();
