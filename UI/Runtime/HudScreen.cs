// Life From Zero — UI/Runtime (ТЗ №042)
// Namespace: LifeFromZero.UI.Runtime
//
// Игровой HUD: подсказка взаимодействия, блок навигации, цель, часы/деньги и мелкие маркеры.
//
// Правила:
//   * HUD полностью исчезает в паузе и на карте (HudVisibility.Hidden) и приглушается
//     в телефоне — политику задаёт UiStateMachine, HUD её только исполняет (§8.3, §11);
//   * HUD не дублирует паузу: никакого списка финансов/целей во весь экран;
//   * всё, что показывается, приходит из UiSnapshot и NavigationService.Cue.

using LifeFromZero.UI.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LifeFromZero.UI.Runtime
{
    /// <summary>Игровой HUD.</summary>
    public sealed class HudScreen : MonoBehaviour
    {
        private Canvas _canvas;
        private CanvasGroup _group;
        private RectTransform _root;

        private RectTransform _navigationBlock;
        private Text _maneuverLabel;
        private Text _maneuverDistance;
        private Text _remainingLabel;
        private Image _maneuverArrow;

        private RectTransform _interactionBlock;
        private Text _interactionLabel;

        private RectTransform _compactBlock;
        private Text _compactObjective;
        private Text _clockLabel;
        private Text _moneyLabel;

        private Text _toastLabel;
        private RectTransform _toastBlock;
        private float _toastTimer;

        private UiSpriteCache _sprites;
        private HudVisibility _visibility = HudVisibility.Visible;

        public bool IsVisible => _visibility != HudVisibility.Hidden;

        private void Awake()
        {
            _sprites = new UiSpriteCache();
            Build();
        }

        private void OnDestroy()
        {
            if (_sprites != null) _sprites.Dispose();
        }

        private void Build()
        {
            _canvas = UiBuilder.CreateCanvas("HUD Canvas", transform, 100);
            _root = (RectTransform)_canvas.transform;
            _group = _root.gameObject.AddComponent<CanvasGroup>();
            _group.interactable = false;
            _group.blocksRaycasts = false;

            // ---- блок навигации (слева сверху) ----
            _navigationBlock = UiBuilder.Panel("Navigation", _root, _sprites, 0.018f, 0.845f, 0.315f, 0.975f,
                UiTheme.HudNavPanel, 14, false);
            _maneuverArrow = UiBuilder.Image("Arrow", _navigationBlock, 0.03f, 0.36f, 0.22f, 0.92f,
                UiTheme.MapRoute, _sprites.Icon(UiIconKind.Route));
            _maneuverArrow.preserveAspect = true;
            _maneuverArrow.raycastTarget = false;
            _maneuverLabel = UiBuilder.Label("Maneuver", _navigationBlock, "Прямо к цели",
                0.25f, 0.52f, 0.97f, 0.94f, UiTheme.Px(20f), UiTheme.TextPrimary,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            _maneuverDistance = UiBuilder.Label("ManeuverDistance", _navigationBlock, "—",
                0.25f, 0.10f, 0.62f, 0.52f, UiTheme.Px(16f), UiTheme.MapRoute);
            _remainingLabel = UiBuilder.Label("Remaining", _navigationBlock, "—",
                0.60f, 0.10f, 0.97f, 0.52f, UiTheme.Px(14f), UiTheme.TextMuted,
                TextAnchor.MiddleRight);
            _navigationBlock.gameObject.SetActive(false);

            // ---- подсказка взаимодействия (по центру снизу) ----
            _interactionBlock = UiBuilder.Panel("Interaction", _root, _sprites, 0.315f, 0.115f, 0.685f, 0.175f,
                new Color(0.05f, 0.06f, 0.07f, 0.72f), 12, false);
            _interactionLabel = UiBuilder.Label("Hint", _interactionBlock, "[E] Взаимодействовать",
                0.03f, 0.10f, 0.97f, 0.90f, UiTheme.Px(18f), UiTheme.TextPrimary,
                TextAnchor.MiddleCenter);
            _interactionBlock.gameObject.SetActive(false);

            // ---- компактные данные (слева снизу) ----
            _compactBlock = UiBuilder.Panel("Compact", _root, _sprites, 0.018f, 0.020f, 0.255f, 0.098f,
                UiTheme.HudPanel, 12, false);
            _clockLabel = UiBuilder.Label("Clock", _compactBlock, "—", 0.04f, 0.48f, 0.60f, 0.96f,
                UiTheme.Px(18f), UiTheme.TextPrimary, TextAnchor.MiddleLeft, FontStyle.Bold);
            _moneyLabel = UiBuilder.Label("Money", _compactBlock, "—", 0.04f, 0.04f, 0.96f, 0.50f,
                UiTheme.Px(14f), UiTheme.TextMuted);
            _compactObjective = UiBuilder.Label("Objective", _compactBlock, string.Empty,
                0.62f, 0.48f, 0.98f, 0.96f, UiTheme.Px(12f), UiTheme.TextMuted,
                TextAnchor.MiddleRight);

            // ---- всплывающее сообщение ----
            _toastBlock = UiBuilder.Panel("Toast", _root, _sprites, 0.32f, 0.205f, 0.68f, 0.265f,
                new Color(0.08f, 0.09f, 0.11f, 0.90f), 12, false);
            _toastLabel = UiBuilder.Label("ToastText", _toastBlock, string.Empty, 0.03f, 0.08f, 0.97f, 0.92f,
                UiTheme.Px(16f), UiTheme.TextPrimary, TextAnchor.MiddleCenter);
            _toastBlock.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ обновление

        /// <summary>Политика видимости от роутера.</summary>
        public void SetVisibility(HudVisibility visibility)
        {
            _visibility = visibility;
            if (_root == null) return;

            _root.gameObject.SetActive(visibility != HudVisibility.Hidden);
            _group.alpha = visibility == HudVisibility.Muted ? 0.35f : 1f;
        }

        /// <summary>Нарисовать HUD по снимку и текущему маршруту.</summary>
        public void Render(UiSnapshot snapshot, NavigationService navigation)
        {
            if (_root == null || snapshot == null) return;

            RenderClock(snapshot);
            RenderNavigation(navigation);
            RenderInteraction(snapshot);
        }

        private void RenderClock(UiSnapshot snapshot)
        {
            ClockView clock = snapshot.Clock ?? new ClockView();
            string time = string.IsNullOrEmpty(clock.TimeLabel) ? UiFormat.Dash : clock.TimeLabel;
            string day = string.IsNullOrEmpty(clock.DayLabel) ? string.Empty : clock.DayLabel;
            _clockLabel.text = string.IsNullOrEmpty(day) ? time : day + " · " + time;

            FinanceView finance = snapshot.Finance ?? new FinanceView();
            _moneyLabel.text = finance.Money.HasValue
                ? UiFormat.Rubles(finance.Money) + (finance.DailyIncome.HasValue
                    ? "   " + UiFormat.SignedRubles(finance.DailyIncome)
                    : string.Empty)
                : UiFormat.Missing;

            string objective = snapshot.Objective;
            _compactObjective.gameObject.SetActive(!string.IsNullOrEmpty(objective));
            _compactObjective.text = Shorten(objective, 18);
        }

        private void RenderNavigation(NavigationService navigation)
        {
            if (navigation == null || navigation.ActiveRoute == null)
            {
                _navigationBlock.gameObject.SetActive(false);
                return;
            }

            UiRouter router = UiRouter.Instance;
            MapPoint player = new MapPoint(0f, 0f);
            float heading = 0f;
            if (router != null && router.World != null)
            {
                Vector3 position;
                float degrees;
                if (router.World.TryGetPlayerPose(out position, out degrees))
                {
                    player = new MapPoint(position.x, position.z);
                    heading = degrees;
                }
            }
            else
            {
                heading = 0f;
            }

            NavigationCue cue = navigation.Cue(player, heading);
            _navigationBlock.gameObject.SetActive(true);
            _maneuverLabel.text = cue.Label;
            _maneuverDistance.text = cue.DistanceToManeuver > 0f
                ? RouteNavigator.FormatDistance(cue.DistanceToManeuver)
                : string.Empty;
            _remainingLabel.text = RouteNavigator.FormatDistance(cue.RemainingDistance);
            _maneuverArrow.rectTransform.localEulerAngles = new Vector3(0f, 0f, -cue.BearingDegrees);
        }

        private void RenderInteraction(UiSnapshot snapshot)
        {
            bool has = !string.IsNullOrEmpty(snapshot.InteractionHint);
            _interactionBlock.gameObject.SetActive(has);
            if (has) _interactionLabel.text = snapshot.InteractionHint;
        }

        /// <summary>Короткое сообщение игроку (например, причина отказа в маршруте).</summary>
        public void ShowToast(string message, float seconds = 3.5f)
        {
            if (_toastBlock == null) return;
            if (string.IsNullOrEmpty(message))
            {
                _toastBlock.gameObject.SetActive(false);
                _toastTimer = 0f;
                return;
            }

            _toastLabel.text = message;
            _toastBlock.gameObject.SetActive(true);
            _toastTimer = Mathf.Max(0.5f, seconds);
        }

        private void Update()
        {
            if (_toastTimer <= 0f) return;
            _toastTimer -= Time.unscaledDeltaTime;
            if (_toastTimer <= 0f && _toastBlock != null) _toastBlock.gameObject.SetActive(false);
        }

        private static string Shorten(string value, int max)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Length <= max ? value : value.Substring(0, max - 1) + "…";
        }
    }
}
