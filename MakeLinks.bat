@ECHO OFF

ECHO.
ECHO ===============================================================================
ECHO =                                 Make Links                                  =
ECHO ===============================================================================
ECHO.
ECHO This batch file creates symbolic links for the HoloToolkit and other Unity 
ECHO source-based libraries that are used by this project. The process for sharing 
ECHO HoloToolkit across projects is documented here:
ECHO.
ECHO http://www.wikiholo.net/index.php?title=Sharing_HoloToolkit
ECHO.
ECHO The libraries used by this project are:
ECHO.
ECHO * HoloToolkit for Unity
ECHO * Microsoft Adept
ECHO.
ECHO All libraries should be downloaded and extracted before running this batch file. 
ECHO If you continue you will be prompted for the full path of each of the above 
ECHO libraries. 
ECHO.
ECHO Are you ready to continue?
ECHO.
CHOICE /C:YN
IF ERRORLEVEL == 2 GOTO End


:HoloToolkit

SET /p HoloKitSource=HoloToolkit-Unity Path? 
IF NOT EXIST "%HoloKitSource%\Assets\csc.rsp" (
ECHO.
ECHO HoloToolkit for Unity not found at %HoloKitSource%
ECHO.
GOTO HoloToolkit
)
ECHO HoloToolkit for Unity FOUND
ECHO.


:Adept

SET /p AdeptSource=Adept Path? 
IF NOT EXIST "%AdeptSource%\Adept.sln" (
ECHO.
ECHO Adept not found at %AdeptSource%
ECHO.
GOTO Adept
)
ECHO Adept FOUND
ECHO.


ECHO.
ECHO ===============================================================================
ECHO =                         Copying HoloToolkit RSPs                            =
ECHO ===============================================================================
ECHO.
XCOPY /Y /Q %HoloKitSource%\Assets\*.rsp FitBoxIPD\Unity\FitBoxIPD\Assets
ECHO.

ECHO.
ECHO ===============================================================================
ECHO =                            Linking HoloToolkit                              =
ECHO ===============================================================================
ECHO.
mklink /J FitBoxIPD\Unity\FitBoxIPD\Assets\HoloToolkit %HoloKitSource%\Assets\HoloToolkit
ECHO.

ECHO.
ECHO ===============================================================================
ECHO =                               Linking Adept                                 =
ECHO ===============================================================================
ECHO.
mklink /J FitBoxIPD\Unity\FitBoxIPD\Assets\Adept %AdeptSource%\Lib\Unity\Assets\Adept
ECHO.

PAUSE

:End

