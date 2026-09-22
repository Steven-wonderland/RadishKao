# CatPet

一隻住在 Windows 工作列上的貓。會自己散步、跑步、坐著發呆、抬頭張望、打瞌睡，點一下會有反應，
可以用滑鼠抓起來丟，也可以用快捷鍵丟毛線球或罐罐讓牠衝過去。
所有互動都寫在 `config.json`，不用改程式就能擴充。

WPF / .NET 9，沒有任何外部套件。

## 跑起來

### 一般使用者

請到 GitHub 的 **Releases** 下載 `CatPet-win-x64.zip`，解壓縮整個資料夾後，雙擊
`CatPet.exe` 即可。不要使用 GitHub 的「Download ZIP」下載可攜版；那是原始碼壓縮檔，
其中的 Git LFS 大型檔案可能只有約 1 KB 的指標檔，Windows 會顯示「此應用程式無法在您的
電腦上執行」。

### 維護者發布版本

在專案根目錄執行 `publish-and-push.cmd`。它會建立完整的
`release\CatPet-win-x64.zip`，請將這個 ZIP 上傳到 GitHub **Releases → Assets**，
讓一般使用者直接下載。`release\CatPet-win-x64\` 仍會同步到 Git LFS，供開發者使用，
但不要把原始碼 ZIP 當成使用者下載檔。

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

**publish 為什麼要跑四五分鐘**（`dotnet build` 只要十幾秒）：`-r win-x64` 讓輸出改到
`bin\Release\net9.0-windows\win-x64\`，跟一般 build 不同資料夾，等於整包重編；
`--self-contained` 要搬整個 .NET + WPF runtime（130MB 以上）；`PublishSingleFile=true`
再把那些打包成一顆 137MB 的 exe，然後防毒會把這顆新檔從頭掃一遍。

開發時請用 `dotnet build -c Release` 跑 `bin\` 那份，只有真的要發版才 publish。

**直接 `-o release\CatPet-win-x64` 會失敗**，實測連三次：

```
error MSB4018: "GenerateBundle" 工作發生未預期的失敗。
System.IO.IOException: The process cannot access the file
'...\release\CatPet-win-x64\CatPet.exe' because it is being used by another process.
```

單檔打包會先把中間產物複製到輸出資料夾、再覆寫成最終那顆 exe，而專案資料夾裡有東西
（防毒即時掃描、編輯器或同步工具的檔案監看）會在這兩步之間抓住那個檔案。**輸出到專案樹
外面就不會**：

```bash
dotnet publish CatPet.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o %TEMP%\catpet-pub
```

成功後把整個資料夾複製到 `release\CatPet-win-x64\` 再打包即可。

失敗時會留下一顆**約 9.8MB** 的 `CatPet.exe` 在輸出資料夾裡 —— 那是中間產物，不是成品。
正常的 self-contained 單檔是 **約 137MB**（壓縮後 ZIP 約 65MB）。發版前請先確認大小。

打 ZIP 用 .NET 的 `ZipFile`（約 10 秒）比 `publish-and-push.cmd` 裡的 `Compress-Archive`
快很多：

```powershell
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory('release\CatPet-win-x64', 'release\CatPet-win-x64.zip', 'Optimal', $false)
```

想連這些麻煩一起省掉，就拿掉 `PublishSingleFile` —— 反正發布本來就是整個資料夾給使用者
（還需要 `Assets\`、`config.json` 和 WPF 原生 DLL），單檔打包換到的好處不多，卻是最慢、
也是唯一會這樣失敗的一步。

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

`triggers` 下面有五個內建觸發點：`leftClick`、`doubleClick`、`middleClick`、`pickUp`、`drop`。
除此之外，名字隨你取的觸發點可以從外面叫（見「接外面來的通知」）。
每個觸發點是一個**陣列**，程式會依 `weight` 隨機抽一個來做。一個動作裡所有欄位都是選填的：

| 欄位 | 說明 |
|---|---|
| `animation` | 要播的動畫名稱（見下表） |
| `say` | 對話泡泡的內容 |
| `icon` | 泡泡左邊的小圖（16×16），例如通知來源的 logo |
| `bubbleSeconds` | 只有這個動作的泡泡停留秒數，蓋掉全域的 `bubbleSeconds` |
| `onClick` | 泡泡還在的時候點貓要做什麼（本身也是一個動作，可以有 `run` / `say` / `animation`）。泡泡淡掉就失效 |
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

### 接外面來的通知

貓會盯著 `%LOCALAPPDATA%\CatPet\inbox` 這個資料夾。往裡面丟一個檔案，檔名第一個點之前
那段就是觸發點名字，檔案內容（有的話）會蓋掉 config 裡的 `say`。附的 `catpet-notify.cmd`
就是在做這件事：

```bash
catpet-notify.cmd claudeNotification
catpet-notify.cmd claudeNotification "Claude 在等你授權"
```

觸發點名字隨你取，在 `triggers` 加一個同名的 key 就會有反應。程式沒開時丟的檔案不會被補播 ——
下次啟動會直接清掉，不然一開機就會冒出昨天的通知。

`catpet-notify.cmd` 是先寫成 `~` 開頭的暫存檔、寫完才 rename 成正式檔名的。這是必要的：
`FileSystemWatcher` 在「檔案出現」時就會觸發，而單純的 `> file` 重導向是先建檔、後寫入，
貓會讀到空檔案、把你的文字吃掉再刪除。`~` 開頭的檔案會被略過，所以 rename 才是「發布」
那個動作。自己寫程式丟檔案的話請照這個做（讀取端另外對空內容做了幾十毫秒的重試，
但別依賴它）。

> **從命令列傳的文字只能用 ANSI 範圍內的字元。** `catpet-notify.cmd` 的 `echo` 走的是
> 主控台字碼頁（這台是 cp950），`♥`、emoji 這類字元會變成 `?`。中文沒問題。要讓貓講
> 這種字元，請寫在 `config.json` 的 `say` / `finishSay` 裡 —— config 是以 UTF-8 解析的。

用資料夾而不是 named pipe，是因為要維持零外部套件（要把 pipe 限定單一使用者得多裝
`System.IO.Pipes.AccessControl`），而且 `%LOCALAPPDATA%` 本身的 NTFS 權限就只有你自己
進得去，不用自己寫 ACL。

| 設定 | 說明 |
|---|---|
| `notifications` | 要不要聽通知。右鍵選單的**接收通知**同步這一項，改完立刻存回 config |
| `notifyWakeFirst` | 睡著時收到通知，先伸懶腰起身再講話（趴睡播 `wake_stretch`，坐睡播 `sleep_out`），`false` 就直接冒泡泡 |
| 動作的 `icon` | 泡泡左邊的小圖，16x16 顯示。任何 PNG 都行 |
| 動作的 `bubbleSeconds` | 這則通知的泡泡留多久。Claude 那兩個設 10 秒 |
| 動作的 `onClick` | 泡泡還在時點貓要做什麼。Claude 那兩個是把 Claude 叫到前面來 |

**泡泡就是按鈕**：帶 `onClick` 的通知冒出來之後，那段時間內點貓會執行 `onClick`
而不是平常的點擊反應；泡泡淡掉就自動解除。這段時間內「煩人Mode」不會被抽中，
否則牠會走開、講自己的台詞，把還能點的泡泡蓋掉。

`icon` 的路徑可以放一個 `*`，會挑最新的那個，給裝在帶版號資料夾裡的程式用。
**但 `C:\Program Files\WindowsApps` 不行** —— 它的 ACL 不讓一般使用者列出根目錄
（指名整包路徑讀得到，列不出來），所以萬用字元在那裡展不開。MSIX 裝的程式請先把圖
複製出來一份再指過去。

### 讓 Claude Code 通知貓

Claude Code 的 hook 可以直接呼叫 `catpet-notify.cmd`。`~/.claude/settings.json`：

```jsonc
"hooks": {
  // Claude 在等你回應（授權、選項、問題）
  "Notification": [{ "hooks": [{ "type": "command", "command": "cmd",
    "args": ["/c", "C:\\source\\radishkao\\CatPet\\bin\\Release\\net9.0-windows\\catpet-notify.cmd",
             "claudeNotification"], "async": true }] }],

  // 做完一輪、把話語權交回給你
  "Stop": [{ "hooks": [{ "type": "command", "command": "cmd",
    "args": ["/c", "C:\\source\\radishkao\\CatPet\\bin\\Release\\net9.0-windows\\catpet-notify.cmd",
             "claudeDone"], "async": true }] }]
}
```

用 `args` 的 exec 形式（不經過 shell），路徑裡的反斜線就不用再多跳一層。
`async` 讓 hook 不擋住 Claude。

想讓貓唸出通知的原文而不是 config 裡寫好的台詞，就把訊息當第二個參數傳進去
（Notification hook 的 stdin JSON 有 `message` 欄位）。

> **這條路不是攔 Windows 通知**，是讓 Claude 主動講。好處是語意精準 —— 分得出「在等你」
> 和「做完了」，而不是只知道「有個泡泡跳出來」，也不會因為 Windows 改版壞掉。
> 要收**全系統所有 app** 的通知得走別條路（`UserNotificationListener` 需要 package
> identity，或輪詢 `wpndatabase.db` 需要 SQLite），目前都沒做。

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
| `finishSay` | `arriveSeconds` 到了、轉頭跑回工作列時的台詞 —— 「吃完了」那一刻。罐罐用它冒 `♥♥♥` |
| `hint` | 選位置時螢幕上方的提示字 |

照著複製一筆就能再加第三種誘餌，不用改程式。

### 泡泡裡能放什麼符號

WPF **不支援彩色字型**，所以 emoji 一律是單色，而且 Segoe UI Emoji 的單色字形有漸層，
放大後會被抖動成棋盤紋，很醜。實測結果：

| 字元 | 結果 |
|---|---|
| `♥` U+2665 | **乾淨的實心愛心**，來自文字字型（Microsoft JhengHei / Segoe UI Symbol） |
| `♡` U+2661 | **乾淨的空心愛心** |
| `❤` U+2764、`❤️`、`💕` | 畫得出來，但網點化 |
| `😻` U+1F63B | 糊成一團 |

所以想要愛心、星星這類符號，挑**文字字型裡就有的**（`♥ ♡ ★ ☆ ♪ ✿`）而不是 emoji。
真的要彩色的話，用動作的 `icon` 指一張 PNG —— 那是圖片，不受字型限制。

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
| 接收通知 | 打勾＝聽外面來的通知（見「接外面來的通知」），取消＝完全不理。狀態會存回 config |
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
| `held-squirm.png` | 6×1 | 被拎住掙扎（圖裡含一隻手；貓本身畫得比別張小，見下方 `Zoom`） |
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
| 7 | 6 | 坐著待機（**這列畫得比較大**，`sit` 用 `Zoom: 0.86` 縮回去） |
| 8 | 6 | 舔爪子理毛（**沒用到**） |
| 9 | 8 | 抬頭張望 |
| 10 | 8 | 抬頭張望（反向） |

要換圖或加新動作，都在 `Sprites/ClipLibrary.cs` 動：`Sheets` 登記檔案和格數，
`All` 登記每個 clip。每個 clip 有三個跟畫面對齊有關的欄位：

| 欄位 | 用途 |
|---|---|
| `Baseline` | 腳掌在格子裡的 y。**每個 clip 各自一份** —— 原圖的側面跑是畫在 y=185，站姿是 y=200，共用一個值會讓跑步時浮在工作列上方 |
| `Zoom` | 純顯示用的大小微調，不影響腳掌線也不影響全域縮放。某張圖裡的貓畫得比主圖小時用這個補 |
| `Reverse` | 倒著播 |
| `Frames` | 直接指定要播哪幾格、什麼順序，取代 `Start`+`Count`。要跳過中間某一格、或讓某一段重複幾次就用這個 |

例如 `wake_stretch` 是 `Frames: [2, 3, 4, 3, 4, 3, 4, 6, 7]` —— 跳過第 5 格（坐著舉手），
並讓伸懶腰的 3/4 兩格來回三次。

### Zoom 要填多少：先量，不要憑感覺

判斷一張圖是不是「畫得比較小」，量**頭寬**最準，不要量整格的邊界 —— 姿勢會騙人
（趴睡的貓整體矮一截，被拎起來的貓整體窄一截，但兩者的頭可能都是正常大小）。

實測值，供日後比對：

| 圖 | 頭寬 | 整格邊界 | 結論 |
|---|---|---|---|
| 主圖站姿 `r0c1` | 約 104 | 139×194，腳掌 y=200 | 基準 |
| 主圖 `r5c0`（睡覺列第一格） | 約 110 | 137×194，腳掌 y=200 | 跟基準同尺度，`Zoom` 應為 `1.0` |
| `held-squirm.png` | 約 79 | 寬只有 79（吊著所以窄） | 真的小約 25%，需要 `Zoom ≈ 1.3` |
| 主圖 `r6c0`（玩毛球，同樣是坐姿） | 約 106 | 136×184，腳掌 y=200 | 跟基準同尺度 |
| 主圖 row 7（坐著待機，六格平均） | 約 121 | 寬 149–163 | 真的大約 16%，`Zoom = 0.86` |

row 7 是主圖裡唯一畫歪尺度的一列：同樣是坐姿，row 6 的頭量起來 106 跟基準吻合，
row 7 卻是 121，牠一坐下待機就整隻脹一圈。`Zoom = 0.86`（104 ÷ 121）縮回去；
小於 1 的縮放一樣以腳掌點為原點，所以只是往下縮，不會離開工作列。

`held-squirm` 會小的原因是構圖：**那隻手畫在貓的上方**，手和貓一起塞進 192×208 的
格子，貓就只佔格子的一部分；別張圖的貓是獨佔整格的。切圖沒切錯，只能用 `Zoom` 補。

`Zoom` 大於 1 會有個副作用：縮放是**以腳掌點為原點**的，所以放大等於往上長，圖會頂進
上面那條留給泡泡的 54 dip。被拎起來時最明顯 —— 補正幅度大，貓的頭會壓到自己的台詞。
`PetWindow.KeepBubbleClearOfArt` 會把泡泡往上挪掉溢出的量（`腳掌線 × scale × (zoom−1)`），
上限是那條 54 dip 還剩多少空位，因為再上去就超出視窗上緣被裁掉了。

睡覺列（主圖 row 5）的高度從 194 一路掉到 132，那是牠逐格蹲下去的動作，不是畫小 ——
這裡曾經被誤填成 `0.88`，結果整段睡覺動畫都小了 12%。

## 想再加東西

| 想做的事 | 改哪裡 |
|---|---|
| 新增一個待機習慣（隨機會自己做的動作） | `Behavior/PetBrain.cs` 的 `Table`，加一筆權重和步驟 |
| 新增動畫片段 | `Sprites/ClipLibrary.cs` 的 `All` 陣列 |
| 新增觸發點（例如滑鼠移過去） | `PetWindow.xaml.cs` 呼叫 `Trigger("你的名字")`，再到 config 加同名的 key。從外面觸發的不用改程式 |
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
| 撥東西的動作 | `sit`，由 config 的 `startSwatAnimation` 指定，圖進來改這個值就好 |
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
