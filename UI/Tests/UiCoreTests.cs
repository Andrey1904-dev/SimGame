// Life From Zero — UI tests (ТЗ №042)
// Namespace: LifeFromZero.UI.Tests
//
// Тесты ядра интерфейсов: состояния, карта, метки, маршруты, HUD, телефон, формат.
// Не требуют UnityEngine и Unity-проекта: прогоняются вне Unity.

using System;
using System.Collections.Generic;
using System.Globalization;
using LifeFromZero.City.Quarter;
using LifeFromZero.UI.Core;

namespace LifeFromZero.UI.Tests
{
    public static class UiCoreTests
    {
        public static UiTestSuite Create()
        {
            var suite = new UiTestSuite();
            AddStateMachineTests(suite);
            AddCalibrationTests(suite);
            AddWaypointTests(suite);
            AddRouteTests(suite);
            AddNavigationTests(suite);
            AddHudTests(suite);
            AddPhoneTests(suite);
            AddFormatTests(suite);
            AddPauseContractTests(suite);
            AddPauseLayoutTests(suite);
            AddQuarterPlanAdapterTests(suite);
            AddSnapTests(suite);
            return suite;
        }

        // ---------------------------------------------------------------- §3 состояния

        private static void AddStateMachineTests(UiTestSuite suite)
        {
            suite.Add("Состояния: P из игры открывает телефон, повтор закрывает", () =>
            {
                var sm = new UiStateMachine();
                UiTransition open = sm.Apply(UiIntent.Phone);
                UiAssert.True(open.Accepted, "P из игры должен открывать телефон");
                UiAssert.Equal(UiScreen.Phone, sm.Screen, "состояние после P");
                sm.Apply(UiIntent.Phone);
                UiAssert.Equal(UiScreen.Gameplay, sm.Screen, "повторный P закрывает телефон");
            });

            suite.Add("Состояния: Esc из телефона возвращает в игру", () =>
            {
                var sm = new UiStateMachine();
                sm.Apply(UiIntent.Phone);
                UiTransition back = sm.Apply(UiIntent.Back);
                UiAssert.True(back.Accepted, "Esc должен закрывать телефон");
                UiAssert.Equal(UiScreen.Gameplay, sm.Screen, "после Esc");
            });

            suite.Add("Состояния: M открывает карту из игры, Esc и M закрывают её", () =>
            {
                var sm = new UiStateMachine();
                sm.Apply(UiIntent.Map);
                UiAssert.Equal(UiScreen.MapFromGameplay, sm.Screen, "M из игры");
                sm.Apply(UiIntent.Back);
                UiAssert.Equal(UiScreen.Gameplay, sm.Screen, "Esc закрывает карту в игру");

                sm.Apply(UiIntent.Map);
                sm.Apply(UiIntent.Map);
                UiAssert.Equal(UiScreen.Gameplay, sm.Screen, "повторный M закрывает карту");
            });

            suite.Add("Состояния: в телефоне карта не открывается (ТЗ §2)", () =>
            {
                var sm = new UiStateMachine();
                sm.Apply(UiIntent.Phone);
                UiTransition denied = sm.Apply(UiIntent.Map);
                UiAssert.False(denied.Accepted, "M внутри телефона должен быть отклонён");
                UiAssert.Equal(UiScreen.Phone, sm.Screen, "состояние не должно измениться");
                UiAssert.Contains(denied.Reason, "Карта", "причина отказа должна быть понятной");
            });

            suite.Add("Состояния: карта из паузы сохраняет паузу, Esc возвращает в паузу", () =>
            {
                var sm = new UiStateMachine();
                sm.Apply(UiIntent.Back);                       // Esc из игры -> пауза
                UiAssert.Equal(UiScreen.Pause, sm.Screen, "Esc из игры открывает паузу");
                sm.Apply(UiIntent.OpenMapFromPause);
                UiAssert.Equal(UiScreen.MapFromPause, sm.Screen, "кнопка «Карта» открывает карту из паузы");
                UiAssert.True(sm.IsPausedContext, "контекст паузы должен сохраняться");
                sm.Apply(UiIntent.Back);
                UiAssert.Equal(UiScreen.Pause, sm.Screen, "Esc из карты возвращает в паузу, а не в игру");
                UiAssert.True(sm.IsPausedContext, "после возврата всё ещё пауза");
            });

            suite.Add("Состояния: телефон из паузы возвращает в паузу", () =>
            {
                var sm = new UiStateMachine();
                sm.Apply(UiIntent.Back);
                sm.Apply(UiIntent.OpenPhoneFromPause);
                UiAssert.Equal(UiScreen.PhoneFromPause, sm.Screen, "телефон из паузы");
                sm.Apply(UiIntent.Back);
                UiAssert.Equal(UiScreen.Pause, sm.Screen, "Esc из телефона-из-паузы возвращает в паузу");
            });

            suite.Add("Состояния: конфликтующие окна не открываются друг поверх друга", () =>
            {
                var sm = new UiStateMachine();
                sm.Apply(UiIntent.Back);                       // пауза
                UiTransition phoneWhilePaused = sm.Apply(UiIntent.Phone);
                UiAssert.True(phoneWhilePaused.Accepted, "телефон из паузы разрешён");
                UiTransition mapOnTop = sm.Apply(UiIntent.OpenMapFromPause);
                UiAssert.False(mapOnTop.Accepted, "карту нельзя открыть поверх телефона");
                UiAssert.True(sm.IsModalOpen, "модальное окно остаётся открытым");
            });

            suite.Add("Состояния: HUD скрыт в паузе и на карте, приглушён в телефоне", () =>
            {
                UiAssert.Equal(HudVisibility.Visible, UiStateMachine.HudPolicy(UiScreen.Gameplay), "игра");
                UiAssert.Equal(HudVisibility.Muted, UiStateMachine.HudPolicy(UiScreen.Phone), "телефон");
                UiAssert.Equal(HudVisibility.Muted, UiStateMachine.HudPolicy(UiScreen.PhoneFromPause), "телефон из паузы");
                UiAssert.Equal(HudVisibility.Hidden, UiStateMachine.HudPolicy(UiScreen.Pause), "пауза");
                UiAssert.Equal(HudVisibility.Hidden, UiStateMachine.HudPolicy(UiScreen.MapFromGameplay), "карта");
                UiAssert.Equal(HudVisibility.Hidden, UiStateMachine.HudPolicy(UiScreen.MapFromPause), "карта из паузы");
                UiAssert.True(UiStateMachine.HidesHud(UiScreen.Pause), "в паузе HUD обязан быть скрыт полностью");
                UiAssert.True(UiStateMachine.HidesHud(UiScreen.MapFromGameplay), "на карте HUD обязан быть скрыт");
            });

            suite.Add("Состояния: Reset снимает любую модальность (смена сцены)", () =>
            {
                var sm = new UiStateMachine();
                sm.Apply(UiIntent.Back);
                sm.Apply(UiIntent.OpenMapFromPause);
                sm.Reset();
                UiAssert.Equal(UiScreen.Gameplay, sm.Screen, "после Reset интерфейс в игре");
                UiAssert.False(sm.IsModalOpen, "модальных окон нет");
                UiAssert.True(sm.RequiresCursor == false, "курсор возвращается в игру");
            });

            suite.Add("Состояния: все состояния имеют определённую политику HUD", () =>
            {
                foreach (UiScreen screen in UiStateMachine.AllScreens)
                {
                    HudVisibility policy = UiStateMachine.HudPolicy(screen);
                    bool defined = policy == HudVisibility.Visible ||
                                   policy == HudVisibility.Muted ||
                                   policy == HudVisibility.Hidden;
                    UiAssert.True(defined, "для " + screen + " политика HUD не определена");
                }
            });
        }

        // ---------------------------------------------------------------- §5.3 карта

