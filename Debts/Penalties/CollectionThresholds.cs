// Life From Zero — Debts module (ТЗ №016)
// Namespace: LifeFromZero.Debts.Penalties

using System;

namespace LifeFromZero.Debts.Penalties
{
    /// <summary>
    /// Пороги стадий взыскания в минутах просрочки (после окончания льготного периода).
    /// Настраиваются для каждого долга. Значения по умолчанию — данные, а не сюжетный факт.
    /// </summary>
    public sealed class CollectionThresholds
    {
        /// <summary>Порог стадии Reminder (напоминания).</summary>
        public long ReminderAfterMinutes { get; }

        /// <summary>Порог стадии Collection (усиленное взыскание).</summary>
        public long CollectionAfterMinutes { get; }

        /// <summary>Порог стадии LegalAction (юридическая стадия).</summary>
        public long LegalActionAfterMinutes { get; }

        public CollectionThresholds(long reminderAfterMinutes, long collectionAfterMinutes, long legalActionAfterMinutes)
        {
            if (reminderAfterMinutes <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(reminderAfterMinutes), "Порог Reminder должен быть положительным.");
            }

            if (collectionAfterMinutes < reminderAfterMinutes)
            {
                throw new ArgumentOutOfRangeException(nameof(collectionAfterMinutes), "Порог Collection не может быть раньше Reminder.");
            }

            if (legalActionAfterMinutes < collectionAfterMinutes)
            {
                throw new ArgumentOutOfRangeException(nameof(legalActionAfterMinutes), "Порог LegalAction не может быть раньше Collection.");
            }

            ReminderAfterMinutes = reminderAfterMinutes;
            CollectionAfterMinutes = collectionAfterMinutes;
            LegalActionAfterMinutes = legalActionAfterMinutes;
        }

        /// <summary>Значения по умолчанию: 3 / 14 / 30 игровых суток просрочки.</summary>
        public static CollectionThresholds CreateDefault()
        {
            const long minutesPerDay = 1440L;
            return new CollectionThresholds(3L * minutesPerDay, 14L * minutesPerDay, 30L * minutesPerDay);
        }
    }
}
