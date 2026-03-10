using System.Globalization;

namespace RVS.Domain.Shared;

public static class DateTimeUtils
{

    // TODO replace with NodaTime by Jon Skeet

    // http://stackoverflow.com/questions/7983441/unix-time-conversions-in-c-sharp
    private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static DateTime GetEmptyDateTime()
    {
        return DateTime.MinValue;
    }
    public static DateTime GetMaxDateTime()
    {
        return DateTime.MaxValue;
    }
    public static long GetEmptyDateTimeMillis()
    {
        return ConvertDateTimeToMillis(GetEmptyDateTime());
    }
    public static long GetMaxDateTimeMillis()
    {
        return ConvertDateTimeToMillis(GetMaxDateTime());
    }
    public static long GetMinDateMillis()
    {
        return -9223372036854775807L;
    }
    public static long GetMaxDateMillis()
    {
        return 9223372036854775807L;
    }
    public static long CurrentDateTimeMillis()
    {
        return (long)(DateTime.UtcNow - UnixEpoch).TotalMilliseconds;
    }

    public static long ConvertDateTimeToMillis(DateTime dt)
    {
        return (long)(dt.ToUniversalTime() - UnixEpoch).TotalMilliseconds;
    }
    public static long ConvertDateTimeOffsetToMillis(DateTimeOffset dateTimeOffset)
    {
        DateTime dt = ConvertDateTimeOffsetToDateTime(dateTimeOffset);
        return ConvertDateTimeToMillis(dt);
    }

    public static DateTime ConvertMillisToDateTime(long millis)
    {
        return UnixEpoch.AddMilliseconds(millis);
    }
    public static DateTimeOffset ConvertMillisToDateTimeOffset(long millis)
    {
        DateTime dt = DateTimeUtils.ConvertMillisToDateTime(millis);
        return new DateTimeOffset(dt);
    }
    public static DateTime ConvertDateTimeOffsetToDateTime(DateTimeOffset dateTimeOffset)
    {
        if (dateTimeOffset.Offset.Equals(TimeSpan.Zero))
        {
            return dateTimeOffset.UtcDateTime;
        }
        else if (dateTimeOffset.Offset.Equals(TimeZoneInfo.Local.GetUtcOffset(dateTimeOffset.DateTime)))
        {
            return DateTime.SpecifyKind(dateTimeOffset.DateTime, DateTimeKind.Local);
        }
        else
        {
            return dateTimeOffset.DateTime;
        }
    }

    /// <summary>
    ///  Return the Unix time in millis of previously ocurring DateTimeAgo increment passed in.  Time 
    ///  component returned is always 12:00:00
    /// </summary>
    /// <param name="ago"></param>
    /// <returns></returns>
    public static long GetDateAgoMillis(DateAgo ago)
    {
        // Get today's date, time 12:00:00
        DateTime today = DateTime.UtcNow.Date;

        // Get the date ago - initialize with UTC kind to avoid timezone ambiguity
        DateTime dateAgo = today;

        switch (ago)
        {
            case DateAgo.Today:
                dateAgo = today;
                break;
            case DateAgo.Yesterday:
                dateAgo = today.AddDays(-1);
                break;
            case DateAgo.Week:
                dateAgo = today.AddDays(-7);
                break;
            case DateAgo.Month:
                dateAgo = today.AddDays(-30);
                break;
            case DateAgo.Year:
                dateAgo = today.AddDays(-365);
                break;
        }

        long dateAgoMillis = ConvertDateTimeToMillis(dateAgo);

        return dateAgoMillis;
    }

    /// <summary>
    ///  Return the DateTimeOffset of previously ocurring DateTimeAgo increment passed in.   
    ///  Time component returned is always 12:00:00 (?)
    /// </summary>
    /// <param name="ago"></param>
    /// <returns></returns>
    public static DateTimeOffset GetPreviousDate(DateAgo ago)
    {
        // if anytime, return as far back as you can go
        if (ago == DateAgo.Anytime)
        {
            return DateTimeOffset.MinValue;
        }

        // today's date
        DateTimeOffset today = DateTimeOffset.UtcNow;

        // days to go back
        int daysAgo = (int)ago;

        // (today - days to go back) = target date
        DateTimeOffset dateAgo = today.AddDays(-daysAgo);

        return dateAgo;
    }

    /// <summary>
    /// Try to parse the passed in date string to a DateTime object.  Time component always 12:00:00
    /// </summary>
    /// <param name="dateString"></param>
    /// <returns></returns>
    public static DateTime ParseDateString(string dateString)
    {
        CultureInfo enUS = new CultureInfo("en-US");
        DateTime dateValue;
        DateTime.TryParseExact(dateString, "d", enUS, DateTimeStyles.None, out dateValue);

        return dateValue;
    }

    /// <summary>
    /// Convert the passed in date string to Unix time in millis.  Time component always 12:00:00
    /// Returns 0 if the conversion fails
    /// </summary>
    /// <param name="dateString"></param>
    /// <returns></returns>
    public static long ParseDateStringToMillis(string dateString)
    {
        DateTime dateValue = ParseDateString(dateString);
        // DateTime is a value type - check for default value instead of null
        if (dateValue != default)
        {
            return ConvertDateTimeToMillis(dateValue);
        }
        else
        {
            return 0;
        }

    }

    #region Second based methods COMMENTED
    /*
    public static long GetCurrentUnixTimestampSeconds()
    {
        return (long)(DateTime.UtcNow - UnixEpoch).TotalSeconds;
    }

    public static DateTime DateTimeFromUnixTimestampSeconds(long seconds)
    {
        return UnixEpoch.AddSeconds(seconds);
    }
     */
    #endregion

}

public enum DateAgo
{
    Today = 0,
    Yesterday = 1,
    Week = 7,
    Month = 30,
    Year = 365,
    Anytime = 10000
}

