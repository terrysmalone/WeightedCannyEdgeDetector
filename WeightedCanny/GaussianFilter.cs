using System;
using System.ComponentModel;
using System.Reflection.Metadata.Ecma335;

namespace WeightedCanny;

internal sealed class GaussianFilter
{
    internal int KernelWidth { get; private set; }
    internal float KernelSigma { get; private set; }

    private int _imageWidth;
    private int _imageHeight;
    
    internal GaussianFilter(int kernelWidth, float kernelSigma)
    {
        KernelWidth = kernelWidth;
        KernelSigma = kernelSigma;      
    }

    internal (float[] kernel, float[] diffKernel) CalculateKernels()
    {
        float[] kernel = new float[KernelWidth];
        float[] diffKernel = new float[KernelWidth];

        for (int i = 0; i < KernelWidth; i++)
        {
            var g1 = Gaussian(i, KernelSigma);

            // We don't care about the edges. Make it smaller
            // for efficiency
            if (g1 <= 0.005 && i >= 2)
            {
                Array.Resize(ref kernel, i - 1);
                Array.Resize(ref diffKernel, i - 1);
                break;
            }

            float g2 = Gaussian(i - 0.5f, KernelSigma);
            float g3 = Gaussian(i + 0.5f, KernelSigma);

            kernel[i] =
                (g1 + g2 + g3)
                / 3f
                / (2f * (float)Math.PI * KernelSigma * KernelSigma);

            diffKernel[i] = g3 - g2;
        }

        return (kernel, diffKernel);
    }

    // Calculate the value at a given point in the gaussian kernel
    private static float Gaussian(float position, float sigma)
    {
        var gaussianValue = (float)Math.Exp(-(position * position) / (2f * sigma * sigma));

        return gaussianValue;
    }    
}
