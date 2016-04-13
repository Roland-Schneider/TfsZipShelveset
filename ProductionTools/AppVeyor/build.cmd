@setlocal


@set

@echo Platform=%PLATFORM%
@echo Configuration=%CONFIGURATION%

@if "%APPVEYOR_BUILD_VERSION%" EQU "" set APPVEYOR_BUILD_VERSION=LocalBuild
@if "%PLATFORM%" EQU "" set PLATFORM=AnyCPU
@if "%CONFIGURATION%" EQU "" set CONFIGURATION=Debug

msbuild TfZip\TfZip.TfsLib12.0.csproj /t:Clean,Build /p:Configuration=%CONFIGURATION% /p:Platform=%PLATFORM% /p:"ReferencePath=c:\Program Files (x86)\Microsoft Visual Studio 12.0\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer"
if ERRORLEVEL 1 goto :EOF

:: Generating github release package
set ReleaseZip=TfZip-Executable.%APPVEYOR_BUILD_VERSION%.zip

@pushd TfZip\bin
7z a ..\..\%ReleaseZip% ^
TfsLib12\%CONFIGURATION%\TfZip.exe ^
TfsLib12\%CONFIGURATION%\TfZip.exe.config ^
TfsLib12\%CONFIGURATION%\TfZip.pdb
@popd

appveyor PushArtifact "%ReleaseZip%"

@endlocal
