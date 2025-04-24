@echo off
setlocal

set M9=C:\Program Files\Manifold\v9.0\shared

set ADDINNAME=http-for-manifold-9


if exist "%M9%\%ADDINNAME%\" GOTO ALREADYINSTALLED
GOTO DOINST


:DOINST
echo ------- Creating directory %ADDINNAME% 
echo ------- under %M9%\ 
mkdir "%M9%\%ADDINNAME%"
if exist "%M9%\%ADDINNAME%\" GOTO COPYFILES 
GOTO CANNOTCREATEDIR

:COPYFILES
echo ------- Copying add-in files

copy AWSSDK.Core.dll "%M9%\%ADDINNAME%\"
copy AWSSDK.S3.dll   "%M9%\%ADDINNAME%\"
copy %ADDINNAME%.dll "%M9%\%ADDINNAME%\"
copy %ADDINNAME%.dll.addin "%M9%\%ADDINNAME%\"
copy %ADDINNAME%.uninstall.bat "%M9%\%ADDINNAME%\"
copy %ADDINNAME%.readme.txt "%M9%\%ADDINNAME%\"
copy %ADDINNAME%.sql "%M9%\%ADDINNAME%\"
copy %ADDINNAME%-examples.sql "%M9%\%ADDINNAME%\"

goto END

:CANNOTCREATEDIR
echo Error: Cannot create Addin directory.
echo You must have write permission for %M9%
goto END

:ALREADYINSTALLED
echo Error: Cannot install
echo %ADDINNAME% directory already exists
echo Try running %M9%\%ADDINNAME%\%ADDINNAME%.uninstall.bat first
GOTO END

:END
endlocal
pause
