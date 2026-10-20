# QuickShot 1.0.1

## Correções e melhorias

- A seleção mostra a tela congelada no momento do Print Screen, incluindo os tooltips visíveis.
- Menos cópias temporárias de imagens durante a captura, exportação e cópia para a área de transferência.
- Liberação de referências às imagens e ao histórico ao fechar a captura.
- Exportação cerca de 50% mais rápida no teste local de dez exportações Full HD; o resultado varia conforme o computador e a captura.
- Rotação do log ao atingir 1 MiB, mantendo um arquivo anterior.
- Descarte explícito de ícone, menu e timer do guia de configuração.
- Nova versão portátil compacta com os mesmos recursos, para computadores com .NET Desktop Runtime 8 x64 instalado.

## Downloads

- `QuickShot-Setup-1.0.1-x64.exe`: instalador com runtime incluído.
- `QuickShot-Portable-1.0.1-x64.zip`: versão portátil com runtime incluído.
- `QuickShot-Compact-1.0.1-x64.zip`: versão portátil compacta; requer .NET Desktop Runtime 8 x64.
- `SHA256SUMS.txt`: hashes SHA-256 dos três downloads.

Encerre a versão anterior pela bandeja antes de instalar ou abrir a nova versão. As ferramentas de anotação, cópia e salvamento continuam disponíveis.

## Validação

Compilação Release e geração do instalador concluídas. Passaram as verificações de captura, exportação PNG, equivalência visual das anotações, desfazer/refazer, limpeza de referências e rotação do log. Detalhes e limites das medições estão em `PERFORMANCE.md`.

O aplicativo continua funcionando offline, sem telemetria ou envio de capturas.

---

# QuickShot 1.0.0

Aplicativo para Windows x64 que captura regiões da tela e permite adicionar anotações antes de copiar a imagem ou salvá-la em PNG.

## Recursos

- Captura pela tecla Print Screen ou pelo menu na bandeja.
- Caneta, linhas, setas, retângulos, elipses, texto, marca-texto e borracha.
- Ajuste de cor e espessura, com desfazer e refazer.
- Cópia para a área de transferência e exportação em PNG.

## Privacidade

O QuickShot não coleta dados pessoais para análise ou rastreamento e não se comunica com a internet. Não há contas, telemetria, upload, serviços em nuvem ou atualização automática. As imagens são processadas localmente.

O único registro de diagnóstico é o arquivo local `quickshot-debug.log`, que pode conter eventos, erros e caminhos de arquivos salvos. Nenhum registro é enviado pelo aplicativo.

## Instalação

Baixe o arquivo `QuickShot-Setup-1.0.0-x64.exe` anexado a esta release e execute-o. O instalador inclui o runtime .NET e instala para o usuário atual, sem exigir privilégios de administrador.

A inicialização automática ao entrar no Windows é opcional e vem marcada no instalador. Desmarque-a se preferir iniciar o QuickShot manualmente.

Se o Windows interceptar Print Screen, abra **Configurar Print Screen no Windows…** no menu da bandeja, desative **Usar a tecla Print Screen para abrir a captura de tela** e reinicie o QuickShot.

O arquivo `SHA256SUMS.txt` permite conferir a integridade do instalador.
