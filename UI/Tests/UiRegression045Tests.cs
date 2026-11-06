// Life From Zero — UI tests (ТЗ №045)
// Namespace: LifeFromZero.UI.Tests
//
// РЕГРЕССИИ ПО СЛЕДАМ РЕАЛЬНОГО PLAY MODE в сцене Apartment67.
//
// Эти тесты проверяют РЕШЕНИЯ, которые привели к ошибкам в игре:
//   * перерисовка карты на каждое обращение (и как следствие — сотни исключений
//     от слоя без CanvasRenderer);
//   * пустой рабочий стол телефона и подсказка про PhoneAppRegistry, которая
//     к плиткам телефона отношения не имеет;
//   * порядок выбора источника данных телефона (поле → мост доставки → мост сцены → снимок);
//   * «план района не подключён» как ошибка там, где района нет вовсе.
//
// ЧЕГО ЭТИ ТЕСТЫ НЕ ДЕЛАЮТ (и не должны):
//   * не создают CanvasRenderer в Unity и не проверяют uGUI;
//   * не открывают сцены Apartment67/District750;
//   * не заменяют Play Mode.
// Настоящая проверка исправления №2 — Unity Editor: Tools → Life From Zero → UI →
// Verify Scenes & Adapters и запуск сцены (см. CODEX_FIX_UI.md).

using System;
using System.Collections.Generic;
using LifeFromZero.UI.Core;

namespace LifeFromZero.UI.Tests
{
    public static class UiRegression045Tests
    {
        public static void AddTo(UiTestSuite suite)
        {
            AddMapRedrawTests(suite);
            AddPhoneHomeTests(suite);
            AddPhoneSourcePriorityTests(suite);
            AddDistrictPlanTests(suite);
        }

        // ---------------------------------------------------------------- карта: перерисовка

        private static MapFrameKey Key(float zoom = 1.15f, float centerX = 375f, float centerY = 375f,
            float gridStep = 100f, int geometryId = 7, int route = 0, float playerX = 10f, float playerY = 20f,
            bool visible = true)
        {
            return new MapFrameKey(visible, zoom, centerX, centerY, 750f, 750f, gridStep, geometryId, route,
                true, MapRedrawGate.PlayerCell(playerX), MapRedrawGate.PlayerCell(playerY));
        }

