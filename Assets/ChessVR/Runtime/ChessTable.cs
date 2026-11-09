using System;
using System.Collections.Generic;
using System.Text;
using ChessVR.Core;
using UnityEngine;

namespace ChessVR
{
    [DisallowMultipleComponent]
    public sealed class ChessTable : MonoBehaviour
    {
        private enum Menu { Game, NewGame, Resign, Promotion }

        [SerializeField] private Shader tableShader = null;
        [SerializeField, Range(.68f, 1.05f)] private float boardHeight = .82f;
        [SerializeField, Range(.09f, .115f)] private float squareSize = .105f;
        [SerializeField, Tooltip("FEN opcional para testar posições. Vazio inicia o xadrez tradicional.")]
        private string startingFen = "";

        private TableArt art;
        private BoardView board;
        private MatchPanel panel;
        private TableInput input;
        private ChessGame game;
        private Menu menu;
        private int selected = Square.None;
        private int hoveredSquare = Square.None;
        private TableTarget hoveredButton;
        private List<Move> legal = new();
        private List<Move> promotions = new();
        private bool claiming;
        private bool held;
        private string notice = "";
        private double lastTick;

        internal float BoardHeight => boardHeight;

        private void Awake()
        {
            if (tableShader == null)
            {
                Debug.LogError("Atribua o shader Gambito/Table Surface ao ChessTable.", this);
                enabled = false;
                return;
            }

            art = new TableArt(tableShader);
            ChessRoom.Build(art, transform, boardHeight);
            board = new BoardView(art, transform, boardHeight, squareSize);
            panel = new MatchPanel(art, transform, boardHeight);
            input = new TableInput(this, art, transform);
            StartMatch(0, 0);
        }

        private void Update()
        {
            bool wasFinished = game.Result.Finished;
            double now = Time.realtimeSinceStartupAsDouble;
            game.Elapse(now - lastTick);
            lastTick = now;

            if (!wasFinished && game.Result.Finished)
            {
                input.CancelGrab();
                menu = Menu.Game;
                claiming = false;
                ClearSelection();
                Refresh();
            }
            input.Tick();
            board.Animate();
            panel.UpdateClocks(game);
        }

        private void OnDestroy()
        {
            input?.Dispose();
            art?.Dispose();
        }

        private void StartMatch(double seconds, double increment)
        {
            Position position = null;
            if (!string.IsNullOrWhiteSpace(startingFen))
            {
                try { position = Position.FromFen(startingFen); }
                catch (FormatException exception) { Debug.LogWarning(exception.Message, this); }
            }

            game = new ChessGame(position, seconds, increment);
            input.CancelGrab();
            menu = Menu.Game;
            claiming = false;
            notice = "";
            ClearSelection();
            board.Sync(game.Position, null);
            lastTick = Time.realtimeSinceStartupAsDouble;
            Refresh();
        }

        internal void Hover(TableTarget target)
        {
            TableTarget button = target != null && target.Action != TableAction.Square ? target : null;
            if (hoveredButton != button)
            {
                if (hoveredButton != null)
                    hoveredButton.SetHovered(false);
                hoveredButton = button;
                if (button != null)
                    button.SetHovered(true);
            }

            if (held)
                return;
            int square = target != null && target.Action == TableAction.Square ? target.Square : Square.None;
            if (square != hoveredSquare)
            {
                hoveredSquare = square;
                UpdateBoardHighlights();
            }
        }

        internal void Activate(TableAction action, int square = Square.None)
        {
            if (action == TableAction.Cancel)
            {
                Cancel();
                return;
            }
            if (held)
                return;
            if (action == TableAction.Recenter)
            {
                input.Recenter();
                return;
            }
            if (action == TableAction.NewGame && menu == Menu.Game)
            {
                menu = Menu.NewGame;
                Refresh();
                return;
            }

            switch (menu)
            {
                case Menu.NewGame:
                    if (action == TableAction.Untimed) StartMatch(0, 0);
                    else if (action == TableAction.Rapid) StartMatch(600, 5);
                    else if (action == TableAction.Blitz) StartMatch(300, 0);
                    return;
                case Menu.Resign:
                    if (action == TableAction.Confirm)
                    {
                        game.Resign();
                        FinishAction();
                    }
                    return;
                case Menu.Promotion:
                    ChoosePromotion(action);
                    return;
            }

            if (game.Result.Finished)
                return;

            switch (action)
            {
                case TableAction.Square: SelectOrPlay(square); break;
                case TableAction.Resign: menu = Menu.Resign; Refresh(); break;
                case TableAction.Claim: BeginClaim(); break;
                case TableAction.Offer:
                    if (game.AcceptDraw())
                        FinishAction();
                    else if (game.OfferDraw())
                    {
                        notice = "Oferta armada. Jogue seu lance.";
                        Refresh();
                    }
                    break;
            }
        }

