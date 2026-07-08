/**
 * copilot.js — Copiloto da Proposta · AgentTools IA
 *
 * Fluxo de importação:
 *   Arquivo → API analisa → TravelProposalDraft em memória →
 *   Chat apresenta bloco a bloco → Agente confirma → Persistência incremental
 */

(function () {
    'use strict';

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    function init() {
        const panel      = document.getElementById('copilotPanel');
        if (!panel) return;

        const toggleBtn  = document.getElementById('copilotToggle');
        const closeBtn   = document.getElementById('copilotClose');
        const messages   = document.getElementById('copilotMessages');
        const inputEl    = document.getElementById('copilotInput');
        const sendBtn    = document.getElementById('copilotSend');
        const chips      = document.getElementById('cpChips');
        const fileInput  = document.getElementById('cpFileInput');
        const attachDiv  = document.getElementById('cpAttachments');

        // ── Estado ──────────────────────────────────────────────────────────
        let apiHistory     = [];
        let displayLog     = [];
        let isLoading      = false;
        let opened         = false;
        let attachedFiles  = [];

        // Draft em memória — NUNCA vai direto ao banco
        let currentDraft   = null;
        let draftBlocos    = [];
        let blocoIdx       = 0;
        let blocosPulados  = new Set();

        const propostaId = (panel.dataset.propostaId || '').trim() || null;
        const isEditMode = !!propostaId;

        // ── Pendente vindo de "Criar com IA" ────────────────────────────────
        let pendingAutoImport = null;
        if (new URLSearchParams(window.location.search).get('pendingImport') === '1' && isEditMode) {
            try {
                const raw = sessionStorage.getItem('pendingImport_' + propostaId);
                if (raw) {
                    pendingAutoImport = JSON.parse(raw);
                    sessionStorage.removeItem('pendingImport_' + propostaId);
                }
            } catch (e) { /* ignore */ }
        }

        // ── Painel abrir/fechar ─────────────────────────────────────────────
        function openPanel() {
            panel.classList.add('open');
            toggleBtn.style.display = 'none';
            if (!opened) {
                opened = true;
                if (pendingAutoImport) {
                    // Vem do fluxo "Criar com IA" — o objeto já é o response de AnalisarArquivos
                    const data = pendingAutoImport;
                    pendingAutoImport = null;
                    setTimeout(() => iniciarRevisaoDraft(data.draft || data), 300);
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

        if (pendingAutoImport) setTimeout(openPanel, 400);

        // ── Chips rápidos ────────────────────────────────────────────────────
        if (chips) {
            chips.querySelectorAll('button[data-msg]').forEach(btn =>
                btn.addEventListener('click', () => sendMessage(btn.dataset.msg)));
        }

        // ── File attachment (label for= cuida do clique) ─────────────────────
        if (fileInput) {
            fileInput.addEventListener('change', () => {
                Array.from(fileInput.files).forEach(f => {
                    if (attachedFiles.length >= 5) return;
                    if (!attachedFiles.find(x => x.name === f.name && x.size === f.size))
                        attachedFiles.push(f);
                });
                fileInput.value = '';
                renderAttachChips();
            });
        }

        function renderAttachChips() {
            if (!attachDiv) return;
            if (!attachedFiles.length) { attachDiv.innerHTML = ''; return; }
            attachDiv.innerHTML = attachedFiles.map((f, i) => {
                const ext  = f.name.split('.').pop().toLowerCase();
                const icon = { pdf: '📄', jpg: '🖼', jpeg: '🖼', png: '🖼', gif: '🖼',
                               webp: '🖼', docx: '📝', doc: '📝', txt: '📃', eml: '📧' }[ext] || '📎';
                return `<div class="cp-attach-chip">
                    <span>${icon}</span>
                    <span class="chip-name">${esc(f.name)}</span>
                    <button onclick="cpRemoveAnexo(${i})" title="Remover">✕</button>
                </div>`;
            }).join('');
        }

        window.cpRemoveAnexo = idx => { attachedFiles.splice(idx, 1); renderAttachChips(); };

        // ── Persistência de histórico por proposta ────────────────────────────
        function storageKey() { return 'cp_' + propostaId; }

        function saveState() {
            if (!isEditMode) return;
            try {
                localStorage.setItem(storageKey(), JSON.stringify({
                    apiHistory: apiHistory.slice(-24),
                    displayLog: displayLog.slice(-50),
                    ts: Date.now()
                }));
            } catch (e) { /* quota */ }
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
                sep.textContent = '— continuando conversa —';
                messages.appendChild(sep);
                scrollBottom();
                return true;
            } catch (e) { return false; }
        }

        // ── Boas-vindas ───────────────────────────────────────────────────────
        function showWelcome() {
            const msg = isEditMode
                ? 'Analisei a proposta e estou pronto para ajudar. Posso sugerir melhorias, fortalecer a argumentação ou identificar lacunas.\n\nUse o 📎 para enviar um voucher, PDF ou e-mail e eu identifico e adiciono os itens nesta proposta automaticamente.'
                : 'Olá! Vou te ajudar a construir essa proposta. Me conte: para onde é a viagem, quando e quem vai?';
            const item = { type: 'ai', data: { mensagem: msg, pontosFort: [], gaps: [], proximaPergunta: null } };
            renderItem(item);
            if (isEditMode) displayLog.push(item);
            apiHistory.push({ role: 'assistant', content: msg });
            if (isEditMode) saveState();
        }

        // ════════════════════════════════════════════════════════════════════
        //  FLUXO DE IMPORTAÇÃO INTELIGENTE
        // ════════════════════════════════════════════════════════════════════

        // Inicia a revisão do TravelProposalDraft
        function iniciarRevisaoDraft(draft) {
            if (!draft) return;
            currentDraft  = draft;
            draftBlocos   = buildBlocos(draft);
            blocoIdx      = 0;
            blocosPulados = new Set();

            // Mensagem de abertura da IA
            const mensagem = draft.mensagemInicial || 'Analisei o documento. Vamos revisar os itens encontrados.';
            appendMsgAI(mensagem);

            // Alertas e pendências
            const alertas = [...(draft.alertas || []), ...(draft.pendentes || [])];
            if (alertas.length) {
                const alertEl = document.createElement('div');
                alertEl.className = 'msg-ai';
                alertEl.innerHTML = `<div class="msg-bubble cp-alerta-bubble">
                    <div class="cp-alerta-title">⚠️ Atenção</div>
                    ${alertas.map(a => `<div class="cp-alerta-item">• ${esc(a)}</div>`).join('')}
                </div>`;
                messages.appendChild(alertEl);
                scrollBottom();
            }

            // Apresentar blocos
            if (draftBlocos.length > 0) {
                setTimeout(apresentarProximoBloco, 700);
            } else {
                appendMsgAI('Não encontrei itens estruturados neste documento. Tente com um arquivo diferente.');
            }
        }

        function buildBlocos(draft) {
            const blocos = [];
            if (draft.proposta?.titulo)     blocos.push({ tipo: 'proposta',    icon: '📋', label: 'Dados da proposta',       data: draft.proposta });
            if (draft.passageiros?.length)  blocos.push({ tipo: 'passageiros', icon: '👥', label: 'Passageiros',             data: draft.passageiros });
            if (draft.voos?.length)         blocos.push({ tipo: 'voos',        icon: '✈️', label: 'Voos',                   data: draft.voos });
            if (draft.destinos?.length)     blocos.push({ tipo: 'destinos',    icon: '📍', label: 'Destinos e hospedagens',  data: draft.destinos });
            if (draft.seguros?.length)      blocos.push({ tipo: 'seguros',     icon: '🛡️', label: 'Seguros',               data: draft.seguros });
            return blocos;
        }

        function apresentarProximoBloco() {
            if (blocoIdx >= draftBlocos.length) {
                renderFinalizacao();
                return;
            }
            renderBlocoCard(draftBlocos[blocoIdx], blocoIdx);
        }

        function renderBlocoCard(bloco, idx) {
            const cardId = `cp-bloco-${idx}`;
            const el = document.createElement('div');
            el.className = 'msg-ai';
            el.id = cardId;

            let itensHtml = '';
            switch (bloco.tipo) {
                case 'proposta':
                    itensHtml = renderPropostaItens(bloco.data);
                    break;
                case 'passageiros':
                    itensHtml = bloco.data.map(p =>
                        `<div class="cp-bloco-item">👤 <strong>${esc(p.nome)}</strong>${p.dataNascimento ? ` <span class="cp-item-detalhe">· ${fmtNasc(p.dataNascimento)}</span>` : ''}${p.observacoes ? ` <span class="cp-item-detalhe">· ${esc(p.observacoes)}</span>` : ''}</div>`
                    ).join('');
                    break;
                case 'voos':
                    itensHtml = bloco.data.map(v => {
                        const icon = v.tipoVoo === 'Volta' ? '↙️' : '↗️';
                        const hora = v.horarioSaida ? ' · ' + v.horarioSaida.substring(11, 16) : '';
                        const data = v.horarioSaida ? ' · ' + fmtDataCurta(v.horarioSaida.substring(0, 10)) : '';
                        return `<div class="cp-bloco-item">${icon} <strong>${esc(v.numeroVoo)}</strong> · ${esc(v.origem)} → ${esc(v.destino)}${data}${hora} · <span class="cp-item-detalhe">${esc(v.companhia)}${v.classe ? ', ' + esc(v.classe) : ''}</span></div>`;
                    }).join('');
                    break;
                case 'destinos':
                    itensHtml = bloco.data.map(d => {
                        let html = `<div class="cp-bloco-item-destino">`;
                        html += `<div>📍 <strong>${esc(d.nome)}</strong>`;
                        if (d.dataChegada && d.dataSaida) html += ` <span class="cp-item-detalhe">· ${fmtDataCurta(d.dataChegada)} → ${fmtDataCurta(d.dataSaida)}</span>`;
                        html += `</div>`;
                        d.hospedagens?.forEach(h => {
                            html += `<div class="cp-bloco-sub">🏨 ${esc(h.nome)}<span class="cp-item-detalhe"> · ${fmtPensao(h.tipoPensao)}</span></div>`;
                        });
                        d.transportes?.forEach(t => {
                            html += `<div class="cp-bloco-sub">🚌 ${esc(t.titulo)}</div>`;
                        });
                        d.experiencias?.forEach(e => {
                            html += `<div class="cp-bloco-sub">🎯 ${esc(e.tipoPasseio)}</div>`;
                        });
                        html += `</div>`;
                        return html;
                    }).join('');
                    break;
                case 'seguros':
                    itensHtml = bloco.data.map(s =>
                        `<div class="cp-bloco-item">🛡️ <strong>${esc(s.titulo)}</strong>${s.valor ? ` <span class="cp-item-detalhe">· R$ ${fmtNum(s.valor)}</span>` : ''}</div>`
                    ).join('');
                    break;
            }

            const total = bloco.tipo === 'destinos'
                ? `${bloco.data.length} destino${bloco.data.length !== 1 ? 's' : ''}`
                : `${bloco.data.length || 1} item${(bloco.data.length || 1) !== 1 ? 's' : ''}`;

            el.innerHTML = `<div class="msg-bubble cp-bloco-card">
                <div class="cp-bloco-header">
                    ${bloco.icon} <strong>${bloco.label}</strong>
                    <span class="cp-bloco-count">${total}</span>
                </div>
                <div class="cp-bloco-itens">${itensHtml}</div>
                <div class="cp-bloco-actions">
                    <button class="btn btn-sm btn-success" id="btn-confirmar-${idx}" onclick="cpConfirmarBloco(${idx})">
                        <i class="fas fa-check me-1"></i>Confirmar
                    </button>
                    <button class="btn btn-sm btn-outline-secondary" onclick="cpPularBloco(${idx})">
                        Pular
                    </button>
                </div>
            </div>`;
            messages.appendChild(el);
            scrollBottom();
        }

        function renderPropostaItens(proposta) {
            let html = '';
            if (proposta.titulo)           html += `<div class="cp-bloco-item">📌 <strong>Título:</strong> ${esc(proposta.titulo)}</div>`;
            if (proposta.operadora)        html += `<div class="cp-bloco-item">🏢 <strong>Operadora:</strong> ${esc(proposta.operadora)}</div>`;
            if (proposta.observacoesGerais) html += `<div class="cp-bloco-item cp-obs-item">📝 ${esc(proposta.observacoesGerais)}</div>`;
            return html;
        }

        // ── Confirmar bloco ───────────────────────────────────────────────────
        window.cpConfirmarBloco = async function (idx) {
            if (!propostaId || isLoading) return;

            const bloco  = draftBlocos[idx];
            const cardEl = document.getElementById(`cp-bloco-${idx}`);

            // Desabilitar botões do card
            cardEl?.querySelectorAll('button').forEach(b => b.disabled = true);

            const loadEl = appendLoading();
            setLoading(true);

            try {
                const body = buildBlocoRequest(idx, bloco);
                const res  = await fetch('/Importacao/ConfirmarBloco', {
                    method:  'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body:    JSON.stringify(body)
                });
                if (!res.ok) {
                    const err = await res.json().catch(() => ({}));
                    throw new Error(err.erro || `Erro ${res.status}`);
                }
                const data = await res.json();
                loadEl.remove();

                // Remover botões do card confirmado
                cardEl?.querySelector('.cp-bloco-actions')?.remove();
                const label = cardEl?.querySelector('.cp-bloco-header');
                if (label) label.insertAdjacentHTML('beforeend', ' <span class="cp-confirmado-badge">✓ confirmado</span>');

                appendMsgAI(`✅ ${data.mensagem}`);
            } catch (e) {
                loadEl.remove();
                appendMsgAI(`Erro ao confirmar: ${e.message}`);
                cardEl?.querySelectorAll('button').forEach(b => b.disabled = false);
                setLoading(false);
                return;
            }

            setLoading(false);
            blocoIdx = idx + 1;
            setTimeout(apresentarProximoBloco, 600);
        };

        // ── Pular bloco ───────────────────────────────────────────────────────
        window.cpPularBloco = function (idx) {
            const cardEl = document.getElementById(`cp-bloco-${idx}`);
            cardEl?.querySelector('.cp-bloco-actions')?.remove();
            const label = cardEl?.querySelector('.cp-bloco-header');
            if (label) label.insertAdjacentHTML('beforeend', ' <span class="cp-pulado-badge">pulado</span>');

            blocosPulados.add(idx);
            appendMsgAI('Ok, vou pular esse bloco. Podemos voltar a ele depois se precisar.');
            blocoIdx = idx + 1;
            setTimeout(apresentarProximoBloco, 500);
        };

        function buildBlocoRequest(idx, bloco) {
            const base = { propostaId };
            switch (bloco.tipo) {
                case 'proposta':    return { ...base, bloco: 'proposta', proposta: bloco.data };
                case 'passageiros': return { ...base, bloco: 'passageiros', passageiros: bloco.data };
                case 'voos':        return { ...base, bloco: 'voos', voos: bloco.data };
                case 'destinos':    return { ...base, bloco: 'destinos', destinos: bloco.data };
                case 'seguros':     return { ...base, bloco: 'seguros', seguros: bloco.data };
                default:            return base;
            }
        }

        function renderFinalizacao() {
            const confirmados = draftBlocos.length - blocosPulados.size;
            const pulados     = blocosPulados.size;

            let msg = confirmados > 0
                ? `✅ Pronto! Importei ${confirmados} seção${confirmados !== 1 ? 'ões' : ''} para a sua proposta.`
                : '📋 Nenhum bloco foi importado nesta sessão.';

            if (pulados > 0) msg += `\n\n${pulados} bloco${pulados !== 1 ? 's foram pulados' : ' foi pulado'}. Se quiser importá-los depois, é só anexar o documento novamente.`;

            if (confirmados > 0) msg += '\n\n**Recarregue a página para ver todas as alterações aplicadas.**';

            const el = document.createElement('div');
            el.className = 'msg-ai';
            el.innerHTML = `<div class="msg-bubble">
                <p style="white-space:pre-wrap;margin:0 0 10px">${esc(msg)}</p>
                ${confirmados > 0 ? `<button class="btn btn-sm btn-primary" onclick="location.reload()">
                    <i class="fas fa-sync-alt me-1"></i>Recarregar página
                </button>` : ''}
            </div>`;
            messages.appendChild(el);
            scrollBottom();

            // Limpar estado do draft
            currentDraft = null;
            draftBlocos  = [];
        }

        // ════════════════════════════════════════════════════════════════════
        //  ENVIO DE MENSAGENS
        // ════════════════════════════════════════════════════════════════════

        async function sendMessage(text) {
            text = (text || '').trim();

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

            const payload = {
                propostaId, message: text,
                history: apiHistory.slice(-14),
                abaAtual: getAba(),
                formContext: propostaId ? null : getFormCtx(),
            };

            const loadEl = appendLoading();
            setLoading(true);

            try {
                const res  = await fetch('/AiCopilot/Chat', {
                    method: 'POST', headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(payload),
                });
                if (!res.ok) throw new Error(`HTTP ${res.status}`);
                const data = await res.json();
                loadEl.remove();
                apiHistory.push({ role: 'assistant', content: data.mensagem || '' });
                const aiItem = { type: 'ai', data: { mensagem: data.mensagem || '', pontosFort: data.pontosFort || [], gaps: data.gaps || [], proximaPergunta: data.proximaPergunta || null } };
                displayLog.push(aiItem);
                renderItem(aiItem);
                saveState();
            } catch (e) {
                loadEl.remove();
                renderItem({ type: 'ai', data: { mensagem: 'Não consegui processar a mensagem. Tente novamente.', pontosFort: [], gaps: [], proximaPergunta: null } });
            } finally {
                setLoading(false);
            }
        }

        // ── Envio com arquivos → análise de importação ────────────────────────
        async function sendWithFiles(extraText) {
            if (isLoading) return;

            const files   = [...attachedFiles];
            const nomes   = files.map(f => f.name).join(', ');
            const userTxt = extraText || `Analisar documento${files.length > 1 ? 's' : ''}: ${nomes}`;

            renderItem({ type: 'user', text: userTxt });

            attachedFiles = [];
            renderAttachChips();
            inputEl.value = '';
            inputEl.style.height = '';

            const loadEl = appendLoading();
            setLoading(true);

            try {
                const fd = new FormData();
                fd.append('propostaId', propostaId || '');
                files.forEach(f => fd.append('arquivos', f));

                const res = await fetch('/AiCopilot/AnalisarArquivos', { method: 'POST', body: fd });
                if (!res.ok) {
                    const err = await res.json().catch(() => ({}));
                    throw new Error(err.erro || `Erro ${res.status}`);
                }
                const data = await res.json();
                loadEl.remove();

                if (data.tipo === 'analise_documentos' && data.draft) {
                    iniciarRevisaoDraft(data.draft);
                } else {
                    appendMsgAI(data.mensagem || 'Análise concluída, mas não encontrei itens estruturados.');
                }
            } catch (e) {
                loadEl.remove();
                appendMsgAI(`Erro ao analisar o documento: ${e.message}`);
            } finally {
                setLoading(false);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  RENDERIZAÇÃO
        // ════════════════════════════════════════════════════════════════════

        function appendMsgAI(texto) {
            const el = document.createElement('div');
            el.className = 'msg-ai';
            el.innerHTML = `<div class="msg-bubble" style="white-space:pre-wrap">${esc(texto)}</div>`;
            messages.appendChild(el);
            scrollBottom();
        }

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
                if (d.mensagem || d.pontosFort?.length) {
                    const el = document.createElement('div');
                    el.className = 'msg-ai';
                    let html = '<div class="msg-bubble">';
                    if (d.mensagem) html += `<p style="margin:0 0 6px;white-space:pre-wrap">${esc(d.mensagem)}</p>`;
                    if (d.pontosFort?.length) {
                        html += '<div class="cp-pontos">';
                        d.pontosFort.forEach(p => { html += `<div class="cp-ponto"><i class="fas fa-check-circle"></i>${esc(p)}</div>`; });
                        html += '</div>';
                    }
                    html += '</div>';
                    el.innerHTML = html;
                    messages.appendChild(el);
                }
                if (d.gaps?.length) {
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

        function setLoading(on) {
            isLoading = on;
            sendBtn.disabled = on;
            sendBtn.style.opacity = on ? '.5' : '';
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        function getAba() {
            const a = document.querySelector('#editarTabs .nav-link.active');
            return a ? (a.getAttribute('href') || '').replace('#tab-', '') || 'dados' : 'dados';
        }
        function getFormCtx() {
            return { isNovaProposta: true, titulo: val('Titulo'), dataInicio: val('DataInicio'), dataFim: val('DataFim'), numeroPassageiros: intVal('NumeroPassageiros', 1), numeroCriancas: intVal('NumeroCriancas', 0) };
        }
        function val(id)       { return (document.getElementById(id) || {}).value || ''; }
        function intVal(id, d) { return parseInt((document.getElementById(id) || {}).value, 10) || d; }
        function esc(t)        { if (t == null) return ''; const d = document.createElement('div'); d.textContent = String(t); return d.innerHTML; }
        function scrollBottom() { messages.scrollTop = messages.scrollHeight; }

        function fmtDataCurta(iso) {
            if (!iso) return '';
            try { const p = iso.split('-'); return `${p[2]}/${p[1]}`; } catch { return iso; }
        }
        function fmtNasc(iso) {
            if (!iso) return '';
            try { const p = iso.split('-'); return `${p[2]}/${p[1]}/${p[0]}`; } catch { return iso; }
        }
        function fmtNum(n) {
            if (n == null) return '';
            return Number(n).toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
        }
        function fmtPensao(v) {
            const m = { SemPensao: 'Sem pensão', CafeDaManha: 'Café da manhã', MeiaPensao: 'Meia pensão', PensaoCompleta: 'Pensão completa', AllInclusive: 'All inclusive', Outros: 'Outros' };
            return m[v] || v || '';
        }

        // ── Input / envio ─────────────────────────────────────────────────────
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
