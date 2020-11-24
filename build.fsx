#r @"paket: groupref Build //"

#if !FAKE
#load "./.fake/build.fsx/intellisense.fsx"
#r "netstandard" // Temp fix for https://github.com/fsharp/FAKE/issues/1985
#endif

open Fake.Core
open Fake.DotNet
open Fake.Tools
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Fake.Core.TargetOperators
open Fake.Api
open Paket.Core
open Fake.DotNet.Testing
open Fake.DotNet.Testing

// --------------------------------------------------------------------------------------
// Information about the project to be used at NuGet and in AssemblyInfo files
// --------------------------------------------------------------------------------------

let project = "FSharp.Linq.ComposableQuery"

let summary = "A Compositional, Safe Query Framework for Dynamic F# Queries."

let description = """
  A Compositional, Safe Query Framework for Dynamic F# Queries
  A quotations evaluator for F# based on LINQ expression tree compilation. Some constructs are not supported and performance may be slower than F# compiled code. Fork from https://github.com/fsprojects/FSharp.Quotations.Evaluator. Repack using .NET SDK, targeting netstandard2.0, netcoreapp3.1, net472. Removed deprecated LinqToSQL 'Like' support. Original repo based on the old F# 2.0 PowerPack code. 
  """

let authors =  "James Cheney, Sam Lindley, Yordan Stoyanov, Beau Van Kirk (Update/repack)"
let tags = "F# fsharp LINQ SQL database data dynamic query"
let copyright = "November 2020"

let gitOwner = "Beau Van Kirk"
let gitName = "FSharp.Linq.ComposableQuery"
let gitHome = "https://github.com/" + gitOwner
let gitUrl = gitHome + "/" + gitName

// --------------------------------------------------------------------------------------
// Build variables
// --------------------------------------------------------------------------------------

let buildDir  = "./build/"
let nugetDir  = "./out/"


System.Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

let sourceProjectsGlob =  "src/**/*.fsproj"
let testProjectsGlob = "tests/**/*.fsproj"

let sourceProjectFiles = !! sourceProjectsGlob

let testProjectFiles = !! testProjectsGlob

let allProjectFiles = !! sourceProjectsGlob  ++ testProjectsGlob
// let changelogFilename = "CHANGELOG.md"
// let changelog = Changelog.load changelogFilename
// let latestEntry = changelog.LatestEntry

// Helper function to remove blank lines
let isEmptyChange = function
    | Changelog.Change.Added s
    | Changelog.Change.Changed s
    | Changelog.Change.Deprecated s
    | Changelog.Change.Fixed s
    | Changelog.Change.Removed s
    | Changelog.Change.Security s
    | Changelog.Change.Custom (_, s) ->
        String.isNullOrWhiteSpace s.CleanedText
let loadProjects projectPaths =
    seq {
        for fp in projectPaths do
            yield Paket.ProjectFile.loadFromFile fp
    }

let allProjects = loadProjects allProjectFiles
let testProjects = loadProjects testProjectFiles

let packableProjects =
    allProjects
    |> Seq.filter (
        fun p ->
            p.GetProperty "IsPackable"
            |> function
                | Some "true" | Some "" -> true
                | Some "false" -> false
                | _ -> false
    )

// let nugetVersion = latestEntry.NuGetVersion
// let packageReleaseNotes = sprintf "%s/blob/v%s/CHANGELOG.md" gitUrl latestEntry.NuGetVersion
let releaseNotesPath = "RELEASE_NOTES.md"
let releaseNotes = ReleaseNotes.load releaseNotesPath
let nugetVersion = releaseNotes.NugetVersion
// let releaseNotes =
//     latestEntry.Changes
//     |> List.filter (isEmptyChange >> not)
//     |> List.map (fun c -> " * " + c.ToString())
//     |> String.concat "\n"

// --------------------------------------------------------------------------------------
// Helpers
// --------------------------------------------------------------------------------------
let isNullOrWhiteSpace = System.String.IsNullOrWhiteSpace

let exec cmd args dir =
    let proc =
        CreateProcess.fromRawCommandLine cmd args
        |> CreateProcess.ensureExitCodeWithMessage (sprintf "Error while running '%s' with args: %s" cmd args)
    (if isNullOrWhiteSpace dir then proc
    else proc |> CreateProcess.withWorkingDirectory dir)
    |> Proc.run
    |> ignore

