# AAS Generator - Rules Engine & Pipeline Architecture

The AAS Generator (`MnestixCore/AASGenerator/`) is a specialized component within the Mnestix Backend that enables automated creation of AAS Submodels from structured JSON data using template-based rules. This component was developed as part of Luis Schweinberger's Bachelor thesis on rules engines for Asset Administration Shells ([see thesis on GitHub](https://github.com/XITASO/thesis-automatic-generation-aas)).

## Purpose & Scope

The AAS Generator solves the Industry 4.0 challenge of transforming existing structured data into standardized digital twins (AAS). Instead of manual AAS creation, it provides:

- **Automated Transformation**: JSON data → AAS Submodel instances
- **Template-Based Rules**: Embedded mapping rules within AAS templates
- **Complex Mappings**: Beyond 1:1 field mapping (collections, filtering, conditions)
- **AAS Compliance**: Generated Submodels conform to AAS Metamodel v3.x

## Component Architecture

### Entry Points
- **Main Interface**: `IAasGenerator` (`AasGenerator.cs:23`) - Primary service interface
- **REST Endpoint**: `POST /api/v1/DataIngest` - HTTP API for generation requests
- **Integration**: Called by other Mnestix components for automated Submodel creation

### Core Classes
- **AasGenerator**: Orchestrates the entire generation process
- **SubmodelDataToInstanceMapper**: Coordinates the transformation pipeline
- **BlueprintProvider**: Manages template storage and retrieval
- **Pipeline Steps**: Individual transformation operations (6 steps)

### Pipeline Processing Architecture
Uses Pipes-and-Filters pattern (`MnestixCore/Shared/Pipeline/`) with 6 sequential steps:

1. **DeepCloneTemplate** - Creates working copy (`DeepCloneTemplateStep.cs:15`)
2. **SetKindInstance** - Changes Template to Instance (`SetKindInstanceStep.cs:12`) 
3. **DuplicateCollections** - Processes arrays/lists (`DuplicateCollectionsStep.cs:25`)
4. **FilterElements** - Removes elements failing filter conditions (`FilterElementsStep.cs:15`)
5. **MapDataToInstance** - Maps JSON data to elements using Jsonata expressions (`MapDataToInstanceStep.cs:20`)
6. **RemoveTopLevelQualifiers** - Cleans template metadata (`RemoveTopLevelQualifiersStep.cs:18`)
7. **ReplaceIdentification** - Assigns new Submodel ID (`ReplaceIdentificationStep.cs:14`)

**Context Object**: `SubmodelMappingContext` carries immutable inputs and mutable state through all steps

**Extensibility**: New rule types implement `IPipelineStep<TContext>` and register in `PipelineBuilder`

### Pipeline Pattern Implementation

The AAS Generator uses the Pipes-and-Filters pattern based on Buschmann et al.'s design:

```
Template Input → [Step1] → [Step2] → [Step3] → ... → [StepN] → Submodel Instance
                   │         │         │              │
                   ▼         ▼         ▼              ▼
                Context   Context   Context       Context
```

**Benefits:**
- Clear separation of concerns per transformation step
- Easy extensibility for new rule types
- Robust error handling with context preservation
- Individually testable components

**Pipeline Builder Pattern**: Steps registered fluently in `SubmodelDataToInstanceMapper.cs:10-17`

### Data Flow Through Pipeline

1. **Input**: Template (JObject), Data (JObject), Language (string), NewSubmodelId (string)
2. **Context Creation**: `SubmodelMappingContext` initialized with inputs
3. **Sequential Processing**: Each step modifies context and passes to next
4. **Output**: Generated Submodel instance in context.SubmodelInstance
5. **Error Handling**: Pipeline halts on first error, preserves full context

## Rule System

Rules are stored as Template Qualifiers directly within AAS Submodel templates.

**Template Qualifier Format:**
```json
{
  "type": "SMT/<RuleType>",
  "value": "<rule-configuration>"
}
```

## Rule Types

### 1. Default Values (Static)
**Purpose**: Set static values directly in templates  
**Qualifier**: None required - values set directly in template element  
**Result**: Value copied unchanged to instance

