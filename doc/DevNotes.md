# Developer Notes

## LINQPad

Documentation about writing a custom data context driver for LINQPad:  
<https://www.linqpad.net/Extensibility.aspx>

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
