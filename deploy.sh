#!/usr/bin/env bash
set -euo pipefail

if [[ $# -gt 2 ]]; then
    printf 'Usage: %s [destination] [Debug|Release]\n' "$0" >&2
    exit 2
fi

destination=${1:-/mnt/homeassistant/nd5}
configuration=${2:-Release}
case "$configuration" in
    Debug|Release) ;;
    *)
        printf 'Invalid build configuration: %s\n' "$configuration" >&2
        exit 2
        ;;
esac

if [[ "$destination" == /mnt/homeassistant/* ]] && ! mountpoint -q -- /mnt/homeassistant; then
    printf 'Mount the Home Assistant share at /mnt/homeassistant before deploying.\n' >&2
    exit 1
fi

project_root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
solution="$project_root/NetDaemonApps.slnx"
project="$project_root/src/NetDaemonApps/NetDaemonApps.csproj"
publish_directory="$project_root/artifacts/publish"

(
    cd -- "$project_root"
    dotnet restore "$solution" --locked-mode -m:1
    dotnet test "$solution" --configuration "$configuration" --no-restore
    dotnet publish "$project" --configuration "$configuration" --no-restore --output "$publish_directory"
)

mkdir -p -- "$destination"
cp -R -- "$publish_directory"/. "$destination"/
printf 'Published NetDaemon to %s\n' "$destination"
