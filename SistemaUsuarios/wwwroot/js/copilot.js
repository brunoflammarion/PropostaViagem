/**
 * copilot.js — AgentTools Copiloto da Proposta
 *
 * Copiloto consultivo com memória por proposta (localStorage).
 * Suporta upload de documentos para importação via IA.
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

        const toggleBtn    = document.getElementById('copilotToggle');
        const closeBtn     = document.getElementById('copilotClose');
        const messages     = document.getElementById('copilotMessages');
        const inputEl      = document.getElementById('copilotInput');
        const sendBtn      = document.getElementById('copilotSend');
        const chips        = document.getElementById('cpChips');
        const attachBtn    = document.getElementById('cpAttachBtn');
        const fileInput    = document.getElementById('cpFileInput');
        const attachments  = document.getElementById('cpAttachments');

        let apiHistory      = [];
        let displayLog      = [];
        let isLoading       = false;
        let opened          = false;
        let attachedFiles   = [];       // files waiting to be sent
        let pendingPreview  = null;     // last import analysis preview (for confirmation)

        const isEditMode = !!(panel.dataset.propostaId && panel.dataset.propostaId.length > 0);
        const propostaId = panel.dataset.propostaId || null;

        // ── Check for pending import from "Criar com IA" flow ────────────────
        let pendingAutoImport = null;
        const urlParams = new URLSearchParams(window.location.search);
        if (urlParams.get('pendingImport') === '1' && isEditMode) {
            try {
                const stored = sessionStorage.getItem('pendingImport_' + propostaId);
                if (stored) {
                    pendingAutoImport = JSON.parse(stored);
                    sessionStorage.removeItem('pendingImport_' + propostaId);
                }
            } catch (e) { /* ignore */ }
        }

        // ── Abrir / fechar ───────────────────────────────────────────────────
        function openPanel() {
            panel.classList.add('open');
            toggleBtn.style.display = 'none';
            if (!opened) {
                opened = true;
                if (pendingAutoImport) {
                    renderImportAnalysis(pendingAutoImport);
                    pendingAutoImport = null;
                } else {
                    const hadHistory = isEditMode && loadState();
                    if (!hadHistory) showWelcome();
                }
            }
        }

        toggleBtn.addEventListener('click', openPanel);

        closeBtn.addEventListener('click', () => {
            panel.classList.remove('open');
            toggleBtn.style.display = '';
        });

        // Auto-open if pending import
        if (pendingAutoImport) {
            setTimeout(openPanel, 400);
        }

        // ── Chips rápidos ────────────────────────────────────────────────────
        if (chips) {
            chips.querySelectorAll('button[data-msg]').forEach(btn => {
                btn.addEventListener('click', () => sendMessage(btn.dataset.msg));
            });
        }

        // ── File attachment ──────────────────────────────────────────────────
        if (attachBtn && fileInput) {
            attachBtn.addEventListener('click', () => fileInput.click());

            fileInput.addEventListener('change', () => {
                Array.from(fileInput.files).forEach(f => {
                    if (attachedFiles.length >= 5) return;
                    if (!attachedFiles.find(x => x.name === f.name && x.size === f.size))
                        attachedFiles.push(f);
                });
                fileInput.value = '';
                renderAttachments();
            });
        }

        function renderAttachments() {
            if (!attachments) return;
            if (!attachedFiles.length) { attachments.innerHTML = ''; return; }
            attachments.innerHTML = attachedFiles.map((f, i) => {
                const ext = f.name.split('.').pop().toLowerCase();
                const icon = { pdf: '📄', jpg: '🖼', jpeg: '🖼', png: '🖼', gif: '🖼',
                               webp: '🖼', docx: '📝', doc: '📝', txt: '📃',
                               eml: '📧', html: '🌐' }[ext] || '📎';
                return `<div class="cp-attach-chip">
                    <span>${icon}</span>
                    <span class="chip-name">${esc(f.name)}</span>
                    <button onclick="removeAttach(${i})" title="Remover">✕</button>
                </div>`;
            }).join('');
        }

        window.removeAttach = function (idx) {
            attachedFiles.splice(idx, 1);
            renderAttachments();
        };

        // ── Memória por proposta (localStorage) ─────────────────────────────
        function storageKey() { return 'cp_' + propostaId; }

        function saveState() {
            if (!isEditMode) return;
            try {
                localStorage.setItem(storageKey(), JSON.stringify({
                    apiHistory: apiHistory.slice(-24),
                    displayLog: displayLog.slice(-50),
                    ts: Date.now()
                }));
            } catch (e) { /* quota exceeded */ }
        }

        function loadState() {
            try {
                const raw = localStorage.getItem(storageKey());
                if (!raw) return false;
                const state = JSON.parse(raw);
                if (!state.displayLog?.length) return false;

                apiHistory = state.apiHistory || [];
                state.displayLog.forEach(item => renderItem(item));

                const sep = document.createElement('div');
                sep.className = 'cp-session-sep';
                sep.textContent = '— continuando análise —';
                messages.appendChild(sep);
                scrollBottom();
                return true;
            } catch (e) { return false; }
        }

        // ── Boas-vindas ──────────────────────────────────────────────────────
        function showWelcome() {
            if (isEditMode) {
                const welcome = {
                    type: 'ai',
                    data: {
                        mensagem: 'Analisei a proposta e estou pronto para ajudar. Posso sugerir pontos que vão encantar o cliente, fortalecer a argumentação comercial ou identificar lacunas antes de enviar.\n\nVocê também pode usar o 📎 para enviar um documento e eu identifico e adiciono os itens nesta proposta.',
                        pontosFort: [], gaps: [], proximaPergunta: null
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

        // ── Enviar ───────────────────────────────────────────────────────────
        async function sendMessage(text) {
            text = (text || '').trim();

            // If files attached → run import analysis
            if (attachedFiles.length > 0) {
                await sendWithFiles(text);
                return;
            }

            if (!text || isLoading) return;

            const userItem = { type: 'user', text };
            displayLog.push(userItem);
            renderItem(userItem);
            apiHistory.push({ role: 'user', content: text });
            saveState();

            inputEl.value = '';
            inputEl.style.height = '';

            // Check if user is confirming a pending import
            if (pendingPreview) {
                const lower = text.toLowerCase();
                const confirming = /\b(sim|yes|ok|pode|confirm|importa|add|adiciona|vamos)\b/.test(lower);
                const canceling  = /\b(n[aã]o|no|cancel|par[ae]|aguar)\b/.test(lower);

                if (confirming) {
                    await confirmarImportacao();
                    return;
                }
                if (canceling) {
                    pendingPreview = null;
                    const item = { type: 'ai', data: { mensagem: 'Tudo bem! Não vou importar os itens. Se mudar de ideia, é só anexar o documento novamente.', pontosFort: [], gaps: [], proximaPergunta: null } };
                    renderItem(item);
                    return;
                }
            }

            const payload = {
                propostaId:  propostaId,
                message:     text,
                history:     apiHistory.slice(-14),
                abaAtual:    getAba(),
                formContext: propostaId ? null : getFormCtx(),
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
                apiHistory.push({ role: 'assistant', content: data.mensagem || '' });

                const aiItem = { type: 'ai', data: { mensagem: data.mensagem || '', pontosFort: data.pontosFort || [], gaps: data.gaps || [], proximaPergunta: data.proximaPergunta || null } };
                displayLog.push(aiItem);
                renderItem(aiItem);
                saveState();
            } catch (e) {
                loadingEl.remove();
                renderItem({ type: 'ai', data: { mensagem: 'Não consegui processar a mensagem. Tente novamente.', pontosFort: [], gaps: [], proximaPergunta: null } });
                console.warn('[Copiloto]', e);
            } finally {
                setLoading(false);
            }
        }

        // ── Envio com arquivos → análise de importação ───────────────────────
        async function sendWithFiles(extraText) {
            if (isLoading) return;

            const files = [...attachedFiles];
            const nomes = files.map(f => f.name).join(', ');
            const userText = extraText || `Analisar: ${nomes}`;

            renderItem({ type: 'user', text: userText + '\n📎 ' + nomes });

            // Limpar anexos
            attachedFiles = [];
            renderAttachments();
            inputEl.value = '';
            inputEl.style.height = '';

            const loadingEl = appendLoading();
            setLoading(true);

            try {
                const formData = new FormData();
                formData.append('propostaId', propostaId || '');
                files.forEach(f => formData.append('arquivos', f));

                const res = await fetch('/AiCopilot/AnalisarArquivos', { method: 'POST', body: formData });
                if (!res.ok) {
                    const err = await res.json().catch(() => ({}));
                    throw new Error(err.erro || `Erro ${res.status}`);
                }
                const data = await res.json();
                loadingEl.remove();

                if (data.tipo === 'analise_documentos') {
                    renderImportAnalysis(data);
                } else {
                    renderItem({ type: 'ai', data: { mensagem: data.mensagem || 'Análise concluída.', pontosFort: [], gaps: [], proximaPergunta: null } });
                }
            } catch (e) {
                loadingEl.remove();
                renderItem({ type: 'ai', data: { mensagem: `Erro ao analisar arquivos: ${e.message}`, pontosFort: [], gaps: [], proximaPergunta: null } });
                console.warn('[Copiloto]', e);
            } finally {
                setLoading(false);
            }
        }

        // ── Renderizar análise de importação ─────────────────────────────────
        function renderImportAnalysis(data) {
            pendingPreview = data.preview || data; // store for later confirmation

            const preview = data.preview || data;
            const counts = buildCounts(preview);

            const el = document.createElement('div');
            el.className = 'msg-ai';

            const resumo = data.resumo || buildResumoText(preview);
            const temItens = data.temItens !== false && counts.length > 0;

            let html = '<div class="msg-bubble">'
                + `<p style="margin:0 0 8px">${esc(resumo)}</p>`;

            if (temItens && counts.length) {
                html += '<div class="cp-import-counts">';
                counts.forEach(c => {
                    html += `<span class="badge bg-primary">${c}</span>`;
                });
                html += '</div>';
                html += '<div class="cp-import-actions">'
                    + '<button class="btn btn-sm btn-success" onclick="cpConfirmarImport()" id="btnCpConfirmar"><i class="fas fa-check me-1"></i>Importar para a proposta</button>'
                    + '<button class="btn btn-sm btn-outline-secondary" onclick="cpCancelarImport()">Cancelar</button>'
                    + '</div>';
            }

            html += '</div>';
            el.innerHTML = html;
            messages.appendChild(el);
            scrollBottom();
        }

        function buildCounts(preview) {
            if (!preview) return [];
            const items = [];
            if (preview.passageiros?.length) items.push(`${preview.passageiros.length} passageiro${preview.passageiros.length > 1 ? 's' : ''}`);
            if (preview.voos?.length) items.push(`${preview.voos.length} voo${preview.voos.length > 1 ? 's' : ''}`);
            if (preview.destinos?.length) {
                items.push(`${preview.destinos.length} destino${preview.destinos.length > 1 ? 's' : ''}`);
                const h = preview.destinos.reduce((s, d) => s + (d.hospedagens?.length || 0), 0);
                const e = preview.destinos.reduce((s, d) => s + (d.experiencias?.length || 0), 0);
                const t = preview.destinos.reduce((s, d) => s + (d.transportes?.length || 0), 0);
                if (h) items.push(`${h} hospedagem${h > 1 ? 'ns' : ''}`);
                if (e) items.push(`${e} experiência${e > 1 ? 's' : ''}`);
                if (t) items.push(`${t} transporte${t > 1 ? 's' : ''}`);
            }
            if (preview.seguros?.length) items.push(`${preview.seguros.length} seguro${preview.seguros.length > 1 ? 's' : ''}`);
            return items;
        }

        function buildResumoText(preview) {
            const counts = buildCounts(preview);
            if (!counts.length) return 'Não consegui identificar itens estruturados neste documento.';
            return 'Identifiquei neste documento:\n— ' + counts.join('\n— ') + '\n\nPosso adicionar esses itens a esta proposta?';
        }

        // ── Confirmação de importação ────────────────────────────────────────
        window.cpConfirmarImport = async function () {
            await confirmarImportacao();
        };

        window.cpCancelarImport = function () {
            pendingPreview = null;
            document.getElementById('btnCpConfirmar')?.closest('.msg-bubble')
                ?.querySelector('.cp-import-actions')
                ?.remove();
            renderItem({ type: 'ai', data: { mensagem: 'Ok, não vou importar os itens. Se precisar, é só anexar o documento novamente.', pontosFort: [], gaps: [], proximaPergunta: null } });
        };

        async function confirmarImportacao() {
            if (!pendingPreview || !propostaId) return;

            const preview = pendingPreview;
            pendingPreview = null;

            // Remove action buttons
            document.querySelector('.cp-import-actions')?.remove();

            const loadingEl = appendLoading();
            setLoading(true);

            try {
                const res = await fetch('/Importacao/Confirmar', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ propostaId, preview })
                });
                if (!res.ok) {
                    const err = await res.json().catch(() => ({}));
                    throw new Error(err.erro || `Erro ${res.status}`);
                }
                const data = await res.json();
                loadingEl.remove();

                const r = data.resumo || {};
                const totais = [];
                if (r.passageiros) totais.push(`${r.passageiros} passageiro${r.passageiros > 1 ? 's' : ''}`);
                if (r.voos) totais.push(`${r.voos} voo${r.voos > 1 ? 's' : ''}`);
                if (r.destinos) totais.push(`${r.destinos} destino${r.destinos > 1 ? 's' : ''}`);
                if (r.hospedagens) totais.push(`${r.hospedagens} hospedagem${r.hospedagens > 1 ? 'ns' : ''}`);
                if (r.experiencias) totais.push(`${r.experiencias} experiência${r.experiencias > 1 ? 's' : ''}`);
                if (r.transportes) totais.push(`${r.transportes} transporte${r.transportes > 1 ? 's' : ''}`);
                if (r.seguros) totais.push(`${r.seguros} seguro${r.seguros > 1 ? 's' : ''}`);

                const msg = totais.length
                    ? `✓ Importação concluída! Foram adicionados: ${totais.join(', ')}. Role a tela para ver os itens.`
                    : '✓ Itens importados com sucesso!';

                renderItem({ type: 'ai', data: { mensagem: msg, pontosFort: [], gaps: [], proximaPergunta: null } });

            } catch (e) {
                loadingEl.remove();
                renderItem({ type: 'ai', data: { mensagem: `Erro ao importar: ${e.message}`, pontosFort: [], gaps: [], proximaPergunta: null } });
                console.warn('[Copiloto] import error', e);
            } finally {
                setLoading(false);
            }
        }

        function setLoading(on) {
            isLoading = on;
            sendBtn.disabled = on;
            sendBtn.style.opacity = on ? '.5' : '';
        }

        // ── Renderização de itens ────────────────────────────────────────────
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

                if (d.mensagem || (d.pontosFort && d.pontosFort.length)) {
                    const el = document.createElement('div');
                    el.className = 'msg-ai';
                    let html = '<div class="msg-bubble">';
                    if (d.mensagem) html += `<p style="margin:0 0 6px;white-space:pre-wrap">${esc(d.mensagem)}</p>`;
                    if (d.pontosFort && d.pontosFort.length) {
                        html += '<div class="cp-pontos">';
                        d.pontosFort.forEach(p => { html += `<div class="cp-ponto"><i class="fas fa-check-circle"></i>${esc(p)}</div>`; });
                        html += '</div>';
                    }
                    html += '</div>';
                    el.innerHTML = html;
                    messages.appendChild(el);
                }

                if (d.gaps && d.gaps.length) {
                    const el = document.createElement('div');
                    el.className = 'msg-ai';
                    let html = '<div class="msg-bubble cp-gaps-bubble"><div class="cp-gaps-title"><i class="fas fa-exclamation-triangle me-1"></i>Oportunidades de melhoria</div>';
                    d.gaps.forEach(g => { html += `<div class="cp-gap"><i class="fas fa-circle"></i>${esc(g)}</div>`; });
                    html += '</div>';
                    el.innerHTML = html;
                    messages.appendChild(el);
                }

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

        // ── Helpers ──────────────────────────────────────────────────────────
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

        // ── Input ────────────────────────────────────────────────────────────
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
