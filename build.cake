#tool "nuget:?package=GitVersion.CommandLine"
#tool "nuget:?package=TeamCity.VSTest.TestAdapter"
#tool "nuget:?package=TeamCity.Dotnet.Integration"
#tool "nuget:?package=JetBrains.dotCover.CommandLineTools"

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");

//////////////////////////////////////////////////////////////////////
// SET PACKAGE VERSION
//////////////////////////////////////////////////////////////////////
var versionSuffix = "alpha";

//////////////////////////////////////////////////////////////////////
// DEFINE RUN CONSTANTS
//////////////////////////////////////////////////////////////////////

var solutionFile = File("./src/XPlat.sln").Path;
var testProjects = GetFiles("./src/**/*Tests*.csproj");
var isLocalBuild = BuildSystem.IsLocalBuild;
{
    //MaxCpuCount = 1
};

//////////////////////////////////////////////////////////////////////
// Clean
//////////////////////////////////////////////////////////////////////

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

//////////////////////////////////////////////////////////////////////
// Build
//////////////////////////////////////////////////////////////////////

Task("Build")
    .Does(() =>
    {
        DotNetCoreRestore(solutionFile.FullPath, new DotNetCoreRestoreSettings
        {
            Verbosity = DotNetCoreVerbosity.Minimal
        });

        // https://github.com/JetBrains/TeamCity.MSBuild.Logger/wiki/How-to-use


        var buildSettings = new DotNetCoreMSBuildSettings
        {
            MaxCpuCount = 8
        };
        
        if(TeamCity.IsRunningOnTeamCity) 
        {
            buildSettings.DisableConsoleLogger();

            var logger = File("./tools/TeamCity.Dotnet.Integration.1.0.2/build/_common/msbuild15/TeamCity.MSBuild.Logger.dll").Path.FullPath;
            buildSettings.WithLogger(logger, "TeamCity.MSBuild.Logger.TeamCityMSBuildLogger", "teamcity;Summary");
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

//////////////////////////////////////////////////////////////////////
// TEST
//////////////////////////////////////////////////////////////////////

Task("DotNetTest-All")    
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
                    Logger = $"trx;LogFileName={project.GetFilenameWithoutExtension()}.{framework}.trx",
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

Task("DotNetTest-WithCoverage")    
    .IsDependentOn("Build")
    .Does(() => 
    {
        bool hasError = false;
        foreach(var project in testProjects)
        {
            string frameworks = XmlPeek(project, "//TargetFrameworks");
            
            foreach(var framework in frameworks.Split(';')) 
            {
                try{
                    DotCoverCover(tool => {
                        tool.DotNetCoreTest(project.FullPath, new DotNetCoreTestSettings
                        {
                            Framework = framework,
                            NoBuild = true,
                            NoRestore = true,
                            Configuration = configuration,
                            ResultsDirectory = Directory("./output/testresults"),
                            Logger = $"trx;LogFileName={project.GetFilenameWithoutExtension()}.{framework}.trx",
                            //Settings = File("testsettings.xml"),
                            //ArgumentCustomization = args=>args.Append("/parallel") 
                            //TestAdapterPath = Directory("./tools/TeamCity.Dotnet.Integration.1.0.2/build/_common/vstest15"),
                            //Logger = ""
                        });
                        },
                        new FilePath($"./output/{project.GetFilenameWithoutExtension()}.{framework}.dcvr"),
                        new DotCoverCoverSettings()
                            .WithFilter("+:XPlat*")
                            .WithFilter("-:*Test*")
                    );
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
        Information("Merging dotCover output");

        DotCoverMerge(
            GetFiles("./output/*.dcvr"), 
            File("./output/merged.dcvr"));
        
        Information("Compiling report");
        DotCoverReport(
            File("./output/merged.dcvr"),
            File("./output/report.html"),
            new DotCoverReportSettings {
                ReportType = DotCoverReportType.HTML
            });
        
        foreach(var f in GetFiles("./output/testresults/*.trx")) 
        {
            TeamCity.ImportData("vstest", f);
        }
    });

//////////////////////////////////////////////////////////////////////
// Publish
//////////////////////////////////////////////////////////////////////

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
        });
	});


//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Rebuild")
    .IsDependentOn("Clean")
    .IsDependentOn("Build");

Task("Test")
    .IsDependentOn("DotNetTest-WithCoverage");

Task("Publish")
    .IsDependentOn("Publish-WebApp")
    .IsDependentOn("Publish-Apps")
    .Does(() => 
    {
        TeamCity.PublishArtifacts("./output");
    });
    
Task("Default")
    .IsDependentOn("Rebuild")
    .IsDependentOn("Test")
    .IsDependentOn("Publish");

RunTarget(target);