# GD&T v1 — T10 GUI 驗證流程

> 對象：`feature/gdt-tolerance-v1`。用 `data/images/gdt_*.png` 合成圖逐項目視。
> 性質：驗「管線接線」（建工具→量測→判定→overlay→結果表→CSV），**非**驗真實零件準度。
> 提醒：HALCON 次像素邊緣自帶誤差 → 數值不會精確等於合成圖設定值；**以「量級對 + OK/NG 在正確 T 翻轉」為準**，不要求絕對相等。

---

## 0. 前置準備

1. 關閉舊 app，build x64：
   ```
   dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64
   ```
2. 啟動：`.\src\FlashMeasurementSystem.App.Wpf\bin\x64\Debug\FlashMeasurementSystem.App.Wpf.exe`
3. 到「量測」分頁，記下 **Pixel Size X/Y（µm）** 的值（設為等向，例如 X=Y=10）。
   - **偏差換算**：`偏差(mm) = 偏差(px) × PixelSize(µm) / 1000`。例：PixelSize=10 時，12px → 0.12mm。
   - 設 T 時就以此為基準。最穩的驗法：先量一次看結果表的偏差值，再把 T 設在它的上下，看 OK↔NG 翻轉。

---

## 1. 共通操作

### 建立元素工具（circle / line）並取 ROI
1. 載入對應合成圖（載入影像）。
2. 按「編輯…」開啟配方編輯器（**需先載入影像**；編輯器與主視窗共用同一影像，ROI 畫在主視窗）。
3. 編輯器按 **New**（新配方）。**不要按設參考姿態**——這些配方無模板，HasReferencePose 須為 false，Run Recipe 才能直接跑。
4. 按 **+ Circle** 或 **+ Line** 加元素工具 → 在左側清單選它 → 按 **Take ROI from Image** → 在主視窗影像上拉一個矩形框住目標特徵。
   - **circle**：框要**完整包住整個圓周**（真圓度/同心度靠整圈點雲，框成弧段會算錯）。
   - **line**：框成**沿著邊緣的長條**（長軸對齊邊緣，偵測器以長寬比自動判方向）。

### 建立 GD&T 工具
1. 按對應按鈕（**+ 真圓度 / + 真直度 / + 平行度 / + 垂直度 / + 同心度**）。
2. 選它 → 右側出現「**GD&T 形位公差**」群組（特性唯讀）+「**Reference Tools**」群組，且「Tolerance」雙邊群組**隱藏**。
3. 設 **Ref1 = 量測元素**；平行/垂直/同心再設 **Ref2 = 基準元素**（順序有意義）。下拉只列**合格型別**（真圓/同心列 circle；真直/平行/垂直列 line）。
4. 設「**公差帶 T (mm)**」。
5. **Save / Save As** 存成 .zcp（存檔後 callback 會回寫主視窗，Run Recipe 立即可用）。

### 執行
- **Run Recipe**：直接在目前影像跑配方 → 畫 overlay + 更新結果表（不做影像品質檢查、不需模板）。**主要用這個驗量測/overlay/結果表。**
- **一鍵量測**：跑完整流程並寫 CSV。合成圖請先勾「**跳過影像品質檢查**」核取框（合成平面圖可能過不了 IQC）。**用這個驗 CSV。**

---

## 2. 逐項驗證

> 每項：建元素 → 建 GD&T → Run Recipe → 看 overlay + 結果表 → 調 T 驗 OK/NG 翻轉。

### 2.1 同心度（gdt_concentricity.png，墊圈：外盤+偏心內孔）
- 建 circle「outer」框外盤外緣、circle「inner」框內孔。各 Run 一次確認兩圓擬合正確（綠/黃圓貼合邊緣）。
- 建 **同心度**：Ref1=outer，Ref2=inner，T 先設大（如 0.30）。
- **預期**：偏差 ≈ 12px×PixelSize（內孔偏心 6px → 直徑帶 12px）；overlay 畫**兩圓心間的短連線**；結果表有此列。
- 把 T 調到偏差值以下 → 變 **紅(NG)**；調回以上 → **綠(OK)**。

### 2.2 平行度（gdt_parallelism.png，上水平 + 下傾斜 5°）
- 建 line「top」框上條、line「bottom」框下條。
- 建 **平行度**：Ref1=top（量測），Ref2=bottom（基準），T 設小（如 0.10）。
- **預期**：偏差 ≈ 線長×sin5°×PixelSize（線長約 520px → 約 45px×PixelSize）；overlay 畫量測線到基準線的連線；T 太小 → NG。
- 反向驗證：若另建一個平行度 Ref 指向兩條幾乎同向的線，偏差應接近 0。

