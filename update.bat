REM @echo off
echo =========================================
echo   Storage Report - GitHub Auto-Updater
echo =========================================
echo.
echo Step 1: Fetching new data from SQL Server...
dotnet run
echo.

echo Step 2: Staging data.json for upload...
git add data.json
echo.

echo Step 3: Committing to history...
git commit -m "Automated update: %date% %time%"
echo.

echo Step 4: Publishing to GitHub Pages...
git push origin main
echo.

echo =========================================
echo   Update Complete! 
echo   Your website will reflect changes in 1-2 mins.
echo =========================================