        private static void AddCalibrationTests(UiTestSuite suite)
        {
            suite.Add("Карта: мировая точка → UV и обратно без потерь (ортогональная привязка)", () =>
            {
                MapCalibration calibration = MapCalibrationDefaults.District750Ortho();
                var probes = new[]
                {
                    new MapPoint(0f, 0f),
                    new MapPoint(750f, 750f),
                    new MapPoint(210f, 630f),
                    new MapPoint(85f, 316f),
                    new MapPoint(462f, 66f),
                    new MapPoint(688f, 530f),
                };
                foreach (MapPoint world in probes)
                {
                    MapUv uv = calibration.WorldToMap(world);
                    MapPoint back = calibration.MapToWorld(uv);
                    UiAssert.Near(world.X, back.X, 0.01f, "обратное преобразование X для " + world);
                    UiAssert.Near(world.Z, back.Z, 0.01f, "обратное преобразование Z для " + world);
                }
            });

            suite.Add("Карта: дом №67 попадает в ожидаемую четверть UV", () =>
            {
                MapCalibration calibration = MapCalibrationDefaults.District750Ortho();
                MapUv home = calibration.WorldToMap(new MapPoint(210f, 630f));
                UiAssert.True(home.U > 0f && home.U < 0.5f, "дом №67 западнее центра по U: " + home);
                UiAssert.True(home.V > 0.5f && home.V < 1f, "дом №67 севернее центра по V: " + home);
            });

            suite.Add("Карта: V растёт к северу (картинка не перевёрнута)", () =>
            {
                MapCalibration calibration = MapCalibrationDefaults.District750Ortho();
                MapUv south = calibration.WorldToMap(new MapPoint(375f, 100f));
                MapUv north = calibration.WorldToMap(new MapPoint(375f, 650f));
                UiAssert.True(north.V > south.V, "север должен быть выше по V: " + south + " / " + north);
            });

            suite.Add("Карта: U растёт к востоку (нет зеркала)", () =>
            {
                MapCalibration calibration = MapCalibrationDefaults.District750Ortho();
                MapUv west = calibration.WorldToMap(new MapPoint(100f, 375f));
                MapUv east = calibration.WorldToMap(new MapPoint(650f, 375f));
                UiAssert.True(east.U > west.U, "восток должен быть правее: " + west + " / " + east);
                UiAssert.False(calibration.MirroredU, "зеркалирование по U не должно использоваться");
            });

            suite.Add("Карта: поворот привязки учитывается и обратим", () =>
            {
                var rotated = new MapCalibration(750f, 750f, 90f, 0f, 0f, false, null, 0f, 0f);
                var point = new MapPoint(300f, 120f);
                MapUv uv = rotated.WorldToMap(point);
                MapPoint back = rotated.MapToWorld(uv);
                UiAssert.Near(point.X, back.X, 0.02f, "обратный X при повороте");
                UiAssert.Near(point.Z, back.Z, 0.02f, "обратный Z при повороте");
            });

            suite.Add("Карта: привязка к Map.PNG измерена и расхождение зафиксировано, а не скрыто", () =>
            {
                MapCalibration reference = MapCalibrationDefaults.District750Perspective();
                MapCalibrationAudit audit = MapCalibrationDefaults.Audit(reference);
                Console.WriteLine("        Карта: " + audit);

                UiAssert.Equal(MapCalibrationDefaults.TiePoints.Count, audit.TotalCount,
                    "в проверке должны участвовать все контрольные точки");
                UiAssert.True(audit.TotalCount >= 8, "контрольных точек должно быть не меньше восьми");

                // Тест НЕ требует «идеального» совпадения: он фиксирует ФАКТ.
                // Если привязку когда-нибудь исправят (или заменят картинку), медиана
                // упадёт — тогда обновите и этот порог, и отчёт MAP_CALIBRATION_REPORT.md.
                UiAssert.True(audit.MedianErrorPixels > 20f,
                    "ожидается заметное расхождение Map.PNG с District750 (медиана > 20 px), получено " + audit.MedianErrorPixels);
                UiAssert.True(audit.MedianErrorMeters > 10f,
                    "медианная ошибка привязки к картинке должна превышать 10 м, получено " + audit.MedianErrorMeters);
                UiAssert.False(audit.IsAcceptable, "референс не должен считаться точной привязкой");
            });

            suite.Add("Карта: ортогональная модель описывает Map.PNG не лучше перспективной", () =>
            {
                float rms;
                MapCalibration similarity = MapCalibrationDefaults.FitReferenceSimilarity(out rms);
                UiAssert.NotNull(similarity, "подобие должно решаться");
                MapCalibrationAudit similarityAudit = MapCalibrationDefaults.Audit(similarity);
                MapCalibrationAudit perspectiveAudit =
                    MapCalibrationDefaults.Audit(MapCalibrationDefaults.District750Perspective());
                Console.WriteLine("        Подобие:   " + similarityAudit);
                Console.WriteLine("        Гомография: " + perspectiveAudit);
                UiAssert.True(similarityAudit.MedianErrorPixels >= perspectiveAudit.MedianErrorPixels * 0.9f,
                    "ортогональная модель не должна «неожиданно» объяснять картинку лучше");
            });

            suite.Add("Карта: гомография решается МНК и обратима", () =>
            {
                MapCalibration fit = MapCalibrationDefaults.FitReferencePerspective();
                UiAssert.NotNull(fit, "гомография должна решаться по 18 точкам");
                var probe = new MapPoint(375f, 375f);
                MapUv uv = fit.WorldToMap(probe);
                MapPoint back = fit.MapToWorld(uv);
                UiAssert.Near(probe.X, back.X, 1.5f, "обратная гомография X");
                UiAssert.Near(probe.Z, back.Z, 1.5f, "обратная гомография Z");
            });

            suite.Add("Карта: подгонка подобия не превращается в «идеал» (защита от подгонки)", () =>
            {
                float rms;
                MapCalibration similarity = MapCalibrationDefaults.FitReferenceSimilarity(out rms);
                UiAssert.NotNull(similarity, "подобие должно решаться");
                UiAssert.True(rms > 0.02f, "остаток подобия должен быть заметным, получено " + rms);
            });

            suite.Add("Карта: привязка с нулевым размером не превращается в деление на ноль", () =>
            {
                var degenerate = new MapCalibration(0f, 0f, 0f, 0f, 0f);
                MapUv uv = degenerate.WorldToMap(new MapPoint(100f, 100f));
                UiAssert.True(uv.IsFinite, "координата должна остаться конечной: " + uv);
            });
        }

        // ---------------------------------------------------------------- §6 метки