### 2.3 真圓度（gdt_roundness_lobed.png，3 瓣，峰對峰 ~8px）
- 建 circle 框整個瓣形盤。
- 建 **真圓度**：Ref1=該圓，T 先設大。
- **預期**：偏差 ≈ 8px×PixelSize（瓣形峰對峰）；結果表有列（**無專屬幾何 overlay**，盤的擬合圓由 circle 工具自身畫出）。
- 對照：再用一張正圓（可自畫或框 concentricity 的外盤）做真圓度，偏差應明顯更小。

### 2.4 垂直度（gdt_perpendicularity.png，一橫一直偏 3°）
- 建 line「horiz」框橫條、line「vert」框直條。
- 建 **垂直度**：Ref1=vert（量測），Ref2=horiz（基準），T 設小。
- **預期**：偏差 ≈ 線長×sin3°×PixelSize；overlay 連線；理想垂直時應接近 0。

### 2.5 真直度（gdt_straightness_bow.png，上緣弓幅 ~5px）
- 建 line 框上緣弓形邊。
- 建 **真直度**：Ref1=該線。
- **預期**：偏差為**擬合殘差 RMS**（v1 近似，**非**峰對峰，故會**小於** 5px×PixelSize，屬正常）；結果表有列。
- 對照：框一條直邊（如 parallelism 的上條）做真直度，偏差應更小。

---

## 3. 通用檢查清單

- [ ] 編輯器工具列出現 **5 顆新按鈕**（真圓度/真直度/平行度/垂直度/同心度）。
- [ ] 選 GD&T 工具時：顯示「GD&T 形位公差」群組 + Reference Tools；**隱藏**雙邊 Tolerance 群組。選回 circle/line 時相反。
- [ ] Ref 下拉**只列合格型別**（真圓/同心→circle；真直/平行/垂直→line）。
- [ ] **存檔→重載配方**後，GD&T 工具的特性、T、Ref **都還在**（驗 `DeepCopyTool` 修正；漏複製會在此露餡）。
- [ ] **一鍵量測**（勾跳過 IQC）後，`data/reports/measure_YYYYMMDD.csv` 有 GD&T 列：`IsOk` 正確、`MeasuredValue`=偏差、`LowerLimit=0`、`UpperLimit=T`、`Unit=mm`。
- [ ] 主視窗結果列 **OK/NG 計數**涵蓋 GD&T 工具（綠/紅）。
- [ ] 故意把某 GD&T 工具 Ref 設成錯型別（如真圓度指向 line）→ 結果顯示「需 circle」之類訊息且不擋其他工具。

---

## 4. 注意事項 / 常見陷阱

1. **不要設參考姿態**：這些配方無模板，設了 Run Recipe 會要求先做模板匹配而擋住。
2. **circle ROI 要包整圈**：真圓度/同心度用整圈點雲；框成弧段 → max-min 失真、圓心偏。
3. **Ref 順序**：Ref1=量測、Ref2=基準。平行/垂直的帶寬用「量測線(Ref1)的長度」；指錯會改變量級。同心度對稱、順序不影響值。
4. **偏差是 mm，受 PixelSize 影響**：改 PixelSize 偏差等比例變。驗證以「量級對 + OK/NG 在正確 T 翻轉」為準。
5. **line ROI 要框「單一直邊」，勿框住整條厚 bar**：厚特徵整個被框住時 edges_sub_pix 回封閉雙邊輪廓。平行/垂直靠 PCA 仍能取得正確「角度」（中線平行於兩邊），但**真直度**用殘差 RMS——框住整條 bar 時 RMS 反映的是「半厚度」而非真直度。量真直度時 ROI 應只跨單一直邊。
6. **真直度是 RMS 近似**：會小於肉眼弓幅，是 v1 預期（真值峰對峰留 v2）。別當成 bug。
6. **真圓度 max-min 對離群點敏感**：若邊緣抓到雜點使偏差異常大，調高 edge Threshold 或縮 ROI 再試。
7. **一鍵量測在合成圖**：務必勾「跳過影像品質檢查」，否則平面合成圖可能在 IQC 階段就失敗、不產 CSV。
8. **元素工具要先在同配方內**：GD&T 於 Pass 1.7 跑，參照的 circle/line 必須同配方且量測成功；否則顯示「找不到參考元素/參考元素未量測」。

---

## 5. 通過標準
- 5 項各自：overlay 合理、偏差量級對、OK/NG 在正確 T 翻轉。
- 通用清單全勾（尤以**存載往返保留 GD&T** 與 **CSV 正確**兩項為關鍵回歸點）。
- 全過 → merge 回 main（--no-ff）、刪分支、ROADMAP/spec/plan 一併 commit、更新每日進度。
- 任一項 overlay/數值/判定不對 → 回報具體現象，修正後重驗該項。
