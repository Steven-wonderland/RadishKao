using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using CatPet.Behavior;
using CatPet.Config;
using System.Windows.Media.Imaging;
using CatPet.Interop;
using CatPet.Sprites;
using CatPet.Windows;
using Application = System.Windows.Application;
using Cursor = System.Windows.Input.Cursor;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using TriggerAction = CatPet.Config.TriggerAction;

namespace CatPet;

public partial class PetWindow : Window
{
    /// <summary>Transparent strip reserved above the cat for the speech bubble.</summary>
    private const double BubbleHeightDip = 54;

    /// <summary>Transparent room either side of the cat so bubbles can be wider than it.</summary>
    private const double SideMarginDip = 95;

    private const double GravityDipPerSec2 = 2600;
    private const byte HitAlphaThreshold = 24;
    private const double DragThresholdDip = 6;

    private const double MinScale = 0.15;
    private const double MaxScale = 2.0;

    /// <summary>One notch of the resize menu items.</summary>
    private const double ScaleStep = 1.05;

    /// <summary>
    /// How much a chase has to climb or descend before the back/front running
    /// art is used instead of the side-on art. 2.2 means any slope steeper than
    /// about 25 degrees counts as running into or out of the screen.
    /// </summary>
    private const double VerticalBias = 2.2;

    private readonly string _baseDirectory = AppContext.BaseDirectory;
    private readonly string _configPath;
    private readonly ActionRunner _actions = new();
    private readonly DispatcherTimer _bubbleTimer = new();
    private readonly DispatcherTimer _clickTimer = new();
    private readonly Queue<BehaviorStep> _steps = new();
    private readonly string? _configError;
    private readonly Animator _animator;
    private readonly List<HotKeyBinding> _hotkeys = [];
    private readonly Dictionary<int, LureConfig> _luresByHotkeyId = [];
    private readonly List<string> _hotkeyProblems = [];
    private readonly Dictionary<string, SpriteSheet> _sheets = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _missingSheets = [];

    private LurePicker? _picker;
    private Chase? _chase;

    /// <summary>Somewhere along the taskbar the cat has been asked to go.</summary>
    private double? _walkToX;

    /// <summary>What to do on getting there; a sit when nothing else is asked.</summary>
    private Action? _onArrive;

    private PetConfig _config;
    private PetBrain _brain;

    private BehaviorStep? _step;
    private double _stepElapsed;
    private int _direction = 1;

    private IntPtr _hwnd;
    private double _dpi = 1;
    private double _scale = 0.5;
    private double _spriteWidthDip;
    private double _spriteHeightDip;

    private Ground _ground;
    private double _x;
    private double _baselineY;
    private double _fallSpeed;
    private bool _falling;

    private bool _paused;
    private bool _pressed;
    private bool _dragging;
    private bool _clickPending;
    private NativeMethods.POINT _pressCursor;
    private double _grabDx;
    private double _grabDy;

    private TimeSpan _lastRender;
    private long _frames;
    private double _groundAgeMs;

    private int _hitRow;
    private int _hitColumn;
    private SpriteSheet? _hitSheet;

    /// <summary>
    /// Render-only scaling: the depth effect during a chase times the clip's own
    /// size correction. Applied about the paw point so the cat never leaves the
    /// taskbar, and kept out of the window box so layout stays still.
    /// </summary>
    private readonly ScaleTransform _renderScale = new(1, 1);

    private double _renderScaleValue = 1;

    private System.Windows.Forms.NotifyIcon? _tray;
    private System.Windows.Forms.ContextMenuStrip? _menu;
    private System.Windows.Forms.ToolStripMenuItem? _pauseItem;
    private System.Windows.Forms.ToolStripMenuItem? _sizeLabel;
    private bool _keepMenuOpen;
    private IntPtr _iconHandle;

