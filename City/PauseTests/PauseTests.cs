using System;
using System.Collections;
using LifeFromZero.City.Pause;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace LifeFromZero.City.Pause.Tests
{
    public sealed class TestIntegration : PauseIntegration
    {
        public bool allowed = true, locked, available;
        public int executed;
        public PauseAction last;
        public override bool CanOpenPause() => allowed;
        public override IDisposable AcquireGameplayLock() { locked = true; return new Lock(this); }
        public override bool IsAvailable(PauseAction action) => available;
        public override void Execute(PauseAction action) { executed++; last = action; }
        private sealed class Lock : IDisposable
        {
            private TestIntegration owner;
            public Lock(TestIntegration value) { owner = value; }
            public void Dispose() { owner.locked = false; }
        }
    }
    public sealed class TestPlayerInput : MonoBehaviour { }
    public sealed class PauseTests
    {
        private GameObject root;
        private PauseController pause;
        private TestIntegration integration;
        private float oldTime;
        private bool oldVisible;
        private CursorLockMode oldLock;
        [UnitySetUp] public IEnumerator SetUp()
        {
            oldTime = Time.timeScale; oldVisible = Cursor.visible; oldLock = Cursor.lockState;
            root = new GameObject("Pause test"); integration = root.AddComponent<TestIntegration>(); pause = root.AddComponent<PauseController>();
            pause.Configure(integration, null); yield return null;
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            yield return null;
            Time.timeScale = oldTime; Cursor.lockState = oldLock; Cursor.visible = oldVisible;
        }
        [Test] public void StateStartsClosedAndTransitionsOnce()
        {
            var s = new PauseState(); Assert.IsFalse(s.IsOpen); Assert.IsTrue(s.Open()); Assert.IsFalse(s.Open());
            Assert.IsTrue(s.Close()); Assert.IsFalse(s.Close());
        }
        [UnityTest] public IEnumerator EscapeRestoresTimeCursorAndControlsAndReusesView()
        {
            Time.timeScale = .37f; Cursor.lockState = CursorLockMode.None; Cursor.visible = false;
            bool expectedVisible = Cursor.visible; var expectedLock = Cursor.lockState;
            Assert.IsFalse(pause.IsOpen); var view = pause.ViewObject;
            pause.HandleEscape(); Assert.IsTrue(pause.IsOpen); Assert.AreEqual(0, Time.timeScale); Assert.IsTrue(integration.locked); Assert.IsTrue(Cursor.visible);
            pause.HandleEscape(); Assert.IsFalse(pause.IsOpen); Assert.AreEqual(.37f, Time.timeScale); Assert.IsFalse(integration.locked);
            Assert.AreEqual(expectedVisible, Cursor.visible); Assert.AreEqual(expectedLock, Cursor.lockState);
            pause.HandleEscape(); Assert.AreSame(view, pause.ViewObject);
            // Continue invokes the real first button listener.
            Button resume = null;
            foreach (var button in view.GetComponentsInChildren<Button>(true))
                if (button.GetComponentInChildren<Text>().text.Contains("Продолжить")) { resume = button; break; }
            Assert.IsNotNull(resume); resume.onClick.Invoke(); Assert.IsFalse(pause.IsOpen);
            yield return null;
        }
        [UnityTest] public IEnumerator NullDataUnavailableActionsAndPhoneAreSafe()
        {
            integration.allowed = false; Assert.IsFalse(pause.Open());
            integration.allowed = true; Assert.IsTrue(pause.Open()); pause.Refresh();
            pause.Invoke(PauseAction.Save); pause.HandlePhoneKey(); Assert.AreEqual(0, integration.executed); Assert.IsTrue(pause.IsOpen);
            integration.available = true; pause.HandlePhoneKey(); Assert.AreEqual(1, integration.executed);
            Assert.AreEqual(PauseAction.Phone, integration.last); Assert.IsFalse(pause.IsOpen);
            yield return null;
        }
        [UnityTest] public IEnumerator StandaloneLockRestoresEachOriginalBehaviourState()
        {
            var enabledInput = root.AddComponent<TestPlayerInput>();
            var disabledInput = root.AddComponent<TestPlayerInput>(); disabledInput.enabled = false;
            var adapter = root.AddComponent<StandalonePauseIntegration>();
            var field = typeof(StandalonePauseIntegration).GetField("playerInputBehaviours", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            // Also verify duplicate Behaviour references in Inspector do not leave enabledInput disabled on Close().
            field.SetValue(adapter, new Behaviour[] { enabledInput, enabledInput, disabledInput });
            pause.Configure(adapter, null);
            Assert.IsTrue(pause.Open()); Assert.IsFalse(enabledInput.enabled); Assert.IsFalse(disabledInput.enabled);
            pause.Close(); Assert.IsTrue(enabledInput.enabled); Assert.IsFalse(disabledInput.enabled);
            // Reject pause-own components in playerInputBehaviours so pause cannot disable itself.
            field.SetValue(adapter, new Behaviour[] { enabledInput, pause });
            Assert.IsFalse(pause.Open());
            yield return null;
        }
        [UnityTest] public IEnumerator MissingIntegrationAndButtonNavigationAreSafe()
        {
            pause.Configure(null, null);
            pause.HandleEscape();
            Assert.IsFalse(pause.IsOpen);
            foreach (var button in pause.ViewObject.GetComponentsInChildren<Button>(true))
            {
                Assert.AreEqual(Navigation.Mode.None, button.navigation.mode);
                Assert.AreEqual(button.colors.normalColor, button.colors.selectedColor);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator DuplicateAcrossScenesDoesNotReplaceInstance()
        {
            var scene = SceneManager.CreateScene("PauseTestScene_" + Guid.NewGuid());
            var other = new GameObject("Duplicate"); SceneManager.MoveGameObjectToScene(other, scene);
            other.AddComponent<PauseController>(); yield return null;
            Assert.AreSame(pause, PauseController.Instance); Assert.IsTrue(other == null);
            yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
