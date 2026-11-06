// Life From Zero — UI/Runtime (ТЗ №042)
// Namespace: LifeFromZero.UI.Runtime
//
// Полноэкранная карта (клавиша M): район, дороги, маршрут, метки, легенда.
//
// Ограничения, которые соблюдаются намеренно:
//   * карта рисуется из ДАННЫХ района (сетка + дороги + POI), а НЕ из скриншота
//     Map.PNG — тот не совпадает с District750 и не может быть подложкой (§10);
//   * маршрут берётся только из NavigationService.ActiveRoute — второго расчёта нет;
//   * никакой клик-по-карте не назначает маршрут (в игре нет координат клика по миру):
//     ручная метка ставится игроком в мире, а карта её только показывает.

using System;
using System.Collections.Generic;
using LifeFromZero.UI.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LifeFromZero.UI.Runtime
{
    /// <summary>Полноэкранная карта.</summary>
    public sealed class MapScreen : MonoBehaviour
    {
        [Header("Вид")]
        [Tooltip("Отступ карты от краёв экрана, доли.")]
        [SerializeField] private float margin = 0.022f;

        [SerializeField] private float minZoom = 0.85f;
        [SerializeField] private float maxZoom = 3.2f;
        [SerializeField] private float initialZoom = 1.15f;
        [SerializeField] private float scrollSensitivity = 0.12f;

        [Tooltip("Шаг сетки карты в метрах; 0 — без сетки.")]
        [SerializeField] private float gridStepMeters = 100f;

        private Canvas _canvas;
        private RectTransform _root;
        private RectTransform _viewport;
        private MapCanvasGraphic _graphic;
        private MapCanvasGraphic _routeGraphic;
        private RectTransform _markers;
        private Text _zoomLabel;
        private Text _infoTitle;
        private Text _infoSubtitle;
        private RectTransform _infoPanel;

        private UiSpriteCache _sprites;
        private NavigationService _navigation;
        private UiWorldAdapter _world;

        private float _zoom = 1.15f;
        private Vector2 _center = new Vector2(375f, 375f);
        private Vector2 _worldSize = new Vector2(750f, 750f);
        private float _lastDragTime;
        private readonly List<GameObject> _markerObjects = new List<GameObject>();

        // Перерисовка только по изменению данных (ТЗ №045): без этого uGUI перестраивал
        // меш на каждое обращение к Refresh(), а слой без CanvasRenderer падал столько же раз.
        private readonly MapRedrawGate _redrawGate = new MapRedrawGate();
        private bool _missingLayerLogged;

        /// <summary>Геометрия района (дороги, точки, сетка). Может быть null — тогда только сетка.</summary>
        public IMapGeometrySource Geometry { get; set; }

        /// <summary>Размер района в метрах (X — восток, Y — север).</summary>
        public Vector2 WorldSize
        {
            get { return _worldSize; }
            set
            {
                _worldSize = value;
                _center = new Vector2(value.x * 0.5f, value.y * 0.5f);
                ApplyTransform();
            }
        }

        public bool IsVisible => _root != null && _root.gameObject.activeSelf;

        public float Zoom => _zoom;

        /// <summary>Шаг сетки карты в метрах (0 — сетки нет).</summary>
        public float GridStepMeters
        {
            get { return gridStepMeters; }
            set { gridStepMeters = value; if (_graphic != null) _graphic.GridStepMeters = value; }
        }

        // ------------------------------------------------------------------ создание

        private void Awake()
        {
            _sprites = new UiSpriteCache();
            _zoom = Mathf.Clamp(initialZoom, minZoom, maxZoom);
            BuildShell();
            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (_sprites != null) _sprites.Dispose();
        }

        private void BuildShell()
        {
            _canvas = UiBuilder.CreateCanvas("Map Canvas", transform, 150);
            _root = (RectTransform)_canvas.transform;
            UiBuilder.Image("Backdrop", _root, 0f, 0f, 1f, 1f, new Color(0.03f, 0.035f, 0.04f, 0.97f), null, true);

            // Заголовок.
            UiBuilder.Label("Title", _root, "КАРТА РАЙОНА", margin, 0.935f, 0.55f, 0.985f,
                UiTheme.Px(22f), UiTheme.TextPrimary, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiBuilder.Label("Subtitle", _root, "Северный квартал · District750", margin, 0.905f, 0.55f, 0.935f,
                UiTheme.Px(14f), UiTheme.TextMuted);

            // Панель информации (объектив/маршрут).
            RectTransform panel = UiBuilder.Panel("Info", _root, _sprites, 0.60f, 0.845f, 0.975f, 0.975f,
                UiTheme.MapPanel, 14);
            _infoPanel = panel;
            _infoTitle = UiBuilder.Label("ObjectiveTitle", panel, "Маршрут не построен", 0.05f, 0.52f, 0.95f, 0.94f,
                UiTheme.Px(17f), UiTheme.TextPrimary, TextAnchor.MiddleLeft, FontStyle.Bold);
            _infoSubtitle = UiBuilder.Label("ObjectiveSubtitle",
                panel, "Выберите заказ в телефоне или метку в мире.", 0.05f, 0.08f, 0.95f, 0.52f,
                UiTheme.Px(13f), UiTheme.TextMuted);

            // Область карты.
            float viewTop = 0.885f;
            float viewBottom = margin + 0.075f;
            _viewport = UiBuilder.Rect("Viewport", _root, margin, viewBottom, 1f - margin, viewTop);
            Image viewportBackground = UiBuilder.Fill(_viewport, UiTheme.MapBackground, _sprites.Rounded(16), true);
            viewportBackground.raycastTarget = true;
            _viewport.gameObject.AddComponent<RectMask2D>();

            // Слои карты создаются ТОЛЬКО через UiBuilder.GraphicLayer: он гарантирует
            // CanvasRenderer (без него uGUI сыпал MissingComponentException) и сам
            // отключает слой с одним понятным сообщением, если рендерер не появился.
            _graphic = UiBuilder.GraphicLayer<MapCanvasGraphic>("Grid", _viewport, 0f, 0f, 1f, 1f,
                out _);
            _graphic.Project = WorldToUnit;
            _graphic.GridColor = new Color(1f, 1f, 1f, 0.06f);
            _graphic.RoadColor = new Color(0.60f, 0.65f, 0.72f, 0.95f);
            _graphic.DrivewayColor = new Color(0.40f, 0.43f, 0.48f, 0.90f);
            _graphic.PointColor = new Color(0.70f, 0.76f, 0.84f, 0.80f);
            _graphic.GridStepMeters = gridStepMeters;
            _graphic.PixelsPerMeterProvider = () => PixelsPerMeter();
            _graphic.raycastTarget = false;

            _routeGraphic = UiBuilder.GraphicLayer<MapCanvasGraphic>("Route", _viewport, 0f, 0f, 1f, 1f,
                out _);
            _routeGraphic.Project = WorldToUnit;
            _routeGraphic.GridStepMeters = 0f;
            _routeGraphic.RouteColor = UiTheme.MapRoute;
            _routeGraphic.PixelsPerMeterProvider = () => PixelsPerMeter();
            _routeGraphic.raycastTarget = false;

            _markers = UiBuilder.Rect("Markers", _viewport, 0f, 0f, 1f, 1f);
            _markers.gameObject.AddComponent<RectMask2D>();

            // Легенда.
            UiBuilder.Label("LegendRoad", _root, "— дороги", margin, margin + 0.035f, 0.26f, margin + 0.072f,
                UiTheme.Px(13f), UiTheme.TextMuted);
            UiBuilder.Label("LegendRoute", _root, "— маршрут", 0.26f, margin + 0.035f, 0.44f, margin + 0.072f,
                UiTheme.Px(13f), UiTheme.MapRoute);
            UiBuilder.Label("LegendOrder", _root, "● заказ", 0.44f, margin + 0.035f, 0.60f, margin + 0.072f,
                UiTheme.Px(13f), UiTheme.MapOrder);
            UiBuilder.Label("LegendPlayer", _root, "▲ игрок", 0.60f, margin + 0.035f, 0.76f, margin + 0.072f,
                UiTheme.Px(13f), UiTheme.MapPlayer);

            // Кнопки.
            RectTransform controls = UiBuilder.Rect("Controls", _root, 1f - margin - 0.145f, margin + 0.03f,
                1f - margin, margin + 0.075f);
            UiBuilder.PillButton("ZoomOut", controls, _sprites, "−", 0f, 0f, 0.28f, 1f,
                new Color(0.14f, 0.15f, 0.17f, 0.98f), UiTheme.TextPrimary, () => ZoomBy(-0.25f), 10,
                UiTheme.Px(24f));
            RectTransform zoomLabelRect = UiBuilder.Rect("ZoomLabel", controls, 0.30f, 0f, 0.70f, 1f);
            _zoomLabel = UiBuilder.TextOn(zoomLabelRect, "1.2×", UiTheme.Px(14f),
                UiTheme.TextMuted, TextAnchor.MiddleCenter);
            UiBuilder.PillButton("ZoomIn", controls, _sprites, "+", 0.72f, 0f, 1f, 1f,
                new Color(0.14f, 0.15f, 0.17f, 0.98f), UiTheme.TextPrimary, () => ZoomBy(0.25f), 10,
                UiTheme.Px(24f));

            UiBuilder.PillButton("Center", _root, _sprites, "К игроку", 1f - margin - 0.145f, margin + 0.075f,
                1f - margin, margin + 0.125f, new Color(0.14f, 0.15f, 0.17f, 0.98f), UiTheme.TextPrimary,
                CenterOnPlayer, 10, UiTheme.Px(14f));

            UiBuilder.Label("Hint", _root, "M / Esc — закрыть · колесо — масштаб · перетаскивание — сдвиг",
                margin, margin + 0.075f, 1f - margin, margin + 0.125f, UiTheme.Px(13f),
                UiTheme.TextMuted);

            // Перетаскивание карты.
            var drag = _viewport.gameObject.AddComponent<MapDragHandler>();
            drag.DragHandler = OnDrag;
            drag.ScrollHandler = OnScroll;
        }

        // ------------------------------------------------------------------ привязка

        /// <summary>Передать сервисы карте (вызывает роутер при открытии и после смены сцены).</summary>
        public void Bind(NavigationService navigation, UiWorldAdapter world)
        {
            _navigation = navigation;
            _world = world;
        }

        // ------------------------------------------------------------------ видимость

        public void SetVisible(bool visible)
        {
            if (_root == null) return;
            _root.gameObject.SetActive(visible);
            if (visible)
            {
                ApplyTransform();
                Refresh(true);
            }
        }

        /// <summary>Перерисовать, если данные изменились (см. <see cref="MapRedrawGate"/>).</summary>
        public void Refresh() => Refresh(false);

        /// <summary>
        /// Полная перерисовка: маршрут, метки, информация. <paramref name="force"/> = true
        /// перерисовывает даже при неизменных данных (открытие карты, смена района).
        /// </summary>
        public void Refresh(bool force)
        {
            if (!IsVisible) return;

            if (_graphic == null || _routeGraphic == null || _zoomLabel == null)
            {
                // Слой карты не создался (например, объект «Grid»/«Route» остался в сцене
                // без CanvasRenderer). Сообщаем ОДИН раз и не спамим каждый кадр.
                if (!_missingLayerLogged)
                {
                    _missingLayerLogged = true;
                    Debug.LogError("[MapScreen] Слои карты не созданы — карта не будет нарисована. " +
                                   "Проверьте «Grid»/«Route»: Tools → Life From Zero → UI → " +
                                   "Verify Scenes & Adapters.");
                }
                return;
            }

            if (!force && !_redrawGate.ShouldRedraw(BuildFrameKey())) return;

            _graphic.Source = Geometry;
            _graphic.GridStepMeters = gridStepMeters;
            _graphic.WorldBounds = new Rect(0f, 0f, _worldSize.x, _worldSize.y);
            _graphic.Redraw();

            RoutePath route = _navigation != null ? _navigation.ActiveRoute : null;
            BuildRoute(route);
            BuildMarkers(route);
            UpdateInfo(route);
            _zoomLabel.text = _zoom.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "×";
        }

        /// <summary>Что видно на карте сейчас — вход для <see cref="MapRedrawGate"/>.</summary>
        private MapFrameKey BuildFrameKey()
        {
            Vector2 player;
            float heading;
            bool hasPlayer = TryPlayer(out player, out heading);
            RoutePath route = _navigation != null ? _navigation.ActiveRoute : null;

            return new MapFrameKey(
                IsVisible,
                _zoom,
                _center.x,
                _center.y,
                _worldSize.x,
                _worldSize.y,
                gridStepMeters,
                Geometry == null ? 0 : Geometry.GetHashCode(),
                MapRedrawGate.RouteSignature(route),
                hasPlayer,
                hasPlayer ? MapRedrawGate.PlayerCell(player.x) : 0f,
                hasPlayer ? MapRedrawGate.PlayerCell(player.y) : 0f);
        }

        private void BuildRoute(RoutePath route)
        {
            if (_routeGraphic == null) return;
            if (route == null)
            {
                _routeGraphic.RoutePoints = null;
                _routeGraphic.Redraw();
                return;
            }

            var points = new List<Vector2>(route.Steps.Count + 1);
            points.Add(new Vector2(route.SnapStart.X, route.SnapStart.Z));
            for (int i = 0; i < route.Steps.Count; i++)
            {
                MapPoint step = route.Steps[i].Position;
                if (points.Count > 0 && Vector2.Distance(points[points.Count - 1], new Vector2(step.X, step.Z)) < 0.5f)
                    continue;
                points.Add(new Vector2(step.X, step.Z));
            }

            MapPoint destination = route.Destination;
            var last = new Vector2(destination.X, destination.Z);
            if (points.Count == 0 || Vector2.Distance(points[points.Count - 1], last) > 0.5f) points.Add(last);

            _routeGraphic.RoutePoints = points;
            _routeGraphic.RouteWidthPixels = Mathf.Clamp(7f * _zoom, 6f, 18f);
            _routeGraphic.Redraw();
        }

        private void BuildMarkers(RoutePath route)
        {
            for (int i = 0; i < _markerObjects.Count; i++)
            {
                if (_markerObjects[i] != null) UiBuilder.Destroy(_markerObjects[i]);
            }
            _markerObjects.Clear();

            Vector2 playerPosition;
            float heading;
            if (TryPlayer(out playerPosition, out heading))
            {
                AddMarker(playerPosition, UiTheme.MapPlayer, UiIconKind.Player, 26f, heading);
            }

            if (route != null)
            {
                AddMarker(new Vector2(route.Destination.X, route.Destination.Z), UiTheme.MapOrder,
                    UiIconKind.Order, 28f, 0f);
            }

            if (_navigation != null)
            {
                IReadOnlyList<Waypoint> waypoints = _navigation.Waypoints.Waypoints;
                for (int i = 0; i < waypoints.Count; i++)
                {
                    Waypoint waypoint = waypoints[i];
                    if (route != null && waypoint.Id == route.DestinationId) continue;

                    Color color = waypoint.Kind == WaypointKind.Home
                        ? UiTheme.MapHome
                        : waypoint.Kind == WaypointKind.Manual ? UiTheme.MapManual : UiTheme.MapOrder;
                    UiIconKind icon = waypoint.Kind == WaypointKind.Home ? UiIconKind.Home : UiIconKind.Flag;
                    AddMarker(new Vector2(waypoint.Position.X, waypoint.Position.Z), color, icon, 22f, 0f);
                }
            }
        }

        private void AddMarker(Vector2 worldPoint, Color color, UiIconKind icon, float sizePixels, float rotationDegrees)
        {
            Vector2 unit = WorldToUnit(worldPoint);
            if (unit.x < -0.05f || unit.x > 1.05f || unit.y < -0.05f || unit.y > 1.05f) return;

            RectTransform rect = UiBuilder.Rect("Marker " + icon, _markers, unit.x, unit.y, unit.x, unit.y);
            rect.sizeDelta = new Vector2(sizePixels, sizePixels);
            rect.anchoredPosition = Vector2.zero;
            rect.localEulerAngles = new Vector3(0f, 0f, rotationDegrees);

            Image background = rect.gameObject.AddComponent<Image>();
            background.sprite = _sprites.Circle();
            background.color = new Color(color.r, color.g, color.b, 0.22f);
            background.raycastTarget = false;

            Image glyph = UiBuilder.Icon("Glyph", rect, _sprites, icon, 0.18f, 0.18f, 0.82f, 0.82f, color);
            glyph.raycastTarget = false;
            _markerObjects.Add(rect.gameObject);
        }

        private void UpdateInfo(RoutePath route)
        {
            if (route != null)
            {
                float remaining = RouteNavigator.RemainingDistance(route, TryPlayerPosition());
                _infoTitle.text = string.IsNullOrEmpty(route.DestinationLabel)
                    ? "Маршрут построен"
                    : route.DestinationLabel;
                _infoSubtitle.text = "Осталось " + RouteNavigator.FormatDistance(remaining)
                                     + " · всего " + RouteNavigator.FormatDistance(route.TotalDistance);
                _infoTitle.color = UiTheme.TextPrimary;
                return;
            }

            string message = _navigation != null ? _navigation.LastOutcome.Message : string.Empty;
            _infoTitle.text = "Маршрут не построен";
            _infoSubtitle.text = string.IsNullOrEmpty(message)
                ? "Выберите заказ в телефоне или поставьте метку в мире."
                : message;
            _infoTitle.color = UiTheme.TextPrimary;
        }

        // ------------------------------------------------------------------ преобразования

        /// <summary>Мировые координаты (метры) → нормализованные координаты области карты (0..1).</summary>
        public Vector2 WorldToUnit(Vector2 world)
        {
            float halfWidthMeters = _worldSize.x / (2f * _zoom);
            float halfHeightMeters = _worldSize.y * (2f / 3f) / (2f * _zoom);

            float u = (world.x - (_center.x - halfWidthMeters)) / (halfWidthMeters * 2f);
            float v = (world.y - (_center.y - halfHeightMeters)) / (halfHeightMeters * 2f);
            return new Vector2(u, v);
        }

        private float PixelsPerMeter()
        {
            if (_viewport == null) return 1f;
            float width = _viewport.rect.width;
            if (width <= 1f) width = 1600f;
            return width * _zoom / Mathf.Max(1f, _worldSize.x);
        }

        private void ApplyTransform()
        {
            _zoom = Mathf.Clamp(_zoom, minZoom, maxZoom);
            float halfWidthMeters = _worldSize.x / (2f * _zoom);
            float halfHeightMeters = _worldSize.y * (2f / 3f) / (2f * _zoom);
            _center.x = Mathf.Clamp(_center.x, -halfWidthMeters * 0.5f, _worldSize.x + halfWidthMeters * 0.5f);
            _center.y = Mathf.Clamp(_center.y, -halfHeightMeters * 0.5f, _worldSize.y + halfHeightMeters * 0.5f);
        }

        private void ZoomBy(float delta)
        {
            float before = _zoom;
            _zoom = Mathf.Clamp(_zoom + delta, minZoom, maxZoom);
            if (Mathf.Abs(before - _zoom) < 0.0001f) return;
            ApplyTransform();
            Refresh();
        }

        private void OnScroll(float scroll)
        {
            if (Mathf.Abs(scroll) < 0.01f) return;
            _zoom = Mathf.Clamp(_zoom + scroll * scrollSensitivity, minZoom, maxZoom);
            ApplyTransform();
            Refresh();
        }

        private void OnDrag(Vector2 deltaPixels)
        {
            if (_viewport == null) return;
            float width = Mathf.Max(1f, _viewport.rect.width);
            float height = Mathf.Max(1f, _viewport.rect.height);

            Vector2 worldPerPixel = new Vector2(
                _worldSize.x / (_zoom * width),
                _worldSize.y * (2f / 3f) / (_zoom * height));

            _center -= new Vector2(deltaPixels.x * worldPerPixel.x, -deltaPixels.y * worldPerPixel.y);
            ApplyTransform();
            Refresh();
        }

        private void CenterOnPlayer()
        {
            Vector2 position;
            float heading;
            if (TryPlayer(out position, out heading)) _center = position;
            ApplyTransform();
            Refresh();
        }

        private bool TryPlayer(out Vector2 position, out float heading)
        {
            position = Vector2.zero;
            heading = 0f;
            if (_world == null) return false;

            Vector3 world;
            float degrees;
            if (!_world.TryGetPlayerPose(out world, out degrees)) return false;

            position = new Vector2(world.x, world.z);
            heading = degrees;
            return true;
        }

        private MapPoint TryPlayerPosition()
        {
            Vector2 position;
            float heading;
            if (!TryPlayer(out position, out heading)) return new MapPoint(0f, 0f);
            return new MapPoint(position.x, position.y);
        }

        /// <summary>Обновление от роутера (игрок сдвинулся, маршрут перестроился).</summary>
        public void Tick()
        {
            if (!IsVisible) return;
            Refresh();
        }
    }

    /// <summary>Перетаскивание/колесо для области карты.</summary>
    internal sealed class MapDragHandler : MonoBehaviour, UnityEngine.EventSystems.IBeginDragHandler,
        UnityEngine.EventSystems.IDragHandler, UnityEngine.EventSystems.IScrollHandler
    {
        public Action<Vector2> DragHandler;
        public Action<float> ScrollHandler;

        public void OnBeginDrag(UnityEngine.EventSystems.PointerEventData eventData)
        {
        }

        public void OnDrag(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (DragHandler != null) DragHandler(eventData.delta);
        }

        public void OnScroll(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (ScrollHandler != null) ScrollHandler(eventData.scrollDelta.y);
        }
    }
}