        private static void AddMapRedrawTests(UiTestSuite suite)
        {
            suite.Add("Карта №045: повторный Refresh без изменений НЕ перерисовывает меш", () =>
            {
                var gate = new MapRedrawGate();

                UiAssert.True(gate.ShouldRedraw(Key()), "первое обращение обязано нарисовать карту");
                UiAssert.False(gate.ShouldRedraw(Key()), "повтор с теми же данными не должен перерисовывать");
                UiAssert.False(gate.ShouldRedraw(Key()), "третье обращение тоже не перерисовывает");

                UiAssert.Equal(1, gate.RedrawCount, "перерисовка должна быть ровно одна");
                UiAssert.Equal(2, gate.SkippedCount, "пропущенные обращения считаются");
            });

            suite.Add("Карта №045: изменение зума/центра/сетки/геометрии/маршрута/игрока перерисовывает", () =>
            {
                var cases = new List<KeyValuePair<string, MapFrameKey>>
                {
                    new KeyValuePair<string, MapFrameKey>("зум", Key(zoom: 1.4f)),
                    new KeyValuePair<string, MapFrameKey>("центр", Key(centerX: 200f)),
                    new KeyValuePair<string, MapFrameKey>("шаг сетки", Key(gridStep: 50f)),
                    new KeyValuePair<string, MapFrameKey>("геометрия (сменился район)", Key(geometryId: 8)),
                    new KeyValuePair<string, MapFrameKey>("маршрут", Key(route: 12345)),
                    new KeyValuePair<string, MapFrameKey>("позиция игрока", Key(playerX: 40f)),
                    new KeyValuePair<string, MapFrameKey>("видимость", Key(visible: false)),
                };

                foreach (KeyValuePair<string, MapFrameKey> item in cases)
                {
                    var gate = new MapRedrawGate();
                    gate.ShouldRedraw(Key());
                    UiAssert.True(gate.ShouldRedraw(item.Value), "изменение «" + item.Key + "» должно перерисовывать карту");
                }
            });

            suite.Add("Карта №045: дробное смещение игрока внутри полуметра не перерисовывает карту", () =>
            {
                var gate = new MapRedrawGate();
                gate.ShouldRedraw(Key(playerX: 10f, playerY: 20f));

                UiAssert.False(gate.ShouldRedraw(Key(playerX: 10.2f, playerY: 20.2f)),
                    "метка игрока не должна дёргать меш на каждый кадр");
                UiAssert.True(gate.ShouldRedraw(Key(playerX: 11.0f, playerY: 20f)),
                    "переход в следующую полуметровую ячейку перерисовывает карту");
            });

            suite.Add("Карта №045: Reset() (смена района/сцены) перерисовывает даже без изменений", () =>
            {
                var gate = new MapRedrawGate();
                gate.ShouldRedraw(Key());
                gate.Reset();

                UiAssert.True(gate.ShouldRedraw(Key()), "после Reset карта рисуется заново");
                UiAssert.Equal(2, gate.RedrawCount, "Reset не отменяет учёт перерисовок");
            });

            suite.Add("Карта №045: подпись маршрута зависит от маршрута, а не от форматирования", () =>
            {
                RoutePath first = BuildRoute("ord-1", 120f, new MapPoint(100f, 100f));
                RoutePath same = BuildRoute("ord-1", 120f, new MapPoint(100f, 100f));
                RoutePath otherLength = BuildRoute("ord-1", 180f, new MapPoint(100f, 100f));
                RoutePath otherTarget = BuildRoute("ord-2", 120f, new MapPoint(100f, 100f));

                UiAssert.Equal(MapRedrawGate.RouteSignature(first), MapRedrawGate.RouteSignature(same),
                    "одинаковый маршрут — одинаковая подпись");
                UiAssert.NotEqual(MapRedrawGate.RouteSignature(first), MapRedrawGate.RouteSignature(otherLength),
                    "другая длина маршрута — другая подпись");
                UiAssert.NotEqual(MapRedrawGate.RouteSignature(first), MapRedrawGate.RouteSignature(otherTarget),
                    "другая цель — другая подпись");
                UiAssert.Equal(0, MapRedrawGate.RouteSignature(null), "маршрута нет — подпись 0");
            });
        }

        private static RoutePath BuildRoute(string destinationId, float distance, MapPoint destination)
        {
            var steps = new[]
            {
                new RouteStep(new MapPoint(0f, 0f), RouteEdgeKind.Street, false, 0f),
                new RouteStep(new MapPoint(10f, 10f), RouteEdgeKind.Street, false, 14f),
            };
            return new RoutePath(destinationId, "Цель", destination, steps, distance,
                new MapPoint(0f, 0f), new MapPoint(9f, 9f), false);
        }

        // ---------------------------------------------------------------- телефон: рабочий стол

