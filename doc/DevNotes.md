# Developer Notes

## LINQPad

Documentation about writing a custom data context driver for LINQPad:  
<https://www.linqpad.net/Extensibility.aspx>

LINQPad caches drivers in `%LocalAppData%\LINQPad\Drivers\DataContext`
and NuGet drivers in `%LocalAppData%\LINQPad\NuGet.Drivers`. LINQPad
also uses the Windows NuGet cache in `%UserProfile%\.nuget\packages`.
During development it may be useful to remove the driver from any or
all of these locations.

A driver deployed as a NuGet package can **include samples** that LINQPad
will show on its Samples tab: simply add `*.linq` files (stored LINQPad
queries) in a top-level `linqpad-samples` folder to the NuGet package and
the `linqpad-samples` package tag to its metadata.
Documentation is at <https://www.linqpad.net/nugetsamples.aspx>.
In the `.csproj` it could look like this:

```xml
...
<PackageTags>linqpaddriver;linqpad-samples;...</PackageTags>
...
<None Include="linqpad-samples/*.linq" Pack="True" PackagePath="linqpad-samples" />
...
```

## NuGet

Including symbols with NuGet:  
<https://learn.microsoft.com/en-us/nuget/create-packages/symbol-packages-snupkg>

Including a README in NuGet:  
<https://devblogs.microsoft.com/nuget/add-a-readme-to-your-nuget-package/>

NuGet has a test instance (packages not preserved)
at <https://int.nugettest.org/> with feed URL
<https://apiint.nugettest.org/v3/index.json>
