# QuickShot 1.1.0

## Novidades

- Tarja preta e opaca para ocultar dados na imagem final.
- Botões para copiar e fechar, com destaque para copiar. `Enter` copia, `Ctrl+S` salva e `Ctrl+Y` refaz.
- Clique em uma janela para capturá-la ou, fora das janelas, no monitor inteiro.
- `R`, ou o menu da bandeja, repete a última região.
- A área escolhida aparece clara durante o arraste, com as medidas em pixels.
- Nova ferramenta para mover anotações e a seleção.
- `Shift` desenha linhas retas, quadrados e círculos.
- Ferramenta, cor e espessura são lembradas. O marca-texto tem estilo próprio, amarelo e grosso por padrão.
- Nova cor amarela.
- Modo privado: mantém as capturas copiadas fora do histórico e da nuvem da área de transferência do Windows.
- Linhas finas ficaram mais fáceis de apagar e mover.

## Memória

Depois de cada captura, o aplicativo devolve a memória das imagens. Na medição com dez capturas na tela, o uso caiu de 278 MiB para 206 MiB após a limpeza, que leva cerca de 30 ms. A maior parte do restante pertence ao driver de vídeo e varia conforme o computador.

## Downloads

- `QuickShot-Setup-1.1.0-x64.exe`: instalador com runtime incluído.
- `QuickShot-Portable-1.1.0-x64.zip`: versão portátil com runtime incluído.
- `QuickShot-Compact-1.1.0-x64.zip`: versão portátil menor; requer .NET Desktop Runtime 10 x64.
- `SHA256SUMS.txt`: hashes SHA-256 dos três downloads.

Encerre a versão anterior pela bandeja antes de instalar ou abrir a nova versão. Os arquivos não têm assinatura digital, e o Windows SmartScreen pode exibir um aviso.

O aplicativo continua funcionando offline, sem telemetria ou envio de capturas.

---
# QuickShot 1.0.3

## Correções e melhorias

- Captura correta em escalas de tela acima de 100%: a imagem não aparece mais ampliada nem cortada.
- Barra, alças, traços e textos acompanham a escala do monitor.
- A instrução inicial aparece no monitor em que está o cursor.
- Menor uso de memória: as bibliotecas do executável não ficam mais expandidas na memória. O arquivo instalado é maior.
- Uma falha ao salvar não danifica mais o arquivo que seria substituído.
- O log passa para `%LOCALAPPDATA%\QuickShot`, sem registrar o local das imagens salvas, e é removido na desinstalação.
- O menu da bandeja não aparece mais na captura iniciada por ele.
- Um clique sem arrastar não cria mais formas invisíveis no histórico de desfazer.
- A janela de ajuda do Print Screen acompanha a escala da tela.
- Verificações automatizadas incluídas no repositório.

## Downloads

- `QuickShot-Setup-1.0.3-x64.exe`: instalador com runtime incluído.
- `QuickShot-Portable-1.0.3-x64.zip`: versão portátil com runtime incluído.
- `QuickShot-Compact-1.0.3-x64.zip`: versão portátil menor; requer .NET Desktop Runtime 10 x64.
- `SHA256SUMS.txt`: hashes SHA-256 dos três downloads.

Encerre a versão anterior pela bandeja antes de instalar ou abrir a nova versão. Os arquivos não têm assinatura digital, e o Windows SmartScreen pode exibir um aviso.

## Validação

Testado na tela real com um monitor em 125% e outro em 150%, inclusive com seleções entre os dois. As escalas de 100% a 300% passaram em verificações automatizadas.

O aplicativo continua funcionando offline, sem telemetria ou envio de capturas.

---

# QuickShot 1.0.2

## Correções e melhorias

- A seleção pode ser ajustada arrastando as bordas ou os cantos, sem deslocar as anotações.
- Novo ícone do aplicativo e barra de ferramentas redesenhada, com ícones vetoriais, ferramenta ativa destacada e cores em amostras.
- Primeira captura mais rápida: o aplicativo se prepara em segundo plano ao iniciar.
- O texto é exportado sem moldura e pode ser reeditado ou apagado com a borracha.
- Durante a digitação, `Ctrl+Z` e `Esc` agem sobre o texto e `Ctrl+Enter` confirma. `Ctrl+C` copia o trecho selecionado ou, sem seleção, a captura.
- A barra de ferramentas permanece dentro do monitor da seleção.
- Uma falha de captura avisa na bandeja e não impede as capturas seguintes.
- Aviso na bandeja quando `Print Screen` está em uso por outro programa.
- Apenas uma instância por sessão do Windows.
- Atualização do .NET 8, sem suporte desde 10/11/2026, para o .NET 10. O instalador continua incluindo o runtime.

- Remoção do editor antigo, que não era acessível, e da opção `Ctrl+Print Screen` que só existia nele.

## Downloads

- `QuickShot-Setup-1.0.2-x64.exe`: instalador com runtime incluído.
- `QuickShot-Portable-1.0.2-x64.zip`: versão portátil com runtime incluído.
- `QuickShot-Compact-1.0.2-x64.zip`: versão portátil menor; requer .NET Desktop Runtime 10 x64.
- `SHA256SUMS.txt`: hashes SHA-256 dos três downloads.

Encerre a versão anterior pela bandeja antes de instalar ou abrir a nova versão. Os arquivos não têm assinatura digital, e o Windows SmartScreen pode exibir um aviso.

## Limitações conhecidas

Escalas de tela acima de 100% e monitores com escalas diferentes ainda não foram validados.

O aplicativo continua funcionando offline, sem telemetria ou envio de capturas.

---

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

Compilação Release e geração do instalador concluídas. Passaram as verificações de captura, exportação PNG, equivalência visual das anotações, desfazer/refazer, limpeza de referências e rotação do log. Detalhes e limites das medições estão em [PERFORMANCE.md](https://github.com/danielboschi/QuickShot/blob/v1.0.1/PERFORMANCE.md), na versão 1.0.1.

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