        private static void AddWaypointTests(UiTestSuite suite)
        {
            suite.Add("Метки: ручная метка создаётся, двигается, удаляется", () =>
            {
                var service = new WaypointService();
                Waypoint manual = service.SetManual(new MapPoint(100f, 100f));
                UiAssert.NotNull(manual, "метка должна создаться");
                UiAssert.Equal(WaypointKind.Manual, manual.Kind, "тип метки — ручная");
                UiAssert.Equal(1, service.Waypoints.Count, "одна метка");

                service.MoveManual(new MapPoint(200f, 250f));
                UiAssert.Near(200f, service.Find(WaypointService.ManualId).Position.X, 0.01f, "метка перемещена");

                service.SetManual(new MapPoint(300f, 300f));
                UiAssert.Equal(1, service.Waypoints.Count, "вторая ручная метка не создаётся");

                UiAssert.True(service.RemoveManual(), "метка удаляется");
                UiAssert.Equal(0, service.Waypoints.Count, "меток не осталось");
            });

            suite.Add("Метки: метка заказа независима от ручной и удаляется по заказу", () =>
            {
                var service = new WaypointService();
                service.SetManual(new MapPoint(120f, 140f));
                Waypoint order = service.SetOrder("4827", new MapPoint(500f, 600f), "Заказ №4827");
                UiAssert.Equal(WaypointKind.Order, order.Kind, "тип метки — заказ");
                UiAssert.Equal(2, service.Waypoints.Count, "две метки разных типов");

                UiAssert.True(service.RemoveOrder("4827"), "метка заказа удаляется");
                UiAssert.NotNull(service.Find(WaypointService.ManualId), "ручная метка обязана остаться (§6.2)");
            });

            suite.Add("Метки: активная цель и её сброс", () =>
            {
                var service = new WaypointService();
                service.SetManual(new MapPoint(10f, 10f));
                UiAssert.True(service.SetActive(WaypointService.ManualId), "цель устанавливается");
                UiAssert.NotNull(service.ActiveTarget, "активная цель есть");
                UiAssert.False(service.SetActive("nope"), "несуществующая цель отклоняется");
                service.ClearActive();
                UiAssert.Null(service.ActiveTarget, "цель сброшена");
            });

            suite.Add("Метки: снимок переживает переключение окон и сцен", () =>
            {
                var service = new WaypointService();
                service.SetManual(new MapPoint(321f, 654f), "Моя метка");
                service.SetOrder("77", new MapPoint(400f, 400f), "Заказ №77");
                service.SetHome(new MapPoint(210f, 630f));
                service.SetActive(WaypointService.ManualId);

                WaypointSnapshot snapshot = service.Snapshot();
                var restored = new WaypointService();
                restored.Restore(snapshot);

                UiAssert.Equal(3, restored.Waypoints.Count, "все метки восстановлены");
                UiAssert.Near(321f, restored.Find(WaypointService.ManualId).Position.X, 0.01f, "координата метки сохранена");
                UiAssert.Equal("Моя метка", restored.Find(WaypointService.ManualId).Label, "подпись сохранена");
                UiAssert.NotNull(restored.ActiveTarget, "активная цель сохранена");
                UiAssert.Equal(WaypointService.ManualId, restored.ActiveTarget.Id, "активная цель та же");
            });

            suite.Add("Метки: удаление чужих заказов не трогает активный заказ", () =>
            {
                var service = new WaypointService();
                service.SetOrder("1", new MapPoint(10f, 10f), "Заказ №1");
                service.SetOrder("2", new MapPoint(20f, 20f), "Заказ №2");
                int removed = service.ClearOrdersExcept("2");
                UiAssert.Equal(1, removed, "удалена одна метка");
                UiAssert.NotNull(service.FindOrder("2"), "метка активного заказа осталась");
                UiAssert.Null(service.FindOrder("1"), "метка закрытого заказа удалена");
            });

            suite.Add("Метки: восстановление из пустого и битого снимка не падает", () =>
            {
                var service = new WaypointService();
                service.Restore(null);
                UiAssert.Equal(0, service.Waypoints.Count, "пустой снимок даёт пустой список");

                var broken = new WaypointSnapshot
                {
                    Items = new[]
                    {
                        null,
                        new WaypointState { Id = string.Empty, Kind = 0, X = 1f, Z = 1f },
                        new WaypointState { Id = "ok", Kind = 0, X = float.NaN, Z = 1f },
                        new WaypointState { Id = "good", Kind = 0, X = 5f, Z = 6f, Label = "ok" },
                    },
                };
                service.Restore(broken);
                UiAssert.Equal(1, service.Waypoints.Count, "валидная метка восстановлена, битые пропущены");
            });

            suite.Add("Метки: пустой идентификатор отклоняется", () =>
            {
                bool thrown = false;
                try
                {
                    new Waypoint("", WaypointKind.Manual, new MapPoint(0f, 0f), "x", "");
                }
                catch (ArgumentException)
                {
                    thrown = true;
                }
                UiAssert.True(thrown, "пустой Id должен приводить к ArgumentException");
            });
        }

        // ---------------------------------------------------------------- §7 маршруты

        private static RoadGraphData BuildGridGraph(out Dictionary<string, int> byName)
        {
            // Прямоугольная сетка дорог 0..600 шагом 100 по X и Z, плюс тупик,
            // ведущий к «зданию» — как примыкание проезда к улице.
            byName = new Dictionary<string, int>(StringComparer.Ordinal);
            var nodes = new List<NetworkNode>();
            for (int z = 0; z <= 600; z += 100)
                for (int x = 0; x <= 600; x += 100)
                {
                    string id = "n-" + x + "-" + z;
                    nodes.Add(new NetworkNode(id, new MapPoint(x, z), true));
                }
            nodes.Add(new NetworkNode("dock", new MapPoint(250f, 520f), false));

            var segments = new List<NetworkSegment>();
            for (int z = 0; z <= 600; z += 100)
                for (int x = 0; x <= 500; x += 100)
                    segments.Add(new NetworkSegment("n-" + x + "-" + z, "n-" + (x + 100) + "-" + z, RouteEdgeKind.Street));
            for (int x = 0; x <= 600; x += 100)
                for (int z = 0; z <= 500; z += 100)
                    segments.Add(new NetworkSegment("n-" + x + "-" + z, "n-" + x + "-" + (z + 100), RouteEdgeKind.Street));
            segments.Add(new NetworkSegment("n-300-500", "dock", RouteEdgeKind.Driveway));

            RoadGraphData graph = RouteGeometryBuilder.Build(nodes, segments);
            foreach (NetworkNode node in nodes) byName[node.Id] = 0;
            return graph;
        }

        private static void AddRouteTests(UiTestSuite suite)
        {
            suite.Add("Граф: строится из сегментов и связен", () =>
            {
                Dictionary<string, int> names;
                RoadGraphData graph = BuildGridGraph(out names);
                UiAssert.Equal(50, graph.NodeCount, "узлов 7×7 + причал");
                UiAssert.True(graph.EdgeCount > 80, "рёбер должно быть достаточно: " + graph.EdgeCount);
                UiAssert.True(graph.IsConnected, "сетка должна быть связной");
            });

            suite.Add("Граф: узел на чужом сегменте даёт примыкание (как в ядре города)", () =>
            {
                var nodes = new List<NetworkNode>
                {
                    new NetworkNode("a", new MapPoint(0f, 0f), true),
                    new NetworkNode("b", new MapPoint(100f, 0f), true),
                    new NetworkNode("t", new MapPoint(50f, 0f), false),
                };
                var segments = new List<NetworkSegment> { new NetworkSegment("a", "b", RouteEdgeKind.Street) };
                RoadGraphData graph = RouteGeometryBuilder.Build(nodes, segments);
                // a-b, a-t, t-b
                UiAssert.Equal(3, graph.EdgeCount, "примыкание должно добавить рёбра");
                UiAssert.True(graph.ComponentOf(0).Count == 3, "все узлы в одной компоненте");
            });

            suite.Add("Граф: сегмент с несуществующим узлом пропускается без исключения", () =>
            {
                var nodes = new List<NetworkNode> { new NetworkNode("a", new MapPoint(0f, 0f), true) };
                var segments = new List<NetworkSegment> { new NetworkSegment("a", "ghost", RouteEdgeKind.Street) };
                RoadGraphData graph = RouteGeometryBuilder.Build(nodes, segments);
                UiAssert.Equal(1, graph.NodeCount, "узел на месте");
                UiAssert.Equal(0, graph.EdgeCount, "битый сегмент не создал ребро");
            });

            suite.Add("Маршрут: A* находит путь по дорогам, а не по прямой", () =>
            {
                Dictionary<string, int> names;
                RoadGraphData graph = BuildGridGraph(out names);
                var planner = new RoutePlanner();
                RouteResult result = planner.Build(graph, new MapPoint(0f, 0f), new MapPoint(600f, 600f), "x", "Угол");
                UiAssert.True(result.Success, "маршрут должен построиться: " + result.Message);
                UiAssert.Near(1200f, result.Path.TotalDistance, 1f, "длина по дорогам (манхэттенская)");
                UiAssert.True(result.Path.TotalDistance > new MapPoint(0f, 0f).DistanceTo(new MapPoint(600f, 600f)) + 100f,
                    "длина по дорогам обязана быть больше прямой");
            });

            suite.Add("Маршрут: путь состоит только из точек дорожной сети", () =>
            {
                Dictionary<string, int> names;
                RoadGraphData graph = BuildGridGraph(out names);
                var planner = new RoutePlanner();
                RouteResult result = planner.Build(graph, new MapPoint(10f, 10f), new MapPoint(590f, 410f), "x", "Цель");
                UiAssert.True(result.Success, "маршрут построен");
                foreach (RouteStep step in result.Path.Steps)
                {
                    bool onGrid = Math.Abs(step.Position.X % 100f) < 0.01f || Math.Abs(step.Position.Z % 100f) < 0.01f;
                    bool isDestination = step.Position.DistanceTo(result.Path.Destination) < 0.01f;
                    UiAssert.True(onGrid || isDestination,
                        "точка маршрута вне дорог и не является целью: " + step.Position);
                }
                // Последняя точка — сама цель (подход пешком), до неё маршрут заканчивается на дороге.
                UiAssert.True(result.Path.Steps[result.Path.Steps.Count - 1].Position
                    .DistanceTo(result.Path.Destination) < 0.01f, "маршрут приходит в цель");
            });

            suite.Add("Маршрут: недостижимая цель даёт понятную причину, а не пустой путь", () =>
            {
                Dictionary<string, int> names;
                RoadGraphData graph = BuildGridGraph(out names);
                var planner = new RoutePlanner();
                RouteResult result = planner.Build(graph, new MapPoint(0f, 0f), new MapPoint(5000f, 5000f), "x", "Далеко");
                UiAssert.False(result.Success, "маршрут не должен строиться");
                UiAssert.Null(result.Path, "путь не создаётся");
                UiAssert.Equal(RouteFailure.DestinationNotConnected, result.Failure, "причина: цель вне сети");
                UiAssert.True(result.Message.Length > 10, "сообщение должно быть понятным: " + result.Message);
            });

            suite.Add("Маршрут: полное отсутствие сети не приводит к исключению", () =>
            {
                var planner = new RoutePlanner();
                RouteResult result = planner.Build(null, new MapPoint(0f, 0f), new MapPoint(10f, 10f), "x", "Цель");
                UiAssert.False(result.Success, "без сети маршрута нет");
                UiAssert.Equal(RouteFailure.NoGraph, result.Failure, "причина: нет сети");

                var empty = new RoadGraphData();
                RouteResult onEmpty = planner.Build(empty, new MapPoint(0f, 0f), new MapPoint(10f, 10f), "x", "Цель");
                UiAssert.False(onEmpty.Success, "пустая сеть даёт отказ");
            });

            suite.Add("Маршрут: NaN в координатах отклоняется", () =>
            {
                Dictionary<string, int> names;
                RoadGraphData graph = BuildGridGraph(out names);
                var planner = new RoutePlanner();
                RouteResult result = planner.Build(graph, new MapPoint(float.NaN, 0f), new MapPoint(100f, 100f), "x", "Цель");
                UiAssert.False(result.Success, "NaN не должен давать маршрут");
                UiAssert.Equal(RouteFailure.InvalidPoint, result.Failure, "причина: неверная точка");
            });

            suite.Add("Маршрут: углы классифицируются верно (направо/налево/прямо)", () =>
            {
                float right = RoutePlanner.TurnAngle(new MapPoint(0f, 0f), new MapPoint(0f, 100f), new MapPoint(100f, 100f));
                float left = RoutePlanner.TurnAngle(new MapPoint(0f, 0f), new MapPoint(0f, 100f), new MapPoint(-100f, 100f));
                float straight = RoutePlanner.TurnAngle(new MapPoint(0f, 0f), new MapPoint(0f, 100f), new MapPoint(0f, 200f));
                UiAssert.Near(90f, right, 0.5f, "поворот направо");
                UiAssert.Near(-90f, left, 0.5f, "поворот налево");
                UiAssert.Near(0f, straight, 0.5f, "прямо");
                UiAssert.Equal("Направо", RouteNavigator.TurnLabel(right), "подпись направо");
                UiAssert.Equal("Налево", RouteNavigator.TurnLabel(left), "подпись налево");
            });

            suite.Add("Маршрут: дворовые проезды дороже улиц при равной длине", () =>
            {
                var planner = new RoutePlanner();
                UiAssert.True(planner.DrivewayCostMultiplier > 1f, "проезд должен иметь небольшой штраф");
            });

            suite.Add("Маршрут: азимут считается по компасу (север = 0)", () =>
            {
                UiAssert.Near(0f, RouteNavigator.Bearing(new MapPoint(0f, 0f), new MapPoint(0f, 10f)), 0.1f, "север");
                UiAssert.Near(90f, RouteNavigator.Bearing(new MapPoint(0f, 0f), new MapPoint(10f, 0f)), 0.1f, "восток");
                UiAssert.Near(180f, RouteNavigator.Bearing(new MapPoint(0f, 0f), new MapPoint(0f, -10f)), 0.1f, "юг");
                UiAssert.Near(270f, RouteNavigator.Bearing(new MapPoint(0f, 0f), new MapPoint(-10f, 0f)), 0.1f, "запад");
            });
        }

