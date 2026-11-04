// Life From Zero — Debts module (ТЗ №016)
// Namespace: LifeFromZero.Debts.Core
//
// Центральный сервис долгов. Зависит только от абстракций:
// IDebtClockAdapter (время), IDebtWalletAdapter (кошелёк), IDebtEventSink (события).
// Собственного кошелька, часов, телефона, UI, NPC и Singleton-ов нет.
//
// ФИНАНСОВЫЕ ГАРАНТИИ (подробно в INTEGRATION.md):
//  * Все суммы — long рублей; арифметика проверяет переполнение (DebtMath).
//  * Платёж идемпотентен по paymentId (история + журнал незавершённых операций).
//  * Журнал намерения пишется ДО списания; сбой после списания восстанавливается
//    через TryWasDebited / TryRefund адаптера. Пока журнал непуст, новые платежи
//    блокируются кодом PendingRecovery — деньги не теряются и не дублируются.
//  * Расчёт распределения платежа выполняется ДО списания, поэтому зачёт после
//    успешного списания — это только присваивания (не может «упасть на середине»).
//  * Штраф начисляется один раз за расчётный период; повторный UpdateTime(now)
//    идемпотентен. Одноразовые события защищены флагами.
//  * Снимок валидируется целиком до применения; повреждённый снимок не меняет состояние.

using System;
using System.Collections.Generic;
using LifeFromZero.Debts.Events;
using LifeFromZero.Debts.Payments;
using LifeFromZero.Debts.Penalties;
using LifeFromZero.Debts.Persistence;

namespace LifeFromZero.Debts.Core
{
    /// <summary>Сервис долгов, кредиторов, платежей, штрафов, стадий и событий.</summary>
    public sealed class DebtService : IDebtReadService
    {
        private readonly IDebtClockAdapter _clock;
        private readonly IDebtWalletAdapter _wallet;
        private readonly IDebtEventSink _sink;

        private readonly Dictionary<string, Creditor> _creditors =
            new Dictionary<string, Creditor>(StringComparer.Ordinal);

        private readonly Dictionary<string, Debt> _debts =
            new Dictionary<string, Debt>(StringComparer.Ordinal);

        private readonly List<string> _debtOrder = new List<string>();

        private readonly Dictionary<string, DebtPaymentRecord> _payments =
            new Dictionary<string, DebtPaymentRecord>(StringComparer.Ordinal);

        private readonly List<DebtPaymentRecord> _paymentOrder = new List<DebtPaymentRecord>();

        private readonly List<DebtPendingOperation> _pending = new List<DebtPendingOperation>();

        /// <summary>
        /// true, пока списание «в полёте» (между записью журнала и зачётом).
        /// Блокирует реентрантное восстановление текущей операции.
        /// </summary>
        private bool _inDebitWindow;

        private long _lastNow;

        public DebtService(IDebtClockAdapter clock, IDebtWalletAdapter wallet, IDebtEventSink sink)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _sink = sink; // null допустим: события просто некому доставлять
        }

        /// <summary>Число незавершённых операций в журнале восстановления.</summary>
        public int PendingOperationCount => _pending.Count;

        /// <summary>Последнее обработанное игровое время, минуты.</summary>
        public long LastProcessedMinutes => _lastNow;

        // ---------------------------------------------------------------------
        // Кредиторы
        // ---------------------------------------------------------------------

        public DebtResult RegisterCreditor(Creditor creditor)
        {
            if (creditor == null)
            {
                return DebtResult.Fail(DebtResultCode.InvalidArgument, null, "Кредитор не передан.");
            }

            if (_creditors.ContainsKey(creditor.Id))
            {
                return DebtResult.Fail(DebtResultCode.DuplicateCreditor, null, "Кредитор уже зарегистрирован: " + creditor.Id);
            }

            _creditors.Add(creditor.Id, creditor);
            return DebtResult.Ok(null);
        }

        public bool TryGetCreditor(string creditorId, out Creditor creditor)
        {
            creditor = null;
            if (string.IsNullOrEmpty(creditorId))
            {
                return false;
            }

            return _creditors.TryGetValue(creditorId, out creditor);
        }

        // ---------------------------------------------------------------------
        // Создание долга
        // ---------------------------------------------------------------------

