#!/usr/bin/env python3
"""Generate minimal valid Unity placeholder prefabs for Streetscape features.
Run this script from the project root. It creates empty-GameObject prefabs
that the user replaces with real art assets later.
"""
import os
import uuid
import hashlib

PREFABS = {
    "TrafficSignal_Pole": "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6",
    "StreetSign_Post":   "b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7",
    "UtilityPole":       "c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8",
    "Bench":             "d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9",
    "Mailbox":           "e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0",
    "FireHydrant":       "f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0c1",
    "TrashCan":          "a7b8c9d0e1f2a3b4c5d6e7f8a9b0c1d2",
    "ParkedCar_Sedan":   "b8c9d0e1f2a3b4c5d6e7f8a9b0c1d2e3",
    "ParkedCar_Sedan_02":"c9d0e1f2a3b4c5d6e7f8a9b0c1d2e3f4",
}

# Unity FileIDs must fit in a signed 32-bit integer.
# Use deterministic scatter in the 7-9 digit range so IDs stay well below 2^31.
_FILE_ID_TABLE = {
    "TrafficSignal_Pole": 752_826_155,
    "StreetSign_Post":   852_937_164,
    "UtilityPole":       953_048_275,
    "Bench":             154_159_386,
    "Mailbox":           265_270_497,
    "FireHydrant":       376_381_508,
    "TrashCan":          487_492_619,
    "ParkedCar_Sedan":   598_603_720,
    "ParkedCar_Sedan_02":609_714_831,
}

def prefab_yaml(name: str) -> str:
    go_id = _FILE_ID_TABLE[name]
    tr_id = go_id + 1
    return f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &{go_id}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {tr_id}}}
  m_Layer: 0
  m_Name: {name}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &{tr_id}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_RootOrder: -1
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
"""

def meta_content(guid_str: str) -> str:
    return f"""fileFormatVersion: 2
guid: {guid_str}
PrefabImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

def main():
    base = os.path.join("Assets", "Shared", "Prefabs", "World", "Streetscape")
    os.makedirs(base, exist_ok=True)
    for name, guid_str in PREFABS.items():
        prefab_path = os.path.join(base, f"{name}.prefab")
        meta_path = os.path.join(base, f"{name}.prefab.meta")
        with open(prefab_path, "w", newline="\n") as f:
            f.write(prefab_yaml(name))
        with open(meta_path, "w", newline="\n") as f:
            f.write(meta_content(guid_str))
        print(f"Created {prefab_path}")

if __name__ == "__main__":
    main()
