# 一鍵式閃測儀軟體開發手冊

> 版本：0.1  
> 目標讀者：有 C# 基礎但初次接觸 Halcon 與閃測儀開發的工程師  
> 開發平台：Windows + C# WPF + Halcon  
> 目標精度：±5 µm（參考級）  
> 目標視野：100 mm x 80 mm

---

# 第一章 前言

## 1.1 什麼是閃測儀

閃測儀（Flash Measurement Instrument / 一鍵式閃測儀）是一種利用光學影像進行工件尺寸量測的設備。操作者將工件放置在玻璃載物台上，系統透過工業相機與遠心鏡頭在固定視野內快速取像，再經由影像處理演算法自動計算出工件上各個幾何特徵（點、線、圓、孔、角度、距離等）的實際尺寸，並與設計公差比對，輸出 OK/NG 判定。

其核心特點是「一鍵量測」——操作者只需放工件、按一下按鈕，系統自動完成所有量測步驟，不需要人工對位或逐項量測。

## 1.2 量測原理概述

本系統的量測流程可濃縮為以下步驟：

```
工件放置 → 光源頻閃 + 相機取像 → 影像品質檢查
  → 模板匹配定位 → ROI 邊緣檢測 → 幾何元素擬合
  → 尺寸計算 → 公差判定 → OK/NG 輸出 → 報表儲存
```

每次量測的原始資料來源是一張高解析度灰階影像（通常在 12 MP 到 25 MP 之間）。所有尺寸計算都在影像空間中以像素為單位進行，再透過校正參數（pixel size, µm/px）轉換為物理單位（mm）。

### 1.2.1 亞像素精度

由於目標精度 ±5 µm 在 100 mm x 80 mm 視野下約等於 0.5 個像素（以 10 µm/px 估算），單純依賴整數像素無法達到要求。因此邊緣檢測必須採用**亞像素（subpixel）**技術，利用邊緣附近的灰階梯度曲線內插出更精確的邊緣位置，目標穩定度 0.05 px ~ 0.1 px。

### 1.2.2 校正的重要性

像素尺寸（µm/px）並非固定值，它會受到鏡頭畸變、安裝公差、溫度變化等因素影響。系統必須提供定期校正機制，以維持精度。校正方式一般是使用已知尺寸的標準件（如校正片），計算出實際的像素比例。

## 1.3 手冊使用方式

本手冊假設你已具備以下能力：

- 熟悉 C# 語法與 .NET 專案結構
- 能操作 Visual Studio 2022 或 Rider
- 了解基本的 WPF XAML 與 MVVM 概念

你不必事先熟悉 Halcon 或電腦視覺領域知識——每個量測功能都會從原理開始說明，並提供可直接複製執行的完整範例程式碼。

### 章節閱讀建議

| 讀者類型 | 建議路徑 |
|---|---|
| 初次接觸、想了解整體架構 | 依序閱讀第 1→2→3→4 章，再挑選需要的功能章節 |
| 已熟悉專案、需要特定功能實作 | 直接跳到第 4 章對應功能 |
| 正在除錯量測不準的問題 | 跳到第 6 章〈除錯與調校指南〉 |

### 範例程式碼約定

- 所有程式碼皆使用 C# + Halcon .NET 介面
- Halcon 類型置於 `using HalconDotNet;` 命名空間下
- 程式碼中的 `// --- 說明 ---` 註解標示該段落的用途
- 多數字串常數以 `public const` 或 `appsettings.json` 管理，此處寫在程式碼內以便展示

---

# 第二章 開發環境建置

## 2.1 必要套件與版本

| 元件 | 版本建議 | 說明 |
|---|---|---|
| Visual Studio | 2022 以上 | 社群版可，建議 Professional 以上 |
|| .NET | .NET Framework 4.8 | 建議使用 .NET Framework 4.8（Halcon 17.12 僅支援此平台） |
|| Halcon | 17.12 以上 | 建議 17.12 或相容版本，需包含 HALCON/.NET 元件 |
| Halcon 授權 | HDevelop / Runtime | 開發期需要 HDevelop 除錯，佈署可使用 Runtime |
| Windows SDK | 10.0.22621 以上 | 與 VS2022 一起安裝 |
| Git | 最新穩定版 | 版本控管 |

### 2.1.1 關於 Halcon 版本

Halcon 由 MVTec 公司提供，分為三個授權層級：

- **HDevelop**（開發環境）：包含圖形化除錯工具、影像瀏覽器、變數監控，開發期必備
- **Runtime**（執行環境）：僅能執行程式，不可編輯，佈署到產線使用
- **Steady / Steady Plus**（年度授權）：包含 HDevelop + Runtime

本手冊所有範例使用 Halcon .NET 介面（`HalconDotNet.dll`）。Halcon 17.12 版本對 .NET Framework 4.8 有完整支援。若使用其他版本 Halcon，請確認你的 Halcon 版本支援 `HalconDotNet` 命名空間。

## 2.2 安裝步驟

### 2.2.1 安裝 Halcon

1. 從 MVTec 官網下載 Halcon 安裝程式。
2. 執行安裝，選擇「Full Installation」。
3. 安裝過程中勾選「HALCON/.NET」元件。
4. 安裝完成後，系統會自動設定環境變數 `HALCONROOT` 與 `HALCONARCH`。
5. 打開 HDevelop，確認 `get_system('version')` 回傳的版本號正確。

### 2.2.2 建立 WPF 專案

開啟 Visual Studio，建立新專案：

```
專案範本：WPF App (.NET Framework 4.8)
專案名稱：FlashMeasurementSystem
解決方案名稱：FlashMeasurementSystem
位置：選擇你的工作目錄
```

### 2.2.3 加入 Halcon 參考

在方案總管中對專案按右鍵 → Add → Reference → Browse，導覽至 Halcon 安裝目錄下的 `bin\dotnet4` 資料夾，選取：

```
halcondotnet.dll
```

由於 Halcon 17.12 未提供 NuGet 套件，請使用上述手動加入參考的方式。

> 💡 **提示**：本手冊部分範例使用 Newtonsoft.Json 進行 JSON 序列化，請在 Package Manager Console 中執行以下指令安裝：

> ```
> Install-Package Newtonsoft.Json
> ```

### 2.2.4 設定平台目標

Halcon 目前僅提供 64-bit 版本，因此專案必須設定為 x64：

1. 專案右鍵 → Properties → Build
2. Platform target：選擇 **x64**
3. 若使用 AnyCPU，請勾選「Prefer 32-bit」取消

### 2.2.5 驗證安裝

建立一個簡單的 WPF 視窗，加入以下程式碼測試 Halcon 是否能正常載入：

```csharp
using HalconDotNet;
using System.Windows;

namespace FlashMeasurementSystem
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // --- 檢查 Halcon 版本 ---
                HOperatorSet.GetSystem("version", out HTuple version);
                MessageBox.Show($"Halcon 版本：{version.S}\n載入成功！", "驗證");
            }
            catch (HalconException ex)
            {
                MessageBox.Show($"Halcon 載入失敗：{ex.Message}", "錯誤");
            }
        }
    }
}
```

執行後應看到版本號訊息框。若出現 `HalconException`，請檢查：

- `HALCONROOT` 環境變數是否正確設定
- Platform target 是否為 x64
- `halcondotnet.dll` 是否正確加入參考
- 是否有 Halcon 授權（執行 `hdevelop` 看授權狀態）

## 2.3 專案資料夾結構建議

```
FlashMeasurementSystem/
├── FlashMeasurementSystem.sln
├── src/
│   ├── FlashMeasurementSystem.App.Wpf/       # WPF 主程式
│   ├── FlashMeasurementSystem.Domain/        # 領域模型與介面
│   ├── FlashMeasurementSystem.Application/   # 應用邏輯
│   ├── FlashMeasurementSystem.Infrastructure/ # 基礎設施實作
│   ├── FlashMeasurementSystem.Halcon/        # Halcon 封裝層
│   ├── FlashMeasurementSystem.Mes/           # MES 對接
│   └── FlashMeasurementSystem.Reporting/     # 報表
├── tests/
│   └── FlashMeasurementSystem.Tests/
├── data/
│   ├── images/        # Replay 測試影像
│   ├── recipes/       # .zcp 配方檔
│   ├── calibrations/  # 校正資料
│   └── reports/       # CSV 報表輸出
└── docs/
    └── 本手冊
```

## 2.4 Halcon 在 WPF 中的顯示控制項

Halcon 提供 `HWindowControl` WPF 控制項，用於顯示影像與繪製 overlay。在 XAML 中加入：

```xml
<Window x:Class="FlashMeasurementSystem.MainWindow"
        xmlns:halcon="clr-namespace:HalconDotNet;assembly=halcondotnet">
    <Grid>
        <halcon:HWindowControl x:Name="HWindow" />
    </Grid>
</Window>
```

對應的程式碼後端：

```csharp
using HalconDotNet;

// --- 設定顯示字型 ---
HOperatorSet.SetFont(HWindow.HalconWindow, "Consolas-12");

// --- 顯示一張影像 ---
HImage image = new HImage("byte", 640, 480);
image.DispObj(HWindow.HalconWindow);
```

⚠️ **注意**：在 WPF 中使用 `HWindowControl` 時，所有 Halcon 顯示操作必須在 UI 執行緒上執行。若從背景執行緒呼叫，需要使用 `Dispatcher.Invoke`。

---

# 第三章 核心概念說明

## 3.1 量測流程架構

本系統的量測流程設計為一個狀態機，可用以下狀態圖表示：

```
IDLE → LOADING_PROGRAM → WAITING_PART → PREPARING
  → ACQUIRING → CHECKING_IMAGE → MATCHING_TEMPLATE
  → MEASURING → EVALUATING → REPORTING → OUTPUTTING
  → IDLE
```

每個階段的責任：

| 階段 | 說明 |
|---|---|
| IDLE | 待機，等待外部觸發或按鈕事件 |
| LOADING_PROGRAM | 載入 `.zcp` 配方檔 |
| WAITING_PART | 等待工件到位訊號（PartPresent） |
| PREPARING | 設定光源、曝光、Z 軸高度 |
| ACQUIRING | 觸發相機取像 |
| CHECKING_IMAGE | 檢查影像亮度、清晰度 |
| MATCHING_TEMPLATE | 模板匹配，定位工件座標系 |
| MEASURING | 執行所有 ROI 邊緣檢測與幾何量測 |
| EVALUATING | 套用公差，產生 OK/NG |
| REPORTING | 儲存報表、影像，回傳 MES |
| OUTPUTTING | 輸出 I/O 狀態訊號 |
| IDLE | 回到待機，等待下一件 |

## 3.2 常用術語對照表

| 英文 | 中文 | 說明 |
|---|---|---|
| FOV (Field of View) | 視野 | 相機一次可拍攝的範圍 |
| ROI (Region of Interest) | 感興趣區域 | 量測工具在其內部搜尋邊緣的矩形區 |
| Pixel Size | 像素尺寸 | 每個像素對應的物理長度，單位 µm/px |
| Subpixel | 亞像素 | 以內插方式取得非整數像素位置的邊緣 |
| Edge Detection | 邊緣檢測 | 在 ROI 內找出灰階劇烈變化的位置 |
| Edge Polarity | 邊緣極性 | 暗→亮 或 亮→暗 的過渡方向 |
| Template Matching | 模板匹配 | 以已知樣板在影像中搜尋相同特徵 |
| Geometric Fitting | 幾何擬合 | 將離散邊緣點擬合成直線、圓等幾何元素 |
| Tolerance | 公差 | 設計尺寸的允許偏差範圍 |
| OK/NG | 合格/不合格 | 量測值在公差內為 OK，反之为 NG |
| Calibration | 校正 | 建立像素座標到物理座標的轉換關係 |
| GR&R | 量測系統重複性與再現性 | 評估量測系統變異的統計方法 |
| Recipe | 配方 | 一組完整的量測程式設定，儲存為 `.zcp` |
| MES | 製造執行系統 | 工廠管理生產流程的上位系統 |
| Handshake | 握手通訊 | 閃測儀與外部設備之間的 I/O 互鎖流程 |

## 3.3 Halcon 資料型別入門

開發過程中會頻繁接觸以下 Halcon 資料型別：

| 型別 | 說明 | 常用建立方式 |
|---|---|---|
| `HImage` | 影像資料 | `new HImage("byte", width, height)` |
| `HRegion` | 區域（2D 二值遮罩） | `gen_rectangle1`, `gen_circle` |
| `HXLDCont` | 輪廓（亞像素邊緣點連線） | `edges_sub_pix` 輸出 |
| `HTuple` | 萬用容器（整數/浮點/字串/陣列） | 所有 Halcon 算子的輸入輸出 |
| `HShapeModel` | 形狀模板模型 | `create_shape_model` 建立 |
| `HWindow` | 顯示視窗 | `HWindowControl.HalconWindow` |
| `HObject` | 通用影像物件基底 | 用於 `HDevelopExport` 產生的程式碼 |

### 3.3.1 HTuple 使用範例

```csharp
// --- 建立單值 ---
HTuple value = 42;
HTuple text = "hello";

// --- 建立陣列 ---
HTuple values = new HTuple(new double[] { 1.0, 2.0, 3.0 });

// --- 取值 ---
double d = value.D;      // double
int i = value.I;          // int
string s = text.S;        // string
int len = values.Length;  // 陣列長度

// --- 陣列索引（1-based，Halcon 慣例）---
double first = values[0].D;   // C# 為 0-based
```

⚠️ **注意**：HTuple 存取時型別必須正確。若存入的是整數卻用 `value.D` 取值，會拋出 HalconException。不確定型別時可使用 `val.Type` 檢查。

## 3.4 Halcon 例外處理

所有 Halcon 算子在執行失敗時都會擲出 `HalconException`。建議的處理模式：

```csharp
try
{
    HOperatorSet.FindShapeModel(image, model, ...);
}
catch (HalconException ex)
{
    // --- 記錄錯誤到日誌 ---
    Logger.Error($"模板匹配失敗：{ex.Message}");

    // --- 若為可恢復錯誤，回傳預設值或標示量測失敗 ---
    return MeasurementResult.Failure(ErrorCode.MatchingFailed);
}
```

## 3.5 影像座標系統

Halcon 的影像座標系統為：

- 左上角為原點 (0, 0)
- Row 軸向下（Y 方向）
- Column 軸向右（X 方向）
- 單位：pixel

在量測結果回報時，我們會將 Row/Column 轉換為物理單位的 X/Y 座標：

```csharp
// --- Pixel 轉 mm ---
double pixelSizeUmX = 10.0;   // µm/px
double pixelSizeUmY = 10.0;

double xMm = (col - centerCol) * pixelSizeUmX / 1000.0;
double yMm = (row - centerRow) * pixelSizeUmY / 1000.0;
```

---

> 前三章已完成。請輸入「繼續」以進入**第四章：量測功能實作**。

---

# 第四章 量測功能實作

> 本章每個量測功能獨立成一節，每節包含：功能說明、前置條件、完整範例程式碼、逐行註解、參數說明表、常見錯誤與排除方式、小結。

---

## 4.1 影像品質檢查 (Image Quality Check)

### 功能說明

在執行量測之前，必須先確認取得的影像品質是否足夠。品質不良的影像（過暗、過曝、模糊）會直接導致量測失敗或精度下降。影像品質檢查作為量測流程的第一道防線，應在每次取像後、模板匹配前執行。

檢查項目：

| 項目 | 意義 | 臨界值 |
|---|---|---|
| Mean Brightness | 全圖平均灰階 | 80 ~ 180（建議） |
| Saturation Ratio | 灰階 255 的像素比例 | < 1% |
| Blur Score | 影像清晰度（Laplacian variance） | 需實測，越大越清晰 |
| Contrast | 邊緣區域對比度 | > 20 gray level |

### 前置條件

- 已載入影像至 `HImage` 物件
- ROI 區域已定義（若只需檢查全域，可跳過）

### 完整範例程式碼

