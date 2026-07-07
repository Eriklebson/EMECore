$culture = [System.Globalization.CultureInfo]::new("en-US")
[System.Threading.Thread]::CurrentThread.CurrentCulture = $culture
[System.Threading.Thread]::CurrentThread.CurrentUICulture = $culture
[System.Globalization.CultureInfo]::DefaultThreadCurrentCulture = $culture
[System.Globalization.CultureInfo]::DefaultThreadCurrentUICulture = $culture

& dotnet.exe build @args
