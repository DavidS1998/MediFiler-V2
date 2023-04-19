using System;

namespace MediFiler_V2.Code;

public class QuickFolder
{
    public string Name { get; set; }
    public string Path { get; set; }
    public int TimesOpened { get; set; }
    public DateTime LastOpened { get; set; }

    public string RecentFormatting()
    {
        return Name + " - " + GetRelativeTime(LastOpened);
    }
    
    public string TimesOpenedFormatting()
    {
        return Name + " - " + TimesOpened + " times";
    }
    
    public string FavoriteFormatting()
    {
        return Name;
    }



    public static string GetRelativeTime(DateTime lastOpened)
    {
        var now = DateTime.Now;
        var timeSpan = now - lastOpened;

        if (timeSpan.TotalDays >= 365)
        {
            var years = (int) (timeSpan.TotalDays / 365);
            return $"{years} {(years == 1 ? "year" : "years")} ago";
        }

        if (timeSpan.TotalDays >= 30)
        {
            var months = (int) (timeSpan.TotalDays / 30);
            return $"{months} {(months == 1 ? "month" : "months")} ago";
        }

        if (timeSpan.TotalDays >= 1)
        {
            var days = (int) timeSpan.TotalDays;
            return $"{days} {(days == 1 ? "day" : "days")} ago";
        }

        if (timeSpan.TotalHours >= 1)
        {
            var hours = (int) timeSpan.TotalHours;
            return $"{hours} {(hours == 1 ? "hour" : "hours")} ago";
        }

        if (timeSpan.TotalMinutes >= 1)
        {
            var minutes = (int) timeSpan.TotalMinutes;
            return $"{minutes} {(minutes == 1 ? "minute" : "minutes")} ago";
        }

        if (timeSpan.TotalSeconds >= 1)
        {
            var seconds = (int) timeSpan.TotalSeconds;
            return $"{seconds} {(seconds == 1 ? "second" : "seconds")} ago";
        }

        return "Just now";
    }

}