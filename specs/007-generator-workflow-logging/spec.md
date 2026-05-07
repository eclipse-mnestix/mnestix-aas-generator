# Feature Specification: Generator Workflow Logging

**Feature Branch**: `007-generator-workflow-logging`  
**Created**: 2026-04-23  
**Status**: Draft  
**Input**: User description: "Enhancement on the logging inside the AAS generator. Currently only the DataMapper uses logs for context. The whole workflow in AddDataToAasAsync should be saved in the log object, that gets returned if an error occurs or the debug flag was sent in the request."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Full Workflow Logs on Debug Request (Priority: P1)

As an API consumer, I want to receive a complete log trail of every step in the AAS generation workflow when I set the debug flag, so that I can trace exactly what happened during blueprint fetching, ID generation, data mapping, and repository persistence for each blueprint.

**Why this priority**: This is the core ask — currently logs only cover the data mapping step. Extending logging across the full workflow provides the greatest diagnostic value and enables faster root-cause analysis when integrating new blueprints or data sources.

**Independent Test**: Can be fully tested by sending a request with `debug=true` and verifying the response contains chronological log entries for every workflow phase (blueprint retrieval, ID generation, data mapping, repository persistence).

**Acceptance Scenarios**:

1. **Given** a valid AAS generation request with `debug=true`, **When** the generation completes successfully for a blueprint, **Then** the response includes log entries for: blueprint retrieval start/end, submodel ID generation, data mapping start/end, and repository persistence start/end.
2. **Given** a valid AAS generation request with `debug=true` and multiple blueprint IDs, **When** the generation completes, **Then** each blueprint result contains its own independent set of workflow logs.
3. **Given** a valid AAS generation request with `debug=false` (or debug omitted), **When** the generation completes successfully, **Then** no debug log information is included in the response.

---

### User Story 2 - Workflow Logs on Error (Priority: P1)

As an API consumer, I want to see all workflow log entries accumulated up to the point of failure when an error occurs during AAS generation, so that I can understand exactly which step failed and what succeeded before it.

**Why this priority**: Equal priority to P1 because error diagnostics are the most critical use case for logging — users need the log trail precisely when things go wrong.

**Independent Test**: Can be tested by triggering failures at each workflow stage (invalid blueprint ID, ID generation failure, mapping error, repository error) and verifying the response contains all log entries up to and including the failure.

**Acceptance Scenarios**:

1. **Given** a request where blueprint retrieval fails, **When** the error result is returned, **Then** the result includes a log entry showing the blueprint retrieval attempt and failure reason.
2. **Given** a request where submodel ID generation fails, **When** the error result is returned, **Then** the result includes log entries showing successful blueprint retrieval followed by the ID generation failure.
3. **Given** a request where data mapping fails, **When** the error result is returned, **Then** the result includes log entries for successful blueprint retrieval and ID generation, followed by the mapping failure. Existing DataMapper logs are preserved within the overall workflow log.
4. **Given** a request where repository persistence fails, **When** the error result is returned, **Then** the result includes log entries for all preceding successful steps followed by the persistence failure.

---

### User Story 3 - Consistent Log Format Across Workflow Steps (Priority: P2)

As an API consumer, I want all workflow log entries to follow a consistent format with severity level and timestamp, so that I can parse and filter them programmatically regardless of which workflow step produced them.

**Why this priority**: Consistency makes logs machine-parseable and human-readable. This supports tooling and automated monitoring but is secondary to actually having the logs.

**Independent Test**: Can be tested by sending a debug request and validating that every log entry in the response follows the established format pattern (severity, timestamp, message).

**Acceptance Scenarios**:

1. **Given** a debug response with workflow logs, **When** inspecting any log entry, **Then** each entry contains a severity level, a UTC timestamp, and a descriptive message.
2. **Given** workflow logs that include entries from both the data mapping pipeline and other workflow steps, **When** reading the logs, **Then** all entries use the same format convention.

---

### Edge Cases

- What happens when a blueprint retrieval succeeds but returns unexpectedly fast (cached)? Logs should still record the retrieval step.
- How does the system handle concurrent blueprint processing? Each blueprint's log trail must be isolated — no interleaving of entries across blueprints.
- What happens when the debug flag is true but the workflow fails on the very first step? The response should still contain the partial log (at minimum the attempt entry).
- What if the log accumulation itself causes a memory issue with very large payloads? Log entries should be bounded to reasonable string lengths.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST record a log entry at the start and completion (or failure) of each workflow phase: blueprint retrieval, submodel ID generation, data mapping, and repository persistence.
- **FR-002**: System MUST include the accumulated workflow logs in the response for each blueprint when the `debug` flag is `true`, regardless of whether the generation succeeded or failed.
- **FR-003**: System MUST include the accumulated workflow logs in the error information for each blueprint when an error occurs, regardless of the `debug` flag value.
- **FR-004**: System MUST maintain a separate log trail per blueprint processed in a single request — logs from one blueprint's workflow MUST NOT appear in another blueprint's result.
- **FR-005**: System MUST preserve existing DataMapper pipeline logs as part of the overall workflow log trail, maintaining their original order and content.
- **FR-006**: System MUST use a consistent log entry format across all workflow phases, including severity level and UTC timestamp.
- **FR-007**: System MUST NOT include workflow logs in successful responses when the `debug` flag is `false` or omitted.

### Key Entities

- **Workflow Log Trail**: An ordered collection of log entries produced during the processing of a single blueprint within a request. Spans all workflow phases.
- **Log Entry**: A single record containing severity (info, warning, error), UTC timestamp, and descriptive message text.
- **Workflow Phase**: One of the discrete steps in the generation pipeline — blueprint retrieval, ID generation, data mapping, repository persistence.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Every successful debug response contains log entries from all four workflow phases (blueprint retrieval, ID generation, data mapping, repository persistence) for each processed blueprint.
- **SC-002**: Every error response contains log entries for all phases that executed up to and including the point of failure.
- **SC-003**: 100% of log entries across all workflow phases follow the same format convention (severity, timestamp, message).
- **SC-004**: Existing DataMapper log entries remain present and unmodified in the workflow log trail when the debug flag is enabled.
- **SC-005**: No workflow log data appears in responses when the debug flag is `false` and the generation succeeds.

## Assumptions

- The existing log entry format (`SEVERITY [timestamp] - message`) used by the DataMapper context is the established convention and will be followed by the new workflow-level logging.
- The existing API contract (request/response DTOs) can be extended without breaking backward compatibility — the `DebugInfo` and `ErrorInfo` structures already support log lists.
- Workflow logging is scoped to the `AddDataToAasAsync` method and its direct sub-operations; it does not extend to upstream API middleware or authentication.
- Log entries are human-readable text strings, not structured/JSON log objects. Structured logging may be considered in a future enhancement.
- Performance overhead of in-memory log accumulation is negligible for typical request sizes (1–20 blueprints per request).