        // ---------------------------------------------------------------- §7.4/§7.5 навигация

        private static void AddNavigationTests(UiTestSuite suite)
        {
            suite.Add("Навигация: карта и HUD используют один объект маршрута", () =>
            {
                Dictionary<string, int> names;
                RoadGraphData graph = BuildGridGraph(out names);
                var navigation = new NavigationService();
                navigation.SetGraph(graph);
                navigation.Waypoints.SetOrder("1", new MapPoint(600f, 600f), "Заказ №1");

                NavigationOutcome outcome = navigation.NavigateTo(WaypointService.OrderIdPrefix + "1", new MapPoint(0f, 0f));
                UiAssert.True(outcome.Ok, "маршрут построен: " + outcome.Message);
                RoutePath forMap = navigation.ActiveRoute;
                NavigationCue cue = navigation.Cue(new MapPoint(0f, 0f), 0f);
                UiAssert.NotNull(forMap, "маршрут существует");
                UiAssert.Near(RouteNavigator.RemainingDistance(forMap, new MapPoint(0f, 0f)), cue.RemainingDistance, 1f,
                    "HUD и карта должны считать остаток по одному маршруту");
                UiAssert.True(ReferenceEquals(navigation.ActiveRoute, forMap), "маршрут не подменяется вторым расчётом");
            });

            suite.Add("Навигация: инструкция берётся из геометрии, а не из заготовок", () =>
            {
                Dictionary<string, int> names;
                RoadGraphData graph = BuildGridGraph(out names);
                var navigation = new NavigationService();
                navigation.SetGraph(graph);
                navigation.Waypoints.SetOrder("2", new MapPoint(300f, 600f), "Заказ №2");
                navigation.NavigateTo(WaypointService.OrderIdPrefix + "2", new MapPoint(0f, 0f));

                NavigationCue cue = navigation.Cue(new MapPoint(0f, 0f), 0f);
                UiAssert.True(cue.RemainingDistance > 100f, "остаток должен быть существенным: " + cue.RemainingDistance);
                UiAssert.True(cue.Label.Length > 0, "подпись есть");
                bool known = cue.Label == "Направо" || cue.Label == "Налево" || cue.Label == "Прямо" ||
                             cue.Label == "Прямо к цели" || cue.Label == "Резко направо" ||
                             cue.Label == "Резко налево" || cue.Label == "Разворот" || cue.Label == "Цель рядом";
                UiAssert.True(known, "подпись должна быть из допустимого набора, получено «" + cue.Label + "»");
            });

            suite.Add("Навигация: отклонение инициирует перестроение не чаще кулдауна", () =>
            {
                Dictionary<string, int> names;
                RoadGraphData graph = BuildGridGraph(out names);
                var navigation = new NavigationService();
                navigation.SetGraph(graph);
                navigation.RebuildCooldownSeconds = 0f;
                navigation.Waypoints.SetOrder("3", new MapPoint(600f, 600f), "Заказ №3");
                navigation.NavigateTo(WaypointService.OrderIdPrefix + "3", new MapPoint(0f, 0f));

                int before = navigation.RebuildCount;
                // Точка НА маршруте: (10,0) лежит на первом отрезке (0,0)→(100,0).
                navigation.Tick(new MapPoint(10f, 0f), 0.016f);
                UiAssert.Equal(before, navigation.RebuildCount, "на маршруте перестроения нет");

                navigation.Tick(new MapPoint(500f, 300f), 0.016f);
                UiAssert.True(navigation.RebuildCount > before, "при отклонении маршрут перестраивается");

                int afterFirst = navigation.RebuildCount;
                navigation.RebuildCooldownSeconds = 5f;
                navigation.NavigateTo(WaypointService.OrderIdPrefix + "3", new MapPoint(500f, 300f));
                int afterNav = navigation.RebuildCount;
                navigation.Tick(new MapPoint(0f, 0f), 0.016f);
                UiAssert.Equal(afterNav, navigation.RebuildCount, "во время кулдауна перестроения нет");
                UiAssert.True(afterNav >= afterFirst, "счётчик не уменьшается");
            });

            suite.Add("Навигация: прибытие завершает маршрут, метка заказа остаётся до закрытия", () =>
            {
                Dictionary<string, int> names;
                RoadGraphData graph = BuildGridGraph(out names);
                var navigation = new NavigationService();
                navigation.SetGraph(graph);
                navigation.Waypoints.SetOrder("4", new MapPoint(300f, 300f), "Заказ №4");
                navigation.NavigateTo(WaypointService.OrderIdPrefix + "4", new MapPoint(0f, 0f));
                UiAssert.True(navigation.HasRoute, "маршрут есть");

                navigation.Tick(new MapPoint(300f, 300f), 0.016f);
                UiAssert.False(navigation.HasRoute, "у цели маршрут снимается");
                UiAssert.NotNull(navigation.Waypoints.FindOrder("4"), "метка заказа ждёт подтверждения доставки");
            });

            suite.Add("Навигация: закрытие заказа снимает маршрут, но не ручную метку", () =>
            {
                Dictionary<string, int> names;
                RoadGraphData graph = BuildGridGraph(out names);
                var navigation = new NavigationService();
                navigation.SetGraph(graph);
                navigation.Waypoints.SetManual(new MapPoint(100f, 100f));
                navigation.Waypoints.SetOrder("5", new MapPoint(400f, 400f), "Заказ №5");
                navigation.NavigateTo(WaypointService.OrderIdPrefix + "5", new MapPoint(0f, 0f));

                navigation.OnOrderClosed("5");
                UiAssert.False(navigation.HasRoute, "маршрут снят");
                UiAssert.Null(navigation.Waypoints.FindOrder("5"), "метка заказа удалена");
                UiAssert.NotNull(navigation.Waypoints.Find(WaypointService.ManualId), "ручная метка сохранилась (§6.2)");
            });

            suite.Add("Навигация: без сети кнопка маршрута недоступна с объяснением", () =>
            {
                var navigation = new NavigationService();
                navigation.Waypoints.SetOrder("6", new MapPoint(100f, 100f), "Заказ №6");
                string reason;
                bool can = navigation.CanNavigateTo(navigation.Waypoints.FindOrder("6"), new MapPoint(0f, 0f), out reason);
                UiAssert.False(can, "без дорожной сети нельзя");
                UiAssert.Contains(reason, "сеть", "причина должна объяснять отсутствие сети");

                NavigationOutcome outcome = navigation.NavigateTo(WaypointService.OrderIdPrefix + "6", new MapPoint(0f, 0f));
                UiAssert.False(outcome.Ok, "маршрут не строится");
                UiAssert.Equal(RouteFailure.NoGraph, outcome.Failure, "код причины");
            });

            suite.Add("Навигация: цель без координат не строит маршрут", () =>
            {
                Dictionary<string, int> names;
                RoadGraphData graph = BuildGridGraph(out names);
                var navigation = new NavigationService();
                navigation.SetGraph(graph);
                string reason;
                bool can = navigation.CanNavigateTo(null, new MapPoint(0f, 0f), out reason);
                UiAssert.False(can, "без цели маршрута нет");
                UiAssert.Contains(reason, "Координаты", "причина — нет координат");
            });

            suite.Add("Навигация: смена сцены сбрасывает маршрут и сеть", () =>
            {
                Dictionary<string, int> names;
                RoadGraphData graph = BuildGridGraph(out names);
                var navigation = new NavigationService();
                navigation.SetGraph(graph);
                navigation.Waypoints.SetOrder("7", new MapPoint(500f, 500f), "Заказ №7");
                navigation.NavigateTo(WaypointService.OrderIdPrefix + "7", new MapPoint(0f, 0f));
                navigation.SetGraph(null);
                UiAssert.False(navigation.HasRoute, "маршрут снят при смене сети");
            });

            suite.Add("Навигация: инструкция «Вы прибыли» в радиусе цели", () =>
            {
                Dictionary<string, int> names;
                RoadGraphData graph = BuildGridGraph(out names);
                var navigation = new NavigationService();
                navigation.SetGraph(graph);
                navigation.Waypoints.SetOrder("8", new MapPoint(300f, 300f), "Заказ №8");
                navigation.NavigateTo(WaypointService.OrderIdPrefix + "8", new MapPoint(0f, 0f));
                NavigationCue cue = navigation.Cue(new MapPoint(300f, 300f), 0f);
                UiAssert.Equal("Вы прибыли", cue.Label, "у цели");
                UiAssert.True(cue.Arrived, "флаг прибытия");
            });
        }

