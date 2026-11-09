using System;
using System.Reflection;
using ChessVR.Core;
#if !CHESSVR_STANDALONE
using NUnit.Framework;
#endif

namespace ChessVR.Tests
{
    public sealed class ChessRulesTests
    {
        [Test]
        public void InitialPositionPerft()
        {
            Position initial = Position.FromFen(Position.InitialFen);
            Equal(Position.InitialFen, initial.ToFen());
            Equal(20L, Perft(initial, 1));
            Equal(400L, Perft(initial, 2));
            Equal(8902L, Perft(initial, 3));
            Equal(197281L, Perft(initial, 4));
        }

        [Test]
        public void CastlingAndPinPerft()
        {
            Position position = Position.FromFen(
                "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1");
            Equal(48L, Perft(position, 1));
            Equal(2039L, Perft(position, 2));
            Equal(97862L, Perft(position, 3));
        }

        [Test]
        public void EndgamePerft()
        {
            Position position = Position.FromFen("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1");
            Equal(14L, Perft(position, 1));
            Equal(191L, Perft(position, 2));
            Equal(2812L, Perft(position, 3));
            Equal(43238L, Perft(position, 4));
        }

        [Test]
        public void CastlingMovesBothPiecesAndRevokesRights()
        {
            var game = Game("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1");
            Play(game, "e1g1", "e8c8");
            Equal(PieceKind.Rook, game.Position.At(Square.Parse("f1")).Kind);
            Equal(PieceKind.Rook, game.Position.At(Square.Parse("d8")).Kind);
            Equal(PieceKind.King, game.Position.At(Square.Parse("c8")).Kind);
            Equal("O-O", game.History[0]);
            Equal("O-O-O", game.History[1]);
            Equal("2kr3r/8/8/8/8/8/8/R4RK1 w - - 2 2", game.Position.ToFen());
        }

        [Test]
        public void CastlingThroughCheckIsForbidden()
        {
            var game = Game("4k3/8/8/8/8/8/5r2/R3K2R w KQ - 0 1");
            True(!game.Play(ReadMove("e1g1")), "O rei não pode atravessar f1 atacada.");
            Play(game, "e1c1");
        }

        [Test]
        public void ReturningRookCannotRestoreRight()
        {
            var game = Game("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1");
            Play(game, "a1a2", "a8a7", "a2a1", "a7a8");
            Equal("r3k2r/8/8/8/8/8/8/R3K2R w Kk - 4 3", game.Position.ToFen());
            True(!game.Play(ReadMove("e1c1")), "O direito de roque longo foi perdido.");
        }

        [Test]
        public void CapturingRookOnItsHomeSquareRevokesRights()
        {
            var game = Game("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1");
            Play(game, "a1a8");
            Equal("R3k2r/8/8/8/8/8/8/4K2R b Kk - 0 1", game.Position.ToFen());
        }

        [Test]
        public void EnPassantCapturesAndExpires()
        {
            var game = new ChessGame();
            Play(game, "e2e4", "a7a6", "e4e5", "d7d5", "e5d6");
            Equal(PieceKind.None, game.Position.At(Square.Parse("d5")).Kind);
            Equal(PieceKind.Pawn, game.Position.At(Square.Parse("d6")).Kind);
            Equal("exd6", game.History[4]);
            Equal(0, game.Position.Halfmoves);

            game = new ChessGame();
            Play(game, "e2e4", "a7a6", "e4e5", "d7d5", "g1f3", "a6a5");
            True(!game.Play(ReadMove("e5d6")), "A captura en passant expira após um lance.");
        }

        [Test]
        public void IllegalEnPassantDoesNotAffectRepetition()
        {
            Position pinned = Position.FromFen("k3r3/8/8/3pP3/8/8/8/4K3 w - d6 0 1");
            Position without = Position.FromFen("k3r3/8/8/3pP3/8/8/8/4K3 w - - 0 1");
            Equal(without.RepetitionKey(), pinned.RepetitionKey());
            True(!pinned.LegalMoves().Contains(ReadMove("e5d6")), "Peão cravado não pode capturar.");

            Position horizontal = Position.FromFen("8/8/8/4KPpr/8/8/8/7k w - g6 0 2");
            True(!horizontal.LegalMoves().Contains(ReadMove("f5g6")), "A captura não pode expor o rei.");
            Position eligible = Position.FromFen("k7/8/8/3pP3/8/8/8/4K3 w - d6 0 1");
            True(eligible.RepetitionKey().EndsWith(" d6"), "Uma captura legal distingue a posição.");
        }

