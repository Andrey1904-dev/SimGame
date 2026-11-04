// Life From Zero — Debts module (ТЗ №016)
// Namespace: LifeFromZero.Debts.Core

using System;

namespace LifeFromZero.Debts.Core
{
    /// <summary>
    /// Кредитор: сторона, которой должен игрок. Имя и ID настраиваются данными,
    /// модуль не задаёт обязательных сюжетных фактов.
    /// </summary>
    public sealed class Creditor
    {
        public string Id { get; }

        public string Name { get; }

        public Creditor(string id, string name)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("ID кредитора не может быть пустым.", nameof(id));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Имя кредитора не может быть пустым.", nameof(name));
            }

            Id = id;
            Name = name;
        }
    }
}
