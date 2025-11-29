@echo off

cd /d "%~dp0"

title Let's Go Biking - Launcher

echo ==========================================
echo      DEMARRAGE LET'S GO BIKING
echo ==========================================

echo.
echo (1/5) Starting ActiveMQ...
start "" "C:\Users\sadlowe\Downloads\apache-activemq-6.1.8-bin\apache-activemq-6.1.8\bin\win64\activemq.bat"
timeout /t 4 >nul

pushd ".\ProxyCacheServer\bin\Debug"
start "" ProxyCacheService.exe
popd

echo.
echo (2/5) Starting RoutingService...
start "" ".\RoutingServer\bin\Debug\RoutingServer.exe"

echo.
echo (4/5) Starting NotificationService...
start "" ".\NotificationService\bin\Debug\NotificationService.exe"

echo.
echo [5] Lancement du serveur Frontend...
cd /d "c:\Users\sadlowe\source\repos\FrontEnd_LetsGoBiking"
start "Frontend Server" python -m http.server 8081
timeout /t 2 >nul

echo.
echo [6] Ouverture du Frontend dans le navigateur...
start "" http://localhost:8081/index.html

echo.
echo ==========================================
echo   TOUS LES SERVICES SONT LANCES !
echo ==========================================
echo.
echo Backend Services : Running
echo Frontend Server : http://localhost:8081
echo.
pause