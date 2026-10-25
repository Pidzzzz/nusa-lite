using System.Windows.Threading;
using NusaLite.Infrastructure;

namespace NusaLite.Services;

public enum PomodoroPhase
{
    Focus,
    Break
}

public sealed class PomodoroService : IDisposable
{
    private readonly SettingsStore _settings;
    private readonly DispatcherTimer _timer;
    private DateTimeOffset _endsAt;
    private double _durationSeconds;
    private int _lastShownSeconds = -1;
    private bool _running;

    public PomodoroService(SettingsStore settings)
    {
        _settings = settings;
        _durationSeconds = Math.Max(1, settings.Current.FocusMinutes) * 60;
        _remaining = TimeSpan.FromSeconds(_durationSeconds);
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
    }

    public event Action? Updated;

    public event Action<PomodoroPhase>? Completed;

    public PomodoroPhase Phase { get; private set; } = PomodoroPhase.Focus;

    private TimeSpan _remaining;

    public TimeSpan Remaining
    {
        get => _remaining;
        private set
        {
            _remaining = value < TimeSpan.Zero ? TimeSpan.Zero : value;
        }
    }

    public bool IsRunning => _running;

    public int SessionsCompleted { get; private set; }

    public string PhaseLabel => Phase == PomodoroPhase.Focus ? "Fokus" : "Istirahat";

    public string TimeText
    {
        get
        {
            var seconds = (int)Math.Ceiling(Remaining.TotalSeconds);
            return $"{seconds / 60:00}:{seconds % 60:00}";
        }
    }

    public double Progress
    {
        get
        {
            if (_durationSeconds <= 0)
            {
                return 1;
            }

            return Math.Clamp(Remaining.TotalSeconds / _durationSeconds, 0, 1);
        }
    }

    public string SessionsText => SessionsCompleted == 0
        ? "Belum ada sesi selesai"
        : $"{SessionsCompleted} sesi selesai";

    public string StartLabel => IsRunning ? "Jeda" : "Mulai";

    public string DurationText => $"{(int)Math.Round(_durationSeconds / 60)} menit";

    public void Toggle()
    {
        if (_running)
        {
            Remaining = _endsAt - DateTimeOffset.Now;
            _running = false;
        }
        else
        {
            if (Remaining <= TimeSpan.Zero)
            {
                ResetForPhase(Phase);
            }

            _endsAt = DateTimeOffset.Now + Remaining;
            _running = true;
        }

        Notify(force: true);
    }

    public void Reset()
    {
        _running = false;
        ResetForPhase(Phase);
        Notify(force: true);
    }

    public void Skip()
    {
        var previous = Phase;
        MoveToNextPhase(previous);
        Notify(force: true);
    }

    private void Tick()
    {
        try
        {
            if (!_running)
            {
                return;
            }

            Remaining = _endsAt - DateTimeOffset.Now;
            if (Remaining <= TimeSpan.Zero)
            {
                var completed = Phase;
                if (completed == PomodoroPhase.Focus)
                {
                    SessionsCompleted++;
                }

                _running = false;
                MoveToNextPhase(completed);
                Completed?.Invoke(completed);
                _endsAt = DateTimeOffset.Now + Remaining;
                _running = true;
            }

            var shown = (int)Math.Ceiling(Remaining.TotalSeconds);
            if (shown != _lastShownSeconds)
            {
                _lastShownSeconds = shown;
                Notify(force: true);
            }
        }
        catch (Exception ex)
        {
            Log.Write("Pomodoro tick failed.", ex);
        }
    }

    private void MoveToNextPhase(PomodoroPhase completed)
    {
        Phase = completed == PomodoroPhase.Focus ? PomodoroPhase.Break : PomodoroPhase.Focus;
        ResetForPhase(Phase);
    }

    private void ResetForPhase(PomodoroPhase phase)
    {
        var minutes = phase == PomodoroPhase.Focus
            ? _settings.Current.FocusMinutes
            : _settings.Current.BreakMinutes;
        _durationSeconds = Math.Max(1, minutes) * 60;
        Remaining = TimeSpan.FromSeconds(_durationSeconds);
        _lastShownSeconds = -1;
    }

    private void Notify(bool force)
    {
        if (force)
        {
            Updated?.Invoke();
        }
    }

    public void Dispose()
    {
        _timer.Stop();
    }
}
