## Status
[![Build status](https://ci.appveyor.com/api/projects/status/5h20vfal8gu0pb5y/branch/master?svg=true)](https://ci.appveyor.com/project/eventphone/yate-net/branch/master)
![tests](https://img.shields.io/badge/tests-none-red.svg)
![coverage](https://img.shields.io/badge/coverage-0%25-red.svg)
![maintainability](https://img.shields.io/badge/maintainability-F-red.svg)
![nuget](https://img.shields.io/badge/nuget-none-red.svg)

Very early unusable, broken, buggy, not recommended, dev-only pre-alpha. This code was never used in any even nearly productive environment.
You WILL destroy and/or loose all systems this code can reach.

## What?
This project is currently a partial dotnet core port of [yate-tcl](https://github.com/bef/yate-tcl).

## Why?
I'm way better in C# than in tcl.

The idea is to create a fully blown yate client library for all fancy interactions with yate.

## How?
The best way to get an idea is to take a look at the [ystatus](src/ystatus/Program.cs) or [ystatus.redis](src/ystatus.redis/Program.cs) implementation.