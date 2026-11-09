using System;

namespace ChessVR.Core
{
    public enum Side { White, Black }
    public enum PieceKind { None, Pawn, Knight, Bishop, Rook, Queen, King }

    [Flags]
    internal enum Castling { None = 0, WhiteKing = 1, WhiteQueen = 2, BlackKing = 4, BlackQueen = 8 }

    [Flags]
    public enum DrawClaim { None = 0, Repetition = 1, FiftyMoves = 2 }

    public enum EndReason
    {
        None, Checkmate, Stalemate, DeadPosition, FivefoldRepetition, SeventyFiveMoves,
        ClaimedRepetition, ClaimedFiftyMoves, Agreement, Resignation, ResignationDraw, Timeout, TimeoutDraw
    }

    public struct Piece
    {
        public readonly PieceKind Kind;
        public readonly Side Side;

        internal Piece(int value)
        {
            Kind = (PieceKind)Math.Abs(value);
            Side = value < 0 ? Side.Black : Side.White;
        }
    }

    public struct Move : IEquatable<Move>
    {
        public readonly int From;
        public readonly int To;
        public readonly PieceKind Promotion;

        public Move(int from, int to, PieceKind promotion = PieceKind.None)
        {
            From = from;
            To = to;
            Promotion = promotion;
        }

        public bool Equals(Move other)
        {
            return From == other.From && To == other.To && Promotion == other.Promotion;
        }

        public override bool Equals(object obj)
        {
            return obj is Move && Equals((Move)obj);
        }

        public override int GetHashCode()
        {
            return From | (To << 6) | ((int)Promotion << 12);
        }

        public override string ToString()
        {
            string suffix = Promotion == PieceKind.None ? "" : " pnbrqk"[(int)Promotion].ToString();
            return Square.Name(From) + Square.Name(To) + suffix;
        }
    }

    public static class Square
    {
        public const int None = -1;

        public static int Parse(string name)
        {
            if (name == null || name.Length != 2 || name[0] < 'a' || name[0] > 'h' ||
                name[1] < '1' || name[1] > '8')
                throw new FormatException("Casa inválida: " + name);
            return (name[0] - 'a') + 8 * (name[1] - '1');
        }

        public static string Name(int index)
        {
            if (index < 0 || index >= 64)
                throw new ArgumentOutOfRangeException("index");
            return new string(new[] { (char)('a' + index % 8), (char)('1' + index / 8) });
        }
    }

    public struct GameResult
    {
        public readonly EndReason Reason;
        public readonly Side? Winner;

        internal GameResult(EndReason reason, Side? winner = null)
        {
            Reason = reason;
            Winner = winner;
        }

        public bool Finished { get { return Reason != EndReason.None; } }
    }
}
