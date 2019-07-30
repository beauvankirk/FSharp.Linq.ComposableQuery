# FSharpComposableQuery [![NuGet Status](http://img.shields.io/nuget/v/FSharpComposableQuery.svg?style=flat)](https://www.nuget.org/packages/FSharpComposableQuery/)

A Compositional Query Framework for F# Queries, based on ["A Practical Theory of Language-Integrated Query" (ICFP 2013)](http://dl.acm.org/citation.cfm?id=2500586)


##Build

If you want to use the library simply proceed to the Installation section.

### Prerequisites
 - Visual Studio 2012 or later. 


To build, open a command prompt or a terminal window and type

    build
to execute the build script for your system (tested only under Windows). 

The default build target does not execute any unit tests since they require the presence of a properly configured SQL Server database. 
You can follow the instructions in the README.md file [here](tests/FSharpComposableQuery.Tests) on setting up the testing environment. 

If you receive a `File does not exist` error this may indicate you do not have MSBuild in your console path. In such a case do one of the following:
 - Open the VS Command Prompt from its shortcut or from inside Visual Studio
 - Add the respective directories where the executable lies manually to your PATH variable.  


## Installation

You can find the library on [NuGet](https://www.nuget.org/packages/FSharpComposableQuery).


## Usage

Check out the [tutorial](http://fsprojects.github.io/FSharp.Linq.ComposableQuery/) for examples and an overview of the main use cases of this library. 


## Maintainer(s)

- [@ixtreon](https://github.com/ixtreon)
- [@jamescheney](https://github.com/jamescheney)

The default maintainer account for projects under "fsprojects" is [@fsprojectsgit](https://github.com/fsprojectsgit) - F# Community Project Incubation Space (repo management)
