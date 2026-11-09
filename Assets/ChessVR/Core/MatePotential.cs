using System.Collections.Generic;

namespace ChessVR.Core
{
    internal static class MatePotential
    {
        private const int SearchLimit = 4096;

        internal static bool CanPossiblyMate(Position position, Side attacker)
        {
            return !ProvenUnableByMaterial(position, attacker) && !ProvenUnreachable(position, attacker);
        }

        internal static bool IsDead(Position position, List<Move> legalMoves)
        {
            if (ProvenUnableByMaterial(position, Side.White) && ProvenUnableByMaterial(position, Side.Black))
                return true;

            // Search low-branching positions and closed pawn structures. An unfinished search never awards a draw.
            return (legalMoves.Count == 1 || HasLockedPawnStructure(position)) &&
                   ProvenUnreachable(position, null);
        }

        private static bool ProvenUnableByMaterial(Position position, Side attacker)
        {
            int knights = 0, bishops = 0, bishopColors = 0;
            bool anyPawnOrKnight = false;
            bool opponentCanBlockKnight = false;

            for (int square = 0; square < 64; square++)
            {
                Piece piece = position.At(square);
                if (piece.Kind == PieceKind.None || piece.Kind == PieceKind.King)
                    continue;

                if (piece.Kind == PieceKind.Pawn || piece.Kind == PieceKind.Knight)
                    anyPawnOrKnight = true;
                if (piece.Kind == PieceKind.Bishop)
                    bishopColors |= 1 << ((square / 8 + square % 8) & 1);

                if (piece.Side != attacker)
                {
                    if (piece.Kind != PieceKind.Queen)
                        opponentCanBlockKnight = true;
                    continue;
                }

                if (piece.Kind == PieceKind.Pawn || piece.Kind == PieceKind.Rook || piece.Kind == PieceKind.Queen)
                    return false;
                if (piece.Kind == PieceKind.Knight)
                    knights++;
                else
                    bishops++;
            }

            if (bishops > 0)
                return bishopColors != 3 && !anyPawnOrKnight;
            return knights == 0 || (knights == 1 && !opponentCanBlockKnight);
        }

        private static bool HasLockedPawnStructure(Position position)
        {
            bool hasPawn = false;
            for (int square = 0; square < 64; square++)
            {
                Piece piece = position.At(square);
                if (piece.Kind == PieceKind.None || piece.Kind == PieceKind.King)
                    continue;
                if (piece.Kind != PieceKind.Pawn)
                    return false;

                hasPawn = true;
                int step = piece.Side == Side.White ? 8 : -8;
                Piece blocker = position.At(square + step);
                if (blocker.Kind != PieceKind.Pawn || blocker.Side == piece.Side)
                    return false;
            }
            return hasPawn;
        }

        private static bool ProvenUnreachable(Position position, Side? attacker)
        {
            var seen = new HashSet<string> { position.RepetitionKey() };
            var queue = new Queue<Position>();
            queue.Enqueue(position);
            bool closedPawns = HasLockedPawnStructure(position);

            while (queue.Count > 0)
            {
                Position current = queue.Dequeue();
                List<Move> legal = current.LegalMoves();
                if (legal.Count == 0)
                {
                    if (current.IsInCheck(current.Turn) && (!attacker.HasValue || current.Turn != attacker.Value))
                        return false;
                    continue;
                }

                bool cannotMate = attacker.HasValue
                    ? ProvenUnableByMaterial(current, attacker.Value)
                    : ProvenUnableByMaterial(current, Side.White) && ProvenUnableByMaterial(current, Side.Black);
                if (cannotMate)
                    continue;

                if (legal.Count > 1 && (!closedPawns || !HasLockedPawnStructure(current)))
                    return false;

                foreach (Move move in legal)
                {
                    Position next = current.ApplyUnchecked(move);
                    if (!seen.Add(next.RepetitionKey()))
                        continue;
                    if (seen.Count > SearchLimit)
                        return false;
                    queue.Enqueue(next);
                }
            }
            return true;
        }
    }
}
