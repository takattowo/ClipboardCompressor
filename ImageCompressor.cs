using System.Drawing.Imaging;

namespace ClipboardCompressor;

public record CompressionResult(Bitmap Compressed, byte[] PngBytes, long OriginalBytes, long CompressedBytes);

public static class ImageCompressor
{
    // ── Public entry point ────────────────────────────────────────────────────

    public static CompressionResult Compress(Image source, CompressionFormat format,
        int jpegQuality, bool preserveTransparency)
    {
        bool hasAlpha = preserveTransparency && HasAlphaChannel(source);
        var bmp = RenderTo32bpp(source, hasAlpha);

        byte[] original   = SavePng(bmp);
        byte[] compressed = format == CompressionFormat.Jpeg
            ? CompressJpeg(bmp, jpegQuality)
            : PaletteQuantize(bmp, hasAlpha);

        if (compressed.Length >= original.Length) compressed = original;

        // Decode final bytes back to Bitmap using pixel-perfect copy (no DrawImage resampling)
        Bitmap result;
        using (var ms = new MemoryStream(compressed))
        using (var decoded = (Bitmap)Image.FromStream(ms))
            result = DecodeToBitmap(decoded);

        bmp.Dispose();
        return new CompressionResult(result, compressed, original.Length, compressed.Length);
    }

    // ── JPEG path ─────────────────────────────────────────────────────────────

    private static byte[] CompressJpeg(Bitmap source, int quality)
    {
        // Encode JPEG, decode, then palette-quantize for maximum PNG compression.
        // JPEG removes high-frequency noise so the quantizer maps to fewer colors.
        byte[] jpeg = ToJpegBytes(source, quality);
        Bitmap decoded;
        using (var ms = new MemoryStream(jpeg))
        using (var tmp = (Bitmap)Image.FromStream(ms))
            decoded = RenderTo32bpp(tmp, hasAlpha: false);

        byte[] result = PaletteQuantize(decoded, hasAlpha: false);
        decoded.Dispose();
        return result;
    }

    // ── PNG palette quantization ───────────────────────────────────────────────
    // Reduces image to 256-colour palette (median-cut) and saves as indexed PNG.
    // Produces 60-80% smaller files than 32-bit PNG with near-zero visible loss.

    private static unsafe byte[] PaletteQuantize(Bitmap source, bool hasAlpha)
    {
        int width = source.Width, height = source.Height, total = width * height;
        var rect = new Rectangle(0, 0, width, height);

        // Read all pixels into flat int[] (ARGB packing from GDI+)
        var pixels = new int[total];
        var srcData = source.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        fixed (int* pDst = pixels)
        {
            byte* src = (byte*)srcData.Scan0;
            for (int y = 0; y < height; y++)
                Buffer.MemoryCopy(src + y * srcData.Stride,
                    (byte*)(pDst + y * width), width * 4, width * 4);
        }
        source.UnlockBits(srcData);

        // Sample up to 100 K pixels for palette building
        int step = Math.Max(1, total / 100_000);
        var samples = new int[(total + step - 1) / step];
        for (int i = 0, j = 0; i < total; i += step, j++) samples[j] = pixels[i];

        // Build 256-colour palette via median-cut
        Color[] palette = MedianCut(samples, 256);

        // Build 6-bit-per-channel LUT (64³ = 262 144 entries) for fast mapping
        byte[] lut = BuildLut(palette);

        // Map every pixel to its nearest palette index via LUT
        var indices = new byte[total];
        for (int i = 0; i < total; i++)
        {
            int p = pixels[i];
            // Extract 6 MSBs of each channel and combine into LUT address
            int idx = ((p >> 18) & 0x3F) << 12 | ((p >> 10) & 0x3F) << 6 | ((p >> 2) & 0x3F);
            indices[i] = lut[idx];
        }

        // Create 8bpp indexed Bitmap and fill with palette + indices
        var indexed = new Bitmap(width, height, PixelFormat.Format8bppIndexed);
        var cp = indexed.Palette;
        for (int i = 0; i < palette.Length; i++) cp.Entries[i] = palette[i];
        indexed.Palette = cp;

        var dstData = indexed.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);
        fixed (byte* pIdx = indices)
        {
            byte* dst = (byte*)dstData.Scan0;
            for (int y = 0; y < height; y++)
                Buffer.MemoryCopy(pIdx + y * width, dst + y * dstData.Stride, width, width);
        }
        indexed.UnlockBits(dstData);

