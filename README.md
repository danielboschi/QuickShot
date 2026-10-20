# QuickShot

O QuickShot é um aplicativo para Windows que permite capturar uma região da tela, fazer anotações e copiar ou salvar o resultado como PNG. Ele fica na bandeja do sistema e pode ser acionado pela tecla `Print Screen`.

Feito em C#/.NET 8 com WPF, o QuickShot funciona inteiramente no computador: **não coleta dados para envio a terceiros e não se comunica com a internet**.

## Recursos

- Seleção de uma região da tela com o mouse.
- Tela congelada ao iniciar a captura, preservando os tooltips visíveis durante a seleção.
- Anotações com caneta, linha, seta, retângulo, elipse, texto e marca-texto.
- Borracha para remover anotações, além de desfazer e refazer.
- Escolha de cor e espessura do traço.
- Cópia da imagem com anotações para a área de transferência ou gravação em PNG.
- Menu na bandeja para capturar, abrir as configurações locais de Print Screen ou encerrar o aplicativo.

## Privacidade e funcionamento offline

O QuickShot não coleta dados pessoais para análise, publicidade ou rastreamento. Não possui telemetria, contas, servidores, upload, sincronização em nuvem ou atualização automática. O aplicativo e o instalador não fazem requisições de rede e funcionam sem conexão com a internet.

As capturas são processadas localmente, na memória do computador. Ao copiar uma captura, ela é colocada na área de transferência do Windows; ao salvar, o arquivo PNG é gravado no local escolhido por você.

Para diagnóstico, o aplicativo grava um arquivo **local** chamado `quickshot-debug.log`, ao lado do executável. Ao atingir 1 MiB, o log é rotacionado para `quickshot-debug.log.old`, substituindo o anterior. Esses arquivos podem conter horários, eventos de funcionamento, detalhes de erros e caminhos das imagens salvas. Eles não contêm as imagens capturadas e não são enviados pelo QuickShot.

Essas garantias descrevem o comportamento do QuickShot. Recursos do Windows, como a sincronização da área de transferência, e outros programas usados para abrir ou compartilhar as imagens têm configurações próprias.

## Instalação

Na seção [Releases](https://github.com/danielboschi/QuickShot/releases) deste repositório, baixe `QuickShot-Setup-1.0.1-x64.exe` e execute o instalador.

O instalador é destinado a Windows x64 e inclui o runtime .NET: não é necessário instalar o .NET separadamente. Ele instala para o usuário atual, sem exigir privilégios de administrador, cria o atalho do menu Iniciar e oferece opções de atalho na área de trabalho e inicialização automática ao entrar no Windows. A opção de inicialização automática vem marcada e pode ser desmarcada durante a instalação.

Para desinstalar, use a lista de aplicativos instalados do Windows ou o atalho **Desinstalar QuickShot** no menu Iniciar.

## Uso

1. Execute o aplicativo; ele permanece na bandeja do Windows.
2. Pressione `Print Screen` e arraste para selecionar uma região.
3. Anote usando a barra de ferramentas. Pressione `Ctrl+C` para copiar a imagem final ou use o botão de salvar para gravá-la como PNG.

Use `Ctrl+Z` para desfazer, `Ctrl+Shift+Z` para refazer e `Esc` para cancelar ou fechar a captura.

No menu da bandeja é possível iniciar uma captura ou abrir as configurações do Windows para Print Screen. Um guia visual destaca o nome da opção **Usar a tecla Print Screen para abrir a captura de tela**. Para deixar o QuickShot capturar com Print Screen, desative-a. O Windows não oferece nessa tela uma escolha de outro aplicativo padrão para essa tecla.

O seletor `Ctrl+Print Screen` alterna o atalho global do QuickShot entre `Print Screen` e `Ctrl+Print Screen` durante a execução.

## Compilar

A análise de tamanho, otimizações de memória e resultados da verificação estão em [PERFORMANCE.md](PERFORMANCE.md).

Requer o **.NET 8 SDK** no Windows:

```powershell
dotnet build
dotnet run
```

A preparação do ambiente de desenvolvimento pode exigir internet para obter o SDK e os componentes de compilação. Isso não muda o funcionamento offline do aplicativo instalado.

## Gerar instalador

### Pastas de saída

| Pasta | Conteúdo e uso |
| --- | --- |
| `publish\win-x64` | Aplicação portátil em Release, com runtime incluído. Abra `QuickShot.exe` nesta pasta para testar a versão publicada. |
| `publish\win-x64-compact` | Mesmos recursos, sem runtime incluído. Requer .NET Desktop Runtime 8 x64 instalado. Gerada com `build-portable.ps1 -Compact`. |
| `dist` | Instalador gerado a partir de `publish\win-x64` e arquivo de verificação SHA-256. |
| `bin\Debug` | Compilação de desenvolvimento produzida por `dotnet build`. |
| `bin\Release` | Arquivos intermediários da compilação Release; use a aplicação em `publish\win-x64` para testar. |
| `obj` | Arquivos intermediários do .NET; não contém a aplicação para uso. |

Antes de testar uma nova compilação, encerre a instância anterior pelo menu do QuickShot na bandeja do Windows e abra `publish\win-x64\QuickShot.exe`.

Para atualizar somente a aplicação portátil:

```powershell
.\build-portable.ps1
```

Para gerar a variante compacta, use `.\build-portable.ps1 -Compact`. O instalador continua incluindo o runtime para funcionar em computadores sem .NET instalado.

### Instalador

Instale o Inno Setup 6 e execute no PowerShell:

```powershell
.\installer\build-installer.ps1
```

O instalador autocontido para Windows x64 será criado em `dist\QuickShot-Setup-1.0.1-x64.exe`. Ele instala para o usuário atual, cria o atalho do menu Iniciar, oferece um atalho opcional na área de trabalho e marca por padrão a inicialização automática ao entrar no Windows.
