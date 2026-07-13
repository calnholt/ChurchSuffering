# Build Troubleshooting

Common `dotnet build` failures in this repo, especially around MonoGame content and `dotnet tool restore`.

## Symptom: `dotnet tool restore` failed

Typical errors:

```
error MSB3073: The command "dotnet tool restore" exited with code 1.
```

Underlying causes often look like one of these:

```
FatalProtocolException: Unable to load the service index for source https://api.nuget.org/v3/index.json
System.Net.Sockets.SocketException (11001): No such host is known. (api.nuget.org:443)
```

```
IOException: The process cannot access the file 'dotnet-mgcb.3.8.4.1.nupkg' because it is being used by another process.
```

### What is happening

On Windows, a normal `dotnet build` compiles MonoGame content through MGCB. The build needs the `dotnet-mgcb` local tool declared in `.config/dotnet-tools.json`.

`Directory.Build.targets` restores that tool before running MGCB. Restore can fail when:

1. **Network/DNS is down** and NuGet cannot reach `api.nuget.org`.
2. **A partial/corrupt MGCB package** is stuck in the NuGet cache, often after an interrupted restore or build.
3. **Another process is locking** the `.nupkg` file while restore tries to roll back.

The project also accepts a **globally installed** `mgcb` tool as a fallback, so restore failure is not always fatal.

## Quick fixes

### 1. Fastest path: skip content rebuild

Use this when you are not changing assets under `Content/`:

```bash
dotnet build /p:SkipMonoGameContentPipeline=true
```

Requires prebuilt content in `Content/bin/DesktopGL/Content/`. On macOS/Linux this is the default; on Windows it is opt-in.

### 2. Retry the build

If MGCB started rebuilding content but C# compilation began before all `.xnb` files were written, a second build often succeeds:

```bash
dotnet build
```

### 3. Use the global MGCB tool

If restore keeps failing but you already have MGCB installed globally:

```bash
dotnet tool install -g dotnet-mgcb
dotnet build
```

The build prefers `~/.dotnet/tools/mgcb` when present.

## Verify your environment

```bash
# DNS / NuGet reachability
nslookup api.nuget.org
ping api.nuget.org

# Local manifest tools
dotnet tool list

# Global MGCB tool
dotnet tool list -g
where mgcb        # Windows
which mgcb        # macOS/Linux

# MGCB runs
mgcb /?
```

Expected local tools from `.config/dotnet-tools.json`:

- `dotnet-mgcb` 3.8.4.1
- `dotnet-mgcb-editor` 3.8.4.1
- platform-specific `mgcb-editor-*` tools

## Fix a corrupted MGCB NuGet cache

If restore fails with a locked or partial `dotnet-mgcb.3.8.4.1.nupkg`:

1. Close IDE terminals, other `dotnet` processes, and anything else that may hold the file.
2. Delete the partial package folder.

Windows:

```powershell
Remove-Item -Recurse -Force "$env:USERPROFILE\.nuget\packages\dotnet-mgcb\3.8.4.1"
```

macOS/Linux:

```bash
rm -rf ~/.nuget/packages/dotnet-mgcb/3.8.4.1
```

3. Restore tools once network is healthy:

```bash
dotnet tool restore
```

4. Build again:

```bash
dotnet build
```

## How the repo handles this

`Directory.Build.targets` customizes MonoGame's content build target:

- Skips `dotnet tool restore` when either:
  - `.config/.store` already exists (local tools restored), or
  - a global `mgcb` executable exists.
- Uses the global `mgcb` path when available.
- Runs `dotnet tool restore` with `ContinueOnError="true"` only when neither fallback exists.

So a transient NuGet outage should not block builds if MGCB is already installed globally.

## Other build notes

### Missing prebuilt content on macOS/Linux

```
SkipMonoGameContentPipeline is set but Content/bin/DesktopGL/Content is missing.
```

Download the `monogame-content` CI artifact, or run a full Windows content build once and copy `Content/bin/DesktopGL/Content`.

### Rebuilding content after asset changes

Windows default:

```bash
dotnet build
```

Force content pipeline on macOS/Linux:

```bash
dotnet build /p:SkipMonoGameContentPipeline=false
```

See `AGENTS.md` for MGFXC Wine and MGCB ffmpeg setup on non-Windows platforms.

### Still stuck

1. Confirm `dotnet build /p:SkipMonoGameContentPipeline=true` works.
2. Confirm network access to `https://api.nuget.org/v3/index.json`.
3. Clear the partial `dotnet-mgcb/3.8.4.1` cache folder and rerun `dotnet tool restore`.
4. Install global MGCB: `dotnet tool install -g dotnet-mgcb`.
