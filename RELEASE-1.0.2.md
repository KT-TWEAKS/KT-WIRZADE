# KT WIRZADE 1.0.2 — Update

Atualização que fecha o ciclo 1.0.2: verificação de atualizações mais robusta e diálogo único de Atualizações com mais controle para você.

## Instalar ou atualizar

1. Feche o KT WIRZADE antes de atualizar e aguarde qualquer playbook em execução terminar.
2. Baixe o asset final [`KT-WIRZADE-v1.0.2-win-x64.exe`](https://github.com/KT-TWEAKS/KT-WIRZADE/releases/download/v1.0.2/KT-WIRZADE-v1.0.2-win-x64.exe) na release oficial — ou use o próprio verificador de atualizações da versão 1.0.1.
3. Execute o arquivo. As dependências necessárias já estão incorporadas e são preparadas automaticamente em uma pasta temporária privada.
4. A atualização do aplicativo não aplica playbooks automaticamente.

O histórico existente de rollback permanece em `C:\ProgramData\AME\Rollbacks`.

**Quem usa 1.0.0 deve baixar esta atualização manualmente:** aquela versão pode não reconhecer a resposta do GitHub. A partir de 1.0.1, o próprio programa oferece e baixa a atualização conferindo HTTPS, tamanho e SHA-256 antes de salvar.

## Novidades

- **Tudo em uma janela só**: o aviso na abertura, o botão "Atualizações" da barra de título e o cartão do painel principal agora abrem o mesmo diálogo de Atualizações, com o fluxo completo de download e conferência.
- **Controle "Verificar ao iniciar"**: o diálogo ganhou um checkbox para você escolher se o programa procura atualizações quando abre (a escolha fica salva).
- **Download com segurança reforçada**: barra de progresso em tempo real e conferência de SHA-256 antes de salvar; ao concluir, a pasta de Downloads abre com o arquivo selecionado.
- **Painel e diálogo no seu idioma**: os textos de Atualizações e do painel reagem à troca de idioma (PT-BR/EN) em tempo real.
- Canal estável/pré-release e "Ignorar esta versão" continuam disponíveis no diálogo.

## Correções e melhorias

- Quando o GitHub não publica o hash do arquivo, a conferência usa o `SHA256SUMS.txt` da própria release — continua impossível instalar um arquivo sem hash verificável.
- Limite de requisições do GitHub (HTTP 429) tratado, sem tentar de novo em loop.
- Pré-releases são ordenadas pela data de publicação real (a API lista pela data de criação, e um rascunho publicado depois podia retornar versão antiga).
- Versões com mais de três segmentos (ex.: `1.0.2.1`) são comparadas corretamente; versões malformadas falham fechado, nunca instalam nada por engano.
- Erros reais de download aparecem com o motivo exato, em vez de uma mensagem genérica.
- Problemas de inicialização do verificador de atualizações aparecem dentro da janela (inclusive ao trocar o canal estável/pré-release), em vez de fechar o programa.
- Janela de Rollback: novo botão "Abrir pasta" abre no Explorador de Arquivos a pasta da sessão selecionada, com os logs e backups daquela reversão; os botões do rodapé ganharam dicas ao passar o mouse.
- Se o arquivo APBX de um playbook for removido do computador enquanto o programa está aberto, o programa detecta na hora e limpa a seleção desse playbook — em vez de deixar você seguir no fluxo e falhar com erro confuso no início da aplicação.
- Ignorar a condição de "instalação fresca" agora pede confirmação, explicando o risco de aplicar o playbook num sistema com restos de configurações antigas.
- O aviso de versão não suportada informa se a sua build (ou a da ISO) é do Windows 10 ou 11 e explica a diferença de risco entre famílias distintas e builds mais novas da mesma família.
- Efeito de translucidez (blur) das janelas corrigido no Windows 10: sem mais trilhas fantasma ao arrastar janelas, o blur volta corretamente ao maximizar/restaurar, e — com os "Efeitos de transparência" do Windows desligados — a janela usa a cor sólida do seu tema em vez de ficar branca/achatada.
- Telas de Boas-vindas e Sobre exibem a versão atualizada (`v1.0.2`) em todos os textos, e o cartão do próprio programa no painel passa a ler sempre a versão real do aplicativo, sem risco de divergir.

## Compatibilidade e limites

Windows 10/11 x64 com .NET Framework 4.8. Nenhuma alteração de protocolo IPC nesta versão. Rollback não restaura apps/pacotes removidos nem comandos arbitrários; operações sem backup continuam pendentes no histórico.

A conferência de SHA-256 (campo `digest` da API ou `SHA256SUMS.txt` da release) valida a integridade do arquivo publicado pelo GitHub; não substitui assinatura Authenticode.

## Validação

Compilação do motor/CLI/interface, suíte de regressão (99 verificações), empacotamento do executável único e conferência dos recursos incorporados concluídos sem erros em Windows. Permanecem apenas warnings legados de referências e nullable. O hash conferível do asset oficial está no `SHA256SUMS.txt` publicado junto da release.
