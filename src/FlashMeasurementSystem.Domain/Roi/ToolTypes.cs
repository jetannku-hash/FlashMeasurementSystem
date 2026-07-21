namespace FlashMeasurementSystem.Domain.Roi
{
    /// <summary>
    /// 工具型別的共同判準。**放在 Domain 是為了讓驗證器與執行流程共用同一份定義**——
    /// 這類「哪些型別怎麼判定」的知識一旦兩地各寫一份就會漂移，而漂移的症狀通常是假 OK。
    /// </summary>
    public static class ToolTypes
    {
        /// <summary>
        /// 此型別是否以雙邊公差 [Nominal+Lower, Nominal+Upper] 判定 OK/NG。
        ///
        /// 只有真的量得出單一純量的型別才算數。構造工具（intersection / midline / projection）
        /// 產出的是幾何而非可判合格與否的量：它們曾落入報表的雙邊公差 catch-all 分支，
        /// 而取值函式對它們回傳 0、RecipeEditor 又一律給預設公差 [0,0]，於是 0 落在 [0,0] 內
        /// → **每一次量測都在 CSV/PDF 產生一列偽造的「OK（偏差 0.0000）」**，連構造失敗時也是。
        ///
        /// GD&amp;T 走單邊公差、gear/pcd/pin_pitch/hole_array 走各自的多項判定，都不在此集合。
        /// </summary>
        public static bool IsDoubleSidedTolerance(string toolType)
        {
            switch (toolType)
            {
                case "circle":
                case "line":
                case "distance":
                case "angle":
                case "arc":
                    return true;
                default:
                    return false;
            }
        }
    }
}
