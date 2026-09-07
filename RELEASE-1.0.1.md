# KT WIRZADE 1.0.1

Atualização de estabilidade, importação de playbooks, downloads e recuperação.

## Instalar ou atualizar

1. Feche o KT WIRZADE antes de atualizar e aguarde qualquer playbook em execução terminar.
2. Baixe `KT-WIRZADE-v1.0.1-win-x64.exe` na release oficial.
3. Execute o arquivo. As dependências necessárias já estão incorporadas e são preparadas automaticamente em uma pasta temporária privada.
4. A atualização do aplicativo não aplica playbooks automaticamente.

O histórico existente de rollback permanece em `C:\ProgramData\AME\Rollbacks`.

**Quem usa 1.0.0 deve baixar esta atualização manualmente:** aquela versão pode não reconhecer a resposta do GitHub. A correção do atualizador passa a funcionar na 1.0.1.

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
- Build e empacotamento verificam versão e recursos incorporados, gerando um único executável, manifesto e `SHA256SUMS.txt`.

## Compatibilidade e limites

Windows 10/11 x64 com .NET Framework 4.8. As novas verificações de includes exigem arquivos dentro de Configuration; playbooks que dependiam de caminhos externos precisam ser ajustados. O limite é de 64 arquivos na pilha de inclusão. Reinicie todos os processos do aplicativo ao atualizar, pois o protocolo IPC foi atualizado.

Rollback não reinstala automaticamente apps/pacotes removidos nem desfaz comandos arbitrários. Operações sem backup aparecem como pendentes; não há promessa de restauração integral. O modo ISO não se associa ao histórico de outra execução.

A verificação de SHA-256 detecta corrupção e divergência em relação ao arquivo publicado pelo GitHub; não substitui uma assinatura Authenticode. O segredo IPC ainda é transmitido aos processos filhos por argumentos: isolamento contra outros processos da mesma conta exige análise adicional.

Validação: compilação do motor/CLI/interface, suíte de regressão e inicialização isolada do executável único, sem DLLs ou configuração ao lado. Não foi realizada uma bateria de alterações destrutivas em VM. As fontes da estrutura local 2.0 não fazem parte desta release.
