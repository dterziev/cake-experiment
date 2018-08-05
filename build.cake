#tool "nuget:?package=GitVersion.CommandLine"

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
var isLocalBuild = BuildSystem.IsLocalBuild;
    
var solutionFile = File("./src/XPlat.sln").Path;
var testProjects = GetFiles("./src/**/*Tests*.csproj");


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
            Verbosity = DotNetCoreVerbosity.Minimal
            //MSBuildSettings = msBuildSettings
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
            //MSBuildSettings = msBuildSettings
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


Task("Publish")    
    .IsDependentOn("Test")
    .Does(() => 
    {
        DotNetCorePublish(File("./src/XPlat.Runner/XPlat.Runner.csproj").Path.FullPath, new DotNetCorePublishSettings
        {
            Framework = "netcoreapp2.1",
            Runtime = "ubuntu.16.04-x64",
            SelfContained = true,
            Configuration = configuration,
            VersionSuffix = "abcde"
        });

        DotNetCorePublish(File("./src/XPlat.Runner/XPlat.Runner.csproj").Path.FullPath, new DotNetCorePublishSettings
        {
            Framework = "netcoreapp2.1",
            Runtime = "win7-x86",
            SelfContained = true,
            Configuration = configuration,
            VersionSuffix = "abcde"
        });

        DotNetCorePublish(File("./src/XPlat.Runner/XPlat.Runner.csproj").Path.FullPath, new DotNetCorePublishSettings
        {
            Framework = "netcoreapp2.1",
            Runtime = "win7-x64",
            SelfContained = true,
            Configuration = configuration,
            VersionSuffix = "abcde"
        });
    });

Task("Default")
    .IsDependentOn("Clean")
    .IsDependentOn("Restore")
    .IsDependentOn("Build")
    .IsDependentOn("Test")    
    .Does(() => 
    {
    });

RunTarget(target);