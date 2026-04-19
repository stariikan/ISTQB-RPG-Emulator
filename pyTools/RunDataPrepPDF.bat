@echo off
setlocal enabledelayedexpansion
title ISTQB Omni-Pipeline

:MENU
cls
echo =========================================
echo ISTQB Omni-Data Pipeline (CSV + PDF)
echo =========================================
echo.

set count=0
:: Scan for BOTH CSV and PDF
for %%F in (*.csv *.pdf) do (
    set /a count+=1
    set "file!count!=%%F"
    echo [!count!] %%F
)

if !count!==0 (
    echo [!] No files found. Drop CSV or PDF here.
    pause & goto MENU
)

echo.
echo [A] Process ALL (Single Files)
echo [M] Merge Split Exam (Question + Answer PDFs)
echo [Q] Quit
echo.
set /p "choice=Select a number or option: "

if /i "!choice!"=="Q" exit /b
if /i "!choice!"=="A" goto PROCESS_ALL
if /i "!choice!"=="M" goto MERGE_MODE

:: Check if the choice is a number and exists
set "selectedFile="
for /L %%I in (1,1,!count!) do (
    if "!choice!"=="%%I" set "selectedFile=!file%%I!"
)

if defined selectedFile (
    goto PROCESS_SINGLE
) else (
    echo [!] Invalid selection.
    pause
    goto MENU
)

:MERGE_MODE
echo.
echo === PAIRING MODE ===
echo Tip: You can drag and drop the files directly into this window.
echo.
set /p "q_file=Drag and Drop the QUESTIONS PDF: "
set /p "a_file=Drag and Drop the ANSWERS PDF: "

:: Removing potential extra quotes from drag-and-drop to avoid ""path"" errors
set "q_file=%q_file:"=%"
set "a_file=%a_file:"=%"

echo.
echo [+] Merging Split Exams...
python process_split_exam.py "!q_file!" "!a_file!"
if %ERRORLEVEL% NEQ 0 pause
goto MENU

:PROCESS_SINGLE
echo.
echo [+] Processing Single File: "!selectedFile!"
python process_istqb.py "!selectedFile!"
if %ERRORLEVEL% NEQ 0 pause
goto MENU

:PROCESS_ALL
echo.
echo [+] Starting Batch Process...
for /L %%I in (1,1,!count!) do (
    set "currentFile=!file%%I!"
    echo [+] Processing: "!currentFile!"
    python process_istqb.py "!currentFile!"
)
echo.
echo [+] All files finished.
pause
goto MENU