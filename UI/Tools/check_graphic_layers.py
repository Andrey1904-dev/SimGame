#!/usr/bin/env python3
"""Статическая проверка контрактов UI, на которых держатся исправления ТЗ №045.

Зачем: две пойманные в Play Mode ошибки нельзя проверить тестами логики — они про
исходники Unity-слоя:

  1. Graphic без CanvasRenderer (MissingComponentException на «Grid»/«Route»).
     Слои карты обязаны создаваться ТОЛЬКО через UiBuilder.GraphicLayer, а сам
     MapCanvasGraphic обязан объявлять [RequireComponent(typeof(CanvasRenderer))].
  2. Перерисовка карты на каждое обращение к Refresh() (сотни перестроек меша).
     MapScreen обязан проходить через MapRedrawGate, а PhoneScreen — через
     PhoneHomeState (пустой рабочий стол телефона).

Это ПРОВЕРКА ИСХОДНИКОВ, а не Unity: она ловит повторное появление дефекта в коде,
но не доказывает, что сцена в Unity выглядит правильно.

Запуск из корня репозитория:  python3 UI/Tools/check_graphic_layers.py
"""
import os
import re
import sys

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))

failures = []


def read(path):
    full = os.path.join(REPO, path)
    if not os.path.isfile(full):
        failures.append("нет файла " + path)
        return ""
    with open(full, encoding="utf-8-sig") as handle:
        return handle.read()


def require(condition, message):
    if not condition:
        failures.append(message)


def runtime_files():
    directory = os.path.join(REPO, "UI", "Runtime")
    return [os.path.join("UI", "Runtime", name)
            for name in sorted(os.listdir(directory)) if name.endswith(".cs")]


def check_map_graphic():
    source = read("UI/Runtime/MapCanvasGraphic.cs")
    require("RequireComponent(typeof(CanvasRenderer))" in source,
            "MapCanvasGraphic обязан объявлять [RequireComponent(typeof(CanvasRenderer))]")
    require("RequireComponent(typeof(RectTransform))" in source,
            "MapCanvasGraphic обязан объявлять [RequireComponent(typeof(RectTransform))]")
    require("public bool HasCanvasRenderer" in source,
            "MapCanvasGraphic обязан уметь сообщать, есть ли CanvasRenderer")
    require(re.search(r"public void Redraw\(\)\s*\{[^}]*_canvasRenderer == null",
                      source, re.S) is not None,
            "MapCanvasGraphic.Redraw обязан НЕ трогать SetVerticesDirty без CanvasRenderer")


def check_map_screen():
    source = read("UI/Runtime/MapScreen.cs")
    require('UiBuilder.GraphicLayer<MapCanvasGraphic>("Grid"' in source,
            "слой «Grid» обязан создаваться через UiBuilder.GraphicLayer<MapCanvasGraphic>")
    require('UiBuilder.GraphicLayer<MapCanvasGraphic>("Route"' in source,
            "слой «Route» обязан создаваться через UiBuilder.GraphicLayer<MapCanvasGraphic>")
    require("_redrawGate.ShouldRedraw" in source,
            "MapScreen.Refresh обязан спрашивать MapRedrawGate перед перерисовкой")
    require("MapFrameKey" in source,
            "MapScreen обязан строить MapFrameKey для гейта перерисовки")


def check_no_raw_graphic_add():
    pattern = re.compile(r"AddComponent<\s*(MapCanvasGraphic)\s*>")
    for path in runtime_files():
        if os.path.basename(path) in ("UiBuilder.cs", "MapCanvasGraphic.cs"):
            continue
        source = read(path)
        require(pattern.search(source) is None,
                path + ": графику нельзя добавлять через AddComponent напрямую — "
                       "используйте UiBuilder.GraphicLayer (нужен CanvasRenderer)")


def check_phone_screen():
    source = read("UI/Runtime/PhoneScreen.cs")
    require("PhoneHomeState.Resolve" in source,
            "PhoneScreen.RenderHome обязан решать содержимое через PhoneHomeState (UI/Core)")
    # Старый текст экрана: «Приложения не подключены. Регистрация: PhoneAppRegistry.CreateDefault()».
    # Он врал: плитки приходят из PhoneSnapshot.Apps, а не из реестра модуля Phone.
    require("Приложения не подключены" not in source,
            "экран телефона не должен показывать «Приложения не подключены» (текст ушёл в лог)")
    require("Регистрация: PhoneAppRegistry" not in source,
            "подсказка «Регистрация: PhoneAppRegistry.CreateDefault()» вводила в заблуждение")


def check_bootstrap_phone_wiring():
    source = read("UI/Runtime/UiBootstrap.cs")
    require("IPhoneDataSource" in source, "UiBootstrap обязан знать контракт IPhoneDataSource")
    require("PhoneSourceResolver.Choose" in source,
            "UiBootstrap обязан выбирать источник телефона через PhoneSourceResolver.Choose")
    require("ResolveScenePhoneSource" in source,
            "UiBootstrap обязан искать IPhoneDataSource в сцене (PhoneFacadeUiBridge)")
    require("DistrictPlanDiagnostics" in source,
            "UiBootstrap обязан различать «сцены без района» и «район ещё не сгенерирован»")


def check_editor_repair():
    source = read("UI/Editor/UiInstaller.cs")
    require("RepairGraphicRenderers" in source,
            "UiInstaller обязан уметь находить Graphic без CanvasRenderer")
    require("ReportMissingScripts" in source,
            "UiInstaller обязан сообщать о Missing Script в сцене")
    require("AddComponent(type)" in source,
            "починка Graphic без CanvasRenderer выполняется пересозданием компонента")


def main():
    check_map_graphic()
    check_map_screen()
    check_no_raw_graphic_add()
    check_phone_screen()
    check_bootstrap_phone_wiring()
    check_editor_repair()

    if failures:
        print("GRAPHIC-LAYERS: FAILED (%d)" % len(failures))
        for message in failures:
            print("  ! " + message)
        return 1

    print("GRAPHIC-LAYERS: OK (контракты CanvasRenderer / перерисовки / телефона на месте)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
