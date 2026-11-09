using UnityEngine;

namespace ChessVR
{
    internal enum TableAction
    {
        Square, NewGame, Untimed, Rapid, Blitz, Resign, Claim, Offer, Recenter, Cancel,
        Confirm, Queen, Rook, Bishop, Knight
    }

    [RequireComponent(typeof(BoxCollider))]
    internal sealed class TableTarget : MonoBehaviour
    {
        internal TableAction Action;
        internal int Square;
        internal MeshRenderer Button;
        internal Material Normal;
        internal Material Highlight;
        internal BoxCollider Collider;

        internal static TableTarget Attach(GameObject target, TableAction action, Vector3 size, Vector3 center)
        {
            var result = target.AddComponent<TableTarget>();
            result.Action = action;
            result.Collider = target.GetComponent<BoxCollider>();
            result.Collider.size = size;
            result.Collider.center = center;
            return result;
        }

        internal void SetHovered(bool hovered)
        {
            if (Button != null)
                Button.sharedMaterial = hovered ? Highlight : Normal;
        }
    }
}
