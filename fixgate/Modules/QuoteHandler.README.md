# Module 10 — QuoteHandler

## 概述

`QuoteHandler.cs` 實作 Fake FIX Gateway 的報價處理模組，支援 FIX 4.4 協定下列訊息：

| FIX MsgType | 訊息名稱       | 方向     | 說明                                      |
|-------------|----------------|----------|-------------------------------------------|
| `R`         | QuoteRequest   | 收 (R)   | 自動回覆 Quote，含 Bid/Ask = LastPrice ± Spread |
| `S`         | Quote          | 送 (S)   | 回應報價，帶 ValidUntilTime                |
| `Z`         | QuoteCancel    | 收 (R)   | 取消單一或全部報價                         |
| `i`         | MassQuote      | 收 (R)   | 大量報價                                   |
| `b`         | MassQuoteAck   | 送 (S)   | 大量報價確認                               |

## 設定（QuoteHandlerConfig）

```json
{
  "QuoteHandler": {
    "QuoteValidityDuration": "00:00:30",
    "DefaultSpread": 0.10,
    "DefaultQuoteSize": 1000
  }
}
```

| 參數                    | 型別       | 預設值  | 說明                   |
|-------------------------|------------|---------|------------------------|
| `QuoteValidityDuration` | `TimeSpan` | 30 秒   | 報價有效期限           |
| `DefaultSpread`         | `decimal`  | 0.10    | 預設買賣價差           |
| `DefaultQuoteSize`      | `decimal`  | 1000    | 預設報價數量           |

## 報價計算公式

```
BidPx   = LastPrice − Spread / 2
AskPx   = LastPrice + Spread / 2
```

## DI 註冊範例

```csharp
builder.Services.Configure<QuoteHandlerConfig>(
    builder.Configuration.GetSection("QuoteHandler"));
builder.Services.AddSingleton<QuoteHandler>();
```

## 執行測試

```bash
dotnet test fixgate/Modules/
```
