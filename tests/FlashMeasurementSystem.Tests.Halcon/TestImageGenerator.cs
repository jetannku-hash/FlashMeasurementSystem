using System;
using HalconDotNet;

namespace FlashMeasurementSystem.Tests.Halcon
{
    /// <summary>
    /// In-memory test image generation using HOperatorSet.
    /// paint_region returns a NEW image (HALCON procedural semantics) — always capture the result.
    /// </summary>
    public static class TestImageGenerator
    {
        public const int DefaultSize = 256;

        private static HImage PaintRect(HImage src, double r1, double c1, double r2, double c2, double gray)
        {
            HRegion r = new HRegion();
            r.GenRectangle1(r1, c1, r2, c2);
            HImage result = src.PaintRegion(r, gray, "fill");
            r.Dispose();
            src.Dispose();
            return result;
        }

        /// <summary>Left half dark (30), right half bright (200) — clear vertical edge at col=128.</summary>
        public static HImage CreateEdgeImage(int w = DefaultSize, int h = DefaultSize)
        {
            HImage img = new HImage();
            img.GenImageConst("byte", w, h);
            img = PaintRect(img, 0, 0, h - 1, w - 1, 30.0);
            img = PaintRect(img, 0.0, w / 2.0, h - 1.0, w - 1.0, 200.0);
            return img;
        }

        /// <summary>Uniform image at a given gray value.</summary>
        public static HImage CreateUniform(int w, int h, double gray)
        {
            HImage img = new HImage();
            img.GenImageConst("byte", w, h);
            img = PaintRect(img, 0, 0, h - 1, w - 1, gray);
            return img;
        }

        /// <summary>White filled circle on dark background.</summary>
        public static HImage CreateCircleImage(int w, int h, double cRow, double cCol, double radius)
        {
            HImage img = new HImage();
            img.GenImageConst("byte", w, h);
            img = PaintRect(img, 0, 0, h - 1, w - 1, 30.0);
            HRegion circle = new HRegion();
            circle.GenCircle(cRow, cCol, radius);
            HImage result = img.PaintRegion(circle, 220.0, "fill");
            circle.Dispose();
            img.Dispose();
            return result;
        }

        /// <summary>White filled ellipse on dark background (R1 along major axis, Phi rad).</summary>
        public static HImage CreateEllipseImage(int w, int h, double cRow, double cCol, double phi, double r1, double r2)
        {
            HImage img = new HImage();
            img.GenImageConst("byte", w, h);
            img = PaintRect(img, 0, 0, h - 1, w - 1, 30.0);
            HRegion ellipse = new HRegion();
            ellipse.GenEllipse(cRow, cCol, phi, r1, r2);
            HImage result = img.PaintRegion(ellipse, 220.0, "fill");
            ellipse.Dispose();
            img.Dispose();
            return result;
        }

        /// <summary>White filled oriented rectangle on dark background (Length1/2 = half edges, Phi rad).</summary>
        public static HImage CreateRectangleImage(int w, int h, double cRow, double cCol, double phi, double l1, double l2)
        {
            HImage img = new HImage();
            img.GenImageConst("byte", w, h);
            img = PaintRect(img, 0, 0, h - 1, w - 1, 30.0);
            HRegion rect = new HRegion();
            rect.GenRectangle2(cRow, cCol, phi, l1, l2);
            HImage result = img.PaintRegion(rect, 220.0, "fill");
            rect.Dispose();
            img.Dispose();
            return result;
        }

        // 多特徵合成圖（MET2D-04）已知幾何：圓 + 橢圓 + 水平線，互不重疊（256x256）。
        public const double CompCircleRow = 70, CompCircleCol = 70, CompCircleRadius = 35;
        public const double CompEllipseRow = 70, CompEllipseCol = 190, CompEllipsePhi = 0, CompEllipseR1 = 40, CompEllipseR2 = 22;
        public const double CompLineRow = 200, CompLineColBegin = 40, CompLineColEnd = 216;

