using System.Drawing.Imaging;
using static NetSpeedMonitor.OverlayNative;

namespace NetSpeedMonitor;

/// <summary>
/// 32bpp-DIB-Section mit vormultipliziertem Alpha, in die GDI+ direkt zeichnet und die
/// per UpdateLayeredWindow als Fensterinhalt uebergeben wird. Wird nur bei Groessenwechsel
/// neu angelegt - pro Tick entstehen keine neuen Bitmaps/HBITMAPs.
/// </summary>
internal sealed class OverlaySurface : IDisposable
{
    private IntPtr _memDc;
    private IntPtr _hBitmap;
    private IntPtr _oldBitmap;

    public int Width { get; }
    public int Height { get; }
    public Bitmap Bitmap { get; }

    public OverlaySurface(int width, int height)
    {
        Width = Math.Max(1, width);
        Height = Math.Max(1, height);

        var bmi = new BITMAPINFOHEADER
        {
            biSize = System.Runtime.InteropServices.Marshal.SizeOf<BITMAPINFOHEADER>(),
            biWidth = Width,
            biHeight = -Height, // top-down, passt zum Scan0-Layout von GDI+
            biPlanes = 1,
            biBitCount = 32
        };

        var screenDc = GetDC(IntPtr.Zero);
        try
        {
            _memDc = CreateCompatibleDC(screenDc);
            _hBitmap = CreateDIBSection(screenDc, ref bmi, 0, out var bits, IntPtr.Zero, 0);
            if (_memDc == IntPtr.Zero || _hBitmap == IntPtr.Zero)
                throw new InvalidOperationException("DIB-Section fuer das Overlay konnte nicht angelegt werden.");

            _oldBitmap = SelectObject(_memDc, _hBitmap);
            Bitmap = new Bitmap(Width, Height, Width * 4, PixelFormat.Format32bppPArgb, bits);
        }
        finally
        {
            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    public bool Present(IntPtr hwnd, Point location, byte alpha)
    {
        const byte AC_SRC_OVER = 0x00;
        const byte AC_SRC_ALPHA = 0x01;
        const int ULW_ALPHA = 0x02;

        var dst = new POINT(location.X, location.Y);
        var size = new SIZE(Width, Height);
        var src = new POINT(0, 0);
        var blend = new BLENDFUNCTION
        {
            BlendOp = AC_SRC_OVER,
            SourceConstantAlpha = alpha,
            AlphaFormat = AC_SRC_ALPHA
        };
        return UpdateLayeredWindow(hwnd, IntPtr.Zero, ref dst, ref size, _memDc, ref src, 0, ref blend, ULW_ALPHA);
    }

    public void Dispose()
    {
        Bitmap.Dispose();
        if (_memDc != IntPtr.Zero)
        {
            SelectObject(_memDc, _oldBitmap);
            DeleteDC(_memDc);
            _memDc = IntPtr.Zero;
        }
        if (_hBitmap != IntPtr.Zero)
        {
            DeleteObject(_hBitmap);
            _hBitmap = IntPtr.Zero;
        }
    }
}
