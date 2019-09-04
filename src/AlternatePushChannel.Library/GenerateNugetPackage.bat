@echo off

set files=bin\Release\AlternatePushChannel.Library.winmd
set files=%files% bin\Release\AlternatePushChannel.Library.pri
set files=%files% bin\Release\AlternatePushChannel.Library.pdb
set files=%files% bin\Release\AlternatePushChannel.Library.xml

FOR %%f IN (%files%) DO IF NOT EXIST %%f call :file_not_found %%f


echo Here are the current timestamps on the DLL's...
echo.

FOR %%f IN (%files%) DO ECHO %%~tf %%f

echo.

PAUSE



echo Welcome, let's create a new NuGet package for AlternatePushChannel.Library!
echo.

set /p version="Enter Version Number (ex. 0.1.0-beta): "

if not exist "NugetPackages" mkdir "NugetPackages"

"C:\Program Files (x86)\NuGet\nuget.exe" pack AlternatePushChannel.Library.nuspec -Version %version% -OutputDirectory "NugetPackages"

PAUSE

explorer NugetPackages




exit
:file_not_found

echo File not found: %1
PAUSE
exit
