using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace MediFiler_V2.Code;

public class QuickFolder
{
    public string Name { get; set; }
    public string Path { get; set; }
    public int TimesOpened { get; set; }
    public DateTime LastOpened { get; set; }

    public QuickFolder(string name)
    {
        Name = name;
    }
    
    public string GetInitials()
    {
        var cleanedName = Regex.Replace(Name, @"[^a-zA-Z0-9]+", "");
        var matches = Regex.Matches(cleanedName, @"[A-Z0-9]");
        var initials = string.Concat(matches.Select(match => match.Value));

        // If there are one or no initials, use the first three letters of the cleaned name
        if (string.IsNullOrEmpty(initials) || initials.Length == 1)
        {
            initials = cleanedName.Substring(0, Math.Min(cleanedName.Length, 3));
        }

        // If there are only two initials, include the first letter after the last initial
        if (initials.Length == 2)
        {
            // Find the index of the last initial
            var lastInitialIndex = cleanedName.LastIndexOf(initials[1]);

            // If the last initial is not the last letter of the name, add the next letter after it
            if (lastInitialIndex < cleanedName.Length - 1)
            {
                initials += cleanedName[lastInitialIndex + 1];
            }
        }

        // Restrict to 3 letters max
        return initials.Substring(0, Math.Min(initials.Length, 3));
    }

    
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