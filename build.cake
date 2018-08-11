#tool "nuget:?package=GitVersion.CommandLine"

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
var isLocalBuild = BuildSystem.IsLocalBuild;
var msBuildSettings = new DotNetCoreMSBuildSettings
{
    //MaxCpuCount = 1
};

var solutionFile = File("./src/XPlat.sln").Path;
var testProjects = GetFiles("./src/**/*Tests*.csproj");
var versionSuffix = "alpha";

Task("Clean")
    .Does(() =>
    {
        CleanDirectories("./src/**/bin");
	    CleanDirectories("./src/**/obj");
    });

Task("Restore")
    .Does(() => 
    {
        DotNetCoreRestore(solutionFile.FullPath, new DotNetCoreRestoreSettings
        {
            Verbosity = DotNetCoreVerbosity.Minimal,
            MSBuildSettings = msBuildSettings
        });
    });

Task("Build")
	.IsDependentOn("Clean")
	.IsDependentOn("Restore")
    .Does(() =>
    {
        DotNetCoreBuild(solutionFile.FullPath, new DotNetCoreBuildSettings()
        {
            Configuration = configuration,
            NoRestore = true,
            VersionSuffix = versionSuffix,
            MSBuildSettings = msBuildSettings,
            DiagnosticOutput = true,
            Verbosity = DotNetCoreVerbosity.Minimal
        });
    });

Task("Test")    
    .IsDependentOn("Build")
    .Does(() => 
    {
        foreach(var project in testProjects)
        {
            string frameworks = XmlPeek(project, "//TargetFrameworks");

            foreach(var framework in frameworks.Split(';')) 
            {
                DotNetCoreTest(project.FullPath, new DotNetCoreTestSettings
                {
                    Framework = framework,
                    NoBuild = true,
                    NoRestore = true,
                    Configuration = configuration
                });
            }
        }
    });

Task("Publish-WebApp")
	.IsDependentOn("Build")
	.Does(() => {
		DotNetCorePublish(File("./src/XPlat.Web.MVCNet461/XPlat.Web.MVCNet461.csproj").Path.FullPath, new DotNetCorePublishSettings
        {
            Framework = "net461",
            NoRestore = true,
            SelfContained = true,
            Configuration = configuration,
            VersionSuffix = versionSuffix,
            OutputDirectory = Directory("./output/web/net461"),
            MSBuildSettings = msBuildSettings
        });
	});

Task("Publish-Apps")
	.IsDependentOn("Build")
	.Does(() => {
		DotNetCorePublish(File("./src/XPlat.Runner/XPlat.Runner.csproj").Path.FullPath, new DotNetCorePublishSettings
        {
            Framework = "net462",
            NoRestore = true,
            SelfContained = true,
            Configuration = configuration,
            VersionSuffix = versionSuffix,
            OutputDirectory = Directory("./output/net462"),
            MSBuildSettings = msBuildSettings
        });

        DotNetCorePublish(File("./src/XPlat.Runner/XPlat.Runner.csproj").Path.FullPath, new DotNetCorePublishSettings
        {
            Framework = "netcoreapp2.1",
            Runtime = "ubuntu.16.04-x64",
            NoRestore = true,
            SelfContained = true,
            Configuration = configuration,
            VersionSuffix = versionSuffix,
            OutputDirectory = Directory("./output/ubuntu.16.04-x64"),
            MSBuildSettings = msBuildSettings
        });

        DotNetCorePublish(File("./src/XPlat.Runner/XPlat.Runner.csproj").Path.FullPath, new DotNetCorePublishSettings
        {
            Framework = "netcoreapp2.1",
            Runtime = "win7-x86",
            NoRestore = true,
            SelfContained = true,
            Configuration = configuration,
            VersionSuffix = versionSuffix,
            OutputDirectory = Directory("./output/win7-x86"),
            MSBuildSettings = msBuildSettings
        });

        DotNetCorePublish(File("./src/XPlat.Runner/XPlat.Runner.csproj").Path.FullPath, new DotNetCorePublishSettings
        {
            Framework = "netcoreapp2.1",
            Runtime = "win7-x64",
            NoRestore = true,
            SelfContained = true,
            Configuration = configuration,
            VersionSuffix = versionSuffix,
            OutputDirectory = Directory("./output/win7-x64"),
            MSBuildSettings = msBuildSettings
        });
	});

Task("Publish")
    .IsDependentOn("Publish-WebApp")
    .IsDependentOn("Publish-Apps")
    .Does(() => 
    {
    });

Task("Default")
    .IsDependentOn("Clean")
    .IsDependentOn("Restore")
    .IsDependentOn("Build")
    .IsDependentOn("Test")    
    .IsDependentOn("Publish")
    .Does(() => 
    {
    });

RunTarget(target);