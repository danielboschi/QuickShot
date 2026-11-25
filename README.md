# QuickShot

O QuickShot é um aplicativo para Windows que captura uma região da tela, permite fazer anotações e copiar ou salvar o resultado como PNG. Ele fica na bandeja do sistema e é acionado pela tecla `Print Screen`.

Feito em C#/.NET 10 com WPF, funciona inteiramente no computador: **não coleta dados para envio a terceiros e não se comunica com a internet**.

## Recursos

- Seleção por arraste, por clique em uma janela ou em um monitor, ou repetindo a última região.
- Seleção ajustável pelas bordas e pelos cantos, que também pode ser movida, com as medidas em pixels.
- Tela congelada ao iniciar a captura, preservando os tooltips visíveis.
- Caneta, linha, seta, retângulo, elipse, texto reeditável e marca-texto.
- **Tarja preta e opaca para ocultar dados**, aplicada à imagem final.
- Anotações que podem ser movidas, borracha, desfazer e refazer.
- Seis cores e espessura ajustável; a ferramenta, a cor e a espessura são lembradas.
- Cópia para a área de transferência ou gravação em PNG.
- **Modo privado**, que mantém as capturas fora do histórico e da nuvem da área de transferência do Windows.
- Suporte a escalas de tela e a monitores com escalas diferentes.
## Instalação

Na seção [Releases](https://github.com/danielboschi/QuickShot/releases/latest), baixe uma das opções para Windows x64:

| Arquivo | Uso |
| --- | --- |
| `QuickShot-Setup-<versão>-x64.exe` | Instalador, recomendado. Inclui o runtime .NET. |
| `QuickShot-Portable-<versão>-x64.zip` | Versão portátil. Inclui o runtime .NET. |
| `QuickShot-Compact-<versão>-x64.zip` | Versão portátil menor. Requer o .NET Desktop Runtime 10 x64 instalado. |
| `SHA256SUMS.txt` | Hashes SHA-256 para conferir os downloads. |

O instalador atende o usuário atual, sem privilégios de administrador, e cria o atalho do menu Iniciar. O atalho na área de trabalho é opcional; a inicialização com o Windows vem marcada e pode ser desmarcada.

Os arquivos não têm assinatura digital, e o Windows SmartScreen pode exibir um aviso ao abri-los.

Para atualizar, encerre o QuickShot pela bandeja antes de instalar a nova versão. Para desinstalar, use a lista de aplicativos instalados do Windows ou o atalho **Desinstalar QuickShot** no menu Iniciar.

## Uso

1. Execute o aplicativo; ele permanece na bandeja do Windows.
2. Pressione `Print Screen`. Arraste para selecionar uma região ou clique em uma janela; fora das janelas, o clique seleciona o monitor.
3. Se precisar, arraste as bordas ou os cantos para ajustar a seleção. As anotações permanecem no lugar.
4. Anote usando a barra de ferramentas.
5. Pressione `Enter` ou `Ctrl+C` para copiar, ou `Ctrl+S` para salvar como PNG.

| Atalho | Ação |
| --- | --- |
| `Print Screen` | Iniciar uma captura |
| `R` | Repetir a última região, antes de selecionar |
| `Enter` ou `Ctrl+C` | Copiar a captura e fechar |
| `Ctrl+S` | Salvar como PNG |
| `Ctrl+Z` / `Ctrl+Y` | Desfazer / refazer (`Ctrl+Shift+Z` também refaz) |
| `Shift` ao desenhar | Linhas retas, quadrados e círculos |
| `Esc` | Cancelar ou fechar a captura |

**Mover:** com a primeira ferramenta da barra, arraste uma anotação para movê-la ou qualquer outro ponto para mover a seleção.

**Ocultar dados:** use a tarja. Ela é sempre preta e opaca, sem desfoque, e a imagem copiada ou salva não guarda o conteúdo coberto. Confira se a tarja cobre tudo antes de compartilhar.

**Ao digitar um texto:** clique fora da caixa ou pressione `Ctrl+Enter` para confirmar e `Esc` para cancelar a digitação. `Enter` quebra a linha e `Ctrl+Z` age sobre o texto. `Ctrl+C` copia o trecho selecionado ou, sem seleção, a captura. Para reeditar, clique no texto com a mesma ferramenta.

### Modo privado

No menu da bandeja, marque **Modo privado** para pedir ao Windows que as capturas copiadas não entrem no histórico (`Win+V`) nem na sincronização em nuvem. A colagem normal não muda. Outros programas que monitoram a área de transferência podem não respeitar esse pedido.
### Se o Print Screen não funcionar

Outro programa ou o próprio Windows pode estar usando a tecla. Nesse caso, o QuickShot avisa na bandeja ao iniciar. No menu da bandeja, abra **Configurar Print Screen no Windows…**, desative **Usar a tecla Print Screen para abrir a captura de tela** e reinicie o QuickShot. Enquanto isso, é possível capturar com dois cliques no ícone.

### Observação

- Apenas uma instância é executada por sessão do Windows.

## Privacidade e funcionamento offline

O QuickShot não coleta dados pessoais para análise, publicidade ou rastreamento. Não possui telemetria, contas, servidores, upload, sincronização em nuvem ou atualização automática. O aplicativo e o instalador não fazem requisições de rede.

As capturas são processadas na memória do computador. Ao copiar, a imagem vai para a área de transferência do Windows; ao salvar, o PNG é gravado no local escolhido.

Para diagnóstico, o aplicativo grava o arquivo **local** `%LOCALAPPDATA%\QuickShot\quickshot-debug.log`. Ao atingir 1 MiB, ele é rotacionado para `quickshot-debug.log.old`. Esses arquivos podem conter horários, eventos e detalhes de erros. Não contêm as imagens nem o local em que foram salvas.

As preferências ficam em `%LOCALAPPDATA%\QuickShot\settings.ini`: ferramenta, cores, espessuras, modo privado e as coordenadas da última região. Para permitir o clique em janelas, o aplicativo lê a posição delas, mas não os títulos nem o conteúdo. Nada disso é enviado, e o desinstalador remove esses arquivos.

Recursos do Windows, como o histórico e a sincronização da área de transferência, e outros programas usados com as imagens têm configurações próprias.

## Compilar

Requer o **.NET 10 SDK** no Windows:

```powershell
dotnet build
dotnet run
```

Preparar o ambiente pode exigir internet para obter o SDK e os pacotes de compilação. Isso não muda o funcionamento offline do aplicativo.

| Comando | Resultado |
| --- | --- |
| `.\build-portable.ps1` | `publish\win-x64\QuickShot.exe`, com runtime incluído |
| `.\build-portable.ps1 -Compact` | `publish\win-x64-compact\QuickShot.exe`, sem runtime |
| `.\installer\build-installer.ps1` | Instalador em `dist`, com o hash em `SHA256SUMS.txt`. Requer o Inno Setup 6 |
| `dotnet run --project tests` | Executa as verificações. Com `memory` ou `startup`, mede memória ou tempo de abertura. Com `screen`, testa na tela real e move o mouse por alguns segundos |
| `.\Assets\build-icon.ps1` | Regenera `Assets\QuickShot.ico` |

Encerre o QuickShot pela bandeja antes de abrir uma nova compilação.
