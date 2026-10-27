# Third-party notices

Sunny Bar source is MIT licensed; see LICENSE.

The win-x64 self-contained package includes Microsoft .NET 10 runtime components
(.NET runtime, WPF and Windows Forms). Their license and third-party notices are
copied unmodified from the official runtime packs into licenses/ during packaging.
Those notices govern the bundled components; Sunny Bar's license does not replace them.

The application adds no third-party runtime NuGet packages. Solar calculations are
implemented locally from published mathematical relationships, with sources and
accuracy limits documented in SOLAR_CALCULATION.md. The sun/moon/marker artwork
is drawn by the application's own code; Emoji glyphs use the installed system font.

Development/test dependencies (not included as test tooling in the portable app):
- xUnit 2.9.3: Apache-2.0
- xunit.runner.visualstudio 3.1.4: Apache-2.0
- Microsoft.NET.Test.Sdk 17.14.1 and Microsoft test platform: MIT
- Their transitive development dependencies retain their respective licenses.

The build does not distribute Windows system fonts or system libraries separately.
