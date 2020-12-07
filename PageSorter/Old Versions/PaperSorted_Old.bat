@echo off
rem Created by Raymond Tracer

set DownloadURL=https://papermc.io/ci/job/Paper-1.16/lastSuccessfulBuild/artifact/paperclip.jar
set OutputFilename=paperclip.jar
set /a UsePowerShell = 1
set /a PowerShellVersion = -1

if exist %OutputFilename% (
  echo Found "%OutputFilename%".

  if exist work (
    rmdir /s /q work
  )
  mkdir work
  
  move paperclip.jar work
  goto executeJava
)

:powershellCheck
echo Checking PowerShell....
for %%i in (powershell.exe) do (
  if "%%~$path:i" == "" (
    echo Powershell isn't detected.
    echo Will attempt to use BITSAdmin instead.
    set /a UsePowerShell = 0
    pause
    goto afterPowershellCheck
  ) else (
    echo Getting PowerShell version...

    for /f "skip=3 tokens=2" %%j in ('powershell -command "$PSVersionTable"') do (
      if %%j == "" (
        echo Powershell is below verison 2.
        echo Will attempt to use BITSAdmin instead.
        set /a UsePowerShell = 0
        pause
        goto afterPowershellCheck
      ) else (
        echo Found PowerShell verison: %%j

        for /f "delims=." %%k in ("%%j") do (
          set /a PowerShellVersion = %%k
          goto afterPowershellCheck
        )
      )
    )
  )
)

:afterPowershellCheck
echo.

if %UsePowerShell% EQU 1 (
  goto psVersion2
  if PowerShellVersion EQU 2 goto psVersion2
  if PowerShellVersion GEQ 3 goto psVersion3AndHigher
) else (
  echo Downloading "%OutputFilename%".
  bitsadmin /transfer paperclipDownload /download /priority normal %DownloadURL% %cd%\%OutputFilename%
)

goto preLoop

:psVersion3AndHigher
echo Downloading "%OutputFilename%".
powershell -Command "Invoke-WebRequest %DownloadURL% -OutFile %OutputFilename%"
goto preLoop

:psVersion2
echo Downloading "%OutputFilename%".
powershell -Command "(New-Object Net.WebClient).DownloadFile('%DownloadURL%', '%OutputFilename%')"

:preLoop
if exist work (
  rmdir /s /q work
)

mkdir work

echo Checking for "%OutputFilename%".

set /a MessageDisplayed = 0

:loop
if exist %OutputFilename% (
  echo Found "%OutputFilename%".
  move paperclip.jar work

  goto executeJava
) else (
  if MessageDisplayed EQU 0 (
    set /a MessageDisplayed = 1
    echo "%OutputFilename%" not found, waiting for file...
    echo This means you'll have to download the file yourself
    echo and put it in the same directory this batch file is in.
  )
)
goto loop

:executeJava
cd work
echo Executing "%OutputFilename%".
echo.

java -jar %OutputFilename%

if %errorlevel% EQU 1 (
  echo %OutputFilename% failed to execute, restarting script.

  del /f /q %OutputFilename%
  cd %cdCopy%\..
  goto powershellCheck
)

cd cache

echo.
echo Renaming and moving the server jar.

setlocal enableextensions enabledelayedexpansion
for %%i in (*) do (
  set temp=%%i

  if not "%temp%" == "%temp:patched=%" (
    ren %temp% %OutputFilename%

    set /a MessageDisplayed = 0
    :attemptMove
    move /y %OutputFilename% ..\..\..\%OutputFilename%

    if %errorlevel% EQU 1 (
      if %MessageDisplayed% EQU 0 (
        set /a MessageDisplayed = 1
        echo Something is already using %OutputFilename%, waiting for file to become free.
      )

      goto attemptMove
    )
  )
)
endlocal

echo Done!

pause
exit