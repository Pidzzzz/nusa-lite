using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NusaLite.Infrastructure;

public static class ImageDecoder
{
    private const int MaxDecodeWidth = 512;

    public static ImageSource? FromBytes(byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            return null;
        }

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
            bitmap.DecodePixelWidth = MaxDecodeWidth;
            bitmap.StreamSource = new MemoryStream(bytes);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception ex)
        {
            Log.Write("Image could not be decoded.", ex);
            return null;
        }
    }
}