let projectDirectory (p:Paket.ProjectFile) = p.FileName |> Path.getDirectory

let projectPaketDependencies (p:Paket.ProjectFile) =
    p
    |> projectDirectory
    |> fun pd ->
        let refPath = Path.combine pd "paket.references"
        match Path.isFile refPath with
        | false -> []
        | true ->
            seq {
                let groups = Paket.ReferencesFile.FromFile refPath |> fun p -> p.Groups
                for kv in groups do
                    for p in (kv.Value.NugetPackages) do
                            yield p.Name.Name
            } |> Seq.toList
let projectDependsOn (dependencyName: string) (p: Paket.ProjectFile) =
    p
    |> (projectPaketDependencies >> (List.contains dependencyName))
let getBuildParam = Environment.environVar
let addSuffix (suffix: string) (basePath: string) = Path.combine basePath suffix

let guessTestAssemblyPath (p: Paket.ProjectFile) =
    let projectDir = projectDirectory p
    let projectName = p.Name
    let assmName =
        projectDir
        |> addSuffix (Path.combine "Release" "netcoreapp3.1" |> Path.combine "bin" )
        |> addSuffix (String.replace "fsproj" "dll" projectName)
    assmName

let genericRunTests (runner: (('P->'P) -> (string seq) -> unit)) (projects: Paket.ProjectFile seq) =
    projects
    |> Seq.map guessTestAssemblyPath
    |> (fun fps ->
            let validAssemblyPaths = seq {
                for fp in fps do
                    match Path.isFile fp with
                    | false -> fp |> sprintf "Guessed test assembly path %A not found." |> Trace.trace
                    | true -> yield fp
            }
            validAssemblyPaths |> runner id
        )
// --------------------------------------------------------------------------------------
// Build Targets
// --------------------------------------------------------------------------------------

let DoNothing = ignore

Target.create "Clean" (fun _ ->
    Shell.cleanDirs [buildDir; nugetDir]
)

Target.create "AssemblyInfo" (fun _ ->
    let buildTime = let nowUtc = System.DateTime.UtcNow in nowUtc.ToString("yyyy-MM-dd")
    allProjects
    |> Seq.map projectDirectory
    |> Seq.iter (
        fun dir ->
            let fileName = Path.combine dir "AssemblyInfo.fs"
            AssemblyInfoFile.createFSharp fileName
              [ AssemblyInfo.Title gitName
                AssemblyInfo.Product gitName
                AssemblyInfo.Description description
                AssemblyInfo.FileVersion releaseNotes.AssemblyVersion 
                AssemblyInfo.InformationalVersion releaseNotes.AssemblyVersion 
                AssemblyInfo.Metadata("BuildDate", buildTime)  ]
    )
)

Target.create "Build" (fun _ ->
    DotNet.build id ""
)
// let packageNamesFromGroupMap (m: Map<Paket.Domain.GroupName,Paket.InstallGroup>) =
//     seq {
//         for kv in m do
//             let lkv = kv.Value
//             for p in (kv.Value.NugetPackages) do
//                     yield p.Name
//     }


    
Target.create "Test" (fun _ ->
    let expectoProjects =
        testProjects
        |> Seq.filter (projectDependsOn "Expecto")
    let nUnitProjects =
        testProjects
        |> Seq.filter (projectDependsOn "NUnit")
    sprintf "Found Expecto projects: %A." (expectoProjects |> Seq.map (fun p -> p.Name) |> Seq.toList) |> Trace.trace
    
    sprintf "Found NUnit projects: %A." (nUnitProjects |> Seq.map (fun p -> p.Name) |> Seq.toList) |> Trace.trace
    

    // genericRunTests NUnit3.run nUnitProjects
    // genericRunTests Expecto.run expectoProjects
    testProjects
    |> Seq.map (fun p -> p.FileName)
    |> Seq.iter (DotNet.test id)

    // exec "dotnet"  @"run --project .\tests\FSharp.Linq.ComposableQuery.UnitTests\FSharp.Linq.ComposableQuery.UnitTests.fsproj" "."
)

