namespace FlashMeasurementSystem.Domain.Workflow
{
    /// <summary>
    /// 單一工具的執行結果如何計入 OK / NG。
    ///
    /// **這條規則必須只存在這一份。** 它原本在 MeasurementWorkflow 與 MainWindow.DrawRecipeResults
    /// 各寫了一份：workflow 那份修過「量測失敗也算 NG」，UI 那份沒有。而一鍵量測會在 workflow
    /// 算完之後呼叫 DrawRecipeResults，用少一條規則的計數**覆蓋 PASS/FAIL 大橫幅**——於是工具量測
    /// 失敗時，橫幅顯示綠色 PASS、詳細文字卻寫著 FAIL。橫幅是操作員唯一會看的東西，等於放行壞件。
    /// （「執行配方」沒有 workflow 兜底，橫幅與文字都是假 PASS。）
    ///
    /// 規則放在 Domain 而非 App.Wpf，是為了讓它可被單元測試涵蓋——量測編排目前住在 App.Wpf，
    /// 兩個測試專案都碰不到，那正是這次漂移沒被任何測試擋下的原因。
    /// </summary>
    public static class MeasurementOutcome
    {
        /// <summary>判定為合格。</summary>
        public static bool CountsAsOk(bool? isOk)
        {
            return isOk == true;
        }

        /// <summary>
        /// 判定為不合格。除了明確判 NG，還包含「硬量測失敗」：
        /// 支援該工具型別但沒量到（IsOk 停在 null）——沒量到就不能宣稱合格。
        ///
        /// 「成功但不判定」的元素/構造工具（Measured=true、IsOk=null，例如未設公差）
        /// 不落入此分支，維持三態中的「未判定」。
        /// 未支援的型別（Supported=false）同樣不算 NG——它根本沒跑，另有驗證器負責攔截。
        /// </summary>
        public static bool CountsAsNg(bool? isOk, bool supported, bool measured)
        {
            if (isOk == false) return true;
            if (isOk == null && supported && !measured) return true;
            return false;
        }
    }
}
