set

set GIT_VERSION=
for /f %%i in ('git tag') do set GIT_VERSION=%%i
@echo Current GitVersion=%GIT_VERSION%
if %APPVEYOR_REPO_TAG%==true set GIT_VERSION=%APPVEYOR_REPO_TAG_NAME%

set V1=
set V2=
set V3=
for /f "tokens=1,2,3 delims=." %%i in ('echo %GIT_VERSION%') do set V1=%%i&set V2=%%j&set V3=%%k
if "%V1%"=="" set V1=v0
if "%V2%"=="" set V2=0
if "%V3%"=="" set V3=0

set APPVEYOR_BUILD_VERSION=%V1:~1%.%V2%.%V3%.%APPVEYOR_BUILD_NUMBER%

@echo Updated APPVEYOR_BUILD_VERSION=%APPVEYOR_BUILD_VERSION%
