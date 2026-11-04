// Life From Zero — Phone core (ТЗ №014-Т.1)
// Namespace: LifeFromZero.Phone.Core

namespace LifeFromZero.Phone.Core
{
    /// <summary>
    /// Коды исхода операций телефона. Все ожидаемые ошибки возвращаются кодом,
    /// а не исключением. Текстов уведомлений здесь нет — их подбирает UI.
    /// </summary>
    public enum PhoneResultCode
    {
        /// <summary>Операция выполнена.</summary>
        Success = 0,

        /// <summary>Некорректные входные данные (пустой ID, пустое имя, отрицательное значение и т. п.).</summary>
        InvalidArgument = 1,

        /// <summary>Объект с таким ID уже существует (приложение, контакт, уведомление).</summary>
        DuplicateId = 2,

        /// <summary>Объект не найден (приложение, контакт, сообщение, уведомление).</summary>
        NotFound = 3,

        /// <summary>Операция недопустима в текущем состоянии (например, попытка «прочитать» уже прочитанное).</summary>
        InvalidState = 4,

        /// <summary>Счётчик достиг предела, дальнейшее увеличение невозможно.</summary>
        CounterOverflow = 5,

        /// <summary>Внешний адаптер отказал или выбросил исключение.</summary>
        AdapterFailure = 6,

        /// <summary>Внутренняя ошибка модуля.</summary>
        InternalError = 7
    }

    /// <summary>
    /// Результат операции без полезной нагрузки.
    /// </summary>
    public readonly struct PhoneResult
    {
        /// <summary>Код исхода.</summary>
        public PhoneResultCode Code { get; }

        /// <summary>Успешна ли операция.</summary>
        public bool IsSuccess => Code == PhoneResultCode.Success;

        /// <summary>ID объекта, к которому относится операция (может быть null).</summary>
        public string TargetId { get; }

        public PhoneResult(PhoneResultCode code, string targetId)
        {
            Code = code;
            TargetId = targetId;
        }

        public static PhoneResult Ok(string targetId)
        {
            return new PhoneResult(PhoneResultCode.Success, targetId);
        }

        public static PhoneResult Fail(PhoneResultCode code, string targetId)
        {
            return new PhoneResult(code, targetId);
        }

        public override string ToString()
        {
            return "PhoneResult(" + Code + ", " + (TargetId ?? "<none>") + ")";
        }
    }

    /// <summary>
    /// Результат операции с полезной нагрузкой. Иммутабельная обёртка над значением
    /// и кодом исхода.
    /// </summary>
    public readonly struct PhoneResult<T>
    {
        /// <summary>Код исхода.</summary>
        public PhoneResultCode Code { get; }

        /// <summary>Значение результата. Осмысленно только при <see cref="IsSuccess"/>.</summary>
        public T Value { get; }

        /// <summary>Успешна ли операция.</summary>
        public bool IsSuccess => Code == PhoneResultCode.Success;

        public PhoneResult(PhoneResultCode code, T value)
        {
            Code = code;
            Value = value;
        }

        public static PhoneResult<T> Ok(T value)
        {
            return new PhoneResult<T>(PhoneResultCode.Success, value);
        }

        public static PhoneResult<T> Fail(PhoneResultCode code)
        {
            return new PhoneResult<T>(code, default(T));
        }

        public override string ToString()
        {
            return "PhoneResult<" + typeof(T).Name + ">(" + Code + ")";
        }
    }

    /// <summary>
    /// Ошибка интеграции: нарушен контракт кода (null-адаптер и т. п.).
    /// Это не бизнес-ошибка — бизнес-ошибки возвращаются кодами.
    /// </summary>
    public sealed class PhoneIntegrationException : System.Exception
    {
        public PhoneIntegrationException(string message) : base(message)
        {
        }

        public PhoneIntegrationException(string message, System.Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
