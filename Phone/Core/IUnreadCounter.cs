// Life From Zero — Phone core (ТЗ №014-Т.1)
// Namespace: LifeFromZero.Phone.Core

namespace LifeFromZero.Phone.Core
{
    /// <summary>
    /// Общий контракт для систем с непрочитанными элементами:
    /// приложения, переписки, уведомления.
    /// </summary>
    public interface IUnreadCounter
    {
        /// <summary>Количество непрочитанных элементов.</summary>
        int UnreadCount { get; }
    }
}
