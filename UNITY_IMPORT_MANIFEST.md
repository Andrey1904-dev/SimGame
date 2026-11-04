# Манифест импорта

Копировать с сохранением структуры в `Assets/_Project/SimGame/`:

* `Economy/Core/**/*.cs`, `Economy/Shop/**/*.cs`, `Economy/Economy.asmdef`.
* `Jobs/Delivery/**/*.cs`, `Jobs/Delivery/Jobs.Delivery.asmdef`.
* `Phone/{Apps,Contacts,Core,Integrations,Messages,Notifications}/**/*.cs`, `Phone/Phone.asmdef`.
* `Debts/{Core,Events,Payments,Penalties,Persistence}/**/*.cs`, `Debts/Debts.asmdef`.
* `UnityIntegrationKit/PhoneSources.cs` **и** `UnityIntegrationKit/AdapterBridges.cs` в `Assets/_Project/SimGame/UnityIntegrationKit/` вне любых родительских asmdef: Assembly-CSharp автоматически ссылается на четыре runtime asmdef. Если в проекте есть родительский asmdef, проверить явные ссылки, не добавлять циклы.
* Для EditMode: `Economy/Tests/**/*.cs` и `Economy/Tests/Economy.Tests.asmdef`; аналогично `Jobs/Tests/`, `Phone/Tests/`, `Debts/Tests/` со своими asmdef. Сохранить иерархию; тесты Editor-only, `UNITY_INCLUDE_TESTS` и nunit.framework.dll; ни один тест не переносить в runtime.
* Ожидаемое число тестов после импорта ТЗ №021: **Economy 60**, **Delivery 55**, **Debts 96**, Phone — без изменений. Если в Test Runner число меньше — часть файлов не импортирована (обычно `*FinancialSafetyTests.cs`, `DeliveryFinancialSafetyTests.cs`, `DebtFinancialContractTests.cs`) или не попала в `*TestSuite.cs`.

Новые файлы ТЗ №021, которые обязаны попасть в импорт: `Economy/Core/{FinancialOperationId,IdempotentWalletAdapter,IdempotentInventoryAdapter,WalletOperationState}.cs`,
`Economy/Shop/{ShopOperation,ShopSnapshot,ShopSnapshotValidator}.cs`,
`Jobs/Delivery/PayoutOperation.cs`, `Debts/Payments/DebtPaymentId.cs`,
`UnityIntegrationKit/AdapterBridges.cs`, а также тестовые
`Economy/Tests/{ShopFinancialSafetyTests,Fakes/JournalAdapters}.cs`,
`Jobs/Tests/{DeliveryFinancialSafetyTests,Fakes/JournalAdapters}.cs`,
`Debts/Tests/DebtFinancialContractTests.cs` и три `*TestSuite.cs`.
`AdapterBridges.cs` использует `System`/коллекции модулей и НЕ требует UnityEngine.

Четыре runtime-сборки `LifeFromZero.Economy`, `LifeFromZero.Jobs.Delivery`, `LifeFromZero.Phone`, `LifeFromZero.Debts` не имеют межмодульных ссылок и `noEngineReferences=true`. Тестовые сборки ссылаются на соответствующий runtime и UnityEngine.TestRunner/UnityEditor.TestRunner. Нужен Unity Test Framework (NUnit); версии брать совместимые с проектом, не заменять URP. Namespace: `LifeFromZero.Economy.*`, `LifeFromZero.Jobs.Delivery`, `LifeFromZero.Phone.*`, `LifeFromZero.Debts.*`, `LifeFromZero.UnityIntegrationKit`. До импорта проверить совпадения типов/asmdef и уже существующие копии в проекте; не копировать поверх неизвестных изменений, не импортировать дубликаты.

Не импортировать README, отчёты, `*.md`, Git-метаданные, временные файлы как исходники. `Program.cs` и MiniTestRunner находятся только внутри Editor-тестовой asmdef; не переносить их в runtime. Документы хранить вне Assets либо отдельно как документацию. `.meta` отсутствуют: Unity создаст их; проверить GUID-конфликты и закоммитить полученные файлы в проекте.
