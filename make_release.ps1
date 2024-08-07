$platforms = @{ name = 'win64';   profile = 'Windows64bit.pubxml'; runtime = 'win-x64'   },
             @{ name = 'win32';   profile = 'Windows32bit.pubxml'; runtime = 'win-x86'   },
             @{ name = 'linux64'; profile = 'Linux64bit.pubxml';   runtime = 'linux-x64' }

# Build all platform variants:
foreach ($platform in $platforms) {
    # NOTE: dotnet publish doesn't handle .pubxml files correctly, hence all the extra arguments here:
    dotnet publish .\MESS\MESS.csproj -c Release -f net6.0 -r $($platform.runtime) --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true --output .\MESS\bin\Release\net6.0\publish\$($platform.runtime)
}

# Create release zip files:
$version = (Get-Item .\MESS\bin\Release\net6.0\publish\win-x64\MESS.exe).VersionInfo.ProductVersion
foreach ($platform in $platforms) {
    # Create output directory:
    $output_dir = (Join-Path (Get-Location) "Releases\${version}\mess_${version}_$($platform.name)")
    Write-Host $output_dir
    [System.IO.Directory]::CreateDirectory($output_dir)

    # Copy files (executables, config files, template entities and template maps):
    Copy-Item -Path .\MESS\bin\Release\net6.0\publish\$($platform.runtime)\* -Destination $output_dir
    Copy-Item -Path .\files\* -Destination $output_dir
    Copy-Item -Path .\template_entities -Destination $output_dir\template_entities -Recurse -Container
    Copy-Item -Path .\template_maps -Destination $output_dir\template_maps -Recurse -Container
}

# Run MESS.exe to generate the mess.fgd file:
& ".\Releases\${version}\mess_${version}_win64\MESS.exe" -log off

foreach ($platform in $platforms) {
    $output_dir = (Join-Path (Get-Location) "Releases\${version}\mess_${version}_$($platform.name)")

    # Copy the mess.fgd file that was just generated by MESS:
    if ($platform.name -ne 'win64') {
        Copy-Item -Path .\Releases\${version}\mess_${version}_win64\mess.fgd -Destination $output_dir
    }

    # Zip it:
    Compress-Archive -Force -CompressionLevel Optimal -Path $output_dir -DestinationPath "${output_dir}.zip"
}