/**
 * multi-photo.js — Componente reutilizável de upload múltiplo de fotos
 *
 * Uso: adicione o atributo [data-multi-photo-preview="id-do-container"]
 * em qualquer <input type="file">.
 *
 * O elemento com o id informado receberá a pré-visualização dinâmica.
 * Cada miniatura tem um botão × para remover o arquivo antes do envio.
 *
 * Não depende de jQuery — vanilla JS puro.
 */
(function () {
    'use strict';

    var MAX_SIZE    = 10 * 1024 * 1024; // 10 MB
    var ALLOWED     = ['image/jpeg', 'image/jpg', 'image/png', 'image/gif', 'image/bmp', 'image/webp'];
    var ALLOWED_EXT = ['.jpg', '.jpeg', '.png', '.gif', '.bmp', '.webp'];

    /* Reconstrói o FileList sem os arquivos removidos */
    function rebuildFileList(files) {
        var dt = new DataTransfer();
        files.forEach(function (f) { dt.items.add(f); });
        return dt.files;
    }

    /* Atualiza o contador de arquivos no topo do preview */
    function updateCount(container, total) {
        var el = container.querySelector('.mpu-count');
        if (!el) return;
        el.textContent = total === 0
            ? ''
            : total + ' foto' + (total !== 1 ? 's' : '') + ' selecionada' + (total !== 1 ? 's' : '');
    }

    /* Valida um único arquivo — retorna true se OK */
    function validarArquivo(file) {
        if (file.size > MAX_SIZE) {
            alert('"' + file.name + '" é muito grande (máx. 10 MB). O arquivo foi ignorado.');
            return false;
        }
        var mime = (file.type || '').toLowerCase();
        var ext  = file.name.toLowerCase().lastIndexOf('.') >= 0
                   ? file.name.toLowerCase().substring(file.name.lastIndexOf('.'))
                   : '';
        if (!ALLOWED.includes(mime) && !ALLOWED_EXT.includes(ext)) {
            alert('"' + file.name + '" tem formato não permitido. Use JPG, PNG, GIF, BMP ou WebP.');
            return false;
        }
        return true;
    }

    /* Renderiza (ou atualiza) o grid de miniaturas */
    function renderPreview(input, container) {
        var files = Array.from(input.files);

        // Limpa thumbs anteriores (mantém o elemento de contagem)
        var grid = container.querySelector('.mpu-grid');
        if (!grid) {
            grid = document.createElement('div');
            grid.className = 'mpu-grid multi-photo-preview';
            container.appendChild(grid);
        }
        grid.innerHTML = '';

        updateCount(container, files.length);

        files.forEach(function (file, index) {
            var item = document.createElement('div');
            item.className = 'multi-photo-preview-item';

            var img = document.createElement('img');
            img.alt = file.name;
            item.appendChild(img);

            var reader = new FileReader();
            reader.onload = function (e) { img.src = e.target.result; };
            reader.readAsDataURL(file);

            var btn = document.createElement('button');
            btn.type      = 'button';
            btn.className = 'mpu-remove-btn';
            btn.title     = 'Remover ' + file.name;
            btn.innerHTML = '&times;';
            btn.addEventListener('click', function () {
                var remaining = Array.from(input.files).filter(function (_, i) { return i !== index; });
                input.files = rebuildFileList(remaining);
                renderPreview(input, container);
            });
            item.appendChild(btn);

            grid.appendChild(item);
        });
    }

    /* Inicializa um input com o atributo data-multi-photo-preview */
    function initInput(input) {
        var containerId = input.dataset.multiPhotoPreview;
        if (!containerId) return;

        var container = document.getElementById(containerId);
        if (!container) return;

        // Garante que o elemento de contagem existe
        if (!container.querySelector('.mpu-count')) {
            var count = document.createElement('p');
            count.className = 'mpu-count multi-photo-count';
            container.insertBefore(count, container.firstChild);
        }

        input.addEventListener('change', function () {
            // Filtra arquivos inválidos
            var valid = Array.from(this.files).filter(validarArquivo);
            if (valid.length !== this.files.length) {
                this.files = rebuildFileList(valid);
            }
            renderPreview(this, container);
        });
    }

    /* Auto-inicialização no carregamento da página */
    document.addEventListener('DOMContentLoaded', function () {
        document.querySelectorAll('input[data-multi-photo-preview]').forEach(initInput);
    });

    /* API pública — para inputs criados dinamicamente (ex: após abrir modal) */
    window.initMultiPhotoInput = initInput;

    /* Reseta o preview de um container e limpa o input */
    window.resetMultiPhotoInput = function (inputId, containerId) {
        var input = document.getElementById(inputId);
        var container = document.getElementById(containerId);
        if (!input || !container) return;
        input.value = '';
        var grid = container.querySelector('.mpu-grid');
        if (grid) grid.innerHTML = '';
        updateCount(container, 0);
    };
})();