Target.create "Docs" (fun _ ->
    exec "dotnet"  @"fornax build" "docs"
)

// --------------------------------------------------------------------------------------
// Release Targets
// --------------------------------------------------------------------------------------
Target.create "BuildRelease" (fun _ ->
    DotNet.build (fun p ->
        { p with
            Configuration = DotNet.BuildConfiguration.Release
            OutputPath = Some buildDir
            MSBuildParams = { p.MSBuildParams with Properties = [("Version", nugetVersion); ("PackageReleaseNotes", releaseNotesPath)]}
        }
    ) "FSharp.Linq.ComposableQuery.sln"
)


Target.create "Pack" (fun _ ->
    let properties = [
        ("Version", nugetVersion);
        ("Authors", authors)
        ("PackageProjectUrl", gitUrl)
        ("PackageTags", tags)
        ("RepositoryType", "git")
        ("RepositoryUrl", gitUrl)
        ("PackageLicenseUrl", gitUrl + "/LICENSE")
        ("Copyright", copyright)
        ("PackageReleaseNotes", releaseNotesPath)
        ("PackageDescription", summary)
        ("EnableSourceLink", "true")
    ]


    DotNet.pack (fun p ->
        { p with
            Configuration = DotNet.BuildConfiguration.Release
            OutputPath = Some nugetDir
            MSBuildParams = { p.MSBuildParams with Properties = properties}
        }
    ) "FSharp.Linq.ComposableQuery.sln"
)

Target.create "ReleaseGitHub" (fun _ ->
    let remote =
        Git.CommandHelper.getGitResult "" "remote -v"
        |> Seq.filter (fun (s: string) -> s.EndsWith("(push)"))
        |> Seq.tryFind (fun (s: string) -> s.Contains(gitOwner + "/" + gitName))
        |> function None -> gitHome + "/" + gitName | Some (s: string) -> s.Split().[0]

    Git.Staging.stageAll ""
    Git.Commit.exec "" (sprintf "Bump version to %s" nugetVersion)
    Git.Branches.pushBranch "" remote (Git.Information.getBranchName "")


    Git.Branches.tag "" nugetVersion
    Git.Branches.pushTag "" remote nugetVersion

    let client =
        let user =
            match getBuildParam "github-user" with
            | s when not (isNullOrWhiteSpace s) -> s
            | _ -> UserInput.getUserInput "Username: "
        let pw =
            match getBuildParam "github-pw" with
            | s when not (isNullOrWhiteSpace s) -> s
            | _ -> UserInput.getUserPassword "Password: "

        // Git.createClient user pw
        GitHub.createClient user pw
    let files = !! (nugetDir </> "*.nupkg")



    // release on github
    let cl =
        client
        |> GitHub.draftNewRelease gitOwner gitName nugetVersion (releaseNotes.SemVer.PreRelease <> None) [releaseNotesPath]
    (cl,files)
    ||> Seq.fold (fun acc e -> acc |> GitHub.uploadFile e)
    |> GitHub.publishDraft//releaseDraft
    |> Async.RunSynchronously
)

Target.create "Push" (fun _ ->
    let key =
        match getBuildParam "nuget-key" with
        | s when not (isNullOrWhiteSpace s) -> s
        | _ -> UserInput.getUserPassword "NuGet Key: "
    Paket.push (fun p -> { p with WorkingDir = nugetDir; ApiKey = key; ToolType = ToolType.CreateLocalTool() }))

// Target.create "GenerateDocs" (fun _ ->
//     DotNet.exec "fsi" 
//     // executeFSIWithArgs "docs/tools" "generate.fsx" ["--define:RELEASE"] [] |> ignore
// )

// --------------------------------------------------------------------------------------
// Release Scripts

// Target.create "ReleaseDocs" (fun _ ->
//     let tempDocsDir = "temp/gh-pages"
//     CleanDir tempDocsDir
//     Repository.cloneSingleBranch "" (gitHome + "/" + gitName + ".git") "gh-pages" tempDocsDir

