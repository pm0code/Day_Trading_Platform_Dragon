namespace TradingPlatform.Common.Extensions;

/// <summary>
/// Extension methods for DateTime and DateTimeOffset to support trading-specific operations.
/// Includes market hours, trading session management, and time zone handling.
/// </summary>
public static class DateTimeExtensions
{
    #region Market Hours and Sessions

    /// <summary>
    /// Checks if the given time falls within regular US market hours (9:30 AM - 4:00 PM ET).
    /// </summary>
    /// <param name="dateTime">DateTime to check</param>
    /// <param name="timeZone">Target timezone (defaults to Eastern Time)</param>
    /// <returns>True if within regular market hours</returns>
    public static bool IsMarketHours(this DateTime dateTime, TimeZoneInfo? timeZone = null)
    {
        var easternTimeZone = timeZone ?? GetEasternTimeZone();
        var easternTime = TimeZoneInfo.ConvertTime(dateTime, easternTimeZone);
        
        // Check if it's a weekday
        if (easternTime.DayOfWeek == DayOfWeek.Saturday || easternTime.DayOfWeek == DayOfWeek.Sunday)
            return false;

        // Check if time is between 9:30 AM and 4:00 PM ET
        var marketOpen = new TimeSpan(9, 30, 0);
        var marketClose = new TimeSpan(16, 0, 0);
        
        return easternTime.TimeOfDay >= marketOpen && easternTime.TimeOfDay <= marketClose;
    }

    /// <summary>
    /// Checks if the given time falls within pre-market hours (4:00 AM - 9:30 AM ET).
    /// </summary>
    /// <param name="dateTime">DateTime to check</param>
    /// <param name="timeZone">Target timezone (defaults to Eastern Time)</param>
    /// <returns>True if within pre-market hours</returns>
    public static bool IsPreMarketHours(this DateTime dateTime, TimeZoneInfo? timeZone = null)
    {
        var easternTimeZone = timeZone ?? GetEasternTimeZone();
        var easternTime = TimeZoneInfo.ConvertTime(dateTime, easternTimeZone);
        
        // Check if it's a weekday
        if (easternTime.DayOfWeek == DayOfWeek.Saturday || easternTime.DayOfWeek == DayOfWeek.Sunday)
            return false;

        // Check if time is between 4:00 AM and 9:30 AM ET
        var preMarketOpen = new TimeSpan(4, 0, 0);
        var preMarketClose = new TimeSpan(9, 30, 0);
        
        return easternTime.TimeOfDay >= preMarketOpen && easternTime.TimeOfDay < preMarketClose;
    }

    /// <summary>
    /// Checks if the given time falls within after-hours trading (4:00 PM - 8:00 PM ET).
    /// </summary>
    /// <param name="dateTime">DateTime to check</param>
    /// <param name="timeZone">Target timezone (defaults to Eastern Time)</param>
    /// <returns>True if within after-hours trading</returns>
    public static bool IsAfterHours(this DateTime dateTime, TimeZoneInfo? timeZone = null)
    {
        var easternTimeZone = timeZone ?? GetEasternTimeZone();
        var easternTime = TimeZoneInfo.ConvertTime(dateTime, easternTimeZone);
        
        // Check if it's a weekday
        if (easternTime.DayOfWeek == DayOfWeek.Saturday || easternTime.DayOfWeek == DayOfWeek.Sunday)
            return false;

        // Check if time is between 4:00 PM and 8:00 PM ET
        var afterHoursOpen = new TimeSpan(16, 0, 0);
        var afterHoursClose = new TimeSpan(20, 0, 0);
        
        return easternTime.TimeOfDay >= afterHoursOpen && easternTime.TimeOfDay <= afterHoursClose;
    }

    /// <summary>
    /// Checks if the given date is a trading day (weekday, excluding known holidays).
    /// </summary>
    /// <param name="date">Date to check</param>
    /// <returns>True if it's a trading day</returns>
    public static bool IsTradingDay(this DateTime date)
    {
        // Check if it's a weekend
        if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
            return false;

        // Check for common US market holidays
        return !IsMarketHoliday(date);
    }

