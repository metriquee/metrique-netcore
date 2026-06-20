# Notes to Deploy

Using the terminal. Build in Release (this also packs the `.nupkg` via `GeneratePackageOnBuild`),
then push. Supply the API key from an environment variable or CI secret — never inline it.

```bash
dotnet build -c Release
dotnet nuget push "Metriquee.NetCore\bin\Release\Metriquee.NetCore.*.nupkg" \
  --api-key "$NUGET_API_KEY" --source <your-nuget-source> --skip-duplicate
```

> The API key that used to live in this file is exposed in git history — rotate it.