```csharp
using HalconDotNet;
using System;

public class ImageQualityChecker
{
    /// <summary>
    /// 影像品質檢查結果
    /// </summary>
    public class QualityResult
    {
        public bool Pass { get; set; }
        public double MeanBrightness { get; set; }
        public double SaturationRatio { get; set; }
        public double BlurScore { get; set; }
        public double Contrast { get; set; }
        public string Message { get; set; } = "";
    }

    // --- 參數設定 ---
    public double MinBrightness { get; set; } = 80;
    public double MaxBrightness { get; set; } = 180;
    public double MaxSaturationRatio { get; set; } = 1.0;  // %
    public double MinBlurScore { get; set; } = 100.0;
    public double MinContrast { get; set; } = 20.0;

    /// <summary>
    /// 執行影像品質檢查
    /// </summary>
    public QualityResult Check(HImage image)
    {
        var result = new QualityResult();

        try
        {
            // --- 1. 計算全圖平均灰階與灰階直方圖 ---
            HOperatorSet.Intensity(new HImage(), image,
                out HTuple meanBrightness, out HTuple _);
            result.MeanBrightness = meanBrightness.D;

            // --- 2. 計算飽和比例 — 灰階值 = 255 的像素佔比 ---
            // 使用門檻值選出灰階 254~255 的區域
            HRegion saturatedRegion = image.Threshold(254.0, 255.0);
            HOperatorSet.AreaCenter(saturatedRegion, out HTuple satArea, 
                out HTuple _, out HTuple _);
            HOperatorSet.GetImagePointer1(image, out HTuple _, 
                out HTuple _, out HTuple width, out HTuple height);
            double totalPixels = width.D * height.D;
            result.SaturationRatio = (satArea.D / totalPixels) * 100.0;

            // --- 3. 計算清晰度分數：Laplacian 變異數 ---
            // 先以 Laplace 濾波器強化邊緣
            HImage laplace = image.Laplace("absolute", 3, "n_4_self_opt");
            HOperatorSet.Intensity(new HImage(), laplace,
                out HTuple _, out HTuple blurDeviation);
            result.BlurScore = blurDeviation.D;

            // --- 4. 計算對比度：影像灰階標準差 ---
            HOperatorSet.Intensity(new HImage(), image,
                out HTuple _, out HTuple deviation);
            result.Contrast = deviation.D;

            // --- 5. 判定是否合格 ---
            var failures = new System.Collections.Generic.List<string>();

            if (result.MeanBrightness < MinBrightness)
                failures.Add($"過暗 (mean={result.MeanBrightness:F1} < {MinBrightness})");
            else if (result.MeanBrightness > MaxBrightness)
                failures.Add($"過亮 (mean={result.MeanBrightness:F1} > {MaxBrightness})");

            if (result.SaturationRatio > MaxSaturationRatio)
                failures.Add($"飽和過高 ({result.SaturationRatio:F2}% > {MaxSaturationRatio}%)");

            if (result.BlurScore < MinBlurScore)
                failures.Add($"模糊 (blur score={result.BlurScore:F1} < {MinBlurScore})");

            if (result.Contrast < MinContrast)
                failures.Add($"對比不足 (contrast={result.Contrast:F1} < {MinContrast})");

            result.Pass = failures.Count == 0;
            result.Message = result.Pass ? "影像品質合格" : string.Join("; ", failures);
        }
        catch (HalconException ex)
        {
            result.Pass = false;
            result.Message = $"影像品質檢查異常：{ex.Message}";
        }

        return result;
    }
}
```

### 逐行註解

| 行範圍 | 說明 |
|---|---|
| `HOperatorSet.Intensity` | 計算影像或區域內的平均灰階與標準差。第一個參數傳入空 `HImage` 表示全圖範圍 |
| `Threshold(254, 255)` | 選出灰階值在 254~255 之間的像素區域，用來計算飽和像素數量 |
| `Laplace("absolute", 3, "n_4_self_opt")` | Laplace 邊緣強化濾波。參數 `"absolute"` 取絕對值，`3` 為濾波器大小，`"n_4_self_opt"` 為遮罩類型 |
| `Intensity` 對 laplace 影像取標準差 | Laplacian 影像的標準差越大，表示原圖邊緣越銳利、越清晰 |

### 參數說明表

| 參數 | 型別 | 預設值 | 建議範圍 | 說明 |
|---|---|---|---|---|
| `MinBrightness` | double | 80 | 60 ~ 100 | 平均灰階下限，低於此值判定過暗 |
| `MaxBrightness` | double | 180 | 160 ~ 200 | 平均灰階上限，高於此值判定過亮 |
| `MaxSaturationRatio` | double | 1.0 | 0.5 ~ 5.0 | 飽和像素最大容許比例 (%) |
| `MinBlurScore` | double | 100 | 50 ~ 500 | 清晰度門檻，須依實際影像調校 |
| `MinContrast` | double | 20 | 10 ~ 40 | 最低可接受對比度 |

### 常見錯誤與排除方式

| 問題 | 可能原因 | 排除方式 |
|---|---|---|
| 總是判定過暗 | 光源亮度不足或曝光時間太短 | 提高光源亮度或增加曝光時間 |
| 總是判定過亮/飽和 | 光源太強或曝光過度 | 降低亮度或曝光，或加 ND 濾鏡 |
| 總是判定模糊 | 未對焦、Z 軸高度不對 | 執行自動對焦或調整 Z 軸 |
| 清晰度分數對調焦不敏感 | Laplacian 參數不匹配 | 嘗試 `n_8` 遮罩或增大濾波器尺寸到 5 |
| 對比度偏低但人眼看起來正常 | 工件本身低對比（如透明塑膠） | 改用背光照明或降低對比門檻 |

### 小結

- 影像品質檢查是量測流程的第一道關卡，不可省略。
- `BlurScore` 的門檻值高度依賴實際光學系統，建議在調校階段收集 50~100 張典型影像後統計決定。
- 若工件本身有高反光或低對比特徵，可考慮在 ROI 區域（而非全圖）執行品質檢查。

---

## 4.2 模板匹配 (Template Matching)

### 功能說明

模板匹配用於在待測影像中尋找與預先註冊的樣板最相似的位置。在一鍵閃測儀中，模板匹配有兩個主要用途：

1. **定位工件座標系**：找到工件在視野中的精確位置與旋轉角度，作為後續 ROI 量測的基準。
2. **識別工件類型**：在有多種工件的場合，可依匹配結果決定使用的配方。

### 前置條件

- 已建立模板影像與對應的 `.zcp` 配方
- 當前量測影像已載入
- 已執行影像品質檢查（建議）

### 完整範例程式碼

#### 4.2.1 建立模板

```csharp
using HalconDotNet;
using System;
using System.IO;

public class TemplateManager
{
    /// <summary>
    /// 從參考影像中建立形狀模型模板
    /// </summary>
    /// <param name="image">參考影像（基準件或標準件）</param>
    /// <param name="templateRegion">模板區域（工件的 ROI）</param>
    /// <param name="modelFilePath">儲存 .shm 模型檔的路徑</param>
    /// <param name="angleStart">起始角度（度）</param>
    /// <param name="angleExtent">角度範圍（度）</param>
    /// <param name="pyramidLevel">金字塔層數</param>
    /// <returns>建立好的 HShapeModel</returns>
    public HShapeModel CreateTemplate(
        HImage image,
        HRegion templateRegion,
        string modelFilePath,
        double angleStart = -5.0,
        double angleExtent = 10.0,
        int pyramidLevel = 3)
    {
        try
        {
            // --- 1. 從影像中裁切出模板區域 ---
            HImage templateImage = image.ReduceDomain(templateRegion);
            // --- 2. 裁切後的影像可能含有無關背景，計算最佳 ROI ---
            HOperatorSet.ReduceDomain(templateImage, templateRegion, 
                out HImage reduced);

            // --- 3. 建立形狀模型 ---
            //     numLevels: 金字塔層數，層數越多搜尋越快但精度略降
            //     angleStart/Extent: 搜尋角度範圍（度）
            //     angleStep: 角度步長（度），自動計算
            //     optimization: "none" 使用完整輪廓，"pregeneration" 加快
            //     metric: "use_polarity" 使用灰階極性
            //     minContrast:  最小對比度，低於此的邊緣點被忽略
            HShapeModel model = new HShapeModel();
            model.CreateShapeModel(
                reduced,
                pyramidLevel,          // numLevels
                0.0,                   // angleStart (rad)
                HTuple.TupleRad(angleExtent),  // angleExtent (rad)
                "auto",                // angleStep
                "none",                // optimization
                "use_polarity",        // metric
                10,                    // minContrast
                (int)4,                // minContrast (lowest pyramid level)
                "greediness"           // greediness (0~1, 0=slow but reliable)
            );

            // --- 4. 儲存到檔案供後續使用 ---
            string dir = Path.GetDirectoryName(modelFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            model.WriteShapeModel(modelFilePath);

            return model;
        }
        catch (HalconException ex)
        {
            throw new InvalidOperationException(
                $"模板建立失敗：{ex.Message}", ex);
        }
    }
}
```

#### 4.2.2 執行模板匹配

```csharp
public class TemplateMatcher
{
    /// <summary>
    /// 模板匹配結果
    /// </summary>
    public class MatchResult
    {
        public bool Found { get; set; }
        public double Row { get; set; }
        public double Column { get; set; }
        public double AngleDeg { get; set; }
        public double Score { get; set; }
        public double ScaleX { get; set; } = 1.0;
    }

    private HShapeModel _model;

    /// <summary>
    /// 從檔案載入已建立的形狀模型
    /// </summary>
    public void LoadModel(string modelFilePath)
    {
        _model = new HShapeModel(modelFilePath);
    }

    /// <summary>
    /// 在當前影像中尋找模板
    /// </summary>
    /// <param name="image">待搜尋影像</param>
    /// <param name="searchRegion">搜尋區域（null 表示全圖）</param>
    /// <param name="minScore">最低匹配分數 (0~1)</param>
    /// <param name="numMatches">最多回傳幾個匹配結果</param>
    /// <param name="maxOverlap">最大重疊比例，避免回傳重疊的匹配</param>
    /// <returns>匹配結果列表，依分數降序排列</returns>
    public List<MatchResult> FindMatches(
        HImage image,
        HRegion searchRegion = null,
        double minScore = 0.75,
        int numMatches = 1,
        double maxOverlap = 0.5)
    {
        var results = new List<MatchResult>();

        if (_model == null)
            throw new InvalidOperationException("請先呼叫 LoadModel 載入模板");

        try
        {
            HImage searchImage;

            // --- 1. 若指定搜尋區域，則縮小搜尋範圍 ---
            if (searchRegion != null)
                searchImage = image.ReduceDomain(searchRegion);
            else
                searchImage = image;

            // --- 2. 執行形狀匹配搜尋 ---
            HOperatorSet.FindShapeModel(
                searchImage,
                _model,
                HTuple.TupleRad(10.0),     // angleStart (rad)
                HTuple.TupleRad(10.0),     // angleExtent (rad)
                minScore,
                numMatches,
                maxOverlap,
                "none",                    // subPixel
                0,                         // numLevels (0=all)
                0.5,                       // greediness
                out HTuple matchRow,
                out HTuple matchCol,
                out HTuple matchAngle,
                out HTuple matchScore
            );

            // --- 3. 整理結果 ---
            for (int i = 0; i < matchScore.Length; i++)
            {
                results.Add(new MatchResult
                {
                    Found = true,
                    Row = matchRow[i].D,
                    Column = matchCol[i].D,
                    AngleDeg = matchAngle[i].D * 180.0 / Math.PI,
                    Score = matchScore[i].D
                });
            }

            // --- 4. 若沒找到，回傳 Found=false 的項目 ---
            if (results.Count == 0)
            {
                results.Add(new MatchResult { Found = false, Score = 0 });
            }
        }
        catch (HalconException ex)
        {
            results.Add(new MatchResult { Found = false, Score = 0 });
            System.Diagnostics.Debug.WriteLine($"模板匹配錯誤：{ex.Message}");
        }

        return results;
    }

    public void Dispose()
    {
        _model?.Dispose();
    }
}
```

### 逐行註解

| 步驟 | 說明 |
|---|---|
| `ReduceDomain` | 用指定的 ROI 區域裁切影像，只保留 ROI 內部的像素內容，減少 CPu 負擔 |
| `CreateShapeModel` | 建立形狀模型模板。Halcon 會自動提取 ROI 內的邊緣輪廓作為匹配依據 |
| `TupleRad` | 將角度從度轉換為弧度，Halcon 內部使用弧度 |
| `FindShapeModel` | 在搜尋影像中尋找最符合模板的位置。回傳 row/col/angle/score |
| `subPixel` = `"none"` | 這裡不啟用亞像素定位以加快速度；若需更高精度可設為 `"least_squares"` |

### 參數說明表

**建立模板參數**

| 參數 | 型別 | 預設值 | 建議範圍 | 說明 |
|---|---|---|---|---|
| `numLevels` | int | 3 | 1 ~ 5 | 金字塔層數，越大搜尋越快但精度略降 |
| `angleStart` | double | -5.0 | 依實際擺放誤差 | 模板可偏轉的起始角度（度） |
| `angleExtent` | double | 10.0 | 2 ~ 30 | 總角度範圍（度） |
| `metric` | string | "use_polarity" | use_polarity / ignore_local_polarity | 若工件反光導致極性翻轉，使用後者 |
| `minContrast` | int | 10 | 5 ~ 30 | 最小邊緣對比度，低於此的邊緣點不入模板 |

**匹配參數**

| 參數 | 型別 | 預設值 | 建議範圍 | 說明 |
|---|---|---|---|---|
| `minScore` | double | 0.75 | 0.5 ~ 0.95 | 匹配分數門檻，越低越容易找到但誤報增加 |
| `numMatches` | int | 1 | 1 ~ 10 | 最多回傳的匹配結果數量 |
| `greediness` | double | 0.5 | 0.0 ~ 1.0 | 0=全程精搜（慢），1=快速貪婪（可能漏） |

### 常見錯誤與排除方式

| 問題 | 可能原因 | 排除方式 |
|---|---|---|
| 匹配分數偏低 (< 0.5) | 工件外觀與模板差異大、光線不同 | 重新取像建立模板，或使用 `ignore_local_polarity` |
| 匹配位置偏移 | 模板建立時的 ROI 不精確 | 確認模板區域包含足夠的特徵邊緣 |
| 匹配速度過慢 | 搜尋範圍太大或角度範圍太寬 | 縮小搜尋區域或限縮角度範圍 |
| 誤匹配（找到錯誤位置） | 工件上有重複特徵 | 加大模板範圍或提高 `minScore` |
| `HalconException`：model not initialized | 未載入模板 | 檢查 `.shm` 檔案路徑是否正確 |

### 小結

- 模板匹配是閃測儀定位的核心，建議先用 HDevelop 試跑最佳參數再寫入程式。
- 模板應使用標準件在標準光源環境下建立，以確保一致性。
- 對於對稱性工件，考慮使用多個模板或調整角度範圍避免誤匹配。

---

## 4.3 邊緣檢測 (Edge Detection)

### 功能說明

邊緣檢測是閃測儀量測的基礎操作。在指定的 ROI 區域內，沿著搜尋方向找出灰階急遽變化的位置（邊緣點），並以亞像素精度輸出這些邊緣點的座標。後續的幾何擬合（直線、圓）都要依賴這些邊緣點。

### 前置條件

- 已載入影像
- ROI（感興趣區域）已定義
- 搜尋方向與邊緣極性已設定

### 完整範例程式碼

