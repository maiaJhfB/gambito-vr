using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ChessVR.Core
{
    public sealed class ChessGame
    {
        private readonly Dictionary<string, int> occurrences = new Dictionary<string, int>();
        private readonly List<string> history = new List<string>();
        private readonly double[] seconds = new double[2];
        private readonly double increment;
        private bool offerQueued;

        public Position Position { get; private set; }
        public GameResult Result { get; private set; }
        public Side? DrawOfferedBy { get; private set; }
        public bool DrawOfferQueued { get { return offerQueued; } }
        public bool Timed { get; private set; }
        public Move? LastMove { get; private set; }
        public ReadOnlyCollection<string> History { get; private set; }

        public ChessGame(Position position = null, double initialSeconds = 0, double incrementSeconds = 0)
        {
            if (double.IsNaN(initialSeconds) || double.IsInfinity(initialSeconds) || initialSeconds < 0)
                throw new ArgumentOutOfRangeException("initialSeconds");
            if (double.IsNaN(incrementSeconds) || double.IsInfinity(incrementSeconds) || incrementSeconds < 0)
                throw new ArgumentOutOfRangeException("incrementSeconds");

            Position = position ?? Position.FromFen(Position.InitialFen);
            Timed = initialSeconds > 0;
            increment = incrementSeconds;
            seconds[0] = seconds[1] = initialSeconds;
            History = history.AsReadOnly();
            RecordPosition();
            Evaluate();
        }

        public double Remaining(Side side) { return seconds[(int)side]; }

        public void Elapse(double elapsed)
        {
            if (!Timed || Result.Finished || elapsed <= 0 || double.IsNaN(elapsed))
                return;

            Side active = Position.Turn;
            seconds[(int)active] = Math.Max(0, seconds[(int)active] - elapsed);
            if (seconds[(int)active] > 0)
                return;
            Side opponent = Position.Opponent(active);
            bool matePossible = MatePotential.CanPossiblyMate(Position, opponent);
            End(matePossible ? EndReason.Timeout : EndReason.TimeoutDraw, matePossible ? (Side?)opponent : null);
        }

        public bool Play(Move move)
        {
            if (Result.Finished)
                return false;

            Position next;
            if (!Position.TryPlay(move, out next))
                return false;

            Side mover = Position.Turn;
            string san = Notation(move, next);
            if (Timed)
                seconds[(int)mover] += increment;

            history.Add(san);
            LastMove = move;
            Position = next;
            DrawOfferedBy = offerQueued ? (Side?)mover : null;
            offerQueued = false;
            RecordPosition();
            Evaluate();
            return true;
        }

        public DrawClaim AvailableClaims(Move? intended = null)
        {
            if (Result.Finished)
                return DrawClaim.None;
            Position next = Position;
            if (intended.HasValue && !Position.TryPlay(intended.Value, out next))
                return DrawClaim.None;

            DrawClaim claims = DrawClaim.None;
            if (Count(next.RepetitionKey()) + (intended.HasValue ? 1 : 0) >= 3)
                claims |= DrawClaim.Repetition;
            if (next.Halfmoves >= 100)
                claims |= DrawClaim.FiftyMoves;
            return claims;
        }

        public bool ClaimDraw(Move? intended = null)
        {
            DrawClaim claims = AvailableClaims(intended);
            if (claims == DrawClaim.None)
                return false;
            End((claims & DrawClaim.Repetition) != 0 ? EndReason.ClaimedRepetition : EndReason.ClaimedFiftyMoves);
            return true;
        }

        public bool OfferDraw()
        {
            if (Result.Finished || history.Count < 2 || DrawOfferedBy.HasValue || offerQueued)
                return false;
            offerQueued = true;
            return true;
        }

        public bool AcceptDraw()
        {
            if (Result.Finished || !DrawOfferedBy.HasValue || DrawOfferedBy == Position.Turn)
                return false;
            End(EndReason.Agreement);
            return true;
        }

        public bool Resign()
        {
            if (Result.Finished)
                return false;
            Side opponent = Position.Opponent(Position.Turn);
            if (MatePotential.CanPossiblyMate(Position, opponent))
                End(EndReason.Resignation, opponent);
            else
                End(EndReason.ResignationDraw);
            return true;
        }

        private void Evaluate()
        {
            List<Move> legal = Position.LegalMoves();
            if (legal.Count == 0)
            {
                if (Position.IsInCheck(Position.Turn))
                    End(EndReason.Checkmate, Position.Opponent(Position.Turn));
                else
                    End(EndReason.Stalemate);
            }
            else if (MatePotential.IsDead(Position, legal))
                End(EndReason.DeadPosition);
            else if (Count(Position.RepetitionKey()) >= 5)
                End(EndReason.FivefoldRepetition);
            else if (Position.Halfmoves >= 150)
                End(EndReason.SeventyFiveMoves);
        }

        private void End(EndReason reason, Side? winner = null)
        {
            Result = new GameResult(reason, winner);
            DrawOfferedBy = null;
            offerQueued = false;
        }

        private int Count(string key)
        {
            int count;
            occurrences.TryGetValue(key, out count);
            return count;
        }

        private void RecordPosition()
        {
            string key = Position.RepetitionKey();
            occurrences[key] = Count(key) + 1;
        }

        private string Notation(Move move, Position next)
        {
            Piece piece = Position.At(move.From);
            bool capture = Position.At(move.To).Kind != PieceKind.None ||
                           (piece.Kind == PieceKind.Pawn && Position.EnPassant == move.To);
            string text;

            if (piece.Kind == PieceKind.King && Math.Abs(move.To - move.From) == 2)
                text = move.To > move.From ? "O-O" : "O-O-O";
            else
            {
                string source = Square.Name(move.From);
                if (piece.Kind == PieceKind.Pawn)
                    text = capture ? source.Substring(0, 1) : "";
                else
                    text = " PNBRQK"[(int)piece.Kind] + Disambiguation(move, piece.Kind, source);

                text += (capture ? "x" : "") + Square.Name(move.To);
                if (move.Promotion != PieceKind.None)
                    text += "=" + " PNBRQK"[(int)move.Promotion];
            }

            if (next.IsInCheck(next.Turn))
                text += next.LegalMoves().Count == 0 ? "#" : "+";
            return text;
        }

        private string Disambiguation(Move move, PieceKind kind, string source)
        {
            bool ambiguous = false, sameFile = false, sameRank = false;
            foreach (Move other in Position.LegalMoves())
            {
                if (other.From == move.From || other.To != move.To || Position.At(other.From).Kind != kind)
                    continue;
                ambiguous = true;
                sameFile |= other.From % 8 == move.From % 8;
                sameRank |= other.From / 8 == move.From / 8;
            }

            if (!ambiguous)
                return "";
            if (!sameFile)
                return source.Substring(0, 1);
            return sameRank ? source : source.Substring(1, 1);
        }
    }
}
