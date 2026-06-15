namespace MnestixCore.AasCreator;

/// <summary>
/// Outcome of an AAS creation attempt. Maps to HTTP status in the controller.
/// </summary>
public enum AasCreationStatus
{
    /// <summary>A new shell was created. → 201.</summary>
    Created,

    /// <summary>An existing shell was replaced because overwrite=true. → 200.</summary>
    Overwritten,

    /// <summary>A shell with the generated id already exists and overwrite=false. → 409.</summary>
    Conflict,

    /// <summary>Shell already exists, reported by the pre-check in the shell-only creation path. → 400.</summary>
    AlreadyExists,

    /// <summary>Submodel generation or input validation failed; no AAS was created. → 400.</summary>
    GenerationFailed,

    /// <summary>An unexpected infrastructure/persistence failure. → 500.</summary>
    UnknownError
}