# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 專案簡介

本 repo 是初級 AI Agent 實作培訓套件，包含兩個部分：

- `documents/`：培訓指南、活動說明、參考文件（Markdown，不含可執行程式碼）
- `training-repo/`：練習用的 **OrderHub** 訂單管理系統（公司內部系統，業務用於建立/查詢訂單、管理商品與客戶）

## 技術棧

- .NET 8 / ASP.NET Core MVC（Razor Views + Bootstrap 5，前端靜態資源全部本地，不依賴 CDN）
- EF Core 8 + SQL Server（本機安裝，非 Docker）
- xUnit 測試（EF Core InMemory，**不需要** SQL Server）

## 常用指令

所有指令在 `training-repo/` 目錄下執行：

```powershell
dotnet build                          # 建置
dotnet test                           # 跑全部測試（InMemory，不需 SQL Server）
dotnet run --project src/OrderHub.Web # 啟動網站
```

重置資料庫（回到初始種子資料）：

```powershell
dotnet ef database drop -f -p src/OrderHub.Infrastructure -s src/OrderHub.Web
dotnet run --project src/OrderHub.Web   # 重新 migrate + seed
```

## 分層架構與慣例

```
training-repo/src/
├── OrderHub.Web/         # Controller / ViewModel / View（只做接線與顯示）
├── OrderHub.Core/        # Domain model、service 介面、商業邏輯
└── OrderHub.Infrastructure/  # EF Core DbContext、Repository、Migrations、DbSeeder
tests/
└── OrderHub.Tests/       # xUnit（InMemory）
```

強制慣例（違反即算錯誤）：

- **Controller 保持薄**：不含商業邏輯，只轉接 service 結果；操作結果用 `TempData["Success"] / TempData["Error"]` 傳遞
- **只有 Repository 碰 `DbContext`**：Service / Controller 不可直接使用 EF Core
- **Service 回傳 `ServiceResult<T>`**：表達預期內的失敗，不用丟例外
- **View 一律綁 ViewModel**：不直接綁 domain model，mapping 手寫
- **使用者輸入用 DataAnnotations + ModelState 驗證**：輸入錯誤絕不能變成 500
- **金額用 `decimal`**：會員折扣（Standard 不打折 / Silver 95 折 / Gold 90 折）集中在 `OrderService.CalculateTotal`，不要在別處重算

參考範例：新增 Controller 照 `ProductsController.cs`，新增 Service 照 `ProductService.cs`。

## 重要 / 危險檔案

- `src/OrderHub.Infrastructure/Migrations/**`：EF migration 是資料庫歷史，不要手改
- `src/OrderHub.Web/appsettings.Development.json`：連線字串，改動前先確認

## 不要做的事

- 不要未經同意新增 NuGet 套件
- 不要在 Controller / Service 直接使用 DbContext
- 不要為了「順手」重構與當前任務無關的程式碼
- 不要讀取或寫入任何機密檔（`*.pfx`、`appsettings.Production.json`、user-secrets）
