# Release Steps

If you want to make a new release of PrettyPrompt:

- Pull the latest `main` branch.
- Increment the version in PrettyPrompt.csproj
- Update the release notes
- Make a pull request against `main`

The GitHub action will handle the rest -- it will read the version from the csproj, publish and build to nuget, and create a git tag automatically.
