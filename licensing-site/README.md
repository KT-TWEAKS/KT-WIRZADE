# KT WIRZADE - Sistema de Verificacao de Playbooks

Sistema completo de verificacao e gestao de playbooks KT WIRZADE.

## Estrutura

```
licensing-site/
├── index.html          # Pagina principal
├── verify.html         # Pagina de verificacao
├── playbooks.html      # Catalogo de playbooks
├── api-docs.html       # Documentacao da API
├── css/style.css       # Estilos
├── js/                 # JavaScript
│   ├── main.js         # Funcoes gerais
│   ├── verify.js       # Logica de verificacao
│   └── api.js          # Cliente API
├── assets/logo.svg     # Logo KT WIRZADE
├── api/                # Backend
│   ├── server.js       # Servidor Express
│   ├── package.json    # Dependencias
│   ├── db/             # Banco de dados
│   │   └── playbooks.json
│   └── middleware/
│       └── cors.js
└── README.md
```

## Instalacao

### 1. Instalar dependencias

```bash
cd api
npm install
```

### 2. Iniciar o servidor

```bash
npm start
```

O servidor inicia em `http://localhost:3000`.

### 3. Variaveis de ambiente (opcional)

| Variavel | Padrao | Descricao |
|----------|--------|-----------|
| `PORT` | `3000` | Porta do servidor |
| `ADMIN_KEY` | `ktwirzade-admin-2026` | Chave de administrador |

## Endpoints da API

### GET /api/verify

Verifica a autenticidade de um playbook.

```
GET /api/verify?prodID=KTW-DEBLOAT-001
GET /api/verify?hash=a1b2c3d4...
```

**Resposta:**
```json
{
  "verified": true,
  "status": "verified",
  "playbook": { ... }
}
```

### GET /api/playbooks

Lista playbooks verificados.

```
GET /api/playbooks
GET /api/playbooks?search=debloat
GET /api/playbooks?tag=gaming
```

### GET /api/playbooks/:id

Detalhes de um playbook.

```
GET /api/playbooks/example-001
```

### POST /api/playbooks/register

Registra um novo playbook (requer chave admin).

```
POST /api/playbooks/register
Headers: X-Admin-Key: <sua-chave>

{
  "productCode": "KTW-NOVO-001",
  "name": "Nome do Playbook",
  ...
}
```

## Deploy

### Producao

1. Configure as variaveis de ambiente:
   ```bash
   set PORT=80
   set ADMIN_KEY=sua-chave-secreta
   ```

2. Inicie o servidor:
   ```bash
   node server.js
   ```

### Docker (opcional)

```dockerfile
FROM node:18-alpine
WORKDIR /app
COPY api/package*.json ./api/
RUN cd api && npm ci --production
COPY . .
EXPOSE 3000
CMD ["node", "api/server.js"]
```

## Frontend

O frontend e estatico e servido pelo Express. Basta acessar `http://localhost:3000` no navegador.

Paginas:
- `/` - Pagina principal
- `/verify.html` - Verificacao
- `/playbooks.html` - Catalogo
- `/api-docs.html` - Documentacao da API