using System.Windows.Threading;
using NusaLite.Models;

namespace NusaLite.Services;

public sealed class WaterService : IDisposable
{
    private readonly SettingsStore _settings;
    private readonly DispatcherTimer _timer;
    private DateTimeOffset _lastDrink = DateTimeOffset.MinValue;
    private DateTimeOffset _lastReminder = DateTimeOffset.MinValue;

    public WaterService(SettingsStore settings)
    {
        _settings = settings;
        EnsureToday();
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(60)
        };
        _timer.Tick += (_, _) => CheckReminder();
        _timer.Start();
    }

    public event Action? Updated;

    public event Action? ReminderDue;

    public int TodayMl => _settings.Current.WaterTodayMl;

    public int GoalMl => Math.Max(250, _settings.Current.WaterGoalMl);

    public double Progress => Math.Clamp((double)TodayMl / GoalMl, 0, 1);

    public bool GoalReached => TodayMl >= GoalMl;

    public string AmountText => $"{TodayMl / 1000.0:0.0} / {GoalMl / 1000.0:0.0} L";

    public string PercentText => $"{(int)Math.Round(Progress * 100)}%";

    public string RemainingText
    {
        get
        {
            var left = Math.Max(0, GoalMl - TodayMl);
            return left == 0 ? "Target tercapai" : $"Kurang {(int)Math.Ceiling(left / 250.0)} gelas lagi";
        }
    }

    public IReadOnlyList<WaterDay> LastWeek
    {
        get
        {
            var days = new List<WaterDay>();
            var today = DateTime.Today;
            for (var i = 6; i >= 0; i--)
            {
                var date = today.AddDays(-i);
                var key = date.ToString("yyyy-MM-dd");
                var ml = date == today
                    ? TodayMl
                    : _settings.Current.WaterHistory.GetValueOrDefault(key, 0);
                days.Add(new WaterDay(date, ml, GoalMl));
            }

            return days;
        }
    }

    public void Add(int milliliters)
    {
        EnsureToday();
        _settings.Current.WaterTodayMl = Math.Clamp(TodayMl + milliliters, 0, GoalMl * 3);
        _settings.Current.WaterHistory[DateTime.Today.ToString("yyyy-MM-dd")] = _settings.Current.WaterTodayMl;
        _lastDrink = DateTimeOffset.Now;
        _settings.Save();
        Updated?.Invoke();
    }

    public void ResetToday()
    {
        EnsureToday();
        _settings.Current.WaterTodayMl = 0;
        _settings.Current.WaterHistory[DateTime.Today.ToString("yyyy-MM-dd")] = 0;
        _settings.Save();
        Updated?.Invoke();
    }

    public void SetGoal(int milliliters)
    {
        _settings.Current.WaterGoalMl = Math.Clamp(milliliters, 500, 6000);
        _settings.Save();
        Updated?.Invoke();
    }

    private void EnsureToday()
    {
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        if (_settings.Current.WaterDate != today)
        {
            _settings.Current.WaterDate = today;
            _settings.Current.WaterTodayMl = _settings.Current.WaterHistory.GetValueOrDefault(today, 0);
        }

        var cutoff = DateTime.Today.AddDays(-30).ToString("yyyy-MM-dd");
        var stale = _settings.Current.WaterHistory.Keys
            .Where(key => string.Compare(key, cutoff, StringComparison.Ordinal) < 0)
            .ToList();
        foreach (var key in stale)
        {
            _settings.Current.WaterHistory.Remove(key);
        }
    }

    private void CheckReminder()
    {
        EnsureToday();
        if (GoalReached)
        {
            return;
        }

        var minutes = Math.Clamp(_settings.Current.WaterReminderMinutes, 5, 240);
        var now = DateTimeOffset.Now;

        if (_lastDrink == DateTimeOffset.MinValue)
        {
            _lastDrink = now;
            return;
        }

        if ((now - _lastDrink).TotalMinutes < minutes)
        {
            return;
        }

        if ((now - _lastReminder).TotalMinutes < minutes)
        {
            return;
        }

        _lastReminder = now;
        ReminderDue?.Invoke();
    }

    public void Dispose() => _timer.Stop();
}

public sealed record WaterDay(DateTime Date, int Milliliters, int Goal)
{
    public double Ratio => Goal <= 0 ? 0 : Math.Clamp((double)Milliliters / Goal, 0, 1);

    public bool IsToday => Date.Date == DateTime.Today;

    public string Label => Date.DayOfWeek switch
    {
        DayOfWeek.Monday => "Sen",
        DayOfWeek.Tuesday => "Sel",
        DayOfWeek.Wednesday => "Rab",
        DayOfWeek.Thursday => "Kam",
        DayOfWeek.Friday => "Jum",
        DayOfWeek.Saturday => "Sab",
        _ => "Min"
    };

    public string Tooltip => $"{Date:dd MMM}: {Milliliters} ml";
}
