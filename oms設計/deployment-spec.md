# FB-BTS OMS 部署規格建議書

> **版本**: 5.2 | **日期**: 2026-04-01
> **系統**: FB-BTS Order Management System (WPF + .NET 10)

---

## 一、系統概覽

### 後端

| 指標 | 數值 |
|------|------|
| 框架 | .NET 10 (ASP.NET Core Web API + SignalR) |
| 資料庫 | MSSQL (Production) / SQLite (Local Dev) |
| ORM | Entity Framework Core 10.0.5 |
| SignalR Hub | 1 (OmsHub, 即時推播) |
| 訊息佇列 | RabbitMQ (Quorum Queue)，訂單暫存 + 保證不遺失 |
| 批次寫入 | 2,500 筆/批, 20ms 間隔, RabbitMQ → SqlBulkCopy（詳見[第五章](#batch-flush)） |

### 前端 — AP (Desktop)

| 指標 | 數值 |
|------|------|
| 框架 | WPF (.NET 10) |
| 用途 | 交易員桌面端，本機安裝，僅支援 Windows |

---

## 二、建議規格總覽

### 2.1 硬體規格 (Hardware)

> **規格等級說明**：表格中的「K 筆/sec」為每秒可處理的訂單筆數（Orders per Second, OPS），代表系統在持續負載下的吞吐量上限。10K 筆/sec = 每秒 10,000 筆委託，50K 筆/sec = 每秒 50,000 筆委託。

#### App Server

| 規格 | 開發/測試 | 正式 (10K/sec) | 正式 (50K/sec) | HA (× 2 台, AA) |
|------|:--------:|:-------:|:-------:|:-----------:|
| **CPU** | 8 vCPU | 16 vCPU (≥3.0 GHz) | 32 vCPU (≥3.0 GHz) | 同正式環境 |
| **RAM** | 16 GB | 32 GB | 64 GB | 同正式環境 |
| **系統碟** | 50 GB SSD | 100 GB NVMe SSD | 100 GB NVMe SSD | 同正式環境 |
| **資料碟** | 200 GB SSD | 500 GB NVMe SSD | 1 TB NVMe SSD | NVMe (RAID 1) |
| **網路** | 1 Gbps | 1 Gbps（[頻寬影響](#bandwidth-impact)） | 10 Gbps（[頻寬影響](#bandwidth-impact)） | 同正式環境 |
| **節點數** | 1 | 1 | 1 | 2 (Active-Active) |

> 推估邏輯：[3.1 App Server](#app-server-sizing)

#### DB Server

| 規格 | 開發/測試 | 正式 (10K/sec) | 正式 (50K/sec) | HA (1+2) |
|------|:--------:|:-------:|:-------:|:-----------:|
| **CPU** | 8 vCPU | 16 vCPU | 32 vCPU | 同正式環境 |
| **RAM** | 32 GB | 64 GB | 128 GB | 同正式環境 |
| **資料碟** | 100 GB SSD | 1 TB NVMe SSD | 2 TB NVMe SSD | NVMe (RAID 10) |
| **Log 碟** | — | 100 GB NVMe SSD | 200 GB NVMe SSD | 同正式環境 |
| **TempDB 碟** | — | 50 GB NVMe SSD | 100 GB NVMe SSD | 同正式環境 |
| **網路** | 1 Gbps | 1 Gbps | 10 Gbps | 同正式環境 |
| **節點數** | 1 | 1 | 1 | 3 (1 Primary + 2 Secondary) |

> **HA 說明**: 1 台 Primary 負責讀寫，2 台 Secondary 透過 Always On AG 即時抄寫資料。
> Primary 故障時，其中一台 Secondary 自動升為 Primary。另一台 Secondary 可供唯讀查詢（報表、歷史查詢分流）。

> 推估邏輯：[3.2 DB Server](#db-server-sizing)

#### FIX Gateway Server

| 規格 | 開發/測試 | 正式 (10K/sec) | 正式 (50K/sec) | HA (A/S) |
|------|:--------:|:-------:|:-------:|:-------:|
| **CPU** | 8 vCPU | 8 vCPU | 16 vCPU (≥3.0 GHz) | 同正式環境 |
| **RAM** | 16 GB | 16 GB | 32 GB | 同正式環境 |
| **系統碟** | 50 GB SSD | 100 GB NVMe SSD | 100 GB NVMe SSD | 同正式環境 |
| **資料碟** | 100 GB SSD | 500 GB NVMe SSD | 500 GB NVMe SSD | 同正式環境 |
| **網路** | 1 Gbps | 1 Gbps | 10 Gbps (dedicated, 低延遲) | 同正式環境 |
| **節點數** | 1 | 1 | 1 | 2 (Active-Standby) |

> **A/S 說明**：#1 Active 負責所有 FIX Session 收發；#2 Standby 保持熱備，Active 故障時手動或自動切換，FIX Session 重連後繼續服務。

> 推估邏輯：[3.3 FIX Gateway Server](#fix-gateway-sizing)

#### Market Data Server (行情主機)

| 規格 | 開發/測試 | 正式 (10K/sec) | 正式 (50K/sec) | HA (A/S) |
|------|:--------:|:-------:|:-------:|:-------:|
| **CPU** | 8 vCPU | 8 vCPU | 16 vCPU (≥3.0 GHz) | 同正式環境 |
| **RAM** | 16 GB | 16 GB | 32 GB | 同正式環境 |
| **系統碟** | 50 GB SSD | 100 GB NVMe SSD | 100 GB NVMe SSD | 同正式環境 |
| **資料碟** | 100 GB SSD | 500 GB NVMe SSD | 500 GB NVMe SSD | 同正式環境 |
| **網路** | 1 Gbps | 1 Gbps | 10 Gbps (dedicated, 低延遲) | 同正式環境 |
| **節點數** | 1 | 1 | 1 | 2 (Active-Standby) |

> **A/S 說明**：#1 Active 負責行情接收與分發；#2 Standby 同步訂閱行情源，Active 故障時切換接管，報價快取重建時間約數秒。

> 推估邏輯：[3.4 Market Data Server](#market-data-sizing)

#### Algo Server (AGL)

| 規格 | 開發/測試 | 正式 (10K/sec) | 正式 (50K/sec) | HA (A/S) |
|------|:--------:|:-------:|:-------:|:-------:|
| **CPU** | 8 vCPU | 8 vCPU | 16 vCPU (≥3.0 GHz) | 同正式環境 |
| **RAM** | 16 GB | 16 GB | 32 GB | 同正式環境 |
| **系統碟** | 50 GB SSD | 100 GB NVMe SSD | 100 GB NVMe SSD | 同正式環境 |
| **資料碟** | 100 GB SSD | 500 GB NVMe SSD | 500 GB NVMe SSD | 同正式環境 |
| **網路** | 1 Gbps | 1 Gbps | 10 Gbps (dedicated, 低延遲) | 同正式環境 |
| **節點數** | 1 | 1 | 1 | 2 (Active-Standby) |

> **A/S 說明**：#1 Active 負責所有策略運算；#2 Standby 維持策略實例熱備狀態，Active 故障時切換接管，進行中子單透過 RabbitMQ 確保不遺失。

> 推估邏輯：[3.5 Algo Server](#algo-server-sizing)

#### RabbitMQ Server

| 規格 | 開發/測試 | 正式 (10K/sec) × 3 台 | 正式 (50K/sec) × 3 台 |
|------|:--------:|:-------:|:-------:|
| **CPU** | 4 vCPU | 4 vCPU | 8 vCPU |
| **RAM** | 8 GB | 16 GB | 32 GB |
| **資料碟** | 50 GB SSD | 100 GB NVMe SSD | 200 GB NVMe SSD |
| **網路** | 1 Gbps | 1 Gbps | 10 Gbps |
| **節點數** | 1 | 3 (Quorum Queue) | 3 (Quorum Queue) |

> **為什麼需要 3 台？**
> RabbitMQ Quorum Queue 基於 Raft 共識協議，需要**過半數節點存活**才能確認訊息寫入：
> - 3 台中 2 台同意即完成寫入 → 允許 1 台故障，服務不中斷
> - 2 台部署時，任一台故障即失去多數 → 佇列無法寫入，系統停擺
> - 1 台部署無冗餘，故障即遺失所有未消費訊息
>
> 因此 **3 台是 Quorum Queue 保證資料不遺失的最低需求**。

> 推估邏輯：[3.6 RabbitMQ Server](#rabbitmq-server-sizing)

#### Load Balancer (F5)

> 使用 F5 硬體負載均衡器，規格由設備本身決定，無需額外規劃主機。
> 負責將流量以 Sticky Session (IP Hash) 分配至 App Server，並透過 Health Check (/health/live) 偵測節點存活狀態，自動排除故障節點。

#### Monitoring Server (監控主機)

| 規格 | 開發/測試 | 正式 |
|------|:--------:|:----:|
| **CPU** | 4 vCPU | 8 vCPU |
| **RAM** | 8 GB | 16 GB |
| **系統碟** | 50 GB SSD | 100 GB NVMe SSD |
| **資料碟** | 100 GB SSD | 500 GB NVMe SSD |
| **網路** | 1 Gbps | 1 Gbps |
| **節點數** | 1 | 1 |

> **監控範疇**：獨立主機，負責統一收集並呈現所有主機的運作狀態，提供即時監控儀表板與異常告警，確保各服務健康狀況可視化。

### 2.2 軟體規格 (Software)

#### 作業系統 & Runtime

| 元件 | 建議版本 | 支援至 | 官方文件 |
|------|---------|:------:|---------|
| **App Server OS** | Ubuntu 24.04 LTS | 2029-04 | [.NET 10 支援矩陣](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md) |
| **DB Server OS** | Windows Server 2025 | 2034 | [SQL Server on Linux](https://learn.microsoft.com/en-us/sql/linux/sql-server-linux-setup) |
| **Client OS** | Windows 10 (22H2) / Windows 11 | — | WPF 僅支援 Windows |
| **.NET SDK** | 10.0.x (LTS) | ~2028 | [.NET 10 Download](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) |
| **ASP.NET Core** | 10.0.x | ~2028 | [ASP.NET Core 10.0](https://learn.microsoft.com/en-us/aspnet/core/release-notes/aspnetcore-10.0) |
| **WPF** | .NET 10 內建 | ~2028 | [WPF on .NET](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/) |

#### 資料庫 & 訊息佇列

| 元件 | 建議版本 | 支援至 | 官方文件 |
|------|---------|:------:|---------|
| **SQL Server** | 2022 (16.x) | 2033 | [SQL Server 2022](https://learn.microsoft.com/en-us/sql/sql-server/what-s-new-in-sql-server-2022) |
| **EF Core** | 10.0.x | 隨 .NET 10 | [EF Core 10.0](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-10.0/whatsnew) |
| **RabbitMQ** | 4.1.x | Ongoing | [RabbitMQ Docs](https://www.rabbitmq.com/docs) |
| **RabbitMQ .NET Client** | 7.x | Ongoing | [RabbitMQ .NET Client](https://www.rabbitmq.com/client-libraries/dotnet-api-guide) |

> **備註**: SQL Server 2025 (17.x) 已於 2025 年 11 月正式 GA，但 CU1 曾因特定問題暫時撤下，穩定性仍待觀察。建議以 SQL Server 2022 部署（支援至 2033），待 2025 版累積足夠 CU 驗證後再評估升級。

#### 中介軟體 & 服務元件

| 元件 | 用途 | 版本 | 官方文件 |
|------|------|------|---------|
| **Kestrel** | HTTP/WebSocket Server | ASP.NET Core 內建 | [Kestrel](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel) |
| **SignalR** | 即時雙向通訊 | ASP.NET Core 內建 | [SignalR](https://learn.microsoft.com/en-us/aspnet/core/signalr) |
| **Serilog** | 結構化日誌 | 9.0.0 | [Serilog](https://serilog.net/) |
| **OpenTelemetry** | 分散式追蹤 + Metrics | 1.11.2 | [OTel .NET](https://opentelemetry.io/docs/languages/dotnet/) |
| **BCrypt.Net-Next** | 密碼雜湊 | 4.1.0 | [bcrypt.net](https://github.com/BcryptNet/bcrypt.net) |

#### 安全性

| 項目 | 目的 | 機制 | 官方文件 |
|------|------|------|---------|
| **TLS** | 防止傳輸過程被竊聽或竄改 | F5 TLS 1.2/1.3 終結 | [Mozilla SSL Config](https://ssl-config.mozilla.org/) |
| **CSRF** | 防止跨站請求偽造攻擊 | Anti-Forgery (X-XSRF-TOKEN) | [Anti-Forgery](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery) |
| **Rate Limiting** | 防止暴力破解與 API 濫用 | 全域 100 req/10s; Login 10 req/min | [Rate Limiting](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit) |
| **Security Headers** | 防止 XSS、點擊劫持、MIME 嗅探攻擊 | CSP, X-Frame-Options, X-Content-Type-Options | [OWASP Top 10](https://owasp.org/www-project-top-ten/) |
| **Health Checks** | 供 Load Balancer 偵測服務存活與就緒狀態 | /health/live (liveness), /health/ready (readiness) | [Health Checks](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks) |

---

## 三、硬體規格推估邏輯

### 3.1 App Server

**CPU (32 vCPU):**

OmsEngine 每 20ms 執行一次批次 flush (2,500 筆)，需 4 核心專用引擎。AA 架構下 LB 以 Sticky Session 分配使用者，兩台各承擔約半數流量，加上 SignalR 推播、API 處理、BackgroundService，32 核心確保充足並行處理能力。

- 佐證: [.NET Performance Improvements](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-9/)

**RAM (64 GB):**

EF Core DbContext pool 需擴大至 500 concurrent，約 2.5 GB。RabbitMQ client buffer ~200 MB。SignalR 大量推播需 ~1 GB。實際使用約 5-8 GB，64 GB 提供 8x headroom 應對 GC Gen2 壓力。

- 佐證: [ASP.NET Core Performance Best Practices](https://learn.microsoft.com/en-us/aspnet/core/performance/performance-best-practices)

**磁碟分離 (系統碟 + 資料碟):**

| 磁碟 | 存放內容 | 分離理由 |
|------|---------|---------|
| **系統碟** | OS、.NET Runtime、OMS 應用程式 | 大小固定，讀取為主 |
| **資料碟** | 日誌檔、DB 備份、暫存資料 | 持續增長，可獨立擴容，碟滿不影響 OS |

**網路 (10 Gbps):**

每台 App Server 承擔約半數訂單流量 × 500 bytes，加上 SignalR 推播、DB 寫入，1 Gbps 不足。10 Gbps 提供充足 headroom。

### 3.2 DB Server

**RAM (128 GB):**

MSSQL Buffer Pool 建議配置為總 RAM 的 75% (96 GB)。50K orders/sec 下需大量 buffer 減少磁碟 I/O，確保批次寫入不受 page flush 延遲影響。

- 佐證: [SQL Server Memory Configuration](https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/server-memory-server-configuration-options)

**Storage IOPS:**

50,000 orders + 75,000 fills + 50,000 routes = 175,000 rows/sec 寫入需求。NVMe SSD 提供 100,000+ random IOPS，搭配 32 vCPU 平行處理與 SqlBulkCopy 批次寫入可達成。

- 佐證: [SQL Server Storage Best Practices](https://learn.microsoft.com/en-us/azure/azure-sql/virtual-machines/windows/performance-guidelines-best-practices-storage)

**磁碟分離 (資料碟 + Log 碟 + TempDB 碟):**

| 磁碟 | 存放內容 | I/O 模式 | 分離理由 |
|------|---------|---------|---------|
| **資料碟** | .mdf 資料檔 | 隨機讀寫 | 年增量 ~1.05 TB，需獨立擴容 |
| **Log 碟** | .ldf 交易日誌 | 循序寫入 | 獨立碟循序 I/O 效能最佳 |
| **TempDB 碟** | tempdb 暫存 | 高頻隨機讀寫 | 避免與主資料競爭 IOPS |

- 佐證: [Microsoft TempDB 最佳做法](https://learn.microsoft.com/en-us/sql/relational-databases/databases/tempdb-database)

### 3.3 FIX Gateway Server

FIX Gateway 獨立部署的理由：FIX 協議對延遲敏感，與 App Server 分離可避免 API 請求與 FIX 訊息處理互相搶佔 CPU，同時隔離故障範圍（Gateway 掛掉不影響 Web/API 服務）。

**CPU (16 vCPU):**

FIX 訊息解析 ~20μs/筆，加上 sequence 管理與路由邏輯，16 核心確保低延遲處理。

**RAM (32 GB):**

每個 FIX Session 佔用 ~5-10 MB（socket buffer + message store），message store 需保留近期訊息用於 resend recovery，約 2-4 GB。32 GB 提供充足 headroom。

**資料碟 (500 GB):**

存放 FIX message log（用於審計與 resend recovery），依合規需求可能需保留 1-2 年歷史紀錄。

**獨立部署優勢：**

| 優勢 | 說明 |
|------|------|
| 延遲隔離 | FIX 訊息處理不受 API/SignalR 流量影響 |
| 故障隔離 | Gateway 故障不影響 Web 端；App Server 重啟不中斷 FIX Session |
| 獨立擴展 | 可依 FIX Session 數量獨立調整規格 |
| 安全分區 | FIX 對外連線與內部 API 可放置於不同網路區段 |

### 3.4 Market Data Server

行情主機獨立部署，負責接收外部行情源（交易所、資訊商）並分發至 App Server。

**CPU (16 vCPU):**

行情 tick 需即時解析並分發至 App Server + Algo Server。16 核心確保高頻解析不產生延遲。

**RAM (32 GB):**

需維護 symbol 訂閱表、即時報價快取（last price、bid/ask、volume）。高吞吐環境下標的數量與快取深度增加，約 1-2 GB，32 GB 提供充足 headroom。

**獨立部署理由：** 行情資料流量大且持續，與交易邏輯分離可避免行情 spike 影響下單延遲。行情主機故障不影響已送出的訂單處理。

### 3.5 Algo Server (AGL)

Algo Server 獨立部署，負責演算法交易策略的運算與執行。

**CPU (16 vCPU):**

即時處理行情 tick、計算訊號（均線、VWAP、TWAP 等），並產生子單送至 FIX Gateway。16 核心確保多策略同時運算不互相阻塞。

**RAM (32 GB):**

每個策略實例需維護歷史 tick 序列、訂單狀態、持倉部位計算。以 20-50 個同時運行策略估算，每策略 ~200-500 MB，合計 < 10 GB，32 GB 提供充足 headroom。

**獨立部署理由：** 演算法運算為 CPU 密集型，與 App Server 分離避免策略計算拖慢 API 回應。策略異常（如無限迴圈）不影響核心交易系統。

### 3.6 RabbitMQ Server

**為什麼選擇 RabbitMQ 而非 Kafka：**

| 比較項目 | RabbitMQ | Kafka |
|---------|---------|-------|
| 延遲 | ~1ms（交易系統關鍵） | ~5-10ms |
| 模式 | Work Queue → 批次 flush，契合 OMS 場景 | Event Log / 串流，偏大數據場景 |
| 訊息確認 | Consumer ACK 後移除，天然保證不遺失 | 需自行管理 offset commit |
| 運維複雜度 | 低（單一服務 + Web 管理介面） | 高（partition rebalance、log retention） |
| 50K/sec 需求 | Quorum Queue ~80K msg/sec，足夠 | 百萬級，遠超需求 |

**RAM (32 GB):**

Quorum Queue 將訊息複製至 3 節點記憶體。50K msg/sec × 500 bytes × 20ms 暫存 ≈ 持續 ~50 MB in-flight。加上 Raft log、連線管理，實際使用 ~2-4 GB，32 GB 提供充足 headroom。

**資料碟 (200 GB):**

Quorum Queue 訊息持久化至磁碟（WAL + segment files）。正常情況下訊息消費後即清除，磁碟主要用於故障恢復與 Raft snapshot。200 GB 足夠支撐突發堆積場景。

- 佐證: [RabbitMQ Quorum Queues](https://www.rabbitmq.com/docs/quorum-queues)
- 佐證: [RabbitMQ Clustering](https://www.rabbitmq.com/docs/clustering)

---

## 四、高可用 (HA) 架構說明

HA 環境的單機規格與正式環境相同：

| 角色 | HA 模式 | 節點數 | Failover 偵測 | 切換機制 | 預估停機 |
|------|--------|:-----:|:------------:|---------|:-------:|
| **App Server** | **Active-Active** | 2 | LB 健康檢查 (/health/live) | LB 自動排除故障節點，Sticky Session 使用者重新連線至存活節點 | < 5 秒 (重連) |
| **RabbitMQ** | Quorum Queue (Raft) | 3 | 節點互相探測 | Raft 自動選舉新 leader，過半數存活即可服務 | 0 秒 (無中斷) |
| **DB Server** | Always On AG (1+2) | 3 | WSFC Heartbeat (每秒探測) | Secondary 自動升為 Primary，另一台繼續抄寫 | 10-30 秒 |
| **FIX Gateway** | **Active-Standby** | 2 | Heartbeat / 監控主機探測 | Standby 切換為 Active，FIX Session 重連交易所 | 30-60 秒 |
| **Market Data** | **Active-Standby** | 2 | Heartbeat / 監控主機探測 | Standby 切換為 Active，重建行情快取 | 數秒（快取重建） |
| **Algo Server** | **Active-Standby** | 2 | Heartbeat / 監控主機探測 | Standby 切換為 Active，進行中子單透過 RabbitMQ 保全 | 30-60 秒 |
| **監控主機** | 單台 | 1 | — | — | 需人工介入（不影響交易） |

> **AA 說明**: App Server 採用 Active-Active + Sticky Session（F5 以 IP Hash 分配使用者）。
> 兩台透過 RabbitMQ 訊息匯流排解耦通訊，DB (Always On AG) 為唯一共享狀態源。
> 任一台故障時，F5 自動將流量導向存活節點，使用者重新連線後從 DB 讀取最新狀態。

- 佐證: [SQL Server Always On AG](https://learn.microsoft.com/en-us/sql/database-engine/availability-groups/windows/overview-of-always-on-availability-groups-sql-server)

---

## 五、批次寫入機制

OmsEngine 採用 **RabbitMQ 佇列 + 定時批次 flush** 設計：

1. **收單** — API 或 FIX Gateway 收到訂單，發布至 RabbitMQ Quorum Queue
2. **批次 flush** — OmsEngine 每 20ms 從 RabbitMQ 消費最多 2,500 筆，透過 SqlBulkCopy 批次寫入 DB
3. **確認** — DB 寫入成功後發送 ACK，RabbitMQ 移除已確認訊息
4. **推播** — 寫入完成後透過 SignalR 推播狀態變更（Sticky Session 下每台只推播自己的使用者）

**AA 架構下的運作方式：**

| 機制 | 說明 |
|------|------|
| **Sticky Session** | F5 以 IP Hash 將使用者固定分配至同一台 App Server |
| **共享佇列** | 兩台 App Server 共用同一組 RabbitMQ Cluster，各自消費不同訊息 |
| **DB 共享** | 兩台 App Server 寫入同一個 DB (Always On AG)，DB 為唯一真實資料源 |
| **SignalR 獨立** | 不需 Backplane，每台只推播連線在自己節點上的使用者 |
| **資料安全** | Quorum Queue 訊息寫入即複製至 3 節點，App 故障不遺失任何訂單 |

**吞吐量計算：**

> 每批 2,500 筆 / 20ms 間隔 = **50 批/sec × 2,500 = 125,000 orders/sec** (單節點理論上限)
> AA 兩台合計理論上限 250,000 orders/sec，實際受 DB 寫入能力限制

**效能比較：**

| 指標 | 逐筆即時寫入 | 批次 flush + RabbitMQ (本系統) |
|------|:-----------:|:-----------------:|
| DB 寫入次數 | 50,000 次/秒 | 50 次/秒 (每次 2,500 筆) |
| 吞吐量 | ~5,000 orders/sec | >50,000 orders/sec |
| 延遲 | ~5ms/筆 | 最大 20ms + RabbitMQ ~1ms |
| 資料安全 | 即時落碟 | Quorum Queue 3 節點複製，**App 故障零遺失** |

### 5.2 RabbitMQ 訊息匯流排架構

RabbitMQ 在本系統中不僅用於訂單批次寫入，同時作為**中央訊息匯流排**，所有元件之間的通訊皆透過 RabbitMQ 解耦：

```
                          ┌─────────────────────┐
  API (下單/改單) ───────→ │                     │ ──→ OmsEngine      (訂單批次寫入 DB)
  FIX Gateway (回報) ───→ │                     │ ──→ App Server     (DropCopy 狀態更新)
  Market Data (tick) ───→ │     RabbitMQ        │ ──→ Algo Server    (行情訂閱 + 策略運算)
  Algo Server (子單) ───→ │   訊息匯流排         │ ──→ FIX Gateway    (子單送出)
  系統事件 ──────────────→ │                     │ ──→ 通知服務        (Email / Slack / LINE)
                          └─────────────────────┘
```

**應用場景：**

| # | 場景 | 發布者 | 消費者 | Exchange 類型 | 說明 |
|:-:|------|-------|-------|:------------:|------|
| 1 | 訂單批次寫入 | API / FIX Gateway | OmsEngine | Direct | 收單 → 佇列 → 批次 flush → DB |
| 2 | FIX 訊息路由 | App Server | FIX Gateway | Direct | 下單指令發布至佇列，FIX 消費後送出，解耦 App 與 FIX 生命週期 |
| 3 | DropCopy 回報 | FIX Gateway | App Server | Direct | 交易所回報 → 佇列 → App Server 更新訂單狀態 |
| 4 | 行情分發 | Market Data | App + Algo | Fanout | tick 發布至 fanout exchange，多個消費者各自訂閱 |
| 5 | SignalR 推播觸發 | OmsEngine | App Server | Topic | DB 寫入完成發布事件 → App Server 消費後推播 SignalR |
| 6 | Algo 訊號與子單 | Algo Server | App → FIX | Direct | 策略產生訊號 → 風控驗證 → FIX 送單，形成 Work Queue 鏈 |
| 7 | 系統事件 / 審計 | 各元件 | 日誌服務 | Topic | 登入、設定變更、風控觸發 → 非同步寫入 DB / log |
| 8 | 通知服務 | 各元件 | 通知模組 | Topic | 大額成交、風控警示、系統異常 → Email / Slack / LINE |

**訊息匯流排的核心效益：**

| 效益 | 說明 |
|------|------|
| **解耦** | 各元件不直接互相呼叫，可獨立部署、重啟、升級，任一元件故障不影響其他服務 |
| **削峰** | 瞬間大量訂單或行情 spike 先堆積在佇列，消費端按自己節奏處理，避免被打爆 |
| **可靠性** | 每筆訊息經過持久化 + ACK 確認機制，確保不遺失 |
| **可觀測** | RabbitMQ Management UI 可即時監控各佇列深度、消費速率、未 ACK 數量 |
| **合併友善** | 即使 FIX/Market/Algo 合併至 App Server（[方案 A](#consolidation-a)），元件之間仍透過佇列解耦，Process crash 不拖垮其他服務 |

### 5.3 單台 RabbitMQ Failover 策略

正式環境使用 1 台 RabbitMQ（Durable Queue + Persistent Message），HA 環境使用 3 台（Quorum Queue）。

**單台資料安全保證：**

| 設定 | 作用 |
|------|------|
| **Durable Queue** | 佇列定義持久化至磁碟，RabbitMQ 重啟後佇列仍存在 |
| **Persistent Message** (delivery_mode=2) | 每筆訊息寫入磁碟後才回覆 publisher confirm |
| **Manual ACK** | Consumer 處理完成（DB 寫入成功）後才發送 ACK，未 ACK 的訊息自動重新派送 |

**故障場景與應對：**

| 故障類型 | 停機時間 | 資料影響 | 應對方式 |
|---------|:-------:|---------|---------|
| RabbitMQ 進程 crash | ~3-5 秒 | 不遺失（磁碟持久化） | systemd 自動重啟，App 短暫重連 |
| 硬體故障（主機掛掉） | 數分鐘～數小時 | 可能遺失佇列中未消費訊息（風險窗口 ~20ms ≈ 最多 2,500 筆） | App Server 降級處理 |

**App Server 降級策略（硬體故障時）：**

| 策略 | 做法 | 適用場景 |
|------|------|---------|
| **拒絕下單（建議）** | RabbitMQ 不可用時 API 回傳 503，前端提示暫停下單 | 交易系統，寧可暫停也不能遺失或重複 |
| **記憶體降級** | 自動切換至 ConcurrentQueue，恢復後切回 | 可接受極小遺失風險的場景 |

---

## 六、容量規劃

### 6.1 儲存空間估算

以 50,000 orders/sec、每日交易時段 6 小時估算：

> 50,000 orders/sec × 21,600 sec (6hr) = **1,080,000,000 筆/日** (理論峰值)
> 實際平均負載約峰值 10-20%，預估 **~1,500,000 筆/日**

| 資料表 | 預估筆數/日 | 每筆大小 | 日增量 |
|--------|:---------:|:-------:|:-----:|
| orders | 250,000 | ~1 KB | 250 MB |
| fills | 400,000 | ~0.5 KB | 200 MB |
| routes | 250,000 | ~0.5 KB | 125 MB |
| drop_copy_logs | 800,000 | ~1 KB | 800 MB |
| system_events | 50,000 | ~0.5 KB | 25 MB |
| order_confirmations | 25,000 | ~2 KB | 50 MB |
| **合計** | | | **~1.45 GB/日** |

- 年增量 (含 index): ~1.05 TB
- **建議**: 正式環境 2 TB（可支撐約 1.5 年，含 index + overhead），建議搭配定期歸檔策略

### 6.2 同時在線使用者承載估算

以 AA 架構兩台 App Server (各 32 vCPU / 64 GB)，Sticky Session 分配使用者（不含頻寬限制）：

| 規格 | 掛線待命 (idle) | 活躍操作 (下單/查詢) | 瓶頸 |
|------|:--------------:|:------------------:|------|
| **32 vCPU / 64 GB × 1** (單台) | ~12,000-16,000 人 | ~8,000-10,000 人 | CPU |
| **32 vCPU / 64 GB × 2** (AA 合計) | ~24,000-32,000 人 | ~16,000-20,000 人 | CPU |

> Sticky Session 下每台 App Server 獨立服務各自的使用者，不需跨節點同步。
> 若實作 SignalR Group 訂閱過濾（每人只接收相關訂單），承載量可提升 2-3 倍。

**考量頻寬限制：**

Sticky Session 模式下，每台 App Server 只推播自身處理的訂單事件。假設使用者接收該節點全部事件：

> 單台處理約 25,000 events/sec × 500 bytes = **12.5 MB/s (100 Mbps) per user**

| 規格 (AA 兩台合計) | 網路 (單台) | 全量推播上限 | 訂閱過濾 (每人收 1%) | 瓶頸 |
|------|:----:|:----------:|:------------------:|------|
| **32 vCPU / 64 GB × 2** | 1 Gbps | ~10 人 | ~1,000 人 | 頻寬 |
| **32 vCPU / 64 GB × 2** | 10 Gbps | ~100 人 | ~10,000 人 | 頻寬 |
| **32 vCPU / 64 GB × 2** | 10 Gbps + 訂閱過濾 | — | ~20,000 人 | CPU |

> 在全量推播模式下，**頻寬仍是最先觸及的瓶頸**。
> **建議實作 SignalR Group 訂閱過濾**，每人只接收相關訂單事件（假設接收 1%），
> 頻寬需求降低至每人 ~1 Mbps，10 Gbps 兩台合計可支撐 10,000 人以上。

- 佐證: [SignalR Scale-out](https://learn.microsoft.com/en-us/aspnet/core/signalr/scale)

---

## 七、部署架構圖

<iframe src="https://kk-capital.github.io/repository/oms%E8%A8%AD%E8%A8%88/oms-arch.html" width="100%" height="800px" frameborder="0" style="border:none;"></iframe>

---

## 八、規格比較：10,000 vs 50,000 orders/sec

### 8.0 情境定義

兩種規格等級對應不同業務規模與客群，架構相同、節點數相同，差異在單機規格與網路配置。

#### 10,000 orders/sec — 標準法人交易室

| 項目 | 說明 |
|------|------|
| **目標客群** | 中型複委託券商、法人自營部門、資產管理公司 |
| **典型同時在線** | 500–2,000 位交易員同時操作 |
| **訂單來源** | 人工下單為主，少量程式單（Program Trading）|
| **尖峰情境** | 市場開盤前 15 分鐘集中委託、重大事件後集中補單 |
| **批次 flush 設定** | 500 筆/批，50ms 間隔（延遲較寬鬆，DB 壓力低） |
| **DB 每日寫入量** | ~290 MB，年增約 210 GB（含 index）|
| **網路需求** | 1 Gbps 足夠（訂單頻寬 ~40 Mbps，SignalR 推播有餘裕） |
| **適用交易市場** | 台股、美股、港股、日股 全市場皆適用 |
| **擴容路徑** | 升級單機規格至 50K/sec 規格即可，不需變更架構或節點數 |

> **選擇此規格的條件**：日均成交筆數 < 500 萬筆，尖峰訂單流量不超過 10,000 筆/秒持續超過 5 分鐘。

---

#### 50,000 orders/sec — 高頻 / 大型複委託平台

| 項目 | 說明 |
|------|------|
| **目標客群** | 大型複委託平台、高頻交易自營商、多市場聚合券商 |
| **典型同時在線** | 2,000–10,000 位交易員 + 大量程式單並發 |
| **訂單來源** | 程式單（Algo / HFT）為主，人工單為輔 |
| **尖峰情境** | 多市場同時開盤（台股 09:00 + 美股 22:30 夏令）、Algo 策略同時觸發訊號 |
| **批次 flush 設定** | 2,500 筆/批，20ms 間隔（最大延遲 21ms，吞吐量優先） |
| **DB 每日寫入量** | ~1.45 GB，年增約 1.05 TB（含 index）|
| **網路需求** | 10 Gbps 必要（訂單頻寬 ~200 Mbps，SignalR 推播需訂閱過濾） |
| **適用交易市場** | 美股（高頻）、港股（大宗）、日股（程式單）為主要壓力來源 |
| **擴容路徑** | 水平擴展 App Server 至 3–4 台 AA，RabbitMQ 擴為 5 節點 |

> **選擇此規格的條件**：日均成交筆數 > 500 萬筆，或有 Algo / HFT 策略同時運行超過 20 個，或單一市場尖峰流量超過 20,000 筆/秒。

---

#### 規格等級快速選擇指引

| 評估項目 | 選 10K/sec | 選 50K/sec |
|---------|:----------:|:----------:|
| 日均委託筆數 | < 500 萬 | > 500 萬 |
| 同時在線交易員 | < 2,000 | > 2,000 |
| Algo 策略數量 | < 20 個 | > 20 個 |
| 程式單佔比 | < 30% | > 30% |
| 預算考量 | 硬體成本優先 | 效能穩定優先 |

---



| 主機 | 項目 | 10,000/sec | 50,000/sec |
|------|------|:----------:|:----------:|
| **App Server** | CPU | 16 vCPU | 32 vCPU |
| | RAM | 32 GB | 64 GB |
| | 系統碟 | 100 GB NVMe | 100 GB NVMe |
| | 資料碟 | 500 GB NVMe | 1 TB NVMe |
| | 網路 | 1 Gbps | 10 Gbps |
| | 節點數 | 2 (AA) | 2 (AA) |
| **DB Server** | CPU | 16 vCPU | 32 vCPU |
| | RAM | 64 GB | 128 GB |
| | 資料碟 | 1 TB NVMe | 2 TB NVMe |
| | Log 碟 | 100 GB NVMe | 200 GB NVMe |
| | TempDB 碟 | 50 GB NVMe | 100 GB NVMe |
| | 網路 | 1 Gbps | 10 Gbps |
| | 節點數 | 3 (1+2) | 3 (1+2) |
| **RabbitMQ** | CPU | 4 vCPU | 8 vCPU |
| | RAM | 16 GB | 32 GB |
| | 資料碟 | 100 GB NVMe | 200 GB NVMe |
| | 網路 | 1 Gbps | 10 Gbps |
| | 節點數 | 3 (Quorum) | 3 (Quorum) |
| **FIX Gateway** | CPU | 8 vCPU | 16 vCPU |
| | RAM | 16 GB | 32 GB |
| | 網路 | 1 Gbps | 10 Gbps |
| | 節點數 | 2 (A/S) | 2 (A/S) |
| **Market Data** | CPU | 8 vCPU | 16 vCPU |
| | RAM | 16 GB | 32 GB |
| | 網路 | 1 Gbps | 10 Gbps |
| | 節點數 | 2 (A/S) | 2 (A/S) |
| **Algo Server** | CPU | 8 vCPU | 16 vCPU |
| | RAM | 16 GB | 32 GB |
| | 網路 | 1 Gbps | 10 Gbps |
| | 節點數 | 2 (A/S) | 2 (A/S) |
| **監控主機** | CPU | 8 vCPU | 8 vCPU |
| | RAM | 16 GB | 16 GB |
| | 資料碟 | 500 GB NVMe | 500 GB NVMe |
| | 節點數 | 1 | 1 |
| **F5** | — | 不變 | 不變 |

### 8.2 容量與效能對照

| 指標 | 10,000/sec | 50,000/sec |
|------|:----------:|:----------:|
| 批次 flush | 500 筆/批, 50ms 間隔 | 2,500 筆/批, 20ms 間隔 |
| DB 寫入 (rows/sec) | ~35,000 | ~175,000 |
| 訂單頻寬 | 5 MB/s (40 Mbps) | 25 MB/s (200 Mbps) |
| 日增量 (DB) | ~290 MB | ~1.45 GB |
| 年增量 (含 index) | ~210 GB | ~1.05 TB |
| DB 資料碟建議 | 1 TB（可撐 ~4 年） | 2 TB（可撐 ~1.5 年） |
| 全量推播 per user | 40 Mbps | 200 Mbps |
| 1 Gbps 全量推播上限 | ~25 人 | ~5 人 |
| 10 Gbps + 訂閱過濾上限 | ~20,000 人 | ~20,000 人 |

> **訂單批次寫入說明**：系統不採逐筆即時寫入 DB，而是將訂單先暫存於 RabbitMQ 佇列，由 OmsEngine 定時批次取出並一次性寫入 DB。10K/sec 規格每 50ms 寫一批（500 筆），50K/sec 規格每 20ms 寫一批（2,500 筆）。批次寫入可將 DB 寫入次數從 50,000 次/秒降至 50 次/秒，大幅降低 DB I/O 壓力，同時透過 RabbitMQ Quorum Queue 三節點複製確保訂單不遺失。

> **結論**：10K/sec 與 50K/sec 的**架構與節點數完全相同**（AA × 2、DB 1+2、RabbitMQ × 3），
> 差異僅在單機規格（CPU/RAM 約減半）與網路需求（1 Gbps vs 10 Gbps）。
> 建議依實際業務量選擇，未來擴容只需升級單機規格，不需變更架構。

---

## 九、主機合併方案

現行架構正式環境 + HA 共 **16 台**（不含 F5），以下依合併程度列出三種方案。

### 9.1 現況盤點

| 主機 | 台數 | 角色 | 合併可能性 |
|------|:----:|------|-----------|
| App Server | 2 | API / SignalR / OmsEngine | 核心服務，不可減少 |
| DB Server | 3 | SQL Server Always On AG (1+2) | 可考慮降為 1+1 |
| RabbitMQ | 3 | Quorum Queue 訂單佇列 | 可共置於現有主機 |
| FIX Gateway | 2 | FIX 協議收發 (Active-Standby) | 可併入 App Server |
| Market Data | 2 | 行情接收與分發 (Active-Standby) | 可併入 App Server |
| Algo Server | 2 | 演算法策略運算 (Active-Standby) | 可併入 App Server |
| 監控主機 | 1 | 負責統一收集並呈現所有主機的運作狀態 | 不建議合併，獨立監控更可靠 |
| **合計** | **15** | | |

### 9.2 方案 A：精簡（15 → 11 台）

**做法**：FIX Gateway、Market Data、Algo Server 的 Active + Standby 各自併入兩台 App Server，以獨立 Process 或 Background Service 運行；監控主機保留獨立。

| 主機 | 台數 | 說明 |
|------|:----:|------|
| App Server | 2 | 含 FIX + Market Data + Algo 服務 Active + Standby (AA) |
| DB Server | 3 | 不變 (1+2) |
| RabbitMQ | 3 | 不變 (Quorum) |
| 監控主機 | 1 | 不變 |
| **合計** | **9** | **省 6 台** |

```
    ┌─────────────────────────┐   ┌─────────────────────────┐
    │  App Server #1          │   │  App Server #2          │  AA
    │  ASP.NET + SignalR      │   │  ASP.NET + SignalR      │  32C/64G
    │  OmsEngine              │   │  OmsEngine              │
    │  FIX Gateway (Process)  │   │  FIX Gateway (Process)  │
    │  Market Data (Service)  │   │  Market Data (Service)  │
    │  Algo Engine (Service)  │   │  Algo Engine (Service)  │
    └────────────┬────────────┘   └────────────┬────────────┘
                 └──────────┬──────────────┬───┘
                            ▼              ▼
    ┌──────────────────────────┐  ┌──────────────────────────┐
    │  SQL Server 1+2          │  │  RabbitMQ Cluster × 3    │
    │  16~32C / 64~128G        │  │  4~8C / 16~32G           │
    └──────────────────────────┘  └──────────────────────────┘
```

**App Server 規格（合併後不需調整）：**

FIX / Market Data / Algo 三個服務的實際資源消耗輕量（合計約 +8–16 GB RAM、+8C CPU），原本 32C/64G 的 headroom 已足以吸收，**無需升規**。

| 服務 | RAM 實際用量 | CPU 特性 |
|------|:-----------:|---------|
| FIX Gateway | ~2–4 GB | 大部分時間 idle，下單時短暫忙碌 |
| Market Data | ~1–2 GB | I/O 等待為主，CPU 佔用低 |
| Algo Engine | ~5–10 GB | 策略運算 CPU 中等，非持續滿載 |
| **合計追加** | **~8–16 GB** | **32C/64G 仍有充足 headroom** |

> ⚠️ **Algo 策略數量監控**：若同時運行策略超過 50 個且每策略需維護大量歷史 tick，RAM 可能逼近 25 GB。建議上線後持續監控，必要時再評估 RAM 升至 80G。

**優點**：
- 省 3 台主機，運維與授權成本降低
- FIX/Market/Algo 以獨立 Process 運行，可透過 Process 隔離減少互相影響
- App Server 原本 32C/64G 足以承擔額外負載，**不須額外採購**

**風險**：
- 故障隔離降低：FIX 異常或 Algo 策略錯誤（如無限迴圈）可能影響 API 回應
- 建議搭配 Process 監控 + 自動重啟（systemd / Windows Service）

> **建議程度：★★★★★** — 風險可控，節省最明顯

### 9.3 方案 B：進階（15 → 6 台）

**做法**：方案 A 基礎上，將 RabbitMQ 共置於 App Server (2 台) + DB Secondary (1 台)。

| 主機 | 台數 | 說明 |
|------|:----:|------|
| App Server | 2 | 含 FIX + Market + Algo + RabbitMQ (AA) |
| DB Server | 3 | 1 Primary + 1 Secondary + 1 Secondary (含 RabbitMQ) |
| 監控主機 | 1 | 不變 |
| **合計** | **6** | **省 9 台** |

```
    ┌─────────────────────────┐   ┌─────────────────────────┐
    │  App Server #1          │   │  App Server #2          │  AA
    │  ASP.NET + FIX + ...    │   │  ASP.NET + FIX + ...    │  32C/80G ← 調升
    │  RabbitMQ node 1        │   │  RabbitMQ node 2        │
    └────────────┬────────────┘   └────────────┬────────────┘
                 └──────────┬──────────────────┘
                            ▼
    ┌──────────────┐  ┌──────────────┐  ┌──────────────────────────┐
    │  DB Primary  │  │ DB Secondary │  │ DB Secondary             │
    │  32C / 128G  │  │  32C / 128G  │  │ 32C / 128G               │
    │              │  │              │  │ + RabbitMQ node 3        │
    │              │  │              │  │ + RabbitMQ 專用碟 200G ← │
    └──────────────┘  └──────────────┘  └──────────────────────────┘
```

**規格調整說明：**

共置 RabbitMQ 後，資源競爭問題需透過規格升級補償，以下為各調整項目的依據：

| 主機 | 項目 | 原規格 | 調整後 | 調整原因 |
|------|------|:------:|:------:|---------|
| **App Server** | RAM | 64 GB | **80 GB** | RabbitMQ node 自身約 2–4 GB，加上 Raft log 與 WAL 在峰值可能達 8–16 GB；+16 GB 補足 headroom |
| **App Server** | CPU | 32C | **32C**（維持，加 cgroup 限制） | RabbitMQ Raft 同步消耗 CPU，建議以 cgroup 限制 RabbitMQ 最多使用 4C，避免搶佔 ASP.NET 核心 |
| **DB Secondary** | RAM | 128 GB | **128 GB**（維持） | SQL Server Buffer Pool 已有足夠 headroom，RabbitMQ 記憶體佔用相對輕量（~2–4 GB），不需升規 |
| **DB Secondary** | 磁碟 | 資料碟 + Log 碟 + TempDB 碟 | **新增 RabbitMQ 專用碟 200 GB NVMe** | RabbitMQ WAL 為循序寫入，與 AG 同步的 Log 碟 I/O 模式相似但頻率更高；獨立一顆碟避免 I/O 互搶，確保 AG 抄寫延遲不上升 |

> **cgroup 設定建議（App Server，Linux）：**
> ```
> # /etc/systemd/system/rabbitmq-server.service.d/override.conf
> [Service]
> CPUQuota=400%          # 最多使用 4C（400% = 4 × 100%）
> MemoryMax=20G          # 硬限 20G，防止 OOM 波及 ASP.NET
> ```

**優點**：
- 僅 6 台主機，大幅降低硬體與維運成本
- RabbitMQ 記憶體需求低（~2-4 GB 正常使用），規格調升後與 App/DB 共存可行

**風險**：
- RabbitMQ 與 App Server 搶 CPU/RAM，需透過 cgroup 嚴格設定資源上限
- DB Secondary 共置 RabbitMQ 時，WAL 與 AG 同步 I/O 仍有競爭風險，需透過獨立磁碟緩解

> **建議程度：★★★☆☆** — 需額外調配資源，適合預算有限場景

### 9.4 方案 C：極簡（15 → 5 台）

**做法**：方案 B 基礎上，DB 從 1+2 降為 1+1。

| 主機 | 台數 | 說明 |
|------|:----:|------|
| App Server | 2 | 含 FIX + Market + Algo + RabbitMQ (AA)，規格同方案 B |
| DB Server | 2 | 1 Primary + 1 Secondary (含 RabbitMQ) |
| 監控主機 | 1 | 不變 |
| **合計** | **5** | **省 10 台** |

**優點**：
- 最少主機數，硬體成本最低

**風險**：
- DB 僅 1 台備援，Primary 故障後 **無第二備援、無唯讀分流**
- RabbitMQ 第 3 節點放在 DB，DB 故障同時影響 RabbitMQ quorum 穩定性
- 任何一台主機故障，同時影響多個角色

> **建議程度：★★☆☆☆** — 僅適合非關鍵環境或預算極度受限

### 9.5 方案比較總覽

| | 現行 | 方案 A | 方案 B | 方案 C |
|--|:----:|:-----:|:-----:|:-----:|
| **主機數** | 15 | 9 | 6 | 5 |
| **省幾台** | — | 6 | 9 | 10 |
| **故障隔離** | ★★★★★ | ★★★★☆ | ★★★☆☆ | ★★☆☆☆ |
| **運維複雜度** | 高（多主機） | 中 | 中高（資源調配） | 中高 |
| **DB 備援** | 1+2 | 1+2 | 1+2 | 1+1 |
| **RabbitMQ** | 獨立 3 台 | 獨立 3 台 | 共置 | 共置 |
| **FIX/MKT/AGL** | 各 A/S 獨立 | 併入 App Server | 併入 App Server | 併入 App Server |
| **監控主機** | 獨立 1 台 | 獨立 1 台 | 獨立 1 台 | 獨立 1 台 |
| **App Server RAM** | 64G | 64G | **80G** | **80G** |
| **DB Secondary RAM** | 128G | 128G | 128G | 128G |
| **DB Secondary 磁碟** | 標準三碟 | 標準三碟 | **+RabbitMQ 專用碟** | **+RabbitMQ 專用碟** |
| **建議場景** | 大型正式環境 | **一般正式環境** | 預算有限 | 非關鍵/測試 |

> **總結建議**：優先採用 **方案 A（9 台）**，在省下 6 台主機的同時維持 DB 與 RabbitMQ 的獨立性，
> 風險最低、效益最高。未來若業務量增長，FIX/Market/Algo 可隨時拆分回獨立 A/S 主機，架構彈性高。

---

## 十、參考文件彙整

| # | 主題 | 連結 |
|:-:|------|------|
| 1 | .NET 10 Release Notes | https://learn.microsoft.com/en-us/dotnet/core/introduction |
| 2 | ASP.NET Core Performance Best Practices | https://learn.microsoft.com/en-us/aspnet/core/performance/performance-best-practices |
| 3 | SignalR Scale-out | https://learn.microsoft.com/en-us/aspnet/core/signalr/scale |
| 4 | EF Core Performance | https://learn.microsoft.com/en-us/ef/core/performance/ |
| 5 | SQL Server 2022 What's New | https://learn.microsoft.com/en-us/sql/sql-server/what-s-new-in-sql-server-2022 |
| 6 | SQL Server Hardware Guidelines | https://learn.microsoft.com/en-us/sql/sql-server/install/hardware-and-software-requirements-for-installing-sql-server-2022 |
| 7 | SQL Server Memory Configuration | https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/server-memory-server-configuration-options |
| 8 | SQL Server Storage Best Practices | https://learn.microsoft.com/en-us/azure/azure-sql/virtual-machines/windows/performance-guidelines-best-practices-storage |
| 9 | SQL Server TempDB Best Practices | https://learn.microsoft.com/en-us/sql/relational-databases/databases/tempdb-database |
| 10 | RabbitMQ Documentation | https://www.rabbitmq.com/docs |
| 11 | RabbitMQ Quorum Queues | https://www.rabbitmq.com/docs/quorum-queues |
| 12 | RabbitMQ Clustering | https://www.rabbitmq.com/docs/clustering |
| 13 | RabbitMQ .NET Client | https://www.rabbitmq.com/client-libraries/dotnet-api-guide |
| 14 | Serilog for ASP.NET Core | https://github.com/serilog/serilog-aspnetcore |
| 15 | OpenTelemetry .NET | https://opentelemetry.io/docs/languages/dotnet/getting-started/ |
| 16 | WPF on .NET | https://learn.microsoft.com/en-us/dotnet/desktop/wpf/ |
| 17 | .NET 10 Supported OS | https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md |
| 18 | OWASP Top 10 | https://owasp.org/www-project-top-ten/ |
| 19 | Mozilla SSL Configuration | https://ssl-config.mozilla.org/ |
| 20 | TechEmpower Benchmarks | https://www.techempower.com/benchmarks/ |

---

## 附錄 A：主機採購清單（BOM）

> 採購方（IT 部門／財務）可直接依此表送廠商詢價，所有規格已對應正文各章節。

### A.1 正式環境 — 10,000 orders/sec

| 主機角色 | 數量 | CPU | RAM | 系統碟 | 資料碟 | Log 碟 | 網路 | 小計 |
|---------|:----:|:---:|:---:|:-----:|:-----:|:------:|:----:|:----:|
| App Server (AA) | 2 | 16C ≥3.0 GHz | 32 GB | 100 GB NVMe | 500 GB NVMe | — | 1 Gbps | 2 |
| DB Server Primary | 1 | 16C | 64 GB | — | 1 TB NVMe | 100 GB NVMe | 1 Gbps | 1 |
| DB Server Secondary | 2 | 16C | 64 GB | — | 1 TB NVMe | 100 GB NVMe | 1 Gbps | 2 |
| FIX Gateway (A/S) | 2 | 8C ≥3.0 GHz | 16 GB | 100 GB NVMe | 500 GB NVMe | — | 1 Gbps | 2 |
| Market Data (A/S) | 2 | 8C ≥3.0 GHz | 16 GB | 100 GB NVMe | 500 GB NVMe | — | 1 Gbps | 2 |
| Algo Server (A/S) | 2 | 8C ≥3.0 GHz | 16 GB | 100 GB NVMe | 500 GB NVMe | — | 1 Gbps | 2 |
| RabbitMQ (Quorum) | 3 | 4C | 16 GB | — | 100 GB NVMe | — | 1 Gbps | 3 |
| 監控主機 | 1 | 8C | 16 GB | 100 GB NVMe | 500 GB NVMe | — | 1 Gbps | 1 |
| **小計** | **15 台** | | | | | | | |

> TempDB 碟：DB Server 每台另需 50 GB NVMe（可與資料碟同片實體碟分割，或獨立一顆）。

---

### A.2 正式環境 — 50,000 orders/sec

| 主機角色 | 數量 | CPU | RAM | 系統碟 | 資料碟 | Log 碟 | 網路 | 小計 |
|---------|:----:|:---:|:---:|:-----:|:-----:|:------:|:----:|:----:|
| App Server (AA) | 2 | 32C ≥3.0 GHz | 64 GB | 100 GB NVMe | 1 TB NVMe | — | 10 Gbps | 2 |
| DB Server Primary | 1 | 32C | 128 GB | — | 2 TB NVMe | 200 GB NVMe | 10 Gbps | 1 |
| DB Server Secondary | 2 | 32C | 128 GB | — | 2 TB NVMe | 200 GB NVMe | 10 Gbps | 2 |
| FIX Gateway (A/S) | 2 | 16C ≥3.0 GHz | 32 GB | 100 GB NVMe | 500 GB NVMe | — | 10 Gbps | 2 |
| Market Data (A/S) | 2 | 16C ≥3.0 GHz | 32 GB | 100 GB NVMe | 500 GB NVMe | — | 10 Gbps | 2 |
| Algo Server (A/S) | 2 | 16C ≥3.0 GHz | 32 GB | 100 GB NVMe | 500 GB NVMe | — | 10 Gbps | 2 |
| RabbitMQ (Quorum) | 3 | 8C | 32 GB | — | 200 GB NVMe | — | 10 Gbps | 3 |
| 監控主機 | 1 | 8C | 16 GB | 100 GB NVMe | 500 GB NVMe | — | 1 Gbps | 1 |
| **小計** | **15 台** | | | | | | | |

> TempDB 碟：DB Server 每台另需 100 GB NVMe。

---

## 附錄 B：部署形式建議

本系統採**全實體機部署**，不使用虛擬化（Hypervisor）層。

### B.1 各角色部署形式

| 主機角色 | 部署形式 | 理由 |
|---------|:--------:|------|
| **FIX Gateway** | 實體機 | FIX 協議對延遲敏感（μs 級），Hypervisor 排程抖動（jitter）可能導致 FIX Session Heartbeat 超時，增加非預期斷線風險 |
| **Market Data** | 實體機 | 高頻行情 tick 需低延遲處理，VM CPU 排程延遲會造成 tick 堆積與行情落後 |
| **App Server** | 實體機 | 批次 flush 每 20ms 執行一次，VM 環境下 CPU 搶佔可能造成 flush 延遲，影響訂單落庫時間 |
| **DB Server** | 實體機 | SQL Server I/O 效能對虛擬化延遲極敏感，Always On AG 同步需穩定低延遲磁碟與網路，實體機是最佳選擇 |
| **RabbitMQ** | 實體機 | Raft 共識協議依賴低延遲節點間通訊，VM 網路虛擬化可能影響選舉穩定性 |
| **Algo Server** | 實體機 | 策略運算需穩定 CPU 時脈，VM CPU Pinning 難以完全排除 Hypervisor 影響 |
| **監控主機** | 實體機 | 統一採購實體機，簡化維運；監控流量輕量，不影響其他主機效能 |

### B.2 網路建議

所有主機建議部署於同一機房、同一物理交換器下，以確保主機間網路延遲 < 1ms。FIX Gateway 與 Market Data 建議使用獨立網路區段（VLAN）隔離對外連線與內部服務通訊。

---

## 附錄 C：授權成本提示

採購預算請特別注意以下授權費用項目：

| 項目 | 授權類型 | 適用主機 | 備註 |
|------|---------|---------|------|
| **Windows Server 2025 Standard** | 商業授權（依實體核心計費） | DB Server × 3 | 每台 DB Server 需獨立授權；Standard 版授權兩個 VM，若純實體機部署，Standard 已足夠 |
| **SQL Server 2022 Enterprise** | 商業授權（依實體核心計費） | DB Server × 3 | Always On AG 需 Enterprise 版；核心授權以實體核心數計算，建議提前確認廠商報價方式（Open License / EA） |
| **Ubuntu 24.04 LTS** | 免費（社群版） | App Server、FIX、Market Data、Algo、RabbitMQ | 無授權費用；若需官方 Support SLA，可評估 Ubuntu Pro（約 $500/年/台） |
| **Windows 10 / 11 Pro** | 商業授權 | AP Client 端（交易員桌機） | 通常已隨主機採購，確認 OEM 授權是否涵蓋；WPF 執行環境為 Windows 內建，無額外費用 |

> ⚠️ **重要提醒**：DB Server 的 Windows Server + SQL Server Enterprise 授權，通常是整個系統採購預算中**金額最高的單項授權成本**，請務必在預算編列階段單獨列項，避免漏算。以 32 核心實體機為例，SQL Server 2022 Enterprise 核心授權市價約 **USD 6,000–7,000 / 2-core pack**，3 台 DB Server × 32 核心的授權成本可能超過硬體本身。建議提前洽詢 Microsoft 大量授權（Volume License / EA）或向廠商確認是否提供 SPLA 方案。

