using System;
using System.Globalization;

namespace PCBSCalculatorHelper
{
    internal struct WindowPoint
    {
        public float X, Y;
        public WindowPoint(float x, float y) { X = x; Y = y; }
    }

    internal static class WindowPositionMath
    {
        public static bool TryParse(string value, out WindowPoint point)
        {
            point = new WindowPoint();
            string[] fields = (value ?? "").Split(',');
            float x, y;
            if (fields.Length != 2 ||
                !Single.TryParse(fields[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x) ||
                !Single.TryParse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y) ||
                !Finite(x) || !Finite(y) || x < 0 || x > 1 || y < 0 || y > 1) return false;
            point = new WindowPoint(x, y);
            return true;
        }

        public static string Serialize(WindowPoint normalized)
        {
            return normalized.X.ToString("R", CultureInfo.InvariantCulture) + "," +
                normalized.Y.ToString("R", CultureInfo.InvariantCulture);
        }

        public static WindowPoint Normalize(WindowPoint pixels, float screenWidth, float screenHeight)
        {
            return new WindowPoint(Limit(pixels.X / Math.Max(1, screenWidth), 0, 1),
                Limit(pixels.Y / Math.Max(1, screenHeight), 0, 1));
        }

        public static WindowPoint Resolve(WindowPoint normalized, float screenWidth, float screenHeight,
            float windowWidth, float windowHeight)
        {
            return Clamp(new WindowPoint(normalized.X * screenWidth, normalized.Y * screenHeight),
                screenWidth, screenHeight, windowWidth, windowHeight);
        }

        public static WindowPoint Clamp(WindowPoint pixels, float screenWidth, float screenHeight,
            float windowWidth, float windowHeight)
        {
            return new WindowPoint(Limit(pixels.X, 0, Math.Max(0, screenWidth - Math.Max(0, windowWidth))),
                Limit(pixels.Y, 0, Math.Max(0, screenHeight - Math.Max(0, windowHeight))));
        }

        private static bool Finite(float value) { return !Single.IsNaN(value) && !Single.IsInfinity(value); }
        private static float Limit(float value, float min, float max)
        { return Finite(value) ? Math.Max(min, Math.Min(max, value)) : min; }
    }
}
