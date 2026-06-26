namespace FlashMeasurementSystem.Domain.Geometry
{
    public enum GeometricPrimitiveKind { Point, Line, Circle }

    /// <summary>
    /// 幾何基元 value object：統一承載量測工具的幾何輸出，供構造與下游 distance/angle 消費。
    /// 座標 (row, col)，row 向下。依 Kind 使用對應欄位。
    /// </summary>
    public sealed class GeometricPrimitive
    {
        public GeometricPrimitiveKind Kind { get; private set; }

        // Point
        public double Row { get; private set; }
        public double Col { get; private set; }

        // Line
        public double Row1 { get; private set; }
        public double Col1 { get; private set; }
        public double Row2 { get; private set; }
        public double Col2 { get; private set; }

        // Circle
        public double CenterRow { get; private set; }
        public double CenterCol { get; private set; }
        public double RadiusPx { get; private set; }

        public static GeometricPrimitive Point(double row, double col)
        {
            return new GeometricPrimitive { Kind = GeometricPrimitiveKind.Point, Row = row, Col = col };
        }

        public static GeometricPrimitive Line(double r1, double c1, double r2, double c2)
        {
            return new GeometricPrimitive
            {
                Kind = GeometricPrimitiveKind.Line,
                Row1 = r1, Col1 = c1, Row2 = r2, Col2 = c2
            };
        }

        public static GeometricPrimitive Circle(double centerRow, double centerCol, double radiusPx)
        {
            return new GeometricPrimitive
            {
                Kind = GeometricPrimitiveKind.Circle,
                CenterRow = centerRow, CenterCol = centerCol, RadiusPx = radiusPx
            };
        }

        /// <summary>取「點」語意：Point 回自身、Circle 回圓心、Line 回 false。</summary>
        public bool TryAsPoint(out double row, out double col)
        {
            if (Kind == GeometricPrimitiveKind.Point) { row = Row; col = Col; return true; }
            if (Kind == GeometricPrimitiveKind.Circle) { row = CenterRow; col = CenterCol; return true; }
            row = 0; col = 0; return false;
        }
    }
}