```csharp
using HalconDotNet;
using System;
using System.Collections.Generic;

public class EdgeDetector
{
    /// <summary>
    /// 單一邊緣檢測結果
    /// </summary>
    public class EdgeResult
    {
        public bool Success { get; set; }
        public List<EdgePoint> EdgePoints { get; set; } = new();
        public string ErrorMessage { get; set; } = "";
    }

    public class EdgePoint
    {
        public double Row { get; set; }
        public double Column { get; set; }
        public double Amplitude { get; set; }
        public double Distance { get; set; }  // 沿搜尋方向的距離
    }

    // --- 參數設定 ---
    public double RoiWidth { get; set; } = 100;         // ROI 寬度 (px)
    public double ScanLength { get; set; } = 500;       // 搜尋長度 (px)
    public double Sigma { get; set; } = 1.2;            // 平滑係數
    public double Threshold { get; set; } = 25;         // 梯度門檻
    public string Polarity { get; set; } = "all";       // edge polarity
    public string EdgeSelector { get; set; } = "all";   // all / first / last
    public string SubpixelMethod { get; set; } = "parabolic";  // parabolic / gaussian / none

    /// <summary>
    /// 在 ROI 矩形內執行邊緣檢測
    /// </summary>
    public EdgeResult DetectEdges(
        HImage image,
        double centerRow,
        double centerCol,
        double length1,     // ROI 半長（沿搜尋方向）
        double length2,     // ROI 半寬（垂直搜尋方向）
        double angleRad)    // ROI 旋轉角度 (rad)
    {
        var result = new EdgeResult();

        try
        {
            // --- 1. 使用 measure_pos 進行一維邊緣檢測 ---
            //     建立測量物件
            HOperatorSet.GenMeasureRectangle2(
                centerRow, centerCol,
                angleRad,          // ROI 角度
                length1,           // 半長
                length2,           // 半寬
                image.GetImageSize()[0].D,  // width
                image.GetImageSize()[0].D,  // height
                "nearest_neighbor",
                out HTuple measureHandle);

            // --- 2. 執行邊緣檢測 ---
            HOperatorSet.MeasurePos(
                image,
                measureHandle,
                Sigma,             // 平滑係數
                Threshold,         // 邊緣門檻
                Polarity,          // 邊緣極性
                EdgeSelector,      // 邊緣選擇
                out HTuple edgeRow,
                out HTuple edgeCol,
                out HTuple edgeAmplitude,
                out HTuple edgeDistance);

            // --- 3. 釋放測量物件 ---
            HOperatorSet.CloseMeasure(measureHandle);

            // --- 4. 轉換為 EdgePoint 列表 ---
            for (int i = 0; i < edgeRow.Length; i++)
            {
                result.EdgePoints.Add(new EdgePoint
                {
                    Row = edgeRow[i].D,
                    Column = edgeCol[i].D,
                    Amplitude = edgeAmplitude[i].D,
                    Distance = edgeDistance[i].D
                });
            }

            result.Success = result.EdgePoints.Count > 0;

            if (!result.Success)
            {
                result.ErrorMessage = $"未檢測到邊緣點 (threshold={Threshold}, sigma={Sigma})";
            }
        }
        catch (HalconException ex)
        {
            result.Success = false;
            result.ErrorMessage = $"邊緣檢測異常：{ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// 使用亞像素 edges_sub_pix 進行二維邊緣檢測（適合曲線輪廓）
    /// </summary>
    public HXLDCont DetectEdgesSubPix(HImage image, HRegion roiRegion)
    {
        try
        {
            // --- 1. 只處理 ROI 內部 ---
            HImage reduced = image.ReduceDomain(roiRegion);

            // --- 2. 亞像素邊緣提取 ---
            HOperatorSet.EdgesSubPix(
                reduced,
                out HXLDCont contours,
                "canny",       // 演算法：canny / deriche1 / lanser1 / lanser2 / mshen / sobel_fast
                Sigma,         // 平滑係數
                Threshold,     // 低門檻
                40);           // 高門檻

            return contours;
        }
        catch (HalconException ex)
        {
            System.Diagnostics.Debug.WriteLine($"亞像素邊緣檢測錯誤：{ex.Message}");
            return null;
        }
    }
}
```

### 逐行註解

| 步驟 | 說明 |
|---|---|
| `GenMeasureRectangle2` | 建立一個矩形測量 ROI，參數為中心點、角度、半長半寬。`length1` 為沿搜尋方向的半長，`length2` 為垂直方向的半寬 |
| `MeasurePos` | 沿 ROI 長軸方向掃描，找出灰階梯度超過門檻的位置。回傳亞像素等級的邊緣點座標 |
| `CloseMeasure` | 釋放測量物件，避免記憶體洩漏 |
| `EdgesSubPix("canny")` | Canny 邊緣檢測 + 亞像素細化，輸出為 `HXLDCont`（亞像素輪廓），適合非直線的曲線邊緣 |

### 參數說明表

| 參數 | 型別 | 預設值 | 建議範圍 | 說明 |
|---|---|---|---|---|
| `Sigma` | double | 1.2 | 0.5 ~ 3.0 | 高斯平滑係數，越大越抗噪但邊緣定位變差 |
| `Threshold` | double | 25 | 5 ~ 80 | 邊緣梯度門檻，越低越容易找到邊緣但雜訊增多 |
| `Polarity` | string | "all" | all / positive / negative | 邊緣極性：all=全部，positive=暗→亮，negative=亮→暗 |
| `EdgeSelector` | string | "all" | all / first / last | 選擇哪個邊緣：all=全部，first=第一個，last=最後一個 |
| `SubpixelMethod` | string | "parabolic" | parabolic / gaussian / none | 亞像素內插方式 |
| `RoiWidth` (length2) | double | 100 | 20 ~ 500 px | ROI 寬度，影響平均降噪效果 |

### 常見錯誤與排除方式

| 問題 | 可能原因 | 排除方式 |
|---|---|---|
| 找不到邊緣點 | Threshold 太高或 Sigma 太大 | 降低 Threshold 或 Sigma |
| 邊緣點過多（雜訊） | Threshold 太低或光源不穩 | 提高 Threshold 或 Sigma |
| 邊緣位置不穩定 | 亞像素方法不合適 | 嘗試 `gaussian` 或 `moment` 方法 |
| 邊緣點呈現階梯狀 | ROI 角度與實際邊緣不平行 | 調整 ROI 角度 |
| 部分邊緣找不到 | 工件局部反光或汙損 | 改用多段 ROI 或調整光源 |

### 小結

- `MeasurePos` 適合直線邊緣的快速檢測，是閃測儀中最常用的算子。
- `EdgesSubPix` 適合曲線、圓形或不規則輪廓，精度更高但計算量較大。
- 亞像素參數 `"parabolic"` 為最常用的折衷方案；`"gaussian"` 精度更高但對雜訊敏感。
- 邊緣檢測的結果品質直接影響後續幾何擬合與最終量測精度，參數調校應以 HDevelop 先行測試。

---

## 4.4 直線擬合 (Line Fitting)

### 功能說明

將邊緣檢測得到的離散邊緣點擬合成一條直線，用於量測工件的直線邊緣位置與方向。閃測儀中常見的應用包括：工件外框直邊、槽孔邊緣、階梯邊緣等。

### 前置條件

- 已完成邊緣檢測，取得一組邊緣點座標
- 邊緣點應該大致落在一條直線上（偏離過大的點會被視為離群值）

### 完整範例程式碼

```csharp
using HalconDotNet;
using System;
using System.Collections.Generic;

public class LineFitter
{
    /// <summary>
    /// 直線擬合結果
    /// </summary>
    public class LineResult
    {
        public bool Success { get; set; }
        public double Row1 { get; set; }    // 起點 Row
        public double Col1 { get; set; }    // 起點 Column
        public double Row2 { get; set; }    // 終點 Row
        public double Col2 { get; set; }    // 終點 Column
        public double AngleDeg { get; set; } // 角度（度）
        public double Length { get; set; }   // 線段長度 (px)
        public double ResidualRms { get; set; }  // 擬合殘差 RMS (px)
        public int UsedPoints { get; set; }      // 使用的邊緣點數量
        public string ErrorMessage { get; set; } = "";
    }

    /// <summary>
    /// 從邊緣點集合擬合直線
    /// </summary>
    public LineResult FitLine(List<EdgeDetector.EdgePoint> edgePoints)
    {
        var result = new LineResult();

        if (edgePoints == null || edgePoints.Count < 3)
        {
            result.ErrorMessage = $"邊緣點不足 (need >= 3, got {edgePoints?.Count ?? 0})";
            return result;
        }

        try
        {
            // --- 1. 將 List<EdgePoint> 轉換為 Halcon HTuple ---
            int n = edgePoints.Count;
            var rows = new double[n];
            var cols = new double[n];

            for (int i = 0; i < n; i++)
            {
                rows[i] = edgePoints[i].Row;
                cols[i] = edgePoints[i].Column;
            }

            HTuple rowTuple = new HTuple(rows);
            HTuple colTuple = new HTuple(cols);

            // --- 2. 擬合直線 ---
            //     Algorithm 選擇：
            //     "least_squares"    — 標準最小二乘法，快但對離群值敏感
            //     "huber"            — 加權最小二乘法，適度抗離群值
            //     "tukey"            — 更強的抗離群值能力
            //     "ransac"           — 隨機取樣共識法，最強抗離群值
            HOperatorSet.FitLineContourXld(
                CreateXldContour(rows, cols),
                "tukey",           // 演算法
                -1,                // maxNumPoints (-1 = all)
                0,                 // 裁切端點比例
                5,                 // 離群值門檻 (sigma)
                3,                 // 最小點數需求
                out HTuple rowBegin,
                out HTuple colBegin,
                out HTuple rowEnd,
                out HTuple colEnd,
                out HTuple nr,
                out HTuple nc,
                out HTuple distance);

            // --- 3. 計算結果 ---
            result.Row1 = rowBegin.D;
            result.Col1 = colBegin.D;
            result.Row2 = rowEnd.D;
            result.Col2 = colEnd.D;

            // --- 4. 計算角度（度）---
            double deltaRow = result.Row2 - result.Row1;
            double deltaCol = result.Col2 - result.Col1;
            result.AngleDeg = Math.Atan2(deltaRow, deltaCol) * 180.0 / Math.PI;
            result.Length = Math.Sqrt(deltaRow * deltaRow + deltaCol * deltaCol);

            // --- 5. 計算殘差 RMS ---
            result.UsedPoints = n;
            double sumSq = 0;
            for (int i = 0; i < distance.Length; i++)
                sumSq += distance[i].D * distance[i].D;
            result.ResidualRms = Math.Sqrt(sumSq / distance.Length);

            result.Success = true;
        }
        catch (HalconException ex)
        {
            result.ErrorMessage = $"直線擬合失敗：{ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// 將離散點轉為 XLD 輪廓（Halcon 擬合函數的輸入格式）
    /// </summary>
    private HXLDCont CreateXldContour(double[] rows, double[] cols)
    {
        HXLDCont contour = new HXLDCont();
        contour.GenContourPolygonXld(rows, cols);
        return contour;
    }
}
```

### 參數說明表

| 參數 | 型別 | 建議值 | 說明 |
|---|---|---|---|
| Algorithm | string | "tukey" | least_squares / huber / tukey / ransac，依離群值比例選擇 |
| ClippingFactor | double | 2.0 | 離群值判定門檻（sigma 倍數），越低越嚴格 |
| MinPoints | int | 3 | 最少需要的邊緣點數 |
| MaxNumPoints | int | -1 | -1 = 使用所有點 |

### Algorithm 選擇指南

| 演算法 | 離群值容忍度 | 速度 | 適用情境 |
|---|---|---|---|
| least_squares | 極低 | 最快 | 邊緣點品質極佳、無離群值 |
| huber | 中 | 快 | 少量離群值 (< 10%) |
| tukey | 高 | 中 | 一般情況，推薦預設值 |
| ransac | 極高 | 慢 | 離群值比例高 (> 30%) 或邊緣有大量雜訊 |

### 常見錯誤與排除方式

| 問題 | 可能原因 | 排除方式 |
|---|---|---|
| 擬合線偏離實際邊緣 | 離群值過多 | 改用 tukey 或 ransac |
| 殘差 RMS 過大 (> 0.5 px) | 邊緣點品質不良或 ROI 涵蓋非直線區域 | 檢查邊緣檢測參數，或縮小 ROI |
| 擬合失敗：insufficient points | 邊緣點不足 3 個 | 檢查邊緣檢測是否成功 |
| 角度跳動 | ROI 長度太短 | 增加 ROI 長度以提高角度穩定性 |

### 小結

- 預設建議使用 `"tukey"` 演算法，兼顧速度與抗離群值能力。
- 擬合殘差 RMS 是評估邊緣品質的重要指標。若 RMS > 0.3 px，表示邊緣點品質可能有問題。
- 較長的 ROI 可提供更穩定的角度擬合，但需確保邊緣點都落在同一條直線上。

---

## 4.5 圓/孔擬合 (Circle Fitting)

### 功能說明

將邊緣檢測得到的離散邊緣點擬合成圓形，用於量測工件的圓形特徵（外圓、內孔、圓弧）。閃測儀中常見應用包括：軸承孔徑、螺絲孔位置、圓形工件外徑、圓弧半徑等。

### 前置條件

- 已完成邊緣檢測，取得一組邊緣點座標
- 邊緣點應大致分布在圓形輪廓上
- 對於完整圓，建議至少 8 ~ 12 個均勻分布的邊緣點

### 完整範例程式碼

```csharp
using HalconDotNet;
using System;
using System.Collections.Generic;

public class CircleFitter
{
    /// <summary>
    /// 圓擬合結果
    /// </summary>
    public class CircleResult
    {
        public bool Success { get; set; }
        public double CenterRow { get; set; }
        public double CenterCol { get; set; }
        public double RadiusPx { get; set; }
        public double ResidualRms { get; set; }  // 擬合殘差 RMS (px)
        public double Roundness { get; set; }     // 真圓度 (px)
        public int UsedPoints { get; set; }
        public string ErrorMessage { get; set; } = "";
    }

    /// <summary>
    /// 從邊緣點集合擬合圓
    /// </summary>
    public CircleResult FitCircle(List<EdgeDetector.EdgePoint> edgePoints)
    {
        var result = new CircleResult();

        if (edgePoints == null || edgePoints.Count < 3)
        {
            result.ErrorMessage = $"邊緣點不足 (need >= 3, got {edgePoints?.Count ?? 0})";
            return result;
        }

        try
        {
            // --- 1. 轉換為 Halcon HTuple ---
            int n = edgePoints.Count;
            var rows = new double[n];
            var cols = new double[n];

            for (int i = 0; i < n; i++)
            {
                rows[i] = edgePoints[i].Row;
                cols[i] = edgePoints[i].Column;
            }

            // --- 2. 建立 XLD 輪廓 ---
            HXLDCont contour = new HXLDCont();
            contour.GenContourPolygonXld(rows, cols);

            // --- 3. 擬合圓形 ---
            //     Algorithm: "algebraic" (代數法，快) / "geometric" (幾何法，精)
            //     "geometric" 精度較高但迭代次數較多
            HOperatorSet.FitCircleContourXld(
                contour,
                "geometric",       // 演算法
                -1,                // maxNumPoints
                0,                 // 裁切端點
                3,                 // 離群值門檻 (sigma)
                3,                 // 最小點數
                out HTuple row,
                out HTuple col,
                out HTuple radius,
                out HTuple startPhi,
                out HTuple endPhi,
                out HTuple pointOrder);

            // --- 4. 計算殘差 ---
            HOperatorSet.DistancePc(
                contour,
                row.TupleConcat(col),  // 圓心座標必須包裝成 [row, col]
                out HTuple minDist,
                out HTuple maxDist);

            result.CenterRow = row.D;
            result.CenterCol = col.D;
            result.RadiusPx = radius.D;
            result.UsedPoints = n;

            // --- 5. 計算殘差 RMS 與真圓度 ---
            double sumSq = 0;
            for (int i = 0; i < minDist.Length; i++)
            {
                double d = minDist[i].D;
                sumSq += d * d;
            }
            result.ResidualRms = Math.Sqrt(sumSq / minDist.Length);
            result.Roundness = maxDist.D - minDist.D;  // 真圓度 = 最大偏差 - 最小偏差

            result.Success = true;
        }
        catch (HalconException ex)
        {
            result.ErrorMessage = $"圓擬合失敗：{ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// 直接使用圓形測量工具（適用於完整圓的快速量測）
    /// </summary>
    /// <param name="image">輸入影像</param>
    /// <param name="centerRow">圓心 Row 初值</param>
    /// <param name="centerCol">圓心 Column 初值</param>
    /// <param name="radius">半徑初值 (px)</param>
    /// <param name="sigma">平滑係數</param>
    /// <param name="threshold">邊緣門檻</param>
    public CircleResult MeasureCircle(
        HImage image,
        double centerRow,
        double centerCol,
        double radius,
        double sigma = 1.2,
        double threshold = 25)
    {
        var result = new CircleResult();

        try
        {
            // --- 1. 建立環形測量 ROI ---
            HOperatorSet.GenMeasureCircle(
                centerRow, centerCol,
                radius,
                0, 0,              // 起始/終止角度（0~360 度）
                "negative",        // 極性（朝向圓心方向由亮到暗則為 negative）
                1.5,               // ROI 寬度比例
                image.GetImageSize()[0].D,
                image.GetImageSize()[0].D,
                "nearest_neighbor",
                out HTuple measureHandle);

            // --- 2. 執行圓形邊緣測量 ---
            HOperatorSet.MeasureCircle(
                image,
                measureHandle,
                sigma,
                threshold,
                "all",
                out HTuple edgeRow,
                out HTuple edgeCol,
                out HTuple edgeAmplitude,
                out HTuple edgeDistance);

            HOperatorSet.CloseMeasure(measureHandle);

            // --- 3. 擬合圓 ---
            if (edgeRow.Length >= 3)
            {
                HXLDCont edgeContour = new HXLDCont();
                edgeContour.GenContourPolygonXld(
                    edgeRow.ToDArr(),
                    edgeCol.ToDArr());

                HOperatorSet.FitCircleContourXld(
                    edgeContour, "geometric", -1, 0, 3, 3,
                    out HTuple fitRow,
                    out HTuple fitCol,
                    out HTuple fitRadius,
                    out HTuple _, out HTuple _, out HTuple _);

                result.CenterRow = fitRow.D;
                result.CenterCol = fitCol.D;
                result.RadiusPx = fitRadius.D;
                result.UsedPoints = edgeRow.Length;
                result.Success = true;
            }
            else
            {
                result.ErrorMessage = $"圓形邊緣點不足 ({edgeRow.Length} < 3)";
            }
        }
        catch (HalconException ex)
        {
            result.ErrorMessage = $"圓形量測異常：{ex.Message}";
        }

        return result;
    }
}
```

