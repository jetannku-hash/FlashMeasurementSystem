# 工作彙整 2026-07-14（下半場）：DXF/CAD 輪廓度比對 — 從 brainstorm 到 v1.1 merge

> 承接同日上半場 deep-audit 批 A/B（`2026-07-14_深度稽核...`）。本場從「接下來做什麼」的路線討論，落地**第一個 Phase 5 應用方案：DXF/CAD 輪廓度比對**。全程走 brainstorm → spec → plan → subagent-driven-development，含 v1 四層 + v1.1 視覺化，GUI 驗收後 `--no-ff` 併回 main 並 push（merge `5b67bae`）。設計/計畫：`docs/superpowers/specs/2026-07-14-dxf-contour-comparison-design.md`、`docs/superpowers/plans/2026-07-14-dxf-contour-comparison.md`。

## 一、起源與路線確認

盤點未完成項目時，使用者想起「DXF 比對」。查證：曾在 Phase 2 評估「Option B — CAD/DXF master import」並**明確延後**（`2026-06-30-gsd-phase1-2-design.md` Locked Decisions），另在應用方案庫建議文件列「輪廓比對」。先把它正式登記進 `docs/ROADMAP.md` Phase 5（標「像素空間可做／mm 準度待硬體」），再決定實作。

## 二、S0 設計 spike（brainstorming skill）

**HALCON 路徑驗證**（對照離線 17.12 reference，reference 在方案上一層 `halcon_pdf/reference/`）：`read_contour_xld_dxf`(L46458，**只吃 AC1009/R12**、x→col/y→row 無翻轉)、`create_scaled_shape_model_xld`(L113760)、`find_scaled_shape_model`(L115459)、`distance_contours_xld`(L155816，逐點距離存 `distance` 屬性)。路徑可行、免硬體。

**四個設計決策（逐題）**：
| # | 決策 |
|---|---|
| 目的 | 輪廓度 **PASS/FAIL**（max\|dev\|+平均/RMS 對 T） |
| 對位 | DXF **自建 scaled shape model** 定位，scale 吸收 mm→px、免校正 |
| 實際輪廓 | 對位後自動 `edges_sub_pix`（框帶內） |
| 整合 | **獨立動作**（不進配方/一鍵） |

誠實補了兩個技術細節：**scale 需 pixel-size 當粗略種子**（免準度但要收斂搜尋）；**邊緣須框帶濾雜訊**（否則內部特徵被當巨大偏差）。

## 三、subagent-driven-development（v1，Tasks 1–4）

每 task 派全新 implementer subagent + 審查（Task 1/3 完整審查、2/4 coordinator 驗證）：
1. **Domain**：`DxfComparisonParameters/Result` + 純 `DxfDeviationEvaluator`（無號、邊界含 T、含測試）。
2. **Application**：`IDxfContourComparer<TImage>`（泛型保 HALCON-free）+ Fake 契約測試。
3. **Halcon adapter**（opus）：完整管線。opus 對照 reference **修正我起始碼多處簽章錯誤** + 一個實質正確性問題：對位不能變換原始 DXF 輪廓，須用 `get_shape_model_contours`（正規化到參考點 0,0）再 scale→rotate→translate。審查確認簽章/幾何/釋放全對。
4. **UI**：獨立 `DxfComparisonForm`（手寫版面）+ toolbar「DXF 比對...」鈕（**程式碼加、不碰 Designer**，避開 landmine）。

## 四、合成 fixture + GUI 驗收（v1）

為 de-risk GUI 驗證，用 Python/PIL 生成 known-good fixture（DXF 與影像同一份頂點、以 `row=DXF-y` 慣例渲染確保匹配）：`data/dxf/test_house.dxf`（R12 closed POLYLINE，house 五邊形）、`data/images/dxf_house_ok.png`（應 PASS）、`_ng.png`（右邊 8mm bump，應 FAIL）。使用者 GUI 驗收 v1 通過。

## 五、v1.1 視覺化（使用者回饋驅動）

使用者指出兩個專業度缺口：①載入 DXF 無圖形回饋、②結果只有文字。討論後定案 **1a ghost 預覽 + 兩色綠/紅**（連續 heatmap 延 v2），且**先不合併、加進同一 branch**。spec 增 §11、計畫增 Task 5/6：
- **Task 5**（opus）：adapter 加 `LoadNominalContour`（預覽）+ `CompareWithOverlay`（回傳對位標稱/實際邊/超差點 iconic），`Compare` 委派並釋放。**Domain/介面零改動**。所有權切分：iconic 移交呼叫端（含 FAILED 時非 null）。
- **Task 6**：`OverlayAnnotator` 加 `DrawContour`/`DrawContourFitted`（ghost 置中縮放）；Form 載入畫 ghost、執行畫**藍標稱+綠實際+紅超差十字**（上限 200 抽樣）、關閉清 overlay+釋放。
GUI 複驗通過：載入見 ghost、OK 圖藍綠貼合無紅 PASS、NG 圖 bump 亮紅 FAIL。

## 六、收尾（merge + push）

`--no-ff` 併回 main（`5b67bae`）→ 測試綠 → push → 刪分支。**合併前攔下** VS 自動加的 csproj `<SubType>Form</SubType>`（良性、與同儕 Form 一致，判斷後提交）。fixture 依使用者指示**留本地**（`data/dxf/` 未追蹤、PNG gitignore、產生器留 scratchpad）。ROADMAP Phase 5 標「DXF v1+v1.1 已交付」。

## 七、關鍵約束 / 教訓（供後續）

- **DXF 只吃 AC1009/R12**：現代 CAD 需另存 R12，否則 `read_contour_xld_dxf` 讀不到。
- **座標**：x→col、y→row、z 忽略、**無翻轉**（合成影像須以此慣例渲染才匹配）。
- **scaled shape model 對位**：用 `get_shape_model_contours`（正規化）而非原始輪廓；scale 需種子收斂。
- **iconic 所有權**：`CompareWithOverlay` 移交呼叫端，Form 以欄位持有 + `DisposeIconics`（重跑/關閉），FAILED 時非 null 也要清。
- **VS 檔案 churn**：GUI 驗證期 VS 會改 csproj（本次 `SubType`）；合併前必 `git status` 核對。
- subagent-driven 對 HALCON 這種需邊做邊查 reference 的工作很有效（opus 自行修正了計畫起始碼的簽章與對位錯誤）。

## Commit 一覽（本場）
v1：`b15a7e0`(Domain)→`3b20c39`(Application)→`d3d7f64`(Halcon adapter)→`f3ab841`(UI) → docs `82dc981`/`3afdb0a`/`b2655bf`(ROADMAP/spec/plan)。
v1.1：`b83ea3c`(spec+plan §11)→`37eeb9a`(adapter iconic)→`9eb4608`(UI overlay)→`2148975`(csproj SubType)。
收尾：`5b67bae`(--no-ff merge)→`0a00605`(ROADMAP 已交付)。

## 八、狀態 / 下一步

- `origin/main` = 本地 = `0a00605`，工作樹 clean（僅 `data/dxf/` 本地未追蹤）。
- **v2 後續**（未開）：有號偏差(缺料/毛邊方向)、雙向距離抓缺料/缺特徵、連續色階 heatmap、配方整合+一鍵+報表、mm 絕對準度(需硬體校正)、非 R12 自動轉換。
- Phase 5 其餘方案（齒輪齒數/齒距、PCD、pin 間距、直徑）仍未開始。
