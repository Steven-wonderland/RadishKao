using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Interop;
using Microsoft.Win32.SafeHandles;

namespace CatPet.Interop;

/// <summary>Turns a PNG into a mouse cursor with the hotspot in the middle.</summary>
internal static class CursorFactory
{
    private sealed class SafeIconHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeIconHandle(IntPtr handle) : base(true) => SetHandle(handle);

        protected override bool ReleaseHandle() => NativeMethods.DestroyIcon(handle);
    }

    /// <summary>
    /// Scales the image to fit a square cursor of <paramref name="sizePx"/> and
    /// puts the hotspot at its centre, which is what you want for a ball or a
    /// tin sitting under the pointer.
    /// </summary>
    public static System.Windows.Input.Cursor Create(string imagePath, int sizePx)
    {
        if (!File.Exists(imagePath))
        {
            throw new FileNotFoundException($"找不到游標圖：{imagePath}", imagePath);
        }

        sizePx = Math.Clamp(sizePx, 16, 128);

        using var source = new Bitmap(imagePath);
        using var square = new Bitmap(sizePx, sizePx, PixelFormat.Format32bppArgb);

        using (var g = Graphics.FromImage(square))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.Clear(Color.Transparent);

            var fit = Math.Min((double)sizePx / source.Width, (double)sizePx / source.Height);
            var w = (int)Math.Round(source.Width * fit);
            var h = (int)Math.Round(source.Height * fit);
            g.DrawImage(source, (sizePx - w) / 2, (sizePx - h) / 2, w, h);
        }

        var hIcon = square.GetHicon();

        try
        {
            if (!NativeMethods.GetIconInfo(hIcon, out var info))
            {
                throw new InvalidOperationException("GetIconInfo failed");
            }

            try
            {
                // Same bitmaps, but flagged as a cursor and given a hotspot.
                info.fIcon = false;
                info.xHotspot = sizePx / 2;
                info.yHotspot = sizePx / 2;

                var hCursor = NativeMethods.CreateIconIndirect(ref info);
                if (hCursor == IntPtr.Zero)
                {
                    throw new InvalidOperationException("CreateIconIndirect failed");
                }

                return CursorInteropHelper.Create(new SafeIconHandle(hCursor));
            }
            finally
            {
                if (info.hbmColor != IntPtr.Zero)
                {
                    NativeMethods.DeleteObject(info.hbmColor);
                }

                if (info.hbmMask != IntPtr.Zero)
                {
                    NativeMethods.DeleteObject(info.hbmMask);
                }
            }
        }
        finally
        {
            NativeMethods.DestroyIcon(hIcon);
        }
    }
}
