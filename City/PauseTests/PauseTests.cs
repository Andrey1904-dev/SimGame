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
        public string customMapShortcut;
        public override bool CanOpenPause() => allowed;
        public override IDisposable AcquireGameplayLock() { locked = true; return new Lock(this); }
        public override bool IsAvailable(PauseAction action) => available;
        public override void Execute(PauseAction action) { executed++; last = action; }
        public override string ShortcutLabel(PauseAction action) =>
            action == PauseAction.Map ? customMapShortcut : null;
        private sealed class Lock : IDisposable
        {
            private TestIntegration owner;
            public Lock(TestIntegration value) { owner = value; }
            public void Dispose() { owner.locked = false; }
        }
    }

    public sealed class TestDataSource : PauseDataSource
    {
        public PauseSnapshot snapshot;
        public override PauseSnapshot Read() => snapshot;
        public void Notify() => RaiseChanged();
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
            root = new GameObject("Pause test");
            integration = root.AddComponent<TestIntegration>();
            pause = root.AddComponent<PauseController>();
            pause.Configure(integration, null);
            yield return null;
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            yield return null;
            Time.timeScale = oldTime; Cursor.lockState = oldLock; Cursor.visible = oldVisible;
        }

        [Test] public void StateStartsClosedAndTransitionsOnce()
        {
            var s = new PauseState();
            Assert.IsFalse(s.IsOpen);
            Assert.IsTrue(s.Open());
            Assert.IsFalse(s.Open());
            Assert.IsTrue(s.Close());
            Assert.IsFalse(s.Close());
        }

        [Test] public void SnapshotConvertsHungerAndStressToSatietyAndMood()
        {
            var fromLegacy = new PauseSnapshot { Hunger = 38f, Stress = 30f };
            Assert.AreEqual(62f, fromLegacy.ResolveSatiety().Value, 0.01f);
            Assert.AreEqual(70f, fromLegacy.ResolveMood().Value, 0.01f);

            var fromDirect = new PauseSnapshot { Satiety = 85f, Hunger = 50f, Mood = 90f, Stress = 40f };
            Assert.AreEqual(85f, fromDirect.ResolveSatiety().Value, 0.01f);
            Assert.AreEqual(90f, fromDirect.ResolveMood().Value, 0.01f);

            var empty = new PauseSnapshot();
            Assert.IsFalse(empty.ResolveSatiety().HasValue);
            Assert.IsFalse(empty.ResolveMood().HasValue);
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
            field.SetValue(adapter, new Behaviour[] { enabledInput, enabledInput, disabledInput });
            pause.Configure(adapter, null);
            Assert.IsTrue(pause.Open()); Assert.IsFalse(enabledInput.enabled); Assert.IsFalse(disabledInput.enabled);
            pause.Close(); Assert.IsTrue(enabledInput.enabled); Assert.IsFalse(disabledInput.enabled);
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

        [UnityTest] public IEnumerator ReferenceLayoutButtonsCardsLabelsAndColorsMatchSpec()
        {
            Assert.IsTrue(pause.Open());
            var safeFrame = pause.ViewObject.transform.Find("Safe frame");
            Assert.IsNotNull(safeFrame);

            // 1. Left panel: 7 identically styled buttons in strict order, no red quit button.
            var menu = safeFrame.Find("Menu border/Menu") as RectTransform;
            Assert.IsNotNull(menu);
            Assert.IsNotNull(menu.Find("Logo crown")?.GetComponent<Image>()?.sprite);
            string[] expectedMenu =
            {
                "Продолжить игру", "Сохранить игру", "Загрузить игру",
                "Настройки", "Управление", "Выйти в главное меню", "Выйти из игры"
            };
            Color firstBg = Color.clear;
            float prevY = 1f;
            for (int i = 0; i < expectedMenu.Length; i++)
            {
                var btnTr = menu.Find("Menu Button " + i + " border/Menu Button " + i) as RectTransform;
                Assert.IsNotNull(btnTr, "Missing Menu Button " + i);
                var borderTr = btnTr.parent as RectTransform;
                Assert.Less(borderTr.anchorMin.y, prevY);
                prevY = borderTr.anchorMin.y;

                var btn = btnTr.GetComponent<Button>();
                var bg = btnTr.GetComponent<Image>();
                var icon = btnTr.Find("Icon")?.GetComponent<Image>();
                var txt = btnTr.Find("Text")?.GetComponent<Text>();
                Assert.IsNotNull(btn); Assert.IsNotNull(bg); Assert.IsNotNull(icon?.sprite); Assert.IsNotNull(txt);
                Assert.AreEqual(expectedMenu[i], txt.text);
                Assert.AreEqual(Navigation.Mode.None, btn.navigation.mode);
                Assert.AreEqual(btn.colors.normalColor, btn.colors.selectedColor);

                if (i == 0) firstBg = bg.color;
                else Assert.AreEqual(firstBg, bg.color, "All 7 menu buttons must share the same background color");
                // Quit button (index 6) must not be red.
                Assert.AreEqual(bg.color.r, bg.color.g, 0.01f);
                Assert.AreEqual(bg.color.g, bg.color.b, 0.01f);
            }

            // 2. Right panel: card order (Header top; Goals left, Status right in middle; Finance left, Quick links right in bottom).
            var dash = safeFrame.Find("Dashboard border/Dashboard") as RectTransform;
            Assert.IsNotNull(dash);
            var header = dash.Find("Header border") as RectTransform;
            var goals = dash.Find("Goals border") as RectTransform;
            var status = dash.Find("Status border") as RectTransform;
            var finance = dash.Find("Finance border") as RectTransform;
            var links = dash.Find("Quick links border") as RectTransform;
            Assert.IsNotNull(header); Assert.IsNotNull(goals); Assert.IsNotNull(status); Assert.IsNotNull(finance); Assert.IsNotNull(links);

            Assert.Greater(header.anchorMin.y, goals.anchorMax.y);
            Assert.Less(goals.anchorMax.x, status.anchorMin.x);
            Assert.AreEqual(goals.anchorMin.y, status.anchorMin.y, 0.001f);
            Assert.Greater(goals.anchorMin.y, finance.anchorMax.y);
            Assert.Less(finance.anchorMax.x, links.anchorMin.x);
            Assert.AreEqual(finance.anchorMin.y, links.anchorMin.y, 0.001f);

            // 3. Status names: strictly Здоровье, Сытость, Энергия, Настроение.
            var statusInner = status.Find("Status");
            string[] expectedStatus = { "Здоровье", "Сытость", "Энергия", "Настроение" };
            for (int i = 0; i < 4; i++)
            {
                var lbl = statusInner.Find("Status Label " + i)?.GetComponent<Text>();
                Assert.IsNotNull(lbl);
                Assert.AreEqual(expectedStatus[i], lbl.text);
            }

            // 4. Finance rows: strictly Деньги:, Долг:, Доход за день:.
            var financeInner = finance.Find("Finance");
            string[] expectedFinance = { "Деньги:", "Долг:", "Доход за день:" };
            for (int i = 0; i < 3; i++)
            {
                var lbl = financeInner.Find("Finance Label " + i)?.GetComponent<Text>();
                Assert.IsNotNull(lbl);
                Assert.AreEqual(expectedFinance[i], lbl.text);
            }

            // 5. Quick links: 2x2 grid (Телефон [P], Инвентарь, Карта, Задания without auto I/M/J).
            var linksInner = links.Find("Quick links");
            string[] expectedQuick = { "Телефон", "Инвентарь", "Карта", "Задания" };
            for (int i = 0; i < 4; i++)
            {
                var qBtn = linksInner.Find("Quick Button " + i + " border/Quick Button " + i);
                Assert.IsNotNull(qBtn);
                Assert.AreEqual(expectedQuick[i], qBtn.Find("Text")?.GetComponent<Text>()?.text);
                string shortcut = qBtn.Find("Shortcut")?.GetComponent<Text>()?.text;
                Assert.AreEqual(i == 0 ? "[P]" : "", shortcut);
            }

            // 6. Missing data defaults and absence of weather/level/duplicate version.
            Assert.AreEqual("Нет данных", dash.Find("Header border/Header/Day Text")?.GetComponent<Text>()?.text);
            Assert.AreEqual("Нет данных", dash.Find("Header border/Header/Time Text")?.GetComponent<Text>()?.text);
            Assert.AreEqual("Нет данных", dash.Find("Header border/Header/Location Box border/Location Box/Location Text")?.GetComponent<Text>()?.text);
            Assert.AreEqual("Нет данных", financeInner.Find("Finance Value 2")?.GetComponent<Text>()?.text);
            Assert.AreEqual("v0.2.0 (alpha)", safeFrame.Find("Version Text")?.GetComponent<Text>()?.text);

            int versionCount = 0;
            foreach (var t in pause.ViewObject.GetComponentsInChildren<Text>(true))
            {
                if (t.text == "v0.2.0 (alpha)") versionCount++;
                Assert.IsFalse(t.text.Contains("Погода"), "Weather must not be present");
                Assert.IsFalse(t.text.Contains("Уровень"), "Character level must not be present");
                Assert.IsFalse(t.text.Contains("Голод"), "Hunger label must be replaced by Сытость");
                Assert.IsFalse(t.text.Contains("Стресс"), "Stress label must be replaced by Настроение");
                Assert.IsFalse(t.text.Contains("Наличные") || t.text.Contains("На карте"), "Cash/Card split must not be present");
                Assert.IsFalse(t.text.Contains("аренда"), "Debt/rent wording must be replaced by Долг:");
                Assert.IsFalse(t.text.Contains("Выбрать"), "False Enter/Select hint must not be shown when navigation is None");
            }
            Assert.AreEqual(1, versionCount);

            // 7. Data rendering: Hunger/Stress conversion, positive/negative DailyIncome colors.
            var ds = root.AddComponent<TestDataSource>();
            ds.snapshot = new PauseSnapshot
            {
                Day = "День 3 (Среда)",
                Time = "18:42",
                Location = "Квартира №67",
                LocationDetail = "Северный квартал",
                Health = 100f,
                Hunger = 38f, // -> 62% Satiety
                Energy = 78f,
                Stress = 30f, // -> 70% Mood
                Money = 12450m,
                Debt = 35000m,
                DailyIncome = 4200m,
                Goals = new[]
                {
                    new PauseGoal { Title = "Забрать заказы в ПВЗ", Complete = true },
                    new PauseGoal { Title = "Доставить 3 заказа", Progress = "0/3", Complete = false }
                }
            };
            pause.Configure(integration, ds);
            Assert.IsTrue(pause.Open());

            Assert.AreEqual("62%", statusInner.Find("Status Value 1")?.GetComponent<Text>()?.text);
            Assert.AreEqual("70%", statusInner.Find("Status Value 3")?.GetComponent<Text>()?.text);
            Assert.AreEqual("12 450 ₽", financeInner.Find("Finance Value 0")?.GetComponent<Text>()?.text);
            var debtText = financeInner.Find("Finance Value 1")?.GetComponent<Text>();
            Assert.AreEqual("35 000 ₽", debtText.text);
            Assert.Greater(debtText.color.r, 0.8f); Assert.Less(debtText.color.g, 0.4f);

            var incomeText = financeInner.Find("Finance Value 2")?.GetComponent<Text>();
            Assert.AreEqual("+4 200 ₽", incomeText.text);
            Assert.Greater(incomeText.color.g, 0.7f); Assert.Less(incomeText.color.r, 0.5f);

            ds.snapshot.DailyIncome = -1500m;
            ds.Notify();
            Assert.AreEqual("-1 500 ₽", incomeText.text);
            Assert.Greater(incomeText.color.r, 0.8f); Assert.Less(incomeText.color.g, 0.4f);
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
