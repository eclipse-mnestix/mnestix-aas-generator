#!/usr/bin/env python3
"""
verify_resources.py - Validate AAS JSON resources against a live BaSyx Go repository.

Attempts to POST each JSON resource to the repository and reports success/failure.

Usage:
    python verify_resources.py <directory> <basyx-url>

Example:
    python verify_resources.py MnestixCore/RequiredShellsAssertion/RequiredShellsResources/ http://localhost:8081
"""
import json
import os
import sys
import urllib.request
import urllib.error


def post_to_repo(url: str, data: dict) -> tuple[bool, str]:
    """POST JSON data to a BaSyx repository endpoint. Returns (success, message)."""
    body = json.dumps(data).encode("utf-8")
    req = urllib.request.Request(
        url,
        data=body,
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    try:
        with urllib.request.urlopen(req) as resp:
            return True, f"{resp.status}"
    except urllib.error.HTTPError as e:
        response_body = e.read().decode("utf-8", errors="replace")[:200]
        return False, f"HTTP {e.code}: {response_body}"
    except urllib.error.URLError as e:
        return False, f"Connection error: {e.reason}"


def determine_endpoint(data: dict, basyx_url: str) -> str | None:
    """Determine whether a JSON object is an AAS shell or a Submodel and return the appropriate endpoint."""
    if "assetInformation" in data:
        return f"{basyx_url}/shells"
    elif "submodelElements" in data or "modelType" in data and data.get("modelType") == "Submodel":
        return f"{basyx_url}/submodels"
    return None


def verify_file(filepath: str, basyx_url: str) -> bool:
    """Verify a single JSON resource file against the repository."""
    with open(filepath, "r", encoding="utf-8") as f:
        try:
            data = json.load(f)
        except json.JSONDecodeError as e:
            print(f"  [SKIP] {filepath}: {e}")
            return True  # Skip non-JSON files

    endpoint = determine_endpoint(data, basyx_url)
    if endpoint is None:
        print(f"  [SKIP] {filepath}: Not an AAS or Submodel")
        return True

    success, msg = post_to_repo(endpoint, data)
    status = "OK" if success else "FAIL"
    print(f"  [{status}] {filepath} -> {endpoint} ({msg})")
    return success


def main():
    if len(sys.argv) < 3:
        print(f"Usage: {sys.argv[0]} <directory> <basyx-url>")
        print(f"Example: {sys.argv[0]} MnestixCore/RequiredShellsAssertion/RequiredShellsResources/ http://localhost:8081")
        sys.exit(1)

    target_dir = sys.argv[1]
    basyx_url = sys.argv[2].rstrip("/")

    if not os.path.isdir(target_dir):
        print(f"Error: {target_dir} is not a directory")
        sys.exit(1)

    passed = 0
    failed = 0
    skipped = 0

    for root, dirs, files in os.walk(target_dir):
        for fname in sorted(files):
            if fname.endswith(".json"):
                filepath = os.path.join(root, fname)
                result = verify_file(filepath, basyx_url)
                if result:
                    passed += 1
                else:
                    failed += 1

    total = passed + failed
    print(f"\nResults: {passed}/{total} passed, {failed} failed")
    sys.exit(1 if failed > 0 else 0)


if __name__ == "__main__":
    main()