    public PetWindow()
    {
        InitializeComponent();

        _configPath = Path.Combine(_baseDirectory, "config.json");
        _config = PetConfig.Load(_configPath, out _configError);
        LoadSheets();
        _brain = new PetBrain(_config);
        _animator = new Animator(ClipLibrary.Get("idle"));

        ApplySize();

        // Park the window off-screen until OnSourceInitialized places it.
        Left = -8000;
        Top = -8000;

        _bubbleTimer.Tick += OnBubbleTimeout;
        _clickTimer.Tick += OnClickTimeout;

        Sprite.RenderTransform = _renderScale;

        Sprite.MouseLeftButtonDown += OnSpriteLeftDown;
        Sprite.MouseLeftButtonUp += OnSpriteLeftUp;
        Sprite.MouseMove += OnSpriteMouseMove;
        Sprite.MouseDown += OnSpriteOtherButton;

        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    // ------------------------------------------------------------------ setup

    /// <summary>
    /// Loads every sheet named in <see cref="ClipLibrary.Sheets"/>. Only the
    /// main one is fatal; a missing extra strip simply retires the clips that
    /// use it, so the cat keeps working on a partial set of art.
    /// </summary>
    private void LoadSheets()
    {
        _sheets.Clear();
        _missingSheets.Clear();

        foreach (var def in ClipLibrary.Sheets)
        {
            var path = def.Key == ClipLibrary.MainSheet && !string.IsNullOrWhiteSpace(_config.SpriteSheet)
                ? _config.SpriteSheet
                : def.Path;

            try
            {
                _sheets[def.Key] = new SpriteSheet(ResolveAssetPath(path), def.Columns, def.Rows);
            }
            catch when (!def.Required)
            {
                _missingSheets.Add(def.Path);
            }
        }
    }

    private SpriteSheet MainSheet => _sheets[ClipLibrary.MainSheet];

    /// <summary>Clip by name, swapped for idle if its sheet never loaded.</summary>
    private Clip Resolve(string name)
    {
        var clip = ClipLibrary.Get(name);
        return _sheets.ContainsKey(clip.Sheet) ? clip : ClipLibrary.Get("idle");
    }

    private string ResolveSheetPath() => ResolveAssetPath(
        string.IsNullOrWhiteSpace(_config.SpriteSheet)
            ? "Assets/cat-spritesheet.png"
            : _config.SpriteSheet);

    private void ApplySize()
    {
        _scale = Math.Clamp(_config.Scale, MinScale, MaxScale);
        _spriteWidthDip = MainSheet.CellWidth * _scale;
        _spriteHeightDip = MainSheet.CellHeight * _scale;

        Sprite.Width = _spriteWidthDip;
        SpriteRow.Height = new GridLength(_spriteHeightDip);
        Width = _spriteWidthDip + SideMarginDip * 2;
        Height = BubbleHeightDip + _spriteHeightDip;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _hwnd = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(_hwnd)?.AddHook(WndProc);

        var exStyle = NativeMethods.GetWindowLong(_hwnd, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLong(
            _hwnd,
            NativeMethods.GWL_EXSTYLE,
            exStyle | NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE);

        UpdateDpi();
        RefreshGround();

        _x = _ground.Left + _ground.Span / 2.0;
        _baselineY = _ground.FloorY;
        Reposition(forceTopmost: true);

        RegisterLureHotkeys();
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        SetupTray();
        NextStep();
        CompositionTarget.Rendering += OnRendering;

        if (_configError is not null)
        {
            ShowBubble($"config.json 讀不動: {_configError}");
        }
        else if (_missingSheets.Count > 0)
        {
            ShowBubble($"少了圖：{string.Join("、", _missingSheets)}");
        }
        else if (_hotkeyProblems.Count > 0)
        {
            ShowBubble(string.Join("；", _hotkeyProblems));
        }
        else if (!string.IsNullOrWhiteSpace(_config.Greeting))
        {
            Perform(new TriggerAction { Animation = "look_right", Say = _config.Greeting });
        }
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        UpdateDpi();
        RefreshGround();
    }

    private void UpdateDpi()
    {
        var raw = _hwnd != IntPtr.Zero ? NativeMethods.GetDpiForWindow(_hwnd) : 0;
        _dpi = raw > 0 ? raw / 96.0 : VisualTreeHelper.GetDpi(this).DpiScaleX;
    }

    private void RefreshGround()
    {
        var widthPx = (int)Math.Round(_spriteWidthDip * _dpi);
        var offsetPx = (int)Math.Round(_config.FootOffset * _dpi);

        _ground = Taskbar.Resolve(widthPx, offsetPx);

        // While the cat is off chasing something it is allowed outside the band.
        if (_chase is not null)
        {
            return;
        }

        _x = Math.Clamp(_x, _ground.Left, _ground.Right);

        if (!_dragging && !_falling)
        {
            _baselineY = _ground.FloorY;
        }
    }

    // ------------------------------------------------------------- frame loop

    private void OnRendering(object? sender, EventArgs e)
    {
        if (e is not RenderingEventArgs args || args.RenderingTime == _lastRender)
        {
            return;
        }

        var deltaMs = (args.RenderingTime - _lastRender).TotalMilliseconds;
        _lastRender = args.RenderingTime;

        // First frame, or the app was starved; don't let the cat teleport.
        if (deltaMs <= 0 || deltaMs > 200)
        {
            deltaMs = 16.7;
        }

        Tick(deltaMs);
    }

    private void Tick(double deltaMs)
    {
        _groundAgeMs += deltaMs;
        if (_groundAgeMs >= 2000)
        {
            _groundAgeMs = 0;
            RefreshGround();
        }

        var animating = true;

        if (_dragging)
        {
            UpdateDrag();
        }
        else if (_falling)
        {
            UpdateFall(deltaMs);
        }
        else if (_chase is not null)
        {
            UpdateChase(deltaMs);
        }
        else if (_walkToX is { } goal)
        {
            if (MoveTowards(goal, _ground.FloorY, _config.WalkSpeed * 2.4 * _dpi, deltaMs))
            {
                _walkToX = null;
                var arrived = _onArrive;
                _onArrive = null;

                _steps.Clear();
                _step = null;

                if (arrived is not null)
                {
                    arrived();
                }
                else
                {
                    _steps.Enqueue(new BehaviorStep("sit", 5000));
                    NextStep();
                }
            }
        }
        else if (!_paused)
        {
            UpdateBehavior(deltaMs);
        }
        else
        {
            animating = false;
        }

        if (animating)
        {
            _animator.Advance(deltaMs);
        }

        _hitRow = _animator.Row;
        _hitColumn = _animator.Column;
        _hitSheet = _sheets.TryGetValue(_animator.Current.Sheet, out var sheet) ? sheet : MainSheet;
        Sprite.Source = _hitSheet.Frame(_hitRow, _hitColumn);
        ApplyRenderScale();

        Reposition(forceTopmost: ++_frames % 120 == 0);

        // Re-asserted after the cat so the tin stays in front of it while eating.
        if (_chase is { Phase: ChasePhase.Enjoy } chase && !chase.Lure.HideOnArrive)
        {
            chase.Prop.CentreOn(chase.PropX, chase.PropY, _dpi);
        }
    }

    private void UpdateBehavior(double deltaMs)
    {
        if (_step is null)
        {
            NextStep();
            if (_step is null)
            {
                return;
            }
        }

        if (_step.Direction != 0 && _step.Speed > 0)
        {
            _x += _direction * _step.Speed * _dpi * deltaMs / 1000.0;

            if (_x <= _ground.Left)
            {
                _x = _ground.Left;
                _direction = 1;
            }
            else if (_x >= _ground.Right)
            {
                _x = _ground.Right;
                _direction = -1;
            }

            // Turning round at the edge swaps to the mirrored art.
            _animator.Play(Resolve(_step.ResolveClip(_direction)));
        }

        _stepElapsed += deltaMs;

        var finished = _step.RunsUntilClipEnds
            ? _animator.CompletedCycles > 0
            : _stepElapsed >= _step.DurationMs;

        if (finished)
        {
            NextStep();
        }
    }

    private void NextStep()
    {
        if (_steps.Count == 0)
        {
            foreach (var step in _brain.Next())
            {
                _steps.Enqueue(step);
            }
        }

        _step = _steps.Count > 0 ? _steps.Dequeue() : null;
        _stepElapsed = 0;

        if (_step is null)
        {
            return;
        }

        if (_step.Clip == PetBrain.AnnoyingModeStep)
        {
            _step = null;
            AnnoyingMode();
            return;
        }

        if (_step.Direction != 0)
        {
            _direction = _step.Direction;
        }

        _animator.Play(
            Resolve(_step.ResolveClip(_step.Direction == 0 ? 0 : _direction)),
            restartIfSame: true);
    }

    private void UpdateFall(double deltaMs)
    {
        _fallSpeed += GravityDipPerSec2 * _dpi * deltaMs / 1000.0;
        _baselineY += _fallSpeed * deltaMs / 1000.0;

        if (_baselineY < _ground.FloorY)
        {
            return;
        }

        _baselineY = _ground.FloorY;
        _fallSpeed = 0;
        _falling = false;

        _steps.Clear();
        _steps.Enqueue(new BehaviorStep("idle", 700));
        _step = null;
        NextStep();
    }

    // ----------------------------------------------------------------- lures

    private void RegisterLureHotkeys()
    {
        foreach (var binding in _hotkeys)
        {
            binding.Dispose();
        }

        _hotkeys.Clear();
        _luresByHotkeyId.Clear();
        _hotkeyProblems.Clear();

        var id = 0xC0;

        foreach (var lure in _config.Lures)
        {
            if (string.IsNullOrWhiteSpace(lure.Hotkey))
            {
                continue;
            }

            var binding = HotKeyBinding.Register(_hwnd, id, lure.Hotkey);
            _hotkeys.Add(binding);

            if (binding.Error is null)
            {
                _luresByHotkeyId[id] = lure;
            }
            else
            {
                _hotkeyProblems.Add(binding.Error);
            }

            id++;
        }
    }

    private void OnHotKey(int id)
    {
        if (!_luresByHotkeyId.TryGetValue(id, out var lure))
        {
            return;
        }

        // Pressing the same combo again backs out of picking.
        if (_picker is not null)
        {
            _picker.CancelFromOutside();
            _picker = null;
            return;
        }

        StartPicking(lure);
    }

    private void StartPicking(LureConfig lure)
    {
        Cursor cursor;

        try
        {
            cursor = CursorFactory.Create(ResolveAssetPath(lure.Image), (int)Math.Round(32 * _dpi));
        }
        catch (Exception ex)
        {
            ShowBubble(ex.Message);
            return;
        }

        var hint = string.IsNullOrWhiteSpace(lure.Hint)
            ? "點一下決定位置 · Esc 或右鍵取消"
            : lure.Hint;

        var picker = new LurePicker(cursor, hint);
        _picker = picker;

        picker.Picked += (x, y) =>
        {
            _picker = null;
            DropLure(lure, x, y);
        };

        picker.Cancelled += () => _picker = null;
        picker.Show();
    }

    private void DropLure(LureConfig lure, double screenX, double screenY)
    {
        EndChase(resume: false);

        BitmapSource picture;

        try
        {
            picture = LoadImage(ResolveAssetPath(lure.Image));
        }
        catch (Exception ex)
        {
            ShowBubble(ex.Message);
            return;
        }

        var depth = DepthAt(screenY);
        var height = Math.Clamp(lure.DisplaySize, 8, 400) * depth;
        var width = height * picture.PixelWidth / Math.Max(1.0, picture.PixelHeight);

        var prop = new PropWindow(picture, width, height);
        prop.Show();
        prop.CentreOn(screenX, screenY, _dpi);

        _dragging = false;
        _falling = false;
        _pressed = false;
        _walkToX = null;
        _onArrive = null;
        _steps.Clear();
        _step = null;

        var chase = new Chase(lure, prop, screenX, screenY);
        AimChase(chase);
        _chase = chase;

        if (!string.IsNullOrWhiteSpace(lure.Say))
        {
            ShowBubble(lure.Say!);
        }
    }

    /// <summary>
    /// Works out where the cat has to stand for the anchor point of its arrival
    /// pose to land on the dropped item. Recomputed on resize, since every term
    /// scales with the cat.
    /// </summary>
    private void AimChase(Chase chase)
    {
        var arrival = Resolve(chase.Lure.ArriveAnimation);
        var scale = _scale * DepthAt(chase.PropY) * arrival.Zoom;

        chase.TargetX = chase.PropX - chase.Lure.AnchorX * scale * _dpi;
        chase.TargetY = chase.PropY + (arrival.Baseline - chase.Lure.AnchorY) * scale * _dpi;
    }

    private void UpdateChase(double deltaMs)
    {
        var chase = _chase!;
        var lure = chase.Lure;

        switch (chase.Phase)
        {
            case ChasePhase.Outbound:
            {
                var speed = _config.RunSpeed * Math.Max(0.2, lure.SpeedMultiplier) * _dpi;
                if (!MoveTowards(chase.TargetX, chase.TargetY, speed, deltaMs))
                {
                    break;
                }

                chase.Phase = ChasePhase.Enjoy;
                chase.PhaseElapsedMs = 0;

                if (lure.HideOnArrive)
                {
                    // The "play" art draws its own ball, so drop the real one.
                    chase.Prop.Hide();
                }

                _animator.Play(Resolve(lure.ArriveAnimation), restartIfSame: true);

                if (!string.IsNullOrWhiteSpace(lure.ArriveSay))
                {
                    ShowBubble(lure.ArriveSay!);
                }

                break;
            }

            case ChasePhase.Enjoy:
            {
                chase.PhaseElapsedMs += deltaMs;

                if (chase.PhaseElapsedMs < Math.Max(300, lure.ArriveSeconds * 1000))
                {
                    break;
                }

                chase.Prop.Hide();
                chase.Phase = ChasePhase.Home;
                chase.TargetX = Math.Clamp(_x, _ground.Left, _ground.Right);
                chase.TargetY = _ground.FloorY;
                break;
            }

            case ChasePhase.Home:
            {
                if (MoveTowards(chase.TargetX, chase.TargetY, _config.RunSpeed * 1.15 * _dpi, deltaMs))
                {
                    EndChase(resume: true);
                }

                break;
            }
        }
    }

    /// <summary>Straight-line move towards a point; true once it is reached.</summary>
    private bool MoveTowards(double targetX, double targetY, double speedPxPerSecond, double deltaMs)
    {
        var dx = targetX - _x;
        var dy = targetY - _baselineY;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        var step = Math.Max(1, speedPxPerSecond) * deltaMs / 1000.0;

        if (distance <= step)
        {
            _x = targetX;
            _baselineY = targetY;
            return true;
        }

        _x += dx / distance * step;
        _baselineY += dy / distance * step;

        _direction = dx < 0 ? -1 : 1;

        // Any real climb or descent reads as running into or out of the screen,
        // so the front/back art is the default for a chase and the side-on art
        // is kept for runs that are strongly horizontal.
        var clip = Math.Abs(dy) * VerticalBias > Math.Abs(dx)
            ? (dy < 0 ? "run_away" : "run_toward")
            : (_direction < 0 ? "run_left" : "run_right");

        _animator.Play(Resolve(clip));
        return false;
    }

    private void EndChase(bool resume)
    {
        var chase = _chase;
        _chase = null;

        chase?.Prop.Close();

        if (!resume)
        {
            return;
        }

        _baselineY = _ground.FloorY;
        _x = Math.Clamp(_x, _ground.Left, _ground.Right);
        _steps.Clear();
        _step = null;
        NextStep();
    }

    private string ResolveAssetPath(string path) =>
        Path.IsPathRooted(path) ? path : Path.Combine(_baseDirectory, path);

    private static BitmapSource LoadImage(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"找不到圖檔：{path}", path);
        }

        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri(Path.GetFullPath(path));
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();
        return image;
    }

