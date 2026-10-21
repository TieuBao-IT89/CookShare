# Dependency Security

## 1. Automated dependency updates

Dependabot checks NuGet packages and GitHub Actions weekly.
It may open pull requests when updates are available.

## 2. Reviewing Dependabot pull requests

Before merging a dependency update:

1. Review the package name and version changes.
2. Read the release notes and security advisory, if available.
3. Check whether the update introduces breaking changes.
4. Run the CI build, tests, and code-quality checks.
5. Merge only after the required checks pass and the change is reviewed.

Do not automatically merge dependency updates without testing.

## 3. Handling security alerts

When a Dependabot alert appears:

1. Open the alert in the repository's Security tab.
2. Identify the affected package and vulnerable version.
3. Review the recommended patched version and advisory.
4. Update the dependency or review the proposed Dependabot pull request.
5. Run tests and verify that the application still works.
6. Merge the fix after review and confirm that the alert is resolved.

If no safe update is available, document the issue and assess mitigation options.

## 4. CI validation

Dependency updates must pass the repository's applicable build,
test, formatting, style, and analyzer checks before merging.

## 5. Repository settings

A repository maintainer should verify that Dependabot Alerts
and other supported security features are enabled in GitHub settings.
