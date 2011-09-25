if not "%2"=="ReleaseDeploy" goto exit

echo Checking for sign script
if not exist "%~dp0..\sign.bat" goto exit

echo Signing %1
"%~dp0..\sign.bat" %1

:exit
