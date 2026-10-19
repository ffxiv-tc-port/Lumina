<!-- ffxiv-tc-port 繁體中文說明開始 -->
# Lumina(台服 fork)

由 NotAdam 開發的函式庫，用來讀取與解析 FFXIV 遊戲資料檔（sqpack、Excel 表）。

## 台服 fork 的目的

跟隨艦隊釘 API13 / net9，並降低插件讀資料表時的記憶體配置。

## 與上游的差異

- **資料表物件重用**：`GetSheet`／`GetSubrowSheet` 對同一張表、同一種語言重用同一個型別表物件，不再每次呼叫
  都新配置（讀 `RowRef.Value`／`SubrowRef.Value` 時也跟著不配置）；插件組件裡的列型別刻意不快取，
  插件照常可以卸載。
- **連結類型**：`LinkPayloadType` 補上 `EventTutorial`、`Emote`（上游的新增）。
- **CI**：release workflow 改成能在艦隊的自架 runner 上跑。
- 分支歷史裡另含 TC 社群開發者 AtmoOmen 帶入的既有修改（例如 `ExcelModule.GetExcelSheet<T>()` 的
  語言參數無效化、SeString 文字段落擴充），那些不是我們寫的，也沒有被回退。

## 誰在用它

**絕大多數插件是透過 Dalamud 本體間接使用它**——本 fork 編出來的 `Lumina.dll` 隨 Dalamud 出貨。

目前以子模組直接引用的只有 `Dalamud`。（`FFXIVClientStructs` 的 `ExcelGenerator` 工具用的是 NuGet 上的
原版 Lumina 套件，不經過這份 fork。）

---

以下為上游原始 README，內容未經修改：

<!-- ffxiv-tc-port 繁體中文說明結束 -->

# Lumina

[![Nuget](https://img.shields.io/nuget/v/Lumina)](https://www.nuget.org/packages/Lumina/) ![Build](https://github.com/NotAdam/Lumina/workflows/Build/badge.svg?branch=master)

Lumina is a small & high-performance library for interacting with FINAL FANTASY XIV game data in a convenient manner.

Looking for Excel sheets? They've moved [here](https://github.com/NotAdam/Lumina.Excel).

Documentation is located [here](https://lumina.xiv.dev/docs/intro.html).

## Usage

See the `Lumina.Example` project for a quick start, otherwise for more comprehensive usage examples, check the [getting started guide](https://lumina.xiv.dev/docs/guides/basic-usage.html).