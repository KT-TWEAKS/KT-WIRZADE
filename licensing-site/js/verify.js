document.addEventListener('DOMContentLoaded', () => {
  initVerifyPage();
});

function initVerifyPage() {
  const codeTab = document.getElementById('tab-code');
  const fileTab = document.getElementById('tab-file');
  const codePanel = document.getElementById('panel-code');
  const filePanel = document.getElementById('panel-file');
  const verifyBtn = document.getElementById('verify-btn');
  const dropZone = document.getElementById('drop-zone');
  const fileInput = document.getElementById('file-input');

  if (codeTab && fileTab) {
    codeTab.addEventListener('click', () => {
      codeTab.classList.add('active');
      fileTab.classList.remove('active');
      codePanel.style.display = 'block';
      filePanel.style.display = 'none';
    });

    fileTab.addEventListener('click', () => {
      fileTab.classList.add('active');
      codeTab.classList.remove('active');
      filePanel.style.display = 'block';
      codePanel.style.display = 'none';
    });
  }

  if (verifyBtn) {
    verifyBtn.addEventListener('click', handleVerify);
  }

  if (dropZone && fileInput) {
    dropZone.addEventListener('click', () => fileInput.click());

    dropZone.addEventListener('dragover', (e) => {
      e.preventDefault();
      dropZone.classList.add('dragover');
    });

    dropZone.addEventListener('dragleave', () => {
      dropZone.classList.remove('dragover');
    });

    dropZone.addEventListener('drop', (e) => {
      e.preventDefault();
      dropZone.classList.remove('dragover');
      if (e.dataTransfer.files.length) {
        handleFile(e.dataTransfer.files[0]);
      }
    });

    fileInput.addEventListener('change', (e) => {
      if (e.target.files.length) {
        handleFile(e.target.files[0]);
      }
    });
  }

  renderHistory();
}

async function handleVerify() {
  const productCode = document.getElementById('product-code')?.value?.trim();
  const btn = document.getElementById('verify-btn');

  if (!productCode) {
    window.showToast('Informe o ProductCode para verificacao.', 'error');
    return;
  }

  btn.innerHTML = '<span class="spinner"></span> Verificando...';
  btn.disabled = true;

  try {
    const result = await window.ktAPI.verify(productCode, null);
    showResult(result);
    saveToHistory(productCode, result.status);
  } catch (err) {
    window.showToast('Erro ao verificar: ' + err.message, 'error');
  } finally {
    btn.innerHTML = 'Verificar';
    btn.disabled = false;
  }
}

async function handleFile(file) {
  const dropZone = document.getElementById('drop-zone');
  const btn = document.getElementById('verify-btn');

  dropZone.querySelector('.drop-zone-text').innerHTML = `<strong>${file.name}</strong>`;

  btn.innerHTML = '<span class="spinner"></span> Calculando hash...';
  btn.disabled = true;

  try {
    const hash = await computeSHA256(file);
    document.getElementById('file-hash').value = hash;

    btn.innerHTML = '<span class="spinner"></span> Verificando...';
    const result = await window.ktAPI.verify(null, hash);
    showResult(result);
    saveToHistory(file.name, result.status);
  } catch (err) {
    window.showToast('Erro ao processar arquivo: ' + err.message, 'error');
  } finally {
    btn.innerHTML = 'Verificar';
    btn.disabled = false;
  }
}

async function computeSHA256(file) {
  const buffer = await file.arrayBuffer();
  const hashBuffer = await crypto.subtle.digest('SHA-256', buffer);
  const hashArray = Array.from(new Uint8Array(hashBuffer));
  return hashArray.map(b => b.toString(16).padStart(2, '0')).join('');
}

function showResult(result) {
  const box = document.getElementById('result-box');
  if (!box) return;

  box.className = `result-box show ${result.status}`;

  const iconEl = box.querySelector('.result-icon');
  const titleEl = box.querySelector('.result-title');
  const detailsEl = box.querySelector('.result-details');

  if (result.status === 'verified') {
    iconEl.textContent = '\u2713';
    titleEl.textContent = 'Playbook Verificado';
  } else if (result.status === 'unverified') {
    iconEl.textContent = '\u26A0';
    titleEl.textContent = result.playbook ? 'Nao Verificado' : 'Nao Encontrado';
  } else {
    iconEl.textContent = '\u2717';
    titleEl.textContent = 'Perigo Detectado';
  }

  if (result.playbook) {
    detailsEl.innerHTML = `
      <div class="result-detail-row">
        <span class="result-detail-label">Nome</span>
        <span class="result-detail-value">${result.playbook.name}</span>
      </div>
      <div class="result-detail-row">
        <span class="result-detail-label">Autor</span>
        <span class="result-detail-value">${result.playbook.author}</span>
      </div>
      <div class="result-detail-row">
        <span class="result-detail-label">Versao</span>
        <span class="result-detail-value">${result.playbook.version}</span>
      </div>
      <div class="result-detail-row">
        <span class="result-detail-label">Verificado em</span>
        <span class="result-detail-value">${result.playbook.verifiedAt ? new Date(result.playbook.verifiedAt).toLocaleDateString('pt-BR') : 'N/A'}</span>
      </div>
    `;
  } else {
    detailsEl.innerHTML = `<p style="color: var(--text-muted)">${result.message || 'Este playbook nao esta na nossa base de dados verificada.'}</p>`;
  }
}

function saveToHistory(query, status) {
  const history = JSON.parse(localStorage.getItem('ktw-history') || '[]');
  history.unshift({ query, status, time: Date.now() });
  if (history.length > 20) history.length = 20;
  localStorage.setItem('ktw-history', JSON.stringify(history));
  renderHistory();
}

function renderHistory() {
  const list = document.getElementById('history-list');
  if (!list) return;

  const history = JSON.parse(localStorage.getItem('ktw-history') || '[]');

  if (!history.length) {
    list.innerHTML = '<div class="empty-state"><div class="empty-state-icon">\u{1F4CB}</div><p>Nenhuma verificacao recente.</p></div>';
    return;
  }

  list.innerHTML = history.map(item => `
    <div class="history-item" onclick="reverify('${item.query.replace(/'/g, "\\'")}')">
      <div class="history-item-left">
        <div class="history-dot ${item.status}"></div>
        <span class="history-text">${item.query}</span>
      </div>
      <span class="history-time">${timeAgo(item.time)}</span>
    </div>
  `).join('');
}

function reverify(query) {
  const input = document.getElementById('product-code');
  if (input) {
    input.value = query;
    document.getElementById('tab-code')?.click();
    handleVerify();
  }
}

function timeAgo(ts) {
  const diff = Date.now() - ts;
  const mins = Math.floor(diff / 60000);
  if (mins < 1) return 'agora';
  if (mins < 60) return `${mins}min`;
  const hrs = Math.floor(mins / 60);
  if (hrs < 24) return `${hrs}h`;
  return `${Math.floor(hrs / 24)}d`;
}