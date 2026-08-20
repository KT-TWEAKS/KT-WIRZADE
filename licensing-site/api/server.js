const express = require('express');
const helmet = require('helmet');
const cors = require('cors');
const fs = require('fs');
const path = require('path');
const { v4: uuidv4 } = require('uuid');
const { RateLimiterMemory } = require('rate-limiter-flexible');

const app = express();
const PORT = process.env.PORT || 3000;
const ADMIN_KEY = process.env.ADMIN_KEY || 'ktwirzade-admin-2026';

const DB_PATH = path.join(__dirname, 'db', 'playbooks.json');
const FRONTEND_PATH = path.join(__dirname, '..');

const rateLimiter = new RateLimiterMemory({
  points: 60,
  duration: 60,
});

function loadDB() {
  const raw = fs.readFileSync(DB_PATH, 'utf-8');
  return JSON.parse(raw);
}

function saveDB(data) {
  fs.writeFileSync(DB_PATH, JSON.stringify(data, null, 2), 'utf-8');
}

app.use(helmet({ contentSecurityPolicy: false }));
app.use(cors());
app.use(express.json());

app.use(async (req, res, next) => {
  try {
    await rateLimiter.consume(req.ip);
    next();
  } catch {
    res.status(429).json({ error: 'Muitas requisicoes. Tente novamente em 1 minuto.' });
  }
});

app.get('/api/verify', (req, res) => {
  const { prodID, hash } = req.query;

  if (!prodID && !hash) {
    return res.status(400).json({ error: 'Informe prodID ou hash para verificacao.' });
  }

  const db = loadDB();
  let found = null;

  if (prodID) {
    found = db.playbooks.find(p => p.productCode === prodID);
  }

  if (!found && hash) {
    found = db.playbooks.find(p => p.hash && p.hash.toLowerCase() === hash.toLowerCase());
  }

  if (found) {
    return res.json({
      verified: found.verified,
      status: found.verified ? 'verified' : 'unverified',
      playbook: {
        id: found.id,
        name: found.name,
        author: found.author,
        version: found.version,
        description: found.description,
        website: found.website,
        git: found.git,
        verifiedAt: found.verifiedAt,
      },
    });
  }

  return res.json({
    verified: false,
    status: 'unverified',
    playbook: null,
    message: 'Playbook nao encontrado na base de dados.',
  });
});

app.get('/api/playbooks', (req, res) => {
  const db = loadDB();
  const { search, tag } = req.query;

  let results = db.playbooks.filter(p => p.verified);

  if (search) {
    const q = search.toLowerCase();
    results = results.filter(p =>
      p.name.toLowerCase().includes(q) ||
      p.description.toLowerCase().includes(q) ||
      p.author.toLowerCase().includes(q)
    );
  }

  if (tag) {
    results = results.filter(p => p.tags && p.tags.includes(tag));
  }

  res.json({ playbooks: results, total: results.length });
});

app.get('/api/playbooks/:id', (req, res) => {
  const db = loadDB();
  const playbook = db.playbooks.find(p => p.id === req.params.id);

  if (!playbook) {
    return res.status(404).json({ error: 'Playbook nao encontrado.' });
  }

  res.json({ playbook });
});

app.post('/api/playbooks/register', (req, res) => {
  const adminKey = req.headers['x-admin-key'];

  if (adminKey !== ADMIN_KEY) {
    return res.status(403).json({ error: 'Chave de administrador invalida.' });
  }

  const { productCode, name, author, version, description, website, git, hash, tags } = req.body;

  if (!productCode || !name) {
    return res.status(400).json({ error: 'productCode e name sao obrigatorios.' });
  }

  const db = loadDB();
  const exists = db.playbooks.find(p => p.productCode === productCode);

  if (exists) {
    return res.status(409).json({ error: 'ProductCode ja registrado.' });
  }

  const newPlaybook = {
    id: `plb-${uuidv4().slice(0, 8)}`,
    productCode,
    name,
    author: author || 'Desconhecido',
    version: version || '1.0.0',
    description: description || '',
    website: website || null,
    git: git || null,
    verified: false,
    verifiedAt: null,
    hash: hash || null,
    icon: null,
    tags: tags || [],
  };

  db.playbooks.push(newPlaybook);
  saveDB(db);

  res.status(201).json({ playbook: newPlaybook, message: 'Playbook registrado com sucesso.' });
});

app.use(express.static(FRONTEND_PATH));

app.get('*', (req, res) => {
  res.sendFile(path.join(FRONTEND_PATH, 'index.html'));
});

app.listen(PORT, () => {
  console.log(`[KT WIRZADE] Servidor rodando em http://localhost:${PORT}`);
  console.log(`[KT WIRZADE] API: http://localhost:${PORT}/api/verify`);
  console.log(`[KT WIRZADE] Docs: http://localhost:${PORT}/api-docs.html`);
});