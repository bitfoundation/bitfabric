#!/usr/bin/env bash
# bit Fabric's installer for macOS and Linux, served at https://bitfabric.co/install.sh.
#
# The line on bitfabric.co runs it with your answers, which it hands over to bit:
#     curl -fsSL https://bitfabric.co/install.sh | bash -s -- new MyProduct ...
#
# What it does, for your user only and without sudo:
#   1. Installs Microsoft's .NET SDK {{SdkChannel}}, the free developer tools your app is built with, unless it is there already.
#   2. Installs bit CLI, or updates it, as a .NET tool.
#   3. Adds both to your PATH, in this terminal and in your shell's profile for the ones opened later.
#   4. Runs bit with the arguments after --, which sets up the rest and says what it does as it goes.
# It changes nothing else on this computer.
#
# Everything runs inside main, which bash reads whole before it starts: the script arrives through a pipe, and a command
# reading from it halfway would otherwise eat the rest of the script.

main() {
    set -euo pipefail

    local sdk_channel='{{SdkChannel}}'
    local bit_cli_version='{{BitCliVersion}}'
    local dotnet_root="$HOME/.dotnet"
    local tools_path="$HOME/.dotnet/tools"

    say() { printf '\033[36mbit Fabric:\033[0m %s\n' "$1"; }

    fail() {
        printf '\n\033[31mbit Fabric: the setup stopped. %s\033[0m\n' "$1" >&2
        printf 'Nothing else on this computer was changed. Check your internet connection and paste the line again, or write to info@bitplatform.dev and we will help.\n\n' >&2
        exit 1
    }

    has_sdk() {
        command -v dotnet >/dev/null 2>&1 && dotnet --list-sdks 2>/dev/null | grep -q "^${sdk_channel//./\\.}\."
    }

    # The lines that put .NET and bit CLI on the PATH of every terminal opened later, added once to each profile
    remember_path() {
        local profile marker='# bit Fabric: .NET and bit CLI for this user'
        local profiles=("$HOME/.profile")

        case "${SHELL:-}" in
            */zsh) profiles+=("$HOME/.zshrc") ;;
            */bash) profiles+=("$HOME/.bashrc") ;;
        esac

        if [ "$(uname -s)" = "Darwin" ]; then
            profiles+=("$HOME/.zprofile")
        fi

        for profile in "${profiles[@]}"; do
            if [ -f "$profile" ] && grep -qF "$marker" "$profile"; then
                continue
            fi

            printf '\n%s\nexport DOTNET_ROOT="$HOME/.dotnet"\nexport PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"\n' "$marker" >> "$profile"
        done
    }

    command -v curl >/dev/null 2>&1 || fail "curl is needed to download the developer tools."

    if [ -x "$dotnet_root/dotnet" ]; then
        export DOTNET_ROOT="$dotnet_root"
        export PATH="$dotnet_root:$PATH"
    fi

    if has_sdk; then
        say "Microsoft's .NET SDK $sdk_channel is already on this computer."
    else
        say "Installing Microsoft's .NET SDK $sdk_channel for your user. This is the biggest download, a few minutes on most connections..."

        local installer
        installer="$(mktemp)"
        curl -fsSL https://dot.net/v1/dotnet-install.sh -o "$installer" || fail "Microsoft's download server couldn't be reached."
        bash "$installer" --channel "$sdk_channel" --install-dir "$dotnet_root" --no-path >/dev/null || fail "Microsoft's .NET SDK couldn't be installed."
        rm -f "$installer"

        export DOTNET_ROOT="$dotnet_root"
        export PATH="$dotnet_root:$PATH"
    fi

    export PATH="$tools_path:$PATH"
    remember_path

    # The newest bit CLI on nuget.org, a preview as well as a release, unless the server pins one: dotnet doesn't take both
    local tool_args=(--global bit.cli)
    if [ -n "$bit_cli_version" ]; then
        tool_args+=(--version "$bit_cli_version")
    else
        tool_args+=(--prerelease)
    fi

    if dotnet tool list --global 2>/dev/null | grep -qi '^bit\.cli '; then
        say "Updating bit CLI..."
        # A newer bit CLI than this one, such as one built locally, stays: dotnet tool update doesn't go back a version
        dotnet tool update "${tool_args[@]}" >/dev/null 2>&1 || say "Keeping the bit CLI already on this computer."
    else
        say "Installing bit CLI..."
        dotnet tool install "${tool_args[@]}" >/dev/null || fail "bit CLI couldn't be installed."
    fi

    if [ "$#" -eq 0 ]; then
        say "Ready. Open a new terminal, or run: export PATH=\"\$HOME/.dotnet:\$HOME/.dotnet/tools:\$PATH\""
        return
    fi

    say "Ready. Setting up your project now."

    # bit may ask a question or open the browser to sign in, so it gets the terminal rather than the pipe this script came through
    if { : < /dev/tty; } 2>/dev/null; then
        exec bit "$@" < /dev/tty
    else
        exec bit "$@"
    fi
}

main "$@"
