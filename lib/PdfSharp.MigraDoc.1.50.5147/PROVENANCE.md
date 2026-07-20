# PDFsharp + MigraDoc (GDI+ build) 1.50.5147 — vendored

用途：Phase 4 PDF 量測報表產出（`FlashMeasurementSystem.Reporting` 的 PDF writer）。

- **來源**：NuGet 套件 `PDFsharp-MigraDoc-GDI` 1.50.5147 的 `lib/net20/`。
- **授權**：MIT（商用安全）。
- **為何是 GDI+ 版**：本專案 UI 是 WinForms／System.Drawing，GDI+ build 才是相符的變體（另一個是 WPF build）。
- **為何 vendored 而非 NuGet**：與 `lib/Newtonsoft.Json.13.0.3/` 同慣例——非 SDK 式 csproj 的 PackageReference 在 `dotnet build` 下不會注入編譯引用；vendored DLL 同時相容 `dotnet build` 與 VS msbuild，且離線可重現。
- **只放需要的三個**：`PdfSharp-gdi.dll`（PDF 核心）、`MigraDoc.DocumentObjectModel-gdi.dll`（文件/表格模型）、`MigraDoc.Rendering-gdi.dll`（渲染成 PDF）。刻意不放 `PdfSharp.Charting-gdi.dll` 與 `MigraDoc.RtfRendering-gdi.dll`（用不到）。

## ⚠️ 中文字型限制（實測結論，勿踩）

PdfSharp 1.50 的字型解析器**無法處理 TrueType Collection（`.ttc`）**，會拋
`InvalidOperationException: Error while parsing an OpenType font.`

Windows 的中文字型幾乎全是 .ttc，實測**全數失敗**：
微軟正黑體 `msjh.ttc`、細明體/新細明體 `mingliu.ttc`、`SimSun` `simsun.ttc`、`msyh.ttc`。

**可用者只有單檔 `.ttf` 的字型**。本機唯一的繁體中文單檔 TTF 是
**標楷體 DFKai-SB（`C:\Windows\Fonts\kaiu.ttf`）**，實測可正常嵌入
（含中文的 PDF 38.8KB vs 同版面純拉丁 26.0KB，差額即 CJK glyph 子集）。

因此報表字型固定使用 **DFKai-SB**。若日後要換字型，必須先確認該字型是單檔 .ttf，
或改用能處理 .ttc 的其他 PDF 方案。
