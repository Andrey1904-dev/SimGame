// Life From Zero — Economy module (ТЗ №021)
// Namespace: LifeFromZero.Economy.Core
//
// Идемпотентный контракт кошелька и переходник «старый адаптер → новый контракт».
//
// ЧТО РЕШАЕТСЯ.
//  1. Повторное выполнение финансовой операции. Старый IWalletAdapter не принимает
//     идентификатор: второй вызов TrySpend — это второе списание, и отличить
//     «повтор после сбоя» от «новая покупка» невозможно ни сервису, ни адаптеру.
//     IIdempotentWalletAdapter принимает operationId и обязан вернуть тот же исход,
//     что и в первый раз, не двигая деньги повторно.
//  2. Неоднозначный результат при исключении. Исключение списания больше не
//     трактуется как «денег не ушло»: сервис запрашивает журнал адаптера
//     (TryGetOperationState) и восстанавливает исход.
//  3. Потеря незавершённых операций. Журнал операций сервиса ссылается на operationId,
//     поэтому после загрузки снимка «висящее» списание можно либо доиграть (выдать товар),
//     либо вернуть деньги — но не «забыть».

using System;
using System.Collections.Generic;

namespace LifeFromZero.Economy.Core
{
    /// <summary>
    /// Контракт идемпотентного кошелька. Расширяет <see cref="IWalletAdapter"/>,
    /// поэтому существующие адаптеры продолжают работать через
    /// <see cref="LegacyWalletAdapter"/> и пользуются урезанными гарантиями.
    /// </summary>
    /// <remarks>
    /// Обязанности адаптера (без них сервис не может дать гарантии):
    /// <list type="number">
    /// <item>Журнал операций долговечен: переживает перезапуск игры и загрузку сохранения.
    ///       Хранение в памяти процесса допустимо только для «Режима A» (сессия без
    ///       сохранений), и это должно быть явно задокументировано у реализации.</item>
    /// <item><see cref="TrySpend(string, Money)"/> идемпотентен по operationId: повторный
    ///       вызов с тем же ID и той же суммой возвращает <c>true</c> и НЕ списывает
    ///       деньги второй раз. Тот же ID с другой суммой — <c>false</c> (конфликт).</item>
    /// <item><see cref="TryRefund(string, Money)"/> идемпотентен так же: повторный возврат
    ///       по тому же ID возвращает <c>true</c> и не начисляет деньги второй раз.</item>
    /// <item><see cref="TryGetOperationState"/> отвечает по журналу: это единственный способ
    ///       отличить «не списали» от «списали, но ответ потерялся».</item>
    /// <item>Исключения не считаются отказом: они означают «исход неизвестен», и сервис
    ///       всё равно спросит журнал. Адаптер должен стремиться не бросать исключений
    ///       в штатных ситуациях (нет денег, отказ лимита).</item>
    /// </list>
    /// </remarks>
    public interface IIdempotentWalletAdapter : IWalletAdapter
    {
        /// <summary>Журнал операций долговечен (переживает перезапуск игры).</summary>
        bool IsDurable { get; }

        /// <summary>
        /// Списывает сумму под идентификатором операции.
        /// </summary>
        /// <returns><c>false</c> — отказ; деньги не изменены (или уже были списаны ранее
        /// с другим результатом — проверяйте <see cref="TryGetOperationState"/>).</returns>
        bool TrySpend(string operationId, Money amount);

        /// <summary>Возвращает ранее списанную сумму под тем же идентификатором операции.</summary>
        bool TryRefund(string operationId, Money amount);

        /// <summary>
        /// Состояние операции по журналу адаптера.
        /// </summary>
        /// <param name="result">Исход операции.</param>
        /// <returns><c>false</c> — журнал недоступен, исход неизвестен.</returns>
        bool TryGetOperationState(string operationId, out WalletOperationState result);
    }

