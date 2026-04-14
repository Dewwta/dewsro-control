using Pfim;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

// DDJ structure:
//   [0..7]   JMXVDDJ  (8 bytes — Joymax magic)
//   [8..11]  version  (4 bytes — e.g. "1000")
//   [12..15] data size (4 bytes, little-endian)
//   [16..19] flags    (4 bytes)
//   [20..]   raw DDS payload

const int DdjHeaderSize = 20;

string iconRoot = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"icons"));

bool forceOverwrite = args.Contains("--force");

if (!Directory.Exists(iconRoot))
{
    Console.Error.WriteLine($"Directory not found: {iconRoot}");
    Console.Error.WriteLine("Usage: DdConverter [path/to/Icon] [--force]");
    return 1;
}

Console.WriteLine($"Icon root : {iconRoot}");
Console.WriteLine($"Overwrite : {(forceOverwrite ? "yes (--force)" : "no — skipping existing PNGs")}");

var files = Directory.GetFiles(iconRoot, "*.ddj", SearchOption.AllDirectories);
Console.WriteLine($"Found     : {files.Length} DDJ files\n");

int converted = 0, skipped = 0, failed = 0;
object _lock = new();

Parallel.ForEach(files,
    new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
    ddjPath =>
    {
        var pngPath = Path.ChangeExtension(ddjPath, ".png");

        if (!forceOverwrite && File.Exists(pngPath))
        {
            Interlocked.Increment(ref skipped);
            return;
        }

        try
        {
            var ddjBytes = File.ReadAllBytes(ddjPath);

            // Validate JMXV magic
            if (ddjBytes.Length < DdjHeaderSize + 4 ||
                ddjBytes[0] != 'J' || ddjBytes[1] != 'M' || ddjBytes[2] != 'X' || ddjBytes[3] != 'V')
            {
                lock (_lock) Console.WriteLine($"  [SKIP] Not a valid DDJ: {Path.GetRelativePath(iconRoot, ddjPath)}");
                Interlocked.Increment(ref failed);
                return;
            }

            // Extract raw DDS (everything after the 20-byte DDJ header)
            using var ddsStream = new MemoryStream(ddjBytes, DdjHeaderSize, ddjBytes.Length - DdjHeaderSize, writable: false);
            using var pfimImage = Pfimage.FromStream(ddsStream);
            pfimImage.Decompress();

            using var image = ToImageSharp(pfimImage);
            image.SaveAsPng(pngPath);

            int n = Interlocked.Increment(ref converted);
            if (n % 200 == 0)
                lock (_lock) Console.WriteLine($"  [{n} converted...]");
        }
        catch (Exception ex)
        {
            lock (_lock) Console.WriteLine($"  [FAIL] {Path.GetRelativePath(iconRoot, ddjPath)}: {ex.Message}");
            Interlocked.Increment(ref failed);
        }
    });

Console.WriteLine($"\nDone.");
Console.WriteLine($"  Converted : {converted}");
Console.WriteLine($"  Skipped   : {skipped}  (PNG already exists)");
Console.WriteLine($"  Failed    : {failed}");
return failed > 0 ? 1 : 0;

// ── Helpers ────────────────────────────────────────────────────────────────────

static Image ToImageSharp(IImage pfimImage)
{
    int w = pfimImage.Width, h = pfimImage.Height, stride = pfimImage.Stride;
    var src = pfimImage.Data;

    switch (pfimImage.Format)
    {
        // ── 32-bit BGRA (most uncompressed RGBA and all BCn decompressed) ───────
        case ImageFormat.Rgba32:
        {
            var pixels = StripStride(src, h, stride, w * 4);
            return Image.LoadPixelData<Bgra32>(pixels, w, h);
        }

        // ── 24-bit BGR (uncompressed RGB, no alpha) ──────────────────────────
        case ImageFormat.Rgb24:
        {
            var pixels = StripStride(src, h, stride, w * 3);
            return Image.LoadPixelData<Bgr24>(pixels, w, h);
        }

        // ── 16-bit ARGB1555 (A=bit15, R=bits14-10, G=bits9-5, B=bits4-0) ────
        case ImageFormat.R5g5b5a1:
        case ImageFormat.Rgba16:
        {
            var pixels = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int s = y * stride + x * 2;
                int d = (y * w + x) * 4;
                ushort px = (ushort)(src[s] | (src[s + 1] << 8));
                pixels[d]     = Scale5(px >> 10);   // R
                pixels[d + 1] = Scale5(px >> 5);    // G
                pixels[d + 2] = Scale5(px);          // B
                pixels[d + 3] = (byte)((px >> 15) != 0 ? 255 : 0); // A
            }
            return Image.LoadPixelData<Rgba32>(pixels, w, h);
        }

        // ── 16-bit RGB555 (no alpha, R=bits14-10, G=bits9-5, B=bits4-0) ────
        case ImageFormat.R5g5b5:
        {
            var pixels = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int s = y * stride + x * 2;
                int d = (y * w + x) * 4;
                ushort px = (ushort)(src[s] | (src[s + 1] << 8));
                pixels[d]     = Scale5(px >> 10);
                pixels[d + 1] = Scale5(px >> 5);
                pixels[d + 2] = Scale5(px);
                pixels[d + 3] = 255;
            }
            return Image.LoadPixelData<Rgba32>(pixels, w, h);
        }

        // ── 16-bit RGB565 (R=bits15-11, G=bits10-5, B=bits4-0) ─────────────
        case ImageFormat.R5g6b5:
        {
            var pixels = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int s = y * stride + x * 2;
                int d = (y * w + x) * 4;
                ushort px = (ushort)(src[s] | (src[s + 1] << 8));
                pixels[d]     = Scale5(px >> 11);
                pixels[d + 1] = (byte)(((px >> 5) & 0x3F) * 255 / 63);
                pixels[d + 2] = Scale5(px);
                pixels[d + 3] = 255;
            }
            return Image.LoadPixelData<Rgba32>(pixels, w, h);
        }

        default:
            throw new NotSupportedException($"Unsupported Pfim format: {pfimImage.Format}");
    }
}

/// <summary>Scale a 5-bit channel value (0-31) to 8-bit (0-255).</summary>
static byte Scale5(int v) => (byte)((v & 0x1F) * 255 / 31);

/// <summary>Copy rows from <paramref name="src"/> removing any stride padding.</summary>
static byte[] StripStride(byte[] src, int h, int stride, int rowBytes)
{
    var dst = new byte[h * rowBytes];
    for (int y = 0; y < h; y++)
        Buffer.BlockCopy(src, y * stride, dst, y * rowBytes, rowBytes);
    return dst;
}
