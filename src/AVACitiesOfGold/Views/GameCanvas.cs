using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using AVACitiesOfGold.Game;

namespace AVACitiesOfGold.Views;

/// <summary>
/// The game's screen: a fixed 1024×768 picture scaled to fit the window. Keys go to the engine by
/// name; a 60 Hz timer runs the engine's clock (fights play out, the demo walks) and redraws.
/// </summary>
public sealed partial class GameCanvas : Control
{
    public const double ScreenWidth = 1024;
    public const double ScreenHeight = 768;

    private readonly DispatcherTimer? _timer;
    private DateTime _last = DateTime.Now;

    public GameEngine Engine { get; }
    public SoundManager Sound { get; }

    public GameCanvas(bool silent = false) : this(new GameEngine(Environment.TickCount), new SoundManager(silent), startTimer: true) { }

    /// <summary>For tests: an engine of your own, and no timer (call <see cref="Tick"/>).</summary>
    public GameCanvas(GameEngine engine, SoundManager sound, bool startTimer)
    {
        Engine = engine;
        Sound = sound;
        Engine.Sound += Sound.Play;
        Engine.QuitRequested += () => (TopLevel.GetTopLevel(this) as Window)?.Close();
        Focusable = true;
        ClipToBounds = true;
        if (startTimer)
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _timer.Tick += (_, _) => Tick();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _timer?.Start();
        Focus();
        // The title tune starts before the sound system is listening; start it again.
        if (Engine.Mode == Mode.Title) Sound.Play(Sfx.TitleTune);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer?.Stop();
        Sound.Dispose();
    }

    /// <summary>One frame: run the game's clock and redraw.</summary>
    public void Tick(double? step = null)
    {
        var now = DateTime.Now;
        double dt = step ?? Math.Min(0.1, (now - _last).TotalSeconds);
        _last = now;
        Engine.Update(dt);
        InvalidateVisual();
    }

    private static string? KeyName(Key key) => key switch
    {
        Key.Up => "Up", Key.Down => "Down", Key.Left => "Left", Key.Right => "Right",
        Key.Enter => "Enter", Key.Escape => "Esc", Key.Space => "Space", Key.Tab => "Tab", Key.Back => "Back",
        Key.Home => "Home", Key.End => "End", Key.PageUp => "PageUp", Key.PageDown => "PageDown",
        >= Key.A and <= Key.Z => ((char)('A' + (key - Key.A))).ToString(),
        >= Key.D0 and <= Key.D9 => ((char)('0' + (key - Key.D0))).ToString(),
        >= Key.NumPad0 and <= Key.NumPad9 => ((char)('0' + (key - Key.NumPad0))).ToString(),
        _ => null,
    };

    protected override void OnKeyDown(KeyEventArgs e)
    {
        e.Handled = true;
        bool ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        if (e.Key == Key.S && ctrl)
        {
            SaveScreenshot(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), $"AVACitiesOfGold_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png"));
            return;
        }
        if (e.Key == Key.F2 || (e.Key == Key.M && ctrl))
        {
            Sound.IsMuted = !Sound.IsMuted;
            return;
        }
        if (KeyName(e.Key) is { } name) Engine.Press(name);
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        if (e.Text is { } text && Engine.Top is { Entry: not null }) Engine.Type(text);
        e.Handled = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e) => Focus();

    /// <summary>Saves what's on screen as a PNG.</summary>
    public void SaveScreenshot(string path)
    {
        var size = new PixelSize((int)Math.Max(1, Bounds.Width), (int)Math.Max(1, Bounds.Height));
        using var bitmap = new RenderTargetBitmap(size);
        bitmap.Render(this);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        bitmap.Save(path, PngBitmapEncoderOptions.Default);
    }

    /// <summary>How the fixed screen fits the window: scale and letterbox offsets.</summary>
    private (double Scale, double X, double Y) Fit()
    {
        double s = Math.Min(Bounds.Width / ScreenWidth, Bounds.Height / ScreenHeight);
        return (s, (Bounds.Width - ScreenWidth * s) / 2, (Bounds.Height - ScreenHeight * s) / 2);
    }

    public override void Render(DrawingContext dc)
    {
        dc.FillRectangle(Brushes.Black, new Rect(Bounds.Size));
        var (s, ox, oy) = Fit();
        if (s <= 0) return;
        using (dc.PushTransform(Matrix.CreateScale(s, s) * Matrix.CreateTranslation(ox, oy)))
        using (dc.PushClip(new Rect(0, 0, ScreenWidth, ScreenHeight)))
            DrawScreen(dc);
    }
}