    /// <summary>
    /// Переходник: старый <see cref="IWalletAdapter"/> → <see cref="IIdempotentWalletAdapter"/>.
    /// </summary>
    /// <remarks>
    /// ВАЖНО И ЧЕСТНО. Этот класс НЕ добавляет долговечности и не делает адаптер
    /// идемпотентным на уровне кошелька: он помнит исходы операций в памяти процесса.
    /// Поэтому:
    /// <list type="bullet">
    /// <item><see cref="IsDurable"/> = <c>false</c>;</item>
    /// <item>повтор операции внутри одной сессии защищён (второго списания не будет);</item>
    /// <item>после перезапуска игры журнал пуст, и «висящую» операцию нельзя восстановить
    ///       по журналу кошелька — сервис сообщит
    ///       <see cref="EconomyResultCode.OutcomeUnknown"/> и не будет списывать повторно.</item>
    /// </list>
    /// Для «Режима B» (сохранения) нужен настоящий журнал в MoneyWallet, а не этот переходник.
    /// </remarks>
    public sealed class LegacyWalletAdapter : IIdempotentWalletAdapter
    {
        private const int MaxRememberedOperations = 512;

        private readonly IWalletAdapter _inner;
        private readonly Dictionary<string, WalletOperationState> _operations =
            new Dictionary<string, WalletOperationState>(StringComparer.Ordinal);

        private readonly List<string> _order = new List<string>();

        public LegacyWalletAdapter(IWalletAdapter inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        /// <summary>Всегда <c>false</c>: журнал живёт только в памяти процесса.</summary>
        public bool IsDurable => false;

        public Money Balance => _inner.Balance;

        /// <summary>
        /// Списание без идентификатора оставлено для совместимости (интерфейс
        /// <see cref="IWalletAdapter"/>) и НЕ защищено от повтора: используйте
        /// <see cref="TrySpend(string, Money)"/>.
        /// </summary>
        public bool TrySpend(Money amount) => _inner.TrySpend(amount);

        /// <summary>Возврат без идентификатора: только для совместимости.</summary>
        public bool TryRefund(Money amount) => _inner.TryRefund(amount);

        public bool TrySpend(string operationId, Money amount)
        {
            if (string.IsNullOrEmpty(operationId))
            {
                return false; // без идентификатора идемпотентность невозможна
            }

            WalletOperationState known;
            if (_operations.TryGetValue(operationId, out known))
            {
                // Повтор: возвращаем прежний исход, деньги не двигаем.
                if (known.Kind == WalletOperationKind.Spend && known.Amount == amount)
                {
                    return known.Status != WalletOperationStatus.Declined;
                }

                return false; // конфликт ID/суммы/вида операции
            }

            bool ok;
            bool completed = SafeCall.Try(() => _inner.TrySpend(amount), out ok);
            WalletOperationStatus status = completed && ok
                ? WalletOperationStatus.Succeeded
                : (completed ? WalletOperationStatus.Declined : WalletOperationStatus.Unknown);

            Remember(operationId, new WalletOperationState(WalletOperationKind.Spend, amount, status));
            return status == WalletOperationStatus.Succeeded;
        }

        public bool TryRefund(string operationId, Money amount)
        {
            if (string.IsNullOrEmpty(operationId))
            {
                return false;
            }

            WalletOperationState known;
            if (_operations.TryGetValue(operationId, out known))
            {
                if (known.Kind == WalletOperationKind.Refund && known.Amount == amount)
                {
                    return known.Status != WalletOperationStatus.Declined;
                }

                return false;
            }

            bool ok;
            bool completed = SafeCall.Try(() => _inner.TryRefund(amount), out ok);
            WalletOperationStatus status = completed && ok
                ? WalletOperationStatus.Succeeded
                : (completed ? WalletOperationStatus.Declined : WalletOperationStatus.Unknown);

            Remember(operationId, new WalletOperationState(WalletOperationKind.Refund, amount, status));
            return status == WalletOperationStatus.Succeeded;
        }

        public bool TryGetOperationState(string operationId, out WalletOperationState result)
        {
            if (!string.IsNullOrEmpty(operationId) && _operations.TryGetValue(operationId, out result))
            {
                return true;
            }

            result = WalletOperationState.Unknown;
            return false;
        }

        private void Remember(string operationId, WalletOperationState state)
        {
            // Ограниченная память: старые записи вытесняются. Для сессии этого достаточно,
            // для сохранений — нет (и это ровно то, о чём предупреждает IsDurable = false).
            if (_operations.Count >= MaxRememberedOperations && _order.Count > 0)
            {
                _operations.Remove(_order[0]);
                _order.RemoveAt(0);
            }

            _operations[operationId] = state;
            _order.Add(operationId);
        }
    }
}