        internal int NearbyPiece(Vector3 point) => board.NearbyPiece(point);

        internal int DirectDropSquare(Vector3 point)
        {
            float height = transform.InverseTransformPoint(point).y - boardHeight;
            return height > -.14f && height < .28f ? board.ProjectToSquare(point) : Square.None;
        }

        internal bool TryHold(int source, Vector3 point)
        {
            if (menu != Menu.Game || held || game.Result.Finished || !Select(source))
                return false;

            held = true;
            board.Hold(source, point);
            return true;
        }

        internal void Drag(Vector3 point, int square)
        {
            if (!held)
                return;
            board.MoveHeld(point);
            if (hoveredSquare != square)
            {
                hoveredSquare = square;
                UpdateBoardHighlights();
            }
        }

        internal void Drop(int square)
        {
            if (!held)
                return;
            held = false;
            board.Release();
            if (square == selected || square == Square.None || !TryDestination(square))
            {
                notice = "Peça devolvida à casa de origem.";
                Refresh();
            }
        }

        internal void CancelHold()
        {
            held = false;
            board.Release();
            ClearSelection();
            Refresh();
        }

        private void Cancel()
        {
            input.CancelGrab();
            menu = Menu.Game;
            claiming = false;
            notice = "";
            ClearSelection();
            Refresh();
        }

        private void BeginClaim()
        {
            if (game.ClaimDraw())
            {
                FinishAction();
                return;
            }

            claiming = game.Position.LegalMoves().Exists(move => game.AvailableClaims(move) != DrawClaim.None);
            notice = claiming ? "Indique o lance que garante o empate." : "Nenhum empate pode ser reivindicado agora.";
            ClearSelection();
            Refresh();
        }

        private void SelectOrPlay(int square)
        {
            if (selected != Square.None && TryDestination(square))
                return;
            if (selected == square)
            {
                ClearSelection();
                notice = "";
                Refresh();
            }
            else if (!Select(square) && selected != Square.None)
            {
                notice = "Escolha uma das casas destacadas.";
                Refresh();
            }
        }

        private bool Select(int square)
        {
            if ((uint)square >= 64)
                return false;
            Piece piece = game.Position.At(square);
            if (piece.Kind == PieceKind.None || piece.Side != game.Position.Turn)
                return false;

            selected = square;
            legal = game.Position.LegalMoves(square);
            if (claiming)
                legal.RemoveAll(move => game.AvailableClaims(move) == DrawClaim.None);
            notice = legal.Count == 0 ? "Esta peça não tem um lance disponível." :
                claiming ? "Escolha a casa para reivindicar o empate." : $"Peça em {Square.Name(square)} · escolha o destino.";
            Refresh();
            return true;
        }

        private bool TryDestination(int square)
        {
            Move first = default;
            int count = 0;
            foreach (Move move in legal)
                if (move.To == square)
                {
                    first = move;
                    count++;
                }

            if (count == 0)
                return false;
            if (count == 1)
                Commit(first);
            else
            {
                promotions = legal.FindAll(move => move.To == square);
                menu = Menu.Promotion;
                Refresh();
            }
            return true;
        }

        private void ChoosePromotion(TableAction action)
        {
            PieceKind kind = action switch
            {
                TableAction.Queen => PieceKind.Queen,
                TableAction.Rook => PieceKind.Rook,
                TableAction.Bishop => PieceKind.Bishop,
                TableAction.Knight => PieceKind.Knight,
                _ => PieceKind.None
            };
            foreach (Move move in promotions)
                if (move.Promotion == kind)
                {
                    Commit(move);
                    return;
                }
        }

        private void Commit(Move move)
        {
            if (claiming ? game.ClaimDraw(move) : game.Play(move))
            {
                if (!claiming)
                    board.Sync(game.Position, move);
                FinishAction();
            }
        }

        private void FinishAction()
        {
            menu = Menu.Game;
            claiming = false;
            notice = "";
            ClearSelection();
            Refresh();
        }

        private void ClearSelection()
        {
            selected = hoveredSquare = Square.None;
            legal.Clear();
            promotions.Clear();
        }

        private void UpdateBoardHighlights()
        {
            board.Highlight(game.Position, game.LastMove, selected, hoveredSquare, legal);
        }

