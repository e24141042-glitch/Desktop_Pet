using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DesktopPet
{
    public static class CollisionHelper
    {
        public static bool CheckPixelCollision(MainWindow petA, MainWindow petB)
        {
            var imageA = petA.GetPetImage()?.Source as BitmapSource;
            var imageB = petB.GetPetImage()?.Source as BitmapSource;

            if (imageA == null || imageB == null || petA.ActualWidth < 1 || petA.ActualHeight < 1 || petB.ActualWidth < 1 || petB.ActualHeight < 1)
            {
                return false;
            }

            Rect rectA = new Rect(petA.Left, petA.Top, petA.ActualWidth, petA.ActualHeight);
            Rect rectB = new Rect(petB.Left, petB.Top, petB.ActualWidth, petB.ActualHeight);

            if (!rectA.IntersectsWith(rectB))
            {
                return false;
            }
            
            Rect intersection = Rect.Intersect(rectA, rectB);
            if (intersection.IsEmpty) return false;

            // Get transform information
            var transformA = petA.GetPetImage().RenderTransform as ScaleTransform;
            bool isFlippedA = transformA != null && transformA.ScaleX < 0;
            var transformB = petB.GetPetImage().RenderTransform as ScaleTransform;
            bool isFlippedB = transformB != null && transformB.ScaleX < 0;

            // Get the entire image pixel data for both pets
            byte[] pixelsA;
            int fullWidthA, fullHeightA;
            if (!TryGetPixelData(imageA, new Int32Rect(0, 0, imageA.PixelWidth, imageA.PixelHeight), out pixelsA, out fullWidthA, out fullHeightA)) return false;

            byte[] pixelsB;
            int fullWidthB, fullHeightB;
            if (!TryGetPixelData(imageB, new Int32Rect(0, 0, imageB.PixelWidth, imageB.PixelHeight), out pixelsB, out fullWidthB, out fullHeightB)) return false;
            
            int strideA = fullWidthA * 4;
            int strideB = fullWidthB * 4;

            // Iterate over the screen-space intersection pixels
            for (int y = (int)intersection.Y; y < (int)intersection.Bottom; y++)
            {
                for (int x = (int)intersection.X; x < (int)intersection.Right; x++)
                {
                    // Map screen (x,y) to petA's source image pixels
                    int petAX = (int)((x - rectA.X) * ((double)fullWidthA / rectA.Width));
                    int petAY = (int)((y - rectA.Y) * ((double)fullHeightA / rectA.Height));

                    if (isFlippedA) petAX = fullWidthA - 1 - petAX;

                    // Map screen (x,y) to petB's source image pixels
                    int petBX = (int)((x - rectB.X) * ((double)fullWidthB / rectB.Width));
                    int petBY = (int)((y - rectB.Y) * ((double)fullHeightB / rectB.Height));

                    if (isFlippedB) petBX = fullWidthB - 1 - petBX;

                    // Check bounds for mapped pixels (should not be out of bounds if rects are correct)
                    if (petAX < 0 || petAX >= fullWidthA || petAY < 0 || petAY >= fullHeightA ||
                        petBX < 0 || petBX >= fullWidthB || petBY < 0 || petBY >= fullHeightB)
                    {
                        continue; 
                    }

                    int indexA = petAY * strideA + petAX * 4;
                    int indexB = petBY * strideB + petBX * 4;

                    if (pixelsA[indexA + 3] > 0 && pixelsB[indexB + 3] > 0)
                    {
                        return true; // Collision
                    }
                }
            }

            return false;
        }

        private static bool TryGetPixelData(BitmapSource source, Int32Rect rectToCopy, out byte[] pixels, out int actualWidth, out int actualHeight)
        {
            // Clamp rectToCopy to be fully within source bounds
            int x = Math.Max(0, rectToCopy.X);
            int y = Math.Max(0, rectToCopy.Y);
            actualWidth = Math.Min(source.PixelWidth - x, rectToCopy.Width);
            actualHeight = Math.Min(source.PixelHeight - y, rectToCopy.Height);

            pixels = null;
            if (actualWidth <= 0 || actualHeight <= 0) return false;

            // Adjust rectToCopy to the actual clamped values
            rectToCopy = new Int32Rect(x, y, actualWidth, actualHeight);
            int stride = rectToCopy.Width * 4; // 4 bytes per pixel for Bgra32
            pixels = new byte[rectToCopy.Height * stride];

            try
            {
                source.CopyPixels(rectToCopy, pixels, stride, 0);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error copying pixels: {ex.Message}");
                return false;
            }
            
            return true;
        }

    }
}