### 參數說明表

| 參數 | 型別 | 建議值 | 說明 |
|---|---|---|---|
| Algorithm | string | "geometric" | algebraic（快）/ geometric（精） |
| ClippingFactor | double | 3.0 | 離群值門檻 (sigma 倍數) |
| MinPoints | int | 3 | 最小點數 |
| Sigma | double | 1.2 | 平滑係數（用在 `MeasureCircle`） |
| Threshold | double | 25 | 邊緣門檻（用在 `MeasureCircle`） |

### 常見錯誤與排除方式

| 問題 | 可能原因 | 排除方式 |
|---|---|---|
| 擬合圓明顯偏大或偏小 | 邊緣點分布不均（只集中在圓弧一小段） | 使用 `MeasureCircle` 確保 360 度取點均勻 |
| 半徑跳動 | ROI 內的邊緣點混入雜訊或附近有其他特徵 | 縮小 ROI 或提高門檻 |
| 擬合失敗 | 邊緣點數量不足或分布太集中 | 確認 ROI 涵蓋至少 1/4 圓弧 |
| 真圓度過大 | 工件本身不圓、邊緣檢測參數不合適 | 檢查邊緣 Sigma 是否太大導致定位偏差 |

### 小結

- 對於完整圓孔，`MeasureCircle` 比手動取邊緣點再擬合更方便且穩定。
- 對於圓弧（不足 180 度），建議使用 `FitCircleContourXld` 搭配較高的離群值門檻。
- `"geometric"` 演算法精度較高，在 ±5 µm 應用中選擇它，即使稍微慢一些。
- 殘差 RMS 應 < 0.3 px 才算良好的擬合。
- 真圓度（Roundness）可以作為評估圓孔品質的額外指標。

---

> 第四章（功能 4.1 ~ 4.5）已完成。請輸入「繼續」以進入功能 4.6 ~ 4.10。


## 4.6 距離量測 (Distance Measurement)

### 功能說明

距離量測是閃測儀最核心的尺寸輸出方式。它將兩個幾何元素（點、線、圓）之間的距離計算為物理尺寸，並與設計公差比對。常見的距離量測類型包括：

| 量測類型 | 幾何組合 | 應用範例 |
|---|---|---|
| 點到點距離 | 點 ↔ 點 | 孔心距、特徵點間距 |
| 點到線距離 | 點 ↔ 直線 | 孔心到邊緣距離 |
| 平行線距離 | 直線 ↔ 直線 | 工件寬度、槽寬 |
| 圓心距 | 圓 ↔ 圓 | 兩孔中心距 |
| 最長/最短距離 | 輪廓 ↔ 輪廓 | 外形最大尺寸 |

### 前置條件

- 兩個幾何元素已完成擬合（直線擬合、圓擬合、或點檢測）
- 已載入校正參數（pixel size）

### 完整範例程式碼

```csharp
using HalconDotNet;
using System;

public class DistanceMeasurer
{
    /// <summary>
    /// 距離量測結果（物理單位）
    /// </summary>
    public class DistanceResult
    {
        public bool Success { get; set; }
        public double DistanceMm { get; set; }
        public double DistancePx { get; set; }
        public string ErrorMessage { get; set; } = "";
    }

    /// <summary>
    /// 計算兩條平行線之間的垂直距離（寬度量測）
    /// </summary>
    public DistanceResult MeasureLineToLine(
        double line1Row1, double line1Col1, double line1Row2, double line1Col2,
        double line2Row1, double line2Col1, double line2Row2, double line2Col2,
        double pixelSizeUmX, double pixelSizeUmY)
    {
        var result = new DistanceResult();

        try
        {
            // --- 1. 計算兩線的法向量方向 ---
            //     第一條線的方向向量
            double dx1 = line1Col2 - line1Col1;
            double dy1 = line1Row2 - line1Row1;
            double len1 = Math.Sqrt(dx1 * dx1 + dy1 * dy1);

            // --- 2. 歸一化法向量 ---
            double nx = -dy1 / len1;  // 垂直方向
            double ny = dx1 / len1;

            // --- 3. 將兩條線上的點投影到法向量上 ---
            double proj1 = line1Col1 * nx + line1Row1 * ny;
            double proj2 = line2Col1 * nx + line2Row1 * ny;

            // --- 4. 距離即為投影差值 ---
            double distancePx = Math.Abs(proj2 - proj1);

            result.DistancePx = distancePx;
            result.DistanceMm = ConvertToMm(distancePx, nx, ny, pixelSizeUmX, pixelSizeUmY);
            result.Success = true;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"線到線距離計算失敗：{ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// 使用 Halcon distance_pl 計算點到直線距離
    /// </summary>
    public DistanceResult MeasurePointToLine(
        double pointRow, double pointCol,
        double lineRow1, double lineCol1, double lineRow2, double lineCol2,
        double pixelSizeUmX, double pixelSizeUmY)
    {
        var result = new DistanceResult();

        try
        {
            // --- Halcon distance_pl: 點 (row, col) 到直線 (row1,col1)-(row2,col2) ---
            HOperatorSet.DistancePl(
                pointRow, pointCol,
                lineRow1, lineCol1, lineRow2, lineCol2,
                out HTuple distance);

            double distPx = distance.D;

            result.DistancePx = distPx;

            // --- 若直線接近垂直，使用 pixelSizeUmX；否則使用平均值 ---
            double deltaRow = lineRow2 - lineRow1;
            double deltaCol = lineCol2 - lineCol1;
            double angle = Math.Atan2(Math.Abs(deltaRow), Math.Abs(deltaCol));
            // 根據線的角度混合 pixel size
            double pixelSize = pixelSizeUmX * Math.Cos(angle) + pixelSizeUmY * Math.Sin(angle);
            result.DistanceMm = distPx * pixelSize / 1000.0;

            result.Success = true;
        }
        catch (HalconException ex)
        {
            result.ErrorMessage = $"點到線距離計算失敗：{ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// 計算兩個圓心之間的距離
    /// </summary>
    public DistanceResult MeasureCircleToCircle(
        double c1Row, double c1Col,
        double c2Row, double c2Col,
        double pixelSizeUmX, double pixelSizeUmY)
    {
        var result = new DistanceResult();

        try
        {
            double dRow = c2Row - c1Row;
            double dCol = c2Col - c1Col;
            double distPx = Math.Sqrt(dRow * dRow + dCol * dCol);

            result.DistancePx = distPx;

            // --- 使用兩個方向的 pixel size 合成 ---
            double angle = Math.Atan2(dRow, dCol);
            double pixelSize = Math.Abs(pixelSizeUmX * Math.Cos(angle))
                             + Math.Abs(pixelSizeUmY * Math.Sin(angle));
            result.DistanceMm = distPx * pixelSize / 1000.0;

            result.Success = true;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"圓心距計算失敗：{ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// 簡易 pixel 轉 mm（非等向校正版本）
    /// </summary>
    private double ConvertToMm(double distPx, double nx, double ny,
        double pixelSizeUmX, double pixelSizeUmY)
    {
        // --- 依方向混合 pixel size ---
        double absNx = Math.Abs(nx);
        double absNy = Math.Abs(ny);
        double weightX = absNx / (absNx + absNy + 1e-10);
        double weightY = absNy / (absNx + absNy + 1e-10);
        double pixelSize = pixelSizeUmX * weightX + pixelSizeUmY * weightY;

        return distPx * pixelSize / 1000.0;
    }
}
```

### 參數說明表

| 參數 | 型別 | 說明 |
|---|---|---|
| `pixelSizeUmX` | double | X 方向每個像素對應的微米數 |
| `pixelSizeUmY` | double | Y 方向每個像素對應的微米數 |

### 常見錯誤與排除方式

| 問題 | 可能原因 | 排除方式 |
|---|---|---|
| 距離跳動 | 邊緣檢測位置不穩定 | 先檢查邊緣檢測與擬合的 repeatability |
| 距離偏差 | 校正不準確 | 重新校正 pixel size |
| 線到線距離不穩定 | 兩條線不平行 | 先計算兩線角度差，若 > 0.5° 應考慮使用最寬/最窄距離 |

### 小結

- 距離量測的精度上限由邊緣檢測與擬合決定，而非計算公式本身。
- 在 ±5 µm 應用中，每次量測的 pixel size 應該來自最近的校正，而非固定常數。
- 線到線距離量測時，若兩線不平行，應明確輸出角度資訊讓使用者判斷。

---

## 4.7 角度量測 (Angle Measurement)

### 功能說明

角度量測用於計算兩條直線之間的夾角，或直線與水平/垂直方向的夾角。常見應用包括：工件邊緣的角度偏移、V 型槽的角度、定位基準的角度偏差。

### 前置條件

- 兩條直線已完成擬合（得到起點與終點座標）
- 若為水平/垂直參考角，可使用固定的參考向量

### 完整範例程式碼

```csharp
using System;

public class AngleMeasurer
{
    /// <summary>
    /// 角度量測結果
    /// </summary>
    public class AngleResult
    {
        public bool Success { get; set; }
        public double AngleDeg { get; set; }      // 兩線夾角（度）
        public double AngleRad { get; set; }      // 兩線夾角（弧度）
        public double RefAngleDeg { get; set; }   // 第一條線與水平夾角
        public string ErrorMessage { get; set; } = "";
    }

    /// <summary>
    /// 計算兩條直線之間的夾角（銳角）
    /// </summary>
    public AngleResult MeasureAngle(
        double line1Row1, double line1Col1,
        double line1Row2, double line1Col2,
        double line2Row1, double line2Col1,
        double line2Row2, double line2Col2)
    {
        var result = new AngleResult();

        try
        {
            // --- 1. 計算第一條線的方向向量 ---
            double d1Row = line1Row2 - line1Row1;
            double d1Col = line1Col2 - line1Col1;

            // --- 2. 計算第二條線的方向向量 ---
            double d2Row = line2Row2 - line2Row1;
            double d2Col = line2Col2 - line2Col1;

            // --- 3. 計算各線與水平軸的夾角 ---
            double angle1 = Math.Atan2(d1Row, d1Col);  // 弧度
            double angle2 = Math.Atan2(d2Row, d2Col);

            // --- 4. 計算夾角差值（取銳角）---
            double diff = Math.Abs(angle2 - angle1);

            // --- 確保在 0 ~ π 之間 ---
            if (diff > Math.PI)
                diff = 2 * Math.PI - diff;

            // --- 若超過 90 度，取補角（通常量測興趣在銳角或指定角）---
            if (diff > Math.PI / 2)
                diff = Math.PI - diff;

            result.AngleRad = diff;
            result.AngleDeg = diff * 180.0 / Math.PI;
            result.RefAngleDeg = angle1 * 180.0 / Math.PI;
            result.Success = true;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"角度計算失敗：{ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// 使用 Halcon angle_ll 直接計算兩線夾角
    /// </summary>
    public AngleResult MeasureAngleHalcon(
        double line1Row1, double line1Col1,
        double line1Row2, double line1Col2,
        double line2Row1, double line2Col1,
        double line2Row2, double line2Col2)
    {
        var result = new AngleResult();

        try
        {
            // --- Halcon angle_ll 計算兩直線夾角 ---
            //     角度計算方式：從第一條線旋轉到第二條線，範圍 -π ~ π
            HalconDotNet.HOperatorSet.AngleLl(
                line1Row1, line1Col1, line1Row2, line1Col2,
                line2Row1, line2Col1, line2Row2, line2Col2,
                out HalconDotNet.HTuple angle);

            double rad = angle.D;
            result.AngleRad = Math.Abs(rad);
            result.AngleDeg = Math.Abs(rad) * 180.0 / Math.PI;

            // --- 使用 atan2 分別計算各線參考角 ---
            double d1Row = line1Row2 - line1Row1;
            double d1Col = line1Col2 - line1Col1;
            result.RefAngleDeg = Math.Atan2(d1Row, d1Col) * 180.0 / Math.PI;

            result.Success = true;
        }
        catch (HalconDotNet.HalconException ex)
        {
            result.ErrorMessage = $"Halcon 角度計算失敗：{ex.Message}";
        }

        return result;
    }
}
```

### 參數說明表

| 參數 | 型別 | 說明 |
|---|---|---|
| `line*Row1/Col1` | double | 直線起點（pixel 座標） |
| `line*Row2/Col2` | double | 直線終點（pixel 座標） |

### 常見錯誤與排除方式

| 問題 | 可能原因 | 排除方式 |
|---|---|---|
| 角度跳動 | 直線長度太短，方向向量不穩定 | ROI 長度建議至少 200 px |
| 量測角度與預期差 90° | 選到補角而非銳角 | 確認直線方向與預期一致 |
| Halcon 回傳負角 | `angle_ll` 回傳有號角度 | 取絕對值即可 |

### 小結

- 角度量測的精度高度依賴直線擬合的穩定性，直線越長角度越穩定。
- 兩條接近平行的線（夾角 < 2°）時，建議改用距離量測代替角度量測。

---

## 4.8 公差判定 (Tolerance Judgment)

### 功能說明

公差判定將每個量測項目的實際量測值與設計值（nominal）及允許偏差範圍（tolerance）進行比對，輸出 OK（合格）或 NG（不合格）判定。這是閃測儀產出最終結果的關鍵步驟。

### 前置條件

- 已完成所有幾何元素的擬合與距離/角度計算
- 每個量測項目的 nominal、lowerTolerance、upperTolerance 已從 `.zcp` 取得

### 完整範例程式碼

```csharp
using System;
using System.Collections.Generic;

public class ToleranceJudger
{
    /// <summary>
    /// 單一量測項目的判定結果
    /// </summary>
    public class ItemJudgment
    {
        public string ToolId { get; set; } = "";
        public string ToolName { get; set; } = "";
        public double MeasuredValue { get; set; }
        public double Nominal { get; set; }
        public double LowerTolerance { get; set; }
        public double UpperTolerance { get; set; }
        public double Deviation { get; set; }        // 偏差值 = 實測 - nominal
        public double DeviationPercent { get; set; } // 偏差百分比
        public bool IsOk { get; set; }
        public string Unit { get; set; } = "mm";
        public string Message { get; set; } = "";
    }

    /// <summary>
    /// 整體量測判定結果
    /// </summary>
    public class OverallJudgment
    {
        public bool AllOk { get; set; }
        public List<ItemJudgment> Items { get; set; } = new();
        public int OkCount { get; set; }
        public int NgCount { get; set; }
    }

    /// <summary>
    /// 執行公差判定
    /// </summary>
    public OverallJudgment Judge(List<MeasurementItem> items)
    {
        var result = new OverallJudgment();

        foreach (var item in items)
        {
            var j = new ItemJudgment
            {
                ToolId = item.ToolId,
                ToolName = item.ToolName,
                MeasuredValue = item.MeasuredValue,
                Nominal = item.Nominal,
                LowerTolerance = item.LowerTolerance,
                UpperTolerance = item.UpperTolerance,
                Unit = item.Unit,
                Deviation = item.MeasuredValue - item.Nominal
            };

            // --- 計算偏差百分比（避免除以零）---
            if (Math.Abs(item.Nominal) > 1e-10)
                j.DeviationPercent = (j.Deviation / item.Nominal) * 100.0;
            else
                j.DeviationPercent = 0;

            // --- 判定 OK/NG ---
            double lower = item.Nominal + item.LowerTolerance;
            double upper = item.Nominal + item.UpperTolerance;

            j.IsOk = j.MeasuredValue >= lower && j.MeasuredValue <= upper;

            // --- 產生訊息 ---
            if (j.IsOk)
            {
                j.Message = $"OK (偏差 {j.Deviation:F4} {j.Unit})";
            }
            else
            {
                if (j.MeasuredValue < lower)
                    j.Message = $"NG：低於下限 {j.Deviation:F4} {j.Unit} (下限 {lower:F4})";
                else
                    j.Message = $"NG：超出上限 +{j.Deviation:F4} {j.Unit} (上限 {upper:F4})";
            }

            // --- 邊界警告（在公差邊緣，雖 OK 但需注意）---
            double tolRange = item.UpperTolerance - item.LowerTolerance;
            double margin = Math.Abs(j.Deviation) / (tolRange > 1e-10 ? tolRange : 1);
            if (j.IsOk && margin > 0.8)
            {
                j.Message += " ⚠️ 接近公差邊界";
            }

            result.Items.Add(j);

            if (j.IsOk)
                result.OkCount++;
            else
                result.NgCount++;
        }

        result.AllOk = result.NgCount == 0;
        return result;
    }
}

/// <summary>
/// 量測項目的資料模型（通常來自 .zcp 配方）
/// </summary>
public class MeasurementItem
{
    public string ToolId { get; set; } = "";
    public string ToolName { get; set; } = "";
    public double MeasuredValue { get; set; }
    public double Nominal { get; set; }
    public double LowerTolerance { get; set; } // 負值，例如 -0.005
    public double UpperTolerance { get; set; } // 正值，例如 +0.005
    public string Unit { get; set; } = "mm";
}
```