    /// <summary>
    /// Gets the next trading day from the given date.
    /// </summary>
    /// <param name="date">Starting date</param>
    /// <returns>Next trading day</returns>
    public static DateTime GetNextTradingDay(this DateTime date)
    {
        var nextDay = date.AddDays(1);
        while (!nextDay.IsTradingDay())
        {
            nextDay = nextDay.AddDays(1);
        }
        return nextDay;
    }

    /// <summary>
    /// Gets the previous trading day from the given date.
    /// </summary>
    /// <param name="date">Starting date</param>
    /// <returns>Previous trading day</returns>
    public static DateTime GetPreviousTradingDay(this DateTime date)
    {
        var previousDay = date.AddDays(-1);
        while (!previousDay.IsTradingDay())
        {
            previousDay = previousDay.AddDays(-1);
        }
        return previousDay;
    }

    #endregion

    #region Market Session Management

    /// <summary>
    /// Gets the market open time for the given date in Eastern Time.
    /// </summary>
    /// <param name="date">Date to get market open for</param>
    /// <returns>Market open DateTime in Eastern Time</returns>
    public static DateTime GetMarketOpen(this DateTime date)
    {
        var easternTimeZone = GetEasternTimeZone();
        var marketOpenDate = date.Date.Add(new TimeSpan(9, 30, 0));
        return TimeZoneInfo.ConvertTime(marketOpenDate, easternTimeZone, TimeZoneInfo.Local);
    }

    /// <summary>
    /// Gets the market close time for the given date in Eastern Time.
    /// </summary>
    /// <param name="date">Date to get market close for</param>
    /// <returns>Market close DateTime in Eastern Time</returns>
    public static DateTime GetMarketClose(this DateTime date)
    {
        var easternTimeZone = GetEasternTimeZone();
        var marketCloseDate = date.Date.Add(new TimeSpan(16, 0, 0));
        return TimeZoneInfo.ConvertTime(marketCloseDate, easternTimeZone, TimeZoneInfo.Local);
    }

    /// <summary>
    /// Gets the time remaining until market open.
    /// </summary>
    /// <param name="currentTime">Current time</param>
    /// <returns>TimeSpan until market open, or TimeSpan.Zero if market is open</returns>
    public static TimeSpan TimeUntilMarketOpen(this DateTime currentTime)
    {
        if (currentTime.IsMarketHours())
            return TimeSpan.Zero;

        var today = currentTime.Date;
        var marketOpen = today.GetMarketOpen();

        // If current time is after today's market close, get tomorrow's open
        if (currentTime > today.GetMarketClose())
        {
            var nextTradingDay = today.GetNextTradingDay();
            marketOpen = nextTradingDay.GetMarketOpen();
        }
        // If current time is before today's market open and today is a trading day
        else if (currentTime < marketOpen && today.IsTradingDay())
        {
            // Use today's market open
        }
        else
        {
            // Get next trading day's open
            var nextTradingDay = today.GetNextTradingDay();
            marketOpen = nextTradingDay.GetMarketOpen();
        }

        return marketOpen > currentTime ? marketOpen - currentTime : TimeSpan.Zero;
    }

    /// <summary>
    /// Gets the time remaining until market close.
    /// </summary>
    /// <param name="currentTime">Current time</param>
    /// <returns>TimeSpan until market close, or TimeSpan.Zero if market is closed</returns>
    public static TimeSpan TimeUntilMarketClose(this DateTime currentTime)
    {
        if (!currentTime.IsMarketHours())
            return TimeSpan.Zero;

        var marketClose = currentTime.Date.GetMarketClose();
        return marketClose > currentTime ? marketClose - currentTime : TimeSpan.Zero;
    }

    #endregion

    #region Time Zone Utilities

    /// <summary>
    /// Converts DateTime to Eastern Time Zone.
    /// </summary>
    /// <param name="dateTime">DateTime to convert</param>
    /// <param name="sourceTimeZone">Source timezone (defaults to local)</param>
    /// <returns>DateTime in Eastern Time</returns>
    public static DateTime ToEasternTime(this DateTime dateTime, TimeZoneInfo? sourceTimeZone = null)
    {
        var source = sourceTimeZone ?? TimeZoneInfo.Local;
        var easternTimeZone = GetEasternTimeZone();
        return TimeZoneInfo.ConvertTime(dateTime, source, easternTimeZone);
    }