        private void Refresh()
        {
            bool check = game.Position.IsInCheck(game.Position.Turn);
            string title = game.Result.Finished
                ? game.Result.Winner.HasValue ? "Vitória das " + SideName(game.Result.Winner.Value).ToLowerInvariant() : "Empate"
                : SideName(game.Position.Turn) + (check ? " em xeque" : " a jogar");
            string hint = game.Result.Finished ? ResultText(game.Result.Reason) :
                notice.Length > 0 ? notice :
                game.DrawOfferedBy.HasValue ? "O adversário ofereceu empate." :
                game.DrawOfferQueued ? "Oferta armada. Jogue seu lance." : "Aponte e selecione uma peça.";

            for (int i = 0; i < 5; i++)
                panel.SetButton(i, TableAction.Cancel, "");

            switch (menu)
            {
                case Menu.NewGame:
                    hint = "Nova partida · escolha o controle de tempo.";
                    panel.SetButton(0, TableAction.Untimed, "Sem relógio", accent: true);
                    panel.SetButton(1, TableAction.Rapid, "10 min + 5 s");
                    panel.SetButton(2, TableAction.Blitz, "5 min");
                    panel.SetButton(4, TableAction.Cancel, "Voltar");
                    break;
                case Menu.Resign:
                    hint = "Confirmar a desistência das " + SideName(game.Position.Turn).ToLowerInvariant() + "?";
                    panel.SetButton(0, TableAction.Confirm, "Desistir", accent: true);
                    panel.SetButton(4, TableAction.Cancel, "Voltar");
                    break;
                case Menu.Promotion:
                    hint = "Promoção · escolha a nova peça.";
                    panel.SetButton(0, TableAction.Queen, "Dama", accent: true);
                    panel.SetButton(1, TableAction.Rook, "Torre");
                    panel.SetButton(2, TableAction.Bishop, "Bispo");
                    panel.SetButton(3, TableAction.Knight, "Cavalo");
                    panel.SetButton(4, TableAction.Cancel, "Voltar");
                    break;
                default:
                    panel.SetButton(0, TableAction.NewGame, "Nova partida", accent: game.Result.Finished);
                    panel.SetButton(1, TableAction.Offer, game.DrawOfferedBy.HasValue ? "Aceitar empate" : "Ofertar empate",
                        !game.Result.Finished && !claiming && game.History.Count >= 2 && !game.DrawOfferQueued);
                    panel.SetButton(2, claiming ? TableAction.Cancel : TableAction.Claim, claiming ? "Cancelar" : "Reivindicar",
                        !game.Result.Finished, claiming || game.AvailableClaims() != DrawClaim.None);
                    panel.SetButton(3, TableAction.Resign, "Desistir", !game.Result.Finished && !claiming);
                    panel.SetButton(4, TableAction.Recenter, "Recentrar");
                    break;
            }
            hoveredButton = null;
            panel.SetSummary(title, hint, HistoryText(), check || game.Result.Finished);
            panel.UpdateClocks(game);
            UpdateBoardHighlights();
        }

        private string HistoryText()
        {
            int count = game.History.Count;
            if (count == 0)
                return "LANCES   /   Aguardando o primeiro lance";

            var text = new StringBuilder("LANCES   /   ");
            int first = Math.Max(0, count - 8);
            for (int i = first; i < count; i++)
            {
                if (i > first)
                    text.Append("   ");
                text.Append(game.History[i]);
            }
            return text.ToString();
        }

        private static string SideName(Side side) => side == Side.White ? "Brancas" : "Pretas";

        private static string ResultText(EndReason reason) => reason switch
        {
            EndReason.Checkmate => "Xeque-mate.",
            EndReason.Stalemate => "Rei afogado.",
            EndReason.DeadPosition => "Posição morta.",
            EndReason.FivefoldRepetition => "Cinco repetições da mesma posição.",
            EndReason.SeventyFiveMoves => "75 lances por lado sem captura ou movimento de peão.",
            EndReason.ClaimedRepetition => "Reivindicação válida por repetição tripla.",
            EndReason.ClaimedFiftyMoves => "Reivindicação válida pela regra dos 50 lances.",
            EndReason.Agreement => "Empate por acordo.",
            EndReason.Resignation => "Partida encerrada por desistência.",
            EndReason.ResignationDraw => "Desistência sem possibilidade de mate pelo adversário.",
            EndReason.Timeout => "Tempo esgotado.",
            EndReason.TimeoutDraw => "Tempo esgotado sem possibilidade de mate pelo adversário.",
            _ => ""
        };
    }
}
