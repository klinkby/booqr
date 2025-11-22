# Security Policy

## Supply Chain Security

This project implements multiple layers of defense to protect against supply chain injection attacks:

### 1. 7-Day Dependency Cooldown Period

We have configured Dependabot with a **7-day minimum release age** for all NuGet packages (`.github/dependabot.yml`). This means:

- Dependabot will only create pull requests for packages that have been published for at least 7 days
- This cooldown period blocks approximately **80% of supply chain attacks** by:
  - Allowing time for the security community to detect malicious packages
  - Giving security scanners time to identify vulnerabilities
  - Preventing immediate adoption of compromised packages

### 2. Package Source Mapping

The `nuget.config` file explicitly maps all packages to `nuget.org`, preventing:

- **Dependency confusion attacks** where malicious packages from unexpected sources could be installed
- Accidental use of packages from untrusted or unknown sources

### 3. Package Signature Validation

All NuGet packages must be signed and trusted:

- `signatureValidationMode` is set to `require`
- Only packages signed by trusted sources (nuget.org repository certificate) are accepted
- This prevents installation of tampered or malicious unsigned packages

### 4. Trusted Signers

The `nuget.config` explicitly lists trusted certificate fingerprints for:

- NuGet.org repository (certificate fingerprint: `0E5F38F57DC1BCC806D8494F4F90FBCEDD988B46760709CBEEC6F4219AA6157D`)

### 5. Reproducible Builds

- `RestorePackagesWithLockFile` is enabled in `Directory.Build.props`
- Ensures consistent package versions across all builds
- Makes supply chain attacks easier to detect through unexpected lock file changes

## Reporting Security Issues

If you discover a security vulnerability, please email the maintainer directly rather than opening a public issue.

## Security Best Practices for Contributors

When contributing to this project:

1. **Never disable security settings** in `nuget.config` or `.github/dependabot.yml`
2. **Review dependency updates carefully** - even with the cooldown period, inspect changes
3. **Check lock files** - unexpected changes to `packages.lock.json` may indicate issues
4. **Run vulnerability scans** - use `dotnet list package --vulnerable` before submitting PRs
5. **Keep dependencies minimal** - fewer dependencies = smaller attack surface

## References

- [Dependency Cooldowns Block 80% of Supply Chain Attacks](https://byteiota.com/dependency-cooldowns-supply-chain-security/)
- [Microsoft: NuGet Security Best Practices](https://learn.microsoft.com/en-us/nuget/concepts/security-best-practices)
- [Package Source Mapping](https://learn.microsoft.com/en-us/nuget/consume-packages/package-source-mapping)
- [Trusted Signers](https://learn.microsoft.com/en-us/nuget/consume-packages/installing-signed-packages)
