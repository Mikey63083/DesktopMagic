using Ical.Net;
using Ical.Net.CalendarComponents;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DesktopMagic.Helpers;

public sealed class CalendarEventEntry
{
    public string Title { get; init; } = "Untitled";
    public string? Location { get; init; }
    public DateTime StartLocal { get; init; }
    public DateTime EndLocal { get; init; }
    public bool IsAllDay { get; init; }
}

public static class IcsCalendarHelper
{
    private static readonly HttpClient httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(12)
    };

    static IcsCalendarHelper()
    {
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("DesktopMagic");
        httpClient.DefaultRequestHeaders.Accept.ParseAdd("text/calendar");
    }

    public static async Task<List<CalendarEventEntry>> GetUpcomingEventsAsync(
        string calendarUrl,
        DateTime windowStartLocal,
        DateTime windowEndLocal,
        int maxEvents,
        bool includeAllDayEvents,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await httpClient.GetAsync(calendarUrl, cancellationToken);
        response.EnsureSuccessStatusCode();
        string icsContent = await response.Content.ReadAsStringAsync(cancellationToken);

        Calendar calendar = Calendar.Load(icsContent);
        DateTime windowStartUtc = windowStartLocal.ToUniversalTime();
        DateTime windowEndUtc = windowEndLocal.ToUniversalTime();

        List<CalendarEventEntry> results = [];

        foreach (var occurrence in calendar.GetOccurrences(windowStartUtc, windowEndUtc))
        {
            if (occurrence.Source is not CalendarEvent calendarEvent)
            {
                continue;
            }

            DateTime startLocal = occurrence.Period.StartTime.AsSystemLocal;
            DateTime endLocal = occurrence.Period.EndTime?.AsSystemLocal ?? startLocal.AddHours(1);
            bool isAllDay = !occurrence.Period.StartTime.HasTime;

            if (!includeAllDayEvents && isAllDay)
            {
                continue;
            }

            if (endLocal <= windowStartLocal)
            {
                continue;
            }

            results.Add(new CalendarEventEntry
            {
                Title = string.IsNullOrWhiteSpace(calendarEvent.Summary) ? "Untitled" : calendarEvent.Summary,
                Location = string.IsNullOrWhiteSpace(calendarEvent.Location) ? null : calendarEvent.Location,
                StartLocal = startLocal,
                EndLocal = endLocal,
                IsAllDay = isAllDay
            });
        }

        return results
            .OrderBy(eventEntry => eventEntry.StartLocal)
            .Take(Math.Max(1, maxEvents))
            .ToList();
    }
}
