# MyDll — MES 介面調用說明

> **適用版本：** commit `3364c1e`（含超時重傳 + 失敗圖片歸檔，2026-04-10 最新版）  
> **命名空間：** `MyDll`  
> **主要類別：** `MesClass`

---

## 目錄

1. [版本功能一覽](#1-版本功能一覽)
2. [快速開始（必讀）](#2-快速開始必讀)
3. [方法說明與呼叫範例](#3-方法說明與呼叫範例)
   - [SN_CheckOut（帶重試）](#31-sn_checkout帶重試)
   - [SN_FileUpload（帶重試 + 歸檔）](#32-sn_fileupload帶重試--歸檔)
4. [回傳值判斷](#4-回傳值判斷)
5. [失敗圖片歸檔說明](#5-失敗圖片歸檔說明)
6. [常見錯誤與注意事項](#6-常見錯誤與注意事項)
7. [升級舊版呼叫檢查清單](#7-升級舊版呼叫檢查清單)

---

## 1. 版本功能一覽

| 功能 | 舊方法（_0209） | 新方法（_WithRetry） |
|------|:--------------:|:-------------------:|
| 發送 MES 請求 | ✅ | ✅ |
| 超時自動重試（最多 3 次） | ❌ | ✅ |
| 僅超時重試、服務端錯誤不重試 | ❌ | ✅ |
| 失敗圖片自動歸檔（FileUpload 用） | ❌ | ✅ |
| 失敗日誌寫入 | ❌ | ✅ |

> **舊方法（`_0209`）仍然保留，可繼續使用。新方法為獨立新增，不影響現有邏輯。**

---

## 2. 快速開始（必讀）

### 步驟一：設定失敗圖片歸檔路徑

在程式**啟動時設定一次**（Main 函數或 Form Load），未設定則預設存到 exe 同目錄下的 `FailedImages\`：

```csharp
// 建議設定到有足夠空間的磁碟
MesClass.FailedImageRootPath = @"D:\MES_FailedImages";
```

### 步驟二：選擇正確方法

```
需要 SN CheckOut？  →  MesClass.SN_CheckOutRequest_WithRetry(...)
需要上傳圖片？      →  MesClass.SN_FileUploadRequest_WithRetry(...)
```

### 步驟三：判斷回傳值

```csharp
if      (response.StartsWith("Success"))   { /* 成功 */ }
else if (response.StartsWith("Timeout"))   { /* 超時，已重試 3 次 */ }
else if (response.StartsWith("Error"))     { /* 服務端錯誤 */ }
else if (response.StartsWith("Exception")) { /* 其他例外 */ }
```

---

## 3. 方法說明與呼叫範例

### 3.1 SN_CheckOut（帶重試）

#### 方法簽名

```csharp
public static string SN_CheckOutRequest_WithRetry(
    string Line,
    string StationID,
    string MachineID,
    string Mold,
    string OPID,
    string TOKen,
    string FixSN,
    string apiUrl,
    string sn,
    string result,
    string errCode,
    List<DcInfoItem>   dcInfoList,
    List<CompListItem> compList
)
```

#### 參數說明

| 參數 | 型別 | 說明 | 空值處理 |
|------|------|------|----------|
| `Line` | `string` | 產線編號 | 必填 |
| `StationID` | `string` | 站點 ID | 必填 |
| `MachineID` | `string` | 設備 ID | 必填 |
| `Mold` | `string` | 治具編號 | 無則傳 `""` |
| `OPID` | `string` | 操作員 ID | 必填 |
| `TOKen` | `string` | MES 認證 Token | 必填 |
| `FixSN` | `string` | 修復序列號 | 無則傳 `""` |
| `apiUrl` | `string` | MES API 位址 | 必填 |
| `sn` | `string` | 條碼序列號 | 必填 |
| `result` | `string` | 檢測結果 | `"PASS"` 或 `"FAIL"` |
| `errCode` | `string` | 錯誤代碼 | PASS 時傳 `""` |
| `dcInfoList` | `List<DcInfoItem>` | DC 量測資料列表 | 無則傳 `new List<DcInfoItem>()` |
| `compList` | `List<CompListItem>` | 組件列表 | 無則傳 `new List<CompListItem>()` |

#### 完整呼叫範例

```csharp
// 準備 DC 資料（無資料可傳空列表）
var dcInfoList = new List<DcInfoItem> {
    new DcInfoItem { Item = "Voltage", Value = "3.30", Result = "PASS" },
    new DcInfoItem { Item = "Current", Value = "0.52", Result = "PASS" }
};

var compList = new List<CompListItem> {
    new CompListItem { CompID = "R001", Qty = "1" }
};

// 呼叫
string response = MesClass.SN_CheckOutRequest_WithRetry(
    Line:       "L01",
    StationID:  "ICT_ST01",
    MachineID:  "MC001",
    Mold:       "MOLD_A",
    OPID:       "OP001",
    TOKen:      "your_mes_token_here",
    FixSN:      "",
    apiUrl:     "http://192.168.1.100/mes/api/checkout",
    sn:         "SN2026041000001",
    result:     "PASS",
    errCode:    "",
    dcInfoList: dcInfoList,
    compList:   compList
);

// 判斷結果
if (response.StartsWith("Success")) {
    Console.WriteLine("CheckOut 成功：" + response);
} else if (response.StartsWith("Timeout Error")) {
    Console.WriteLine("CheckOut 超時（已重試 3 次）：" + response);
} else {
    Console.WriteLine("CheckOut 失敗：" + response);
}
```

---

### 3.2 SN_FileUpload（帶重試 + 歸檔）

#### 方法簽名

```csharp
public static string SN_FileUploadRequest_WithRetry(
    string Line,
    string StationID,
    string MachineID,
    string OPID,
    string sn,
    string FileName,
    string FilePath,
    string apiUrl
)
```

#### 參數說明

| 參數 | 型別 | 說明 | 注意事項 |
|------|------|------|----------|
| `Line` | `string` | 產線編號 | 必填 |
| `StationID` | `string` | 站點 ID | 必填 |
| `MachineID` | `string` | 設備 ID | 必填 |
| `OPID` | `string` | 操作員 ID | 必填 |
| `sn` | `string` | 條碼序列號 | 必填 |
| `FileName` | `string` | 上傳的檔案名稱 | 例如 `"capture.jpg"` |
| `FilePath` | `string` | 圖片完整路徑 | ⚠️ 必須是 **MES Server 可存取**的路徑（UNC 或共享資料夾），非本機路徑 |
| `apiUrl` | `string` | MES API 位址 | 必填 |

#### 完整呼叫範例

```csharp
// [程式啟動時] 設定歸檔路徑（只需設定一次）
MesClass.FailedImageRootPath = @"D:\MES_FailedImages";

// 呼叫上傳
string response = MesClass.SN_FileUploadRequest_WithRetry(
    Line:      "L01",
    StationID: "ICT_ST01",
    MachineID: "MC001",
    OPID:      "OP001",
    sn:        "SN2026041000001",
    FileName:  "capture_front.jpg",
    FilePath:  @"\\fileserver\share\ICT\capture_front.jpg",  // MES 可存取路徑
    apiUrl:    "http://192.168.1.100/mes/api/fileupload"
);

// 判斷結果
if (response.StartsWith("Success")) {
    Console.WriteLine("上傳成功：" + response);
} else if (response.StartsWith("Timeout Error")) {
    // 已自動重試 3 次且已歸檔至 FailedImageRootPath
    Console.WriteLine("上傳超時，圖片已歸檔：" + response);
} else if (response.StartsWith("Error")) {
    // 服務端錯誤，不重試、不歸檔
    Console.WriteLine("服務端錯誤：" + response);
} else {
    Console.WriteLine("其他例外：" + response);
}
```

---

## 4. 回傳值判斷

| 回傳前綴 | 說明 | 是否重試 | CheckOut 是否歸檔 | FileUpload 是否歸檔 |
|----------|------|:--------:|:-----------------:|:-------------------:|
| `Success: ...` | HTTP 2xx，請求成功 | — | 否 | 否 |
| `Error: ...` | HTTP 非 2xx，服務端拒絕 | 否 | 否 | 否 |
| `Timeout Error: All 3 attempts...` | 3 次全部超時 | 已重試 3 次 | 否 | **是** |
| `Exception: ...` | 網路或其他例外 | 否 | 否 | 否 |

---

## 5. 失敗圖片歸檔說明

> 僅 `SN_FileUploadRequest_WithRetry` 在 **3 次全部超時** 時觸發，服務端錯誤不觸發。

### 歸檔目錄結構

```
D:\MES_FailedImages\                          ← FailedImageRootPath
└── 20260410\                                 ← 依日期分層
    ├── 20260410_143022_123\                  ← 每次失敗事件（精確到毫秒）
    │   └── capture_front.jpg                 ← 圖片副本（同名自動加 _1, _2...）
    └── failed_upload_log_20260410.txt        ← 當日失敗日誌（累加）
```

### 日誌內容範例

```
------------------------------------------------------------
FailedTime:        2026-04-10 14:30:22.123
SN:                SN2026041000001
SourceImagePath:   \\fileserver\share\ICT\capture_front.jpg
ArchivedImagePath: D:\MES_FailedImages\20260410\20260410_143022_123\capture_front.jpg
RetryCount:        3
ErrorMessage:      Timeout Error: All 3 attempts timed out after 5s each.
ApiUrl:            http://192.168.1.100/mes/api/fileupload
```

### 重要特性

- 歸檔失敗**不影響**方法回傳值，主流程不受干擾
- 圖片複製與日誌寫入為執行緒安全（多執行緒同時上傳安全）
- 來源圖片不存在時，仍會寫入日誌（記錄錯誤訊息）

---

## 6. 常見錯誤與注意事項

| 情境 | 錯誤做法 | 正確做法 |
|------|----------|----------|
| 判斷成功 | `response == "Success"` | `response.StartsWith("Success")` |
| 圖片路徑 | 傳本機路徑 `C:\image.jpg` | 傳 MES Server 可存取的 UNC 路徑 `\\server\share\image.jpg` |
| 歸檔路徑 | 未設定，使用預設路徑部署生產 | 程式啟動時明確設定 `MesClass.FailedImageRootPath` |
| 期望服務端錯誤也重試 | 直接用 WithRetry 方法 | WithRetry 僅重試超時；服務端錯誤不重試，需自行包裝 |
| 混用新舊方法 | 同一流程混用 `_0209` 與 `_WithRetry` | 選定一種並統一，避免行為不一致 |
| 磁碟管理 | 歸檔目錄從不清理 | 需規劃定期清理舊日期資料夾的策略 |

---

## 7. 升級舊版呼叫檢查清單

從舊版 `_0209` 方法升級至新版 `_WithRetry` 方法，請逐項確認：

```
□ 已取得包含 commit 3364c1e 的最新版 MyDll.dll
□ 程式啟動時已加入 MesClass.FailedImageRootPath = @"目標路徑"
□ 目標磁碟空間充足，且程式帳號具有該目錄的寫入權限
□ SN_CheckOut 呼叫已改為 SN_CheckOutRequest_WithRetry(...)
□ SN_FileUpload 呼叫已改為 SN_FileUploadRequest_WithRetry(...)
□ 回傳值判斷已新增 "Timeout Error" 與 "Exception" 分支
□ FilePath 參數確認使用 MES Server 可存取的網路路徑
□ 已在測試環境模擬超時（斷網或假 URL）確認重試行為正常
□ 已確認超時後 FailedImageRootPath 目錄有正確產生歸檔資料夾與日誌
□ 已規劃 FailedImages 目錄的定期清理策略
```

---

## 8. 現場端（上位機）實作整合指南與代碼範本

為了確保現場機台的穩定性（即使遇到廠區網路斷線也不會使停機台卡站），請現場負責人直接複製以下的「最佳實踐架構」到上位機程式中：

### 步驟一：設定安全歸檔目錄（全域執行一次）
在機台主程式（例如 `Form1_Load` 或是 `Program.cs`）的啟動處，定義一個本機的安全路徑。

```csharp
// 【程式啟動時設定】建議設定在主控電腦空間充足且非系統槽的獨立硬碟目錄
MyDll.MesClass.FailedImageRootPath = @"D:\MES_FailedImages_Archive";
```

### 步驟二：測試完成站點的防呆處理框架
當單一個產品測試完成，呼叫上傳圖片邏輯時，請套用此判斷邏輯，確保「非致命異常」不會阻斷產線：

```csharp
// 1. 準備呼叫參數
string currentSn = "CCAE173002551";
string imageFileName = "ResultImage.jpg";
string imageFullPath = @"C:\TestImages\ResultImage.jpg"; // 本機產生的圖片路徑

// 2. 呼叫底層防護上傳方法 (自動含三次重傳防護)
string response = MyDll.MesClass.SN_FileUploadRequest_WithRetry(
    Line: "F-PA-02",
    StationID: "OQC_AVI",
    MachineID: "F-02-M9-AV-01",
    OPID: "12280738",
    sn: currentSn,
    FileName: imageFileName,
    FilePath: imageFullPath,
    apiUrl: "https://your-mes-server.com/api/upload"
);

// 3. 處理回傳字串（影響產線是否繼續的關鍵）
if (response.StartsWith("Success"))
{
    // 【✅ 情況 A：上傳成功】
    // 流程正常，亮綠燈或記錄成功日誌
    Log("MES資料與圖片完整上傳成功。");
}
else if (response.StartsWith("Timeout Error"))
{
    // 【⚠️ 情況 B：網路超時且重傳3次皆失敗】
    // 重點：DLL已經默默幫你在 D:\MES_FailedImages_Archive 建立副本了！
    // 處置：只需記錄日誌，接著讓程式「直接放行」，機台繼續測試下一個零件，千萬別拋出阻塞提示框！
    Log($"【警告】MES網路異常放棄上傳，圖片已自動本地歸檔。條碼：{currentSn}");
    
    // -> DO NOT STOP 機台，讓輸送帶繼續
}
else
{
    // 【❌ 情況 C：系統服務異常 (網址錯、MES回應500等)】
    // 此類非網路延遲問題，表示設定錯誤或MES端觸發防呆！
    // 處置：考慮跳出警報暫停產線，通知 IT 或生管人員確認。
    Log("發生嚴重致命錯誤：" + response);
    
    // -> 這裡可視業務需求決定是否中斷測試
}
```

---

*文件對應版本：commit `3364c1e` — 最新超時與實作框架增強版*