### 參數說明表

| 參數 | 型別 | 範例 | 說明 |
|---|---|---|---|
| `Nominal` | double | 50.000 | 設計名義值 (mm) |
| `LowerTolerance` | double | -0.005 | 下偏差 (mm)，通常為負值 |
| `UpperTolerance` | double | +0.005 | 上偏差 (mm)，通常為正值 |

### 常見錯誤與排除方式

| 問題 | 可能原因 | 排除方式 |
|---|---|---|
| 所有項目都 NG | 座標系定位偏移或校正參數錯誤 | 先檢查模板匹配與校正 |
| 單一項目 NG | 該 ROI 邊緣檢測失敗 | 檢查該項目的邊緣參數 |
| 偶發性 NG | 工件擺放偏差或光源波動 | 檢查 repeatability，排除隨機誤差 |

### 小結

- 公差判定只是最後的比對步驟，判定品質完全取決於前面的量測精度。
- 實務上建議在 OK/NG 之外，額外記錄「接近公差邊界」的警告（margin > 80%），讓工程師有機會提前調整。
- ±5 µm 目標下，tolRange 可能只有 10 µm，這對量測系統的穩定度要求非常高。

---

## 4.9 自動對焦 (Autofocus)

### 功能說明

自動對焦在 Z 軸行程範圍內掃描多個高度位置，找出影像最清晰的平面。這在工件厚度有變化、或更換工件時確保量測精度一致非常重要。

### 前置條件

- Z 軸運動控制已初始化（至少支援 `MoveAbsolute`）
- 已安裝相機並可取像
- 已定義對焦評估方法（通常使用 Laplacian 變異數）

### 完整範例程式碼

```csharp
using HalconDotNet;
using System;
using System.Collections.Generic;

public class AutofocusService
{
    /// <summary>
    /// 對焦結果
    /// </summary>
    public class AutofocusResult
    {
        public bool Success { get; set; }
        public double BestPositionMm { get; set; }
        public double BestScore { get; set; }
        public double FocusRangeMm { get; set; }
        public int Steps { get; set; }
        public string ErrorMessage { get; set; } = "";
    }

    /// <summary>
    /// 對焦步進的掃描點資料
    /// </summary>
    public class FocusSample
    {
        public double PositionMm { get; set; }
        public double Score { get; set; }
    }

    // --- 對焦參數 ---
    public double ScanRangeMm { get; set; } = 1.0;     // 掃描範圍 (mm)
    public double StepUm { get; set; } = 20;           // 步進間距 (µm)
    public int LaplaceMaskSize { get; set; } = 5;      // Laplace 遮罩大小
    public string LaplaceMaskType { get; set; } = "n_4_self_opt";

    /// <summary>
    /// 執行 Z 軸掃描對焦
    /// </summary>
    /// <param name="captureFunc">在指定高度取像的委派</param>
    /// <param name="centerPositionMm">掃描中心位置 (mm)</param>
    public AutofocusResult Run(Func<double, HImage> captureFunc, double centerPositionMm)
    {
        var result = new AutofocusResult();

        try
        {
            // --- 1. 計算掃描參數 ---
            double halfRange = ScanRangeMm / 2.0;
            double startPos = centerPositionMm - halfRange;
            double endPos = centerPositionMm + halfRange;
            double stepMm = StepUm / 1000.0;

            int steps = (int)(ScanRangeMm / stepMm) + 1;
            var samples = new List<FocusSample>();

            // --- 2. 逐點掃描 ---
            for (int i = 0; i < steps; i++)
            {
                double pos = startPos + i * stepMm;

                // --- 移動 Z 軸到該位置 ---
                // (這裡假設 IMotionService 已注入)
                // _motionService.MoveAbsoluteAsync(pos, 5.0).Wait();

                // --- 取像 ---
                HImage image = captureFunc(pos);

                // --- 計算清晰度分數：Laplacian 變異數 ---
                double score = CalculateFocusScore(image);

                samples.Add(new FocusSample
                {
                    PositionMm = pos,
                    Score = score
                });

                // --- 中間結果除錯用 ---
                System.Diagnostics.Debug.WriteLine(
                    $"Focus: pos={pos:F3}mm, score={score:F1}");
            }

            // --- 3. 找出最高分的位置 ---
            double bestScore = double.MinValue;
            double bestPos = startPos;

            foreach (var s in samples)
            {
                if (s.Score > bestScore)
                {
                    bestScore = s.Score;
                    bestPos = s.PositionMm;
                }
            }

            // --- 4. 二次曲線擬合求精準位置（亞步進精度）---
            int bestIdx = samples.FindIndex(s =>
                Math.Abs(s.PositionMm - bestPos) < 1e-6);

            if (bestIdx > 0 && bestIdx < samples.Count - 1)
            {
                // --- 取最高點與前後兩點做二次曲線擬合 ---
                double x0 = samples[bestIdx - 1].PositionMm;
                double x1 = samples[bestIdx].PositionMm;
                double x2 = samples[bestIdx + 1].PositionMm;
                double y0 = samples[bestIdx - 1].Score;
                double y1 = samples[bestIdx].Score;
                double y2 = samples[bestIdx + 1].Score;

                // --- 二次曲線頂點公式 ---
                double a = ((y0 - y1) * (x2 - x1) - (y2 - y1) * (x0 - x1))
                         / ((x0 * x0 - x1 * x1) * (x2 - x1) - (x2 * x2 - x1 * x1) * (x0 - x1));
                double b = ((y0 - y1) - a * (x0 * x0 - x1 * x1)) / (x0 - x1);

                if (Math.Abs(a) > 1e-15)
                {
                    double refinedPos = -b / (2 * a);
                    // --- 確保精化位置仍在掃描範圍內 ---
                    if (refinedPos >= startPos && refinedPos <= endPos)
                    {
                        bestPos = refinedPos;
                        bestScore = a * bestPos * bestPos + b * bestPos + y1;
                    }
                }
            }

            // --- 5. 移動到最佳位置 ---
            // _motionService.MoveAbsoluteAsync(bestPos, 5.0).Wait();

            result.BestPositionMm = bestPos;
            result.BestScore = bestScore;
            result.Steps = steps;
            result.Success = true;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"自動對焦失敗：{ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// 計算單張影像的對焦分數
    /// </summary>
    private double CalculateFocusScore(HImage image)
    {
        // --- 使用 Laplacian 變異數作為清晰度指標 ---
        HImage laplace = image.Laplace("absolute", LaplaceMaskSize, LaplaceMaskType);
        HOperatorSet.Intensity(new HImage(), laplace,
            out HTuple _, out HTuple deviation);
        return deviation.D;
    }
}
```

### 參數說明表

| 參數 | 型別 | 建議值 | 說明 |
|---|---|---|---|
| `ScanRangeMm` | double | 1.0 mm | 掃描總範圍，依工件高度公差決定 |
| `StepUm` | double | 20 µm | 步進間距，越小越精準但時間越長 |
| `LaplaceMaskSize` | int | 5 | 越大對低頻清晰度變化越敏感 |
| `LaplaceMaskType` | string | n_4_self_opt | 遮罩類型，一般使用預設值 |

### 常見錯誤與排除方式

| 問題 | 可能原因 | 排除方式 |
|---|---|---|
| 對焦曲線平坦無峰值 | 掃描範圍太窄或步進太大 | 增加掃描範圍或減小步進 |
| 最佳位置總在邊界 | 掃描範圍中心點設錯 | 調整 centerPositionMm |
| 對焦分數雜訊大 | 光源閃爍或相機噪聲 | 提高光源亮度或增加曝光時間 |
| 掃描時間過長 | 步進太小或範圍太大 | 先以大步進粗掃，再以小步進細掃 |

### 小結

- 對焦曲線的峰值越尖銳，表示光學系統的景深越淺、對焦越敏感。
- 二次曲線擬合可將對焦精度提高到亞步進等級（即使步進 20 µm，定位精度可達 1~2 µm）。
- 對透明工件，對焦曲線可能出現雙峰值（來自工件上下表面），需要透過 ROI 限制只取工件表面區域。

---

## 4.10 校正管理 (Calibration)

### 功能說明

校正管理負責建立像素座標到物理座標的轉換關係。未校正的系統只能輸出「像素」為單位的尺寸，經過校正後才能輸出「毫米」。校正主要包含：

1. **Pixel size 校正**：使用已知尺寸的標準件計算每個像素對應的物理長度
2. **畸變補償**（選用）：修正鏡頭畸變造成的量測偏差
3. **視野平面校正**：確保視野內各位置量測結果一致

### 前置條件

- 標準校正片（如玻璃光罩、陶瓷校正片）已準備
- 校正片上有已知尺寸的特徵（如精密圓陣列、方格圖案）
- 光源與成像條件穩定

### 完整範例程式碼

```csharp
using HalconDotNet;
using Newtonsoft.Json;
using System;

public class CalibrationManager
{
    /// <summary>
    /// 校正設定
    /// </summary>
    public class CalibrationProfile
    {
        public string ProfileId { get; set; } = "CALIB-DEFAULT";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public double PixelSizeUmX { get; set; } = 10.0;
        public double PixelSizeUmY { get; set; } = 10.0;
        public double FieldOfViewMmX { get; set; }
        public double FieldOfViewMmY { get; set; }
        public bool DistortionCorrected { get; set; } = false;
        public double CalibrationStandardMm { get; set; }
        public double MeasuredPixels { get; set; }
    }

    /// <summary>
    /// 使用標準件進行 pixel size 校正
    /// </summary>
    /// <param name="image">校正片影像</param>
    /// <param name="knownDistanceMm">標準件上已知的兩特徵距離 (mm)</param>
    /// <param name="p1Row">第一個特徵位置的 Row</param>
    /// <param name="p1Col">第一個特徵位置的 Column</param>
    /// <param name="p2Row">第二個特徵位置的 Row</param>
    /// <param name="p2Col">第二個特徵位置的 Column</param>
    public CalibrationProfile CalibratePixelSize(
        HImage image,
        double knownDistanceMm,
        double p1Row, double p1Col,
        double p2Row, double p2Col)
    {
        var profile = new CalibrationProfile();

        try
        {
            // --- 1. 計算兩特徵之間的像素距離 ---
            double dRow = p2Row - p1Row;
            double dCol = p2Col - p1Col;
            double distPx = Math.Sqrt(dRow * dRow + dCol * dCol);

            profile.MeasuredPixels = distPx;

            // --- 2. 計算 pixel size ---
            //     假設校正特徵在水平方向 ---
            //     若校正特徵有方向性，可分別計算 X/Y pixel size
            double angle = Math.Atan2(Math.Abs(dRow), Math.Abs(dCol));

            // --- 單一 pixel size（等向校正）---
            double pixelSizeUm = (knownDistanceMm * 1000.0) / distPx;

            if (angle < 0.1) // 接近水平
            {
                profile.PixelSizeUmX = pixelSizeUm;
                profile.PixelSizeUmY = pixelSizeUm;  // 暫用相同值
            }
            else if (angle > 1.4) // 接近垂直 (π/2 ≈ 1.57)
            {
                profile.PixelSizeUmY = pixelSizeUm;
                profile.PixelSizeUmX = pixelSizeUm;  // 暫用相同值
            }
            else
            {
                // --- 斜向校正，使用投影分解 ---
                double cosAngle = Math.Cos(angle);
                double sinAngle = Math.Sin(angle);
                profile.PixelSizeUmX = pixelSizeUm / cosAngle;
                profile.PixelSizeUmY = pixelSizeUm / sinAngle;
            }

            profile.CalibrationStandardMm = knownDistanceMm;

            // --- 3. 計算視野大小 ---
            HOperatorSet.GetImageSize(image, out HTuple width, out HTuple height);
            profile.FieldOfViewMmX = width.D * profile.PixelSizeUmX / 1000.0;
            profile.FieldOfViewMmY = height.D * profile.PixelSizeUmY / 1000.0;

            profile.CreatedAt = DateTime.Now;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"校正失敗：{ex.Message}", ex);
        }

        return profile;
    }

    /// <summary>
    /// 使用多個校正點（加權平均）提高校正精度
    /// </summary>
    public CalibrationProfile CalibrateMultiPoint(
        HImage image,
        double[][] knownDistancesMm,
        double[][] pixelPoints)
    {
        // --- 使用多組校正特徵，以 RANSAC 方式加權平均 ---
        double totalWeight = 0;
        double wx = 0, wy = 0;

        for (int i = 0; i < knownDistancesMm.Length; i++)
        {
            double dMm = knownDistancesMm[i][0];
            double p1Row = pixelPoints[i][0], p1Col = pixelPoints[i][1];
            double p2Row = pixelPoints[i][2], p2Col = pixelPoints[i][3];

            double dRow = p2Row - p1Row;
            double dCol = p2Col - p1Col;
            double distPx = Math.Sqrt(dRow * dRow + dCol * dCol);

            if (distPx < 1) continue;

            double pixelSize = (dMm * 1000.0) / distPx;

            // --- 根據距離加權（距離越長、權重越高）---
            double weight = distPx;
            double angle = Math.Atan2(Math.Abs(dRow), Math.Abs(dCol));

            if (angle < 0.3) // 水平方向為主
            {
                wx += pixelSize * weight;
                totalWeight += weight;
            }
            else if (angle > 1.2) // 垂直方向為主
            {
                wy += pixelSize * weight;
                totalWeight += weight;
            }
            else
            {
                wx += pixelSize * weight * 0.5;
                wy += pixelSize * weight * 0.5;
                totalWeight += weight;
            }
        }

        var profile = new CalibrationProfile();
        profile.PixelSizeUmX = wx / totalWeight;
        profile.PixelSizeUmY = wy / totalWeight;

        HOperatorSet.GetImageSize(image, out HTuple w, out HTuple h);
        profile.FieldOfViewMmX = w.D * profile.PixelSizeUmX / 1000.0;
        profile.FieldOfViewMmY = h.D * profile.PixelSizeUmY / 1000.0;
        profile.CreatedAt = DateTime.Now;

        return profile;
    }

    /// <summary>
    /// 將校正結果儲存為 JSON 供後續使用
    /// </summary>
    public void SaveCalibration(CalibrationProfile profile, string filePath)
    {
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(profile,
            Newtonsoft.Json.Formatting.Indented);
        System.IO.File.WriteAllText(filePath, json);
    }

    /// <summary>
    /// 從檔案載入校正結果
    /// </summary>
    public CalibrationProfile LoadCalibration(string filePath)
    {
        string json = System.IO.File.ReadAllText(filePath);
        return Newtonsoft.Json.JsonConvert.DeserializeObject<CalibrationProfile>(json);
    }
}
```

### 參數說明表

| 參數 | 型別 | 說明 |
|---|---|---|
| `knownDistanceMm` | double | 標準件上的已知距離，單位 mm |
| `pixelSizeUmX/Y` | double | 校正後的像素尺寸，單位 µm/px |
| `CalibrationStandardMm` | double | 校正使用的標準值 |

### 常見錯誤與排除方式

| 問題 | 可能原因 | 排除方式 |
|---|---|---|
| 校正後量測值仍然不準 | 校正標準件精度不足 | 使用更高精度的校正片（建議 10 倍於目標精度） |
| X/Y 方向 pixel size 差異過大 | 像素不是正方形（少見）或鏡頭畸變 | 檢查 sensor 規格，必要時做雙向校正 |
| 不同位置量測同一尺寸結果不同 | 鏡頭畸變 or 視野平面傾斜 | 做多點校正或使用畸變補償模型 |
| 校正值隨時間漂移 | 溫度變化導致機構熱膨脹 | 建立定期校正排程 |

### 小結

- 校正的精度直接決定了量測的絕對精度。校正片的精度應至少為目標精度的 5~10 倍。
- 建議每日開機後執行一次校正檢查（使用標準件確認 pixel size 未漂移）。
- 若預算允許，可使用 Halcon 的 `calibrate_cameras` 進行完整的相機校正（含畸變補償）。

---

