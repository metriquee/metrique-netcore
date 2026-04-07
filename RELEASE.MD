# Notes to Deploy

using terminal

```
dotnet build -c Release
dotnet nuget push "MetriqueePackage\bin\Release\*.nupkg" --api-key 8356bc19-faf6-3798-b6c5-a8159a2895c5 --skip-duplicate
```