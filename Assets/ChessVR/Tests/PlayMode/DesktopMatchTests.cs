using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ChessVR.Tests
{
    public sealed class DesktopMatchTests
    {
        [UnityTest]
        public IEnumerator SceneAcceptsLegalDesktopMoveAndRejectsOutOfTurnInput()
        {
            Scene previous = SceneManager.GetActiveScene();
            yield return SceneManager.LoadSceneAsync("Gambito", LoadSceneMode.Additive);
            Scene match = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
            SceneManager.SetActiveScene(match);
#if UNITY_EDITOR
            var editorFocus = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            var background = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                mouse.MakeCurrent();
                yield return null;
                var table = match.GetRootGameObjects()[0].GetComponent<ChessTable>();
                Assert.That(table, Is.Not.Null);
                Transform board = table.transform.Find("Tabuleiro");
                Assert.That(board, Is.Not.Null);
                Camera camera = table.GetComponentInChildren<Camera>();
                Assert.That(camera, Is.Not.Null);

                Transform pawn = board.Find("e2 · Pawn");
                Assert.That(pawn, Is.Not.Null);
                yield return Click(mouse, camera, pawn.position + Vector3.up * .073f, pawn.name);
                yield return Click(mouse, camera, board.Find("e4").position, "e4");
                Assert.That(board.Find("e2 · Pawn"), Is.Null);
                Assert.That(board.Find("e4 · Pawn"), Is.Not.Null);

                pawn = board.Find("d2 · Pawn");
                yield return Click(mouse, camera, pawn.position + Vector3.up * .073f, pawn.name);
                yield return Click(mouse, camera, board.Find("d4").position, "d4");
                Assert.That(board.Find("d2 · Pawn"), Is.Not.Null);
                Assert.That(board.Find("d4 · Pawn"), Is.Null);
            }
            finally
            {
                InputSystem.RemoveDevice(mouse);
                InputSystem.settings.backgroundBehavior = background;
#if UNITY_EDITOR
                InputSystem.settings.editorInputBehaviorInPlayMode = editorFocus;
#endif
                SceneManager.SetActiveScene(previous);
                SceneManager.UnloadSceneAsync(match);
            }
        }

        private static IEnumerator Click(Mouse mouse, Camera camera, Vector3 point, string expectedTarget)
        {
            Physics.SyncTransforms();
            Vector2 position = camera.WorldToScreenPoint(point);
            Assert.That(Physics.Raycast(camera.ScreenPointToRay(position), out RaycastHit hit, 3f), Is.True);
            Assert.That(hit.collider.name, Is.EqualTo(expectedTarget));
            mouse.MakeCurrent();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left));
            yield return null;
            Assert.That(mouse.leftButton.isPressed, Is.True, "O clique sintético deve chegar ao Input System.");
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            yield return null;
        }

    }
}