### 2. Path Rules (Dynamic Values & Jsonata Expressions)
**Purpose**: 1:1 mapping from JSON paths OR advanced Jsonata expressions to element values  
**Qualifier**: `SMT/MappingInfo`  
**Examples**:
  - Simple path: `"value": "car.serialNo"` maps `data.car.serialNo` to element value
  - String function: `"value": "$uppercase(car.code)"` transforms to uppercase
  - Numeric conversion: `"value": "$string(quantity)"` converts number to string
  - Boolean expression: `"value": "car.price > 1000"` returns true/false
  - Chained operations: `"value": "car.email ~> $substringAfter('@')"` extracts domain
**Implementation**: `MapDataToInstanceStep.cs:45-78` (uses Jsonata.Net.Native library)  
**Jsonata Reference**: See [Blueprint and Rules](Blueprint-and-Rules#jsonata-expressions-in-mapping-rules) for complete function list

### 3. Collection Rules (List/Array Processing)
**Purpose**: Duplicate elements for each array item  
**Qualifier**: `SMT/CollectionMappingInfo`  
**Example**: `"value": "car.contacts[*]"` creates N elements for N contacts  
**Algorithm**: Recursive processing, shallowest-first, replaces `[*]` with indices  
**Implementation**: `DuplicateCollectionsStep.cs:35-120` (see algorithm comments)  
**Result**: `contactPerson_0`, `contactPerson_1`, etc. with mapped child values

### 4. Filter Rules (Conditional Creation)
**Purpose**: Create elements only when conditions are met  
**Qualifier**: `SMT/FilterMappingInfo`  
**Status**: ✅ **Implemented** - Uses Jsonata boolean expressions  
**Example**: `"value": "car.engineType = 'electric'"` creates element only for electric cars  
**Implementation**: `FilterElementsStep.cs:15-95`  
**Syntax**: Supports Jsonata boolean operators (=, !=, >, <, >=, <=, and, or, in)

### 5. Cardinality Rules (Optional/Mandatory)
**Purpose**: Define behavior when referenced data is missing  
**Qualifier**: `SMT/Cardinality`  
**Values**: `"One"` (mandatory, throws error) | `"ZeroToOne"` (optional, empty value)  
**Implementation**: Checked in `MapDataToInstanceStep.cs:65-72`

## Path Expressions
JSONata-style syntax:
- `data.field` - Simple access
- `data.nested.field` - Nested objects  
- `data.array[*]` - Array placeholder (collections)
- `data.array[0]` - Specific index (after processing)

## Jsonata Expression Support

The AAS Generator includes comprehensive Jsonata expression support for advanced data transformations beyond simple path navigation.

### Supported in Path Mapping (`SMT/MappingInfo`)

**String Functions:**
- `$length(str)` - Character count
- `$substring(str, start, length)` - Extract substring
- `$contains(str, pattern)` - Check if contains (returns boolean)
- `$uppercase(str)`, `$lowercase(str)` - Case conversion
- `$trim(str)` - Remove whitespace
- `$split(str, sep)`, `$join(array, sep)` - String/array operations
- `$replace(str, old, new)` - Text replacement

**Numeric Functions:**
- `$number(value)` - Convert to number
- `$string(value)` - Convert to string
- `$abs(num)`, `$floor(num)`, `$ceil(num)` - Math functions
- `$round(num, precision)` - Rounding
- `$power(base, exp)`, `$sqrt(num)` - Advanced math

**Comparison & Boolean Operations:**
- `=`, `!=`, `>`, `<`, `>=`, `<=` - Comparisons (return boolean)
- `and`, `or` - Logical operators
- `in` - Array membership

**Pipe Operator:**
- `data.value ~> $function($)` - Pass result to next function

### Supported in Filter Rules (`SMT/FilterMappingInfo`)

**Boolean Expressions:**
- `field = 'value'` - Equality check
- `field != 'value'` - Inequality check
- `numA > numB` - Numeric comparison
- `field and otherfield` - Logical AND
- `field or otherfield` - Logical OR
- `value in ['a', 'b', 'c']` - Array membership

### Examples

**String Transformation:**
```json
{
  "type": "SMT/MappingInfo",
  "value": "$substring(code, 0, 3) ~> $uppercase($)"
}
```
Input: `"code": "abc123"` → Output: `"ABC"`

**Type Conversion:**
```json
{
  "type": "SMT/MappingInfo",
  "value": "$string(quantity)"
}
```
Input: `"quantity": 42` → Output: `"42"`

**Boolean Check:**
```json
{
  "type": "SMT/MappingInfo",
  "value": "email ~> $contains('@')"
}
```
Input: `"email": "user@example.com"` → Output: `true`

**Filter Expression:**
```json
{
  "type": "SMT/FilterMappingInfo",
  "value": "vehicle.engineType = 'electric' and vehicle.year >= 2020"
}
```
Element created only if both conditions are true

## Error Handling
- **Missing mandatory data**: `SubmodelDataToInstanceMapperException` with context (`SubmodelMappingContext.cs:24`)
- **Missing optional data**: Empty value assignment
- **Structured errors**: Include qualifier, path, and processing context

## Workflow Logging

The AAS Generator includes a workflow-level logging system that captures a chronological log trail across the entire Submodel generation lifecycle. This provides full observability into the generation process for debugging and error diagnosis.

### WorkflowLogger

`WorkflowLogger` (`MnestixCore/AASGenerator/WorkflowLogger.cs`) is a lightweight dual-write logger that:
1. **Accumulates** log entries in an `IList<string>` for inclusion in API responses
2. **Forwards** each entry to the injected `ILogger` at the appropriate log level

A new `WorkflowLogger` instance is created per blueprint in `AddDataToAasAsync`, ensuring each blueprint has an independent log trail.

### Log Format

All entries follow the convention: `SEVERITY [timestamp] - message` where timestamps use the ISO-8601 round-trip format (`"O"` specifier):

```
INFO [2026-04-24T10:30:01.0000000Z] - Mapping blueprint contact-template-v1 to AAS aHR0cHM6...
INFO [2026-04-24T10:30:01.1000000Z] - Fetching blueprint: contact-template-v1
INFO [2026-04-24T10:30:01.2000000Z] - Blueprint fetched successfully
ERROR [2026-04-24T10:30:01.3000000Z] - Data mapping failed: ...
```

This matches the format used by the existing `DataMappingContext` pipeline logs, so all log entries are consistent when merged.

### Logged Workflow Phases

Each phase of `AddDataToAasAsync` is instrumented:

| Phase | Log Entries |
|-------|------------|
| **Context Preamble** | Optional caller-provided preamble (e.g. `Created a new AAS with aasId {aasId}` from AasCreator), then `Mapping blueprint {id} to AAS {aasId}` |
| **Blueprint Retrieval** | `Fetching blueprint: {id}`, `Blueprint fetched successfully` |
| **IdShort Extraction** | `Extracted idShort: {idShort}` |
| **ID Generation** | `Generating submodel ID`, `Submodel ID generated: {id}` |
| **Data Mapping** | `Starting data mapping`, pipeline step logs (merged via `AddRange`), `Data mapping completed` |
| **Repository Persistence** | `Posting submodel to repository`, `Adding submodel reference to shell`, `Submodel reference added to shell` |

### Log Inclusion in API Responses

- **`debug=true` + success**: `DebugInfo.Logs` contains the full log trail from all phases
- **`debug=false` + success**: `DebugInfo` is `null` (no logs returned)
- **Error (any `debug` value)**: `ErrorInfo.Logs` always contains the log trail up to and including the failure point — this aids error diagnosis without requiring the caller to opt into debug mode

## Current Limitations
1. **SubmodelElementList**: Partial support  
2. **MultiLanguageProperty**: Single language only per generation call
3. **Template Qualifiers**: Not fully removed from instances
4. **Complex expressions**: Advanced Jsonata features (aggregation, conditionals) not fully supported

## Usage Example

### Basic Generation Request
```http
POST /api/v1/DataIngest
{
  "aasId": "base64-encoded-aas-id",
  "blueprintIds": ["contact-template-v1"],
  "data": {
    "contacts": [
      {"name": "John Doe", "email": "john@example.com"}
    ]
  },
  "language": "en"
}
```

### Template Creation
Templates are AAS Submodels with `kind: "Template"` and embedded Template Qualifiers. Created via Template Builder UI or direct API.

### Drawbacks with MultiLanguageProperties

_If you move this file or change this section, please also update the comment in `MnestixCore/AASGenerator/SubmodelDataToInstanceMapper/Steps/MapDataToInstanceStep.cs`_

The current implementation supports the creation of data in MLP, however only one language can be used at a time which makes the feature kind of useless in my opinion. We either need to allow the editing of existing SubModels so multiple Generation calls can be done (one for each language) or change the logic so multiple languages can be created at the same time. 

Ideas:

- treating the different languages as properties in a collection.
- creating a new Qualifer-Type and enforcing a certain structure (e.g. `[{key: value}*]` with key being based on [BCP 47 language tags](https://en.wikipedia.org/wiki/IETF_language_tag))