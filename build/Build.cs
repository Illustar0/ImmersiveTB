using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

/// <summary>Builds and packages ImmersiveTB for Windows.</summary>
[SuppressMessage("Design", "CA1050",
    Justification = "NUKE convention keeps its build entry point in the global namespace.")]
[SuppressMessage("Design", "MA0047",
    Justification = "NUKE convention keeps its build entry point in the global namespace.")]
[SuppressMessage("Major Code Smell", "S3903",
    Justification = "NUKE convention keeps its build entry point in the global namespace.")]
[SuppressMessage("Design", "MA0049", Justification = "The NUKE build entry point is intentionally named Build.")]
public sealed class Build : NukeBuild
{
    private const string ArtifactsDirectoryName = "artifacts";
    private const string PackageName = "Illustar0.ImmersiveTB";
    private static readonly string[] Architectures = ["x64", "arm64"];
    private static AbsolutePath Project => RootDirectory / "ImmersiveTB" / "ImmersiveTB.csproj";
    private static AbsolutePath Manifest => RootDirectory / "ImmersiveTB" / "Package.appxmanifest";
    private static AbsolutePath Packages => RootDirectory / ArtifactsDirectoryName / "packages";
    private static AbsolutePath Symbols => RootDirectory / ArtifactsDirectoryName / "symbols";
    private static AbsolutePath Portable => RootDirectory / ArtifactsDirectoryName / "portable";

    [Parameter("Build configuration")] private readonly string Configuration = IsLocalBuild ? "Debug" : "Release";

    [Parameter("Major.Minor.Patch version; defaults to the package manifest version")]
    private readonly string? SemVer;

    [Parameter("Fourth MSIX version component, from 0 to 65535")]
    private readonly int Revision;

    [Parameter] [Secret] private readonly string? SigningCertificate;
    [Parameter] [Secret] private readonly string? SigningCertificatePassword;

    [Parameter("Thumbprint of a signing certificate in the Windows Personal certificate store")]
    private readonly string? SigningCertificateThumbprint;

    [Parameter("Separate PDBs from release packages and create ZIP archives")]
    private readonly bool SeparatePdbs;

    /// <summary>Restores the x64 and arm64 applications and their project references.</summary>
    [SuppressMessage("Performance", "CA1822", Justification = "NUKE discovers instance target properties.")]
    [SuppressMessage("Major Code Smell", "S2325", Justification = "NUKE discovers instance target properties.")]
    private Target Restore => target => target.Executes(() =>
        DotNetRestore(s => s
            .SetProjectFile(Project)
            .CombineWith(Architectures, (settings, architecture) => settings
                .SetProperty("Platform", architecture))));

    /// <summary>Builds the x64 and arm64 applications without packaging them.</summary>
    private Target Compile => target => target
        .DependsOn(Restore)
        .Executes(() => DotNetBuild(s => s
            .SetProjectFile(Project)
            .SetConfiguration(Configuration)
            .EnableNoRestore()
            .CombineWith(Architectures, (settings, architecture) => settings
                .SetProperty("Platform", architecture))));

    /// <summary>Publishes x64 and arm64 application layouts.</summary>
    private Target Publish => target => target
        .Produces(RootDirectory / ArtifactsDirectoryName / "publish" / "**/*")
        .Executes(() => DotNetPublish(s => s
            .SetProject(Project)
            .SetConfiguration("Release")
            .SetProperty("GenerateAppxPackageOnBuild", "false")
            .SetProperty("Version", GetSemanticVersion())
            .CombineWith(Architectures, (settings, architecture) => settings
                .SetRuntime($"win-{architecture}")
                .SetProperty("Platform", architecture)
                .SetOutput(PublishDirectory(architecture)))));

    /// <summary>Publishes and archives portable x64 and arm64 applications.</summary>
    [SuppressMessage("Major Code Smell", "S1144", Justification = "NUKE invokes this target by name from CI.")]
    private Target ArchivePortable => target => target
        .DependsOn(Publish)
        .Produces(Portable / "*")
        .Executes(ArchivePortableBuilds);

    /// <summary>Creates signed MSIX packages and a signed dual architecture bundle.</summary>
    [SuppressMessage("Major Code Smell", "S1144", Justification = "NUKE invokes this target by name from CI.")]
    private Target Package => target => target
        .DependsOn(Publish)
        .Triggers(ArchivePortable)
        .Produces(Packages / "*")
        .Executes(() =>
        {
            if (SeparatePdbs)
            {
                SeparateSymbols();
            }

            Pack();
            if (SeparatePdbs)
            {
                ArchiveSymbols();
            }
        });

    /// <summary>Runs the default local build.</summary>
    public static int Main() => Execute<Build>(x => x.Compile);

