# bit Fabric's installer for Windows, served at https://bitfabric.co/install.ps1.
#
# The line on bitfabric.co runs it, then bit new with your answers, in PowerShell or Command Prompt alike:
#     powershell -c "irm https://bitfabric.co/install.ps1 | iex; bit new MyProduct ..."
#
# What it does, for your Windows user only and without administrator rights:
#   1. Installs Microsoft's .NET SDK {{SdkChannel}}, the free developer tools your app is built with, unless it is there already.
#   2. Installs bit CLI, or updates it, as a .NET tool.
#   3. Adds both to your PATH, so bit new right after it and every window you open later find them.
# It changes nothing else on this computer. bit new then sets up the rest and says what it does as it goes.
#
# It runs in Windows PowerShell 5.1 as well as PowerShell 7, so it uses neither ?? nor other 7-only syntax.

& {
    $ErrorActionPreference = 'Stop'
    $ProgressPreference = 'SilentlyContinue'

    $sdkChannel = '{{SdkChannel}}'
    $bitCliVersion = '{{BitCliVersion}}'
    $dotnetRoot = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
    $toolsPath = Join-Path $env:USERPROFILE '.dotnet\tools'

    function Say([string] $text) {
        Write-Host 'bit Fabric: ' -ForegroundColor Cyan -NoNewline
        Write-Host $text
    }

    # In front of this window's PATH, and of the user's PATH for the windows opened later
    function Add-ToPath([string] $folder) {
        if (($env:PATH -split ';') -notcontains $folder) {
            $env:PATH = "$folder;$env:PATH"
        }

        $userPath = [Environment]::GetEnvironmentVariable('PATH', 'User')
        $parts = @()
        if ($userPath) { $parts = $userPath -split ';' }

        if ($parts -notcontains $folder) {
            [Environment]::SetEnvironmentVariable('PATH', ((@($folder) + $parts) | Where-Object { $_ }) -join ';', 'User')
        }
    }

    function Test-Sdk {
        if ($null -eq (Get-Command dotnet -ErrorAction SilentlyContinue)) {
            return $false
        }

        $prefix = '^' + [regex]::Escape($sdkChannel) + '\.'
        return [bool](& dotnet --list-sdks 2>$null | Where-Object { $_ -match $prefix })
    }

    try {
        # Old Windows PowerShell may still offer TLS 1.0 first, which Microsoft's download servers refuse
        [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

        if ((Test-Path (Join-Path $dotnetRoot 'dotnet.exe')) -and ((Test-Sdk) -eq $false)) {
            Add-ToPath $dotnetRoot
        }

        if (Test-Sdk) {
            Say "Microsoft's .NET SDK $sdkChannel is already on this computer."
        }
        else {
            Say "Installing Microsoft's .NET SDK $sdkChannel for your user. This is the biggest download, a few minutes on most connections..."

            # Run from memory, as this script is: Windows' default execution policy refuses a downloaded .ps1 file
            $installer = Invoke-WebRequest 'https://dot.net/v1/dotnet-install.ps1' -UseBasicParsing
            & ([scriptblock]::Create($installer.Content)) -Channel $sdkChannel -InstallDir $dotnetRoot -NoPath

            [Environment]::SetEnvironmentVariable('DOTNET_ROOT', $dotnetRoot, 'User')
            $env:DOTNET_ROOT = $dotnetRoot
            Add-ToPath $dotnetRoot
        }

        Add-ToPath $toolsPath

        # The newest bit CLI on nuget.org, a preview as well as a release, unless the server pins one: dotnet doesn't take both
        $toolArgs = @('--global', 'bit.cli')
        if ($bitCliVersion) { $toolArgs += @('--version', $bitCliVersion) } else { $toolArgs += '--prerelease' }

        $isInstalled = [bool](& dotnet tool list --global 2>$null | Where-Object { $_ -match '^bit\.cli\s' })

        if ($isInstalled) {
            Say 'Updating bit CLI...'
            # Its output goes nowhere, errors included, as in install.sh: a failure here only means the one installed stays. Windows
            # PowerShell 5.1 turns a redirected error line into an exception under Stop, so Continue holds for this call alone
            & { $ErrorActionPreference = 'Continue'; & dotnet tool update @toolArgs *> $null }

            # A newer bit CLI than this one, such as one built locally, stays: dotnet tool update doesn't go back a version
            if ($LASTEXITCODE -ne 0) {
                Say 'Keeping the bit CLI already on this computer.'
            }
        }
        else {
            Say 'Installing bit CLI...'
            & dotnet tool install @toolArgs | Out-Null

            if ($LASTEXITCODE -ne 0) {
                throw "bit CLI couldn't be installed (dotnet tool exited with $LASTEXITCODE)."
            }
        }

        Say 'Ready. Setting up your project now.'
    }
    catch {
        Write-Host ''
        Write-Host "bit Fabric: the setup stopped. $($_.Exception.Message)" -ForegroundColor Red
        Write-Host 'Nothing else on this computer was changed. Check your internet connection and paste the line again, or write to info@bitplatform.dev and we will help.'
        Write-Host ''
    }
}