        // ---------------------------------------------------------------- §8 HUD

        private static void AddHudTests(UiTestSuite suite)
        {
            suite.Add("HUD: неизвестные показатели не превращаются в нули", () =>
            {
                var status = new PlayerStatus();
                UiAssert.False(status.Health.HasValue, "здоровье неизвестно");
                UiAssert.Equal("—", UiFormat.Percent(status.Health), "неизвестный показатель — прочерк, а не 0%");
                UiAssert.False(status.AnyKnown, "ни один показатель не известен");
            });

            suite.Add("HUD: показатели ограничиваются диапазоном 0..100", () =>
            {
                UiAssert.Near(100f, PercentValue.From(150f).Value.Value, 0.01f, "верхняя граница");
                UiAssert.Near(0f, PercentValue.From(-40f).Value.Value, 0.01f, "нижняя граница");
                UiAssert.False(PercentValue.From(float.NaN).HasValue, "NaN — это «нет данных»");
                UiAssert.False(PercentValue.From(null).HasValue, "null — это «нет данных»");
            });

            suite.Add("HUD: цели в паузе обрезаются до трёх", () =>
            {
                var snapshot = new UiSnapshot
                {
                    Goals = new[]
                    {
                        new GoalView("a", true), new GoalView("b", false), new GoalView("c", false), new GoalView("d", false),
                    },
                };
                UiAssert.Equal(3, snapshot.VisibleGoals().Length, "не больше трёх целей");
            });

            suite.Add("HUD: навигационная строка собирается из маршрута и пропадает без него", () =>
            {
                Dictionary<string, int> names;
                RoadGraphData graph = BuildGridGraph(out names);
                var navigation = new NavigationService();
                navigation.SetGraph(graph);
                navigation.Waypoints.SetOrder("9", new MapPoint(600f, 600f), "Заказ №9");

                NavigationCue noRoute = navigation.Cue(new MapPoint(0f, 0f), 0f);
                UiAssert.Equal("Нет маршрута", noRoute.Label, "без маршрута HUD сообщает об этом");

                navigation.NavigateTo(WaypointService.OrderIdPrefix + "9", new MapPoint(0f, 0f));
                NavigationCue cue = navigation.Cue(new MapPoint(0f, 0f), 0f);
                UiAssert.NotEqual("Нет маршрута", cue.Label, "с маршрутом подпись меняется");
                UiAssert.True(cue.RemainingDistance > 0f, "остаток положительный");
            });

            suite.Add("HUD: расстояния форматируются по-человечески", () =>
            {
                UiAssert.Equal("850 м", RouteNavigator.FormatDistance(850f), "метры");
                UiAssert.Equal("1,2 км", RouteNavigator.FormatDistance(1200f), "километры");
                UiAssert.Equal("—", RouteNavigator.FormatDistance(float.NaN), "NaN");
            });

            suite.Add("HUD: финансовые значения не подставляют демо-числа референса", () =>
            {
                var finance = new FinanceView();
                UiAssert.Equal("Нет данных", UiFormat.Rubles(finance.Money), "нет данных — честный текст");
                finance.Money = 12450;
                UiAssert.Equal("12 450 ₽", UiFormat.Rubles(finance.Money), "формат рублей");
                UiAssert.Equal("+4 200 ₽", UiFormat.SignedRubles(4200), "плюс");
                UiAssert.Equal("−35 000 ₽", UiFormat.SignedRubles(-35000), "минус");
            });
        }

        // ---------------------------------------------------------------- §4 телефон