        byte[] result = SavePng(indexed);
        indexed.Dispose();
        return result;
    }

    // ── Median-cut colour quantizer ───────────────────────────────────────────

    private static Color[] MedianCut(int[] samples, int maxColors)
    {
        // Operate on index ranges into samples[] to avoid copying
        var buckets = new List<(int start, int end)> { (0, samples.Length) };

        while (buckets.Count < maxColors)
        {
            int bi = -1, maxRange = 0;
            for (int i = 0; i < buckets.Count; i++)
            {
                var (s, e) = buckets[i];
                if (e - s <= 1) continue;
                int rng = ColorRange(samples, s, e);
                if (rng > maxRange) { maxRange = rng; bi = i; }
            }
            if (bi < 0) break;

            var (start, end) = buckets[bi];
            int mid = SortSplitMid(samples, start, end);
            buckets.RemoveAt(bi);
            buckets.Add((start, mid));
            buckets.Add((mid, end));
        }

        return buckets.Select(b => AverageColor(samples, b.start, b.end)).ToArray();
    }

    private static int ColorRange(int[] p, int s, int e)
    {
        byte minR=255,maxR=0,minG=255,maxG=0,minB=255,maxB=0;
        for (int i = s; i < e; i++)
        {
            byte r=(byte)((p[i]>>16)&0xFF), g=(byte)((p[i]>>8)&0xFF), b=(byte)(p[i]&0xFF);
            if(r<minR)minR=r; if(r>maxR)maxR=r;
            if(g<minG)minG=g; if(g>maxG)maxG=g;
            if(b<minB)minB=b; if(b>maxB)maxB=b;
        }
        return Math.Max(maxR-minR, Math.Max(maxG-minG, maxB-minB));
    }

    private static int SortSplitMid(int[] p, int start, int end)
    {
        byte minR=255,maxR=0,minG=255,maxG=0,minB=255,maxB=0;
        for (int i = start; i < end; i++)
        {
            byte r=(byte)((p[i]>>16)&0xFF), g=(byte)((p[i]>>8)&0xFF), b=(byte)(p[i]&0xFF);
            if(r<minR)minR=r; if(r>maxR)maxR=r;
            if(g<minG)minG=g; if(g>maxG)maxG=g;
            if(b<minB)minB=b; if(b>maxB)maxB=b;
        }
        int rr=maxR-minR, gr=maxG-minG, br2=maxB-minB;
        if (rr>=gr && rr>=br2)
            Array.Sort(p, start, end-start, Comparer<int>.Create((a,b2)=>((a>>16)&0xFF)-((b2>>16)&0xFF)));
        else if (gr>=br2)
            Array.Sort(p, start, end-start, Comparer<int>.Create((a,b2)=>((a>>8)&0xFF)-((b2>>8)&0xFF)));
        else
            Array.Sort(p, start, end-start, Comparer<int>.Create((a,b2) => (a&0xFF)-(b2&0xFF)));
        return start + (end-start)/2;
    }

    private static Color AverageColor(int[] p, int start, int end)
    {
        long r=0, g=0, b=0;
        for (int i = start; i < end; i++) { r+=(p[i]>>16)&0xFF; g+=(p[i]>>8)&0xFF; b+=p[i]&0xFF; }
        int n = end-start;
        return Color.FromArgb((int)(r/n), (int)(g/n), (int)(b/n));
    }

    // 6-bit per channel LUT: 64*64*64 = 262144 entries, maps quantised RGB → palette index
    private static byte[] BuildLut(Color[] palette)
    {
        var lut = new byte[64 * 64 * 64];
        for (int r = 0; r < 64; r++)
        for (int g = 0; g < 64; g++)
        for (int b = 0; b < 64; b++)
        {
            int r8 = r * 255 / 63, g8 = g * 255 / 63, b8 = b * 255 / 63;
            int best = 0, bestDist = int.MaxValue;
            for (int i = 0; i < palette.Length; i++)
            {
                int dr = r8-palette[i].R, dg = g8-palette[i].G, db = b8-palette[i].B;
                int dist = dr*dr + dg*dg + db*db;
                if (dist < bestDist) { bestDist = dist; best = i; }
            }
            lut[(r<<12)|(g<<6)|b] = (byte)best;
        }
        return lut;
    }

    // ── Shared helpers ────────────────────────────────────────────────────────

    private static byte[] ToJpegBytes(Bitmap source, int quality)
    {
        var codec = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
        using var ep = new EncoderParameters(1);
        ep.Param[0] = new EncoderParameter(Encoder.Quality, (long)quality);
        using var ms = new MemoryStream();
        source.Save(ms, codec, ep);
        return ms.ToArray();
    }

    private static Bitmap RenderTo32bpp(Image source, bool hasAlpha)
    {
        var bmp = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode   = System.Drawing.Drawing2D.PixelOffsetMode.Half;
        g.CompositingMode   = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
        if (!hasAlpha) g.Clear(Color.White);
        g.DrawImage(source, 0, 0, source.Width, source.Height);
        return bmp;
    }

    private static unsafe Bitmap DecodeToBitmap(Bitmap decoded)
    {
        // Pixel-accurate LockBits copy — no DrawImage to avoid resampling
        var dst  = new Bitmap(decoded.Width, decoded.Height, PixelFormat.Format32bppArgb);
        var rect = new Rectangle(0, 0, decoded.Width, decoded.Height);
        var srcD = decoded.LockBits(rect, ImageLockMode.ReadOnly,  PixelFormat.Format32bppArgb);
        var dstD = dst.LockBits(rect,    ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            int rowBytes = decoded.Width * 4;
            byte* s = (byte*)srcD.Scan0, d = (byte*)dstD.Scan0;
            for (int y = 0; y < decoded.Height; y++)
                Buffer.MemoryCopy(s + y * srcD.Stride, d + y * dstD.Stride, rowBytes, rowBytes);
        }
        finally { decoded.UnlockBits(srcD); dst.UnlockBits(dstD); }
        return dst;
    }

    private static byte[] SavePng(Image img)
    {
        using var ms = new MemoryStream();
        img.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    private static bool HasAlphaChannel(Image image)
        => Image.IsAlphaPixelFormat(image.PixelFormat);

    public static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }
}
