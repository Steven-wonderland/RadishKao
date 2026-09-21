# CatPet

一隻住在 Windows 工作列上的貓。會自己散步、跑步、坐著發呆、抬頭張望、打瞌睡，點一下會有反應，
可以用滑鼠抓起來丟，也可以用快捷鍵丟毛線球或罐罐讓牠衝過去。
所有互動都寫在 `config.json`，不用改程式就能擴充。

WPF / .NET 9，沒有任何外部套件。

## 跑起來

```bash
cd C:\source\radishkao\CatPet && dotnet build -c Release
```

然後執行 `bin\Release\net9.0-windows\CatPet.exe`，或直接：

```bash
cd C:\source\radishkao\CatPet && dotnet run -c Release
```

想做成一個可以搬到別台機器的單一執行檔（不用裝 .NET）：

```bash
cd C:\source\radishkao\CatPet && dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

要發布並同步到 GitHub，執行專案根目錄的 `publish-and-push.cmd`。它會把完整可執行版本放到
`release\CatPet-win-x64\`，並透過 Git LFS 推送大型 `CatPet.exe`。發布資料夾請整個提供給使用者，不能只拿 exe，因為還需要 `Assets\`、`config.json` 與 WPF 原生檔案。

**開機自動啟動**：按 `Win+R` 輸入 `shell:startup`，把 `CatPet.exe` 的捷徑丟進去就好。

## 怎麼玩

| 操作 | 預設行為 |
|---|---|
| 左鍵單擊 | 隨機反應（抬頭看你 / 坐下）＋ 對話泡泡 |
| 左鍵連點兩下 | `config.json` 裡的 `doubleClick`（預設只是回你一句話） |
| 中鍵 | 想睡 |
| 趴著睡時點牠 | 起身伸懶腰＋打呵欠 |
| 拖曳 | 抓著後頸把貓拎起來，會掙扎；放開會掉回工作列上 |
| 右鍵 | 選單（動作 / 放誘餌 / 動作比例 / 放大縮小 / 暫停 / 重新載入設定 / 結束） |
| **Ctrl+F10** | 滑鼠變成粉紅毛線球，點一下放下，貓衝過去玩一下再跑回工作列 |
| **Ctrl+F11** | 滑鼠變成罐罐，點一下放下，貓衝過去吃完再跑回工作列 |
| 系統列圖示 | 左鍵＝抬頭看看，右鍵＝同一份選單 |

貓身體以外的地方**滑鼠會穿透過去**，所以牠壓在工作列按鈕上時不會擋到你點。

## config.json

存檔之後從選單選「重新載入 config.json」就會立刻套用，不用重開。

### 外觀

| 欄位 | 說明 |
|---|---|
| `spriteSheet` | 圖檔路徑（相對 exe 或絕對路徑） |
| `scale` | `1.0` = 原圖 192×208；`0.52` 約 100×108 |
| `walkSpeed` / `runSpeed` | 散步 / 衝刺速度，每秒像素 |
| `footOffset` | 正數＝往工作列裡踩深一點，負數＝往上浮 |
| `bubbleSeconds` | 對話泡泡停留秒數 |
| `draggable` | 能不能被拖曳 |
| `greeting` | 啟動時說的話；留空字串就安靜登場 |
| `depthEffect` / `minDepth` | 追誘餌時往上跑縮小、跑回來放大；`minDepth` 是畫面最上緣的縮放倍率 |
| `behaviors` | 各待機動作被抽中的相對機率，右鍵選單「動作比例」會改寫這段 |

右鍵調整過的動作比例也會另存到執行檔旁的 `behavior-ratios.json`。這個檔案會在下次啟動時自動讀回，請和 `CatPet.exe` 放在同一個資料夾。

### 互動

`triggers` 下面有五個觸發點：`leftClick`、`doubleClick`、`middleClick`、`pickUp`、`drop`。
每個觸發點是一個**陣列**，程式會依 `weight` 隨機抽一個來做。一個動作裡所有欄位都是選填的：

| 欄位 | 說明 |
|---|---|
| `animation` | 要播的動畫名稱（見下表） |
| `say` | 對話泡泡的內容 |
| `run` | 要開的程式、檔案或網址 |
| `args` | 傳給 `run` 的命令列參數 |
| `workingDirectory` | `run` 的工作目錄 |
| `weight` | 被抽中的相對機率，預設 `1` |

### 「點下去要做某件事」怎麼加

這就是 `run` 在做的事。幾個例子：

```jsonc
"doubleClick": [
  { "animation": "look_right", "say": "開工！", "run": "notepad.exe" },
  { "animation": "look_left", "say": "查一下", "run": "https://www.google.com" },
  { "animation": "sit", "say": "打開報表", "run": "C:\\work\\daily.xlsx" },
  { "animation": "idle", "run": "powershell.exe",
    "args": "-NoProfile -File C:\\scripts\\standup.ps1", "workingDirectory": "C:\\scripts" }
]
```

`run` 走的是 ShellExecute，所以程式、文件、資料夾、網址都吃得下。
路徑裡的 `\` 在 JSON 要寫成 `\\`。環境變數可以用，例如 `%USERPROFILE%\\Desktop`。

> `run` 會照你寫的直接執行，所以這個檔案請當成自己的腳本在管。

### 誘餌（Ctrl+F10 / Ctrl+F11）

`lures` 是一個陣列，每一筆就是一個快捷鍵。按下去之後滑鼠會變成那個東西，
點一下就放在該處，貓會衝過去、待一下、再跑回工作列。

| 欄位 | 說明 |
|---|---|
| `name` | 選單上顯示的名字 |
| `hotkey` | 全域快捷鍵，例如 `Ctrl+F10`、`Ctrl+Shift+B` |
| `image` | 游標和畫面上那個東西的圖 |
| `displaySize` | 放在畫面上的高度（dip） |
| `anchorX` / `anchorY` | 貓到達時，192×208 格子裡的哪一點要對準那個東西（腳掌線 y=202） |
| `arriveAnimation` | 到了之後播哪個動畫 |
| `arriveSeconds` | 在那邊待多久 |
| `hideOnArrive` | 動畫本身已經畫了那個東西時設 `true`（毛線球就是，`play` 動畫自帶一顆球） |
| `speedMultiplier` | 衝過去時是 `runSpeed` 的幾倍 |
| `say` / `arriveSay` | 放下時、到達時的台詞 |
| `hint` | 選位置時螢幕上方的提示字 |

照著複製一筆就能再加第三種誘餌，不用改程式。

### 可用的動畫名稱

`idle`、`walk_left`、`walk_right`、`run_left`、`run_right`、`held`、
`sleep_in`、`sleep_deep`、`sleep_out`、`sit`、`look_left`、`look_right`、
`play`（自帶毛球，建議只給 Ctrl+F10 的誘餌用）

## 右鍵選單

對著貓按右鍵，或對系統列圖示按右鍵，都是同一份選單。

| 項目 | 做什麼 |
|---|---|
| 抬頭看看 | 立刻播一次抬頭張望，並說「喵～」 |
| 跑一下 | 往螢幕比較空的那一側衝刺約 2.2 秒 |
| 去睡覺 | 立刻坐下打盹 9 秒再起來（不等隨機排程） |
| 煩人Mode | 找出「開始」按鈕、走過去、說「理我！」、撥它一下，爪子碰到的那一格彈出開始選單 |
| 放毛線球（Ctrl+F10） | 同快捷鍵：滑鼠變成毛線球，點一下放下 |
| 放罐罐（Ctrl+F11） | 同快捷鍵 |
| **動作比例** ▸ | 子選單，八個待機動作各自可選 **關閉 / 少一點 / 普通 / 多一點**。目前值會打勾，選完選單不會關掉，可以連續調好幾個 |
| 目前大小 45% | 只是顯示，不能點 |
| **放大 5%** | 每按一次放大一級（×1.05），選單留著讓你連按 |
| **縮小 5%** | 每按一次縮小一級（÷1.05） |
| 暫停動作 | 打勾後整隻貓定格，再點一次解除 |
| 重新載入 config.json | 重讀設定並套用，不用重開程式 |
| 開啟 config.json | 用預設編輯器打開設定檔 |
| 結束 | 關掉程式 |

**動作比例**的四個等級是相對於各動作的內建權重（散步本來就比打盹常見，所以兩者的
「普通」不是同一個數字）。改完會寫回 `config.json` 的 `behaviors` 區塊，
子選單最下面的「全部回到預設」會把整段清掉。

大小和比例都是**立即生效並存檔**的，下次啟動會記得。縮放後貓還是貼著工作列 ——
視窗位置是從腳掌線反推的，跟大小無關。

## 換一張圖

動畫分散在幾個檔案裡，都用同一種 192×208 的格子。對應表在
`Sprites/ClipLibrary.cs` 的 `Sheets` 陣列：

| 檔案 | 格數 | 內容 |
|---|---|---|
| `cat-spritesheet.png` | 8×11 | 原始主圖，見下表 |
| `run-front-back.png` | 8×1 | 0-3 背對鏡頭跑、4-7 面對鏡頭跑 |
| `doze-stretch.png` | 8×1 | 0-1 趴著呼吸、2-7 醒來→伸懶腰→坐起→打呵欠 |
| `held-squirm.png` | 6×1 | 被拎住掙扎（圖裡含一隻手） |
| `taskbar-tap.png` | 8×1 | 撥工作列圖示；爪子在第 4–5 格落下 |
| `side-roll.png` | 8×1 | 左右翻滾，**目前未載入** |

除了主圖之外都是選用的：檔案不在就只是那幾個動作失效，貓照跑。

`Assets/cat-spritesheet.png` 是 8 欄 × 11 列、每格 192×208 的網格：

| 列 | 格數 | 動作 |
|---|---|---|
| 0 | 6 | 站立待機（會眨眼） |
| 1 | 8 | 往右跑 |
| 2 | 8 | 往左跑 |
| 3 | 4 | 舉手打招呼（**沒用到**） |
| 4 | 5 | 跳躍（**沒用到**） |
| 5 | 8 | 坐下 → 垂耳 → 睡著 → 醒來 |
| 6 | 6 | 坐著玩毛球（只有 Ctrl+F10 丟球時才會播） |
| 7 | 6 | 坐著待機 |
| 8 | 6 | 舔爪子理毛（**沒用到**） |
| 9 | 8 | 抬頭張望 |
| 10 | 8 | 抬頭張望（反向） |

要換圖或加新動作，都在 `Sprites/ClipLibrary.cs` 動：`Sheets` 登記檔案和格數，
`All` 登記每個 clip。每個 clip 有三個跟畫面對齊有關的欄位：

| 欄位 | 用途 |
|---|---|
| `Baseline` | 腳掌在格子裡的 y。**每個 clip 各自一份** —— 原圖的側面跑是畫在 y=185，站姿是 y=200，共用一個值會讓跑步時浮在工作列上方 |
| `Zoom` | 純顯示用的大小微調，不影響腳掌線也不影響全域縮放。後面幾張圖比主圖略小時用這個補 |
| `Reverse` | 倒著播 |
| `Frames` | 直接指定要播哪幾格、什麼順序，取代 `Start`+`Count`。要跳過中間某一格、或讓某一段重複幾次就用這個 |

例如 `wake_stretch` 是 `Frames: [2, 3, 4, 3, 4, 3, 4, 6, 7]` —— 跳過第 5 格（坐著舉手），
並讓伸懶腰的 3/4 兩格來回三次。

## 想再加東西

| 想做的事 | 改哪裡 |
|---|---|
| 新增一個待機習慣（隨機會自己做的動作） | `Behavior/PetBrain.cs` 的 `Table`，加一筆權重和步驟 |
| 新增動畫片段 | `Sprites/ClipLibrary.cs` 的 `All` 陣列 |
| 新增觸發點（例如滑鼠移過去） | `PetWindow.xaml.cs` 呼叫 `Trigger("你的名字")`，再到 config 加同名的 key |
| 反應除了開程式還要做別的 | `Behavior/ActionRunner.Launch`，和 `Config/TriggerAction` 的欄位 |

## 找「開始」按鈕

`Interop/StartButton.cs`。每台電腦的工作列設定都不一樣，所以有兩條路：

| 路徑 | 適用 |
|---|---|
| `Shell_TrayWnd` 底下 class 為 `Start` 的子視窗 | Windows 10；也包含用 StartAllBack 之類把傳統工作列裝回去的 Win11 |
| UI Automation 找 `AutomationId = "StartButton"` | Windows 11 的 XAML 工作列 |

**第一次查詢時兩條都會跑一次做校準**。Win11 其實也留著那個舊的 `Start` 視窗，在我測的這台
上它的座標跟真正的按鈕完全吻合（差 1px，而且工作列內容變動時會一起移動），但別台不保證。
所以只有在兩條路算出來的中心點相差 4px 以內時，才信任比較快的舊視窗（之後每次 0–1ms）；
對不上就改走 UIA（每次 20–70ms，因此一律在背景執行緒跑，絕不擋住畫面）。

已經處理掉的機器差異：

- **工作列置中或靠左** —— Win11 的開始鈕會隨圖示增減左右移動，所以結果只快取 3 秒
- **工作列停在上下左右任一邊**
- **開始鈕在副螢幕的工作列上** —— 會掃 `Shell_SecondaryTrayWnd`
- **自動隱藏** —— 回傳 `OnScreen = false`，選單會告訴你，而不是把貓送到畫面外
- **任何顯示語言** —— 比對的是 `AutomationId`，不是會被翻譯的 `Name`

都找不到就回傳 null，貓不動作，並在泡泡說找不到。

### 貓要站在哪

不是對齊 sprite 格子，而是對齊**爪子落點**。撥的動畫裡爪子在格子的 x=109 落下
（`ClipLibrary.TapPawX`），所以站位是：

```
_walkToX = 開始鈕中心X − TapPawX × scale × dpi
```

用格子左緣對齊會差很多：貓的圖在格子裡本來就內縮 26px（`BodyLeftX`），加上爪子是往
身體左前方伸，兩者加起來會讓爪子打到開始鈕右邊約一個按鈕寬的地方 —— 也就是隔壁那個圖示。

開始選單彈出的時機同樣是算出來的：`TapContactFrame × TapFrameMs`，預設第 4 格 × 120ms
= 480ms。config 的 `startSwatDelayMs` 填 0 就用這個值，要自己指定就填毫秒（換成別的
撥圖、爪子落在不同格時會用到）。

### 開始選單怎麼打開

`Interop/StartMenu.cs` 預設送 `WM_SYSCOMMAND` / `SC_TASKLIST` 給工作列 —— 從 Win95
起這就是「打開開始選單」的意思，完全不碰鍵盤狀態。把 `startSwatUsesWinKey` 設成 true
會改成真的合成一次 Windows 鍵。兩種都實測可行，但後者萬一你當下按著 Shift 就會變成
Win+Shift+…，所以不是預設。

> **真的去改工作列圖示順序則做不到。** Windows 會辨識注入式輸入並忽略對自己介面的拖放，
> 實測三次（含明確是釘選的項目）都沒有效果。開始選單不受這個限制，因為那只是送一個
> 「請打開」的訊息或按鍵，不是拖放。

## 還缺的圖

| 缺的東西 | 現在的替代 |
|---|---|
| 吃罐罐的動作 | `sit`（坐著，沒有低頭吃的姿勢） |
| 罐罐本身 | `Assets/can.png` 是用程式畫的幾何圖形，跟手繪風格對不起來 |

新圖的格式跟現有的一樣：每格 192×208、腳掌落在格子裡 y≈200、去背 PNG、
一個動作一列由左往右排。給獨立的 PNG 就行，在 `ClipLibrary.Sheets` 加一行登記。
罐罐的圖直接覆蓋同名檔案即可，尺寸不限。

## 兩份 config.json

專案根目錄那份是**範本**，`bin\Release\net9.0-windows\config.json` 才是程式實際讀寫的那份。
右鍵選單改大小或動作比例時，寫的是後者。

build 用的是 `PreserveNewest`，所以只要你編過輸出那份，之後 build 就不會覆蓋它。
反過來說，改了根目錄那份要生效，得手動複製過去（或先刪掉輸出那份再 build）。

## 已知限制

- 只認**主螢幕的工作列**。工作列靠左／靠右／靠上時，貓會改成沿著工作區底邊走。
- 平常貓待在主螢幕，但誘餌可以丟到任何一台螢幕，貓會跑過去再回來。
- 選位置的半透明遮罩會蓋住全部螢幕，期間點擊只會用來放誘餌；Esc、右鍵、再按一次快捷鍵，或放著 12 秒都會取消。
- 兩台螢幕 DPI 不同時，位置可能會有幾個像素的偏差。