//     Repository.fullclean tempDocsDir
//     CopyRecursive "docs/output" tempDocsDir true |> tracefn "%A"
//     StageAll tempDocsDir
//     Commit tempDocsDir (sprintf "Update generated documentation for version %s" release.NugetVersion)
//     Branches.push tempDocsDir
// )
// --------------------------------------------------------------------------------------
// Build order
// --------------------------------------------------------------------------------------
Target.create "Default" DoNothing
Target.create "Release" DoNothing
Target.create "Rebuild" DoNothing
Target.create "CleanTest" DoNothing

"Clean"
    ?=> "AssemblyInfo"
    ?=> "Build"
    ?=> "BuildRelease"
    ?=> "Test"

"Clean" ==> "Rebuild"
"Clean" ==> "AssemblyInfo"
"Build" ==> "Rebuild"

"Rebuild" ==> "CleanTest"
"Test" ==> "CleanTest"

"Clean" ==> "BuildRelease"
"AssemblyInfo" ==> "BuildRelease"
//  ==> "GenerateDocs"

"BuildRelease"
  ==> "Pack"
  ==> "ReleaseGitHub"
  ==> "Push"
  ==> "Release"

"Pack"
  ==> "Default"

Target.runOrDefault "Default"

// // --------------------------------------------------------------------------------------
// // FAKE build script 
// // --------------------------------------------------------------------------------------

// #r @"packages/FAKE/tools/FakeLib.dll"
// open Fake 
// open Fake.Git
// open Fake.AssemblyInfoFile
// open Fake.ReleaseNotesHelper
// open System

// // --------------------------------------------------------------------------------------
// // START TODO: Provide project-specific details below
// // --------------------------------------------------------------------------------------

// // Information about the project are used
// //  - for version and project name in generated AssemblyInfo file
// //  - by the generated NuGet package 
// //  - to run tests and to publish documentation on GitHub gh-pages
// //  - for documentation, you also need to edit info in "docs/tools/generate.fsx"

// // The name of the project 
// // (used by attributes in AssemblyInfo, name of a NuGet package and directory in 'src')
// let project = "FSharpComposableQuery"

// // Short summary of the project
// // (used as description in AssemblyInfo and as a short summary for NuGet package)
// let summary = "A Compositional, Safe Query Framework for Dynamic F# Queries."

// // Longer description of the project
// // (used as a description for NuGet package; line breaks are automatically cleaned up)
// let description = """
//   A Compositional, Safe Query Framework for Dynamic F# Queries
//   """
// // List of author names (for NuGet package)
// let authors = [ "James Cheney"; "Sam Lindley"; "Yordan Stoyanov" ]
// // Tags for your project (for NuGet package)
// let tags = "F# fsharp LINQ SQL database data dynamic query"

// // File system information 
// // Pattern specifying all library files (projects or solutions)
// let libraryReferences  = !! "src/*/*.fsproj"
// // Pattern specifying all test files (projects or solutions)
// let testReferences = !! "tests/*/*.fsproj"
// // The output directory
// let buildDir = "./bin/"


// // Pattern specifying assemblies to be tested using MSTest
// let testAssemblies = !! "bin/FSharpComposableQuery*Tests*.exe"

// // Git configuration (used for publishing documentation in gh-pages branch)
// // The profile where the project is posted 
// let gitHome = "https://github.com/fsprojects"
// // The name of the project on GitHub
// let gitName = "FSharpComposableQuery"

// // --------------------------------------------------------------------------------------
// // END TODO: The rest of the file includes standard build steps 
// // --------------------------------------------------------------------------------------

// // Read additional information from the release notes document
// Environment.CurrentDirectory <- __SOURCE_DIRECTORY__
// let release = parseReleaseNotes (IO.File.ReadAllLines "RELEASE_NOTES.md")

// // Generate assembly info files with the right version & up-to-date information
// Target "AssemblyInfo" (fun _ ->
//   let fileName = "src/" + project + "/AssemblyInfo.fs"
//   CreateFSharpAssemblyInfo fileName
//       [ Attribute.InternalsVisibleTo "FSharpComposableQuery.Tests"
//         Attribute.Title project
//         Attribute.Product project
//         Attribute.Description summary
//         Attribute.Version release.AssemblyVersion
//         Attribute.FileVersion release.AssemblyVersion ] 
// )

// // --------------------------------------------------------------------------------------
// // Clean build results & restore NuGet packages