> 功能 4.6 ~ 4.10 已完成。請輸入「繼續」以進入功能 4.11 ~ 4.14（座標系定位、ROI 編輯、尺寸標註、一鍵量測流程）與第五章綜合實作範例。


## 4.11 座標系定位 (Coordinate System)

### 功能說明

座標系定位是將模板匹配找到的工件位置轉換為量測參考座標系的過程。當工件在視野內有平移或旋轉時，所有 ROI 必須跟著旋轉與平移，才能正確對應到工件上的量測特徵。

### 前置條件

- 模板匹配已完成，取得工件的 row/col/angle
- 所有 ROI 位置已事先在模板建立時的參考座標系下定義

### 完整範例程式碼

```csharp
using System;

public class CoordinateSystem
{
    /// <summary>
    /// 二維座標轉換參數
    /// </summary>
    public class Transform2D
    {
        public double OffsetRow { get; set; }     // Row 方向平移 (px)
        public double OffsetCol { get; set; }     // Column 方向平移 (px)
        public double AngleDeg { get; set; }       // 旋轉角度 (度)
        public double ScaleX { get; set; } = 1.0;  // X 方向縮放
        public double ScaleY { get; set; } = 1.0;  // Y 方向縮放
    }

    /// <summary>
    /// 根據模板匹配結果建立座標轉換
    /// </summary>
    public static Transform2D CreateFromMatch(
        double refRow, double refCol, double refAngleDeg,
        double matchRow, double matchCol, double matchAngleDeg)
    {
        return new Transform2D
        {
            OffsetRow = matchRow - refRow,
            OffsetCol = matchCol - refCol,
            AngleDeg = matchAngleDeg - refAngleDeg
        };
    }

    /// <summary>
    /// 將參考座標系下的 ROI 轉換到當前影像座標系
    /// </summary>
    /// <param name="refCenterRow">參考 ROI 中心 Row</param>
    /// <param name="refCenterCol">參考 ROI 中心 Column</param>
    /// <param name="refAngleDeg">參考 ROI 角度（度）</param>
    /// <param name="transform">座標轉換參數</param>
    /// <returns>轉換後的 (row, col, angleDeg)</returns>
    public static (double row, double col, double angleDeg) TransformRoi(
        double refCenterRow, double refCenterCol, double refAngleDeg,
        Transform2D transform)
    {
        // --- 1. 將 ROI 中心從參考原點平移到匹配位置 ---
        double dx = refCenterCol - 0;  // 假設參考原點在 (0,0)
        double dy = refCenterRow - 0;

        // --- 2. 應用旋轉 ---
        double angleRad = transform.AngleDeg * Math.PI / 180.0;
        double cosA = Math.Cos(angleRad);
        double sinA = Math.Sin(angleRad);

        double rotatedCol = dx * cosA - dy * sinA;
        double rotatedRow = dx * sinA + dy * cosA;

        // --- 3. 加上匹配偏移 ---
        double newCol = rotatedCol + transform.OffsetCol;
        double newRow = rotatedRow + transform.OffsetRow;

        // --- 4. ROI 本身的旋轉也要跟著轉 ---
        double newAngle = refAngleDeg + transform.AngleDeg;

        return (newRow, newCol, newAngle);
    }

    /// <summary>
    /// 批量轉換所有 ROI
    /// </summary>
    public static (double row, double col, double angleDeg)[] TransformAllRois(
        (double row, double col, double angleDeg)[] referenceRois,
        Transform2D transform)
    {
        var results = new (double row, double col, double angleDeg)[referenceRois.Length];

        for (int i = 0; i < referenceRois.Length; i++)
        {
            results[i] = TransformRoi(
                referenceRois[i].row,
                referenceRois[i].col,
                referenceRois[i].angleDeg,
                transform);
        }

        return results;
    }

    /// <summary>
    /// 將量測結果從 pixel 轉換為以工件為參考的物理座標
    /// </summary>
    public static (double xMm, double yMm) PixelToWorkpieceMm(
        double pixelRow, double pixelCol,
        Transform2D transform,
        double pixelSizeUmX, double pixelSizeUmY)
    {
        // --- 1. 先將 pixel 座標轉回參考座標系（反旋轉）---
        double dx = pixelCol - transform.OffsetCol;
        double dy = pixelRow - transform.OffsetRow;
        double angleRad = -transform.AngleDeg * Math.PI / 180.0;

        double origCol = dx * Math.Cos(angleRad) - dy * Math.Sin(angleRad);
        double origRow = dx * Math.Sin(angleRad) + dy * Math.Cos(angleRad);

        // --- 2. 轉換為 mm（原點在工件中心）---
        double xMm = origCol * pixelSizeUmX / 1000.0;
        double yMm = origRow * pixelSizeUmY / 1000.0;

        return (xMm, yMm);
    }
}
```

### 常見錯誤與排除方式

| 問題 | 可能原因 | 排除方式 |
|---|---|---|
| ROI 位置偏移 | 座標轉換未正確應用旋轉偏移量 | 確認參考 ROI 是在模板建立時的座標系下定義的 |
| 旋轉後 ROI 跑到影像外 | 工件的旋轉角度太大 | 確認模板匹配的 angleRange 是否包含實際偏差 |
| 量測結果與實際相反方向 | X/Y 方向轉換的正負號錯誤 | 使用 Halcon `affine_trans_pixel` 驗證 |

### 小結

- 座標系轉換是模板匹配與 ROI 量測之間的橋樑，關係到所有量測項目的正確性。
- 建議在 HDevelop 中用 `vector_angle_to_rigid` + `affine_trans_region` 驗證轉換矩陣是否正確。

---

## 4.12 ROI 編輯 (Region of Interest)

### 功能說明

ROI 編輯讓操作者可以在影像上定義每個量測工具的搜尋區域。每個 ROI 包含位置、大小、旋轉角度，以及該工具專屬的邊緣檢測參數。

### 前置條件

- 已載入參考影像（模板建立時的影像）
- 已在模板匹配中建立座標系

### 完整範例程式碼

```csharp
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

/// <summary>
/// ROI 資料模型（對應 .zcp 中的 tools 陣列）
/// </summary>
public class MeasurementRoi
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "";
    public string ToolType { get; set; } = "edge"; // edge / distance / circle / angle

    // --- ROI 幾何 ---
    public double CenterRow { get; set; }
    public double CenterCol { get; set; }
    public double Length1 { get; set; } = 100;   // 半長 (px)，沿搜尋方向
    public double Length2 { get; set; } = 50;    // 半寬 (px)，垂直搜尋方向
    public double AngleDeg { get; set; } = 0;    // 旋轉角度

    // --- 邊緣檢測參數 ---
    public double Sigma { get; set; } = 1.2;
    public double Threshold { get; set; } = 25;
    public string Polarity { get; set; } = "all";
    public string EdgeSelector { get; set; } = "all";
    public string SubpixelMethod { get; set; } = "parabolic";

    // --- 公差（距離/角度工具適用）---
    public double Nominal { get; set; }
    public double LowerTolerance { get; set; }
    public double UpperTolerance { get; set; }
    public string Unit { get; set; } = "mm";

    // --- 結果 ---
    public double MeasuredValue { get; set; }
    public bool IsOk { get; set; }
}

/// <summary>
/// ROI 管理員
/// </summary>
public class RoiManager
{
    public List<MeasurementRoi> Rois { get; private set; } = new();

    /// <summary>
    /// 新增 ROI
    /// </summary>
    public void AddRoi(MeasurementRoi roi)
    {
        // --- 確保 ID 唯一 ---
        if (Rois.Exists(r => r.Id == roi.Id))
            roi.Id = Guid.NewGuid().ToString("N")[..8];

        Rois.Add(roi);
    }

    /// <summary>
    /// 刪除 ROI
    /// </summary>
    public bool RemoveRoi(string roiId)
    {
        return Rois.RemoveAll(r => r.Id == roiId) > 0;
    }

    /// <summary>
    /// 根據工具類型篩選 ROI
    /// </summary>
    public List<MeasurementRoi> GetByType(string toolType)
    {
        return Rois.FindAll(r => r.ToolType == toolType);
    }

    /// <summary>
    /// 將 ROI 轉換為 Halcon 的測量矩形（用於 measure_pos）
    /// </summary>
    public HalconDotNet.HTuple CreateMeasureHandle(
        MeasurementRoi roi, int imageWidth, int imageHeight)
    {
        HalconDotNet.HOperatorSet.GenMeasureRectangle2(
            roi.CenterRow, roi.CenterCol,
            roi.AngleDeg * Math.PI / 180.0,
            roi.Length1, roi.Length2,
            imageWidth, imageHeight,
            "nearest_neighbor",
            out HalconDotNet.HTuple measureHandle);

        return measureHandle;
    }

    /// <summary>
    /// 將所有 ROI 匯出為 JSON 供 .zcp 使用
    /// </summary>
    public string ExportToJson()
    {
        return JsonConvert.SerializeObject(Rois, Formatting.Indented);
    }

    /// <summary>
    /// 從 JSON 載入 ROI
    /// </summary>
    public void ImportFromJson(string json)
    {
        Rois = JsonConvert.DeserializeObject<List<MeasurementRoi>>(json)
               ?? new List<MeasurementRoi>();
    }
}
```

### 小結

- ROI 編輯是工程模式的核心功能，所有量測工具的位置都需要透過 ROI 編輯來設定。
- 每個 ROI 獨立儲存其邊緣檢測參數，使不同特徵可以使用不同的檢測設定。

---

## 4.13 尺寸標註 (Dimension Annotation)

### 功能說明

尺寸標註在影像上以視覺化方式呈現量測結果，包括測量線、圓形輪廓、距離箭頭、角度弧線以及 OK/NG 顏色。這對操作者快速理解量測結果非常重要。

### 前置條件

- 已取得量測結果（邊緣點、擬合線、圓、距離值）
- `HWindowControl` 已顯示影像

### 完整範例程式碼

```csharp
using HalconDotNet;

public class DimensionAnnotator
{
    private HWindow _window;

    /// <summary>
    /// 顏色定義
    /// </summary>
    public static class Colors
    {
        public const string Ok = "green";
        public const string Ng = "red";
        public const string Edge = "blue";
        public const string Roi = "cyan";
        public const string Center = "yellow";
        public const string Dimension = "white";
        public const string Warning = "orange";
    }

    public DimensionAnnotator(HWindow window)
    {
        _window = window;
    }

    /// <summary>
    /// 在視窗上繪製 ROI 矩形
    /// </summary>
    public void DrawRoi(double row, double col, double length1,
        double length2, double angleDeg, string color = Colors.Roi)
    {
        HOperatorSet.SetColor(_window, color);
        HOperatorSet.SetLineWidth(_window, 1);

        // --- 建立 ROI 矩形並顯示 ---
        using (var rect = new HRegion())
        {
            rect.GenRectangle2(row, col, angleDeg * Math.PI / 180.0,
                length1, length2);
            rect.DispRegion(_window);
        }

    /// <summary>
    /// 繪製檢測到的邊緣點
    /// </summary>
    public void DrawEdgePoints(double[] rows, double[] cols,
        string color = Colors.Edge)
    {
        HOperatorSet.SetColor(_window, color);
        HOperatorSet.SetLineWidth(_window, 2);

        // --- 使用十字標記顯示每個邊緣點 ---
        for (int i = 0; i < rows.Length; i++)
        {
            HOperatorSet.DispCross(_window, rows[i], cols[i],
                6, 0.0);  // 6px 十字，角度 0
        }
    }

    /// <summary>
    /// 繪製擬合直線（含兩端延伸）
    /// </summary>
    public void DrawLine(double row1, double col1,
        double row2, double col2, string color = Colors.Dimension)
    {
        HOperatorSet.SetColor(_window, color);
        HOperatorSet.SetLineWidth(_window, 2);
        HOperatorSet.DispLine(_window, row1, col1, row2, col2);
    }

    /// <summary>
    /// 繪製擬合圓
    /// </summary>
    public void DrawCircle(double centerRow, double centerCol,
        double radiusPx, string color = Colors.Dimension)
    {
        HOperatorSet.SetColor(_window, color);
        HOperatorSet.SetLineWidth(_window, 2);
        HOperatorSet.DispCircle(_window, centerRow, centerCol, radiusPx);
    }

    /// <summary>
    /// 繪製距離標註（兩端箭頭 + 數值文字）
    /// </summary>
    public void DrawDistance(double startRow, double startCol,
        double endRow, double endCol, double valueMm,
        bool isOk, string unit = "mm")
    {
        string color = isOk ? Colors.Ok : Colors.Ng;
        HOperatorSet.SetColor(_window, color);
        HOperatorSet.SetLineWidth(_window, 2);

        // --- 繪製連線 ---
        HOperatorSet.DispLine(_window, startRow, startCol, endRow, endCol);

        // --- 在中點顯示數值 ---
        double midRow = (startRow + endRow) / 2.0;
        double midCol = (startCol + endCol) / 2.0;

        string text = $"{valueMm:F4} {unit}";
        HOperatorSet.SetTposition(_window, midRow - 15, midCol);
        HOperatorSet.WriteString(_window, text);
    }

    /// <summary>
    /// 繪製角度弧線與數值
    /// </summary>
    public void DrawAngle(double centerRow, double centerCol,
        double startAngleDeg, double endAngleDeg, double radiusPx,
        double angleDeg, bool isOk)
    {
        string color = isOk ? Colors.Ok : Colors.Ng;
        HOperatorSet.SetColor(_window, color);
        HOperatorSet.SetLineWidth(_window, 2);

        // --- 繪製角度弧線 ---
        HOperatorSet.DispArc(
            _window,
            centerRow, centerCol,
            startAngleDeg * Math.PI / 180.0,
            endAngleDeg * Math.PI / 180.0,
            radiusPx);

        // --- 顯示角度值 ---
        double midAngle = (startAngleDeg + endAngleDeg) / 2.0 * Math.PI / 180.0;
        double textRow = centerRow + (radiusPx + 20) * Math.Sin(midAngle);
        double textCol = centerCol + (radiusPx + 20) * Math.Cos(midAngle);

        HOperatorSet.SetTposition(_window, textRow, textCol);
        HOperatorSet.WriteString(_window, $"{angleDeg:F2}°");
    }

    /// <summary>
    /// 在右上角顯示量測結果表格
    /// </summary>
    public void DrawResultTable(List<(string name, double value, string unit, bool isOk)> items)
    {
        int startRow = 20;
        int col1 = 10;
        int col2 = 120;
        int col3 = 200;
        int lineHeight = 22;

        HOperatorSet.SetColor(_window, "white");
        HOperatorSet.SetTposition(_window, startRow, col1);
        HOperatorSet.WriteString(_window, "項目");
        HOperatorSet.SetTposition(_window, startRow, col2);
        HOperatorSet.WriteString(_window, "實測值");
        HOperatorSet.SetTposition(_window, startRow, col3);
        HOperatorSet.WriteString(_window, "判定");

        for (int i = 0; i < items.Count; i++)
        {
            int y = startRow + (i + 1) * lineHeight;
            HOperatorSet.SetColor(_window, items[i].isOk ? "green" : "red");
            HOperatorSet.SetTposition(_window, y, col1);
            HOperatorSet.WriteString(_window, items[i].name);
            HOperatorSet.SetTposition(_window, y, col2);
            HOperatorSet.WriteString(_window, $"{items[i].value:F4}");
            HOperatorSet.SetTposition(_window, y, col3);
            HOperatorSet.WriteString(_window, items[i].isOk ? "OK" : "NG");
        }
    }

    /// <summary>
    /// 清除所有 overlay
    /// </summary>
    public void ClearOverlay()
    {
        HOperatorSet.ClearWindow(_window);
    }
}
```

### 小結

- 尺寸標註對操作者體驗至關重要，應提供 OK (綠色) / NG (紅色) 的即時視覺反饋。
- Halcon 的 `DispCross`、`DispLine`、`DispCircle`、`DispArc` 是 overlay 繪製的主要算子。
- 注意每次量測前應先 `ClearWindow` 清除上一次的 overlay。

---

## 4.14 一鍵量測流程 (One-Click Measurement Flow)

### 功能說明

一鍵量測流程是將前面所有功能串聯起來的狀態機。操作者只需放置工件並按下「量測」按鈕，系統自動完成從取像到 OK/NG 輸出的所有步驟。

### 前置條件

- 以上 4.1 ~ 4.13 的所有功能已完成實作與單元測試
- `.zcp` 配方已載入，包含所有 ROI 與參數設定
- 硬體服務（相機、光源、I/O）已初始化

### 完整範例程式碼

