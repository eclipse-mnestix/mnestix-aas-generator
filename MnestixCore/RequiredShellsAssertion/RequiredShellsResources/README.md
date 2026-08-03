# Required Shells Resources

This folder contains the required AAS for a repo.

During the startup of the application, the required AAS will be added to the repo,
if the respective AAS doesn't exist.
Additionally the submodels for each AAS will be added or overwritten in the repo,
unless the `SkipIfAlreadyExists` flag is set for the AAS in the `appsettings.json`.
This is done, to be able to add or update submodels of existing AAS.

If `Features__AddExampleAas` is set to `false`, the demo/example AAS (`lni0729`, `Mnestix`)
are skipped. `Configuration`, `DefaultTemplate` and `CustomTemplate` are always checked
regardless of this flag.

## Rules

AAS

1. Each AAS is placed in their own directory.
2. The directory name SHOULD BE a speaking name of the AAS.
3. Each AAS directory MUST CONTAIN exactly one AAS json file.
4. The name of the AAS json file MUST CORRESPOND to the base64 encoded id of the respective AAS.
5. If the created AAS should not show up in the AAS List, add a specificAssetId with the name `aasListFilterId`

Submodel

1. The submodels of an AAS MUST BE placed in a _Submodels_ directory within the corresponding AAS directory.
2. Each submodel MUST BE stored as a separate json file.
3. The name of the submodel json file MUST CORRESPOND to the short id of the respective AAS.

**Hint:** When adding a new AAS or submodel, don't forget to add it to the `appsettings.json`.

**Hint:** The submodel `DefaultTemplate/Submodels/aHR0cHM6Ly9tbmVzdGl4LmNvbS9zbS9UZW1wbGF0ZUJ1aWxkZXJEZW1vLzEvMA==.json` is not added to the AAS, as it just meant to be used for testing purposes.