// Target "RestorePackages" (fun _ ->
//     !! "./**/packages.config"
//     |> Seq.iter (RestorePackage (fun p -> { p with ToolPath = "./.nuget/NuGet.exe" }))
// )

// Target "Clean" (fun _ ->
//     CleanDirs [buildDir; "temp"]
// )

// Target "CleanDocs" (fun _ ->
//     CleanDirs ["docs/output"]
// )

// // --------------------------------------------------------------------------------------
// // Build library

// Target "Build" (fun _ ->
//     let props = [("DocumentationFile", project + ".XML")]   //explicitly generate XML documentation
//     MSBuildReleaseExt buildDir props "Rebuild" libraryReferences
//     |> Log "Build-Output: "
// )

// // --------------------------------------------------------------------------------------
// // Build tests and library

// Target "BuildTest" (fun _ ->
//     MSBuildRelease buildDir "Rebuild" testReferences
//     |> Log "BuildTest-Output: "
// )

// // --------------------------------------------------------------------------------------
// // Run unit tests using test runner & kill test runner when complete

// Target "RunTests" (fun _ ->
//     let nunitVersion = GetPackageVersion "packages" "NUnit.Runners"
//     let nunitPath = sprintf "packages/NUnit.Runners.%s/Tools" nunitVersion
//     ActivateFinalTarget "CloseTestRunner"

//     testAssemblies
//     |> NUnit (fun p ->
//         { p with
//             ToolPath = nunitPath
//             DisableShadowCopy = true
//             TimeOut = TimeSpan.FromMinutes 20.
//             OutputFile = "TestResults.xml" })
// )

// FinalTarget "CloseTestRunner" (fun _ ->
//     ProcessHelper.killProcess "nunit-agent.exe"
// )

// // --------------------------------------------------------------------------------------
// // Build a NuGet package

// Target "NuGet" (fun _ ->
//     // Format the description to fit on a single line (remove \r\n and double-spaces)
//     let description = description.Replace("\r", "")
//                                  .Replace("\n", "")
//                                  .Replace("  ", " ")
//     let nugetPath = ".nuget/nuget.exe"
//     NuGet (fun p -> 
//         { p with   
//             Authors = authors
//             Project = project
//             Summary = summary
//             Description = description
//             Version = release.NugetVersion
//             ReleaseNotes = String.Join(Environment.NewLine, release.Notes)
//             Tags = tags
//             OutputPath = "bin"
//             ToolPath = nugetPath
//             AccessKey = getBuildParamOrDefault "nugetkey" ""
//             Publish = hasBuildParam "nugetkey"
//             Dependencies = [] })
//         ("nuget/" + project + ".nuspec")
// )

// --------------------------------------------------------------------------------------
// Generate the documentation

// Target "GenerateDocs" (fun _ ->
//     executeFSIWithArgs "docs/tools" "generate.fsx" ["--define:RELEASE"] [] |> ignore
// )

// // --------------------------------------------------------------------------------------
// // Release Scripts

// Target "ReleaseDocs" (fun _ ->
//     let tempDocsDir = "temp/gh-pages"
//     CleanDir tempDocsDir
//     Repository.cloneSingleBranch "" (gitHome + "/" + gitName + ".git") "gh-pages" tempDocsDir

//     Repository.fullclean tempDocsDir
//     CopyRecursive "docs/output" tempDocsDir true |> tracefn "%A"
//     StageAll tempDocsDir
//     Commit tempDocsDir (sprintf "Update generated documentation for version %s" release.NugetVersion)
//     Branches.push tempDocsDir
// )

// Target "Release" DoNothing

// Target "All" DoNothing

// // --------------------------------------------------------------------------------------
// // Run 'Build' target by default. Invoke 'build <Target>' to override

// "Clean" ==> "RestorePackages" ==> "AssemblyInfo" ==> "Build"
// "AssemblyInfo" ==> "BuildTest" ==> "RunTests" 
// "CleanDocs" ==> "GenerateDocs" ==> "ReleaseDocs" 
// "Build" ==> "RunTests" ==> "GenerateDocs" ==> "All"
// "RunTests" ==> "NuGet" ==> "Release"

// RunTargetOrDefault "Build"
