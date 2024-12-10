using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace WeightedCanny;
public sealed class CannyDetector
{
    public int KernelWidth { get; set; } = 3;
    public float KernelSigma { get; set; } = 1.0f;
    public float LowThreshold { get; set; } = 0.5f;
    public float HighThreshold { get; set; } = 1.0f;

    public float HorizontalWeight { get; set; } = 1.0f;
    public float VerticalWeight { get; set; } = 1.0f;

    public bool WrapHorizontally { get; set; } = true;
    public bool WrapVertically { get; set; } = false;

    private const float MagnitudeScale = 100000F;

    private readonly int _imageSize;
    private readonly int _imageWidth;
    private readonly int _imageHeight;
    private readonly int[] _pixelData;

    private float[] _xConv, _yConv;
    private float[] _xGradient, _yGradient;
    private int[] _magnitude;
    private int[] _thresholded;
    private bool[] _edgeData;

    private float[] _gaussianKernel, _gaussianDiffKernel;
    private int _roundedKernelWidth;

    private int _initX, _maxX, _initY, _maxY;

    private int _index, _indexW, _indexNW, _indexSW, _indexE, _indexNE, _indexSE, _indexN, _indexS;

    private int _stackCounter;

    public CannyDetector(int[] rawIntData, int imageWidth, int imageHeight)
    {
        _imageWidth = imageWidth;
        _imageHeight = imageHeight;
        _imageSize = _imageWidth * _imageHeight;

        _pixelData = new int[_imageSize];
        ConvertDataFromRGB(rawIntData);
        InitialiseArrays();
    }

    private void ConvertDataFromRGB(int[] rawIntData)
    {
        for (int i = 0; i < _imageSize; i++)
        {
            int currentPixel = rawIntData[i];

            int r = (currentPixel & 0xff0000) >> 16;
            int g = (currentPixel & 0xff00) >> 8;
            int b = currentPixel & 0xff;

            _pixelData[i] = CalculateBrightness(r, g, b);
        }
    }

    private int CalculateBrightness(float r, float g, float b)
    {
        int brightness = (int)Math.Floor(0.334f * r + 0.333f * g + 0.333f * b);

        return brightness;
    }

    public bool[] DetectEdges()
    {
        InitialiseArrays();

        CreateGaussianKernels();
        CalculateBorders();
        PerformConvolution();
        CalculateGradients();   //Calculates the gradient values in the image
        PerformSuppression();   //Perform non-maxima suppression

        int low = (int)Math.Floor(LowThreshold * MagnitudeScale);
        int high = (int)Math.Floor(HighThreshold * MagnitudeScale);
        PerformHysteresis(low, high);
        BinariseEdges();

        return _edgeData;
    }

    private void InitialiseArrays()
    {
        _xConv = new float[_imageSize];
        _yConv = new float[_imageSize];
        _xGradient = new float[_imageSize];
        _yGradient = new float[_imageSize];
        _magnitude = new int[_imageSize];
        _thresholded = new int[_imageSize];
        _edgeData = new bool[_imageSize];
    }

    private void CreateGaussianKernels()
    {
        GaussianFilter gaussianFilter = new GaussianFilter(KernelWidth, KernelSigma);
        (_gaussianKernel, _gaussianDiffKernel) = gaussianFilter.CalculateKernels();

        _roundedKernelWidth = _gaussianKernel.Length;
    }

    private void CalculateBorders()
    {
        SetHorizontalBorders();
        SetVerticalBorders();
    }

    private void SetHorizontalBorders()
    {
        _initX = _roundedKernelWidth;
        _maxX = _imageWidth - _roundedKernelWidth;

        if (WrapHorizontally)
        {
            _initX = 0;
            _maxX = _imageWidth - 1;
        }
    }

    private void SetVerticalBorders()
    {
        _initY = _imageWidth * _roundedKernelWidth;
        _maxY = _imageWidth * (_imageHeight - _roundedKernelWidth);

        if (WrapVertically)
        {
            _initY = 0;
            _maxY = _imageWidth * (_imageHeight - 1);
        }
    }

    private void PerformConvolution()
    {
        for (int x = _initX; x <= _maxX; x++)
        {
            for (int y = _initY; y <= _maxY; y += _imageWidth)
            {
                CalculateConvolutionValue(x, y);
            }
        }
    }

