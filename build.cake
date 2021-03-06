#tool "nuget:?package=GitVersion.CommandLine&version=3.6.5"
#tool "nuget:?package=TeamCity.Dotnet.Integration&version=1.0.2"
#tool "nuget:?package=JetBrains.dotCover.CommandLineTools&version=2018.1.4"
#addin "nuget:?package=Newtonsoft.Json"

using Newtonsoft.Json;

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");

//////////////////////////////////////////////////////////////////////
// SET PACKAGE VERSION
//////////////////////////////////////////////////////////////////////
GitVersion version = null;
var versionSuffix = "alpha";

//////////////////////////////////////////////////////////////////////
// DEFINE RUN CONSTANTS
//////////////////////////////////////////////////////////////////////

var solutionFile = File("./src/XPlat.sln").Path;
var testProjects = GetFiles("./src/**/*Tests*.csproj");
var outputDir = Directory("./output");
var testResultsDir = Directory("./output/testresults");

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
        
        CleanDirectories(outputDir);
        CleanDirectories("./src/**/bin");
	    CleanDirectories("./src/**/obj");
    });

//////////////////////////////////////////////////////////////////////
// Build
//////////////////////////////////////////////////////////////////////
Task("UpdateAssemblyInfo")
    .Does(() => {

        if (!BuildSystem.IsLocalBuild) 
        {
            GitVersion(new GitVersionSettings {
                UpdateAssemblyInfoFilePath = File("./src/SolutionAssemblyInfo.cs"),
                UpdateAssemblyInfo = true,
                OutputType = GitVersionOutput.BuildServer
            });
        }

        version = GitVersion(new GitVersionSettings{ OutputType = GitVersionOutput.Json });
        Information("Version: " + version.NuGetVersion);

        CreateDirectory(outputDir);
        System.IO.File.WriteAllText(
            $"{outputDir}/version.json", 
            JsonConvert.SerializeObject(version, Formatting.Indented));
    });

Task("Build")
    .IsDependentOn("UpdateAssemblyInfo")
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
                    ResultsDirectory = testResultsDir,
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
        foreach(var f in GetFiles($"{testResultsDir}/*.trx")) 
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
                            ResultsDirectory = testResultsDir,
                            Logger = $"trx;LogFileName={project.GetFilenameWithoutExtension()}.{framework}.trx",
                            //Settings = File("testsettings.xml"),
                            //ArgumentCustomization = args=>args.Append("/parallel") 
                            //TestAdapterPath = Directory("./tools/TeamCity.Dotnet.Integration.1.0.2/build/_common/vstest15"),
                            //Logger = ""
                        });
                        },
                        File($"{testResultsDir}/{project.GetFilenameWithoutExtension()}.{framework}.dcvr"),
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

        var mergedCoverage = File($"{testResultsDir}/merged.dcvr");
        DotCoverMerge(
            GetFiles($"{testResultsDir}/*.dcvr"), 
            mergedCoverage);
        
        TeamCity.ImportDotCoverCoverage(mergedCoverage);
        
        foreach(var f in GetFiles($"{testResultsDir}/*.trx")) 
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