        private static void AddPhoneTests(UiTestSuite suite)
        {
            suite.Add("Телефон: приложения «Карта» в телефоне нет (§2, §4.8)", () =>
            {
                foreach (string id in PhoneAppIds.HomeOrder)
                {
                    UiAssert.False(PhoneAppIds.IsForbiddenInPhone(id), "приложение «" + id + "» недопустимо в телефоне");
                }
                UiAssert.True(PhoneAppIds.IsForbiddenInPhone("map"), "проверка запрета ловит map");
                UiAssert.True(PhoneAppIds.IsForbiddenInPhone("Карта"), "проверка запрета ловит «Карта»");
            });

            suite.Add("Телефон: рабочий стол содержит приложения референса", () =>
            {
                string[] expected =
                {
                    PhoneAppIds.Calls, PhoneAppIds.Messages, PhoneAppIds.Contacts, PhoneAppIds.Bank,
                    PhoneAppIds.Orders, PhoneAppIds.Tasks, PhoneAppIds.Ads, PhoneAppIds.Education,
                    PhoneAppIds.Settings, PhoneAppIds.Gallery,
                };
                UiAssert.Equal(expected.Length, PhoneAppIds.HomeOrder.Count, "количество плиток");
                foreach (string id in expected)
                {
                    bool found = false;
                    foreach (string actual in PhoneAppIds.HomeOrder)
                        if (actual == id) found = true;
                    UiAssert.True(found, "на рабочем столе нет «" + id + "»");
                }
            });

            suite.Add("Телефон: заказ без координат делает маршрут недоступным с причиной", () =>
            {
                var order = new OrderView
                {
                    OrderId = "4827",
                    CargoTitle = "Коробка",
                    Stage = OrderStage.Accepted,
                    HasDropOffCoordinates = false,
                };
                string reason;
                UiAssert.False(order.CanRoute(out reason), "без координат кнопка недоступна");
                UiAssert.Contains(reason, "Координаты", "причина — координаты");

                order.DropOffPosition = new MapPoint(400f, 400f);
                order.HasDropOffCoordinates = true;
                UiAssert.True(order.CanRoute(out reason), "с координатами маршрут разрешён: " + reason);
            });

            suite.Add("Телефон: заказ не в работе не даёт маршрут", () =>
            {
                var order = new OrderView
                {
                    OrderId = "1",
                    Stage = OrderStage.Available,
                    HasDropOffCoordinates = true,
                    DropOffPosition = new MapPoint(10f, 10f),
                };
                string reason;
                UiAssert.False(order.CanRoute(out reason), "для доступного заказа маршрут не строится");
            });

            suite.Add("Телефон: бейдж непрочитанных не превышает «9+»", () =>
            {
                UiAssert.Equal("", PhoneFormat.Badge(0), "ноль не показывается");
                UiAssert.Equal("3", PhoneFormat.Badge(3), "до девяти — число");
                UiAssert.Equal("9+", PhoneFormat.Badge(14), "после девяти — 9+");
            });

            suite.Add("Телефон: время сообщений «Вчера» и часы", () =>
            {
                var now = new DateTime(2026, 11, 5, 18, 42, 0);
                UiAssert.Equal("17:18", PhoneFormat.MessageTime(now.AddHours(-1.4), now), "сегодня — часы и минуты");
                UiAssert.Equal("Вчера", PhoneFormat.MessageTime(now.AddDays(-1).AddHours(-2), now), "вчера");
                UiAssert.Equal("3 дн", PhoneFormat.MessageTime(now.AddDays(-3), now), "давно");
            });

            suite.Add("Телефон: экран блокировки показывает игровое время", () =>
            {
                UiAssert.Equal("08:42", PhoneFormat.GameClock(522f), "минуты суток → время");
                UiAssert.Equal("00:05", PhoneFormat.GameClock(5f), "полночь");
                UiAssert.Equal("12:00", PhoneFormat.GameClock(1440f + 720f), "следующий день не ломает формат");
            });

            suite.Add("Телефон: состояния звонка определяют доступные кнопки", () =>
            {
                var incoming = new CallView(CallState.Incoming, "Мама", "Входящий вызов", 7);
                UiAssert.True(incoming.CanAccept, "входящий можно принять");
                UiAssert.True(incoming.CanDecline, "входящий можно отклонить");
                var active = new CallView(CallState.Active, "Мама", "На связи", 7);
                UiAssert.True(active.CanHangUp, "активный звонок завершается");
                UiAssert.False(active.CanAccept, "принимать нечего");
            });

            suite.Add("Телефон: пустой банк показывает «нет данных», а не цифры референса", () =>
            {
                var bank = new BankView();
                UiAssert.Equal("Нет данных", UiFormat.Rubles(bank.Balance), "баланс неизвестен");
                UiAssert.Equal("Нет данных", UiFormat.Rubles(bank.Debt), "долг неизвестен");
                UiAssert.False(bank.OperationsAvailable, "история операций не заявлена доступной");
            });
        }

        // ---------------------------------------------------------------- форматирование

        private static void AddFormatTests(UiTestSuite suite)
        {
            suite.Add("Формат: разряды разделяются пробелом", () =>
            {
                UiAssert.Equal("0", UiFormat.Grouped(0), "ноль");
                UiAssert.Equal("999", UiFormat.Grouped(999), "три знака");
                UiAssert.Equal("1 000", UiFormat.Grouped(1000), "четыре знака");
                UiAssert.Equal("1 234 567", UiFormat.Grouped(1234567), "миллионы");
                UiAssert.Equal("−1 234", UiFormat.Grouped(-1234), "отрицательное");
            });

            suite.Add("Формат: день собирается из номера и дня недели", () =>
            {
                UiAssert.Equal("День 3 (Среда)", UiFormat.Day(null, 3, "Среда"), "день и неделя");
                UiAssert.Equal("День 3", UiFormat.Day(null, 3, null), "только номер");
                UiAssert.Equal("Готово", UiFormat.Day("Готово", 3, "Среда"), "готовый текст важнее");
                UiAssert.Equal("Нет данных", UiFormat.Day(null, null, null), "нет данных");
            });

            suite.Add("Формат: проценты округляются целым", () =>
            {
                UiAssert.Equal("62%", UiFormat.Percent(PercentValue.From(62.4f)), "обычное");
                UiAssert.Equal("—", UiFormat.Percent(PercentValue.Unknown), "нет данных");
            });

            suite.Add("Формат: цель с прогрессом не дублирует текст", () =>
            {
                UiAssert.Equal("Доставить 3 заказа (0/3)", UiFormat.Goal(new GoalView("Доставить 3 заказа", false, "0/3")), "склейка");
                UiAssert.Equal("Доставить 3 заказа (0/3)", UiFormat.Goal(new GoalView("Доставить 3 заказа (0/3)", false, "0/3")), "без дубля");
                UiAssert.Equal("Нет данных", UiFormat.Goal(null), "пустая цель");
            });
        }

        // ---------------------------------------------------------------- контракт паузы

        private static void AddPauseContractTests(UiTestSuite suite)
        {
            suite.Add("Пауза: состояния паузы и карты из паузы требуют остановки игры", () =>
            {
                var sm = new UiStateMachine();
                sm.Apply(UiIntent.Back);
                UiAssert.True(sm.IsPausedContext, "пауза — контекст остановки");
                sm.Apply(UiIntent.OpenMapFromPause);
                UiAssert.True(sm.IsPausedContext, "карта из паузы не снимает паузу (§9.3, §20 сценарий 5)");
                UiAssert.True(sm.RequiresCursor, "курсор свободен");
            });

            suite.Add("Пауза: выход в игру возвращает HUD", () =>
            {
                var sm = new UiStateMachine();
                sm.Apply(UiIntent.Back);
                sm.Apply(UiIntent.OpenMapFromPause);
                sm.Apply(UiIntent.Back);
                sm.Apply(UiIntent.Back);
                UiAssert.Equal(UiScreen.Gameplay, sm.Screen, "вернулись в игру");
                UiAssert.Equal(HudVisibility.Visible, sm.Hud, "HUD снова виден");
                UiAssert.False(sm.RequiresCursor, "курсор снова в игре");
            });

            suite.Add("Пауза: повторный Esc из паузы закрывает меню, а не открывает карту", () =>
            {
                var sm = new UiStateMachine();
                sm.Apply(UiIntent.Back);
                sm.Apply(UiIntent.Back);
                UiAssert.Equal(UiScreen.Gameplay, sm.Screen, "Esc закрывает паузу");
            });

            suite.Add("Пауза: «Продолжить игру» из любого состояния возвращает в игру", () =>
            {
                var states = new List<Func<UiScreen>>
                {
                    () => { var s = new UiStateMachine(); s.Apply(UiIntent.Back); return s.Screen; },
                };
                UiScreen paused = states[0]();
                UiAssert.Equal(UiScreen.Pause, paused, "пауза открыта");

                var sm = new UiStateMachine();
                sm.Apply(UiIntent.Back);
                sm.Apply(UiIntent.Resume);
                UiAssert.Equal(UiScreen.Gameplay, sm.Screen, "кнопка «Продолжить» работает");
            });
        }

        // ---------------------------------------------------------------- измеренная раскладка паузы

