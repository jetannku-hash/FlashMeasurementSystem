using System;
using System.IO;

namespace FlashMeasurementSystem
{
    /// <summary>
    /// 集中解析專案 data 目錄。原本散落在 MainWindow（FindTemplatesDirectory / ResolveDataDir /
    /// ResolveCalibrationsDir / ResolveRecipesDir）與 CalibrationDialog 的「往上找 .sln → data/&lt;sub&gt;」
    /// 邏輯逐字重複五份；統一於此，行為與各原版相同。
    /// </summary>
    internal static class DataPaths
    {
        // 由 app base directory 往上找含 FlashMeasurementSystem.sln 的資料夾；找不到回 null。
        public static string FindSolutionDir()
        {
            try
            {
                var current = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                while (current != null)
                {
                    if (File.Exists(Path.Combine(current.FullName, "FlashMeasurementSystem.sln")))
                        return current.FullName;
                    current = current.Parent;
                }
            }
            catch
            {
                // 任何路徑問題都當作找不到，由呼叫端退回 base directory。
            }
            return null;
        }

        // data 根目錄：.sln 旁的 data/；找不到 .sln 時退回 base directory 下的 data/。
        public static string DataDir()
        {
            string sln = FindSolutionDir();
            return Path.Combine(sln ?? AppDomain.CurrentDomain.BaseDirectory, "data");
        }

        // data/&lt;name&gt; 子目錄（calibrations / recipes / reports / logs …）。
        public static string SubDir(string name)
        {
            return Path.Combine(DataDir(), name);
        }

        // 模板目錄特例：找不到 .sln 時回 null（呼叫端自行退回 temp），與原 FindTemplatesDirectory 一致。
        public static string TemplatesDirOrNull()
        {
            string sln = FindSolutionDir();
            return sln == null ? null : Path.Combine(sln, "data", "templates");
        }
    }
}