    /// <summary>
    /// How big the cat should look at a given height. 1 on the taskbar, down to
    /// <see cref="PetConfig.MinDepth"/> at the top of the screen. Because it
    /// keys off height alone, a run straight along the taskbar is unaffected.
    /// </summary>
    private double DepthAt(double screenY)
    {
        if (!_config.DepthEffect)
        {
            return 1;
        }

        var floor = (double)_ground.FloorY;
        var climb = Math.Max(0, floor - screenY);
        var reach = Math.Max(1, floor);
        var t = Math.Clamp(climb / reach, 0, 1);

        return 1 - t * (1 - Math.Clamp(_config.MinDepth, 0.2, 1));
    }

    private void ApplyRenderScale()
    {
        var clip = _animator.Current;
        var wanted = clip.Zoom * (_chase is not null ? DepthAt(_baselineY) : 1);

        if (Math.Abs(wanted - _renderScaleValue) > 0.001)
        {
            _renderScaleValue = wanted;
            _renderScale.ScaleX = wanted;
            _renderScale.ScaleY = wanted;
        }

        // Pin the scaling to the paw point, so growing and shrinking never
        // lifts the cat off the taskbar.
        Sprite.RenderTransformOrigin =
            new System.Windows.Point(0.5, clip.Baseline / MainSheet.CellHeight);
    }

