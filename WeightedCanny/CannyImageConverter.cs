using System.Drawing;

namespace WeightedCanny;
internal sealed class CannyImageConverter
{
    internal (byte[], int, int) GetByteDataFromImageFile(string filePath)
    {
        Image img = Image.FromFile(filePath);
        byte[] bytes = (byte[])(new ImageConverter()).ConvertTo(img, typeof(byte[]));

        if (bytes == null)
        {
            throw new ArgumentNullException($"Byte data from image {filePath} is null");
        }

        return (bytes, img.Width, img.Height);
    }

    internal Bitmap GetEdgesImage(bool[] edgeData, int imageWidth, int imageHeight)
    {
        // TODO: Don't use System.Drawing.Bitmap. It only works on Windows
        Bitmap drawnImage = new Bitmap(imageWidth, imageHeight);
        for (int width = 0; width < imageWidth; width++)
        {
            for (int height = 0; height < imageHeight; height++)
            {
                if (edgeData[width + (height * imageWidth)] == true)
                {
                    drawnImage.SetPixel(width, height, Color.White);
                }
            }
        }

        return drawnImage;
    }
}
