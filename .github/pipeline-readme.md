# GitHub Actions CI/CD Setup

This repository uses GitHub Actions for automated building, testing, and publishing of the RevitXunit.TestAdapter NuGet package for multiple Revit versions.

## Workflow Overview

The workflow is triggered on:
- **Pull Requests** to `main` branch: Builds, tests, and publishes to GitHub Package Registry for each Revit version
- **Pushes** to `main` branch: Builds, tests, and publishes to GitHub Package Registry for each Revit version
- **Releases**: Builds, tests, and publishes to both GitHub Package Registry and NuGet.org for each Revit version

## Multi-Version Support

The workflows automatically build and publish separate NuGet packages for:
- **Revit 2025**: `RevitXunit.TestAdapter` version `2025.x.y`
- **Revit 2026**: `RevitXunit.TestAdapter` version `2026.x.y`

Each package is specifically compiled for its target Revit version with appropriate dependencies and configurations.

## Workflow Jobs

### 1. Build Job (Matrix Strategy)
- Runs in parallel for each Revit version (2025, 2026)
- Restores NuGet packages with version-specific dependencies
- Builds the entire solution with `/p:RevitVersion=<version>`
- Runs framework tests (excluding Revit integration tests)
- Packs version-specific NuGet packages
- Uses GitVersion for automatic semantic versioning with Revit-specific format

### 2. Publish to GitHub Job (PRs and main branch)
- Publishes version-specific NuGet packages to GitHub Package Registry
- Allows testing of packages before public release
- Runs in parallel for each Revit version

### 3. Publish to NuGet.org Job (Releases only)
- Publishes version-specific NuGet packages to the public NuGet.org feed
- Only triggered when creating a GitHub release
- Runs in parallel for each Revit version

## Test Execution Strategy

### Framework Validation (Included in CI/CD)
- **Build Validation**: Ensures all projects compile correctly for each Revit version
- **Package Creation**: Verifies NuGet packages can be created successfully
- **Dependency Resolution**: Confirms all NuGet package references resolve correctly
- **Multi-Version Support**: Validates that the framework builds for all supported Revit versions

### Revit Integration Tests (Excluded from CI/CD)
- Located in `MyRevitTestsXunit` project
- Uses `[RevitFact]` attributes and requires Autodesk Revit installation
- **Excluded from CI/CD** because GitHub Actions runners don't have Revit installed
- Should be run locally on developer machines with Revit installed

### Why Exclude Revit Tests from CI/CD?
1. **No Revit Installation**: GitHub Actions runners don't have Autodesk Revit installed
2. **Licensing Requirements**: Revit requires specific licensing that's not available in CI/CD environments
3. **GUI Dependencies**: Revit requires a desktop environment with graphics capabilities
4. **Resource Intensive**: Revit tests are slower and resource-intensive
5. **Local Development Focus**: These tests are designed for local development and manual validation

### CI/CD Focus
The CI/CD pipeline focuses on:
- ? **Framework Integrity**: Ensuring the test adapter builds correctly
- ? **Multi-Version Support**: Validating compatibility with multiple Revit versions
- ? **Package Quality**: Creating proper NuGet packages for distribution
- ? **Dependency Management**: Ensuring all references resolve correctly
- ? **Build Consistency**: Confirming reproducible builds across environments

## Required Secrets

You need to configure the following secrets in your GitHub repository settings:

### For GitHub Package Registry
- `GH_TOKEN`: Personal Access Token with package permissions
  1. Go to GitHub Settings ? Developer settings ? Personal access tokens ? Tokens (classic)
  2. Generate a new token with the following scopes:
     - `write:packages` - Allows uploading packages to GitHub Package Registry
     - `read:packages` - Allows downloading packages from GitHub Package Registry
     - `repo` - Required for accessing private repositories
  3. Add this token as a repository secret named `GH_TOKEN`
  
  **Note**: The default `GITHUB_TOKEN` has limited permissions and may not work for package publishing in all scenarios. Using a custom Personal Access Token ensures proper authentication.