        private static void AddPhoneHomeTests(UiTestSuite suite)
        {
            suite.Add("Телефон №045: без источника данных рабочий стол показывает неактивные заглушки и честный текст", () =>
            {
                PhoneHomeContent home = PhoneHomeState.Resolve(Array.Empty<PhoneAppTile>(), false);

                UiAssert.True(home.Placeholder, "без IPhoneDataSource плитки должны быть заглушками");
                UiAssert.True(home.Tiles.Count > 0, "экран не должен быть пустым");
                UiAssert.Equal(PhoneHomeState.NoDataSourceNotice, home.Notice, "игрок должен увидеть причину");

                for (int i = 0; i < home.Tiles.Count; i++)
                    UiAssert.False(home.Tiles[i].Available, "заглушка «" + home.Tiles[i].Id + "» не должна быть активной");
            });

            suite.Add("Телефон №045: подсказка про PhoneAppRegistry больше не выдаётся как инструкция", () =>
            {
                PhoneHomeContent home = PhoneHomeState.Resolve(Array.Empty<PhoneAppTile>(), false);
                string text = home.Notice ?? string.Empty;

                UiAssert.False(text.Contains("PhoneAppRegistry"),
                    "плитки приходят из PhoneSnapshot.Apps, а не из PhoneAppRegistry.CreateDefault()");
                UiAssert.False(text.Contains("CreateDefault"),
                    "сообщение игроку не должно содержать имя метода");
            });

            suite.Add("Телефон №045: данные пришли — ни заглушек, ни сообщения", () =>
            {
                IReadOnlyList<PhoneAppTile> apps = PhoneAppCatalog.Build(true);
                PhoneHomeContent home = PhoneHomeState.Resolve(apps, true);

                UiAssert.False(home.Placeholder, "с данными заглушки не показываются");
                UiAssert.Null(home.Notice, "сообщения нет");
                UiAssert.Equal(apps.Count, home.Tiles.Count, "все плитки на месте");
                UiAssert.True(home.Tiles[0].Available, "первая плитка активна (данные настоящие)");
            });

            suite.Add("Телефон №045: источник есть, но список пуст — отдельное сообщение", () =>
            {
                PhoneHomeContent home = PhoneHomeState.Resolve(null, true);

                UiAssert.True(home.Placeholder, "пустой снимок — заглушки");
                UiAssert.Equal(PhoneHomeState.EmptySourceNotice, home.Notice,
                    "нельзя писать «источник не подключён», если он подключён");
                UiAssert.NotEqual(PhoneHomeState.NoDataSourceNotice, home.Notice, "причины разные");
            });

            suite.Add("Телефон №045: заглушки содержат все приложения референса и ни одного лишнего", () =>
            {
                IReadOnlyList<PhoneAppTile> placeholder = PhoneAppCatalog.BuildPlaceholder();
                UiAssert.Equal(PhoneAppIds.HomeOrder.Count, placeholder.Count, "количество плиток референса");

                foreach (string id in PhoneAppIds.HomeOrder)
                {
                    bool found = false;
                    for (int i = 0; i < placeholder.Count; i++)
                        if (placeholder[i].Id == id) found = true;
                    UiAssert.True(found, "в заглушках нет «" + id + "»");
                }

                for (int i = 0; i < placeholder.Count; i++)
                {
                    UiAssert.False(placeholder[i].Available, "заглушка «" + placeholder[i].Id + "» неактивна");
                    UiAssert.False(PhoneAppIds.IsForbiddenInPhone(placeholder[i].Id),
                        "приложение «" + placeholder[i].Id + "» недопустимо в телефоне");
                }
            });

            suite.Add("Телефон №045: плитки раскладываются в порядке рабочего стола и «map» отбрасывается", () =>
            {
                var unsorted = new List<PhoneAppTile>
                {
                    new PhoneAppTile(PhoneAppIds.Settings, "Настройки", 0, true, "settings"),
                    new PhoneAppTile("map", "Карта", 0, true, "map"),
                    new PhoneAppTile(PhoneAppIds.LegacyJobs, "Работа", 2, true, "jobs"),
                    new PhoneAppTile(PhoneAppIds.Calls, "Звонки", 0, true, "calls"),
                    new PhoneAppTile(PhoneAppIds.LegacyJobs, "Дубль", 0, true, "jobs"),
                };

                List<PhoneAppTile> sorted = PhoneAppIds.Sort(unsorted);

                UiAssert.Equal(3, sorted.Count, "map и дубль jobs отброшены");
                UiAssert.Equal(PhoneAppIds.Calls, sorted[0].Id, "первым идёт «Звонки» (порядок референса)");
                UiAssert.Equal(PhoneAppIds.Orders, PhoneAppIds.Normalize(sorted[1].Id), "jobs показывается как orders");
                UiAssert.Equal(PhoneAppIds.Settings, sorted[2].Id, "настройки замыкают список");
                for (int i = 0; i < sorted.Count; i++)
                    UiAssert.False(PhoneAppIds.IsForbiddenInPhone(sorted[i].Id), "запрещённое приложение не прошло");
            });
        }

        // ---------------------------------------------------------------- телефон: приоритет источников

