using AccountingSystem.Client.Formatting;
using FluentAssertions;

namespace AccountingSystem.Client.Tests;

public class PhilippineDateTimeDisplayTests
{
    [Fact]
    public void FormatDateTime_ShouldConvertUtcValuesToPhilippineTime()
    {
        var timestamp = new DateTime(2026, 3, 30, 4, 15, 0, DateTimeKind.Utc);

        PhilippineDateTimeDisplay.FormatDateTime(timestamp).Should().Be("Mar 30, 2026 12:15 PM");
        PhilippineDateTimeDisplay.FormatDate(timestamp).Should().Be("Mar 30, 2026");
        PhilippineDateTimeDisplay.FormatTime(timestamp).Should().Be("12:15 PM");
    }
}
