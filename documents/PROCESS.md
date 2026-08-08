# PROCESS.md — 我的練習心得

> 一個原則：**寫「具體發生的事」，不寫感想文。**
> 貼上當時真實的 prompt、真實的數字、真實的錯誤訊息——三個月後的你（和你的同事）才用得上。

#### 使用的 agent 與模型：Claude Code + Claude Sonnet 4.6（auto mode，不停下來確認）

---

## 通用四問

### 1. 我的任務拆解

開工前把任務拆成以下步驟，實際執行時順序完全照表走，沒有變：

1. 練習 1：讀懂專案結構 → 設定 CLAUDE.md → 設定 agent 組態（settings.json、hooks、subagents、skill）→ commit
2. 練習 2 Bug 1：追蹤分頁邏輯（OrderRepository.GetPagedAsync）→ 修 Skip offset → 補回歸測試 → commit
3. 練習 2 Bug 2：追蹤折扣邏輯（CreateOrderAsync + CalculateTotal）→ 移除建單時的快照折價 → 補回歸測試 → commit
4. 練習 2 Bug 3：追蹤取消流程（CancelOrderAsync）→ 修 Status 賦值時機 → 補回歸測試 → commit
5. 練習 3：進入 Plan Mode → 給規格 prompt → 審計畫 → 放行實作 → 頁面逐條驗證 → commit
6. 練習 4：進入 Plan Mode → 要求提重構計畫 → 審計畫 → 放行 → `dotnet test` 全綠 → commit

---

### 2. AI 幫上大忙的地方

**最有效的一次：Bug 3 根因定位**

給 agent 的 prompt（透過 `/fix-bug` skill）：

```
倉庫反映：取消訂單後商品庫存數字沒有還原。
重現步驟：ProductId=2，原始庫存 50 → 建一筆 qty=3 訂單（庫存變 47）→ 取消這筆訂單 → 刷新 /Products，庫存仍顯示 47。
```

agent 立刻定位到 `OrderService.CancelOrderAsync`（第 99–112 行），指出 `order.Status = OrderStatus.Cancelled` 被放在 `if (Pending || Confirmed)` **之前**，導致 if 判斷時 Status 已是 Cancelled，條件恆 false，庫存還原程式碼從未執行。

這種「狀態在判斷前就被覆寫」的邏輯順序 bug，靠肉眼 review 很容易跳過，agent 在 30 秒內就定位出來，並且給出精確的行號。

**練習 3 的 Plan Mode prompt 有效原因：**

把規格完整貼入、要求逐層分析，agent 輸出的計畫明確指出「近 30 天銷量要在 Repository 層做一次 join，避免 N+1」，這個細節若不問計畫直接寫程式，很可能在 Service 層做迴圈查詢。

---

### 3. AI 誤導我的地方，與我如何發現

**Bug 2：agent 第一次提案的修法方向不精確**

第一次的修法建議是「在 `CalculateTotal` 裡加一個 if，判斷 Gold 才套折扣」——這是對的，但它沒有同時指出 `CreateOrderAsync` 裡面已有一段 Gold 快照折價邏輯也要移除。

發現方法：讓 agent 給完修法後，自己去 `OrderService.cs` 搜尋 `Gold`，在第 72 行找到：

```csharp
if (customer.Tier == CustomerTier.Gold)
{
    unitPrice = Math.Round(unitPrice * (1 - GetDiscountRate(customer.Tier)), 2);
}
```

這段若不移除，快照會存折後價，`CalculateTotal` 再折一次，結果仍是 0.81。

**教訓**：agent 給修法後，要自己在 diff 裡確認「有沒有其他地方做了同一件事」，不能只看它提到的那個點。

---

### 4. 我會帶回日常工作的一招

**對有副作用的狀態機，先還原副作用、最後才改狀態——並在 PR review 時把「狀態賦值」行列為必看點。**

