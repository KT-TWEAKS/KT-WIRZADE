class KTAPI {
  constructor(baseURL = '') {
    this.baseURL = baseURL || window.location.origin;
  }

  async verify(productCode, hash) {
    const params = new URLSearchParams();
    if (productCode) params.set('prodID', productCode);
    if (hash) params.set('hash', hash);

    const res = await fetch(`${this.baseURL}/api/verify?${params}`);
    if (!res.ok) throw new Error(`Erro ${res.status}: ${res.statusText}`);
    return res.json();
  }

  async getPlaybooks(search = '', tag = '') {
    const params = new URLSearchParams();
    if (search) params.set('search', search);
    if (tag) params.set('tag', tag);

    const res = await fetch(`${this.baseURL}/api/playbooks?${params}`);
    if (!res.ok) throw new Error(`Erro ${res.status}: ${res.statusText}`);
    return res.json();
  }

  async getPlaybook(id) {
    const res = await fetch(`${this.baseURL}/api/playbooks/${id}`);
    if (!res.ok) throw new Error(`Erro ${res.status}: ${res.statusText}`);
    return res.json();
  }

  async registerPlaybook(data, adminKey) {
    const res = await fetch(`${this.baseURL}/api/playbooks/register`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'X-Admin-Key': adminKey,
      },
      body: JSON.stringify(data),
    });
    const json = await res.json();
    if (!res.ok) throw new Error(json.error || `Erro ${res.status}`);
    return json;
  }
}

window.ktAPI = new KTAPI();