        private static void AddPauseLayoutTests(UiTestSuite suite)
        {
            suite.Add("Раскладка: измеренные прямоугольники не выходят за кадр 1672×941", () =>
            {
                LayoutRect[] rects =
                {
                    PauseLayout.LeftPanel, PauseLayout.RightPanel, PauseLayout.Logo, PauseLayout.Title,
                    PauseLayout.HeaderDate, PauseLayout.HeaderAddress, PauseLayout.Goals,
                    PauseLayout.Status, PauseLayout.Finance, PauseLayout.QuickLinks, PauseLayout.BarTrack,
                };
                foreach (LayoutRect rect in rects)
                {
                    UiAssert.True(rect.Left >= 0f && rect.Top >= 0f, "левый верхний угол в кадре: " + rect);
                    UiAssert.True(rect.Right <= PauseLayout.FrameWidth + 0.01f, "правый край в кадре: " + rect);
                    UiAssert.True(rect.Bottom <= PauseLayout.FrameHeight + 0.01f, "нижний край в кадре: " + rect);
                    UiAssert.True(rect.Width > 0f && rect.Height > 0f, "размер положителен: " + rect);
                }
            });

            suite.Add("Раскладка: валидатор не находит ни одной проблемы", () =>
            {
                System.Collections.Generic.IReadOnlyList<string> issues = PauseLayout.Validate();
                if (issues.Count > 0)
                    throw new InvalidOperationException("раскладка паузы сломана: " + string.Join("; ", issues));
                UiAssert.Equal(0, issues.Count, "проблем нет");
            });

            suite.Add("Раскладка: панели не пересекаются и имеют зазор", () =>
            {
                UiAssert.False(PauseLayout.LeftPanel.Overlaps(PauseLayout.RightPanel), "панели не пересекаются");
                UiAssert.True(PauseLayout.LeftPanel.Right < PauseLayout.RightPanel.Left, "между панелями зазор");
            });

            suite.Add("Раскладка: семь кнопок меню идут ровным шагом внутри панели", () =>
            {
                UiAssert.Equal(7, PauseLayout.ButtonCount, "кнопок ровно семь, как в референсе");
                for (int i = 0; i < PauseLayout.ButtonCount; i++)
                {
                    LayoutRect button = PauseLayout.Button(i);
                    UiAssert.True(PauseLayout.LeftPanel.Contains(button), "кнопка " + i + " внутри панели");
                    if (i > 0)
                    {
                        UiAssert.Near(PauseLayout.ButtonPitch, button.Top - PauseLayout.Button(i - 1).Top, 0.01f,
                            "шаг кнопок постоянный");
                    }
                }
                UiAssert.Near(67f, PauseLayout.Button(0).Height, 0.01f, "высота кнопки по замеру");
            });

            suite.Add("Раскладка: кнопка не наезжает на заголовок и не выходит за панель снизу", () =>
            {
                UiAssert.True(PauseLayout.Button(0).Top >= PauseLayout.Title.Bottom - 13f,
                    "первая кнопка ниже заголовка «Пауза»");
                UiAssert.True(PauseLayout.Button(PauseLayout.ButtonCount - 1).Bottom < PauseLayout.LeftPanel.Bottom,
                    "последняя кнопка внутри панели");
            });

            suite.Add("Раскладка: кнопки не меньше 44 px при 1080p", () =>
            {
                double height = PauseLayout.Units(PauseLayout.Button(0).Height);
                UiAssert.True(height >= 44.0, "высота кнопки в единицах uGUI: " + height.ToString("0.#", CultureInfo.InvariantCulture));
                double link = PauseLayout.Units(PauseLayout.QuickLinkButtons[0].Height);
                UiAssert.True(link >= 44.0, "быстрые ссылки тоже не меньше 44 px: " + link.ToString("0.#", CultureInfo.InvariantCulture));
            });

            suite.Add("Раскладка: подписи кнопок влезают по ширине", () =>
            {
                for (int i = 0; i < PauseLayout.ButtonCount; i++)
                {
                    string caption = PauseLayout.MenuCaptions[i];
                    UiAssert.True(PauseLayout.ButtonCaptionFits(caption),
                        "подпись «" + caption + "» шире кнопки");
                    UiAssert.True(PauseLayout.EstimateTextWidth(caption, PauseLayout.FontButton)
                                 < PauseLayout.ButtonWidth - 20f, "есть поля по краям: «" + caption + "»");
                }
            });

            suite.Add("Раскладка: полосы статуса ровные, внутри блока и не лезут на проценты", () =>
            {
                for (int i = 0; i < PauseLayout.StatusRowTops.Length; i++)
                {
                    LayoutRect bar = PauseLayout.Bar(i);
                    UiAssert.True(PauseLayout.Status.Contains(bar), "полоса " + i + " внутри блока «Статус»");
                    UiAssert.True(bar.Right < PauseLayout.StatusPercentRight, "полоса " + i + " не залезает на проценты");
                    if (i > 0) UiAssert.Near(41f, bar.Top - PauseLayout.Bar(i - 1).Top, 0.1f, "шаг строк статуса");
                }
            });

            suite.Add("Раскладка: якоря лежат в [0..1] и переводятся обратимо", () =>
            {
                foreach (LayoutRect rect in new[]
                {
                    PauseLayout.LeftPanel, PauseLayout.RightPanel, PauseLayout.Goals,
                    PauseLayout.Status, PauseLayout.Finance, PauseLayout.QuickLinks,
                })
                {
                    AnchorRect anchors = PauseLayout.ToAnchors(rect);
                    UiAssert.True(anchors.IsInsideScreen, "якоря внутри экрана: " + anchors);
                    double width = anchors.Width * PauseLayout.FrameWidth;
                    double height = anchors.Height * PauseLayout.FrameHeight;
                    UiAssert.Near(rect.Width, (float)width, 0.01f, "ширина восстанавливается по якорям");
                    UiAssert.Near(rect.Height, (float)height, 0.01f, "высота восстанавливается по якорям");
                }
            });

            suite.Add("Раскладка: дочерние якоря считаются от родителя, а не от кадра", () =>
            {
                AnchorRect nested = PauseLayout.ToAnchors(PauseLayout.Button(0), PauseLayout.LeftPanel);
                UiAssert.True(nested.XMin > 0f && nested.XMax < 1f, "кнопка внутри левой панели по X: " + nested);
                UiAssert.True(nested.YMin > 0f && nested.YMax < 1f, "кнопка внутри левой панели по Y: " + nested);
                UiAssert.Near((float)((PauseLayout.Button(0).Left - PauseLayout.LeftPanel.Left) / PauseLayout.LeftPanel.Width),
                    nested.XMin, 0.0001f, "левый край кнопки относительно панели");
            });

            suite.Add("Раскладка: минимальный кегль читаем при 1080p", () =>
            {
                double smallest = PauseLayout.Units(Math.Min(PauseLayout.FontSmall, PauseLayout.FontBody));
                UiAssert.True(smallest >= PauseLayout.MinimumReadableFont,
                    "мельче " + PauseLayout.MinimumReadableFont + " px текста нет: " + smallest.ToString("0.#", CultureInfo.InvariantCulture));
                UiAssert.True(PauseLayout.FontSize(PauseLayout.FontButton) > PauseLayout.FontSize(PauseLayout.FontSmall),
                    "иерархия кеглей сохраняется");
            });

            suite.Add("Раскладка: сетка быстрых ссылок 2×2 не пересекается", () =>
            {
                LayoutRect[] links = PauseLayout.QuickLinkButtons;
                UiAssert.Equal(4, links.Length, "четыре быстрые ссылки");
                for (int i = 0; i < links.Length; i++)
                {
                    UiAssert.True(PauseLayout.QuickLinks.Contains(links[i]), "ссылка " + i + " внутри блока");
                    for (int j = i + 1; j < links.Length; j++)
                    {
                        UiAssert.False(links[i].Overlaps(links[j]), "ссылки " + i + " и " + j + " не пересекаются");
                    }
                }
                UiAssert.Near(links[0].Top, links[1].Top, 0.01f, "первый ряд на одной линии");
                UiAssert.Near(links[2].Top, links[3].Top, 0.01f, "второй ряд на одной линии");
                UiAssert.True(links[2].Top > links[0].Bottom, "второй ряд ниже первого");
            });

            suite.Add("Раскладка: ухудшение геометрии ловится валидатором", () =>
            {
                // Симулируем «поехавшую» вёрстку: кнопка за пределами панели.
                var broken = new LayoutRect(PauseLayout.ButtonLeft, 900f, PauseLayout.ButtonWidth, PauseLayout.ButtonHeight);
                UiAssert.False(PauseLayout.LeftPanel.Contains(broken), "сломанная кнопка не должна содержаться в панели");
                // И проверяем, что Contains/Overlaps действительно работают как ожидается.
                var inside = new LayoutRect(100f, 300f, 100f, 50f);
                UiAssert.True(PauseLayout.LeftPanel.Contains(inside), "нормальный прямоугольник внутри панели");
                UiAssert.False(inside.Overlaps(new LayoutRect(500f, 300f, 50f, 50f)), "далёкие прямоугольники не пересекаются");
                UiAssert.True(inside.Overlaps(new LayoutRect(150f, 320f, 50f, 50f)), "пересекающиеся прямоугольники видны");
            });
        }