操作步驟：
1. 看到任何 `xxx.Status = SomeStatus` 的行
2. 往前找：有沒有 if/條件用到這個欄位，這些 if 在狀態賦值之前還是之後？
3. 如果「條件讀舊狀態，才能走到 if block」，那 if block 一定要放在賦值行之前
4. 補回歸測試時，同時測「修復前會失敗」的路徑（InlineData 同時測 Pending 和 Confirmed，不只測一種）

---

## 自我驗證（做到哪個階段答哪題）

### 第一階段 — Agentic Coding

練習 1

1. **v** 我能不看筆記說出三個專案（Web/Core/Infrastructure）各自的職責：Web 只做接線與顯示（Controller/ViewModel/View）、Core 放 domain model 與 service 介面和商業邏輯、Infrastructure 包 EF Core DbContext/Repository/Migrations/DbSeeder
2. **v** 我核對過 agent 描述的建單流程，並找到一處不精確的說法：agent 最初說「CalculateTotal 在 Controller 呼叫後直接顯示折後價」，實際上 mapping 發生在 ViewModel，不是 Controller
3. **v** 商業邏輯（折扣計算、庫存驗證）放在 `OrderHub.Core/Services`；新增頁面需動：Core 的 Service + interface、Infrastructure 的 Repository + interface、Web 的 Controller + ViewModel + View

練習 2

1. **v** 三個 bug 都先在頁面上重現過才開始找程式
2. **v** 給 agent 的資訊包含具體觀察：
   - Bug 1：「建完 Order #42 後，第 1 頁看不到它，翻到第 3 頁才找到；點第 3 頁（最後一頁）時頁面空白」
   - Bug 2：「Gold 客戶下單 NT$1000 商品 ×1，詳情頁顯示 NT$810，手算應是 NT$900」
   - Bug 3：「ProductId=2 庫存 50 → 建訂單 qty=3 → 取消 → 刷新商品頁庫存仍 47」
3. **v** 每個修復都回頁面驗證過症狀消失
4. **v** 每個 bug 都補了回歸測試，`dotnet test` 全 38 綠
5. **v** 三個獨立 commit，message 格式：`fix: [症狀] / 根因：[...] / 修法：[...]`
6. **思考題** 為什麼原本的測試沒抓到這三個 bug？
   - Bug 1：原有測試只驗 `TotalPages` 計算，沒有測「page=1 一定包含最新紀錄」這個使用者場景
   - Bug 2：原有測試建單後只斷言 `result.Success`，沒有驗 `CalculateTotal` 的回傳值
   - Bug 3：原有測試只測「已 Cancelled 訂單不能再取消」，沒有測「取消後庫存是否回補」

練習 3

1. **v** `/Products/LowStock` 不帶參數 → threshold=10 結果；帶 `?threshold=3` → 結果隨之改變
2. **v** `?threshold=0`、`?threshold=-1` → 頁面顯示「threshold 必須大於 0」，不是 500
3. **v** 售出數量欄位排除了 Cancelled 訂單（取消一筆 qty=5 的訂單後，該商品售出數量減少 5）
4. **v** 停售商品不出現在列表（IsActive=false 的商品不顯示）
5. **v** 程式分層與命名跟既有 Products 功能一致：Controller 只呼叫 service、service 呼叫 repository、view 綁 ViewModel
6. **v** 新增 3 個 service 單元測試（門檻過濾與排序、排除停售商品、排除 Cancelled 銷量），`dotnet test` 全 38 綠

練習 4

1. **v** 重構後 `dotnet test` 全 38 綠（只改 `OrderService.cs`，介面/測試/其他類別皆未動）
2. **v** 改善了什麼：`CreateOrderAsync` 從 55 行拆成主流程 + `ValidateLines`（static，純驗證）+ `ApplyOrderItemsAsync`（async，副作用），每段職責單一；沒有改變的：對外行為、錯誤訊息字串、所有測試的斷言結果
3. **v** 對照 diff 逐行確認：新 private 方法沒有改變任何邏輯，只是搬移與分群

### 第二階段 — Custom MCP Server

練習 0