    private void CalculateConvolutionValue(int x, int y)
    {
        int index = x + y;
        float sumX = _pixelData[index] * _gaussianKernel[0];
        float sumY = sumX;

        int yOffset = _imageWidth;

        for (int xOffset = 1; xOffset < _roundedKernelWidth; xOffset++)
        {
            //Vertical wrapping
            if (y - yOffset < 0)
                sumY += _gaussianKernel[xOffset] * (_pixelData[index - yOffset + _maxY] + _pixelData[index + yOffset]);
            else if (y + yOffset > _maxY)
                sumY += _gaussianKernel[xOffset] * (_pixelData[index - yOffset] + _pixelData[index + yOffset - _maxY]);
            else
                sumY += _gaussianKernel[xOffset] * (_pixelData[index - yOffset] + _pixelData[index + yOffset]);

            //Horizontal wrapping
            if (x - xOffset < 0)
            {
                sumX += _gaussianKernel[xOffset] * (_pixelData[index - xOffset + _imageWidth] + _pixelData[index + xOffset]);
            }
            else if (x + xOffset >= _imageWidth)
            {
                sumX += _gaussianKernel[xOffset] * (_pixelData[index - xOffset] + _pixelData[index + xOffset - _imageWidth]);
            }
            else
            {
                sumX += _gaussianKernel[xOffset] * (_pixelData[index - xOffset] + _pixelData[index + xOffset]);
            }

            yOffset += _imageWidth;
        }

        _yConv[index] = sumY;
        _xConv[index] = sumX;
    }

    // Calculates the x and y gradients of all points in the image
    private void CalculateGradients()
    {
        for (int x = _initX; x <= _maxX; x++)
        {
            for (int y = _initY; y <= _maxY; y += _imageWidth)
            {
                if (VerticalWeight > 0.0f)
                    CalculateYGradient(x, y);

                if (HorizontalWeight > 0.0f)
                    CalculateXGradient(x, y);
            }
        }
    }

    private void CalculateYGradient(int x, int y)
    {
        float sum = 0.0f;
        int index = x + y;
        int yOffset = _imageWidth;

        for (int i = 1; i < _roundedKernelWidth; i++)
        {
            if (y - yOffset < 0)
                sum += _gaussianDiffKernel[i] * (_xConv[index - yOffset + _maxY] - _xConv[index + yOffset]);
            else if (y + yOffset > _maxY)
                sum += _gaussianDiffKernel[i] * (_xConv[index - yOffset] - _xConv[index + yOffset - _maxY]);
            else
                sum += _gaussianDiffKernel[i] * (_xConv[index - yOffset] - _xConv[index + yOffset]);

            yOffset += _imageWidth;
        }

        _yGradient[index] = sum * VerticalWeight;
    }

    private void CalculateXGradient(int x, int y)
    {
        float sum = 0f;
        int index = x + y;

        for (int i = 1; i < _roundedKernelWidth; i++)
        {
            if (x - i < 0)
            {
                //if (wrapHorizontally)
                sum += _gaussianDiffKernel[i] * (_yConv[index - i + _imageWidth] - _yConv[index + i]);
            }
            else if (x + i >= _imageWidth)
            {
                //if (wrapHorizontally)
                sum += _gaussianDiffKernel[i] * (_yConv[index - i] - _yConv[index + i - _imageWidth]);
            }
            else
            {
                sum += _gaussianDiffKernel[i] * (_yConv[index - i] - _yConv[index + i]);
            }
        }

        _xGradient[index] = sum * HorizontalWeight;
    }

    // Performs non-maximal suppression on the image data.  This
    // gives us the magnitude of all points while thinning the lines
    private void PerformSuppression()
    {
        // TODO: Look into setting init and max x when edge wrapping is not set

        for (int x = _initX; x <= _maxX; x++)
        {
            for (int y = _initY; y <= _maxY; y += _imageWidth)
            {
                CalculateMagnitude(x, y);
            }
        }
    }

    private void CalculateMagnitude(int x, int y)
    {
        UpdateIndexValues(x, y);

        float xGrad = _xGradient[_index];
        float yGrad = _yGradient[_index];

        float gradMag = Hypot(xGrad, yGrad);

        float nMag = Hypot(_xGradient[_indexN], _yGradient[_indexN]);
        float sMag = Hypot(_xGradient[_indexS], _yGradient[_indexS]);
        float wMag = Hypot(_xGradient[_indexW], _yGradient[_indexW]);
        float eMag = Hypot(_xGradient[_indexE], _yGradient[_indexE]);
        float neMag = Hypot(_xGradient[_indexNE], _yGradient[_indexNE]);
        float seMag = Hypot(_xGradient[_indexSE], _yGradient[_indexSE]);
        float swMag = Hypot(_xGradient[_indexSW], _yGradient[_indexSW]);
        float nwMag = Hypot(_xGradient[_indexNW], _yGradient[_indexNW]);
        float tmp;

        if (xGrad * yGrad <= (float)0)
        {
            if (Math.Abs(xGrad) >= Math.Abs(yGrad))
            {
                if ((tmp = Math.Abs(xGrad * gradMag)) >= Math.Abs(yGrad * neMag - (xGrad + yGrad) * eMag) && tmp > Math.Abs(yGrad * swMag - (xGrad + yGrad) * wMag))
                    _magnitude[_index] = (int)(MagnitudeScale * gradMag);
                else
                    _magnitude[_index] = 0;
            }
            else
            {
                if ((tmp = Math.Abs(yGrad * gradMag)) >= Math.Abs(xGrad * neMag - (yGrad + xGrad) * nMag) && tmp > Math.Abs(xGrad * swMag - (yGrad + xGrad) * sMag))
                    _magnitude[_index] = (int)(MagnitudeScale * gradMag);
                else
                    _magnitude[_index] = 0;
            }
        }
        else
        {
            if (Math.Abs(xGrad) >= Math.Abs(yGrad))
            {
                if ((tmp = Math.Abs(xGrad * gradMag)) >= Math.Abs(yGrad * seMag + (xGrad - yGrad) * eMag) && tmp > Math.Abs(yGrad * nwMag + (xGrad - yGrad) * wMag))
                    _magnitude[_index] = (int)(MagnitudeScale * gradMag);
                else
                    _magnitude[_index] = 0;
            }
            else
            {
                if ((tmp = Math.Abs(yGrad * gradMag)) >= Math.Abs(xGrad * seMag + (yGrad - xGrad) * sMag) && tmp > Math.Abs(xGrad * nwMag + (yGrad - xGrad) * nMag))
                    _magnitude[_index] = (int)(MagnitudeScale * gradMag);
                else
                    _magnitude[_index] = 0;
            }
        }
    }

