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

### Framework Tests (Included in CI/CD)
- Tests the test adapter framework itself
- Unit tests for discovery, execution, and communication logic
- Does not require Revit installation

### Revit Integration Tests (Excluded from CI/CD)
- Located in `MyRevitTestsXunit` project
- Uses `[RevitFact]` attributes and requires Autodesk Revit installation
- **Excluded from CI/CD** using `--filter "FullyQualifiedName!~MyRevitTestsXunit"`
- Should be run locally on developer machines with Revit installed

### Why Exclude Revit Tests?
- GitHub Actions runners don't have Autodesk Revit installed
- Revit requires a GUI environment and specific licensing
- These tests are designed for local development and manual validation

## Required Secrets

You need to configure the following secrets in your GitHub repository settings:

### For NuGet.org Publishing
- `NUGET_API_KEY`: Your NuGet.org API key
  1. Go to [NuGet.org](https://www.nuget.org/)
  2. Sign in and go to Account Settings
  3. Create a new API key with push permissions for your package
  4. Add this key as a repository secret

### For GitHub Package Registry
- `GITHUB_TOKEN`: Automatically provided by GitHub Actions (no setup required)

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

# Run only framework tests (no Revit required)
dotnet test RevitTestRunner.sln --configuration Release --filter "FullyQualifiedName!~MyRevitTestsXunit"
```

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

### Test Execution Issues
- **Framework tests failing**: Check unit test logic and dependencies
- **Want to run Revit tests**: These must be run locally with Revit installed
- **Filter not working**: Verify the filter syntax matches your project structure

### Version Issues
- Review the GitVersion configuration in `GitVersion.yml`
- Check commit messages for unintended version bumps
- Verify that tags follow the expected format (e.g., `v1.0.0`)
- Ensure the Revit-specific version calculation logic is working correctly

### Matrix Job Issues
- If one Revit version fails, others will continue building
- Check job logs for version-specific errors
- Verify all artifacts are being uploaded with correct names