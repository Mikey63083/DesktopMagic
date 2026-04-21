using DesktopMagic.Api;
using DesktopMagic.Api.Settings;
using DesktopMagic.Helpers;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DesktopMagic.BuiltInPlugins;

internal class AgendaPlugin : AsyncPlugin
{
    [Setting("agenda-calendar-url", "iCal URL")]
    private readonly TextBox calendarUrl = new TextBox(string.Empty);

    [Setting("agenda-refresh-button", "Refresh Calendar")]
    private readonly Button refreshButton = new Button("Refresh");

    [Setting("agenda-refresh-interval", "Refresh Every (minutes)")]
    private readonly IntegerUpDown refreshMinutes = new IntegerUpDown(1, 120, 10);

    [Setting("agenda-lookahead-hours", "Look Ahead (hours)")]
    private readonly IntegerUpDown lookAheadHours = new IntegerUpDown(1, 336, 24);

    [Setting("agenda-max-events", "Maximum Events")]
    private readonly IntegerUpDown maxEvents = new IntegerUpDown(1, 15, 5);

    [Setting("agenda-include-all-day", "Include All-Day Events")]
    private readonly CheckBox includeAllDayEvents = new CheckBox(true);

    [Setting("agenda-show-date", "Show Date")]
    private readonly CheckBox showDate = new CheckBox(true);

    public override int UpdateInterval { get; set; } = 30000;

    private List<CalendarEventEntry> upcomingEvents = [];
    private DateTime nextRefreshTime = DateTime.MinValue;
    private bool forceRefresh = true;
    private bool themeChanged = true;
    private string? errorText;

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        refreshButton.OnClick += () =>
        {
            forceRefresh = true;
            Application.UpdateWindow();
        };

        calendarUrl.OnValueChanged += MarkForRefresh;
        refreshMinutes.OnValueChanged += MarkForRefresh;
        lookAheadHours.OnValueChanged += MarkForRefresh;
        maxEvents.OnValueChanged += MarkForRefresh;
        includeAllDayEvents.OnValueChanged += MarkForRefresh;
        showDate.OnValueChanged += Application.UpdateWindow;