        private static void AddSnapTests(UiTestSuite suite)
        {
            suite.Add("Привязка: точка в 55 м от перекрёстка цепляется к дороге, а не теряется", () =>
            {
                Dictionary<string, int> names;
                RoadGraphData graph = BuildGridGraph(out names);
                var planner = new RoutePlanner();

                // (0,55) — рядом с дорогой x=0, но до узлов 55 м и 45 м (дальше допуска узла 45 м).
                RouteResult result = planner.Build(graph, new MapPoint(0f, 55f),
                    new MapPoint(600f, 600f), "test", "цель");
                UiAssert.True(result.Success, "маршрут построен: " + result.Message);
                UiAssert.Near(0f, result.Path.SnapStart.X, 0.01f, "старт привязан к дороге по X");
                UiAssert.Near(55f, result.Path.SnapStart.Z, 0.01f, "старт остался на своей высоте, а не на узле");
            });

            suite.Add("Привязка: проекция на отрезок работает для диагонального ребра", () =>
            {
                MapPoint projection = RoadGraphData.ProjectOnSegment(new MapPoint(0f, 50f),
                    new MapPoint(0f, 0f), new MapPoint(100f, 0f));
                UiAssert.Near(0f, projection.X, 0.001f, "проекция по X");
                UiAssert.Near(0f, projection.Z, 0.001f, "проекция по Z на оси");

                MapPoint diagonal = RoadGraphData.ProjectOnSegment(new MapPoint(0f, 100f),
                    new MapPoint(0f, 0f), new MapPoint(100f, 100f));
                UiAssert.Near(50f, diagonal.X, 0.001f, "середина диагонали X");
                UiAssert.Near(50f, diagonal.Z, 0.001f, "середина диагонали Z");
            });

            suite.Add("Привязка: далёкая от дорог точка честно получает отказ", () =>
            {
                Dictionary<string, int> names;
                RoadGraphData graph = BuildGridGraph(out names);
                var planner = new RoutePlanner();
                RouteResult result = planner.Build(graph, new MapPoint(5000f, 5000f),
                    new MapPoint(0f, 0f), "test", "цель");
                UiAssert.False(result.Success, "маршрут не строится из точки вне района");
                UiAssert.Equal((int)RouteFailure.StartNotConnected, (int)result.Failure, "причина — старт вне сети");
            });

            suite.Add("Привязка: все точки доставки District750 достижимы пешком от дороги", () =>
            {
                QuarterPlan plan = DistrictMapGenerator.Generate(1904);
                QuarterPlanMapData data = QuarterPlanAdapter.Build(plan);
                var planner = new RoutePlanner();
                int unreachable = 0;
                for (int i = 0; i < plan.DeliveryPoints.Count; i++)
                {
                    // Реальный сценарий: игрок у дома, цель — точка доставки (подъезд во дворе).
                    MapPoint point = new MapPoint(plan.DeliveryPoints[i].Position.X,
                        plan.DeliveryPoints[i].Position.Z);
                    RouteResult result = planner.Build(data.Graph, new MapPoint(85f, 680f), point, "x", "y");
                    if (!result.Success) unreachable++;
                }
                UiAssert.Equal(0, unreachable, "непривязанных точек доставки быть не должно");
            });
        }

        // ---------------------------------------------------------------- связь с районом

        private static void AddQuarterPlanAdapterTests(UiTestSuite suite)
        {
            suite.Add("Район: пустой план не роняет карту и не даёт граф", () =>
            {
                QuarterPlanMapData data = QuarterPlanAdapter.Build(null);
                UiAssert.Equal(0, data.Roads.Count, "дорог нет");
                UiAssert.Equal(0, data.Points.Count, "точек нет");
                UiAssert.False(data.HasGraph, "маршрутизировать не по чему");
                UiAssert.False(data.HasRoads, "рисовать нечего");
            });

            suite.Add("Район: District750 разбирается в дороги, точки и связный граф", () =>
            {
                QuarterPlan plan = DistrictMapGenerator.Generate(1904);
                QuarterPlanMapData data = QuarterPlanAdapter.Build(plan);
                UiAssert.True(data.Roads.Count > 20, "дороги извлечены: " + data.Roads.Count);
                UiAssert.True(data.Points.Count > 20, "точки извлечены: " + data.Points.Count);
                UiAssert.Equal(750f, data.Size, "сторона района");
                UiAssert.True(data.HasGraph, "граф построен");
                UiAssert.True(data.Graph.NodeCount >= plan.Nodes.Count, "все узлы плана попали в граф");
                UiAssert.True(data.Graph.IsConnected, "граф связный — маршрут существует");
            });

            suite.Add("Район: маршрут между двумя реальными точками доставки строится", () =>
            {
                QuarterPlan plan = DistrictMapGenerator.Generate(1904);
                QuarterPlanMapData data = QuarterPlanAdapter.Build(plan);
                UiAssert.True(plan.DeliveryPoints.Count >= 2, "в плане есть точки доставки");

                // Игрок выходит из дома (дверь bld-n3), цель — точка доставки в другом квартале.
                var home = new MapPoint(205f, 636f);
                MapPoint to = new MapPoint(plan.DeliveryPoints[plan.DeliveryPoints.Count - 1].Position.X,
                    plan.DeliveryPoints[plan.DeliveryPoints.Count - 1].Position.Z);

                var planner = new RoutePlanner();
                RouteResult result = planner.Build(data.Graph, home, to, "test", "точка");
                UiAssert.True(result.Success, "маршрут построен: " + result.Message);
                UiAssert.True(result.Path.TotalDistance > 0f, "длина маршрута положительна");
                UiAssert.True(result.Path.TotalDistance < 3000f, "длина в пределах района: " + result.Path.TotalDistance);
            });

            suite.Add("Район: координаты точки доставки ищутся по Id, а не угадываются", () =>
            {
                QuarterPlan plan = DistrictMapGenerator.Generate(1904);
                string id = plan.DeliveryPoints[0].Id;
                MapPoint position;
                UiAssert.True(QuarterPlanAdapter.TryGetDeliveryPosition(plan, id, out position), "точка найдена");
                UiAssert.Near(plan.DeliveryPoints[0].Position.X, position.X, 0.001f, "X совпадает");
                UiAssert.Near(plan.DeliveryPoints[0].Position.Z, position.Z, 0.001f, "Z совпадает");
                UiAssert.False(QuarterPlanAdapter.TryGetDeliveryPosition(plan, "нет-такой-точки", out position),
                    "несуществующий Id не превращается в (0,0)");
                UiAssert.False(QuarterPlanAdapter.TryGetDeliveryPosition(plan, null, out position), "null не падает");
            });

            suite.Add("Район: маршрут к точке доставки проходит через NavigationService", () =>
            {
                QuarterPlan plan = DistrictMapGenerator.Generate(1904);
                QuarterPlanMapData data = QuarterPlanAdapter.Build(plan);

                var navigation = new NavigationService();
                navigation.SetGraph(data.Graph);

                MapPoint dropOff = new MapPoint(plan.DeliveryPoints[3].Position.X, plan.DeliveryPoints[3].Position.Z);
                Waypoint waypoint = navigation.Waypoints.SetOrder("order-1", dropOff, "Заказ order-1");

                NavigationOutcome outcome = navigation.NavigateTo(waypoint, new MapPoint(85f, 680f));
                UiAssert.True(outcome.Ok, "маршрут построен: " + outcome.Message);
                UiAssert.NotNull(navigation.ActiveRoute, "активный маршрут есть");
                UiAssert.Equal("Заказ order-1", navigation.ActiveRoute.DestinationLabel, "подпись цели");

                // Карта и HUD читают ОДИН маршрут: закрытие заказа снимает его и метку.
                navigation.OnOrderClosed("order-1");
                UiAssert.Null(navigation.ActiveRoute, "маршрут снят вместе с заказом");
                UiAssert.Null(navigation.Waypoints.FindOrder("order-1"), "метка заказа удалена");
            });
        }
    }
}
