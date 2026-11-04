// Life From Zero — Economy module (ТЗ №021)
// Namespace: LifeFromZero.Economy.Shop
//
// Снимок журнала операций магазина. Плоские массивы и примитивы — без словарей,
// интерфейсов и вложенных коллекций, чтобы снимок был совместим с Unity JsonUtility
// ([Serializable] + публичные поля). Сам модуль на диск не пишет: формат файла
// выбирает Codex (JsonUtility / System.Text.Json / бинарный).
//
// ЧТО ИМЕННО СОХРАНЯЕТСЯ И ПОЧЕМУ ЭТОГО ДОСТАТОЧНО.
//   * OperationId списания и возврата — выводятся из PurchaseId, поэтому повторная
//     компенсация после загрузки не может получить новый идентификатор.
//   * State — единственный источник правды о том, где остановилась операция.
//   * ProductId / Units / Amount — чтобы доиграть выдачу товара после загрузки
//     без обращения к каталогу (цены могли измениться, а деньги уже списаны).
//
// ЧЕГО НЕ ХВАТАЕТ ДЛЯ ПОЛНОЙ ГАРАНТИИ ПРИ СБОЕ (см. ECONOMY_INTEGRATION_QUICKSTART.md).
// Снимок магазина надо фиксировать ОДНОВРЕМЕННО с балансом кошелька и содержимым
// инвентаря. Пока такой согласованной фиксации нет, гарантии ограничены одной сессией.

using System;

namespace LifeFromZero.Economy.Shop
{
    /// <summary>Запись журнала в снимке.</summary>
    [Serializable]
    public sealed class ShopOperationSnapshot
    {
        public string PurchaseId;
        public string ProductId;

        public int RequestedQuantity;

        /// <summary>Сколько единиц предмета должно быть выдано (для доигрывания выдачи).</summary>
        public int Units;

        /// <summary>Стоимость покупки в рублях.</summary>
        public long AmountRubles;

        /// <summary><see cref="ShopOperationState"/> как int.</summary>
        public int State;

        /// <summary><see cref="LifeFromZero.Economy.Core.EconomyResultCode"/> как int (для Declined).</summary>
        public int DeclinedCode;
    }

    /// <summary>Полный снимок журнала операций магазина.</summary>
    [Serializable]
    public sealed class ShopSnapshot
    {
        /// <summary>Текущая версия схемы снимка.</summary>
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion = CurrentSchemaVersion;

        /// <summary>
        /// Операции в порядке создания. Завершённые записи (Granted/Refunded/Declined)
        /// тоже сохраняются: они нужны, чтобы повторный вызов с тем же PurchaseId
        /// вернул прежний исход, а не создал новую покупку.
        /// </summary>
        public ShopOperationSnapshot[] Operations = new ShopOperationSnapshot[0];
    }
}