        /// <summary>Composite image: filled circle + filled ellipse + thin horizontal line (for one-Apply multi-feature).</summary>
        public static HImage CreateCompositeImage(int w = DefaultSize, int h = DefaultSize)
        {
            HImage img = new HImage();
            img.GenImageConst("byte", w, h);
            img = PaintRect(img, 0, 0, h - 1, w - 1, 30.0);

            HRegion circle = new HRegion();
            circle.GenCircle(CompCircleRow, CompCircleCol, CompCircleRadius);
            HImage withCircle = img.PaintRegion(circle, 220.0, "fill");
            circle.Dispose();
            img.Dispose();

            HRegion ellipse = new HRegion();
            ellipse.GenEllipse(CompEllipseRow, CompEllipseCol, CompEllipsePhi, CompEllipseR1, CompEllipseR2);
            HImage withEllipse = withCircle.PaintRegion(ellipse, 220.0, "fill");
            ellipse.Dispose();
            withCircle.Dispose();

            // 水平線（±2 px 厚）
            HImage result = PaintRect(withEllipse, CompLineRow - 2, CompLineColBegin, CompLineRow + 2, CompLineColEnd, 220.0);
            return result;
        }

        /// <summary>Edge image with Gaussian blur.</summary>
        public static HImage CreateBlurryImage(int w = DefaultSize, int h = DefaultSize)
        {
            using (HImage edge = CreateEdgeImage(w, h))
            {
                return edge.GaussFilter(9);
            }
        }

        /// <summary>Very narrow gray range (120 vs 130, diff=10) = low contrast.</summary>
        public static HImage CreateLowContrastImage(int w = DefaultSize, int h = DefaultSize)
        {
            HImage img = new HImage();
            img.GenImageConst("byte", w, h);
            img = PaintRect(img, 0, 0, h - 1, w - 1, 120.0);
            img = PaintRect(img, 0.0, w / 2.0, h - 1.0, w - 1.0, 130.0);
            return img;
        }

        /// <summary>RGB 3-channel image.</summary>
        public static HImage CreateRgbImage(int w = DefaultSize, int h = DefaultSize)
        {
            using (HImage r = CreateUniform(w, h, 200))
            using (HImage g = CreateUniform(w, h, 100))
            using (HImage b = CreateUniform(w, h, 50))
            {
                return r.Compose3(g, b);
            }
        }

        /// <summary>White rectangle (120x80) on black background — for template matching.</summary>
        public static HImage CreateTemplateShapesImage(int w = DefaultSize, int h = DefaultSize)
        {
            HImage img = new HImage();
            img.GenImageConst("byte", w, h);
            img = PaintRect(img, 0, 0, h - 1, w - 1, 20.0);
            // Larger shape: 140x100 px to give shape model enough points
            img = PaintRect(img, h / 2.0 - 50, w / 2.0 - 70, h / 2.0 + 50, w / 2.0 + 70, 240.0);
            return img;
        }

        /// <summary>Different image (uniform gray, no template shape).</summary>
        public static HImage CreateNonMatchingImage(int w = DefaultSize, int h = DefaultSize)
        {
            HImage img = new HImage();
            img.GenImageConst("byte", w, h);
            img = PaintRect(img, 0, 0, h - 1, w - 1, 80.0);
            return img;
        }

        /// <summary>Image with a thin straight line for line fitting.</summary>
        public static HImage CreateLineImage(bool horizontal, int w = DefaultSize, int h = DefaultSize)
        {
            HImage img = new HImage();
            img.GenImageConst("byte", w, h);
            img = PaintRect(img, 0, 0, h - 1, w - 1, 30.0);
            if (horizontal)
                img = PaintRect(img, h / 2.0 - 2, w / 4.0, h / 2.0 + 2, w * 3.0 / 4, 220.0);
            else
                img = PaintRect(img, h / 4.0, w / 2.0 - 2, h * 3.0 / 4, w / 2.0 + 2, 220.0);
            return img;
        }
    }
}
