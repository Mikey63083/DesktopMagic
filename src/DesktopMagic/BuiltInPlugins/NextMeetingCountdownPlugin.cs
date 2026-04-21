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

internal class NextMeetingCountdownPlugin : AsyncPlugin
{
    [Setting("meeting-calendar-url", "iCal URL")]
    private readonly TextBox calendarUrl = new TextBox(string.Empty);

    [Setting("meeting-refresh-button", "Refresh Calendar")]
    private readonly Button refreshButton = new Button("Refresh");

    [Setting("meeting-refresh-interval", "Refresh Every (minutes)")]
    private readonly IntegerUpDown refreshMinutes = new IntegerUpDown(1, 120, 10);

    [Setting("meeting-lookahead-hours", "Look Ahead (hours)")]
    private readonly IntegerUpDown lookAheadHours = new IntegerUpDown(1, 336, 48);

    [Setting("meeting-include-all-day", "Include All-Day Events")]
    private readonly CheckBox includeAllDayEvents = new CheckBox(false);

    [Setting("meeting-time-display", "Start Time Format")]
    private readonly ComboBox startTimeDisplay = new ComboBox("None", "Date and time", "Time only");

    [Setting("meeting-show-location", "Show Location")]
    private readonly CheckBox showLocation = new CheckBox(false);

    [Setting("meeting-center-lines", "Center Lines")]
    private readonly CheckBox centerLines = new CheckBox(false);

    public override int UpdateInterval { get; set; } = 1000;

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
        includeAllDayEvents.OnValueChanged += MarkForRefresh;
        startTimeDisplay.OnValueChanged += Application.UpdateWindow;
        showLocation.OnValueChanged += Application.UpdateWindow;
        centerLines.OnValueChanged += Application.UpdateWindow;

        return Task.CompletedTask;
    }

    public override async Task<Bitmap?> MainAsync(CancellationToken cancellationToken)
    {
        if (themeChanged || forceRefresh || DateTime.Now >= nextRefreshTime)
        {
            await RefreshEventsAsync(cancellationToken);
            themeChanged = false;
        }

        return RenderCountdownBitmap();
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
                20,
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

    private Bitmap RenderCountdownBitmap()
    {
        List<string> lines = [];

        if (!string.IsNullOrWhiteSpace(errorText))
        {
            lines.Add(errorText);
        }
        else
        {
            DateTime now = DateTime.Now;
            CalendarEventEntry? nextEvent = upcomingEvents.FirstOrDefault(eventEntry => eventEntry.EndLocal > now);

            if (nextEvent is null)
            {
                lines.Add("No upcoming meetings");
            }
            else
            {
                lines.Add(nextEvent.Title);

                if (startTimeDisplay.Value != "None")
                {
                    string timeText = nextEvent.IsAllDay
                        ? "All day"
                        : startTimeDisplay.Value == "Time only"
                            ? nextEvent.StartLocal.ToString("t")
                            : $"{nextEvent.StartLocal:D} {nextEvent.StartLocal:t}";
                    lines.Add(timeText);
                }

                if (showLocation.Value && !string.IsNullOrWhiteSpace(nextEvent.Location))
                {
                    lines.Add(nextEvent.Location!);
                }

                if (now < nextEvent.StartLocal)
                {
                    lines.Add($"Starts in {FormatCountdown(nextEvent.StartLocal - now)}");
                }
                else
                {
                    lines.Add("In progress");
                }
            }
        }

        using Font font = new Font(Application.Theme.Font, 90);
        using Bitmap measureBitmap = new Bitmap(1, 1);
        using Graphics measureGraphics = Graphics.FromImage(measureBitmap);
        measureGraphics.TextRenderingHint = TextRenderingHint.AntiAlias;

        float maxWidth = 0;
        float lineHeight = 0;
        foreach (string line in lines)
        {
            SizeF size = measureGraphics.MeasureString(line, font);
            maxWidth = Math.Max(maxWidth, size.Width);
            lineHeight = Math.Max(lineHeight, size.Height);
        }

        int width = Math.Max(1, (int)Math.Ceiling(maxWidth));
        int height = Math.Max(1, (int)Math.Ceiling(lineHeight * lines.Count));

        Bitmap bitmap = new Bitmap(width, height);
        bitmap.SetResolution(100, 100);

        using Graphics graphics = Graphics.FromImage(bitmap);
        using SolidBrush brush = new SolidBrush(Application.Theme.PrimaryColor);
        graphics.TextRenderingHint = TextRenderingHint.AntiAlias;
        graphics.Clear(Color.Transparent);

        for (int index = 0; index < lines.Count; index++)
        {
            string line = lines[index];
            float x = 0;
            if (centerLines.Value)
            {
                SizeF lineSize = graphics.MeasureString(line, font);
                x = Math.Max(0, (width - lineSize.Width) / 2f);
            }

            graphics.DrawString(line, font, brush, x, index * lineHeight);
        }

        return bitmap;
    }

    private static string FormatCountdown(TimeSpan timeSpan)
    {
        int totalHours = (int)timeSpan.TotalHours;
        return $"{totalHours:00}:{timeSpan.Minutes:00}:{timeSpan.Seconds:00}";
    }
}