        public DebtResult CreateDebt(DebtConfiguration config)
        {
            if (config == null)
            {
                return DebtResult.Fail(DebtResultCode.InvalidArgument, null, "Конфигурация не передана.");
            }

            if (string.IsNullOrWhiteSpace(config.DebtId))
            {
                return DebtResult.Fail(DebtResultCode.InvalidArgument, null, "Пустой ID долга.");
            }

            if (_debts.ContainsKey(config.DebtId))
            {
                return DebtResult.Fail(DebtResultCode.DuplicateDebt, config.DebtId, "Долг уже существует.");
            }

            if (string.IsNullOrWhiteSpace(config.CreditorId) || !_creditors.ContainsKey(config.CreditorId))
            {
                return DebtResult.Fail(DebtResultCode.CreditorNotFound, config.DebtId, "Кредитор не зарегистрирован: " + config.CreditorId);
            }

            if (config.PrincipalRubles <= 0L)
            {
                return DebtResult.Fail(DebtResultCode.InvalidArgument, config.DebtId, "Основная сумма должна быть положительной.");
            }

            if (config.InterestRateBasisPoints < 0)
            {
                return DebtResult.Fail(DebtResultCode.InvalidArgument, config.DebtId, "Отрицательная процентная ставка.");
            }

            if (config.DueAtMinutes < 0L || config.GracePeriodMinutes < 0L)
            {
                return DebtResult.Fail(DebtResultCode.InvalidArgument, config.DebtId, "Отрицательный срок или льготный период.");
            }

            // Разовая процентная надбавка к основному долгу.
            if (!DebtMath.TryPercent(config.PrincipalRubles, config.InterestRateBasisPoints, out long interest))
            {
                return DebtResult.Fail(DebtResultCode.Overflow, config.DebtId, "Переполнение при расчёте процента.");
            }

            if (!DebtMath.TryAdd(config.PrincipalRubles, interest, out long principalOriginal))
            {
                return DebtResult.Fail(DebtResultCode.Overflow, config.DebtId, "Переполнение основного долга с процентом.");
            }

            PenaltyPolicy penalties = config.Penalties ?? PenaltyPolicy.Disabled;

            // Инвариант: основной долг + максимум штрафов помещается в long.
            // Это гарантирует, что TotalRemaining и PaidTotal никогда не переполнятся.
            if (penalties.Enabled && !DebtMath.TryAdd(principalOriginal, penalties.MaxTotalRubles, out _))
            {
                return DebtResult.Fail(DebtResultCode.Overflow, config.DebtId, "Основной долг + максимум штрафов переполняет long.");
            }

            // DueAt + Grace тоже не должен переполняться (момент начала просрочки).
            if (!DebtMath.TryAdd(config.DueAtMinutes, config.GracePeriodMinutes, out _))
            {
                return DebtResult.Fail(DebtResultCode.Overflow, config.DebtId, "Срок + льготный период переполняет long.");
            }

            CollectionThresholds collection = config.Collection ?? CollectionThresholds.CreateDefault();

            long createdAt = ReadClockOrLast();
            var debt = new Debt(
                config.DebtId,
                config.CreditorId,
                config.Title,
                principalOriginal,
                config.DueAtMinutes,
                config.GracePeriodMinutes,
                createdAt,
                config.InterestRateBasisPoints,
                penalties,
                collection);

            _debts.Add(debt.Id, debt);
            _debtOrder.Add(debt.Id);

            Fire(new DebtEvent(
                DebtEventType.DebtCreated, debt.Id, debt.CreditorId, null,
                debt.TotalRemaining, createdAt, debt.Status, debt.Status, DebtResultCode.Success));

            return DebtResult.Ok(debt.Id);
        }

        // ---------------------------------------------------------------------
        // Платежи
        // ---------------------------------------------------------------------

        public DebtPaymentResult TryPay(DebtPaymentRequest request)
        {
            return TryPay(request.DebtId, request.AmountRubles, request.PaymentId);
        }

