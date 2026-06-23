# txt2loc

Tool for converting a single text file created by [TXT2GAM](https://github.com/QSPFoundation/txt2gam) into individual location files and converting them back.

## Requirements

[.NET 7.0 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/7.0)

## How to use

```
Usage:
  txt2loc [options] <input> <output>
Options:
  -h          Show this help
  -d          Decompose single <input> text file to separate location files
              and put them into <output> directory (default);
              Also will create in <output> directory (or copy from .qproj file
              if used '-p') 'locations.xml' file storing list of locations;
              WARNING: <output> directory will be DELETED and recreated without
              any promt for confirmation!
  -c          Compose separate location files from <input> directory into
              single <output> text file;
              Also will update 'locations.xml' file in <input> directory
              (and copy it into .qproj file if used '-p')
  -p <value>  .qproj file
  -s <value>  'Start of loc' prefix (default: '#')
  -e <value>  'End of loc' prefix (default: '--')
Examples:
  txt2loc -d -p game.qproj game.txt locations
  txt2loc -c -p "my game.qproj" locations "my game.txt"
```

## Example of Windows batch for exporting

This batch will convert qsp game file to single txt file, than decompose it to separate location files and test that locations could be composed back to single txt file.<br>
Edit paths to .qsp and .qproj files and to directory with all exported locations.
```
@echo off
:: EDIT THIS
set qsp_file=".\game\game.qsp"
set qsp_proj=".\game\game.qproj"
set export_dir=".\repo\locations"

:: edit if have problems
set export_file=".\game.txt"
set test_reimport_file=".\game (test reimport).txt"
set log_file=".\export_log.txt"
set loc_start="#<<<<<<< START OF LOCATION:"
set loc_end="#>>>>>>> END OF LOCATION:"

echo Exporting %qsp_file% to %export_dir%...
call :main > %log_file% 2>&1 || (type %log_file% & pause)
exit

:main
if not exist %qsp_file% (echo %qsp_file% not found! & exit /b 1)
if not exist %qsp_proj% (echo %qsp_proj% not found! & exit /b 1)
@echo on
:: export qsp -> txt
txt2gam -d -s %loc_start% -e %loc_end% %qsp_file% %export_file% || exit /b 1
:: export txt -> locations
txt2loc -d -s %loc_start% -e %loc_end% -p %qsp_proj% %export_file% %export_dir% || exit /b 1
:: reimport locations -> txt for binary comparison
txt2loc -c -s %loc_start% -e %loc_end% -p %qsp_proj% %export_dir% %test_reimport_file% || exit /b 1
:: binary comparison
fc /b %export_file% %test_reimport_file% >nul && (echo They match) || (echo Comparison failed! & exit /b 1)

del %test_reimport_file%
::del %export_file%

exit /b
```

## Example of Windows batch for importing

TODO
