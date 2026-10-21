# Third-party notices

## TokenTray

SGB Limit is derived from [TokenTray](https://github.com/emrecengdev/tokentray), originally authored by Emre Canik and distributed under the MIT License.

Copyright (c) 2026 Emre Canik

The original copyright and MIT permission notice are preserved in [LICENSE](LICENSE). SGB Limit contributions by salihbyk are also distributed under that license. Internal `TokenTray` project and namespace names remain in the source.

Provider integrations, polling infrastructure, the quota/core foundation, original taskbar renderers and geometric provider marks build on the upstream project. SGB Limit contributions include the Dynamic Island, circular floating redesign, adaptive taskbar layout, Signal display, Turkish panels and progressive settings, with further integration and stale-reading changes. These additions do not imply that the inherited components were independently authored for SGB Limit.

## .NET runtime and build dependencies

The Windows x64 self-contained package includes the Microsoft .NET 10 runtime. Its own license and third-party notices must accompany the runtime in the release package. The repository's MIT license does not replace those notices. Runtime notices are available from the installed Microsoft.NETCore.App and Microsoft.WindowsDesktop.App runtime packs and the corresponding .NET distribution.

The test project uses Microsoft.NET.Test.Sdk, xUnit, xunit.runner.visualstudio and coverlet.collector. These are development/test dependencies. Their own licenses apply. See the package references in `tests/TokenTray.Tests/TokenTray.Tests.csproj` and the notices included with each package.

## Provider names and artwork

Provider names identify the corresponding services. The geometric symbols drawn in `src/TokenTray/ProviderMark.cs` build on the upstream TokenTray implementation. They are interface identifiers, not a claim to ownership of official provider logos. Neither those symbols nor the use of provider names implies official affiliation, sponsorship or endorsement by a provider or the upstream author. Third-party trademarks remain the property of their respective owners.

## Documentation images

Interface screenshots intended for public documentation use synthetic account and quota data. They show the application's native WPF interface.

Laptop presentation images place native interface captures into a mockup context. They do not establish testing on the pictured hardware or make a hardware compatibility claim.

If an AI-generated wallpaper is included in the repository or documentation images, the repository owner makes their available rights in that asset available under the MIT License. This is not a claim of exclusive copyright in AI-generated material. Provider marks and other third-party rights are not relicensed by that statement.
