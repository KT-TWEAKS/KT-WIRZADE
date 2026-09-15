# KT WIRZADE 1.0.1 — Hotfix

Atualização de estabilidade, importação de playbooks, downloads e recuperação.

## Instalar ou atualizar

1. Feche o KT WIRZADE antes de atualizar e aguarde qualquer playbook em execução terminar.
2. Baixe o asset final [`KT-WIRZADE-v1.0.1-win-x64.exe`](https://github.com/KT-TWEAKS/KT-WIRZADE/releases/download/v1.0.1/KT-WIRZADE-v1.0.1-win-x64.exe) na release oficial.
3. Execute o arquivo. As dependências necessárias já estão incorporadas e são preparadas automaticamente em uma pasta temporária privada.
4. A atualização do aplicativo não aplica playbooks automaticamente.

O histórico existente de rollback permanece em `C:\ProgramData\AME\Rollbacks`.

**Quem usa 1.0.0 deve baixar esta atualização manualmente:** aquela versão pode não reconhecer a resposta do GitHub. Depois da instalação da 1.0.1, o verificador identifica versões antigas e baixa diretamente o EXE oficial, conferindo HTTPS, tamanho e SHA-256 antes de salvar.

## Correções e melhorias

- Atualizador interpreta corretamente versão, data, página e arquivos das releases do GitHub.
- Compatibilidade com `!regKey`, `!regValue` e `!powershell`, mantendo os nomes antigos.
- Inclusões YAML rejeitam ciclos, profundidade excessiva, arquivos ausentes, caminhos externos e links de diretório.
- Filtros somente negativos funcionam em tarefas e ações; alternativas positivas mantêm o comportamento anterior.
- Download sem origem ou destino válido apresenta erro antes de alterar arquivos.
- Downloads usam arquivo temporário, verificam tamanho conhecido e preservam o destino anterior em falhas. A ação `!download` aceita `hash` SHA-256 opcional.
- Download do atualizador exige origem oficial HTTPS e SHA-256 informado pelo GitHub.
- Rollback mostra operações não revertidas e permite retomar entradas pendentes, inclusive históricos marcados incorretamente como concluídos pela versão antiga.
- Cada execução transmite explicitamente sua sessão de rollback. Escritas do histórico usam bloqueio entre processos e mesclagem por identificador.
- Falhas durante a comunicação de execução encerram o histórico como falha.
- Sessões IPC usam segredo completo de 256 bits, nomes de pipes derivados sem expor o segredo original e comparação de MAC sem saída antecipada.
- Interface usa material acrílico com transparência e blur também no Windows 10, mantendo contraste nos temas claro e escuro.
- Verificação APBX consulta o catálogo oficial por `ProductCode` e/ou SHA-256, diferencia `verified`, `unverified`, `unknown` e `malicious` e mantém fallback seguro quando a API está indisponível.
- Build e empacotamento verificam versão e recursos incorporados, gerando um único executável, manifesto e `SHA256SUMS.txt`.
- As telas de inicialização, Sobre e Atualizações exibem `v1.0.1` de forma consistente.
- O botão de atualização baixa o EXE final para a pasta Downloads, exibe progresso e abre o arquivo no Explorer após a validação.

## Compatibilidade e limites

Windows 10/11 x64 com .NET Framework 4.8. As novas verificações de includes exigem arquivos dentro de Configuration; playbooks que dependiam de caminhos externos precisam ser ajustados. O limite é de 64 arquivos na pilha de inclusão. Reinicie todos os processos do aplicativo ao atualizar, pois o protocolo IPC foi atualizado.

Rollback não reinstala automaticamente apps/pacotes removidos nem desfaz comandos arbitrários. Operações sem backup aparecem como pendentes; não há promessa de restauração integral. O modo ISO não se associa ao histórico de outra execução.

A verificação de SHA-256 detecta corrupção e divergência em relação ao arquivo publicado pelo GitHub; não substitui uma assinatura Authenticode. O segredo IPC ainda é transmitido aos processos filhos por argumentos: isolamento contra outros processos da mesma conta exige análise adicional.

Validação executada em Windows: compilação do motor/CLI/interface, empacotamento do executável único e conferência dos recursos incorporados concluídos sem erros. A suíte de regressão é mantida no repositório privado de desenvolvimento. O asset publicado é `KT-WIRZADE-v1.0.1-win-x64.exe`, com SHA-256 `db5bb25f848dcb9258479360e84e3d90e128bbe76167d8ac2cc426b4e7559121`. A compilação ainda emite warnings legados de referências e nullable, mas não apresentou erro de build.