        [Test]
        public void PromotionRequiresAnExplicitValidPiece()
        {
            const string fen = "4k3/1P6/8/8/8/8/8/4K3 w - - 0 1";
            var game = Game(fen);
            int source = Square.Parse("b7");
            int target = Square.Parse("b8");
            Equal(4, game.Position.LegalMoves(source).Count);
            True(!game.Play(new Move(source, target)), "A promoção é obrigatória.");
            foreach (PieceKind piece in new[] { PieceKind.Queen, PieceKind.Rook, PieceKind.Bishop, PieceKind.Knight })
            {
                game = Game(fen);
                True(game.Play(new Move(source, target, piece)), "Promoção legal rejeitada.");
                Equal(piece, game.Position.At(target).Kind);
            }
        }

        [Test]
        public void CheckmateAndStalemateAreDifferent()
        {
            var game = new ChessGame();
            Play(game, "f2f3", "e7e5", "g2g4", "d8h4");
            Equal(EndReason.Checkmate, game.Result.Reason);
            Equal((Side?)Side.Black, game.Result.Winner);
            Equal("Qh4#", game.History[3]);
            True(!game.Play(ReadMove("a2a3")), "A partida terminou.");

            game = Game("7k/5K2/6Q1/8/8/8/8/8 b - - 0 1");
            Equal(EndReason.Stalemate, game.Result.Reason);
            Equal((Side?)null, game.Result.Winner);
        }

        [Test]
        public void DeadMaterialAndLockedPawns()
        {
            foreach (string fen in new[]
            {
                "7k/8/8/8/8/8/8/K7 w - - 0 1",
                "7k/8/8/8/8/8/8/K1N5 w - - 0 1",
                "3b3k/8/8/8/8/8/8/K1B5 w - - 0 1",
                "7k/8/8/p1p1p1p1/P1P1P1P1/8/8/7K w - - 0 1"
            })
                Equal(EndReason.DeadPosition, Game(fen).Result.Reason);

            Equal(EndReason.None, Game("2b4k/8/8/8/8/8/8/K1B5 w - - 0 1").Result.Reason);
            Equal(EndReason.None, Game("7k/8/8/8/8/8/8/K1NN4 w - - 0 1").Result.Reason);
        }

        [Test]
        public void ThreefoldCanBeClaimedBeforeOrAfterTheMove()
        {
            var game = new ChessGame();
            Play(game, "g1f3", "g8f6", "f3g1", "f6g8", "g1f3", "g8f6", "f3g1");
            Equal(DrawClaim.None, game.AvailableClaims());
            Equal(DrawClaim.Repetition, game.AvailableClaims(ReadMove("f6g8")));
            True(game.ClaimDraw(ReadMove("f6g8")), "A terceira ocorrência pode ser reivindicada por intenção.");
            Equal(7, game.History.Count);
            Equal(EndReason.ClaimedRepetition, game.Result.Reason);

            game = new ChessGame();
            RepeatKnights(game, 2);
            True(game.ClaimDraw(), "A posição inicial voltou pela terceira vez.");
            Equal(EndReason.ClaimedRepetition, game.Result.Reason);
        }

        [Test]
        public void FifthOccurrenceEndsGameWithoutAClaim()
        {
            var game = new ChessGame();
            RepeatKnights(game, 3);
            Equal(EndReason.None, game.Result.Reason);
            RepeatKnights(game, 1);
            Equal(EndReason.FivefoldRepetition, game.Result.Reason);
        }

        [Test]
        public void FiftyMoveClaimAndSeventyFiveMoveAutomaticDraw()
        {
            var game = Game("1r5k/8/8/8/8/8/8/K5R1 w - - 99 51");
            Equal(DrawClaim.None, game.AvailableClaims());
            Equal(DrawClaim.FiftyMoves, game.AvailableClaims(ReadMove("g1g2")));
            True(game.ClaimDraw(ReadMove("g1g2")), "O lance pretendido alcança 100 meios-lances.");
            Equal(EndReason.ClaimedFiftyMoves, game.Result.Reason);

            game = Game("1r5k/8/8/8/8/8/8/K5R1 w - - 149 80");
            Play(game, "g1g2");
            Equal(EndReason.SeventyFiveMoves, game.Result.Reason);

            game = Game("7k/8/5KQ1/8/8/8/8/8 w - - 149 80");
            Play(game, "g6g7");
            Equal(EndReason.Checkmate, game.Result.Reason);
        }

