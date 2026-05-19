#!/usr/bin/env python3
"""
fix_resources.py - Rewrite legacy AAS JSON resources for BaSyx Go v3 compliance.

Rules applied (v2 legacy cleanup):
  1.  Strip UTF-8 BOM
  2.  Strip null-valued properties recursively
  3.  Remove deprecated fields: dataSpecification, hasDataSpecification
  4.  Strip v2 Key fields: local, idType, index
  5.  Strip v2 element fields: parent, allowDuplicates, ordered, asset (when null)
  6.  Inject "type": "ExternalReference" on References missing it (semanticId, etc.)
  7.  Coerce numeric/boolean Property.value to strings
  8.  Wrap single-object MultiLanguageProperty.value in arrays
  9.  Normalize valueType to canonical XSD case
 10.  Rename duplicate {arbitrary} idShort to {arbitraryProperty}

Rules applied (BaSyx Go strict validation):
 11.  Remove empty "embeddedDataSpecifications": []
 12.  Fix key types: ConceptDescription/Submodel -> GlobalReference in ExternalReference keys
 13.  Remove qualifier kind (TemplateQualifier not supported)
 14.  Remove empty "submodels": [] from AAS shells
 15.  Remove empty "assetType": "" from AAS shells
 16.  Remove empty "value": "" from File elements
 17.  Remove empty "value": [] from MultiLanguageProperty elements
 18.  Remove empty "value": "" from non-string-typed Property elements
 19.  Remove empty "value": [] from SubmodelElementCollection/List elements
 20.  Remove empty "statements": [] from Entity elements
 21.  Remove semanticId/valueId with empty "keys": []
 22.  Remove description entries with empty text or empty language
 23.  Remove empty "category": ""
 24.  Fix invalid idShort patterns: {00} -> 00, spaces -> underscores
 25.  Trim trailing whitespace from File values

Usage:
    python fix_resources.py <directory> [--dry-run]
"""
import json
import os
import re
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

        # Remove empty string value from Properties with non-string valueType
        if model_type == "Property" and "value" in node and node["value"] == "":
            vt = node.get("valueType", "")
            if vt and vt != "xs:string":
                del node["value"]

        # Remove empty "value": "" from File elements
        if model_type == "File" and "value" in node and node["value"] == "":
            del node["value"]

        # Trim trailing whitespace from File values
        if model_type == "File" and "value" in node and isinstance(node["value"], str):
            node["value"] = node["value"].rstrip()

        # Wrap single-object MultiLanguageProperty.value in array
        if model_type == "MultiLanguageProperty" and "value" in node:
            v = node["value"]
            if isinstance(v, dict):
                node["value"] = [v]

        # Remove empty "value": [] from MultiLanguageProperty
        if model_type == "MultiLanguageProperty" and "value" in node and node["value"] == []:
            del node["value"]

        # Remove empty "value": [] from SubmodelElementCollection/SubmodelElementList
        if model_type in ("SubmodelElementCollection", "SubmodelElementList"):
            if "value" in node and node["value"] == []:
                del node["value"]

        # Remove empty "statements": [] from Entity
        if model_type == "Entity":
            if "statements" in node and node["statements"] == []:
                del node["statements"]

        # Remove empty "submodels": [] from AssetAdministrationShell
        if model_type == "AssetAdministrationShell":
            if "submodels" in node and node["submodels"] == []:
                del node["submodels"]
            # Remove empty assetType in assetInformation
            ai = node.get("assetInformation")
            if isinstance(ai, dict) and "assetType" in ai and ai["assetType"] == "":
                del ai["assetType"]

        # Remove empty embeddedDataSpecifications
        if "embeddedDataSpecifications" in node and node["embeddedDataSpecifications"] == []:
            del node["embeddedDataSpecifications"]

        # Remove empty category
        if "category" in node and node["category"] == "":
            del node["category"]

        # Fix description: remove entries with empty text or empty language
        if "description" in node and isinstance(node["description"], list):
            cleaned = [d for d in node["description"]
                       if isinstance(d, dict) and d.get("text", "") != "" and d.get("language", "") != ""]
            if len(cleaned) != len(node["description"]):
                if cleaned:
                    node["description"] = cleaned
                else:
                    del node["description"]

        # Fix invalid idShort patterns
        if "idShort" in node and isinstance(node["idShort"], str):
            ids = node["idShort"]
            # Remove curly braces around numeric suffixes: {00} -> 00
            ids = re.sub(r'\{(\d+)\}$', r'\1', ids)
            # Replace spaces with underscores
            ids = ids.replace(" ", "_")
            node["idShort"] = ids

        # Inject type on Reference objects missing it
        for ref_field in REFERENCE_FIELDS:
            if ref_field in node and isinstance(node[ref_field], dict):
                ref_obj = node[ref_field]
                if "keys" in ref_obj and "type" not in ref_obj:
                    ref_obj["type"] = "ExternalReference"

        # Remove semanticId/valueId with empty keys
        for ref_field in ("semanticId", "valueId"):
            if ref_field in node and isinstance(node[ref_field], dict):
                ref_obj = node[ref_field]
                keys = ref_obj.get("keys", [])
                if isinstance(keys, list):
                    # Remove keys with empty value
                    keys = [k for k in keys if not (isinstance(k, dict) and k.get("value", "") == "")]
                    if not keys:
                        del node[ref_field]
                    else:
                        ref_obj["keys"] = keys

        # Fix key types in References: ConceptDescription/Submodel -> GlobalReference
        for ref_field in REFERENCE_FIELDS | {"valueId"}:
            if ref_field in node and isinstance(node[ref_field], dict):
                ref_obj = node[ref_field]
                if ref_obj.get("type") == "ExternalReference" and "keys" in ref_obj:
                    for key in ref_obj["keys"]:
                        if isinstance(key, dict) and key.get("type") in ("ConceptDescription", "Submodel"):
                            key["type"] = "GlobalReference"

        # Strip kind from qualifiers (TemplateQualifier -> just type/value)
        if "qualifiers" in node and isinstance(node["qualifiers"], list):
            for q in node["qualifiers"]:
                if isinstance(q, dict):
                    if "kind" in q:
                        del q["kind"]
                    if "type" in q and "value" in q and "valueType" not in q:
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
