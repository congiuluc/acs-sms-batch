# GitHub Actions Workflows

This repository includes three GitHub Actions workflows to automate building and releasing the BatchSMS application.

## 📋 Available Workflows

### 1. 🚀 **Release Workflow** (`release.yml`)

**Purpose**: Creates official releases with pre-built binaries for all platforms.

**Triggers**:
- **Automatic**: When you push a version tag (e.g., `v1.0.0`, `v2.1.3`)
- **Manual**: Through GitHub Actions tab with custom tag input

**What it does**:
- Builds the application for Windows, Linux, and macOS (Intel + Apple Silicon)
- Creates single-file executables for each platform
- Packages each platform with configuration files and documentation
- Creates a GitHub release with release notes
- Uploads platform-specific archives (ZIP for Windows, TAR.GZ for others)

**How to use**:

```bash
# Method 1: Create and push a tag
git tag v1.0.0
git push origin v1.0.0

# Method 2: Manual trigger from GitHub
# Go to Actions tab → Release → Run workflow → Enter tag name
```

### 2. 🔄 **CI Workflow** (`ci.yml`)

**Purpose**: Continuous integration - validates code quality on every change.

**Triggers**:
- Push to `main` or `develop` branches
- Pull requests to `main` or `develop` branches

**What it does**:
- Restores dependencies and builds the application
- Runs tests (if any exist)
- Verifies that Windows executable can be created
- Uploads build artifacts for debugging

### 3. 🛠️ **Manual Build Workflow** (`manual-build.yml`)

**Purpose**: On-demand builds for testing without creating releases.

**Triggers**:
- Manual execution from GitHub Actions tab

**What it does**:
- Allows you to choose which platform(s) to build
- Creates platform-specific builds
- Uploads build artifacts (accessible for 14 days)

**How to use**:
```
Go to Actions tab → Manual Build → Run workflow → Choose platform
```

## 🎯 Recommended Workflow

### For Development:
1. **Work on features** → CI workflow runs automatically
2. **Test builds** → Use Manual Build workflow as needed
3. **Ready for release** → Create version tag → Release workflow runs

### For Releases:
1. **Update version** in your code/documentation if needed
2. **Create and push a tag**:
   ```bash
   git tag v1.2.0
   git push origin v1.2.0
   ```
3. **Release workflow** creates the GitHub release automatically
4. **Download binaries** from the Releases page

## 📦 Release Artifacts

Each release includes:

| Platform | File | Target |
|----------|------|--------|
| Windows | `BatchSMS-win-x64.zip` | Windows 10/11 (64-bit) |
| Linux | `BatchSMS-linux-x64.tar.gz` | Linux distributions (64-bit) |
| macOS Intel | `BatchSMS-macos-x64.tar.gz` | Intel-based Macs |
| macOS Apple Silicon | `BatchSMS-macos-arm64.tar.gz` | M1/M2/M3 Macs |

Each package contains:
- Self-contained executable (no .NET runtime required)
- `appsettings.json` configuration file
- `README.md` documentation
- `LICENSE` file
- Sample CSV file (if available)
- Configuration examples

## 🔧 Customizing Workflows

### To modify build settings:
Edit the `env` section in any workflow file:

```yaml
env:
  DOTNET_VERSION: '8.0.x'          # .NET version
  PROJECT_PATH: 'src/BatchSMS.csproj'  # Path to your project file
```

### To add new platforms:
Add new publish steps in the release workflow:

```yaml
- name: Publish New Platform
  run: |
    dotnet publish ${{ env.PROJECT_PATH }} \
      --configuration Release \
      --runtime new-platform-rid \
      --self-contained true \
      --output ./publish/new-platform \
      -p:PublishSingleFile=true
```

### To customize release notes:
Modify the `Generate release notes` step in `release.yml`.

## 🚨 Troubleshooting

### Build Failures:
1. Check that `src/BatchSMS.csproj` path is correct
2. Verify all dependencies can be restored
3. Ensure .NET 8.0 compatibility

### Release Not Created:
1. Verify tag format: `v1.0.0` (must start with 'v')
2. Check repository permissions (needs `contents: write`)
3. Ensure `GITHUB_TOKEN` has sufficient permissions

### Missing Files in Release:
1. Update file paths in the workflow
2. Check that files exist in the repository
3. Verify copy commands in the workflow

## 📚 GitHub Actions Resources

- [GitHub Actions Documentation](https://docs.github.com/en/actions)
- [.NET Actions](https://github.com/actions/setup-dotnet)
- [Release Action](https://github.com/softprops/action-gh-release)

## 🎉 Quick Start

1. **Commit and push** these workflow files to your repository
2. **Create your first tag**:
   ```bash
   git tag v1.0.0
   git push origin v1.0.0
   ```
3. **Watch the magic** in the Actions tab!
4. **Download your releases** from the Releases page

Your BatchSMS application will now have automated builds and releases! 🚀
