using System;
using System.Collections.Generic;
using ChessVR.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace ChessVR
{
    internal sealed class TableInput : IDisposable
    {
        private sealed class Hand
        {
            internal readonly InputAction Tracked;
            internal readonly InputAction AimPosition;
            internal readonly InputAction AimRotation;
            internal readonly InputAction GripPosition;
            internal readonly InputAction GripRotation;
            internal readonly InputAction Trigger;
            internal readonly InputAction Grip;
            internal readonly LineRenderer RayLine;
            internal bool WasGripping;
            internal bool WasTriggering;

            internal Hand(TableInput input, string usage, Material laser)
            {
                string path = $"<XRController>{{{usage}}}/";
                Tracked = input.Bind(path + "isTracked");
                AimPosition = input.Bind(path + "pointerPosition");
                AimRotation = input.Bind(path + "pointerRotation");
                GripPosition = input.Bind(path + "devicePosition");
                GripRotation = input.Bind(path + "deviceRotation");
                Trigger = input.Bind(path + "trigger", path + "triggerPressed");
                Grip = input.Bind(path + "grip", path + "gripPressed");

                RayLine = TableArt.Node(input.rig, usage + " · raio", Vector3.zero).gameObject.AddComponent<LineRenderer>();
                RayLine.sharedMaterial = laser;
                RayLine.positionCount = 2;
                RayLine.startWidth = .003f;
                RayLine.endWidth = .0012f;
                RayLine.numCapVertices = 3;
                RayLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                RayLine.receiveShadows = false;
                RayLine.enabled = false;
            }
        }

        private readonly ChessTable table;
        private readonly Transform rig;
        private readonly Camera camera;
        private readonly InputAction headTracked;
        private readonly InputAction headPosition;
        private readonly InputAction headRotation;
        private readonly List<XRDisplaySubsystem> displays = new();
        private readonly List<InputAction> actions = new();
        private readonly Hand[] hands;
        private int holdingHand = -1;
        private float grabDistance;
        private bool directGrab;
        private bool usingXr;
        private bool seated;
        private bool recenter;
        private bool desktopSeen;

        internal TableInput(ChessTable table, TableArt art, Transform parent)
        {
            this.table = table;
            rig = TableArt.Node(parent, "Origem OpenXR", Vector3.zero);
            Transform eye = TableArt.Node(rig, "Câmera", Vector3.zero);
            eye.gameObject.tag = "MainCamera";
            camera = eye.gameObject.AddComponent<Camera>();
            eye.gameObject.AddComponent<AudioListener>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = TableArt.Tint(0x152a27);
            camera.nearClipPlane = .035f;
            camera.farClipPlane = 22;
            camera.allowHDR = false;
            camera.allowMSAA = true;

            headTracked = Bind("<XRHMD>/isTracked");
            headPosition = Bind("<XRHMD>/centerEyePosition", "<XRHMD>/devicePosition");
            headRotation = Bind("<XRHMD>/centerEyeRotation", "<XRHMD>/deviceRotation");
            Material left = art.Surface("Raio esquerdo", 0x73e4bf, emission: 1.5f);
            Material right = art.Surface("Raio direito", 0xf3c28a, emission: 1.5f);
            hands = new[] { new Hand(this, "LeftHand", left), new Hand(this, "RightHand", right) };

            SetDesktop();
            Application.onBeforeRender += TrackHead;
        }

        private InputAction Bind(string path, string alternate = null)
        {
            var action = new InputAction(type: InputActionType.Value, binding: path);
            if (alternate != null)
                action.AddBinding(alternate);
            actions.Add(action);
            action.Enable();
            return action;
        }

        private void SetDesktop()
        {
            rig.localPosition = Vector3.zero;
            rig.localRotation = Quaternion.identity;
            camera.transform.localPosition = new Vector3(0, table.BoardHeight + 1.72f, -1.48f);
            camera.transform.LookAt(table.transform.TransformPoint(new Vector3(0, table.BoardHeight + .1f, .105f)));
            camera.fieldOfView = 36;
            camera.stereoTargetEye = StereoTargetEyeMask.None;
            desktopSeen = false;
        }

        internal void Recenter()
        {
            if (usingXr)
                recenter = true;
            else
                SetDesktop();
        }

        internal void CancelGrab()
        {
            if (holdingHand < 0)
                return;
            holdingHand = -1;
            table.CancelHold();
        }

        internal void Tick()
        {
            SubsystemManager.GetSubsystems(displays);
            bool xrActive = false;
            foreach (XRDisplaySubsystem display in displays)
                xrActive |= display.running;

            if (xrActive != usingXr)
            {
                usingXr = xrActive;
                CancelGrab();
                seated = false;
                recenter = false;
                foreach (Hand hand in hands)
                {
                    hand.WasGripping = hand.WasTriggering = false;
                    hand.RayLine.enabled = false;
                }
                if (usingXr)
                {
                    camera.stereoTargetEye = StereoTargetEyeMask.Both;
                    camera.fieldOfView = 60;
                }
                else
                    SetDesktop();
            }

            if (usingXr)
            {
                TrackHead();
                TableTarget left = TickHand(0);
                TableTarget right = TickHand(1);
                table.Hover(right != null ? right : left);
            }
            else
                TickDesktop();
        }

        private void TrackHead()
        {
            if (!usingXr || !headTracked.IsPressed())
                return;
            Vector3 position = headPosition.ReadValue<Vector3>();
            Quaternion rotation = headRotation.ReadValue<Quaternion>();
            if (!seated || recenter)
            {
                Quaternion yaw = Quaternion.Euler(0, -rotation.eulerAngles.y, 0);
                rig.localRotation = yaw;
                rig.localPosition = new Vector3(0, table.BoardHeight + .66f, -.96f) - yaw * position;
                seated = true;
                recenter = false;
            }
            camera.transform.SetLocalPositionAndRotation(position, rotation);
        }

        private void TickDesktop()
        {
            float fov = Camera.VerticalToHorizontalFieldOfView(36, 16f / 9);
            camera.fieldOfView = Mathf.Max(36, Camera.HorizontalToVerticalFieldOfView(fov, camera.aspect));

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame)
                    table.Activate(TableAction.Cancel);
                if (keyboard.nKey.wasPressedThisFrame)
                    table.Activate(TableAction.NewGame);
                if (keyboard.rKey.wasPressedThisFrame)
                    Recenter();
            }

            Mouse mouse = Mouse.current;
            if (mouse == null)
                return;
            TableTarget target = Trace(camera.ScreenPointToRay(mouse.position.ReadValue()), out _);
            table.Hover(target);
            if (desktopSeen && mouse.leftButton.wasPressedThisFrame && target != null)
                table.Activate(target.Action, target.Square);
            if (mouse.rightButton.wasPressedThisFrame)
                table.Activate(TableAction.Cancel);
            desktopSeen = true;
        }

        private TableTarget TickHand(int index)
        {
            Hand hand = hands[index];
            bool trigger = hand.Trigger.IsPressed();
            bool grip = hand.Grip.IsPressed();
            if (!seated || !hand.Tracked.IsPressed())
            {
                hand.RayLine.enabled = false;
                if (holdingHand == index)
                    CancelGrab();
                hand.WasGripping = grip;
                hand.WasTriggering = trigger;
                return null;
            }

            Vector3 gripPoint = rig.TransformPoint(hand.GripPosition.ReadValue<Vector3>());
            bool hasAim = hand.AimPosition.controls.Count > 0 && hand.AimRotation.controls.Count > 0;
            Vector3 origin = hasAim ? rig.TransformPoint(hand.AimPosition.ReadValue<Vector3>()) : gripPoint;
            Quaternion orientation = rig.rotation * (hasAim ? hand.AimRotation : hand.GripRotation).ReadValue<Quaternion>();
            Ray ray = new(origin, orientation * Vector3.forward);
            TableTarget target = Trace(ray, out float distance);

            if (holdingHand == index)
            {
                Vector3 point = directGrab ? gripPoint : ray.GetPoint(grabDistance);
                int drop = directGrab ? table.DirectDropSquare(point) :
                    target != null && target.Action == TableAction.Square ? target.Square : Square.None;
                if (!grip)
                {
                    holdingHand = -1;
                    table.Drop(drop);
                }
                else
                {
                    table.Drag(point, drop);
                    if (directGrab)
                        target = null;
                }
            }
            else if (holdingHand < 0)
            {
                if (grip && !hand.WasGripping)
                {
                    int nearby = table.NearbyPiece(gripPoint);
                    int pointed = target != null && target.Action == TableAction.Square ? target.Square : Square.None;
                    directGrab = nearby != Square.None;
                    int source = directGrab ? nearby : pointed;
                    grabDistance = distance;
                    if (table.TryHold(source, directGrab ? gripPoint : ray.GetPoint(distance)))
                        holdingHand = index;
                }
                else if (trigger && !hand.WasTriggering && target != null)
                    table.Activate(target.Action, target.Square);
            }

            hand.WasGripping = grip;
            hand.WasTriggering = trigger;
            hand.RayLine.enabled = holdingHand != index || !directGrab;
            hand.RayLine.SetPosition(0, origin);
            hand.RayLine.SetPosition(1, ray.GetPoint(distance));
            return target;
        }

        private static TableTarget Trace(Ray ray, out float distance)
        {
            if (Physics.Raycast(ray, out RaycastHit hit, 3f, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore))
            {
                distance = hit.distance;
                return hit.collider.GetComponent<TableTarget>();
            }
            distance = 1.6f;
            return null;
        }

        public void Dispose()
        {
            Application.onBeforeRender -= TrackHead;
            foreach (InputAction action in actions)
                action.Dispose();
        }
    }
}
