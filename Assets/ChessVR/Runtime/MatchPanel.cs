using ChessVR.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ChessVR
{
    internal sealed class MatchPanel
    {
        private sealed class Button
        {
            internal TableTarget Target;
            internal Text Label;
        }

        private readonly Text status;
        private readonly Text message;
        private readonly Text clocks;
        private readonly Text history;
        private readonly Button[] buttons = new Button[5];
        private readonly Material panelButton;
        private readonly Material panelHover;
        private readonly Material panelDisabled;
        private readonly Material panelAccent;
        private readonly Color cream = TableArt.Tint(0xf6e7cf);
        private readonly Color green = TableArt.Tint(0x9ce8be);
        private readonly Color muted = TableArt.Tint(0xa5b5aa);
        private string clockLine;

        internal MatchPanel(TableArt art, Transform parent, float tableHeight)
        {
            Transform root = TableArt.Canvas(parent, "Painel da partida", new Vector3(0, tableHeight + .255f, .61f));
            Material housing = art.Surface("Gabinete", 0x0b211f, .28f, emission: .23f);
            Material brass = art.Surface("Linha do painel", 0xd1a363, .4f, .25f, .12f);
            panelButton = art.Surface("Botão", 0x23473d, .26f, emission: .25f);
            panelHover = art.Surface("Botão destacado", 0x417961, .35f, emission: .36f);
            panelDisabled = art.Surface("Botão desabilitado", 0x172925, emission: .13f);
            panelAccent = art.Surface("Botão de ação", 0x805b33, .35f, emission: .24f);

            art.Block(root, "Gabinete", Vector3.zero, new Vector3(1.21f, .462f, .035f), housing);
            art.Block(root, "Linha superior", new Vector3(0, .225f, -.018f), new Vector3(1.21f, .004f, .005f), brass);
            art.Label(root, "Título", "G A M B I T O", new Vector3(-.55f, .177f, -.022f), .039f,
                TableArt.Tint(0xe8bd83), TextAnchor.MiddleLeft);
            art.Label(root, "Edição", "XADREZ  /  VR", new Vector3(.55f, .177f, -.022f), .024f,
                muted, TextAnchor.MiddleRight);

            status = art.Label(root, "Turno", "", new Vector3(-.55f, .103f, -.022f), .043f, cream, TextAnchor.MiddleLeft);
            clocks = art.Label(root, "Relógios", "", new Vector3(.55f, .103f, -.022f), .029f, green, TextAnchor.MiddleRight);
            message = art.Label(root, "Mensagem", "", new Vector3(-.55f, .041f, -.022f), .027f, green, TextAnchor.MiddleLeft);
            art.Block(root, "Divisor", new Vector3(0, .004f, -.018f), new Vector3(1.1f, .001f, .002f), brass);
            history = art.Label(root, "Histórico", "", new Vector3(-.55f, -.039f, -.022f), .027f,
                muted, TextAnchor.MiddleLeft);

            for (int i = 0; i < buttons.Length; i++)
            {
                Vector3 size = new(.208f, .086f, .022f);
                Transform button = TableArt.Node(root, "Botão " + (i + 1),
                    new Vector3((i - 2) * .225f, -.144f, -.032f));
                MeshRenderer renderer = art.Block(button, "Superfície", Vector3.zero, size, panelButton);
                TableTarget target = TableTarget.Attach(button.gameObject, TableAction.NewGame, size, Vector3.zero);
                target.Button = renderer;
                target.Highlight = panelHover;
                buttons[i] = new Button
                {
                    Target = target,
                    Label = art.Label(button, "Rótulo", "", new Vector3(0, 0, -.012f), .025f, cream, width: .198f)
                };
                buttons[i].Label.fontStyle = FontStyle.Bold;
            }
        }

        internal void SetButton(int slot, TableAction action, string label, bool enabled = true, bool accent = false)
        {
            Button button = buttons[slot];
            button.Target.gameObject.SetActive(label.Length > 0);
            button.Target.Action = action;
            button.Target.Collider.enabled = enabled;
            button.Target.Normal = !enabled ? panelDisabled : accent ? panelAccent : panelButton;
            button.Target.SetHovered(false);
            button.Label.text = label;
            button.Label.color = enabled ? cream : muted;
        }

        internal void SetSummary(string title, string hint, string moves, bool urgent)
        {
            status.text = title;
            status.color = urgent ? TableArt.Tint(0xffa591) : cream;
            message.text = hint;
            history.text = moves;
        }

        internal void UpdateClocks(ChessGame game)
        {
            string line = game.Timed ? $"B {Clock(game.Remaining(Side.White))}   ·   P {Clock(game.Remaining(Side.Black))}"
                : "SEM RELÓGIO";
            if (line == clockLine)
                return;
            clockLine = line;
            clocks.text = line;
        }

        private static string Clock(double seconds)
        {
            int rounded = (int)System.Math.Ceiling(seconds);
            return $"{rounded / 60:00}:{rounded % 60:00}";
        }
    }
}