        public DebtPaymentResult TryPay(string debtId, long amountRubles, string paymentId)
        {
            // 0. Сначала пытаемся закрыть незавершённые операции прошлого вызова.
            ResolvePending();
            if (_pending.Count > 0)
            {
                return Reject(DebtResultCode.PendingRecovery, debtId, paymentId, 0L,
                    "Есть незавершённая операция; новые платежи заблокированы до восстановления.");
            }

            // 1. ID долга.
            if (string.IsNullOrWhiteSpace(debtId))
            {
                return Reject(DebtResultCode.InvalidArgument, debtId, paymentId, 0L, "Пустой ID долга.");
            }

            if (!_debts.TryGetValue(debtId, out Debt debt))
            {
                return Reject(DebtResultCode.DebtNotFound, debtId, paymentId, 0L, "Долг не найден.");
            }

            // 2. Уникальность paymentId (история + журнал).
            if (string.IsNullOrWhiteSpace(paymentId))
            {
                return Reject(DebtResultCode.InvalidArgument, debtId, paymentId, debt.TotalRemaining, "Пустой paymentId.");
            }

            if (_payments.ContainsKey(paymentId))
            {
                return Reject(DebtResultCode.DuplicatePayment, debtId, paymentId, debt.TotalRemaining,
                    "paymentId уже использован: " + paymentId);
            }

            // 3. Сумма.
            if (amountRubles <= 0L)
            {
                return Reject(DebtResultCode.InvalidArgument, debtId, paymentId, debt.TotalRemaining,
                    "Сумма платежа должна быть положительной.");
            }

            // 4. Состояние долга.
            if (debt.IsClosed)
            {
                return Reject(DebtResultCode.InvalidState, debtId, paymentId, 0L, "Долг уже закрыт.");
            }

            long remaining = debt.TotalRemaining;
            if (amountRubles > remaining)
            {
                return Reject(DebtResultCode.Overpayment, debtId, paymentId, remaining,
                    "Сумма превышает текущую задолженность.");
            }

            // 5. Предварительная проверка зачёта (до списания). Само распределение
            // пересчитывается в момент зачёта по АКТУАЛЬНОМУ состоянию долга: внешний
            // адаптер кошелька может реентрантно изменить долг (например, дёрнуть
            // UpdateTime), и запись заранее посчитанных значений потеряла бы начисления.
            if (!CanApplyPayment(debt, amountRubles))
            {
                return Reject(DebtResultCode.Overflow, debtId, paymentId, remaining, "Переполнение при зачёте платежа.");
            }

            // 6. Баланс (одно чтение; адаптер перепроверит при списании).
            if (!TryReadBalance(out long balance))
            {
                return Reject(DebtResultCode.AdapterError, debtId, paymentId, remaining, "Баланс недоступен.");
            }

            if (balance < amountRubles)
            {
                return Reject(DebtResultCode.InsufficientFunds, debtId, paymentId, remaining, "Недостаточно средств.");
            }

            // 7. Журнал намерения ДО списания.
            long now = ReadClockOrLast();
            var operation = new DebtPendingOperation(paymentId, debtId, amountRubles, DebtOperationPhase.DebitPending, now);
            _pending.Add(operation);

            // Критическая секция: пока списание «в полёте», вложенный вызов
            // ResolvePending (например, адаптер дёрнул UpdateTime) не должен
            // разрешать эту операцию — кошелёк ещё не списал деньги, и восстановление
            // ошибочно отменило бы журнал, потеряв защиту от краха.
            _inDebitWindow = true;
            try
            {
                // 8. Списание.
                bool debitOk;
                bool debitThrew = false;
                try
                {
                    debitOk = _wallet.TryDebit(paymentId, amountRubles);
                }
                catch (Exception)
                {
                    debitOk = false;
                    debitThrew = true;
                }

                if (!debitOk)
                {
                    // Исход неизвестен (false или исключение): уточняем по журналу адаптера.
                    bool queried = TryQueryDebited(paymentId, out bool wasDebited);
                    if (!queried)
                    {
                        // Оставляем запись DebitPending: восстановим при следующем вызове.
                        return Reject(DebtResultCode.AdapterError, debtId, paymentId, remaining,
                            "Списание не подтверждено, исход неизвестен; операция ожидает восстановления.");
                    }

                    if (!wasDebited)
                    {
                        _pending.Remove(operation);
                        DebtResultCode code = debitThrew ? DebtResultCode.AdapterError : DebtResultCode.WalletFailure;
                        return Reject(code, debtId, paymentId, remaining, "Кошелёк не списал средства.");
                    }

                    // Адаптер сообщил об ошибке, но деньги фактически списаны — зачитываем.
                }

                operation.Phase = DebtOperationPhase.DebitConfirmed;

                // 9. Зачёт по актуальному состоянию долга.
                return ApplyConfirmedPayment(operation, debt, now);
            }
            finally
            {
                _inDebitWindow = false;
            }
        }

        /// <summary>
        /// Можно ли зачесть сумму в текущее состояние долга без переполнения.
        /// Порядок погашения: сначала штрафы, затем основной долг (игровое правило,
        /// не юридическая норма).
        /// </summary>
        private static bool CanApplyPayment(Debt debt, long amount)
        {
            long penaltyPart = amount < debt.PenaltiesRemaining ? amount : debt.PenaltiesRemaining;
            long principalPart = amount - penaltyPart;

            return DebtMath.TrySubtract(debt.PenaltiesRemaining, penaltyPart, out _)
                && DebtMath.TrySubtract(debt.PrincipalRemaining, principalPart, out _)
                && DebtMath.TryAdd(debt.PaidTotal, amount, out _);
        }