### For NuGet.org Publishing
- `NUGET_API_KEY`: Your NuGet.org API key
  1. Go to [NuGet.org](https://www.nuget.org/)
  2. Sign in and go to Account Settings
  3. Create a new API key with push permissions for your package
  4. Add this key as a repository secret

### Authentication Setup

The workflows automatically configure NuGet authentication using your `GH_TOKEN`:

```yaml
- name: Set up NuGet authentication for GitHub Packages
  run: |
    dotnet nuget add source "https://nuget.pkg.github.com/${{ github.repository_owner }}/index.json" `
      --name github `
      --username ${{ github.actor }} `
      --password ${{ secrets.GH_TOKEN }} `
      --store-password-in-clear-text
```

This ensures proper authentication when pushing packages to your GitHub Package Registry.

## Versioning Strategy

The workflow uses GitVersion for automatic semantic versioning with Revit-specific formatting:

### Base Version Format
GitVersion generates a base semantic version (e.g., `1.2.3-alpha.1`)

### Revit-Specific Version Format
Each Revit version gets a package with format: `<RevitVersion>.<Minor>.<Patch>[<Prerelease>]`

Examples:
- Base version: `1.2.3` ? Revit 2025: `2025.2.3`, Revit 2026: `2026.2.3`
- Base version: `1.2.3-alpha.1` ? Revit 2025: `2025.2.3-alpha.1`, Revit 2026: `2026.2.3-alpha.1`

### Branch-Based Versioning
- **Main branch**: Production releases (e.g., 2025.0.1, 2026.0.1)
- **Feature branches**: Alpha versions (e.g., 2025.1.0-alpha.1, 2026.1.0-alpha.1)
- **Pull requests**: PR versions (e.g., 2025.1.0-PullRequest.1, 2026.1.0-PullRequest.1)
- **Hotfix branches**: Beta versions (e.g., 2025.0.1-beta.1, 2026.0.1-beta.1)

### Version Bumping

You can control version increments using commit messages:
- `+semver: major` or `+semver: breaking` - Major version bump
- `+semver: minor` or `+semver: feature` - Minor version bump  
- `+semver: patch` or `+semver: fix` - Patch version bump
- `+semver: none` or `+semver: skip` - No version bump

## Usage

### For Pull Requests
1. Create a feature branch
2. Make your changes
3. Open a PR to `main`
4. The workflow will build and publish test packages for both Revit versions to GitHub Package Registry

### For Releases
1. Merge your PR to `main`
2. Create a new GitHub release with a version tag (e.g., `v1.0.0`)
3. The workflow will build and publish packages for both Revit versions to both GitHub Package Registry and NuGet.org

### Local Testing with Revit
To run the full test suite including Revit integration tests:

```bash
# Run all tests locally (requires Revit installation)
dotnet test RevitTestRunner.sln --configuration Release

# Run only integration tests
dotnet test MyRevitTestsXunit/MyRevitTestsXunit.csproj --configuration Release

# Or use the provided PowerShell script with more options:
.\run-local-tests.ps1                    # Run all tests for all Revit versions
.\run-local-tests.ps1 -FrameworkOnly     # Test framework without Revit
.\run-local-tests.ps1 -IntegrationOnly   # Test only Revit integration
.\run-local-tests.ps1 -RevitVersions @("2025")  # Test specific version
```

**Prerequisites for Integration Tests:**
- Autodesk Revit 2025+ installed locally
- Valid Revit license
- Desktop environment (not headless)
- .NET 8.0 SDK

## Package Consumption

### From GitHub Package Registry (for testing)
```xml
<PackageSource>
  <add key="github" value="https://nuget.pkg.github.com/YOUR_USERNAME/index.json" />
</PackageSource>
```

### From NuGet.org (for production)
```xml
<!-- For Revit 2025 -->
<PackageReference Include="RevitXunit.TestAdapter" Version="2025.*" />

<!-- For Revit 2026 -->
<PackageReference Include="RevitXunit.TestAdapter" Version="2026.*" />
```

## Adding Support for New Revit Versions

To add support for a new Revit version (e.g., 2027):

1. **Update workflows**: Add the new version to the matrix strategy in both workflow files:
   ```yaml
   strategy:
     matrix:
       revit-version: [2025, 2026, 2027]  # Add 2027
   ```

2. **Update Directory.Build.props**: Ensure the new version is supported in your build properties

3. **Test locally**: Verify the solution builds with `/p:RevitVersion=2027`

## Troubleshooting

### Build Failures
- Check that all project references are correct for each Revit version
- Ensure .NET 8 SDK is compatible with your code
- Verify that test projects can run locally with different Revit versions
- Check for version-specific dependencies that may conflict

### Publishing Failures
- Verify that the `NUGET_API_KEY` secret is correctly configured
- Check that the package ID doesn't conflict with existing packages
- Ensure you have permissions to publish to the target feed
- Verify all Revit-specific packages are being generated correctly
- **Package Path Issues**: Ensure artifact upload/download paths are consistent
- **Missing Packages**: Check that the build job successfully creates .nupkg files
- **Artifact Structure**: Verify that artifacts contain the expected files in the correct paths

### Build Issues
- **Package Creation Failures**: Check the build output for NuGet pack errors
- **Missing Dependencies**: Ensure all project references build successfully
- **Version Conflicts**: Verify that GitVersion generates valid semantic versions
- **Output Directory**: Confirm packages are created in the expected output directory

### Artifact Issues
- **Upload/Download Mismatch**: Ensure artifact paths match between jobs
- **Missing Files**: Check that the build job produces the expected artifacts
- **Path Structure**: Verify directory structure in uploaded artifacts
- **File Permissions**: Ensure artifacts are accessible and not corrupted- Verify all artifacts are being uploaded with correct names
