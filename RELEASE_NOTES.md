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
