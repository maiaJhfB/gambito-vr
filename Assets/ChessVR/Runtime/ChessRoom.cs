using UnityEngine;
using UnityEngine.Rendering;

namespace ChessVR
{
    internal static class ChessRoom
    {
        internal static void Build(TableArt art, Transform parent, float boardHeight)
        {
            Transform room = TableArt.Node(parent, "Sala e iluminação", Vector3.zero);
            Material wall = art.Surface("Verde mineral", 0x183431, .18f);
            Material column = art.Surface("Revestimento", 0x23433e, .22f);
            Material floor = art.Surface("Piso", 0x172521, .25f);
            Material rug = art.Surface("Tapete", 0x27483e, .09f);
            Material table = art.Surface("Nogueira escura", 0x594234, .42f);
            Material warm = art.Surface("Luz ambiente quente", 0xe3ad73, .34f, emission: 1.3f);
            Material cool = art.Surface("Luz ambiente fria", 0x71c6aa, .34f, emission: 1f);

            art.Block(room, "Piso", new Vector3(0, -.065f, 0), new Vector3(8, .12f, 8), floor);
            art.Block(room, "Tapete", new Vector3(0, 0, -.1f), new Vector3(2.8f, .02f, 3.1f), rug);
            art.Block(room, "Parede do fundo", new Vector3(0, 1.65f, 2.12f), new Vector3(7.4f, 3.3f, .14f), wall);
            for (int i = -4; i <= 4; i++)
                art.Block(room, "Painel de parede", new Vector3(i * .59f, 1.44f, 2.026f),
                    new Vector3(.51f, 2.9f, .07f), column);

            for (int side = -1; side <= 1; side += 2)
            {
                art.Block(room, "Parede lateral", new Vector3(side * 3.25f, 1.65f, 0),
                    new Vector3(.16f, 3.3f, 4.2f), wall);
                art.Block(room, "Luz vertical", new Vector3(side * 1.53f, 1.53f, 1.96f),
                    new Vector3(.013f, 1.84f, .018f), side < 0 ? warm : cool);
            }

            Transform furniture = TableArt.Node(room, "Mesa", Vector3.zero);
            art.Block(furniture, "Tampo", new Vector3(0, boardHeight - .09f, .065f),
                new Vector3(1.54f, .08f, 1.37f), table);
            art.Block(furniture, "Base central", new Vector3(0, boardHeight * .39f, .08f),
                new Vector3(.19f, boardHeight * .78f, .44f), table);
            art.Block(furniture, "Sapata", new Vector3(0, .045f, .08f),
                new Vector3(.77f, .07f, .61f), table);
            art.Block(furniture, "Luz frontal", new Vector3(0, boardHeight - .11f, -.627f),
                new Vector3(.68f, .004f, .012f), warm);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = TableArt.Tint(0x667a71);
            var key = TableArt.Node(room, "Luz principal", new Vector3(-2, 3, -1)).gameObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.transform.rotation = Quaternion.Euler(51, -27, 0);
            key.color = TableArt.Tint(0xffe8cd);
            key.intensity = 1.10f;
            key.shadows = LightShadows.Soft;
            key.shadowStrength = .72f;

            var fill = TableArt.Node(room, "Luz de recorte", new Vector3(1, 2, 1)).gameObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.transform.rotation = Quaternion.Euler(38, 153, 0);
            fill.color = TableArt.Tint(0x8cc9bf);
            fill.intensity = .49f;
        }
    }
}
