@echo off
mkdir "C:\Program Files (x86)\Windows Kits\10\Platforms\UAP\10.0.22621.0" 2>nul
copy /Y "C:\Program Files (x86)\Windows Kits\10\Platforms\UAP\10.0.26100.0\Platform.xml" "C:\Program Files (x86)\Windows Kits\10\Platforms\UAP\10.0.22621.0\Platform.xml"
echo Done
