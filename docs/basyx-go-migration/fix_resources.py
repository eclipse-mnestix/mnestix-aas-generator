#!/usr/bin/env python3
"""
fix_resources.py - Rewrite legacy AAS v2 JSON resources for BaSyx Go v3 compliance.

Rules applied:
  1. Strip UTF-8 BOM
  2. Strip null-valued properties recursively
  3. Remove deprecated fields: dataSpecification, hasDataSpecification
  4. Strip v2 Key fields: local, idType, index
  5. Strip v2 element fields: parent, allowDuplicates, ordered, asset (when null)
  6. Inject "type": "ExternalReference" on References missing it (semanticId, etc.)
  7. Coerce numeric/boolean Property.value to strings
  8. Wrap single-object MultiLanguageProperty.value in arrays
  9. Normalize valueType to canonical XSD case
 10. Rename duplicate {arbitrary} idShort to {arbitraryProperty}

Usage:
    python fix_resources.py <directory> [--dry-run]
"""
import json
import os
import sys
import codecs

V2_KEY_FIELDS = {"local", "idType", "index"}
V2_ELEMENT_FIELDS = {"parent", "allowDuplicates", "ordered", "hasDataSpecification", "dataSpecification"}

VALUETYPE_CANONICAL = {
    "xs:string": "xs:string", "xs:boolean": "xs:boolean",
    "xs:integer": "xs:integer", "xs:int": "xs:int", "xs:long": "xs:long",
    "xs:short": "xs:short", "xs:decimal": "xs:decimal", "xs:double": "xs:double",
    "xs:float": "xs:float", "xs:datetime": "xs:dateTime", "xs:date": "xs:date",
    "xs:time": "xs:time", "xs:anyuri": "xs:anyURI", "xs:base64binary": "xs:base64Binary",
    "xs:hexbinary": "xs:hexBinary", "xs:byte": "xs:byte",
    "xs:unsignedbyte": "xs:unsignedByte", "xs:unsignedshort": "xs:unsignedShort",
    "xs:unsignedint": "xs:unsignedInt", "xs:unsignedlong": "xs:unsignedLong",
    "xs:positiveinteger": "xs:positiveInteger", "xs:nonnegativeinteger": "xs:nonNegativeInteger",
    "xs:negativeinteger": "xs:negativeInteger", "xs:nonpositiveinteger": "xs:nonPositiveInteger",
    "xs:duration": "xs:duration", "xs:gday": "xs:gDay", "xs:gmonth": "xs:gMonth",
    "xs:gmonthday": "xs:gMonthDay", "xs:gyear": "xs:gYear", "xs:gyearmonth": "xs:gYearMonth",
}

REFERENCE_FIELDS = {"semanticId", "supplementalSemanticIds", "derivedFrom"}


def fix_node(node, parent_key=None, seen_idshorts=None):
    """Recursively fix a JSON node in-place and return it."""
    if isinstance(node, dict):
        # Remove v2 fields
        for field in list(node.keys()):
            if field in V2_KEY_FIELDS or field in V2_ELEMENT_FIELDS:
                del node[field]
            elif node[field] is None:
                del node[field]

        # Strip "asset" only if null (some assets are real objects)
        if "asset" in node and node["asset"] is None:
            del node["asset"]

        model_type = node.get("modelType")

        # Strip kind from non-Submodel elements
        if "kind" in node and model_type is not None and model_type != "Submodel":
            del node["kind"]

        # Normalize valueType
        if "valueType" in node and isinstance(node["valueType"], str):
            canonical = VALUETYPE_CANONICAL.get(node["valueType"].lower())
            if canonical:
                node["valueType"] = canonical

        # Coerce non-string Property.value to string
        if model_type == "Property" and "value" in node:
            v = node["value"]
            if isinstance(v, (int, float, bool)):
                node["value"] = str(v).lower() if isinstance(v, bool) else str(v)

        # Wrap single-object MultiLanguageProperty.value in array
        if model_type == "MultiLanguageProperty" and "value" in node:
            v = node["value"]
            if isinstance(v, dict):
                node["value"] = [v]

        # Inject type on Reference objects missing it
        for ref_field in REFERENCE_FIELDS:
            if ref_field in node and isinstance(node[ref_field], dict):
                ref_obj = node[ref_field]
                if "keys" in ref_obj and "type" not in ref_obj:
                    ref_obj["type"] = "ExternalReference"

        # Inject valueType on qualifiers missing it
        if "qualifiers" in node and isinstance(node["qualifiers"], list):
            for q in node["qualifiers"]:
                if isinstance(q, dict) and "type" in q and "value" in q and "valueType" not in q:
                    q["valueType"] = "xs:string"

        # Track and rename duplicate idShorts within submodelElements
        if "submodelElements" in node and isinstance(node["submodelElements"], list):
            seen = {}
            for elem in node["submodelElements"]:
                if isinstance(elem, dict) and "idShort" in elem:
                    ids = elem["idShort"]
                    if ids in seen:
                        # Rename duplicate
                        new_ids = ids.replace("{arbitrary}", "{arbitraryProperty}")
                        if new_ids == ids:
                            new_ids = ids + "_2"
                        elem["idShort"] = new_ids
                    else:
                        seen[ids] = True

        # Recurse
        for key in list(node.keys()):
            node[key] = fix_node(node[key], parent_key=key)

    elif isinstance(node, list):
        for i, item in enumerate(node):
            node[i] = fix_node(item, parent_key=parent_key)

    return node


def process_file(filepath, dry_run=False):
    """Read, fix, and write a JSON file."""
    with open(filepath, "rb") as f:
        raw = f.read()

    # Strip BOM
    if raw.startswith(codecs.BOM_UTF8):
        raw = raw[len(codecs.BOM_UTF8):]
        print(f"  [BOM] Stripped UTF-8 BOM from {filepath}")

    try:
        data = json.loads(raw.decode("utf-8"))
    except json.JSONDecodeError as e:
        print(f"  [SKIP] Could not parse {filepath}: {e}")
        return False

    fixed = fix_node(data)
    new_content = json.dumps(fixed, indent=2, ensure_ascii=False) + "\n"

    if not dry_run:
        with open(filepath, "w", encoding="utf-8", newline="\n") as f:
            f.write(new_content)

    old_content = raw.decode("utf-8")
    if old_content != new_content:
        print(f"  [FIXED] {filepath}")
        return True
    else:
        print(f"  [OK] {filepath}")
        return False


def main():
    if len(sys.argv) < 2:
        print(f"Usage: {sys.argv[0]} <directory> [--dry-run]")
        sys.exit(1)

    target_dir = sys.argv[1]
    dry_run = "--dry-run" in sys.argv

    if not os.path.isdir(target_dir):
        print(f"Error: {target_dir} is not a directory")
        sys.exit(1)

    fixed_count = 0
    total_count = 0

    for root, dirs, files in os.walk(target_dir):
        for fname in sorted(files):
            if fname.endswith(".json"):
                filepath = os.path.join(root, fname)
                total_count += 1
                if process_file(filepath, dry_run):
                    fixed_count += 1

    action = "would fix" if dry_run else "fixed"
    print(f"\nDone: {action} {fixed_count}/{total_count} files")


if __name__ == "__main__":
    main()