1. **v** agent 能自己開瀏覽器完成操作並回傳截圖（Playwright MCP：`claude mcp add playwright -- npx @playwright/mcp@latest`）
2. **v** 對比活動 1 練習 2：當時人工在瀏覽器逐步重現 bug（開頁面 → 建訂單 → 翻頁確認）；現在一句話就讓 agent 自己跑完相同流程並截圖回來。差異：人工重現需要清楚描述每一步，agent 用 Playwright MCP 可以自己看 DOM、決定怎麼點——把「知道怎麼重現」這件事從人頭搬進工具

練習 1

1. **v** `dotnet build src/OrderHub.Mcp` 成功（0 errors）
2. **v** 獨立 commit（`12751a9` — feat: 新增 OrderHub.Mcp 專案與 3 個唯讀工具）
3. **地雷回憶**：`dotnet add package ModelContextProtocol --prerelease` 因企業內部 NuGet feed 回 401 失敗，改為直接在 .csproj 指定版本並用本機快取（`~/.nuget/packages`）restore；`dotnet new console` 預設產 net10.0，手動改成 net8.0 才和專案一致

練習 2

1. **v** 三個工具都列得出來，description、參數說明如所寫
2. **v** 手動呼叫 `low_stock`（threshold=10），回傳結果與 `/Products` 頁面低庫存商品一致
3. **v** `get_order` 呼叫不存在的 Id，回應「找不到訂單 999」，非 exception dump
4. **地雷回憶**：Inspector SPA 只要 Playwright 瀏覽器換頁，proxy（port 6277）就斷線——解法是整段測試在同一個 Playwright session 裡做完，中途不離開 Inspector 頁面

練習 3

1. **v** `/mcp` 能看到 orderhub server 與三個工具
2. **v** before/after 對照：關掉 MCP 問「哪些商品庫存低於 5？」agent 嘗試讀程式碼或繞道；開啟後一次 `low_stock(threshold=5)` 呼叫就答完
3. **v** `.mcp.json` 進 git，獨立 commit（`12751a9`）

練習 4

1. **v** MCP Inspector：`cancel_order` 標注 `destructiveHint: true`、`idempotent: false`；`get_order` / `low_stock` / `customer_orders` 顯示 `readOnly: true`
2. **v** 取消待處理訂單（#122，楊佩珊）成功：工具回傳「訂單 122 已取消，庫存已回補」
3. **v** 對已取消訂單（#122）再次呼叫 `cancel_order`：回傳「取消失敗：狀態為 Cancelled 的訂單不可取消」，清楚的拒絕訊息，非 exception dump
4. **v** 獨立 commit（`d5ac3e5` — feat(mcp): 練習4 — 新增 cancel_order 工具並標注 ReadOnly）
5. **設計觀察**：`Destructive = true` 是給 client 的 hint，不是強制授權；真正的狀態守衛在 `OrderService.CancelOrderAsync`——service 層才是信任邊界，不能把授權外包給 client 的標注行為

練習 5