    /// <summary>
    /// Converts DateTime to UTC.
    /// </summary>
    /// <param name="dateTime">DateTime to convert</param>
    /// <param name="sourceTimeZone">Source timezone (defaults to local)</param>
    /// <returns>DateTime in UTC</returns>
    public static DateTime ToUtc(this DateTime dateTime, TimeZoneInfo? sourceTimeZone = null)
    {
        var source = sourceTimeZone ?? TimeZoneInfo.Local;
        return TimeZoneInfo.ConvertTimeToUtc(dateTime, source);
    }

    /// <summary>
    /// Gets Unix timestamp (seconds since epoch) for the DateTime.
    /// </summary>
    /// <param name="dateTime">DateTime to convert</param>
    /// <returns>Unix timestamp</returns>
    public static long ToUnixTimestamp(this DateTime dateTime)
    {
        var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return (long)(dateTime.ToUniversalTime() - epoch).TotalSeconds;
    }

    /// <summary>
    /// Gets Unix timestamp in milliseconds for the DateTime.
    /// </summary>
    /// <param name="dateTime">DateTime to convert</param>
    /// <returns>Unix timestamp in milliseconds</returns>
    public static long ToUnixTimestampMs(this DateTime dateTime)
    {
        var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return (long)(dateTime.ToUniversalTime() - epoch).TotalMilliseconds;
    }

    #endregion

    #region Trading Period Calculations

    /// <summary>
    /// Calculates the number of trading days between two dates.
    /// </summary>
    /// <param name="startDate">Start date</param>
    /// <param name="endDate">End date</param>
    /// <returns>Number of trading days</returns>
    public static int TradingDaysBetween(this DateTime startDate, DateTime endDate)
    {
        if (startDate > endDate)
            return -endDate.TradingDaysBetween(startDate);

        var count = 0;
        var current = startDate.Date;
        
        while (current <= endDate.Date)
        {
            if (current.IsTradingDay())
                count++;
            current = current.AddDays(1);
        }

        return count;
    }

    /// <summary>
    /// Gets all trading days between two dates.
    /// </summary>
    /// <param name="startDate">Start date</param>
    /// <param name="endDate">End date</param>
    /// <returns>Enumerable of trading days</returns>
    public static IEnumerable<DateTime> TradingDaysInRange(this DateTime startDate, DateTime endDate)
    {
        var current = startDate.Date;
        
        while (current <= endDate.Date)
        {
            if (current.IsTradingDay())
                yield return current;
            current = current.AddDays(1);
        }
    }

    /// <summary>
    /// Checks if the given time is within the last N minutes of the trading day.
    /// Useful for end-of-day position management.
    /// </summary>
    /// <param name="dateTime">DateTime to check</param>
    /// <param name="minutes">Number of minutes before close (default 30)</param>
    /// <returns>True if within the specified minutes of market close</returns>
    public static bool IsNearMarketClose(this DateTime dateTime, int minutes = 30)
    {
        if (!dateTime.IsMarketHours())
            return false;

        var marketClose = dateTime.Date.GetMarketClose();
        var threshold = marketClose.AddMinutes(-minutes);
        
        return dateTime >= threshold;
    }

    /// <summary>
    /// Checks if the given time is within the first N minutes of the trading day.
    /// Useful for market open strategies and volatility management.
    /// </summary>
    /// <param name="dateTime">DateTime to check</param>
    /// <param name="minutes">Number of minutes after open (default 30)</param>
    /// <returns>True if within the specified minutes of market open</returns>
    public static bool IsNearMarketOpen(this DateTime dateTime, int minutes = 30)
    {
        if (!dateTime.IsMarketHours())
            return false;

        var marketOpen = dateTime.Date.GetMarketOpen();
        var threshold = marketOpen.AddMinutes(minutes);
        
        return dateTime >= marketOpen && dateTime <= threshold;
    }

    #endregion

    #region Formatting and Display

