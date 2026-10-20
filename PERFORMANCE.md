# Revisão de peso e desempenho — 20/10/2026

Foram revisados os arquivos C#, XAML, projeto, manifesto e scripts do instalador. O foco foi reduzir cópias de imagens e retenção de recursos sem remover ferramentas, histórico ou qualidade da captura.

## Alterações aplicadas

| Componente | Resultado da revisão |
| --- | --- |
| `ScreenCapture` | Usa o buffer do bitmap com `LockBits` para construir a imagem WPF. Elimina o bitmap GDI adicional criado por `GetHbitmap`. Mantém resolução e captura anterior à abertura do seletor. |
| `DrawingSurface` | Exporta fundo e anotações em um único destino de renderização. Elimina a imagem intermediária de anotações. Adiciona limpeza explícita dos elementos e das pilhas de histórico quando a captura é encerrada. |
| `SelectionOverlay` | Libera referências ao snapshot, editor, ferramentas e histórico no fechamento; remove a referência a essa janela em `Application.MainWindow`. A janela agora é opaca, dispensando o modo de janela transparente, pois o fundo inteiro é uma captura congelada. A camada de escurecimento permanece. |
| `ClipboardService` | Reutiliza o stream PNG durante a cópia persistente, eliminando `ToArray()` e o segundo stream. Mantém os formatos bitmap e PNG. |
| `DebugLog` | Rotaciona o log ao atingir 1 MiB e mantém apenas um arquivo anterior. Reúne mensagem e exceção em uma única escrita. O limite pode ser ultrapassado pelo tamanho da última entrada. |
| `TrayService`, `PrintScreenGuideForm` | Descarta explicitamente menu, ícone e timer. O timer continua existindo apenas enquanto o guia está aberto. |
| Publicação | Adiciona variante compacta com os mesmos recursos, dependente do runtime instalado. Mantém a variante independente e o instalador com runtime incluído. Logs de diagnóstico são excluídos do instalador. |

## Componentes preservados

`App`, `CaptureController`, `HotKeyService` e `NativeMethods` já usam eventos e o atalho nativo do Windows, sem polling. Não foi acrescentado timer, serviço ou tarefa de fundo.

`MainWindow` e seu XAML, `AppIcon`, manifesto e recursos da aplicação foram revisados. Não foi necessário remover o editor alternativo, a escolha de atalho, o guia do Windows, os ícones ou qualquer ferramenta. As geometrias de seta já são congeladas, e os recursos temporários do desenho dos ícones já têm descarte.

Não foram limitados os pontos dos traços, a resolução, o número de anotações nem a profundidade do histórico. Essas alterações poderiam reduzir qualidade ou funcionalidades. Também não foi usada coleta de lixo forçada: as referências são liberadas para que o runtime possa recuperar a memória normalmente.

## Tamanho dos executáveis

| Publicação | Bytes | Aproximação | Requisito |
| --- | ---: | ---: | --- |
| Independente anterior | 71.618.366 | 68,30 MiB | Nenhum runtime separado |
| Independente otimizada | 71.618.658 | 68,30 MiB | Nenhum runtime separado |
| Compacta otimizada | 203.393 | 198,63 KiB | .NET Desktop Runtime 8 x64 |

O código compilado original do aplicativo era de 46.080 bytes (45 KiB). O runtime e as bibliotecas da interface dominam o tamanho da versão independente. Por isso, otimizar o processamento das imagens reduz principalmente o custo durante o uso, sem reduzir significativamente o executável independente.

A variante compacta é cerca de 99,7% menor como arquivo distribuído. Ela usa o runtime compartilhado já instalado; esse tamanho não representa o custo total de instalar o runtime em um computador que ainda não o possui.

Trimming não foi ativado: WPF e Windows Forms têm incompatibilidades documentadas com essa opção. Referência: [Microsoft — incompatibilidades de trimming](https://learn.microsoft.com/dotnet/core/deploying/trimming/incompatibilities).

## Verificação

Foi executado um programa de verificação STA separado, comparando o renderizador anterior com o atual. Ele usou imagens sintéticas, texto, linha, seta, retângulo, elipse e traço com transparência.

- Comparação de 320 × 240 pixels: diferença máxima de 1/255 por canal; 47 de 307.200 canais diferiram. A pequena diferença vem do arredondamento na composição de transparência ao eliminar a imagem intermediária.
- Desfazer, refazer e limpeza do histórico passaram.
- Codificação PNG e preservação de dimensões passaram.
- Captura com leitura dos pixels após descarte do bitmap GDI passou.
- Fechamento do seletor com limpeza do snapshot, fundo e referência `MainWindow` passou.
- Rotação do log em 1 MiB passou.

Na execução final, dez exportações sem anotações de uma imagem Full HD levaram **917 ms antes** e **458 ms depois**, aproximadamente 50% menos tempo nessa operação. É um teste local de exportação após aquecimento; não mede a velocidade geral do aplicativo nem garante esse ganho em outros computadores.

Um bitmap de 32 bits em 3840 × 2160 ocupa 33.177.600 bytes, ou 31,64 MiB. A captura deixa de criar um bitmap GDI desse tamanho, e a exportação deixa de criar uma imagem intermediária desse tamanho quando a seleção é 4K. São economias calculadas dos buffers removidos em etapas distintas, não uma medição da RAM total do processo.

Não foi feita comparação controlada de RAM/CPU da instância instalada do usuário. Ela permaneceu em execução, sem ser substituída. Ainda é necessário validar interativamente cópia/colagem em outros aplicativos, seleção em múltiplos monitores, escala de DPI e ferramentas na nova versão.

Durante a preparação, duas falhas do programa de teste sem tratamento puderam acionar o aviso de erro do Windows. O teste foi corrigido para tratar falhas no terminal e a execução final terminou com sucesso. O programa de teste não faz parte do aplicativo ou do instalador e não salva capturas da tela.

## Onde testar

- `publish\win-x64\QuickShot.exe`: versão independente otimizada.
- `publish\win-x64-compact\QuickShot.exe`: versão compacta otimizada.
- `dist\QuickShot-Setup-1.0.1-x64.exe`: instalador da versão independente.

Encerre o QuickShot atual pela bandeja antes de abrir outra versão. A publicação dos arquivos não atualiza automaticamente a instância instalada.

Para reproduzir as publicações: `build-portable.ps1`, `build-portable.ps1 -Compact` e `installer\build-installer.ps1`. O parâmetro `-NoRestore` do script portátil permite usar os ativos já restaurados.
