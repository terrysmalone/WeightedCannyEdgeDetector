using System.Drawing;

namespace WeightedCanny;
internal sealed class ImageConverter
{
    private int _imageWidth, _imageHeight;

    public ImageConverter(int imageWidth, int imageHeight)
    {
        _imageWidth = imageWidth;
        _imageHeight = imageHeight;
    }

    internal Bitmap GetEdgesImage(bool[] edgeData)
    {
        // TODO: Don't use System.Drawing.Bitmap. It only works on Windows
        Bitmap drawnImage = new Bitmap(_imageWidth, _imageHeight);
        for (int width = 0; width < _imageWidth; width++)
        {
            for (int height = 0; height < _imageHeight; height++)
            {
                if (edgeData[width + (height * _imageWidth)] == true)
                {
                    drawnImage.SetPixel(width, height, Color.White);
                }
            }
        }

        return drawnImage;
    }
}
