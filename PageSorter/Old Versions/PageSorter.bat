@echo off
title PageSorter by Raymond Tracer
rem PageSorter by Raymond Tracer.
rem Downloads Paperclip, runs it, and moves the patched server jar (PaperMC) into the previous directory.

set DownloadURL=https://papermc.io/ci/job/Paper-1.16/lastSuccessfulBuild/artifact/paperclip.jar
set OutputFilename=paperclip.jar
set /a UsePowerShell = 1

if exist %OutputFilename% goto loop

:powershellCheck
echo Checking PowerShell.
for %%i in (powershell.exe) do (
  if "%%~$path:i" == "" (
    echo Powershell isn't detected.
    echo Will attempt to use BITSAdmin instead.
    set /a UsePowerShell = 0
    goto afterPowershellCheck
  ) else (
    echo Getting PowerShell version.

    for /f "skip=3 tokens=2" %%j in ('powershell -command "$PSVersionTable"') do (
      if %%j == "" (
        echo Powershell is below verison 2.
        echo Will attempt to use BITSAdmin instead.
        set /a UsePowerShell = 0
        goto afterPowershellCheck
      ) else (
        echo Found PowerShell verison: %%j
        goto afterPowershellCheck
      )
    )
  )
)

:afterPowershellCheck
echo.

if %UsePowerShell% EQU 1 (
 echo Downloading "%OutputFilename%"...
 powershell -Command "(New-Object Net.WebClient).DownloadFile('%DownloadURL%', '%OutputFilename%')"
) else (
 echo Downloading "%OutputFilename%"...
 bitsadmin /transfer PaperclipDownload /download /priority normal %DownloadURL% %cd%\%OutputFilename%
)

echo Checking for "%OutputFilename%".

set /a MessageDisplayed = 0

:loop
if exist %OutputFilename% (
  echo Found "%OutputFilename%".
  goto foundFile
) else (
  if MessageDisplayed EQU 0 (
    set /a MessageDisplayed = 1
    echo "%OutputFilename%" not found, waiting for fileName...
    echo This means you'll have to download the fileName yourself
    echo and put it in the same directory this batch fileName is in.
  )
)
goto loop

:foundFile
rmdir /s /q work >nul 2> nul
mkdir work

move paperclip.jar work > nul

:executeJava
cd work
echo Executing "%OutputFilename%"...
echo.

java -jar %OutputFilename%

if %errorlevel% EQU 1 (
  echo %OutputFilename% failed to execute, restarting script.

  del /f /q %OutputFilename%
  cd ..
  goto powershellCheck
)

cd cache

echo.
echo Renaming and moving the server jar.

setlocal EnableExtensions EnableDelayedExpansion
for %%i in (*) do (
  set fileName=%%i

  if not "!fileName!" == "!fileName:patched=!" (
    ren %%i %OutputFilename%

    set /a MessageDisplayed = 0
    :attemptMove
    move /y %cd%\%OutputFilename% %cd%\..\..\..\%OutputFilename% > nul 2> nul

    if !errorlevel! EQU 1 (
      if !MessageDisplayed! EQU 0 (
        set /a MessageDisplayed = 1
        echo Something is already using %OutputFilename%, waiting for file to become free.
      )

      goto attemptMove
    )

    goto end
  )
)

:end
endlocal

echo Done!
pause
exit