        /// <summary>
        /// Зачёт подтверждённого списания. Единый путь для TryPay и восстановления.
        /// Распределение считается по АКТУАЛЬНОМУ состоянию (не по значениям, снятым
        /// до вызова адаптера). Идемпотентен по paymentId.
        /// Журнал освобождается ДО рассылки событий: иначе обработчик, вызвавший
        /// CreateSnapshot(), получил бы снимок, где paymentId есть и в истории,
        /// и в журнале — такой снимок валидатор отклоняет.
        /// </summary>
        private DebtPaymentResult ApplyConfirmedPayment(DebtPendingOperation operation, Debt debt, long now)
        {
            // Защита от двойного зачёта (повторное восстановление, реентрантность).
            if (_payments.TryGetValue(operation.PaymentId, out DebtPaymentRecord existing))
            {
                _pending.Remove(operation);
                return new DebtPaymentResult(
                    DebtResultCode.Success, debt.Id, operation.PaymentId,
                    existing.AmountRubles, existing.PenaltiesPartRubles, existing.PrincipalPartRubles,
                    debt.TotalRemaining, null);
            }

            long amount = operation.AmountRubles;
            long penaltyPart = amount < debt.PenaltiesRemaining ? amount : debt.PenaltiesRemaining;
            long principalPart = amount - penaltyPart;

            if (!DebtMath.TrySubtract(debt.PenaltiesRemaining, penaltyPart, out long newPenalties)
                || !DebtMath.TrySubtract(debt.PrincipalRemaining, principalPart, out long newPrincipal)
                || !DebtMath.TryAdd(debt.PaidTotal, amount, out long newPaidTotal))
            {
                // Деньги списаны, но зачесть их нельзя (долг изменился/закрыт) —
                // компенсируем, чтобы не потерять деньги игрока.
                operation.Phase = DebtOperationPhase.RefundPending;
                TryResolveRefund(operation);
                return Reject(DebtResultCode.AdapterError, debt.Id, operation.PaymentId, debt.TotalRemaining,
                    "Зачёт невозможен; выполняется компенсация.");
            }

            debt.PenaltiesRemaining = newPenalties;
            debt.PrincipalRemaining = newPrincipal;
            debt.PaidTotal = newPaidTotal;

            var record = new DebtPaymentRecord(operation.PaymentId, debt.Id, amount, penaltyPart, principalPart, now);
            _payments.Add(operation.PaymentId, record);
            _paymentOrder.Add(record);

            DebtStatus previousStatus = debt.Status;
            bool closedNow = false;
            if (debt.TotalRemaining == 0L && !debt.IsClosed)
            {
                debt.Status = DebtStatus.Closed;
                debt.ClosedAtMinutes = now;
                closedNow = true;
            }

            // Состояние полностью зафиксировано — освобождаем журнал ДО событий.
            _pending.Remove(operation);

            if (closedNow)
            {
                Fire(new DebtEvent(
                    DebtEventType.CollectionStageChanged, debt.Id, debt.CreditorId, null,
                    0L, now, DebtStatus.Closed, previousStatus, DebtResultCode.Success));

                Fire(new DebtEvent(
                    DebtEventType.DebtClosed, debt.Id, debt.CreditorId, null,
                    0L, now, DebtStatus.Closed, DebtStatus.Closed, DebtResultCode.Success));
            }

            Fire(new DebtEvent(
                DebtEventType.PaymentCompleted, debt.Id, debt.CreditorId, operation.PaymentId,
                amount, now, debt.Status, debt.Status, DebtResultCode.Success));

            return new DebtPaymentResult(
                DebtResultCode.Success, debt.Id, operation.PaymentId,
                amount, penaltyPart, principalPart, debt.TotalRemaining, null);
        }

        /// <summary>Повторяет восстановление незавершённых операций. true — журнал пуст.</summary>
        public bool RetryPendingRecovery()
        {
            ResolvePending();
            return _pending.Count == 0;
        }

