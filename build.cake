#tool "nuget:?package=GitVersion.CommandLine"
#tool "nuget:?package=TeamCity.VSTest.TestAdapter"
#tool "nuget:?package=TeamCity.Dotnet.Integration"


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
        if (TeamCity.IsRunningOnTeamCity)
        {
            Information(
                @"Environment:
                PullRequest: {0}
                Build Configuration Name: {1}
                TeamCity Project Name: {2}",
                BuildSystem.TeamCity.Environment.PullRequest.IsPullRequest,
                BuildSystem.TeamCity.Environment.Build.BuildConfName,
                BuildSystem.TeamCity.Environment.Project.Name
                );
        }
        else
        {
            Information("Not running on TeamCity");
        }
        
        CleanDirectories("./output");
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
        // https://github.com/JetBrains/TeamCity.MSBuild.Logger/wiki/How-to-use


        var buildSettings = new DotNetCoreMSBuildSettings
        {
            MaxCpuCount = 8
        };
        
        if(TeamCity.IsRunningOnTeamCity) 
        {
            buildSettings.DisableConsoleLogger();

            var logger = File("./tools/TeamCity.Dotnet.Integration.1.0.2/build/_common/msbuild15/TeamCity.MSBuild.Logger.dll").Path.FullPath;
            buildSettings.WithLogger(logger, "TeamCity.MSBuild.Logger.TeamCityMSBuildLogger", "teamcity;PerformanceSummary;Summary");
        }

        DotNetCoreBuild(solutionFile.FullPath, new DotNetCoreBuildSettings()
        {
            Configuration = configuration,
            NoRestore = true,
            VersionSuffix = versionSuffix,
            MSBuildSettings = buildSettings,
            DiagnosticOutput = true,
            Verbosity = DotNetCoreVerbosity.Minimal,
            
        });
    });

Task("Test")    
    .IsDependentOn("Build")
    .Does(() => 
    {
        // https://github.com/JetBrains/TeamCity.VSTest.TestAdapter
        
        bool hasError = false;
        foreach(var project in testProjects)
        {
            string frameworks = XmlPeek(project, "//TargetFrameworks");
            
            foreach(var framework in frameworks.Split(';')) 
            {
                try{
                DotNetCoreTest(project.FullPath, new DotNetCoreTestSettings
                {
                    Framework = framework,
                    NoBuild = true,
                    NoRestore = true,
                    Configuration = configuration,
                    ResultsDirectory = Directory("./output/testresults"),
                    Logger = "trx",
                    //Settings = File("testsettings.xml"),
                    //ArgumentCustomization = args=>args.Append("/parallel") 
                    //TestAdapterPath = Directory("./tools/TeamCity.Dotnet.Integration.1.0.2/build/_common/vstest15"),
                    //Logger = ""
                });
                }
                catch(Exception ex) {
                    Error(ex);
                    hasError = true;
                }
            }
        }

        if(hasError) {
            throw new CakeException("Tests have failed.");
        }
    })
    .Finally(() => {
        foreach(var f in GetFiles("./output/testresults/*.trx")) 
        {
            TeamCity.ImportData("vstest", f);
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
        TeamCity.PublishArtifacts("./output");
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