    private void UpdateIndexValues(int x, int y)
    {
        _index = x + y;

        int xWest, xEast;
        int yNorth, ySouth;

        if (x - 1 < 0)
        {
            xWest = _imageWidth - 1;
            xEast = x + 1;
        }
        else if (x + 1 >= _imageWidth)
        {
            xWest = x - 1;
            xEast = 0;
        }
        else
        {
            xWest = x - 1;
            xEast = x + 1;
        }


        if (y - _imageWidth < 0)
        {
            yNorth = _maxY;
            ySouth = y + _imageWidth;
        }
        else if (y + _imageWidth > _maxY)
        {
            yNorth = y - _imageWidth;
            ySouth = _initY;
        }
        else
        {
            yNorth = y - _imageWidth;
            ySouth = y + _imageWidth;
        }

        _indexN = x + yNorth;
        _indexNE = yNorth + xEast;
        _indexE = y + xEast;
        _indexSE = ySouth + xEast;
        _indexS = x + ySouth;
        _indexSW = ySouth + xWest;
        _indexW = xWest + y;
        _indexNW = yNorth + xWest;
    }

    // Method which returns the hypotenuse of two given parameters
    private static float Hypot(float x, float y)
    {
        float hypotenuse = (float)Math.Sqrt(Math.Pow(x, 2) + Math.Pow(y, 2));

        return hypotenuse;
    }

    // Performs hysteresis on the pixelData
    private void PerformHysteresis(int low, int high)
    {
        int offset = 0;

        for (int y = 0; y < _imageHeight; y++)
        {
            for (int x = 0; x < _imageWidth; x++)
            {
                // If the magnitude is more than or equal to the high threshold follow the edge
                if (_thresholded[offset] == 0 && _magnitude[offset] >= high)
                {
                    _stackCounter = 0;

                    Follow(x, y, offset, low);
                }

                offset++;
            }
        }
    }


    // Connects points from a high threshold point until there are
    // no connecting point above low threshold
    private void Follow(int xCurrent, int yCurrent, int indexValue, int threshold)
    {
        _stackCounter++;

        // Safety net to make sure we don't keep wrapping over the edges
        if (_stackCounter > _imageWidth)
            return;

        int xPrevious, xNext;
        int yPrevious, yNext;

        if (xCurrent == 0)
            xPrevious = xCurrent;
        else
            xPrevious = xCurrent - 1;

        if (xCurrent == _imageWidth - 1)
            xNext = xCurrent;
        else
            xNext = xCurrent + 1;

        if (yCurrent == 0)
            yPrevious = yCurrent;
        else
            yPrevious = yCurrent - 1;

        if (yCurrent == _imageHeight - 1)
            yNext = yCurrent;
        else
            yNext = yCurrent + 1;

        _thresholded[indexValue] = _magnitude[indexValue];

        for (int x = xPrevious; x <= xNext; x++)
        {
            for (int y = yPrevious; y <= yNext; y++)
            {
                int i2 = x + y * _imageWidth;

                if ((y != yCurrent || x != xCurrent) && _thresholded[i2] == 0 && _magnitude[i2] >= threshold)
                {
                    Follow(x, y, i2, threshold);

                    return;
                }
            }
        }
    }

    // Thresholds the edge points so that they appear as either
    // black or white
    private void BinariseEdges()
    {
        for (int i = _imageWidth; i < _imageSize - _imageWidth; i++)
        {
            if (_thresholded[i] > 0)
                _edgeData[i] = true;
            else
                _edgeData[i] = false;
        }
    }
}