        /// <summary>
        /// Восстановление незавершённых операций:
        ///  DebitPending   — уточняем исход списания у адаптера;
        ///  DebitConfirmed — зачитываем платёж в долг (без повторного списания);
        ///  RefundPending  — возвращаем деньги (компенсация).
        /// </summary>
        private void ResolvePending()
        {
            // _inDebitWindow: операция ещё выполняется, её исход определит сам TryPay.
            if (_pending.Count == 0 || _inDebitWindow)
            {
                return;
            }

            DebtPendingOperation[] snapshot = _pending.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                DebtPendingOperation operation = snapshot[i];

                if (!_debts.TryGetValue(operation.DebtId, out Debt debt))
                {
                    // Долг исчез (не должно случаться при валидном снимке) — компенсируем.
                    operation.Phase = DebtOperationPhase.RefundPending;
                    TryResolveRefund(operation);
                    continue;
                }

                switch (operation.Phase)
                {
                    case DebtOperationPhase.DebitPending:
                        if (!TryQueryDebited(operation.PaymentId, out bool debited))
                        {
                            continue; // журнал адаптера недоступен — ждём
                        }

                        if (debited)
                        {
                            operation.Phase = DebtOperationPhase.DebitConfirmed;
                            ApplyRestoredConfirmed(operation, debt);
                        }
                        else
                        {
                            // Списания не было — операция отменена, деньги не тронуты.
                            _pending.Remove(operation);
                        }

                        break;

                    case DebtOperationPhase.DebitConfirmed:
                        ApplyRestoredConfirmed(operation, debt);
                        break;

                    case DebtOperationPhase.RefundPending:
                        TryResolveRefund(operation);
                        break;
                }
            }
        }

        /// <summary>
        /// Зачёт подтверждённого платежа при восстановлении. Использует тот же путь,
        /// что и обычный платёж: распределение по актуальному состоянию, компенсация
        /// при невозможности зачёта, освобождение журнала до рассылки событий.
        /// </summary>
        private void ApplyRestoredConfirmed(DebtPendingOperation operation, Debt debt)
        {
            ApplyConfirmedPayment(operation, debt, ReadClockOrLast());
        }

        private void TryResolveRefund(DebtPendingOperation operation)
        {
            bool refunded;
            try
            {
                refunded = _wallet.TryRefund(operation.PaymentId, operation.AmountRubles);
            }
            catch (Exception)
            {
                refunded = false;
            }

            if (refunded)
            {
                _pending.Remove(operation);
            }
            // Иначе запись остаётся RefundPending и будет повторена позже.
        }

        // ---------------------------------------------------------------------
        // Время, штрафы, стадии
        // ---------------------------------------------------------------------

        /// <summary>
        /// Обрабатывает игровое время: одноразовые события срока/просрочки, штрафы
        /// (один раз за период), смену стадий взыскания. Идемпотентен при повторном
        /// вызове с тем же моментом времени.
        /// </summary>
        public DebtResult UpdateTime()
        {
            // Незавершённые платежи восстанавливаем по возможности, но время не блокируем.
            ResolvePending();

            if (!TryReadClock(out long now))
            {
                return DebtResult.Fail(DebtResultCode.AdapterError, null, "Игровое время недоступно.");
            }

            if (now < _lastNow)
            {
                return DebtResult.Fail(DebtResultCode.AdapterError, null, "Игровое время уменьшилось (нарушение монотонности).");
            }

            _lastNow = now;

            for (int i = 0; i < _debtOrder.Count; i++)
            {
                Debt debt = _debts[_debtOrder[i]];
                if (debt.IsClosed)
                {
                    continue;
                }

                // Одноразовое событие наступления срока.
                if (!debt.DueEventFired && now >= debt.DueAtMinutes)
                {
                    debt.DueEventFired = true;
                    Fire(new DebtEvent(
                        DebtEventType.PaymentDue, debt.Id, debt.CreditorId, null,
                        debt.TotalRemaining, now, debt.Status, debt.Status, DebtResultCode.Success));
                }

                long overdueMinutes = now > debt.OverdueStartMinutes ? now - debt.OverdueStartMinutes : 0L;
                if (overdueMinutes <= 0L)
                {
                    continue;
                }

                // Одноразовое событие начала просрочки.
                if (!debt.OverdueEventFired)
                {
                    debt.OverdueEventFired = true;
                    Fire(new DebtEvent(
                        DebtEventType.DebtOverdue, debt.Id, debt.CreditorId, null,
                        debt.TotalRemaining, now, debt.Status, debt.Status, DebtResultCode.Success));
                }

                // Штрафы: один раз за расчётный период.
                if (debt.Penalties.Enabled)
                {
                    if (PenaltyCalculator.TryAccrue(
                            debt.Penalties,
                            debt.PrincipalRemaining,
                            overdueMinutes,
                            debt.LastPenaltyPeriodIndex,
                            debt.PenaltiesAccrued,
                            out long penaltyAmount,
                            out long newPeriod))
                    {
                        if (penaltyAmount > 0L)
                        {
                            debt.PenaltiesRemaining += penaltyAmount;
                            debt.PenaltiesAccrued += penaltyAmount;

                            Fire(new DebtEvent(
                                DebtEventType.PenaltyApplied, debt.Id, debt.CreditorId, null,
                                penaltyAmount, now, debt.Status, debt.Status, DebtResultCode.Success));
                        }

                        debt.LastPenaltyPeriodIndex = newPeriod;
                    }
                    // При переполнении (false) начисление пропускается, индекс не меняется —
                    // состояние остаётся корректным, попытка повторится на следующем обновлении.
                }

                // Стадия взыскания по длительности просрочки.
                DebtStatus stage = ComputeStage(debt, overdueMinutes);
                if (stage > debt.Status)
                {
                    DebtStatus previous = debt.Status;
                    debt.Status = stage;
                    Fire(new DebtEvent(
                        DebtEventType.CollectionStageChanged, debt.Id, debt.CreditorId, null,
                        0L, now, stage, previous, DebtResultCode.Success));
                }
            }

            return DebtResult.Ok(null);
        }

        private static DebtStatus ComputeStage(Debt debt, long overdueMinutes)
        {
            CollectionThresholds thresholds = debt.Collection;
            if (overdueMinutes >= thresholds.LegalActionAfterMinutes)
            {
                return DebtStatus.LegalAction;
            }

            if (overdueMinutes >= thresholds.CollectionAfterMinutes)
            {
                return DebtStatus.Collection;
            }

            if (overdueMinutes >= thresholds.ReminderAfterMinutes)
            {
                return DebtStatus.Reminder;
            }

            return DebtStatus.Overdue;
        }

        // ---------------------------------------------------------------------
        // Чтение (контракт для телефона)
        // ---------------------------------------------------------------------

        public IReadOnlyList<DebtView> GetDebts()
        {
            var list = new List<DebtView>(_debtOrder.Count);
            for (int i = 0; i < _debtOrder.Count; i++)
            {
                list.Add(ToView(_debts[_debtOrder[i]]));
            }

            return list.AsReadOnly();
        }

        public bool TryGetDebt(string debtId, out DebtView debt)
        {
            debt = null;
            if (string.IsNullOrEmpty(debtId) || !_debts.TryGetValue(debtId, out Debt inner))
            {
                return false;
            }

            debt = ToView(inner);
            return true;
        }

        public IReadOnlyList<DebtPaymentRecord> GetPaymentHistory(string debtId)
        {
            var list = new List<DebtPaymentRecord>();
            if (string.IsNullOrEmpty(debtId))
            {
                return list.AsReadOnly();
            }

            for (int i = 0; i < _paymentOrder.Count; i++)
            {
                if (string.Equals(_paymentOrder[i].DebtId, debtId, StringComparison.Ordinal))
                {
                    list.Add(_paymentOrder[i]);
                }
            }

            return list.AsReadOnly();
        }

        public bool TryGetTotalRemainingRubles(out long rubles)
        {
            rubles = 0L;
            for (int i = 0; i < _debtOrder.Count; i++)
            {
                Debt debt = _debts[_debtOrder[i]];
                if (debt.IsClosed)
                {
                    continue;
                }

                if (!DebtMath.TryAdd(rubles, debt.TotalRemaining, out rubles))
                {
                    rubles = 0L;
                    return false;
                }
            }

            return true;
        }

        private DebtView ToView(Debt debt)
        {
            string creditorName = _creditors.TryGetValue(debt.CreditorId, out Creditor creditor)
                ? creditor.Name
                : string.Empty;

            return new DebtView(
                debt.Id,
                debt.CreditorId,
                creditorName,
                debt.Title,
                debt.PrincipalOriginal,
                debt.PrincipalRemaining,
                debt.PenaltiesRemaining,
                debt.PenaltiesAccrued,
                debt.TotalRemaining,
                debt.PaidTotal,
                debt.DueAtMinutes,
                debt.GracePeriodMinutes,
                debt.CreatedAtMinutes,
                debt.ClosedAtMinutes,
                debt.Status,
                debt.InterestRateBasisPoints,
                debt.Penalties.Enabled);
        }

        // ---------------------------------------------------------------------
        // Сохранение / восстановление
        // ---------------------------------------------------------------------

        public DebtSnapshot CreateSnapshot()
        {
            var creditors = new CreditorSnapshot[_creditors.Count];
            int ci = 0;
            foreach (KeyValuePair<string, Creditor> pair in _creditors)
            {
                creditors[ci++] = new CreditorSnapshot { Id = pair.Value.Id, Name = pair.Value.Name };
            }

            var debts = new DebtEntrySnapshot[_debtOrder.Count];
            for (int i = 0; i < _debtOrder.Count; i++)
            {
                debts[i] = ToEntrySnapshot(_debts[_debtOrder[i]]);
            }

            var payments = new PaymentRecordSnapshot[_paymentOrder.Count];
            for (int i = 0; i < _paymentOrder.Count; i++)
            {
                DebtPaymentRecord record = _paymentOrder[i];
                payments[i] = new PaymentRecordSnapshot
                {
                    PaymentId = record.PaymentId,
                    DebtId = record.DebtId,
                    AmountRubles = record.AmountRubles,
                    PenaltiesPartRubles = record.PenaltiesPartRubles,
                    PrincipalPartRubles = record.PrincipalPartRubles,
                    AtMinutes = record.AtMinutes
                };
            }

            var pending = new PendingOperationSnapshot[_pending.Count];
            for (int i = 0; i < _pending.Count; i++)
            {
                DebtPendingOperation operation = _pending[i];
                pending[i] = new PendingOperationSnapshot
                {
                    PaymentId = operation.PaymentId,
                    DebtId = operation.DebtId,
                    AmountRubles = operation.AmountRubles,
                    Phase = (int)operation.Phase,
                    CreatedAtMinutes = operation.CreatedAtMinutes
                };
            }

            return new DebtSnapshot
            {
                SchemaVersion = DebtSnapshot.CurrentSchemaVersion,
                LastNowMinutes = _lastNow,
                Creditors = creditors,
                Debts = debts,
                Payments = payments,
                PendingOperations = pending
            };
        }

        public DebtResult RestoreSnapshot(DebtSnapshot snapshot)
        {
            // Пока списание «в полёте», зачёт ещё не выполнен, и замена словаря долгов
            // оставила бы платёж применённым к объекту Debt, которого больше нет в сервисе:
            // деньги списаны, остаток долга не уменьшен, история платежей есть —
            // то есть несогласованный снимок при следующем сохранении.
            if (_inDebitWindow)
            {
                return DebtResult.Fail(
                    DebtResultCode.InvalidState, null,
                    "Восстановление снимка во время незавершённого списания запрещено.");
            }

            if (!DebtSnapshotValidator.Validate(snapshot, out string error))
            {
                return DebtResult.Fail(DebtResultCode.InvalidSnapshot, null, error);
            }

            // Строим новое состояние целиком в локальных структурах и только затем
            // заменяем рабочее — повреждённый снимок не оставляет частичных изменений.
            var creditors = new Dictionary<string, Creditor>(StringComparer.Ordinal);
            for (int i = 0; i < snapshot.Creditors.Length; i++)
            {
                CreditorSnapshot cs = snapshot.Creditors[i];
                creditors.Add(cs.Id, new Creditor(cs.Id, cs.Name));
            }

            var debts = new Dictionary<string, Debt>(StringComparer.Ordinal);
            var debtOrder = new List<string>(snapshot.Debts.Length);
            for (int i = 0; i < snapshot.Debts.Length; i++)
            {
                DebtEntrySnapshot ds = snapshot.Debts[i];
                PenaltyPolicy policy = ds.PenaltyEnabled
                    ? new PenaltyPolicy(true, ds.PenaltyPeriodMinutes, ds.PenaltyFixedRubles, ds.PenaltyRateBasisPoints, ds.PenaltyMaxTotalRubles)
                    : PenaltyPolicy.Disabled;
                var thresholds = new CollectionThresholds(
                    ds.ReminderAfterMinutes, ds.CollectionAfterMinutes, ds.LegalActionAfterMinutes);

                var debt = new Debt(
                    ds.Id, ds.CreditorId, ds.Title, ds.PrincipalOriginal,
                    ds.DueAtMinutes, ds.GracePeriodMinutes, ds.CreatedAtMinutes,
                    ds.InterestRateBasisPoints, policy, thresholds)
                {
                    PrincipalRemaining = ds.PrincipalRemaining,
                    PenaltiesAccrued = ds.PenaltiesAccrued,
                    PenaltiesRemaining = ds.PenaltiesRemaining,
                    PaidTotal = ds.PaidTotal,
                    ClosedAtMinutes = ds.ClosedAtMinutes,
                    Status = (DebtStatus)ds.Status,
                    LastPenaltyPeriodIndex = ds.LastPenaltyPeriodIndex,
                    DueEventFired = ds.DueEventFired,
                    OverdueEventFired = ds.OverdueEventFired
                };

                debts.Add(debt.Id, debt);
                debtOrder.Add(debt.Id);
            }

            var payments = new Dictionary<string, DebtPaymentRecord>(StringComparer.Ordinal);
            var paymentOrder = new List<DebtPaymentRecord>(snapshot.Payments.Length);
            for (int i = 0; i < snapshot.Payments.Length; i++)
            {
                PaymentRecordSnapshot ps = snapshot.Payments[i];
                var record = new DebtPaymentRecord(
                    ps.PaymentId, ps.DebtId, ps.AmountRubles,
                    ps.PenaltiesPartRubles, ps.PrincipalPartRubles, ps.AtMinutes);
                payments.Add(record.PaymentId, record);
                paymentOrder.Add(record);
            }

            var pending = new List<DebtPendingOperation>(snapshot.PendingOperations.Length);
            for (int i = 0; i < snapshot.PendingOperations.Length; i++)
            {
                PendingOperationSnapshot po = snapshot.PendingOperations[i];
                pending.Add(new DebtPendingOperation(
                    po.PaymentId, po.DebtId, po.AmountRubles,
                    (DebtOperationPhase)po.Phase, po.CreatedAtMinutes));
            }

            // Атомарная замена рабочего состояния.
            _creditors.Clear();
            foreach (KeyValuePair<string, Creditor> pair in creditors)
            {
                _creditors.Add(pair.Key, pair.Value);
            }

            _debts.Clear();
            foreach (KeyValuePair<string, Debt> pair in debts)
            {
                _debts.Add(pair.Key, pair.Value);
            }

            _debtOrder.Clear();
            _debtOrder.AddRange(debtOrder);

            _payments.Clear();
            foreach (KeyValuePair<string, DebtPaymentRecord> pair in payments)
            {
                _payments.Add(pair.Key, pair.Value);
            }

            _paymentOrder.Clear();
            _paymentOrder.AddRange(paymentOrder);

            _pending.Clear();
            _pending.AddRange(pending);

            _lastNow = snapshot.LastNowMinutes;

            return DebtResult.Ok(null);
        }

        private static DebtEntrySnapshot ToEntrySnapshot(Debt debt)
        {
            return new DebtEntrySnapshot
            {
                Id = debt.Id,
                CreditorId = debt.CreditorId,
                Title = debt.Title,
                PrincipalOriginal = debt.PrincipalOriginal,
                PrincipalRemaining = debt.PrincipalRemaining,
                PenaltiesAccrued = debt.PenaltiesAccrued,
                PenaltiesRemaining = debt.PenaltiesRemaining,
                PaidTotal = debt.PaidTotal,
                DueAtMinutes = debt.DueAtMinutes,
                GracePeriodMinutes = debt.GracePeriodMinutes,
                CreatedAtMinutes = debt.CreatedAtMinutes,
                ClosedAtMinutes = debt.ClosedAtMinutes,
                Status = (int)debt.Status,
                InterestRateBasisPoints = debt.InterestRateBasisPoints,
                PenaltyEnabled = debt.Penalties.Enabled,
                PenaltyPeriodMinutes = debt.Penalties.PeriodMinutes,
                PenaltyFixedRubles = debt.Penalties.FixedRubles,
                PenaltyRateBasisPoints = debt.Penalties.RateBasisPoints,
                PenaltyMaxTotalRubles = debt.Penalties.MaxTotalRubles,
                LastPenaltyPeriodIndex = debt.LastPenaltyPeriodIndex,
                ReminderAfterMinutes = debt.Collection.ReminderAfterMinutes,
                CollectionAfterMinutes = debt.Collection.CollectionAfterMinutes,
                LegalActionAfterMinutes = debt.Collection.LegalActionAfterMinutes,
                DueEventFired = debt.DueEventFired,
                OverdueEventFired = debt.OverdueEventFired
            };
        }

        // ---------------------------------------------------------------------
        // Вспомогательное
        // ---------------------------------------------------------------------

        private DebtPaymentResult Reject(DebtResultCode code, string debtId, string paymentId, long remaining, string message)
        {
            string creditorId = null;
            DebtStatus status = DebtStatus.Current;
            if (!string.IsNullOrEmpty(debtId) && _debts.TryGetValue(debtId, out Debt debt))
            {
                creditorId = debt.CreditorId;
                status = debt.Status;
            }

            Fire(new DebtEvent(
                DebtEventType.PaymentRejected, debtId, creditorId, paymentId,
                0L, ReadClockOrLast(), status, status, code));

            return DebtPaymentResult.Fail(code, debtId, paymentId, remaining, message);
        }

        private void Fire(DebtEvent debtEvent)
        {
            if (_sink == null)
            {
                return;
            }

            try
            {
                _sink.Publish(debtEvent);
            }
            catch (Exception)
            {
                // Сбой обработчика не отменяет подтверждённую операцию и не портит долг.
            }
        }

        private bool TryReadClock(out long now)
        {
            now = 0L;
            try
            {
                now = _clock.NowMinutes;
            }
            catch (Exception)
            {
                return false;
            }

            return now >= 0L;
        }

        /// <summary>Часы для меток событий/создания; при сбое — последнее известное время.</summary>
        private long ReadClockOrLast()
        {
            return TryReadClock(out long now) ? now : _lastNow;
        }

        private bool TryReadBalance(out long balance)
        {
            balance = 0L;
            try
            {
                if (!_wallet.TryGetBalance(out balance))
                {
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }

            return balance >= 0L;
        }

        private bool TryQueryDebited(string paymentId, out bool debited)
        {
            debited = false;
            try
            {
                return _wallet.TryWasDebited(paymentId, out debited);
            }
            catch (Exception)
            {
                debited = false;
                return false;
            }
        }
    }
}
