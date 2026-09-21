using System.IO;
using System.Windows.Media.Imaging;

namespace CatPet.Sprites;

/// <summary>
/// A uniform grid of frames cut out of one PNG. Frames are cropped lazily and
/// frozen so they can be handed straight to the UI thread every tick, and a
/// copy of the alpha channel is kept around for per-pixel hit testing.
/// </summary>
public sealed class SpriteSheet
{
    private readonly BitmapSource _source;
    private readonly BitmapSource?[] _frames;
    private readonly byte[] _alpha;
    private readonly int _pixelWidth;

    public int Columns { get; }
    public int Rows { get; }
    public int CellWidth { get; }
    public int CellHeight { get; }

    public SpriteSheet(string path, int columns, int rows)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"找不到 sprite sheet：{path}", path);
        }

        var loaded = new BitmapImage();
        loaded.BeginInit();
        loaded.UriSource = new Uri(Path.GetFullPath(path));
        loaded.CacheOption = BitmapCacheOption.OnLoad;
        loaded.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
        loaded.EndInit();
        loaded.Freeze();

        _source = loaded.Format == System.Windows.Media.PixelFormats.Bgra32
            ? loaded
            : Convert(loaded);

        Columns = columns;
        Rows = rows;
        _pixelWidth = _source.PixelWidth;
        CellWidth = _source.PixelWidth / columns;
        CellHeight = _source.PixelHeight / rows;
        _frames = new BitmapSource?[columns * rows];
        _alpha = ExtractAlpha(_source);
    }

    private static BitmapSource Convert(BitmapSource source)
    {
        var converted = new FormatConvertedBitmap(source, System.Windows.Media.PixelFormats.Bgra32, null, 0);
        converted.Freeze();
        return converted;
    }

    private static byte[] ExtractAlpha(BitmapSource source)
    {
        var stride = source.PixelWidth * 4;
        var bgra = new byte[stride * source.PixelHeight];
        source.CopyPixels(bgra, stride, 0);

        var alpha = new byte[source.PixelWidth * source.PixelHeight];
        for (var i = 0; i < alpha.Length; i++)
        {
            alpha[i] = bgra[i * 4 + 3];
        }

        return alpha;
    }

    /// <summary>Frame at (row, column), cropped on first use and cached.</summary>
    public BitmapSource Frame(int row, int column)
    {
        var index = row * Columns + column;
        var cached = _frames[index];
        if (cached is not null)
        {
            return cached;
        }

        var cropped = new CroppedBitmap(
            _source,
            new System.Windows.Int32Rect(column * CellWidth, row * CellHeight, CellWidth, CellHeight));
        cropped.Freeze();
        _frames[index] = cropped;
        return cropped;
    }

    /// <summary>Alpha of one pixel inside a frame; (x, y) are cell-local.</summary>
    public byte AlphaAt(int row, int column, int x, int y)
    {
        if (x < 0 || y < 0 || x >= CellWidth || y >= CellHeight)
        {
            return 0;
        }

        var sheetX = column * CellWidth + x;
        var sheetY = row * CellHeight + y;
        return _alpha[sheetY * _pixelWidth + sheetX];
    }
}