```csharp
using System;
using System.Threading.Tasks;
using System.Collections.Generic;

/// <summary>
/// 一鍵量測流程的狀態列舉
/// </summary>
public enum MeasurementState
{
    Idle,
    LoadingProgram,
    WaitingPart,
    Preparing,
    Acquiring,
    CheckingImage,
    MatchingTemplate,
    TransformingRois,
    Measuring,
    Evaluating,
    Reporting,
    Outputting,
    Completed,
    Failed
}

/// <summary>
/// 量測流程引擎 - 狀態機實作
/// </summary>
public class MeasurementWorkflow : IDisposable
{
    // --- 依賴注入的服務（由外部透過屬性設定）---
    public ICameraService Camera { get; set; }
    public ILightService Light { get; set; }
    public IMotionService Motion { get; set; }
    public IIoService Io { get; set; }
    public IMesClient Mes { get; set; }
    public IMeasurementEngine Engine { get; set; }

    // --- 內部狀態 ---
    public MeasurementState State { get; private set; } = MeasurementState.Idle;
    public string LastErrorMessage { get; private set; } = "";

    // --- 事件通知（供 UI 綁定）---
    public event Action<MeasurementState> StateChanged;
    public event Action<OverallResult> MeasurementCompleted;

    /// <summary>
    /// 執行一次完整量測（非同步）
    /// </summary>
    public async Task<OverallResult> RunOnceAsync(
        MeasurementRecipe recipe,
        HImage currentImage = null)
    {
        var result = new OverallResult();

        try
        {
            // --- 1. 載入程式 ---
            SetState(MeasurementState.LoadingProgram);
            if (recipe == null)
                throw new InvalidOperationException("未載入量測程式 (.zcp)");
            result.RecipeId = recipe.Program.Id;

            // --- 2. 等待工件到位（若 I/O 模式啟用）---
            if (Io != null)
            {
                SetState(MeasurementState.WaitingPart);
                bool partPresent = await Io.ReadInputAsync("PartPresent");
                if (!partPresent)
                {
                    // --- 等待 PartPresent 訊號（含 timeout）---
                    // 實際實作會用 WatchChanges 或輪詢
                    await Task.Delay(100);
                }
            }

            // --- 3. 準備量測條件 ---
            SetState(MeasurementState.Preparing);
            await PrepareMeasurement(recipe);

            // --- 4. 取像 ---
            SetState(MeasurementState.Acquiring);
            HImage image;
            if (currentImage != null)
                image = currentImage; // Replay 模式
            else
                image = await CaptureImage(recipe);

            result.RawImage = image;

            // --- 5. 影像品質檢查 ---
            SetState(MeasurementState.CheckingImage);
            var qc = new ImageQualityChecker();
            var qcResult = qc.Check(image);
            if (!qcResult.Pass)
            {
                result.ErrorCode = "E-IMG-001";
                result.ErrorMessage = qcResult.Message;
                SetState(MeasurementState.Failed);
                MeasurementCompleted?.Invoke(result);
                return result;
            }

            // --- 6. 模板匹配 ---
            SetState(MeasurementState.MatchingTemplate);
            var matcher = new TemplateMatcher();
            matcher.LoadModel(recipe.TemplatePath);
            var matches = matcher.FindMatches(image, minScore: recipe.TemplateMatching.MinScore);
            var match = matches[0];

            if (!match.Found)
            {
                result.ErrorCode = "E-MEA-001";
                result.ErrorMessage = "模板匹配失敗";
                SetState(MeasurementState.Failed);
                MeasurementCompleted?.Invoke(result);
                return result;
            }

            // --- 7. 座標轉換 ---
            SetState(MeasurementState.TransformingRois);
            var transform = CoordinateSystem.CreateFromMatch(
                0, 0, 0,
                match.Row, match.Column, match.AngleDeg);

            // --- 8. 執行所有 ROI 量測 ---
            SetState(MeasurementState.Measuring);
            var measureResults = await ExecuteAllRois(image, recipe.Tools, transform);

            // --- 9. 公差判定 ---
            SetState(MeasurementState.Evaluating);
            var judger = new ToleranceJudger();
            var judgment = judger.Judge(measureResults);
            result.Items = judgment.Items;
            result.OverallOk = judgment.AllOk;

            // --- 10. 報表與 MES ---
            SetState(MeasurementState.Reporting);
            await SaveAndReport(result, recipe);

            // --- 11. I/O 輸出 ---
            SetState(MeasurementState.Outputting);
            if (Io != null)
            {
                await Io.WriteOutputAsync("Pass", result.OverallOk);
                await Io.WriteOutputAsync("Fail", !result.OverallOk);
            }

            // --- 完成 ---
            result.Completed = true;
            SetState(MeasurementState.Completed);
            MeasurementCompleted?.Invoke(result);
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
            result.ErrorCode = "E-SYS-001";
            SetState(MeasurementState.Failed);
            MeasurementCompleted?.Invoke(result);
        }

        return result;
    }

    private async Task PrepareMeasurement(MeasurementRecipe recipe)
    {
        // --- 設定光源 ---
        if (Light != null)
        {
            foreach (var l in recipe.Lighting)
            {
                if (l.Strobe)
                    await Light.SetStrobeAsync(l.Channel, l.DelayUs, l.PulseWidthUs);
                else
                    await Light.SetChannelAsync(l.Channel, l.Intensity);
            }
        }

        // --- 設定 Z 軸高度 ---
        if (Motion != null && recipe.Motion != null)
        {
            await Motion.MoveAbsoluteAsync(
                recipe.Motion.ZMeasurePositionMm, 5.0);
        }
    }

    private async Task<HImage> CaptureImage(MeasurementRecipe recipe)
    {
        // --- 設定相機參數 ---
        if (Camera != null)
        {
            await Camera.SetExposureAsync(recipe.Camera.ExposureUs);
            await Camera.SetGainAsync(recipe.Camera.GainDb);
        }

        // --- 觸發取像 ---
        var frame = await Camera.GrabAsync(new GrabOptions());
        return frame.Image;
    }

    private async Task<List<MeasurementItem>> ExecuteAllRois(
        HImage image,
        List<MeasurementRoi> tools,
        CoordinateSystem.Transform2D transform)
    {
        var items = new List<MeasurementItem>();
        var detector = new EdgeDetector();
        var lineFitter = new LineFitter();
        var circleFitter = new CircleFitter();
        var distanceMeasurer = new DistanceMeasurer();
        var angleMeasurer = new AngleMeasurer();

        // --- 依序執行每個 ROI 工具 ---
        // 實際應用中，非相依的工具可平行執行
        foreach (var tool in tools)
        {
            // --- 先將 ROI 轉換到當前影像座標 ---
            var (roiRow, roiCol, roiAngle) = CoordinateSystem.TransformRoi(
                tool.CenterRow, tool.CenterCol, tool.AngleDeg, transform);

            var item = new MeasurementItem
            {
                ToolId = tool.Id,
                ToolName = tool.Name,
                Nominal = tool.Nominal,
                LowerTolerance = tool.LowerTolerance,
                UpperTolerance = tool.UpperTolerance,
                Unit = tool.Unit
            };

            switch (tool.ToolType)
            {
                case "edge":
                    var edgeResult = detector.DetectEdges(image,
                        roiRow, roiCol, tool.Length1, tool.Length2,
                        roiAngle * Math.PI / 180.0);
                    item.MeasuredValue = edgeResult.EdgePoints.Count;
                    break;

                case "distance":
                    // --- 需要兩個邊緣 ROI 才能計算距離（此處為示意）---
                    // 實際應用需要從工具關聯的兩個邊緣結果計算
                    break;

                case "circle":
                    var circleResult = circleFitter.MeasureCircle(image,
                        roiRow, roiCol, tool.Length1,
                        tool.Sigma, tool.Threshold);
                    item.MeasuredValue = circleResult.RadiusPx;
                    break;
            }

            items.Add(item);
        }

        return items;
    }

    private async Task SaveAndReport(OverallResult result, MeasurementRecipe recipe)
    {
        // --- 儲存結果、產生報表、上傳 MES ---
        // 實作取決於 IReportService 與 IMesClient
        await Task.CompletedTask;
    }

    private void SetState(MeasurementState newState)
    {
        State = newState;
        StateChanged?.Invoke(newState);
    }

    public void Dispose()
    {
        // --- 釋放資源 ---
    }
}
```

### 狀態機流程圖

```
Idle → LoadingProgram → WaitingPart → Preparing
  → Acquiring → CheckingImage → MatchingTemplate
  → TransformingRois → Measuring → Evaluating
  → Reporting → Outputting → Completed
                          ↓
                       Failed (任一階段出錯)
```

### 小結

- 一鍵量測流程的核心是狀態機設計，每個階段有明確定義的責任與錯誤處理。
- 使用 async/await 避免 UI 執行緒阻塞，並允許在長時間操作（取像、對焦、MES 通訊）時保持 UI 回應。
- 建議在每個狀態轉換時觸發事件，讓 UI 可以更新狀態指示燈與進度條。

---

> 第四章（14 個量測功能）全部完成。請輸入「繼續」以進入第五章綜合實作範例、第六章除錯指南與第七章附錄。


---

# 第五章 綜合實作範例

本章將以一個具體的工件量測案例，展示如何將第四章的各個量測功能組合起來完成完整的量測流程。

## 5.1 案例說明

**工件**：金屬墊片（washer），外徑 50 mm，內徑 25 mm，厚度 3 mm。  
**量測項目**：

| 項目 | 類型 | 名義值 | 公差 | 說明 |
|---|---|---|---|---|
| 外徑 | 圓（外圓） | 50.000 mm | ±0.010 mm | 工件外緣直徑 |
| 內徑 | 圓（內孔） | 25.000 mm | ±0.008 mm | 中心孔直徑 |
| 同心度 | 圓心距 | 0 mm | ±0.015 mm | 內外圓中心偏移量 |
| 對邊寬度 | 平行線距 | 42.000 mm | ±0.010 mm | 墊片對邊平行邊距離 |

## 5.2 完整實作程式碼

```csharp
using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FlashMeasurementSystem.Examples
{
    /// <summary>
    /// 金屬墊片量測範例 — 展示如何組合多種量測功能
    /// </summary>
    public class WasherMeasurementExample
    {
        // --- 硬體抽象（由外部注入或 Mock）---
        private readonly ICameraService _camera;
        private readonly ILightService _light;
        private readonly IMotionService _motion;

        // --- 校正參數（來自在線校正）---
        private double _pixelSizeUmX = 10.0;
        private double _pixelSizeUmY = 10.0;

        // --- 量測工具 ---
        private readonly ImageQualityChecker _qualityChecker = new();
        private readonly TemplateMatcher _matcher = new();
        private readonly EdgeDetector _edgeDetector = new();
        private readonly CircleFitter _circleFitter = new();
        private readonly DistanceMeasurer _distanceMeasurer = new();
        private readonly ToleranceJudger _judger = new();

        public WasherMeasurementExample(
            ICameraService camera,
            ILightService light,
            IMotionService motion)
        {
            _camera = camera;
            _light = light;
            _motion = motion;
        }

        /// <summary>
        /// 執行一次完整的墊片量測（同步版本，適合按鈕觸發）
        /// </summary>
        public OverallResult RunMeasurement()
        {
            return Task.Run(async () => await RunMeasurementAsync()).Result;
        }

        /// <summary>
        /// 非同步版本，適合 await 呼叫
        /// </summary>
        public async Task<OverallResult> RunMeasurementAsync()
        {
            var result = new OverallResult();
            var measurements = new List<MeasurementItem>();

            try
            {
                Console.WriteLine("[1/8] 設定光源...");
                await _light.SetChannelAsync(1, 200);  // 背光全亮

                Console.WriteLine("[2/8] 移動 Z 軸...");
                await _motion.MoveAbsoluteAsync(12.5, 5.0);

                Console.WriteLine("[3/8] 取像...");
                await _camera.SetExposureAsync(3000);
                await _camera.SetGainAsync(0.0);
                var frame = await _camera.GrabAsync(new GrabOptions());
                HImage image = frame.Image;

                Console.WriteLine("[4/8] 影像品質檢查...");
                var qcResult = _qualityChecker.Check(image);
                if (!qcResult.Pass)
                {
                    result.ErrorCode = "E-IMG-001";
                    result.ErrorMessage = $"影像品質異常：{qcResult.Message}";
                    return result;
                }

                Console.WriteLine("[5/8] 量測外圓（外徑）...");
                var outerCircle = MeasureOuterCircle(image);
                measurements.Add(new MeasurementItem
                {
                    ToolId = "OD-001",
                    ToolName = "外徑",
                    MeasuredValue = outerCircle.DiameterMm,
                    Nominal = 50.000,
                    LowerTolerance = -0.010,
                    UpperTolerance = 0.010,
                    Unit = "mm"
                });
                Console.WriteLine($"  外徑：{outerCircle.DiameterMm:F4} mm");

                Console.WriteLine("[6/8] 量測內圓（內徑）...");
                var innerCircle = MeasureInnerCircle(image);
                measurements.Add(new MeasurementItem
                {
                    ToolId = "ID-001",
                    ToolName = "內徑",
                    MeasuredValue = innerCircle.DiameterMm,
                    Nominal = 25.000,
                    LowerTolerance = -0.008,
                    UpperTolerance = 0.008,
                    Unit = "mm"
                });
                Console.WriteLine($"  內徑：{innerCircle.DiameterMm:F4} mm");

                Console.WriteLine("[7/8] 計算同心度...");
                double concentricityMm = Math.Sqrt(
                    Math.Pow(outerCircle.CenterRow - innerCircle.CenterRow, 2) +
                    Math.Pow(outerCircle.CenterCol - innerCircle.CenterCol, 2)
                ) * (_pixelSizeUmX / 1000.0);  // 轉換為 mm
                measurements.Add(new MeasurementItem
                {
                    ToolId = "CON-001",
                    ToolName = "同心度",
                    MeasuredValue = concentricityMm,
                    Nominal = 0,
                    LowerTolerance = 0,
                    UpperTolerance = 0.015,
                    Unit = "mm"
                });
                Console.WriteLine($"  同心度：{concentricityMm:F4} mm");

                Console.WriteLine("[8/8] 執行公差判定...");
                var judgment = _judger.Judge(measurements);
                result.Items = judgment.Items;
                result.OverallOk = judgment.AllOk;

                // --- 輸出結果摘要 ---
                Console.WriteLine(new string('-', 40));
                Console.WriteLine($"量測完成：{(result.OverallOk ? "✅ ALL OK" : "❌ 有 NG 項目")}");
                foreach (var item in result.Items)
                {
                    Console.WriteLine($"  {item.ToolName}：" +
                        $"{item.MeasuredValue:F4} mm " +
                        $"[nominal={item.Nominal:F4}±{item.UpperTolerance:F4}] " +
                        $"{(item.IsOk ? "OK" : "NG")}");
                }

                result.Completed = true;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                result.ErrorCode = "E-SYS-001";
                Console.WriteLine($"量測失敗：{ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// 量測外圓 — 使用四個象限的 ROI 取邊緣點
        /// </summary>
        private CircleMeasurementResult MeasureOuterCircle(HImage image)
        {
            // --- 在外圓周上設定 8 個 ROI ---
            int imgWidth = image.GetImageSize()[0].I;
            int imgHeight = image.GetImageSize()[1].I;
            double cx = imgWidth / 2.0;    // 影像中心（近似圓心）
            double cy = imgHeight / 2.0;
            double outerRadiusPx = 2500;   // 約 50mm / 10µm/px / 2

            var allEdgeRows = new List<double>();
            var allEdgeCols = new List<double>();

            // --- 在 8 個方向取邊緣點 ---
            for (int i = 0; i < 8; i++)
            {
                double angle = i * Math.PI / 4;  // 每 45 度一個
                double roiCx = cx + (outerRadiusPx - 50) * Math.Cos(angle);
                double roiCy = cy + (outerRadiusPx - 50) * Math.Sin(angle);
                double roiAngle = angle + Math.PI / 2;  // ROI 垂直於半徑方向

                // --- 邊緣檢測（從圓心向外找邊緣）---
                var edgeResult = _edgeDetector.DetectEdges(
                    image,
                    roiCy, roiCx,       // ROI 中心
                    200,                 // length1: 搜尋長度
                    30,                  // length2: ROI 寬度
                    roiAngle);           // ROI 角度

                if (edgeResult.Success)
                {
                    // --- 取最強的邊緣點（edgeSelector = first）---
                    allEdgeRows.Add(edgeResult.EdgePoints[0].Row);
                    allEdgeCols.Add(edgeResult.EdgePoints[0].Column);
                }
            }

            // --- 將邊緣點轉為 EdgeDetector.EdgePoint ---
            var edgePoints = new List<EdgeDetector.EdgePoint>();
            for (int i = 0; i < allEdgeRows.Count; i++)
            {
                edgePoints.Add(new EdgeDetector.EdgePoint
                {
                    Row = allEdgeRows[i],
                    Column = allEdgeCols[i],
                    Amplitude = 0,
                    Distance = 0
                });
            }

            // --- 擬合圓 ---
            var circleResult = _circleFitter.FitCircle(edgePoints);

            var result = new CircleMeasurementResult();
            if (circleResult.Success)
            {
                result.CenterRow = circleResult.CenterRow;
                result.CenterCol = circleResult.CenterCol;
                result.RadiusPx = circleResult.RadiusPx;
                result.DiameterPx = circleResult.RadiusPx * 2;
                result.DiameterMm = result.DiameterPx * _pixelSizeUmX / 1000.0;
                result.ResidualRmsPx = circleResult.ResidualRms;
                result.IsValid = true;
            }

            return result;
        }

        /// <summary>
        /// 量測內孔 — 使用 MeasureCircle
        /// </summary>
        private CircleMeasurementResult MeasureInnerCircle(HImage image)
        {
            int imgWidth = image.GetImageSize()[0].I;
            int imgHeight = image.GetImageSize()[1].I;
            double cx = imgWidth / 2.0;
            double cy = imgHeight / 2.0;
            double innerRadiusPx = 1250;  // 約 25mm / 10µm/px / 2

            // --- 使用圓形測量工具直接量測 ---
            var circleResult = _circleFitter.MeasureCircle(
                image,
                cy, cx,
                innerRadiusPx,
                sigma: 1.2,
                threshold: 25);

            var result = new CircleMeasurementResult();
            if (circleResult.Success)
            {
                result.CenterRow = circleResult.CenterRow;
                result.CenterCol = circleResult.CenterCol;
                result.RadiusPx = circleResult.RadiusPx;
                result.DiameterPx = circleResult.RadiusPx * 2;
                result.DiameterMm = result.DiameterPx * _pixelSizeUmX / 1000.0;
                result.ResidualRmsPx = circleResult.ResidualRms;
                result.IsValid = true;
            }

            return result;
        }
    }

    /// <summary>
    /// 圓形量測結果
    /// </summary>
    public class CircleMeasurementResult
    {
        public bool IsValid { get; set; }
        public double CenterRow { get; set; }
        public double CenterCol { get; set; }
        public double RadiusPx { get; set; }
        public double DiameterPx { get; set; }
        public double DiameterMm { get; set; }
        public double ResidualRmsPx { get; set; }
    }

    /// <summary>
    /// 整體量測結果（用於跨模組傳遞）
    /// </summary>
    public class OverallResult
    {
        public bool Completed { get; set; }
        public bool OverallOk { get; set; }
        public string RecipeId { get; set; } = "";
        public string ErrorCode { get; set; } = "";
        public string ErrorMessage { get; set; } = "";
        public HImage RawImage { get; set; }
        public List<MeasurementItem> Items { get; set; } = new();
    }
}
```

