// Life From Zero — Phone integrations (ТЗ №014-Т.1)
// Namespace: LifeFromZero.Phone.Integrations

using LifeFromZero.Phone.Core;

namespace LifeFromZero.Phone.Integrations
{
    /// <summary>Собирает сюжетные записи для приложения «Сообщения»/сюжетного экрана. Только чтение.</summary>
    public sealed class StorySummaryService
    {
        private readonly IStorySource _source;

        public StorySummaryService(IStorySource source)
        {
            _source = source;
        }

        /// <summary>Всегда успешно; недоступность источника помечается пустым массивом.</summary>
        public PhoneResult<StoryEntryDto[]> GetEntries()
        {
            StoryEntryDto[] entries = new StoryEntryDto[0];

            if (_source != null)
            {
                try
                {
                    if (!_source.TryGetEntries(out entries) || entries == null)
                    {
                        entries = new StoryEntryDto[0];
                    }
                }
                catch (System.Exception)
                {
                    entries = new StoryEntryDto[0];
                }
            }

            return PhoneResult<StoryEntryDto[]>.Ok(entries);
        }
    }
}
