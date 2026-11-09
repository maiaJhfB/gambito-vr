using System.Collections.Generic;
using ChessVR.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChessVR
{
    internal sealed class BoardView
    {
        private sealed class Model
        {
            internal Transform Root;
            internal TableTarget Target;
            internal Piece Piece;
            internal Vector3 Home;
            internal Vector3 Start;
            internal float Arrival;
        }

        private readonly Transform root;
        private readonly PieceModels models;
        private readonly MeshRenderer[] tiles = new MeshRenderer[64];
        private readonly Transform[] dots = new Transform[64];
        private readonly Model[] pieces = new Model[64];
        private readonly Material[] baseTiles;
        private readonly Material[] recentTiles;
        private readonly Material selectedTile;
        private readonly Material captureTile;
        private readonly Material checkTile;
        private readonly float squareSize;
        private int held = Square.None;

        internal BoardView(TableArt art, Transform parent, float height, float squareSize)
        {
            this.squareSize = squareSize;
            root = TableArt.Node(parent, "Tabuleiro", Vector3.up * height);
            models = new PieceModels(art);
            baseTiles = new[] { art.Surface("Casa escura", 0x354c43), art.Surface("Casa clara", 0xd0b388) };
            recentTiles = new[] { art.Surface("Lance anterior escuro", 0x586443), art.Surface("Lance anterior claro", 0xb5aa64) };
            selectedTile = art.Surface("Seleção", 0xeab35c, emission: .14f);
            captureTile = art.Surface("Captura disponível", 0x4f947d, emission: .16f);
            checkTile = art.Surface("Xeque", 0xcb6253, emission: .15f);
            Material dot = art.Surface("Destino disponível", 0x71e1ae, emission: .5f);
            Material trim = art.Surface("Latão escovado", 0xa6784a, .42f, .48f);
            Material frame = art.Surface("Moldura", 0x302823, .4f);

            float width = squareSize * 8;
            art.Block(root, "Base do tabuleiro", new Vector3(0, -.027f, 0),
                new Vector3(width + .11f, .05f, width + .11f), frame);
            art.Block(root, "Filete metálico", new Vector3(0, -.005f, 0),
                new Vector3(width + .035f, .008f, width + .035f), trim);

            Mesh marker = art.Lathe("Marcador", new Vector2(0, 0), new Vector2(.013f, 0),
                new Vector2(.014f, .001f), new Vector2(0, .001f));
            for (int square = 0; square < 64; square++)
            {
                Vector3 center = Center(square);
                Transform tile = TableArt.Node(root, Square.Name(square), center);
                Vector3 size = new(squareSize - .0009f, .01f, squareSize - .0009f);
                tiles[square] = art.Block(tile, "Casa", Vector3.down * .005f, size, baseTiles[Shade(square)]);
                TableTarget.Attach(tile.gameObject, TableAction.Square,
                    new Vector3(squareSize, .012f, squareSize), Vector3.down * .006f).Square = square;

                var mesh = TableArt.Shape(tile, "Destino", Vector3.up * .0008f, marker, dot);
                mesh.shadowCastingMode = ShadowCastingMode.Off;
                dots[square] = mesh.transform;
                dots[square].gameObject.SetActive(false);
            }

            Transform coordinates = TableArt.Canvas(root, "Coordenadas", Vector3.up * .0014f);
            coordinates.localRotation = Quaternion.Euler(90, 0, 0);
            for (int i = 0; i < 8; i++)
            {
                float axis = (i - 3.5f) * squareSize;
                float edge = width * .5f + .035f;
                Coordinate(art, coordinates, ((char)('a' + i)).ToString(), new Vector3(axis, -edge, 0));
                Coordinate(art, coordinates, (i + 1).ToString(), new Vector3(-edge, axis, 0));
            }
        }

        private static void Coordinate(TableArt art, Transform parent, string value, Vector3 position)
        {
            art.Label(parent, "Coordenada " + value, value, position, .025f, TableArt.Tint(0xe1bb83), width: .04f);
        }

        private static int Shade(int square) => (square / 8 + square % 8) & 1;
        private Vector3 Center(int square) => new((square % 8 - 3.5f) * squareSize, 0, (square / 8 - 3.5f) * squareSize);

        internal int ProjectToSquare(Vector3 point)
        {
            Vector3 local = root.InverseTransformPoint(point);
            int file = Mathf.FloorToInt(local.x / squareSize + 4);
            int rank = Mathf.FloorToInt(local.z / squareSize + 4);
            return (uint)file < 8 && (uint)rank < 8 ? rank * 8 + file : Square.None;
        }

        internal int NearbyPiece(Vector3 point)
        {
            int square = ProjectToSquare(point);
            if (square == Square.None || pieces[square] == null)
                return Square.None;
            Vector3 center = root.TransformPoint(Center(square) + Vector3.up * .05f);
            return (point - center).sqrMagnitude < .115f * .115f ? square : Square.None;
        }

        internal void Sync(Position position, Move? move)
        {
            Release();
            for (int square = 0; square < 64; square++)
            {
                Piece piece = position.At(square);
                Model previous = pieces[square];
                if (previous != null && previous.Piece.Kind == piece.Kind && previous.Piece.Side == piece.Side)
                    continue;

                if (previous != null)
                {
                    previous.Root.gameObject.SetActive(false);
                    Object.Destroy(previous.Root.gameObject);
                    pieces[square] = null;
                }
                if (piece.Kind == PieceKind.None)
                    continue;

                Vector3 home = Center(square);
                Vector3 start = home;
                if (move.HasValue)
                {
                    Move played = move.Value;
                    if (played.To == square)
                        start = Center(played.From);
                    else if (piece.Kind == PieceKind.Rook && position.At(played.To).Kind == PieceKind.King &&
                             Mathf.Abs(played.To - played.From) == 2)
                    {
                        int direction = System.Math.Sign(played.To - played.From);
                        if (square == played.From + direction)
                            start = Center(played.From + (direction > 0 ? 3 : -4));
                    }
                }

                Transform rootPiece = TableArt.Node(root, $"{Square.Name(square)} · {piece.Kind}", start);
                models.Build(rootPiece, piece);
                float scale = squareSize / .105f;
                rootPiece.localScale = Vector3.one * scale;
                float height = piece.Kind == PieceKind.Pawn ? .08f : piece.Kind == PieceKind.King ? .15f : .125f;
                TableTarget target = TableTarget.Attach(rootPiece.gameObject, TableAction.Square,
                    new Vector3(.073f, height, .073f), Vector3.up * (height / 2));
                target.Square = square;
                pieces[square] = new Model { Root = rootPiece, Target = target, Piece = piece, Home = home,
                    Start = start, Arrival = start == home ? 0 : Time.unscaledTime + .19f };
            }
        }

        internal void Animate()
        {
            for (int i = 0; i < pieces.Length; i++)
            {
                Model model = pieces[i];
                if (model == null || model.Arrival == 0 || i == held)
                    continue;
                float t = Mathf.Clamp01(1 - (model.Arrival - Time.unscaledTime) / .19f);
                model.Root.localPosition = Vector3.Lerp(model.Start, model.Home, Mathf.SmoothStep(0, 1, t)) +
                                           Vector3.up * (.026f * Mathf.Sin(t * Mathf.PI));
                if (t >= 1)
                    model.Arrival = 0;
            }
        }

        internal void Highlight(Position position, Move? lastMove, int selected, int hovered, List<Move> legal)
        {
            int kingInCheck = position.IsInCheck(position.Turn) ? position.KingSquare(position.Turn) : Square.None;
            for (int square = 0; square < 64; square++)
            {
                bool destination = false;
                foreach (Move move in legal)
                    if (move.To == square)
                    {
                        destination = true;
                        break;
                    }

                bool capture = destination && position.At(square).Kind != PieceKind.None;
                int shade = Shade(square);
                Material surface = baseTiles[shade];
                if (lastMove.HasValue && (lastMove.Value.From == square || lastMove.Value.To == square))
                    surface = recentTiles[shade];
                if (capture || (destination && hovered == square))
                    surface = captureTile;
                if (square == kingInCheck)
                    surface = checkTile;
                if (square == selected)
                    surface = selectedTile;
                tiles[square].sharedMaterial = surface;
                dots[square].gameObject.SetActive(destination && !capture);
            }
        }

        internal void Hold(int square, Vector3 point)
        {
            Model model = pieces[square];
            if (model == null)
                return;
            held = square;
            model.Arrival = 0;
            model.Target.Collider.enabled = false;
            MoveHeld(point);
        }

        internal void MoveHeld(Vector3 point)
        {
            if (held == Square.None)
                return;
            Vector3 local = root.InverseTransformPoint(point);
            local.y = Mathf.Clamp(local.y + .018f, .028f, .5f);
            pieces[held].Root.localPosition = local;
        }

        internal void Release()
        {
            if (held == Square.None)
                return;
            Model model = pieces[held];
            model.Root.localPosition = model.Home;
            model.Target.Collider.enabled = true;
            held = Square.None;
        }
    }
}
