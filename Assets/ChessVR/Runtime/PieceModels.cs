using ChessVR.Core;
using UnityEngine;

namespace ChessVR
{
    internal sealed class PieceModels
    {
        private readonly TableArt art;
        private readonly Mesh[] bodies = new Mesh[7];
        private readonly Mesh band;
        private readonly Mesh knightNeck;
        private readonly Mesh knightHead;
        private readonly Mesh knightEar;
        private readonly Mesh finial;
        private readonly Material ivory;
        private readonly Material jade;
        private readonly Material lightInlay;
        private readonly Material darkInlay;

        internal PieceModels(TableArt art)
        {
            this.art = art;
            ivory = art.Surface("Cerâmica marfim", 0xffe6b2, 0.34f);
            jade = art.Surface("Jade escuro", 0x194f52, 0.44f, 0.08f);
            lightInlay = art.Surface("Gravação escura", 0x976841, 0.55f, 0.45f);
            darkInlay = art.Surface("Latão", 0xe5a455, 0.52f, 0.43f);
            band = Profile("Anel", 0, 7, 35, 7, 35, 10, 0, 10);
            finial = Profile("Esfera", 0, -6, 5, -4, 7, 0, 5, 4, 0, 6);
            bodies[(int)PieceKind.Pawn] = Profile("Peão", 0, 0, 31, 0, 34, 5, 34, 10, 29, 14, 21, 18,
                14, 28, 12, 44, 20, 47, 18, 52, 11, 55, 16, 59, 17, 66, 13, 73, 7, 77, 0, 78);
            bodies[(int)PieceKind.Knight] = Profile("Base do cavalo", 0, 0, 31, 0, 34, 5, 34, 10, 29, 14,
                23, 20, 20, 28, 0, 28);
            bodies[(int)PieceKind.Bishop] = Profile("Bispo", 0, 0, 31, 0, 34, 5, 34, 10, 29, 14, 24, 17,
                16, 28, 12, 65, 22, 68, 22, 72, 13, 76, 18, 82, 21, 90, 17, 101, 10, 111, 0, 118);
            bodies[(int)PieceKind.Rook] = Profile("Torre", 0, 0, 31, 0, 34, 5, 34, 10, 29, 14, 24, 19,
                19, 28, 18, 65, 26, 72, 26, 86, 19, 86, 19, 79, 0, 79);
            bodies[(int)PieceKind.Queen] = Profile("Dama", 0, 0, 31, 0, 34, 5, 34, 10, 29, 14, 23, 19,
                15, 29, 12, 75, 24, 81, 25, 85, 14, 90, 17, 97, 24, 113, 16, 111, 12, 105, 0, 105);
            bodies[(int)PieceKind.King] = Profile("Rei", 0, 0, 31, 0, 34, 5, 34, 10, 29, 14, 23, 19,
                16, 28, 13, 82, 24, 86, 24, 91, 15, 95, 21, 105, 22, 115, 16, 121, 0, 121);

            knightNeck = art.Prism("Pescoço", .017f, new Vector2(-.026f, .026f), new Vector2(.016f, .026f),
                new Vector2(.018f, .062f), new Vector2(.008f, .092f), new Vector2(-.018f, .091f),
                new Vector2(-.032f, .062f));
            knightHead = art.Prism("Cabeça", .014f, new Vector2(-.013f, .064f), new Vector2(.028f, .056f),
                new Vector2(.034f, .066f), new Vector2(.017f, .087f), new Vector2(-.01f, .1f));
            knightEar = art.Prism("Orelha", .008f, new Vector2(-.018f, .087f), new Vector2(.001f, .095f),
                new Vector2(-.014f, .111f));
        }

        private Mesh Profile(string name, params int[] dimensions)
        {
            var points = new Vector2[dimensions.Length / 2];
            for (int i = 0; i < points.Length; i++)
                points[i] = new Vector2(dimensions[i * 2] * .001f, dimensions[i * 2 + 1] * .001f);
            return art.Lathe(name, points);
        }

        internal void Build(Transform parent, Piece piece)
        {
            Material body = piece.Side == Side.White ? ivory : jade;
            Material detail = piece.Side == Side.White ? lightInlay : darkInlay;
            TableArt.Shape(parent, "Corpo", Vector3.zero, bodies[(int)piece.Kind], body);
            TableArt.Shape(parent, "Friso", Vector3.up * .0004f, band, detail);

            switch (piece.Kind)
            {
                case PieceKind.Knight:
                    TableArt.Shape(parent, "Pescoço", Vector3.zero, knightNeck, body);
                    TableArt.Shape(parent, "Cabeça", Vector3.zero, knightHead, body);
                    TableArt.Shape(parent, "Orelha", Vector3.zero, knightEar, detail);
                    parent.localRotation = Quaternion.Euler(0, piece.Side == Side.White ? 0 : 180, 0);
                    break;
                case PieceKind.Bishop:
                    TableArt.Shape(parent, "Pomo", Vector3.up * .12f, finial, detail);
                    break;
                case PieceKind.Rook:
                    for (int i = 0; i < 4; i++)
                    {
                        Quaternion rotation = Quaternion.Euler(0, i * 90 + 45, 0);
                        Vector3 center = Vector3.up * .09f + rotation * (Vector3.forward * .018f);
                        art.Block(parent, "Ameia", center, new Vector3(.014f, .012f, .013f), body)
                            .transform.localRotation = rotation;
                    }
                    break;
                case PieceKind.Queen:
                    TableArt.Shape(parent, "Pomo", Vector3.up * .117f, finial, detail);
                    for (int i = 0; i < 5; i++)
                    {
                        Quaternion rotation = Quaternion.Euler(0, i * 72, 0);
                        Vector3 center = Vector3.up * .115f + rotation * (Vector3.forward * .021f);
                        TableArt.Shape(parent, "Pérola", center, finial, body).transform.localScale = Vector3.one * .48f;
                    }
                    break;
                case PieceKind.King:
                    art.Block(parent, "Cruz vertical", Vector3.up * .133f, new Vector3(.009f, .031f, .009f), detail);
                    art.Block(parent, "Cruz horizontal", Vector3.up * .137f, new Vector3(.029f, .008f, .009f), detail);
                    break;
            }
        }
    }
}