    private string GetSemanticVersion()
    {
        var manifestVersion = XDocument.Load(Manifest).Root?.Elements()
                                  .First(element =>
                                      string.Equals(element.Name.LocalName, "Identity", StringComparison.Ordinal))
                                  .Attribute("Version")?.Value
                              ?? throw new InvalidOperationException("The package manifest has no version.");
        var version = SemVer ?? string.Join('.', manifestVersion.Split('.')[..3]);
        if (!Regex.IsMatch(version, @"^\d+\.\d+\.\d+$", RegexOptions.None, TimeSpan.FromSeconds(1)))
        {
            throw new InvalidOperationException("The semantic version must have three numeric components.");
        }

        return version;
    }

    private string GetPackageVersion()
    {
        if (Revision is < 0 or > 65535)
        {
            throw new InvalidOperationException("MSIX revision must be between 0 and 65535.");
        }

        return $"{GetSemanticVersion()}.{Revision}";
    }

    private static AbsolutePath PublishDirectory(string architecture) =>
        RootDirectory / ArtifactsDirectoryName / "publish" / architecture;

    /// <summary>Moves build-matched PDBs out of the release package layouts.</summary>
    private static void SeparateSymbols()
    {
        if (Directory.Exists(Symbols))
        {
            Directory.Delete(Symbols, true);
        }

        foreach (var architecture in Architectures)
        {
            var publish = PublishDirectory(architecture);
            var pdbs = Directory.GetFiles(publish, "*.pdb", SearchOption.AllDirectories);
            if (pdbs.Length == 0)
            {
                throw new InvalidOperationException($"No PDB files were produced for {architecture}.");
            }

            foreach (var pdb in pdbs)
            {
                var destination = Path.Combine(Symbols.ToString(), architecture, Path.GetRelativePath(publish, pdb));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Move(pdb, destination);
            }
        }
    }

    /// <summary>Archives each architecture's portable application.</summary>
    private void ArchivePortableBuilds()
    {
        Directory.CreateDirectory(Portable);
        foreach (var architecture in Architectures)
        {
            PublishDirectory(architecture).ZipTo(
                Portable / $"ImmersiveTB-{GetSemanticVersion()}-{architecture}-Portable.zip",
                compressionLevel: CompressionLevel.SmallestSize,
                fileMode: FileMode.Create);
        }
    }

    /// <summary>Archives the separated PDBs for each architecture.</summary>
    private void ArchiveSymbols()
    {
        foreach (var architecture in Architectures)
        {
            (Symbols / architecture).ZipTo(
                Symbols / $"ImmersiveTB-{GetSemanticVersion()}-{architecture}-PDBs.zip",
                compressionLevel: CompressionLevel.SmallestSize,
                fileMode: FileMode.Create);
        }
    }

    private void Pack()
    {
        Directory.CreateDirectory(Packages);
        var staging = RootDirectory / ArtifactsDirectoryName / "staging" / Guid.NewGuid().ToString("N");
        var manifests = RootDirectory / ArtifactsDirectoryName / "manifests" / Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(staging);
        Directory.CreateDirectory(manifests);
        string? pfxPath = null;

        try
        {
            X509Certificate2 certificate;
            string[] signingOptions;
            if (string.IsNullOrWhiteSpace(SigningCertificate) ||
                !string.IsNullOrWhiteSpace(SigningCertificateThumbprint))
            {
                var storedCertificate = FindStoreCertificate();
                certificate = storedCertificate.Certificate;
                signingOptions = storedCertificate.Location == StoreLocation.LocalMachine
                    ? ["/sha1", certificate.Thumbprint, "/s", "My", "/sm"]
                    : ["/sha1", certificate.Thumbprint, "/s", "My"];
            }
            else
            {
                if (string.IsNullOrEmpty(SigningCertificatePassword))
                {
                    throw new InvalidOperationException("The PFX certificate password is required.");
                }

                pfxPath = Path.Combine(Path.GetTempPath(), $"immersivetb-{Guid.NewGuid():N}.pfx");
                File.WriteAllBytes(pfxPath, Convert.FromBase64String(SigningCertificate));
                certificate = X509CertificateLoader.LoadPkcs12FromFile(pfxPath, SigningCertificatePassword);
                signingOptions = ["/f", pfxPath, "/p", SigningCertificatePassword];
            }

            using (certificate)
            {
                foreach (var architecture in Architectures)
                {
                    PackArchitecture(architecture, staging, manifests, signingOptions);
                }

                var bundle = Packages / $"{PackageName}_{GetPackageVersion()}_x64_arm64.msixbundle";
                RunWinApp("tool", "makeappx", "bundle", "/o", "/bv", GetPackageVersion(),
                    "/d", staging, "/p", bundle);
                SignArtifact(bundle, signingOptions);

                File.WriteAllBytes(Packages / "Certificate.cer", certificate.Export(X509ContentType.Cert));
            }
            // File.Copy(RootDirectory / "scripts" / "install-msix.ps1", Packages / "install-msix.ps1", true);
        }
        finally
        {
            if (pfxPath is not null)
            {
                File.Delete(pfxPath);
            }
        }
    }

