@echo off
setlocal enabledelayedexpansion
title ISTQB Data Prep Pipeline

:MENU
cls
echo =========================================
echo ISTQB Data Preparation Pipeline
echo =========================================
echo.
echo Scanning current folder for CSV files...
echo.

:: 1. Scan the directory and build a numbered list
set count=0
for %%F in (*.csv) do (
    set /a count+=1
    set "file!count!=%%F"
    echo [!count!] %%F
)

:: 2. Display options based on whether files were found
if !count!==0 (
    echo [!] No CSV files found in this folder.
    echo.
    echo [P] Open File Picker ^(Search elsewhere^)
    echo [Q] Quit
) else (
    echo.
    echo [A] Process ALL listed CSV files above
    echo [P] Open File Picker ^(Search elsewhere^)
    echo [Q] Quit
)

echo.
set "choice="
set /p "choice=Select a number or option: "

:: 3. Handle non-number choices
if /i "!choice!"=="Q" exit /b
if /i "!choice!"=="P" goto PICKER
if /i "!choice!"=="A" goto PROCESS_ALL

:: 4. Handle number choice (Dynamic Array Lookup)
set "selectedFile=!file%choice%!"
if defined selectedFile (
    goto PROCESS_SINGLE
) else (
    echo.
    echo [!] Invalid choice. Please type a valid number or letter.
    pause
    goto MENU
)


:PICKER
echo.
echo [*] Opening file picker...
set "psCommand=Add-Type -AssemblyName System.Windows.Forms; $f = New-Object System.Windows.Forms.OpenFileDialog; $f.Filter = 'CSV Files (*.csv)|*.csv|All Files (*.*)|*.*'; $f.Title = 'Select ISTQB Source CSV'; $f.ShowHelp = $true; $f.ShowDialog() | Out-Null; $f.FileName"
for /f "usebackq delims=" %%I in (`powershell -NoProfile -Command "!psCommand!"`) do set "selectedFile=%%I"

if "!selectedFile!"=="" (
    echo [!] No file selected.
    pause
    goto MENU
)
goto PROCESS_SINGLE


:PROCESS_SINGLE
echo.
echo [+] Processing: "!selectedFile!"
python prepare_istqb_data.py "!selectedFile!"

if %ERRORLEVEL% NEQ 0 (
    echo [!] Python script crashed. Check the error above.
    pause
    goto MENU
)

echo [+] Done!
pause
goto MENU


:PROCESS_ALL
echo.
if !count!==0 (
    echo [!] Nothing to process.
    pause
    goto MENU
)

:: Loop through our simulated array and process each file
for /L %%I in (1,1,!count!) do (
    echo [+] Processing: "!file%%I!"
    python prepare_istqb_data.py "!file%%I!"
    
    if !ERRORLEVEL! NEQ 0 (
         echo [!] Error processing "!file%%I!". Stopping batch execution.
         pause
         goto MENU
    )
)

echo.
echo [+] All CSV files processed successfully!
pause
goto MENU