    private void Reposition(bool forceTopmost)
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        var x = (int)Math.Round(_x - SideMarginDip * _dpi);

        // Measured off whichever clip is on screen, so the paws stay on the
        // taskbar even though the running art sits higher in its cell.
        var y = (int)Math.Round(
            _baselineY - _animator.Current.Baseline * _scale * _dpi - BubbleHeightDip * _dpi);

        var flags = NativeMethods.SWP_NOSIZE
                    | NativeMethods.SWP_NOACTIVATE
                    | NativeMethods.SWP_NOOWNERZORDER;

        if (!forceTopmost)
        {
            flags |= NativeMethods.SWP_NOZORDER;
        }

        NativeMethods.SetWindowPos(
            _hwnd,
            forceTopmost ? NativeMethods.HWND_TOPMOST : IntPtr.Zero,
            x, y, 0, 0, flags);
    }

    // ----------------------------------------------------------- hit testing

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case NativeMethods.WM_NCHITTEST:
                handled = true;
                return HitTest(lParam);

            case NativeMethods.WM_HOTKEY:
                OnHotKey((int)wParam);
                handled = true;
                break;

            case NativeMethods.WM_DISPLAYCHANGE:
            case NativeMethods.WM_SETTINGCHANGE:
                RefreshGround();
                break;
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// Only the cat's own pixels swallow the mouse. Everywhere else in the
    /// window the click falls straight through to the taskbar underneath.
    /// </summary>
    private IntPtr HitTest(IntPtr lParam)
    {
        if (!NativeMethods.GetWindowRect(_hwnd, out var rect))
        {
            return new IntPtr(NativeMethods.HTCLIENT);
        }

        var packed = (long)lParam;
        var screenX = (short)(packed & 0xFFFF);
        var screenY = (short)((packed >> 16) & 0xFFFF);

        var localX = (screenX - rect.Left) / _dpi;
        var localY = (screenY - rect.Top) / _dpi;

        var cellX = (localX - SideMarginDip) / _scale;
        var cellY = (localY - BubbleHeightDip) / _scale;

        // Undo the render transform, which scales about the paw point.
        if (Math.Abs(_renderScaleValue - 1) > 0.001)
        {
            var originX = MainSheet.CellWidth / 2.0;
            var originY = _animator.Current.Baseline;
            cellX = originX + (cellX - originX) / _renderScaleValue;
            cellY = originY + (cellY - originY) / _renderScaleValue;
        }

        var alpha = (_hitSheet ?? MainSheet).AlphaAt(_hitRow, _hitColumn, (int)cellX, (int)cellY);
        return new IntPtr(alpha >= HitAlphaThreshold
            ? NativeMethods.HTCLIENT
            : NativeMethods.HTTRANSPARENT);
    }

    // ----------------------------------------------------------------- mouse

    private void OnSpriteLeftDown(object sender, MouseButtonEventArgs e)
    {
        NativeMethods.GetCursorPos(out _pressCursor);
        _pressed = true;
        _grabDx = _pressCursor.X - _x;
        _grabDy = _pressCursor.Y - _baselineY;
        Sprite.CaptureMouse();
        e.Handled = true;
    }

    private void OnSpriteMouseMove(object sender, MouseEventArgs e)
    {
        if (!_pressed || _dragging || !_config.Draggable)
        {
            return;
        }

        NativeMethods.GetCursorPos(out var cursor);
        var moved = Math.Abs(cursor.X - _pressCursor.X) + Math.Abs(cursor.Y - _pressCursor.Y);

        if (moved < DragThresholdDip * _dpi)
        {
            return;
        }

        _dragging = true;
        _falling = false;
        _fallSpeed = 0;
        _walkToX = null;
        _onArrive = null;
        EndChase(resume: false);
        _steps.Clear();
        _step = null;

        var held = Resolve("held");
        _animator.Play(held, restartIfSame: true);

        // The squirm art draws a hand gripping the scruff, so the cat snaps to
        // hang from the pointer instead of keeping the spot that was grabbed.
        if (held.Sheet != ClipLibrary.MainSheet)
        {
            var gripScale = _scale * held.Zoom;
            _grabDx = ClipLibrary.GripX * gripScale * _dpi;
            _grabDy = -(held.Baseline - ClipLibrary.GripY) * gripScale * _dpi;
        }

        Trigger("pickUp");
    }

    private void OnSpriteLeftUp(object sender, MouseButtonEventArgs e)
    {
        Sprite.ReleaseMouseCapture();

        if (_dragging)
        {
            _dragging = false;
            _falling = _baselineY < _ground.FloorY;
            _fallSpeed = 0;

            if (!_falling)
            {
                _baselineY = _ground.FloorY;
                _steps.Clear();
                _step = null;
                NextStep();
            }

            Trigger("drop");
        }
        else if (_pressed)
        {
            RegisterClick();
        }

        _pressed = false;
        e.Handled = true;
    }

    private void OnSpriteOtherButton(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Right)
        {
            ShowMenu();
            e.Handled = true;
        }
        else if (e.ChangedButton == MouseButton.Middle)
        {
            Trigger("middleClick");
            e.Handled = true;
        }
    }

    private void UpdateDrag()
    {
        NativeMethods.GetCursorPos(out var cursor);
        _x = Math.Clamp(cursor.X - _grabDx, _ground.Left, _ground.Right);
        _baselineY = Math.Min(cursor.Y - _grabDy, _ground.FloorY);
    }

    /// <summary>
    /// Holds a single click back for the system double-click interval, so a
    /// double click does not also fire the single-click reaction.
    /// </summary>
    private void RegisterClick()
    {
        if (_clickPending)
        {
            _clickPending = false;
            _clickTimer.Stop();
            Trigger("doubleClick");
            return;
        }

        _clickPending = true;
        _clickTimer.Interval =
            TimeSpan.FromMilliseconds(System.Windows.Forms.SystemInformation.DoubleClickTime);
        _clickTimer.Start();
    }

    private void OnClickTimeout(object? sender, EventArgs e)
    {
        _clickTimer.Stop();

        if (_clickPending)
        {
            _clickPending = false;
            Trigger("leftClick");
        }
    }

    // ------------------------------------------------------------- reactions

    private void Trigger(string name)
    {
        // Poking a sleeping cat gets you a stretch and a yawn, not the usual
        // click reaction.
        if (name is "leftClick" or "doubleClick"
            && _animator.Current.Name is "doze" or "lie_down"
            && Resolve("wake_stretch").Name == "wake_stretch")
        {
            _steps.Clear();
            _step = null;
            _steps.Enqueue(new BehaviorStep("wake_stretch", 0));
            NextStep();
            ShowBubble("呼啊～");
            return;
        }

        var action = _actions.Pick(_config.ActionsFor(name));
        if (action is not null)
        {
            Perform(action);
        }
    }

    /// <summary>Plays the reaction, says the line, and launches whatever is configured.</summary>
    private void Perform(TriggerAction action)
    {
        // Mid-chase the animation is driven frame by frame, so a reaction only
        // gets to speak and launch; overriding the clip would just flicker.
        if (_chase is null
            && !string.IsNullOrWhiteSpace(action.Animation)
            && ClipLibrary.TryGet(action.Animation, out _)
            && Resolve(action.Animation!) is var clip)
        {
            _steps.Clear();
            _step = null;
            _steps.Enqueue(new BehaviorStep(clip.Name, clip.Loop ? 1800 : 0));
            NextStep();
        }

        if (!string.IsNullOrWhiteSpace(action.Say))
        {
            ShowBubble(action.Say!);
        }

        var error = _actions.Launch(action);
        if (error is not null)
        {
            ShowBubble($"打不開: {error}");
        }
    }

    private void ShowBubble(string text)
    {
        BubbleText.Text = text;
        Bubble.BeginAnimation(OpacityProperty, null);
        Bubble.Opacity = 1;

        _bubbleTimer.Stop();
        _bubbleTimer.Interval = TimeSpan.FromSeconds(Math.Max(0.6, _config.BubbleSeconds));
        _bubbleTimer.Start();
    }

    private void OnBubbleTimeout(object? sender, EventArgs e)
    {
        _bubbleTimer.Stop();
        Bubble.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(0, TimeSpan.FromMilliseconds(320)) { FillBehavior = FillBehavior.HoldEnd });
    }

    // ------------------------------------------------------------------ tray

    private void SetupTray()
    {
        RebuildTrayMenu();

        _tray = new System.Windows.Forms.NotifyIcon
        {
            Icon = BuildTrayIcon(),
            Text = "CatPet",
            Visible = true,
            ContextMenuStrip = _menu,
        };

        _tray.MouseClick += (_, e) =>
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                Perform(new TriggerAction { Animation = "look_right", Say = "喵～" });
            }
        };
    }

    /// <summary>Rebuilt after a config reload so the lure entries stay in step.</summary>
    private void RebuildTrayMenu()
    {
        var previous = _menu;
        _menu = new System.Windows.Forms.ContextMenuStrip { ShowImageMargin = false };

        _menu.Items.Add("抬頭看看", null, (_, _) =>
            Perform(new TriggerAction { Animation = "look_right", Say = "喵～" }));
        _menu.Items.Add("跑一下", null, (_, _) => Dash());
        _menu.Items.Add("去睡覺", null, (_, _) => Nap());
        _menu.Items.Add("煩人Mode", null, (_, _) => AnnoyingMode());

        foreach (var lure in _config.Lures)
        {
            var label = string.IsNullOrWhiteSpace(lure.Hotkey)
                ? $"放{lure.Name}"
                : $"放{lure.Name}（{lure.Hotkey}）";

            var captured = lure;
            _menu.Items.Add(label, null, (_, _) => StartPicking(captured));
        }

        _menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

        _menu.Items.Add(BuildBehaviorMenu());
        _menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

        _sizeLabel = new System.Windows.Forms.ToolStripMenuItem { Enabled = false };
        _menu.Items.Add(_sizeLabel);
        _menu.Items.Add("放大 5%", null, (_, _) => AdjustScale(ScaleStep));
        _menu.Items.Add("縮小 5%", null, (_, _) => AdjustScale(1 / ScaleStep));
        UpdateSizeLabel();

        // The resize entries are meant to be clicked several times in a row, so
        // they leave the menu open instead of making you re-open it each notch.
        _menu.Closing += (_, e) =>
        {
            if (_keepMenuOpen
                && e.CloseReason == System.Windows.Forms.ToolStripDropDownCloseReason.ItemClicked)
            {
                e.Cancel = true;
            }

            _keepMenuOpen = false;
        };

        _menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

        _pauseItem = new System.Windows.Forms.ToolStripMenuItem("暫停動作") { CheckOnClick = true };
        _pauseItem.CheckedChanged += (_, _) => _paused = _pauseItem!.Checked;
        _menu.Items.Add(_pauseItem);

        _menu.Items.Add("重新載入 config.json", null, (_, _) => ReloadConfig());
        _menu.Items.Add("開啟 config.json", null, (_, _) => OpenConfig());
        _menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        _menu.Items.Add("結束", null, (_, _) => Close());

        if (_tray is not null)
        {
            _tray.ContextMenuStrip = _menu;
        }

        previous?.Dispose();
    }

    private void ShowMenu()
    {
        if (_menu is null)
        {
            return;
        }

        NativeMethods.GetCursorPos(out var cursor);
        _menu.Show(new System.Drawing.Point(cursor.X, cursor.Y));

        // Without this the menu refuses to close when you click elsewhere,
        // because the pet window itself never takes focus (WS_EX_NOACTIVATE).
        NativeMethods.SetForegroundWindow(_menu.Handle);
    }

    private System.Drawing.Icon BuildTrayIcon()
    {
        try
        {
            using var sheet = new System.Drawing.Bitmap(ResolveSheetPath());
            using var target = new System.Drawing.Bitmap(32, 32);

            using (var g = System.Drawing.Graphics.FromImage(target))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(
                    sheet,
                    new System.Drawing.Rectangle(0, 0, 32, 32),
                    new System.Drawing.Rectangle(1, 11, 190, 190),
                    System.Drawing.GraphicsUnit.Pixel);
            }

            _iconHandle = target.GetHicon();
            return System.Drawing.Icon.FromHandle(_iconHandle);
        }
        catch
        {
            return System.Drawing.SystemIcons.Application;
        }
    }

    /// <summary>
    /// Choices offered per habit, as multiples of its built-in weight. Relative
    /// rather than absolute so "普通" means what the habit was tuned for, which
    /// is not the same number for strolling as it is for napping.
    /// </summary>
    private static readonly (string Label, double Factor)[] BehaviorLevels =
    [
        ("關閉", 0),
        ("少一點", 0.4),
        ("普通", 1.0),
        ("多一點", 2.0),
    ];

    private System.Windows.Forms.ToolStripMenuItem BuildBehaviorMenu()
    {
        var root = new System.Windows.Forms.ToolStripMenuItem("動作比例");

        foreach (var habit in PetBrain.Catalog)
        {
            var group = new System.Windows.Forms.ToolStripMenuItem(habit.Label);
            var chosen = NearestFactor(habit);

            foreach (var (label, factor) in BehaviorLevels)
            {
                var entry = new System.Windows.Forms.ToolStripMenuItem(label)
                {
                    Checked = Math.Abs(factor - chosen) < 0.001,
                };

                var capturedHabit = habit;
                var capturedFactor = factor;
                var capturedGroup = group;

                entry.Click += (_, _) =>
                {
                    _keepMenuOpen = true;
                    _config.Behaviors[capturedHabit.Key] = capturedHabit.Default * capturedFactor;
                    SaveBehaviorsToConfig();

                    // Move the tick locally; rebuilding the menu while it is
                    // open would pull it out from under the pointer.
                    foreach (var sibling in capturedGroup.DropDownItems
                                 .OfType<System.Windows.Forms.ToolStripMenuItem>())
                    {
                        sibling.Checked = ReferenceEquals(sibling, entry);
                    }
                };

                group.DropDownItems.Add(entry);
            }

            root.DropDownItems.Add(group);
        }

        root.DropDownItems.Add(new System.Windows.Forms.ToolStripSeparator());
        root.DropDownItems.Add("全部回到預設", null, (_, _) =>
        {
            _config.Behaviors.Clear();
            SaveBehaviorsToConfig();
            RebuildTrayMenu();
            ShowBubble("比例回到預設");
        });

        return root;
    }

    private double CurrentWeight(PetBrain.Habit habit) =>
        _config.Behaviors.TryGetValue(habit.Key, out var weight) ? Math.Max(0, weight) : habit.Default;

    private double NearestFactor(PetBrain.Habit habit)
    {
        var current = CurrentWeight(habit);
        var best = 1.0;
        var bestGap = double.MaxValue;

        foreach (var (_, factor) in BehaviorLevels)
        {
            var gap = Math.Abs(habit.Default * factor - current);
            if (gap < bestGap)
            {
                bestGap = gap;
                best = factor;
            }
        }

        return best;
    }

    /// <summary>
    /// Rewrites just the behaviours block, leaving the comments in config.json
    /// alone. The values are plain numbers, so a brace-free match is safe.
    /// </summary>
    private void SaveBehaviorsToConfig()
    {
        try
        {
            if (!File.Exists(_configPath))
            {
                return;
            }

            var text = File.ReadAllText(_configPath);
            var body = string.Join(", ", PetBrain.Catalog.Select(h =>
                $"\"{h.Key}\": {CurrentWeight(h).ToString("0.##", CultureInfo.InvariantCulture)}"));
            var block = $"\"behaviors\": {{ {body} }}";

            var existing = new Regex("\"behaviors\"\\s*:\\s*\\{[^{}]*\\}", RegexOptions.IgnoreCase);
            string updated;

            if (existing.IsMatch(text))
            {
                updated = existing.Replace(text, block.Replace("$", "$$"), 1);
            }
            else
            {
                var open = text.IndexOf('{');
                updated = open < 0 ? text : text.Insert(open + 1, $"\n  {block},");
            }

            if (updated != text)
            {
                File.WriteAllText(_configPath, updated);
            }
        }
        catch (Exception ex)
        {
            ShowBubble($"比例存不回去: {ex.Message}");
        }
    }

    /// <summary>
    /// Grows or shrinks the cat a notch. The paw line is what the window is
    /// positioned from, so the cat stays welded to the taskbar at any size.
    /// </summary>
    private void AdjustScale(double factor)
    {
        _keepMenuOpen = true;

        var next = Math.Clamp(_config.Scale * factor, MinScale, MaxScale);

        if (Math.Abs(next - _config.Scale) < 0.0005)
        {
            ShowBubble(factor > 1 ? "不能再大了" : "不能再小了");
            return;
        }

        var widthBefore = _spriteWidthDip * _dpi;

        _config.Scale = next;
        ApplySize();

        // Grow about the cat's middle rather than its left edge.
        _x -= (_spriteWidthDip * _dpi - widthBefore) / 2;

        RefreshGround();

        if (_chase is not null)
        {
            AimChase(_chase);
        }

        Reposition(forceTopmost: true);
        UpdateSizeLabel();
        SaveScaleToConfig();
    }

    private void UpdateSizeLabel()
    {
        if (_sizeLabel is not null)
        {
            _sizeLabel.Text = $"目前大小 {_config.Scale * 100:0}%";
        }
    }

    /// <summary>
    /// Writes just the scale back, by patching that one line rather than
    /// re-serialising, so the comments in config.json survive.
    /// </summary>
    private void SaveScaleToConfig()
    {
        try
        {
            if (!File.Exists(_configPath))
            {
                return;
            }

            var text = File.ReadAllText(_configPath);
            var value = _config.Scale.ToString("0.###", CultureInfo.InvariantCulture);

            var updated = Regex.Replace(
                text,
                @"^(\s*""scale""\s*:\s*)[0-9]*\.?[0-9]+",
                "${1}" + value,
                RegexOptions.Multiline | RegexOptions.IgnoreCase);

            if (updated != text)
            {
                File.WriteAllText(_configPath, updated);
            }
        }
        catch (Exception ex)
        {
            ShowBubble($"大小存不回去: {ex.Message}");
        }
    }

    /// <summary>
    /// Walks over to the Start button and bats at it, popping the Start menu
    /// part-way through the swat so the paw and the menu line up.
    /// </summary>
    private async void AnnoyingMode()
    {
        if (!await WalkToStartButton())
        {
            return;
        }

        _onArrive = () =>
        {
            _steps.Clear();
            _step = null;
            _steps.Enqueue(new BehaviorStep(_config.StartSwatAnimation, 0));
            NextStep();
            ShowBubble("理我！");

            // Fire on the frame the paw actually connects, unless config says
            // otherwise (a replacement clip may land on a different frame).
            var contact = _config.StartSwatDelayMs > 0
                ? _config.StartSwatDelayMs
                : ClipLibrary.TapContactMs;

            var delay = TimeSpan.FromMilliseconds(Math.Clamp(contact, 0, 3000));
            _ = Dispatcher.InvokeAsync(async () =>
            {
                await Task.Delay(delay);

                var error = StartMenu.Open(_config.StartSwatUsesWinKey);
                if (error is not null)
                {
                    ShowBubble(error);
                }
            });
        };
    }

    /// <summary>
    /// Finds the Start button and starts the cat walking there. Returns false
    /// if it could not be found, having already said why.
    /// </summary>
    private async Task<bool> WalkToStartButton()
    {
        StartButtonInfo? located;

        try
        {
            located = await StartButton.LocateAsync();
        }
        catch (Exception ex)
        {
            ShowBubble($"找開始鈕出錯: {ex.Message}");
            return false;
        }

        if (located is not { } start)
        {
            ShowBubble("找不到開始鈕（這個殼層我不認得）");
            return false;
        }

        if (!start.OnScreen)
        {
            ShowBubble($"開始鈕在畫面外，工作列自動隱藏？\n{start.Rect.Left},{start.Rect.Top} · {start.Source}");
            return false;
        }

        _chase = null;
        _steps.Clear();
        _step = null;
        _onArrive = null;

        // Stand so the swat's paw comes down on the middle of the button.
        // Lining up the sprite cell instead puts the cat a whole button-width
        // too far right, because the art is inset within its cell and the paw
        // reaches down-left of the cat's centre.
        _walkToX = Math.Clamp(
            start.CentreX - ClipLibrary.TapPawX * _scale * _dpi,
            _ground.Left,
            _ground.Right);

        return true;
    }

    private void Dash()
    {
        _steps.Clear();
        _step = null;

        var middle = (_ground.Left + _ground.Right) / 2.0;
        var direction = _x > middle ? -1 : 1;

        _steps.Enqueue(new BehaviorStep("idle", 2200, _config.RunSpeed, direction, "run"));
        NextStep();
    }

    private void Nap()
    {
        _steps.Clear();
        _step = null;
        _steps.Enqueue(new BehaviorStep("sleep_in", 0));
        _steps.Enqueue(new BehaviorStep("sleep_deep", 9000));
        _steps.Enqueue(new BehaviorStep("sleep_out", 0));
        NextStep();
    }

    private void ReloadConfig()
    {
        var next = PetConfig.Load(_configPath, out var error);
        if (error is not null)
        {
            ShowBubble($"config.json 有問題: {error}");
            return;
        }

        var previous = _config;
        _config = next;

        try
        {
            LoadSheets();
        }
        catch (Exception ex)
        {
            _config = previous;
            ShowBubble(ex.Message);
            return;
        }

        ApplySize();
        _brain = new PetBrain(_config);
        RefreshGround();
        EndChase(resume: false);
        RegisterLureHotkeys();
        RebuildTrayMenu();
        _steps.Clear();
        _step = null;
        NextStep();

        ShowBubble(_hotkeyProblems.Count > 0
            ? string.Join("；", _hotkeyProblems)
            : "設定重新載入囉");
    }

    private void OpenConfig()
    {
        try
        {
            Process.Start(new ProcessStartInfo(_configPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ShowBubble(ex.Message);
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        CompositionTarget.Rendering -= OnRendering;
        _bubbleTimer.Stop();
        _clickTimer.Stop();

        _picker?.CancelFromOutside();
        EndChase(resume: false);

        foreach (var binding in _hotkeys)
        {
            binding.Dispose();
        }

        _hotkeys.Clear();

        if (_tray is not null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }

        _menu?.Dispose();

        if (_iconHandle != IntPtr.Zero)
        {
            NativeMethods.DestroyIcon(_iconHandle);
            _iconHandle = IntPtr.Zero;
        }

        Application.Current.Shutdown();
    }
}