        private static void AddPhoneSourcePriorityTests(UiTestSuite suite)
        {
            suite.Add("Телефон №045: поле phoneSystem важнее автоматически найденных мостов", () =>
            {
                UiAssert.Equal(PhoneSourceChoice.PhoneSystemField,
                    PhoneSourceResolver.Choose(true, true, true, true),
                    "явное назначение интегратора не должно перебиваться сценой");
            });

            suite.Add("Телефон №045: мост доставки важнее прочих мостов сцены (он объединяет источники)", () =>
            {
                UiAssert.Equal(PhoneSourceChoice.DeliveryBridge,
                    PhoneSourceResolver.Choose(false, true, true, true),
                    "иначе теряются либо заказы, либо сообщения");
                UiAssert.True(PhoneSourceResolver.NeedsExternalBridge(PhoneSourceChoice.DeliveryBridge),
                    "мосту доставки нужно передать внешний телефонный мост");
            });

            suite.Add("Телефон №045: PhoneFacadeUiBridge находится, даже если доставки в сцене нет", () =>
            {
                UiAssert.Equal(PhoneSourceChoice.ScenePhoneBridge,
                    PhoneSourceResolver.Choose(false, false, true, true),
                    "готовый мост PhoneFacadeUiBridge должен быть найден");
                UiAssert.False(PhoneSourceResolver.NeedsExternalBridge(PhoneSourceChoice.ScenePhoneBridge),
                    "мост сцены используется напрямую");
            });

            suite.Add("Телефон №045: UiSnapshotSource — последний вариант, «нет источника» — честный None", () =>
            {
                UiAssert.Equal(PhoneSourceChoice.SnapshotSource,
                    PhoneSourceResolver.Choose(false, false, false, true), "снимок — резервный вариант");
                UiAssert.Equal(PhoneSourceChoice.None,
                    PhoneSourceResolver.Choose(false, false, false, false), "нет источников — None");
                UiAssert.Equal("нет", PhoneSourceResolver.Describe(PhoneSourceChoice.None), "описание для отчёта");
            });
        }

        // ---------------------------------------------------------------- район

        private static void AddDistrictPlanTests(UiTestSuite suite)
        {
            suite.Add("Район №045: сцена без района (квартира) — это норма, а не дефект", () =>
            {
                DistrictPlanState state = DistrictPlanDiagnostics.Evaluate(false, false);

                UiAssert.Equal(DistrictPlanState.NoDistrictInScene, state, "нет источника района");
                UiAssert.False(DistrictPlanDiagnostics.IsProblem(state),
                    "в Apartment67 карта без района — ожидаемое поведение");
                UiAssert.Contains(DistrictPlanDiagnostics.Message(state, null), "квартир",
                    "сообщение должно объяснять, что это норма");
            });

            suite.Add("Район №045: источник есть, плана нет — ждём генерацию и сообщаем об этом", () =>
            {
                DistrictPlanState state = DistrictPlanDiagnostics.Evaluate(true, false);

                UiAssert.Equal(DistrictPlanState.AwaitingGeneration, state, "источник найден, план ещё нет");
                UiAssert.True(DistrictPlanDiagnostics.IsProblem(state), "это незавершённая настройка, а не норма");
                UiAssert.Contains(DistrictPlanDiagnostics.Message(state, "QuarterUnityAdapter"), "QuarterUnityAdapter",
                    "в сообщении должен быть источник");
                UiAssert.Contains(DistrictPlanDiagnostics.Message(state, "QuarterUnityAdapter"), "PlanChanged",
                    "должно быть понятно, чего ждём");
            });

            suite.Add("Район №045: план получен — «готово», без жалоб", () =>
            {
                DistrictPlanState state = DistrictPlanDiagnostics.Evaluate(true, true);

                UiAssert.Equal(DistrictPlanState.Ready, state, "план есть");
                UiAssert.False(DistrictPlanDiagnostics.IsProblem(state), "готовый район — не проблема");
                UiAssert.Contains(DistrictPlanDiagnostics.Message(state, "QuarterUnityAdapter"), "план района",
                    "сообщение для отчёта");
            });

            suite.Add("Район №045: три состояния дают три РАЗНЫХ сообщения", () =>
            {
                string none = DistrictPlanDiagnostics.Message(DistrictPlanDiagnostics.Evaluate(false, false), "X");
                string waiting = DistrictPlanDiagnostics.Message(DistrictPlanDiagnostics.Evaluate(true, false), "X");
                string ready = DistrictPlanDiagnostics.Message(DistrictPlanDiagnostics.Evaluate(true, true), "X");

                UiAssert.NotEqual(none, waiting, "«нет района» и «ждём генерацию» нельзя путать");
                UiAssert.NotEqual(waiting, ready, "«ждём» и «готово» нельзя путать");
                UiAssert.NotEqual(none, ready, "«нет района» и «готово» нельзя путать");
            });
        }
    }
}
