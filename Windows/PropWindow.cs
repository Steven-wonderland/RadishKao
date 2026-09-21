using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CatPet.Interop;
using Brushes = System.Windows.Media.Brushes;
using Image = System.Windows.Controls.Image;

namespace CatPet.Windows;

/// <summary>
/// The dropped item itself: a small click-through window that just draws a
/// picture wherever it is told to sit.
/// </summary>
internal sealed class PropWindow : Window
{
    private readonly Image _image;
    private IntPtr _hwnd;

    public PropWindow(BitmapSource picture, double widthDip, double heightDip)
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        Focusable = false;
        IsHitTestVisible = false;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = -8000;
        Top = -8000;
        Width = widthDip;
        Height = heightDip;

        _image = new Image
        {
            Source = picture,
            Stretch = Stretch.Fill,
        };
        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.HighQuality);
        Content = _image;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _hwnd = new WindowInteropHelper(this).Handle;
        var exStyle = NativeMethods.GetWindowLong(_hwnd, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLong(
            _hwnd,
            NativeMethods.GWL_EXSTYLE,
            exStyle | NativeMethods.WS_EX_TOOLWINDOW
                    | NativeMethods.WS_EX_NOACTIVATE
                    | NativeMethods.WS_EX_TRANSPARENT);
    }

    /// <summary>Centres the picture on a point given in physical screen pixels.</summary>
    public void CentreOn(double screenX, double screenY, double dpi)
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        var w = Width * dpi;
        var h = Height * dpi;

        NativeMethods.SetWindowPos(
            _hwnd,
            NativeMethods.HWND_TOPMOST,
            (int)Math.Round(screenX - w / 2),
            (int)Math.Round(screenY - h / 2),
            0, 0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_NOOWNERZORDER);
    }

    /// <summary>Half the drawn height in physical pixels, i.e. centre to base.</summary>
    public double HalfHeightPx(double dpi) => Height * dpi / 2;
}
