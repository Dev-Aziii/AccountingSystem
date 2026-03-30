using System.Globalization;

namespace AccountingSystem.Client.Formatting
{
    public static class PhilippineDateTimeDisplay
    {
        private static readonly TimeSpan PhilippineOffset = TimeSpan.FromHours(8);

        public static DateTime ToPhilippineTime(DateTime value)
        {
            var utcValue = value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            };

            return utcValue + PhilippineOffset;
        }

        public static string FormatDateTime(DateTime value) =>
            ToPhilippineTime(value).ToString("MMM dd, yyyy hh:mm tt", CultureInfo.InvariantCulture);

        public static string FormatDate(DateTime value) =>
            ToPhilippineTime(value).ToString("MMM dd, yyyy", CultureInfo.InvariantCulture);

        public static string FormatTime(DateTime value) =>
            ToPhilippineTime(value).ToString("hh:mm tt", CultureInfo.InvariantCulture);
    }
}
