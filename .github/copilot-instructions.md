# Agent Instructions

## Type Definitions
* The Bicep type definitions under `./types` are vendored verbatim from [Azure/bicep-types-k8s](https://github.com/Azure/bicep-types-k8s). **Do not hand-edit them.** To move to a different Kubernetes version, run `./scripts/sync-types.ps1 -KubernetesVersion <version>`.
* The types are embedded into the assembly at build time and served at runtime by `EmbeddedTypeDefinitionBuilder`, which rewrites only the extension name/version and swaps the upstream configuration type for the `Configuration` class in `./src/Bicep.Extension.Kubernetes/Models/Models.cs`.
* Kubernetes' `IntOrString` fields (e.g. `targetPort`, `minAvailable`, `httpGet.port`) are modelled upstream as `string`. In Bicep they must be given a string value - use a named port or a percentage rather than a number.

## Model Authoring
* Bicep model classes should follow standard C# naming conventions, with properties defined using PascalCase. This ensures the types shown in Bicep are using camelCase, even if the underlying API follows a different convention (e.g. snake_case).

## Iterating
To iterate on changes to the extension (e.g. changing the configuration or handler behaviour), use the following flow:
* Make changes
* Build the solution using `dotnet build .`. This'll typically run a lot faster for catching C# errors than runnig a full publish.
* Run `./scripts/publish.ps1 ./bicep-ext-kubernetes` to publish the self-contained extension to `./bicep-ext-kubernetes`.
* Modify `./samples/bicepconfig.json` to use the local extension instead of the one from the registry. For example:
    ```json
    {
      "experimentalFeaturesEnabled": {
        "localDeploy": true
      },
      "extensions": {
        "kubernetes": "../bicep-ext-kubernetes"
      },
      "implicitExtensions": []
    }
    ```
* Check the samples for warnings or errors by running `bicep lint --pattern './samples/**/*.bicepparam'`. Note that the error code BCP427 is expected, as expected env variables are not set - ignore this.

To verify that the extension actually works end-to-end, ask the user to select a sample to run, and be very clear that this will actually interact with the external environment, and can potentially be destructive. If running into errors, it should be possible to troubleshoot by turning on verbose tracing with the env var `BICEP_TRACING_ENABLED` set to `true`.

After making changes, if relevant, add or update samples.
