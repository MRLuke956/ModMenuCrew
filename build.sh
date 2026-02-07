#!/usr/bin/env bash
set -euo pipefail

TARGET="${1:-Build}"

dotnet tool restore
dotnet tool run dotnet-cake -- build.cake --target="$TARGET"

