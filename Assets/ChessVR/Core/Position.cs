using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ChessVR.Core
{
    public sealed class Position
    {
        public const string InitialFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

        private const int Pawn = (int)PieceKind.Pawn;
        private const int Knight = (int)PieceKind.Knight;
        private const int Bishop = (int)PieceKind.Bishop;
        private const int Rook = (int)PieceKind.Rook;
        private const int Queen = (int)PieceKind.Queen;
        private const int King = (int)PieceKind.King;
        private const string PieceSymbols = " PNBRQK";
        private const string CastleSymbols = "KQkq";

        private static readonly int[,] KnightSteps =
            { { 1, 2 }, { 2, 1 }, { 2, -1 }, { 1, -2 }, { -1, -2 }, { -2, -1 }, { -2, 1 }, { -1, 2 } };
        private static readonly int[,] Rays =
            { { 0, 1 }, { 1, 0 }, { 0, -1 }, { -1, 0 }, { 1, 1 }, { 1, -1 }, { -1, -1 }, { -1, 1 } };

        private readonly int[] board;
        private readonly Castling rights;
        private readonly int whiteKing;
        private readonly int blackKing;

        public Side Turn { get; private set; }
        public int EnPassant { get; private set; }
        public int Halfmoves { get; private set; }
        public int Fullmove { get; private set; }

        private Position(int[] board, Side turn, Castling rights, int enPassant, int halfmoves, int fullmove,
            int whiteKing, int blackKing)
        {
            this.board = board;
            this.rights = rights;
            this.whiteKing = whiteKing;
            this.blackKing = blackKing;
            Turn = turn;
            EnPassant = enPassant;
            Halfmoves = halfmoves;
            Fullmove = fullmove;
        }

        public Piece At(int square) { return new Piece(board[square]); }
        public int KingSquare(Side side) { return side == Side.White ? whiteKing : blackKing; }
        public static Side Opponent(Side side) { return side == Side.White ? Side.Black : Side.White; }
        private static int Sign(Side side) { return side == Side.White ? 1 : -1; }
        private static bool OnBoard(int file, int rank) { return (uint)file < 8 && (uint)rank < 8; }

        public static Position FromFen(string fen)
        {
            if (fen == null)
                throw new ArgumentNullException("fen");
            string[] fields = fen.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length != 6 || (fields[1] != "w" && fields[1] != "b"))
                throw new FormatException("FEN deve ter seis campos e indicar w ou b.");

            string[] ranks = fields[0].Split('/');
            if (ranks.Length != 8)
                throw new FormatException("FEN deve ter oito fileiras.");

            int[] board = new int[64];
            int whiteKing = Square.None, blackKing = Square.None;
            for (int rank = 7; rank >= 0; rank--)
            {
                int file = 0;
                foreach (char symbol in ranks[7 - rank])
                {
                    if (symbol >= '1' && symbol <= '8')
                    {
                        file += symbol - '0';
                        continue;
                    }

                    int kind = PieceSymbols.IndexOf(char.ToUpperInvariant(symbol));
                    if (kind <= 0 || file >= 8 || (kind == Pawn && (rank == 0 || rank == 7)))
                        throw new FormatException("Peça ou casa inválida na FEN.");

                    int square = rank * 8 + file++;
                    bool white = char.IsUpper(symbol);
                    board[square] = white ? kind : -kind;
                    if (kind != King)
                        continue;
                    if (white && whiteKing == Square.None)
                        whiteKing = square;
                    else if (!white && blackKing == Square.None)
                        blackKing = square;
                    else
                        throw new FormatException("FEN deve conter exatamente um rei de cada cor.");
                }
                if (file != 8)
                    throw new FormatException("Cada fileira deve conter oito casas.");
            }

            if (whiteKing == Square.None || blackKing == Square.None)
                throw new FormatException("FEN deve conter exatamente um rei de cada cor.");

            Castling rights = Castling.None;
            if (fields[2] != "-")
            {
                foreach (char symbol in fields[2])
                {
                    int index = CastleSymbols.IndexOf(symbol);
                    if (index < 0 || (rights & (Castling)(1 << index)) != 0)
                        throw new FormatException("Direitos de roque inválidos.");
                    rights |= (Castling)(1 << index);
                }
            }
            ValidateCastle(board, rights, Castling.WhiteKing, 4, 7, 1);
            ValidateCastle(board, rights, Castling.WhiteQueen, 4, 0, 1);
            ValidateCastle(board, rights, Castling.BlackKing, 60, 63, -1);
            ValidateCastle(board, rights, Castling.BlackQueen, 60, 56, -1);

            Side turn = fields[1] == "w" ? Side.White : Side.Black;
            int enPassant = fields[3] == "-" ? Square.None : Square.Parse(fields[3]);
            int sign = Sign(turn);
            if (enPassant != Square.None &&
                (enPassant / 8 != (turn == Side.White ? 5 : 2) ||
                 board[enPassant] != 0 || board[enPassant - sign * 8] != -sign * Pawn ||
                 board[enPassant + sign * 8] != 0))
                throw new FormatException("Alvo en passant inconsistente.");

            int halfmoves, fullmove;
            if (!int.TryParse(fields[4], NumberStyles.None, CultureInfo.InvariantCulture, out halfmoves) ||
                !int.TryParse(fields[5], NumberStyles.None, CultureInfo.InvariantCulture, out fullmove) || fullmove < 1)
                throw new FormatException("Contadores FEN inválidos.");

            var position = new Position(board, turn, rights, enPassant, halfmoves, fullmove, whiteKing, blackKing);
            if (position.IsInCheck(Opponent(turn)))
                throw new FormatException("O rei do lado que acabou de jogar está em xeque.");
            return position;
        }

        private static void ValidateCastle(int[] board, Castling rights, Castling flag, int king, int rook, int sign)
        {
            if ((rights & flag) != 0 && (board[king] != sign * King || board[rook] != sign * Rook))
                throw new FormatException("Direito de roque sem o rei e a torre nas casas iniciais.");
        }

        public string ToFen()
        {
            return LayoutKey(EnPassant) + " " + Halfmoves.ToString(CultureInfo.InvariantCulture) +
                   " " + Fullmove.ToString(CultureInfo.InvariantCulture);
        }

        public string RepetitionKey()
        {
            if (EnPassant != Square.None)
            {
                int sourceRank = EnPassant / 8 - Sign(Turn);
                int file = EnPassant % 8;
                for (int offset = -1; offset <= 1; offset += 2)
                {
                    if (!OnBoard(file + offset, sourceRank))
                        continue;
                    int from = sourceRank * 8 + file + offset;
                    if (board[from] != Sign(Turn) * Pawn)
                        continue;
                    Position next = ApplyUnchecked(new Move(from, EnPassant));
                    if (!next.IsInCheck(Turn))
                        return LayoutKey(EnPassant);
                }
            }
            return LayoutKey(Square.None);
        }

        private string LayoutKey(int enPassant)
        {
            var fen = new StringBuilder(80);
            for (int rank = 7; rank >= 0; rank--)
            {
                int empty = 0;
                for (int file = 0; file < 8; file++)
                {
                    int piece = board[rank * 8 + file];
                    if (piece == 0)
                    {
                        empty++;
                        continue;
                    }
                    if (empty > 0)
                        fen.Append(empty);
                    empty = 0;
                    char symbol = PieceSymbols[Math.Abs(piece)];
                    fen.Append(piece < 0 ? char.ToLowerInvariant(symbol) : symbol);
                }
                if (empty > 0)
                    fen.Append(empty);
                if (rank > 0)
                    fen.Append('/');
            }

            fen.Append(Turn == Side.White ? " w " : " b ");
            if (rights == Castling.None)
                fen.Append('-');
            for (int i = 0; i < CastleSymbols.Length; i++)
                if ((rights & (Castling)(1 << i)) != 0)
                    fen.Append(CastleSymbols[i]);
            fen.Append(' ').Append(enPassant == Square.None ? "-" : Square.Name(enPassant));
            return fen.ToString();
        }

        public bool IsInCheck(Side side)
        {
            return IsAttacked(KingSquare(side), Opponent(side));
        }

        private bool IsAttacked(int target, Side attacker)
        {
            int sign = Sign(attacker);
            int file = target % 8;
            int rank = target / 8;
            for (int df = -1; df <= 1; df += 2)
            {
                if (OnBoard(file + df, rank - sign) && board[(rank - sign) * 8 + file + df] == sign * Pawn)
                    return true;
            }

            for (int i = 0; i < 8; i++)
            {
                int x = file + KnightSteps[i, 0], y = rank + KnightSteps[i, 1];
                if (OnBoard(x, y) && board[y * 8 + x] == sign * Knight)
                    return true;
            }

            for (int i = 0; i < 8; i++)
            {
                int dx = Rays[i, 0], dy = Rays[i, 1];
                int x = file + dx, y = rank + dy;
                bool adjacent = true;
                while (OnBoard(x, y))
                {
                    int piece = board[y * 8 + x] * sign;
                    if (piece != 0)
                    {
                        if (piece == Queen || piece == (i < 4 ? Rook : Bishop) || (piece == King && adjacent))
                            return true;
                        break;
                    }
                    x += dx;
                    y += dy;
                    adjacent = false;
                }
            }
            return false;
        }

        public List<Move> LegalMoves(int source = Square.None)
        {
            var moves = new List<Move>(source == Square.None ? 48 : 12);
            int first = source == Square.None ? 0 : source;
            int end = source == Square.None ? 64 : source + 1;
            if ((uint)first >= 64)
                return moves;

            int sign = Sign(Turn);
            for (int from = first; from < end; from++)
            {
                switch (board[from] * sign)
                {
                    case Pawn: AddPawnMoves(moves, from, sign); break;
                    case Knight: AddSteps(moves, from, sign, KnightSteps, 0, 8, false); break;
                    case Bishop: AddSteps(moves, from, sign, Rays, 4, 8, true); break;
                    case Rook: AddSteps(moves, from, sign, Rays, 0, 4, true); break;
                    case Queen: AddSteps(moves, from, sign, Rays, 0, 8, true); break;
                    case King:
                        AddSteps(moves, from, sign, Rays, 0, 8, false);
                        AddCastles(moves, from, sign);
                        break;
                }
            }

            moves.RemoveAll(move => ApplyUnchecked(move).IsInCheck(Turn));
            return moves;
        }

        private void AddSteps(List<Move> moves, int from, int sign, int[,] steps, int first, int end, bool slide)
        {
            for (int i = first; i < end; i++)
            {
                int dx = steps[i, 0], dy = steps[i, 1];
                int x = from % 8 + dx, y = from / 8 + dy;
                while (OnBoard(x, y))
                {
                    int to = y * 8 + x;
                    int occupant = board[to] * sign;
                    if (occupant <= 0 && occupant != -King)
                        moves.Add(new Move(from, to));
                    if (occupant != 0 || !slide)
                        break;
                    x += dx;
                    y += dy;
                }
            }
        }

        private void AddPawnMoves(List<Move> moves, int from, int sign)
        {
            int rank = from / 8;
            int file = from % 8;
            int nextRank = rank + sign;
            if (board[from + sign * 8] == 0)
            {
                AddPawnDestination(moves, from, from + sign * 8);
                if (rank == (sign > 0 ? 1 : 6) && board[from + sign * 16] == 0)
                    moves.Add(new Move(from, from + sign * 16));
            }
            for (int df = -1; df <= 1; df += 2)
            {
                if (!OnBoard(file + df, nextRank))
                    continue;
                int to = nextRank * 8 + file + df;
                int occupant = board[to] * sign;
                if ((occupant < 0 && occupant != -King) || to == EnPassant)
                    AddPawnDestination(moves, from, to);
            }
        }

        private static void AddPawnDestination(List<Move> moves, int from, int to)
        {
            if (to / 8 == 0 || to / 8 == 7)
            {
                for (int kind = Knight; kind <= Queen; kind++)
                    moves.Add(new Move(from, to, (PieceKind)kind));
            }
            else
                moves.Add(new Move(from, to));
        }

        private void AddCastles(List<Move> moves, int from, int sign)
        {
            if (from != (sign > 0 ? 4 : 60) || IsInCheck(Turn))
                return;

            Castling shortRight = sign > 0 ? Castling.WhiteKing : Castling.BlackKing;
            Castling longRight = sign > 0 ? Castling.WhiteQueen : Castling.BlackQueen;
            Side attacker = Opponent(Turn);
            if ((rights & shortRight) != 0 && board[from + 1] == 0 && board[from + 2] == 0 &&
                !IsAttacked(from + 1, attacker))
                moves.Add(new Move(from, from + 2));
            if ((rights & longRight) != 0 && board[from - 1] == 0 && board[from - 2] == 0 &&
                board[from - 3] == 0 && !IsAttacked(from - 1, attacker))
                moves.Add(new Move(from, from - 2));
        }

        public bool TryPlay(Move move, out Position next)
        {
            next = this;
            if (move.From < 0 || !LegalMoves(move.From).Contains(move))
                return false;
            next = ApplyUnchecked(move);
            return true;
        }

        internal Position ApplyUnchecked(Move move)
        {
            int[] next = (int[])board.Clone();
            int piece = next[move.From];
            int kind = Math.Abs(piece);
            int sign = Sign(Turn);
            bool capture = next[move.To] != 0;
            int white = whiteKing, black = blackKing;
            Castling nextRights = rights;

            next[move.From] = 0;
            next[move.To] = move.Promotion == PieceKind.None ? piece : sign * (int)move.Promotion;
            if (kind == King)
            {
                if (sign > 0)
                {
                    white = move.To;
                    nextRights &= ~(Castling.WhiteKing | Castling.WhiteQueen);
                }
                else
                {
                    black = move.To;
                    nextRights &= ~(Castling.BlackKing | Castling.BlackQueen);
                }
                int shift = move.To - move.From;
                if (Math.Abs(shift) == 2)
                {
                    int rook = shift > 0 ? move.From + 3 : move.From - 4;
                    next[rook] = 0;
                    next[move.From + Math.Sign(shift)] = sign * Rook;
                }
            }

            if (kind == Pawn && move.To == EnPassant)
            {
                next[move.To - sign * 8] = 0;
                capture = true;
            }
            if (kind == Rook)
                nextRights = RemoveRookRight(nextRights, move.From);
            if (Math.Abs(board[move.To]) == Rook)
                nextRights = RemoveRookRight(nextRights, move.To);

            int ep = kind == Pawn && Math.Abs(move.To - move.From) == 16
                ? (move.From + move.To) / 2 : Square.None;
            int half = kind == Pawn || capture ? 0 : Halfmoves + 1;
            return new Position(next, Opponent(Turn), nextRights, ep, half,
                Fullmove + (Turn == Side.Black ? 1 : 0), white, black);
        }

        private static Castling RemoveRookRight(Castling rights, int square)
        {
            switch (square)
            {
                case 0: return rights & ~Castling.WhiteQueen;
                case 7: return rights & ~Castling.WhiteKing;
                case 56: return rights & ~Castling.BlackQueen;
                case 63: return rights & ~Castling.BlackKing;
                default: return rights;
            }
        }
    }
}