    /// <summary>Finds one usable certificate that matches the package publisher.</summary>
    private (X509Certificate2 Certificate, StoreLocation Location) FindStoreCertificate()
    {
        var publisher = XDocument.Load(Manifest).Root?.Elements()
                            .First(element =>
                                string.Equals(element.Name.LocalName, "Identity", StringComparison.Ordinal))
                            .Attribute("Publisher")?.Value
                        ?? throw new InvalidOperationException("The package manifest has no publisher.");
        var thumbprint = SigningCertificateThumbprint?.Replace(" ", "", StringComparison.Ordinal);
        var now = DateTime.Now;
        var matches = new List<(X509Certificate2 Certificate, StoreLocation Location)>();

        foreach (var location in new[] { StoreLocation.CurrentUser, StoreLocation.LocalMachine })
        {
            using var store = new X509Store(StoreName.My, location);
            store.Open(OpenFlags.ReadOnly);
            matches.AddRange(store.Certificates
                .Where(certificate => certificate.HasPrivateKey
                                      && certificate.NotBefore <= now && now < certificate.NotAfter
                                      && string.Equals(certificate.Subject, publisher,
                                          StringComparison.OrdinalIgnoreCase)
                                      && (thumbprint is null || string.Equals(certificate.Thumbprint, thumbprint,
                                          StringComparison.OrdinalIgnoreCase)))
                .Select(certificate => (certificate, location)));
        }

        var distinctMatches = matches.DistinctBy(match => match.Certificate.Thumbprint,
            StringComparer.OrdinalIgnoreCase).ToList();
        return distinctMatches.Count switch
        {
            1 => distinctMatches[0],
            0 => throw new InvalidOperationException(
                $"No valid signing certificate with a private key for {publisher} was found in the Windows Personal certificate stores."),
            _ => throw new InvalidOperationException(
                "Multiple signing certificates match the package publisher. Pass --signing-certificate-thumbprint.")
        };
    }

    private void PackArchitecture(string architecture, AbsolutePath staging, AbsolutePath manifests,
        string[] signingOptions)
    {
        var manifest = XDocument.Load(Manifest);
        var identity = manifest.Root?.Elements().First(element =>
                           string.Equals(element.Name.LocalName, "Identity", StringComparison.Ordinal))
                       ?? throw new InvalidOperationException("The package manifest has no Identity element.");
        identity.SetAttributeValue("Version", GetPackageVersion());
        identity.SetAttributeValue("ProcessorArchitecture", architecture);

        var generatedManifest = manifests / $"Package.{architecture}.appxmanifest";
        manifest.Save(generatedManifest);
        var package = $"{PackageName}_{GetPackageVersion()}_{architecture}.msix";
        RunWinApp("pack", PublishDirectory(architecture), "--manifest", generatedManifest,
            "--exe", "ImmersiveTB.exe", "--no-sign", "--output", staging / package);
        SignArtifact(staging / package, signingOptions);
        File.Copy(staging / package, Packages / package, true);
    }

    /// <summary>Signs an MSIX package or bundle with the selected certificate.</summary>
    [SuppressMessage("Security", "S5332",
        Justification = "DigiCert documents an HTTP RFC 3161 endpoint; the timestamp response is signed.")]
    private void SignArtifact(AbsolutePath artifact, string[] signingOptions) =>
        RunWinApp([
            "tool", "signtool", "sign", "/fd", "SHA256", .. signingOptions,
            "/tr", "http://timestamp.digicert.com", "/td", "SHA256", artifact
        ]);

    private void RunWinApp(params string[] arguments)
    {
        var commandLine = string.Join(' ', arguments.Select(QuoteArgument));
        var process = ProcessTasks.StartProcess(ToolPathResolver.GetPathExecutable("winapp"), commandLine,
            RootDirectory,
            logInvocation: false,
            outputFilter: output => SigningCertificatePassword is { Length: > 0 } password
                ? output.Replace(password, "***", StringComparison.Ordinal)
                : output);
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"winapp failed with exit code {process.ExitCode}.");
        }
    }

    private static string QuoteArgument(string argument)
    {
        var escaped = Regex.Replace(argument, "(?<slashes>\\\\*)(?<quote>\"|$)", match =>
        {
            var slashes = match.Groups["slashes"].Length;
            var quote = match.Groups["quote"].Value;
            return new string('\\', slashes * 2 + quote.Length) + quote;
        }, RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));
        return $"\"{escaped}\"";
    }
}
