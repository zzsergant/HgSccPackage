if not "%2"=="ReleaseDeploy" goto exit

echo Checking for sign script
if not exist "%~p0..\sign.bat" goto exit

echo Signing "%~p0\obj\%2\%1"
"%~p0..\sign.bat" "%~p0\obj\%2\%1"

:exit