        [Test]
        public void DrawOffersAndResignation()
        {
            var game = new ChessGame();
            True(!game.OfferDraw(), "Ambos precisam ter jogado.");
            Play(game, "e2e4", "e7e5");
            True(game.OfferDraw(), "Oferta disponível.");
            True(!game.AcceptDraw(), "A oferta ainda pertence ao jogador atual.");
            Play(game, "g1f3");
            Equal((Side?)Side.White, game.DrawOfferedBy);
            True(game.AcceptDraw(), "As pretas podem aceitar a oferta.");
            Equal(EndReason.Agreement, game.Result.Reason);

            game = new ChessGame();
            Play(game, "e2e4");
            True(game.Resign(), "As pretas podem desistir.");
            Equal(EndReason.Resignation, game.Result.Reason);
            Equal((Side?)Side.White, game.Result.Winner);

            game = Game("8/8/8/6k1/8/5K2/R7/8 w - - 0 1");
            True(game.Resign(), "Desistência deve encerrar a partida.");
            Equal(EndReason.ResignationDraw, game.Result.Reason);
        }

        [Test]
        public void ClockChargesMoverAndAwardsIncrement()
        {
            var game = new ChessGame(initialSeconds: 60, incrementSeconds: 5);
            game.Elapse(7);
            Play(game, "e2e4");
            Equal(58d, game.Remaining(Side.White));
            Equal(60d, game.Remaining(Side.Black));
            game.Elapse(60);
            Equal(EndReason.Timeout, game.Result.Reason);
            Equal((Side?)Side.White, game.Result.Winner);

            game = new ChessGame(Position.FromFen("8/8/8/6k1/8/5K2/R7/8 w - - 0 1"), 1);
            game.Elapse(1);
            Equal(EndReason.TimeoutDraw, game.Result.Reason);
        }

        [Test]
        public void IllegalMoveKeepsPositionClockAndHistory()
        {
            var game = new ChessGame(initialSeconds: 30, incrementSeconds: 5);
            True(!game.Play(ReadMove("e2e5")), "Movimento ilegal rejeitado.");
            True(!game.Play(ReadMove("e7e5")), "O lado errado não pode jogar.");
            Equal(Position.InitialFen, game.Position.ToFen());
            Equal(0, game.History.Count);
            Equal(30d, game.Remaining(Side.White));
        }

        [Test]
        public void AmbiguousSanUsesSourceFile()
        {
            var game = Game("4k3/8/8/8/8/8/8/R3K2R w - - 0 1");
            Play(game, "a1d1");
            Equal("Rd1", game.History[0]);

            game = Game("4k3/8/8/8/8/8/8/R2R3K w - - 0 1");
            Play(game, "a1c1");
            Equal("Rac1", game.History[0]);
        }

        private static ChessGame Game(string fen) { return new ChessGame(Position.FromFen(fen)); }

        private static void RepeatKnights(ChessGame game, int times)
        {
            for (int i = 0; i < times; i++)
                Play(game, "g1f3", "g8f6", "f3g1", "f6g8");
        }

        private static void Play(ChessGame game, params string[] moves)
        {
            foreach (string move in moves)
                True(game.Play(ReadMove(move)), "Lance rejeitado: " + move + " em " + game.Position.ToFen());
        }

        private static Move ReadMove(string move)
        {
            return new Move(Square.Parse(move.Substring(0, 2)), Square.Parse(move.Substring(2, 2)));
        }

        private static long Perft(Position position, int depth)
        {
            if (depth == 0)
                return 1;
            long count = 0;
            foreach (Move move in position.LegalMoves())
            {
                Position next;
                True(position.TryPlay(move, out next), "Gerador devolveu um lance ilegal.");
                count += Perft(next, depth - 1);
            }
            return count;
        }

        private static void Equal<T>(T expected, T actual)
        {
            True(object.Equals(expected, actual), string.Format("Esperado {0}, obtido {1}.", expected, actual));
        }

        private static void True(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

#if CHESSVR_STANDALONE
        [AttributeUsage(AttributeTargets.Method)]
        private sealed class TestAttribute : Attribute { }

        public static int Main()
        {
            int passed = 0, failed = 0;
            var tests = new ChessRulesTests();
            foreach (MethodInfo method in typeof(ChessRulesTests).GetMethods())
            {
                if (!method.IsDefined(typeof(TestAttribute), false))
                    continue;
                try
                {
                    method.Invoke(tests, null);
                    Console.WriteLine("PASS " + method.Name);
                    passed++;
                }
                catch (TargetInvocationException error)
                {
                    Console.Error.WriteLine("FAIL " + method.Name + " - " + error.InnerException.Message);
                    failed++;
                }
            }
            Console.WriteLine("{0} passed, {1} failed", passed, failed);
            return failed == 0 ? 0 : 1;
        }
#endif
    }
}