        return Task.CompletedTask;
    }

    public override async Task<Bitmap?> MainAsync(CancellationToken cancellationToken)
    {
        if (themeChanged || forceRefresh || DateTime.Now >= nextRefreshTime)
        {
            await RefreshEventsAsync(cancellationToken);
            themeChanged = false;
        }

        return RenderAgendaBitmap();
    }

    public override void OnThemeChanged()
    {
        themeChanged = true;
    }

    public override void OnSettingsChanged()
    {
        Application.UpdateWindow();
    }

    private void MarkForRefresh()
    {
        forceRefresh = true;
        Application.UpdateWindow();
    }

    private async Task RefreshEventsAsync(CancellationToken cancellationToken)
    {
        forceRefresh = false;
        nextRefreshTime = DateTime.Now.AddMinutes(Math.Max(1, refreshMinutes.Value));

        if (string.IsNullOrWhiteSpace(calendarUrl.Value))
        {
            upcomingEvents = [];
            errorText = "Set iCal URL in settings";
            return;
        }

        try
        {
            DateTime now = DateTime.Now;
            DateTime windowEnd = now.AddHours(Math.Max(1, lookAheadHours.Value));

            upcomingEvents = await IcsCalendarHelper.GetUpcomingEventsAsync(
                calendarUrl.Value.Trim(),
                now,
                windowEnd,
                maxEvents.Value,
                includeAllDayEvents.Value,
                cancellationToken);

            errorText = null;
        }
        catch (HttpRequestException ex)
        {
            upcomingEvents = [];
            errorText = $"Calendar unavailable: {ex.Message}";
        }
        catch (TaskCanceledException ex)
        {
            upcomingEvents = [];
            errorText = $"Calendar unavailable: {ex.Message}";
        }
        catch (InvalidOperationException ex)
        {
            upcomingEvents = [];
            errorText = $"Calendar unavailable: {ex.Message}";
        }
    }

    private Bitmap RenderAgendaBitmap()
    {
        List<(string Day, string Date, string Time, string Title, bool IsMessage)> rows = [];

        if (!string.IsNullOrWhiteSpace(errorText))
        {
            rows.Add((string.Empty, string.Empty, string.Empty, errorText, true));
        }
        else if (upcomingEvents.Count == 0)
        {
            rows.Add((string.Empty, string.Empty, string.Empty, "No events in selected window", true));
        }
        else
        {
            foreach (CalendarEventEntry eventEntry in upcomingEvents)
            {
                string dayPart = showDate.Value ? eventEntry.StartLocal.ToString("ddd") : string.Empty;
                string datePart = showDate.Value ? eventEntry.StartLocal.ToString("dd MMM yyyy") : string.Empty;
                string timePart = eventEntry.IsAllDay ? "All day" : eventEntry.StartLocal.ToShortTimeString();

                rows.Add((dayPart, datePart, timePart, eventEntry.Title, false));
            }
        }

        using Font font = new Font(Application.Theme.Font, 70);
        using Bitmap measureBitmap = new Bitmap(1, 1);
        using Graphics measureGraphics = Graphics.FromImage(measureBitmap);
        measureGraphics.TextRenderingHint = TextRenderingHint.AntiAlias;

        bool hasDayColumn = rows.Any(row => !row.IsMessage && !string.IsNullOrWhiteSpace(row.Day));
        bool hasDateColumn = rows.Any(row => !row.IsMessage && !string.IsNullOrWhiteSpace(row.Date));
        bool hasTimeColumn = rows.Any(row => !row.IsMessage && !string.IsNullOrWhiteSpace(row.Time));
        float maxDayWidth = 0;
        float maxDateWidth = 0;
        float maxTimeWidth = 0;
        float maxTitleWidth = 0;
        float lineHeight = 0;

        foreach ((string day, string date, string time, string title, bool isMessage) in rows)
        {
            if (isMessage)
            {
                SizeF messageSize = measureGraphics.MeasureString(title, font);
                maxTitleWidth = Math.Max(maxTitleWidth, messageSize.Width);
                lineHeight = Math.Max(lineHeight, messageSize.Height);
                continue;
            }

            if (hasDayColumn)
            {
                SizeF daySize = measureGraphics.MeasureString(day, font);
                maxDayWidth = Math.Max(maxDayWidth, daySize.Width);
                lineHeight = Math.Max(lineHeight, daySize.Height);
            }

            if (hasDateColumn)
            {
                SizeF dateSize = measureGraphics.MeasureString(date, font);
                maxDateWidth = Math.Max(maxDateWidth, dateSize.Width);
                lineHeight = Math.Max(lineHeight, dateSize.Height);
            }

            if (hasTimeColumn)
            {
                SizeF timeSize = measureGraphics.MeasureString(time, font);
                maxTimeWidth = Math.Max(maxTimeWidth, timeSize.Width);
                lineHeight = Math.Max(lineHeight, timeSize.Height);
            }

            SizeF titleSize = measureGraphics.MeasureString(title, font);
            maxTitleWidth = Math.Max(maxTitleWidth, titleSize.Width);
            lineHeight = Math.Max(lineHeight, titleSize.Height);
        }

        float columnSpacing = measureGraphics.MeasureString("  ", font).Width;

        float prefixWidth = 0;
        bool hasAnyPrefix = false;
        if (hasDayColumn)
        {
            prefixWidth += maxDayWidth;
            hasAnyPrefix = true;
        }
        if (hasDateColumn)
        {
            prefixWidth += (hasAnyPrefix ? columnSpacing : 0) + maxDateWidth;
            hasAnyPrefix = true;
        }
        if (hasTimeColumn)
        {
            prefixWidth += (hasAnyPrefix ? columnSpacing : 0) + maxTimeWidth;
            hasAnyPrefix = true;
        }

        float totalWidth = (hasAnyPrefix ? prefixWidth + columnSpacing : 0) + maxTitleWidth;

        int width = Math.Max(1, (int)Math.Ceiling(totalWidth));
        int height = Math.Max(1, (int)Math.Ceiling(lineHeight * rows.Count));

        Bitmap bitmap = new Bitmap(width, height);
        bitmap.SetResolution(100, 100);

        using Graphics graphics = Graphics.FromImage(bitmap);
        using SolidBrush brush = new SolidBrush(Application.Theme.PrimaryColor);
        graphics.TextRenderingHint = TextRenderingHint.AntiAlias;
        graphics.Clear(Color.Transparent);

        float dayColumnX = 0;
        float dateColumnX = hasDayColumn ? dayColumnX + maxDayWidth + columnSpacing : dayColumnX;
        float timeColumnX = hasDateColumn ? dateColumnX + maxDateWidth + columnSpacing : dateColumnX;
        float titleColumnX = hasAnyPrefix ? timeColumnX + (hasTimeColumn ? maxTimeWidth : 0) + columnSpacing : 0;

        for (int index = 0; index < rows.Count; index++)
        {
            (string day, string date, string time, string title, bool isMessage) = rows[index];
            float y = index * lineHeight;

            if (isMessage)
            {
                graphics.DrawString(title, font, brush, 0, y);
                continue;
            }

            if (hasDayColumn && !string.IsNullOrWhiteSpace(day))
            {
                graphics.DrawString(day, font, brush, dayColumnX, y);
            }

            if (hasDateColumn && !string.IsNullOrWhiteSpace(date))
            {
                graphics.DrawString(date, font, brush, dateColumnX, y);
            }

            if (hasTimeColumn && !string.IsNullOrWhiteSpace(time))
            {
                graphics.DrawString(time, font, brush, timeColumnX, y);
            }

            graphics.DrawString(title, font, brush, titleColumnX, y);
        }

        return bitmap;
    }
}