---

# 第六章 除錯與調校指南

## 6.1 量測結果異常的系統性排查步驟

當量測結果出現異常時，請按照以下順序排查，不要跳過步驟：

### Step 1：確認影像品質

```
問題：量測值不穩定或明顯錯誤
→ 檢查當前影像是否過暗、過亮或模糊
→ 執行 ImageQualityChecker 查看數值
→ 若品質異常，先調整光源或曝光
```

### Step 2：確認模板匹配

```
問題：ROI 位置偏移導致量測到錯誤特徵
→ 檢查模板匹配分數（應 > 0.7）
→ 在影像上 overlay 顯示匹配結果的邊界框
→ 若匹配偏移，重新建立模板或調整 minScore
```

### Step 3：確認 ROI 轉換

```
問題：量測結果在特定工件角度下特別差
→ 檢查座標轉換後的 ROI 是否在正確位置
→ 手動測量 ROI 與實際特徵的偏差
→ 確認 transform 的 OffsetRow/OffsetCol/AngleDeg 正確
```

### Step 4：確認邊緣檢測

```
問題：單一量測項目的數值跳動
→ 檢查該 ROI 的邊緣點分布
→ 查看 edgeResult.EdgePoints 數量與位置
→ 調整 Sigma（抗噪）或 Threshold（敏感度）
```

### Step 5：確認幾何擬合

```
問題：擬合結果明顯偏離邊緣點
→ 檢查擬合殘差 RMS（應 < 0.3 px）
→ 若 RMS 過高，改用 tukey 或 ransac 演算法
→ 檢查離群值比例
```

### Step 6：確認校正

```
問題：絕對量測值系統性偏差（例如總是偏大 0.02 mm）
→ 使用標準件重新校正 pixel size
→ 將校正後的 pixel size 與前次比較
→ 檢查是否有溫度變化造成的熱漂移
```

## 6.2 常見量測問題速查表

| 現象 | 最可能原因 | 檢查順序 |
|---|---|---|
| 所有尺寸都偏大/偏小 | Pixel size 校正偏移 | 6 → 1 |
| 特定尺寸跳動 | 該 ROI 邊緣檢測不穩定 | 4 → 3 → 2 |
| OK/NG 不穩定（邊界附近） | 量測 repeatability 不足 | 4 → 5 → 1 |
| 工件旋轉後量測失敗 | 模板匹配角度範圍不足 | 2 → 3 |
| 新工件類型首次量測全 NG | 未建立模板或 ROI 位置錯誤 | 2 → 3 → 4 |
| 量測速度太慢 | ROI 太多或參數太嚴格 | 檢討金字塔層數與搜尋範圍 |
| MES 上傳失敗 | 網路或伺服器問題 | 檢查 MES client 連線狀態 |

## 6.3 Repeatability 測試方法

確認量測系統穩定度的標準做法是重複量測同一個工件 N 次（建議 N ≥ 30），然後計算統計指標：

```csharp
public class RepeatabilityReport
{
    public double Mean { get; set; }
    public double StdDev { get; set; }
    public double Min { get; set; }
    public double Max { get; set; }
    public double Range { get; set; }       // Max - Min
    public double SixSigma { get; set; }    // 6 * StdDev（過程能力參考）
    public double GrrPercent { get; set; }  // GR&R %（相對於公差）

    public static RepeatabilityReport Calculate(
        List<double> values, double toleranceRange)
    {
        int n = values.Count;
        double mean = 0, sumSq = 0;
        double min = double.MaxValue, max = double.MinValue;

        foreach (var v in values)
        {
            mean += v;
            if (v < min) min = v;
            if (v > max) max = v;
        }
        mean /= n;

        foreach (var v in values)
            sumSq += (v - mean) * (v - mean);

        double stdDev = Math.Sqrt(sumSq / (n - 1));

        return new RepeatabilityReport
        {
            Mean = mean,
            StdDev = stdDev,
            Min = min,
            Max = max,
            Range = max - min,
            SixSigma = 6 * stdDev,
            GrrPercent = (6 * stdDev / toleranceRange) * 100.0
        };
    }
}

// --- 使用方式 ---
var values = new List<double>();
for (int i = 0; i < 30; i++)
{
    var result = washer.RunMeasurement();
    values.Add(result.Items[0].MeasuredValue); // 第一項量測值
    System.Threading.Thread.Sleep(200); // 間隔 200ms
}

var report = RepeatabilityReport.Calculate(values, 0.020); // tolRange = ±0.010mm
Console.WriteLine($"平均值：{report.Mean:F4} mm");
Console.WriteLine($"標準差：{report.StdDev:F4} mm");
Console.WriteLine($"6σ ：{report.SixSigma:F4} mm");
Console.WriteLine($"GR&R% ：{report.GrrPercent:F1}% （< 30% 為合格）");
```

### GR&R 判定標準

| GR&R% | 判定 | 說明 |
|---|---|---|
| < 10% | 優良 | 量測系統足夠 |
| 10% ~ 30% | 可接受 | 視應用要求而定 |
| > 30% | 不合格 | 量測系統需要改善 |

## 6.4 精度調校流程

```
目標 ±5 µm
  ↓
1. 確認光學系統
   → 遠心鏡頭安裝正確
   → 光源均勻穩定
   → 工作距離固定
  ↓
2. 亞像素邊緣調校
   → 在同一個邊緣重複取像 30 次
   → 檢查邊緣位置的標準差
   → 目標：< 0.1 px（約 1 µm）
  ↓
3. 校正
   → 使用標準件執行 pixel size 校正
   → 校正精度應 < 0.5 µm
  ↓
4. Repeatability 測試
   → 標準件重複量測 30 次
   → 目標：6σ < 5 µm
  ↓
5. Accuracy 驗證
   → 使用第三方校驗件
   → 目標：偏差 < ±5 µm
```

## 6.5 常見 Halcon 錯誤碼與處理

| Halcon 錯誤碼 | 說明 | 處理方式 |
|---|---|---|
| 1301 | 影像尺寸不匹配 | 檢查 HImage 的 width/height 是否與預期一致 |
| 1401 | 找不到模型 | 確認 .shm 檔案路徑，或模型已正確建立 |
| 3201 | 記憶體不足 | 檢查是否有 HImage/HRegion 未釋放，使用 Dispose |
| 4001 | 參數無效 | 檢查傳入 Halcon 算子的參數型別與範圍 |
| 5002 | 影像為空 | 確認 image 已成功載入或擷取 |
| 5210 | 測量 ROI 超出影像邊界 | ROI 位置可能因座標轉換後超出影像範圍 |


---

# 第七章 附錄

## 7.1 常用 Halcon 算子速查表

### 影像基礎

| 算子 | 功能 | 使用頻率 |
|---|---|---|
| `ReadImage` | 從檔案讀取影像 | ⭐⭐⭐ |
| `GetImageSize` | 取得影像寬高 | ⭐⭐⭐ |
| `ReduceDomain` | 以 ROI 裁切影像 | ⭐⭐⭐ |
| `CropDomain` | 移除影像多餘邊界 | ⭐⭐ |
| `ZoomImageFactor` | 縮放影像顯示 | ⭐⭐ |

### 影像預處理

| 算子 | 功能 | 使用頻率 |
|---|---|---|
| `MeanImage` | 均值濾波平滑 | ⭐⭐ |
| `GaussImage` | 高斯濾波平滑 | ⭐⭐⭐ |
| `MedianImage` | 中值濾波（去椒鹽雜訊） | ⭐⭐ |
| `Laplace` | Laplace 邊緣強化 | ⭐⭐⭐ |
| `Illuminate` | 照明不均補償 | ⭐ |
| `ScaleImage` | 灰階線性拉伸 | ⭐⭐ |

### 邊緣檢測

| 算子 | 功能 | 使用頻率 |
|---|---|---|
| `EdgesSubPix` | Canny 亞像素邊緣 | ⭐⭐⭐ |
| `MeasurePos` | 一維測量邊緣 | ⭐⭐⭐ |
| `MeasurePairs` | 同時檢測兩邊邊緣對 | ⭐⭐⭐ |
| `MeasureCircle` | 圓形測量 | ⭐⭐⭐ |
| `GenMeasureRectangle2` | 建立矩形測量 ROI | ⭐⭐⭐ |
| `GenMeasureCircle` | 建立圓形測量 ROI | ⭐⭐ |

### 模板匹配

| 算子 | 功能 | 使用頻率 |
|---|---|---|
| `CreateShapeModel` | 建立形狀模型 | ⭐⭐⭐ |
| `FindShapeModel` | 尋找形狀模型 | ⭐⭐⭐ |
| `WriteShapeModel` | 儲存模型到檔案 | ⭐⭐⭐ |
| `ReadShapeModel` | 從檔案讀取模型 | ⭐⭐⭐ |
| `SetShapeModelParam` | 設定搜尋參數 | ⭐⭐ |

### 幾何擬合

| 算子 | 功能 | 使用頻率 |
|---|---|---|
| `FitLineContourXld` | 擬合直線 | ⭐⭐⭐ |
| `FitCircleContourXld` | 擬合圓形 | ⭐⭐⭐ |
| `FitRectangle2ContourXld` | 擬合矩形 | ⭐⭐ |
| `DistancePl` | 點到直線距離 | ⭐⭐⭐ |
| `DistancePc` | 點到圓週距離 | ⭐⭐ |
| `AngleLl` | 兩直線夾角 | ⭐⭐ |

### 影像顯示

| 算子 | 功能 | 使用頻率 |
|---|---|---|
| `DispObj` | 顯示影像物件 | ⭐⭐⭐ |
| `DispLine` | 繪製直線 | ⭐⭐⭐ |
| `DispCircle` | 繪製圓 | ⭐⭐⭐ |
| `DispCross` | 繪製十字標記 | ⭐⭐⭐ |
| `DispArc` | 繪製弧線 | ⭐⭐ |
| `DispRegion` | 顯示區域 | ⭐⭐ |
| `SetColor` | 設定繪製顏色 | ⭐⭐⭐ |
| `SetLineWidth` | 設定線寬 | ⭐⭐ |

### 座標轉換

| 算子 | 功能 | 使用頻率 |
|---|---|---|
| `VectorAngleToRigid` | 建立剛體轉換矩陣 | ⭐⭐⭐ |
| `AffineTransPixel` | pixel 座標轉換 | ⭐⭐⭐ |
| `AffineTransRegion` | ROI 區域轉換 | ⭐⭐⭐ |
| `AffineTransImage` | 影像旋轉/平移 | ⭐⭐ |

### 校正

| 算子 | 功能 | 使用頻率 |
|---|---|---|
| `CalibrateCameras` | 相機校正 | ⭐ |
| `GenRadialDistortionMap` | 徑向畸變補償 | ⭐ |
| `ChangeRadialDistortionImage` | 套用畸變校正 | ⭐ |
| `ImageToWorldPlane` | 影像轉世界座標 | ⭐ |

## 7.2 術語中英對照表

| 英文 | 中文 | 說明 |
|---|---|---|
| Accuracy | 準確度 | 量測值與真實值的偏差 |
| Autofocus | 自動對焦 | 自動尋找 Z 軸最佳清晰度位置 |
| Calibration | 校正 | 建立像素到物理單位的轉換 |
| Coaxial Light | 同軸光 | 與相機同光路的光源 |
| Contrast | 對比度 | 影像中明暗差異的程度 |
| Depth of Field | 景深 | 成像清晰的深度範圍 |
| Distortion | 畸變 | 鏡頭造成的影像幾何變形 |
| Edge Detection | 邊緣檢測 | 找出影像中灰階劇變的位置 |
| FOV (Field of View) | 視野 | 相機一次拍攝的真實範圍 |
| Gage R&R (GR&R) | 量測系統分析 | 評估量測系統重複性與再現性 |
| Geometric Fitting | 幾何擬合 | 將邊緣點擬合成幾何元素 |
| Global Shutter | 全域快門 | sensor 所有 pixel 同時曝光 |
| Handshake | 握手通訊 | I/O 互鎖流程 |
| Nominal | 名義值 | 設計圖面上的理論尺寸 |
| Pixel Size | 像素尺寸 | 每個像素對應的微米數 |
| Polarity | 邊緣極性 | 邊緣的亮→暗或暗→亮方向 |
| RANSAC | 隨機取樣共識法 | 抗離群值的擬合演算法 |
| Recipe | 配方 | 量測程式與參數的集合 |
| Repeatability | 重複性 | 同一條件下重複量測的一致性 |
| Resolution | 解析度 | 可分辨的最小細節 |
| ROI | 感興趣區域 | 量測工具作用的影像區域 |
| Sigma | 平滑係數 | 高斯濾波的標準差 |
| Subpixel | 亞像素 | 小於一個像素的精度 |
| Telecentric Lens | 遠心鏡頭 | 平行投影的特殊鏡頭 |
| Template Matching | 模板匹配 | 以樣板影像尋找工件位置 |
| Threshold | 門檻值 | 區分邊緣與雜訊的灰階梯度值 |
| Tolerance | 公差 | 設計尺寸容許的偏差範圍 |

## 7.3 推薦讀物與參考資源

- **Halcon 官方文件**：MVTec HALCON Reference Manual（所有算子的完整說明）
- **Halcon Solution Guide I / II**：基礎與進階影像處理實例
- **Machine Vision and Applications**：機器視覺教科書，適合深入了解理論
- **Halcon .NET 程式設計指南**：MVTec 提供的 .NET 整合說明
- **ISO 10360**：座標量測機驗收標準，可參考其量測不確定度評估方法
- **AIAG MSA 手冊**：量測系統分析（Measurement System Analysis）標準參考

---

> 本手冊撰寫完成。檔案：`FlashMeasurementSystem_開發手冊.md`
>
> 若需補充特定章節或調整範例程式碼，請告知。