    /// <summary>
    /// Formats DateTime for trading logs with timezone information.
    /// </summary>
    /// <param name="dateTime">DateTime to format</param>
    /// <param name="includeMilliseconds">Whether to include milliseconds</param>
    /// <returns>Formatted string for trading logs</returns>
    public static string ToTradingLogFormat(this DateTime dateTime, bool includeMilliseconds = true)
    {
        var format = includeMilliseconds ? "yyyy-MM-dd HH:mm:ss.fff" : "yyyy-MM-dd HH:mm:ss";
        return $"{dateTime.ToString(format)} {TimeZoneInfo.Local.StandardName}";
    }

    /// <summary>
    /// Formats DateTime for market data timestamps.
    /// </summary>
    /// <param name="dateTime">DateTime to format</param>
    /// <returns>Formatted string for market data</returns>
    public static string ToMarketDataFormat(this DateTime dateTime)
    {
        return dateTime.ToString("yyyy-MM-dd HH:mm:ss.ffffff");
    }

    /// <summary>
    /// Gets a human-readable description of when the market opens/closes relative to the given time.
    /// </summary>
    /// <param name="dateTime">Current time</param>
    /// <returns>Human-readable market status description</returns>
    public static string GetMarketStatusDescription(this DateTime dateTime)
    {
        if (dateTime.IsMarketHours())
        {
            var timeToClose = dateTime.TimeUntilMarketClose();
            return $"Market is open. Closes in {timeToClose:hh\\:mm\\:ss}";
        }

        var timeToOpen = dateTime.TimeUntilMarketOpen();
        return $"Market is closed. Opens in {timeToOpen.Days} days, {timeToOpen:hh\\:mm\\:ss}";
    }

    #endregion

    #region Private Helper Methods

    /// <summary>
    /// Gets the Eastern Time Zone (handles both EST and EDT).
    /// </summary>
    /// <returns>Eastern Time Zone info</returns>
    private static TimeZoneInfo GetEasternTimeZone()
    {
        // Try different identifiers for cross-platform compatibility
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        }
        catch
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("US/Eastern");
            }
            catch
            {
                return TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
            }
        }
    }

    /// <summary>
    /// Checks if the given date is a US market holiday.
    /// </summary>
    /// <param name="date">Date to check</param>
    /// <returns>True if it's a market holiday</returns>
    private static bool IsMarketHoliday(DateTime date)
    {
        var year = date.Year;
        
        // New Year's Day
        if (date.Month == 1 && date.Day == 1)
            return true;
        
        // Martin Luther King Jr. Day (3rd Monday in January)
        if (date.Month == 1 && date.DayOfWeek == DayOfWeek.Monday && date.Day >= 15 && date.Day <= 21)
            return true;
        
        // Presidents' Day (3rd Monday in February)
        if (date.Month == 2 && date.DayOfWeek == DayOfWeek.Monday && date.Day >= 15 && date.Day <= 21)
            return true;
        
        // Good Friday (Friday before Easter)
        var easter = GetEasterDate(year);
        if (date == easter.AddDays(-2))
            return true;
        
        // Memorial Day (last Monday in May)
        if (date.Month == 5 && date.DayOfWeek == DayOfWeek.Monday && date.Day >= 25)
            return true;
        
        // Juneteenth (June 19)
        if (date.Month == 6 && date.Day == 19)
            return true;
        
        // Independence Day (July 4)
        if (date.Month == 7 && date.Day == 4)
            return true;
        
        // Labor Day (1st Monday in September)
        if (date.Month == 9 && date.DayOfWeek == DayOfWeek.Monday && date.Day <= 7)
            return true;
        
        // Thanksgiving (4th Thursday in November)
        if (date.Month == 11 && date.DayOfWeek == DayOfWeek.Thursday && date.Day >= 22 && date.Day <= 28)
            return true;
        
        // Christmas Day (December 25)
        if (date.Month == 12 && date.Day == 25)
            return true;

        return false;
    }

    /// <summary>
    /// Calculates Easter date for a given year.
    /// </summary>
    /// <param name="year">Year to calculate Easter for</param>
    /// <returns>Easter date</returns>
    private static DateTime GetEasterDate(int year)
    {
        // Algorithm for calculating Easter (Gregorian calendar)
        var a = year % 19;
        var b = year / 100;
        var c = year % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var month = (h + l - 7 * m + 114) / 31;
        var day = ((h + l - 7 * m + 114) % 31) + 1;
        
        return new DateTime(year, month, day);
    }

    #endregion
}