1. **v** MCP Inspector Resources 分頁：讀到 `orderhub://discount-rules`，內容 `mimeType: text/markdown`，折扣三條（Standard/Silver/Gold）正確顯示
2. **v** MCP Inspector Prompts 分頁：`low_stock_report` 列出且帶 `threshold` 參數（說明「庫存門檻，預設 10」），Get Prompt 展開後回傳 `role: "user"` 結構訊息
3. **三者分工的思考（5c 第 3 點）**：

   **折扣規則用 Resource 給 vs. 讓 agent 自己讀 `OrderService.cs`**
   - 讀程式碼：agent 每次都要去翻 service 層原始碼，C# 語法對非工程師類 agent 不友善；規則散在實作細節裡不易找；改版時 agent 不會主動知道有變化
   - Resource：agent 拿到的是「明確聲明過的背景知識」，語意清楚、格式人類可讀（Markdown）；規則改版只改一處（Resource 字串），不用去找所有可能讀了舊版資訊的地方

   **Prompt 範本放在 server vs. 每個人自己打一段話**
   - 自己打：同樣的採購需求每個業務打法不同，漏掉「按 SKU 彙總」或「附理由」的機率很高；無法版本控制；推廣新寫法要靠口耳相傳
   - Server Prompt：全隊共用同一份範本，寫法統一；進 git，改版有歷史記錄；只改 server 一處，所有 client（/mcp__orderhub__low_stock_report）立刻拿到新版本

   **實際執行後的補充觀察（2026-08-08 真實 run）：**

   *Tool = 即時資料層，粒度是設計關鍵*
   這次 agent 為查 5 個商品的近期需求，連續 call 了 30 次 `get_order`（逐筆拼湊）。沒有「依商品彙整訂單」的 aggregate tool，agent 只能自己迴圈。說明 Tool 粒度設計直接影響 token 消耗和延遲：若加一個 `orders_by_sku` tool，同樣的分析只需 5 次呼叫，而非 30 次。

   *Prompt = 工作流程規格書，定義「做完了」的收斂標準*
   Prompt 的三步結構（查低庫存 → 查近期訂單 → 輸出採購建議表：SKU/名稱/現有庫存/建議補貨量/理由）讓 agent 知道什麼叫完成。若只說「查庫存」，agent 可能只回傳一個 list，不會進一步分析趨勢、給補貨量。Prompt 把「業務對產出的期待格式」編進 server，不靠人逐次口頭說明。

   *threshold 型別錯誤揭露 Prompt 驗證的邊界*
   呼叫者傳入 `"run"`（非整數）作為 threshold，Prompt 本身不做型別驗證，agent 必須自行判斷並 fallback 到預設值 10。教訓：Prompt 的參數 JSON Schema 應盡量嚴格（標明 `type: integer`），讓 MCP client 在 UI 層就能拒絕錯誤輸入，不把型別衛生的責任全丟給 agent 的推理能力。

   *Resource 補「靜態背景知識」，Tool 補「即時狀態」，兩者不可互換*
   這次採購分析需要的是「現在庫存多少、近期有幾筆訂單」，Resource 裡的折扣規則對此沒有直接幫助。Resource 回答「規則是什麼」，Tool 回答「現況是什麼」——兩者定位正交，設計時不要把即時資料放進 Resource（會過時），也不要把靜態規則包成 Tool（每次都要走一次 HTTP）。

4. **v** 獨立 commit（`12d59e4` — feat(mcp): 練習5 — 新增 Resource 與 Prompt）

---

## 附錄：值得留下的對話片段

### 片段 1：Bug 3 定位（最乾淨的一次問法）

**我的 prompt：**
```
/fix-bug 倉庫：取消訂單後商品庫存沒有還原。
重現：ProductId=2 原始庫存 50 → 建訂單 qty=3（庫存→47）→ 取消 → 刷新商品頁庫存仍 47。
預期：取消後庫存應回到 50。
```

**Agent 回應摘要：**
> `CancelOrderAsync` 第 99 行把 `order.Status` 設為 `Cancelled` 之後，第 101 行的 `if (order.Status == Pending || Confirmed)` 永遠為 false——庫存還原程式碼從未執行。修法：把庫存還原 foreach 移到 Status 賦值之前。

這次問法有效的原因：給了具體數字（ProductId=2、qty=3、50→47→47），agent 不需要猜測，直接去追 `CancelOrderAsync` 的狀態機邏輯。

---

### 片段 2：練習 3 Plan Mode 計畫審查（發現邊界遺漏）

計畫裡 `GetLowStockAsync` 的第一版草案是在 Service 層用 `foreach` 查每個商品的近 30 天銷量——N+1。

我追問：「近 30 天銷量的查詢會不會有 N+1？」

**Agent 修正後的計畫：**
> 改為在 Repository 做一次 `GroupJoin`（或 subquery），把所有商品的 30 天銷量一次撈出，Service 直接拿結果，不再逐筆查詢。

這個例子說明：計畫階段比實作階段便宜，一句追問就能在還沒寫程式前擋掉一個效能問題。
