# Gambito VR

Xadrez tradicional em uma sala 3D, em português, para duas pessoas que alternam os turnos no mesmo dispositivo. O projeto usa Unity 6.3 LTS, Input System e OpenXR. No Windows, a mesma cena pode ser jogada com mouse ou com um headset OpenXR conectado ao PC.

## Abrir o projeto

1. No Unity Hub, instale o **Unity 6.3 LTS 6000.3.26f1**. A primeira importação precisa de acesso ao registro de pacotes do Unity. [Versão no arquivo oficial](https://unity.com/releases/editor/whats-new/6000.3.26f1)
2. Em **Projects > Add > Add project from disk**, escolha a pasta que contém este README, `Assets`, `Packages` e `ProjectSettings`.
3. Quando o Editor abrir na cena **Gambito**, pressione **Play**. Se estiver em outra cena, use **Gambito > Abrir sala**. A cena também está em `Assets/ChessVR/Scenes/Gambito.unity`.

O modo desktop é ativado quando o projeto não encontra uma sessão OpenXR. Para trabalhar exclusivamente no desktop e evitar mensagens de runtime XR ausente no Console, desmarque **Initialize XR on Startup** em **Edit > Project Settings > XR Plug-in Management**. Reative a opção para testar com headset.

## Jogar

| Ação | Desktop | VR |
| --- | --- | --- |
| Escolher peça, casa ou botão | Clique esquerdo | Aponte e pressione o gatilho |
| Pegar e soltar peça | Clique na origem e depois no destino | Aperte o grip junto da peça ou apontando para ela. Solte sobre o destino |
| Cancelar seleção ou menu | Clique direito ou **Esc** | Aponte para **Voltar** ou selecione a peça novamente |
| Abrir o menu de partida | **N** ou **Nova partida** | **Nova partida** |
| Recentrar a posição | **R** ou **Recentrar** | Olhe para a frente e acione **Recentrar** |

As brancas começam. O tabuleiro marca a peça selecionada, as casas legais, o último lance e o rei em xeque. Para fazer roque, mova o rei duas casas. Na promoção, escolha dama, torre, bispo ou cavalo. Uma soltura ilegal devolve a peça à origem.

**Nova partida** permite jogar sem relógio, em **10 min + 5 s** de incremento ou em **5 min**. **Ofertar empate** envia a oferta junto com o próximo lance. O adversário pode aceitar pelo painel ou continuar jogando. **Reivindicar** verifica repetição tripla e 50 lances. Quando o direito depende do próximo lance, o painel permite indicá-lo antes de jogá-lo. Cinco repetições e 75 lances encerram a partida automaticamente. Xeque-mate tem prioridade no último lance.

O motor reconhece insuficiência de material, algumas posições fechadas de peões e sequências forçadas. A busca por mate possível tem limite e pode deixar posições mortas raras sem adjudicação automática, inclusive na decisão de empate por tempo ou desistência. A referência de regras é o capítulo de xadrez tradicional da FIDE. [Leis do Xadrez](https://handbook.fide.com/chapter/e012023)

## Configurar o headset e gerar o jogo

O alvo deste setup é **Windows x64 com OpenXR**, incluindo uso de Meta Quest como headset de PC via Link. O projeto já referencia o carregador OpenXR, Direct3D 11 e os perfis Oculus Touch, Valve Index, HTC Vive e Microsoft Motion.

1. No aplicativo do headset ou no SteamVR, defina o runtime OpenXR ativo e conecte o headset ao PC. Abra a sessão VR antes de pressionar **Play**.
2. Confira a aba Standalone em **Edit > Project Settings > XR Plug-in Management > Project Validation**. No Unity, a mesma câmera atende o modo desktop e a sessão estéreo.
3. Use **Gambito > Gerar build para Windows x64**. A saída fica em `Builds/Windows/GambitoVR.exe`. Mantenha a pasta de dados junto do executável.

Uma build Android executada diretamente no Quest exige outro alvo e configuração da plataforma. [Configuração do OpenXR](https://docs.unity3d.com/Packages/com.unity.xr.openxr@1.16/manual/index.html)

## Desenvolvimento e testes

- `Assets/ChessVR/Core` contém posição imutável, geração de lances legais, FEN, notação algébrica, adjudicação e relógios. A montagem é C# puro e independe do Unity.
- `Assets/ChessVR/Runtime` contém a cena em execução, as peças, o painel e a entrada. `ChessTable` é o componente de entrada da cena. Altura, tamanho das casas e FEN inicial podem ser ajustados no Inspector. A sala, o tabuleiro e as peças são montados em **Play**, a partir de malhas e materiais gerados localmente.
- `Assets/XR` contém as configurações que acompanham o projeto. `Packages/manifest.json` fixa as dependências diretas.
- `Assets/ChessVR/Tests/EditMode` contém testes para os resultados da partida e para a contagem de lances legais em três posições de referência. `Assets/ChessVR/Tests/PlayMode` verifica a interação por mouse na cena. Execute as suítes em **Window > General > Test Runner**. No Windows, também é possível testar o motor sem abrir o Editor usando `powershell -ExecutionPolicy Bypass -File .\Scripts\Test-Core.ps1`.

Para testar uma promoção rapidamente, coloque `4k3/1P6/8/8/8/8/8/4K3 w - - 0 1` no campo **Starting Fen** do componente `ChessTable` e jogue de **b7** para **b8**. Limpe o campo para voltar à posição tradicional.
