# Unity EditMode: выполнение и диагностика

В этом окружении Unity, dotnet, mono, csc и mcs не найдены: компиляция и тесты не запускались. Четыре test asmdef находятся в Economy/Tests, Jobs/Tests, Phone/Tests, Debts/Tests; они Editor-only, `UNITY_INCLUDE_TESTS`, NUnit через `nunit.framework.dll`, ссылки на runtime и Unity test runners. Новые тесты — `Jobs/Tests/DeliveryIdempotencyTests.cs`, `Phone/Tests/PhoneSnapshotRegressionTests.cs`. Старые NUnit-обёртки запускают внутренние наборы, а не отдельные NUnit-кейсы. Установить/проверить совместимый Unity Test Framework в Package Manager; не менять версии без проверки проекта.

Проверить путь к редактору и что проект не открыт вторым процессом:
```sh
UNITY="/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity"
test -x "$UNITY" || { echo 'Укажите установленный Unity 6000.3.27f1'; exit 1; }
"$UNITY" -batchmode -nographics -projectPath "/Users/homepc/Projects/LifeFromZero" \
  -runTests -testPlatform EditMode -testResults /tmp/simgame-editmode-results.xml \
  -logFile /tmp/simgame-editmode.log
```
Проверить exit code, XML totals/failures и log; ноль обнаруженных тестов не успех. Ошибки до тестов: проверить точный путь редактора, совместимость пакета Test Framework, asmdef references, `UNITY_INCLUDE_TESTS`, layout и конфликты имён. Ошибки компиляции разбирать по первой ошибке в логе и расположению файлов, а не отключать тесты. Для отдельных повторов использовать Test Runner EditMode/фильтр Unity, сначала воспроизвести без фильтра. Не выдавать статическую проверку за запуск.
