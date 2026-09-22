@echo off
rem Tell the cat something happened.
rem
rem   catpet-notify.cmd <trigger> [text ...]
rem
rem <trigger> is a key under "triggers" in config.json; anything after it
rem becomes the speech bubble instead of the line written there. Drops a file
rem into the inbox folder the running cat watches, so there is nothing to
rem connect to and it does not matter whether the cat is running yet -- a drop
rem made while it is closed is discarded at its next start-up rather than
rem replayed late.
rem
rem Exits 0 even when it cannot write: a notification hook should never fail
rem the thing that called it just because the cat is not set up.

setlocal

if "%~1"=="" (
  echo usage: catpet-notify.cmd ^<trigger^> [text ...] 1>&2
  exit /b 0
)

set "INBOX=%LOCALAPPDATA%\CatPet\inbox"
if not exist "%INBOX%" mkdir "%INBOX%" 2>nul

set "TRIGGER=%~1"
shift

rem Collect the rest of the line as the bubble text.
set "TEXT="
:collect
if "%~1"=="" goto write
if defined TEXT (set "TEXT=%TEXT% %~1") else (set "TEXT=%~1")
shift
goto collect

:write
rem %RANDOM% and the clock keep two notifications in the same second apart.
set "STAMP=%RANDOM%%TIME:~9,2%"
set "DROP=%INBOX%\%TRIGGER%.%STAMP%.txt"

rem Write under a "~" name and rename once finished. The cat's watcher fires on
rem the file appearing, which for a plain redirect is before the text has been
rem written -- it would read an empty file and lose the message. Names starting
rem with "~" are skipped, so the rename is what publishes the drop.
set "PARTIAL=%INBOX%\~%TRIGGER%.%STAMP%"

if defined TEXT (
  >"%PARTIAL%" echo %TEXT%
) else (
  type nul >"%PARTIAL%"
)

move /y "%PARTIAL%" "%DROP%" >nul 2>&1

exit /b 0
