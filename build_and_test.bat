@echo off
chcp 65001 >nul
setlocal

set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set PROJ=D:\04_Work\Aqorse\01_project
set NEWTON=%PROJ%\packages\Newtonsoft.Json.13.0.4\lib\net45\Newtonsoft.Json.dll

set DLL_OUT=%PROJ%\MyDll\bin\Debug
set EXE_OUT=%PROJ%\MainTestMydll\bin\Debug

if not exist "%DLL_OUT%" mkdir "%DLL_OUT%"
if not exist "%EXE_OUT%" mkdir "%EXE_OUT%"

echo ============================================================
echo [1/2] 编译 MyDll.dll ...
echo ============================================================

"%CSC%" ^
    /target:library ^
    /out:"%DLL_OUT%\MyDll.dll" ^
    /langversion:6 ^
    /reference:System.dll ^
    /reference:System.Windows.Forms.dll ^
    /reference:System.Drawing.dll ^
    /reference:System.Data.dll ^
    /reference:System.Net.Http.dll ^
    /reference:System.Core.dll ^
    /reference:System.Xml.dll ^
    /reference:"%NEWTON%" ^
    "%PROJ%\MyDll\MyClass.cs" ^
    "%PROJ%\MyDll\Mes.cs" ^
    "%PROJ%\MyDll\FailedImageArchiveService.cs" ^
    "%PROJ%\MyDll\Properties\AssemblyInfo.cs"

if %ERRORLEVEL% NEQ 0 (
    echo [失败] MyDll 编译失败，退出代码: %ERRORLEVEL%
    pause
    exit /b %ERRORLEVEL%
)
echo [成功] MyDll.dll 生成完毕

echo.
echo ============================================================
echo [2/2] 编译 MainTestMydll.exe ...
echo ============================================================

copy /Y "%NEWTON%" "%EXE_OUT%\Newtonsoft.Json.dll" >nul

"%CSC%" ^
    /target:exe ^
    /out:"%EXE_OUT%\MainTestMydll.exe" ^
    /langversion:6 ^
    /reference:System.dll ^
    /reference:System.Drawing.dll ^
    /reference:System.Core.dll ^
    /reference:"%DLL_OUT%\MyDll.dll" ^
    "%PROJ%\MainTestMydll\Program.cs"

if %ERRORLEVEL% NEQ 0 (
    echo [失败] MainTestMydll 编译失败，退出代码: %ERRORLEVEL%
    pause
    exit /b %ERRORLEVEL%
)
echo [成功] MainTestMydll.exe 生成完毕

echo.
echo ============================================================
echo 运行测试...
echo ============================================================
copy /Y "%DLL_OUT%\MyDll.dll" "%EXE_OUT%\MyDll.dll" >nul

"%EXE_OUT%\MainTestMydll.exe"

endlocal
