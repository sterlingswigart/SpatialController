/**
 * This class is borrowed from the TrackingNI project by
 * Richard Pianka and Abouza.
 **/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media.Imaging;

namespace SpatialController
{
    public class DepthCorrection
    {
        public unsafe static void Fix(ref WriteableBitmap image, int XRes, int YRes)
        {
            image.Lock();

            for (int y = 0; y < YRes; y++)
            {
                for (int x = 0; x < XRes; x++)
                {
                    byte* pixel = GetPixel(image, x, y);
                    if (pixel[0] == 0)
                    {
                        FixPixel(image, x, y, XRes, YRes, ref x);
                    }
                }
            }

            image.Unlock();
        }

        public unsafe static byte FixPixel(WriteableBitmap image, int x, int y, int XRes, int YRes, ref int counter)
        {
            List<byte> surrounding = new List<byte>();
            byte* pixel = GetPixel(image, x, y);
            byte lowest = 255;

            if (x > 0 && x < XRes - 1 && y > 0 && y < YRes - 1)
            {
                surrounding.Add(GetPixel(image, x - 1, y - 1)[0]);
                surrounding.Add(GetPixel(image, x, y - 1)[0]);
                surrounding.Add(GetPixel(image, x + 1, y - 1)[0]);

                surrounding.Add(GetPixel(image, x - 1, y + 1)[0]);
                surrounding.Add(GetPixel(image, x, y + 1)[0]);
                surrounding.Add(GetPixel(image, x + 1, y + 1)[0]);

                //surrounding.Add(GetPixel(image, x - 1, y)[0]);
                surrounding.Add(GetPixel(image, x + 1, y)[0]);
            }

            foreach (byte b in surrounding)
            {
                if (b < lowest && b != 0)
                {
                    lowest = b;
                }
            }

            if (x < XRes - 1)
            {
                byte* next = GetPixel(image, x + 1, y);
                if (next[0] == 0 && counter % 10 != 0)
                {
                    byte low = FixPixel(image, x + 1, y, XRes, YRes, ref counter);
                    if (low < lowest)
                    {
                        lowest = low;
                    }
                    counter++;
                }
            }

            pixel[0] = lowest;
            pixel[1] = lowest;
            pixel[2] = lowest;

            return lowest == 255 ? (byte)0 : lowest;
        }

        public unsafe static byte* GetPixel(WriteableBitmap image, int x, int y)
        {
            byte* data = (byte*)image.BackBuffer.ToPointer() + y * image.BackBufferStride;
            data += x * 3;

            return data;
        }
    }
}
