@echo off
rem Builds the Windows 7-compatible Release DLL.
rem Requires the MSVC 14.38 (v143) toolset component in the VS installer --
rem newer toolsets produce a DLL that does not load on Windows 7.
setlocal
set BUILD_DIR=%~dp0out\build\x86-Release

rem If the cache was configured with a different toolset (e.g. by Visual
rem Studio), wipe it so CMake re-detects the 14.38 compiler.
if exist "%BUILD_DIR%\CMakeCache.txt" (
    findstr /C:"MSVC/14.38" "%BUILD_DIR%\CMakeCache.txt" >nul 2>&1
    if errorlevel 1 (
        echo Cache was configured with a different toolset - wiping %BUILD_DIR%
        rmdir /s /q "%BUILD_DIR%"
    )
)

call "C:\Program Files\Microsoft Visual Studio\18\Community\VC\Auxiliary\Build\vcvars32.bat" -vcvars_ver=14.38
cmake -S "%~dp0." -B "%BUILD_DIR%" -G Ninja -DCMAKE_BUILD_TYPE=RelWithDebInfo
if errorlevel 1 exit /b 1
cmake --build "%BUILD_DIR%"
if errorlevel 1 exit /b 1
echo.
echo Output: %BUILD_DIR%\DewSROToolkit.dll
