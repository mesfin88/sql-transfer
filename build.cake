string BranchName = Argument("branchName", string.Empty);
string NugetServer = Argument("nugetServer", string.Empty);
string NugetApiKey = Argument("nugetApiKey", string.Empty);
string SelectedEnvironment = string.Empty;

string SolutionName = "MessageStorage";
var ProjectsToBePacked  = new Project[]
{
  new Project("MessageStorage"),
  new Project("MessageStorage.Db"),
  new Project("MessageStorage.Db.MsSql"),
  new Project("MessageStorage.Db.MsSql.DI.Extension"),
  new Project("MessageStorage.DI.Extension"),
};

var TestProjectPatterns = new string[]{
  "./**/*Test.csproj",
  "./**/*Tests.csproj",
};

var BuildConfig = "Release";
var DotNetPackedPath = "./dotnet-packed";

string MasterEnvironment = "prod";
var BranchEnvironmentPairs = new Dictionary<string, string>()
{
  {"master", MasterEnvironment },
  {"dev", "develop" },
  {"develop", "develop" }
};

string[] DirectoriesToBeRemoved  = new string[]{
  $"./**/{SolutionName}*/**/bin/**",
  $"./**/{SolutionName}*/**/obj/**",
  $"./**/{SolutionName}*/**/build/**",
  DotNetPackedPath,
};

string CheckEnvVariableStage = "Check Env Variable";
string RemoveDirectoriesStage = "Remove Directories";
string DotNetCleanStage = "DotNet Clean";
string UnitTestStage = "Unit Test";
string DotNetPackStage = "DotNet Pack";
string PushNugetStage = "Push Nuget";
string FinalStage = "Final";

Task(CheckEnvVariableStage)
.Does(()=>
{
  if(string.IsNullOrEmpty(BranchName))
    throw new Exception("branchName should not be empty");

  Console.WriteLine($"BranchName = {BranchName}");
  
  if(BranchEnvironmentPairs.ContainsKey(BranchName))
  {
    SelectedEnvironment = BranchEnvironmentPairs[BranchName];
    Console.WriteLine("Selected Env = " + SelectedEnvironment);
  }
  else
  {
    Console.WriteLine("There is no predefined env for this branch");
  }

  if(!string.IsNullOrEmpty(SelectedEnvironment))
    if(string.IsNullOrEmpty(NugetServer) || string.IsNullOrEmpty(NugetApiKey))
      throw new Exception("When selected environment is not empty, you should supply nuget server and nuget api key");
});

Task(RemoveDirectoriesStage)
.DoesForEach(DirectoriesToBeRemoved  , (directoryPath)=>
{
  var directories = GetDirectories(directoryPath);
    
  foreach (var directory in directories)
  {
    if(!DirectoryExists(directory)) continue;
    
    Console.WriteLine("Directory is cleaning : " + directory.ToString());     

    var settings = new DeleteDirectorySettings
    {
      Force = true,
      Recursive  = true
    };
    DeleteDirectory(directory, settings);
  }
});

Task(DotNetCleanStage)
.IsDependentOn(CheckEnvVariableStage)
.IsDependentOn(RemoveDirectoriesStage)
.Does(()=>
{
  DotNetCoreClean($"{SolutionName}.sln");
});

Task(UnitTestStage)
.IsDependentOn(DotNetCleanStage)
.DoesForEach(TestProjectPatterns, (testProjectPattern)=>
{
  FilePathCollection testProjects = GetFiles(testProjectPattern);
  foreach (var testProject in testProjects)
  {
    Console.WriteLine($"Tests are running : {testProject.ToString()}" );
    var testSettings = new DotNetCoreTestSettings{Configuration = BuildConfig};
    DotNetCoreTest(testProject.FullPath, testSettings);
  }
});

Task(DotNetPackStage)
.WithCriteria(!string.IsNullOrEmpty(SelectedEnvironment))
.IsDependentOn(UnitTestStage)
.DoesForEach(ProjectsToBePacked , (project)=>
{
  FilePath projFile = GetCsProjFile(project.Name);
  
  string versionSuffix = SelectedEnvironment;
  if(SelectedEnvironment == MasterEnvironment)
    versionSuffix = string.Empty;

  var settings = new DotNetCorePackSettings
  {
    Configuration = BuildConfig,
    OutputDirectory = DotNetPackedPath,
    VersionSuffix = versionSuffix
  };

  DotNetCorePack(projFile.ToString(), settings);
});


Task(PushNugetStage)
.WithCriteria(!string.IsNullOrEmpty(SelectedEnvironment))
.IsDependentOn(DotNetPackStage)
.DoesForEach(ProjectsToBePacked , (project)=>
{
  string filePathPattern = $"{DotNetPackedPath}*{project.Name}*.nupkg";
  var nugetPackages = GetFiles(filePathPattern);
  foreach (var nugetPackage in nugetPackages)
  {
    PublishNugetPackage(project.Name, nugetPackage, NugetServer, NugetApiKey);  
  }
});


Task(FinalStage)
.IsDependentOn(PushNugetStage)
.Does(() =>
{
  Console.WriteLine("Operation is completed succesfully");
});

var target = Argument("target", FinalStage);
RunTarget(target);

// Utility

FilePath GetCsProjFile(string projectName)
{
  FilePathCollection projFiles = GetFiles($"./**/{projectName}.csproj");
  if(projFiles.Count != 1)
  {
    foreach(var pName in projFiles)
    {
      Console.WriteLine(pName);
    }
    
    throw new Exception($"Only one {projectName}.csproj should be found");
  }
  
  return projFiles.First();
}

private void PublishNugetPackage (string packageId, FilePath packagePath, string nugetSourceUrl, string apiKey)
{
    if(IsNuGetPublished(packageId, packagePath, nugetSourceUrl))
    {
        Console.WriteLine($"{packageId} is already published. Hence this package will be skipped");
        return;
    }

    var nugetPushSettings = new NuGetPushSettings
    {
        ApiKey = apiKey,
        Source = nugetSourceUrl
    };
    
    Console.WriteLine($"{packageId} is publishing");
    NuGetPush(packagePath.FullPath, nugetPushSettings);  
    Console.WriteLine($"{packageId} is publishing");
}

private bool IsNuGetPublished(string packageId, FilePath packagePath, string nugetSourceUrl) {
    string packageNameWithVersion = packagePath.GetFilename().ToString().Replace(".nupkg", "");
    var latestPublishedVersions = NuGetList(
        packageId,
        new NuGetListSettings 
        {
            Prerelease = true,
            Source = new string[]{nugetSourceUrl}
        }
    );

    return latestPublishedVersions.Any(p => packageNameWithVersion.EndsWith(p.Version));
}

class Project
{
  public Project(string name, string runtime = "")
  {
    Name = name;
    Runtime = runtime;
  }
  
  public string Name {get;}
  public string Runtime {get;}
}