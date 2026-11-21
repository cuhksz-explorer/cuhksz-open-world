# -*- coding: utf-8 -*-
from pathlib import Path

SCENE = Path(__file__).resolve().parents[2] / "scene" / "undergarden.unity"

OLD_GUID_LINE = "  m_Script: {fileID: 11500000, guid: 84b8667d373f40a390bfb62768b268b3, type: 3}"
NEW_GUID_LINE = "  m_Script: {fileID: 11500000, guid: 49fc7e03b55c40fcac65a3793413711f, type: 3}"

OLD_POS = "  m_LocalPosition: {x: -1359.56, y: 14.58, z: 1075.28}"
NEW_POS = "  m_LocalPosition: {x: 51.83997, y: 0.583995, z: -22.8559}"


def main() -> None:
    text = SCENE.read_text(encoding="utf-8")
    marker = "--- !u!114 &1964672601"
    start = text.find(marker)
    if start < 0:
        raise SystemExit("FlowerBed_CreditsTrigger MonoBehaviour not found")

    end = text.find("\n--- !u!", start + 1)
    block = text[start:end]

    if OLD_GUID_LINE not in block:
        if NEW_GUID_LINE in block:
            print("Already patched script guid.")
        else:
            raise SystemExit("Unexpected MonoBehaviour block")

    block = block.replace(OLD_GUID_LINE, NEW_GUID_LINE, 1)
    for line in (
        "  documentId: \n",
        "  questAcceptNpcId: \n",
        "  hideAfterStoryCompleted: 1\n",
    ):
        block = block.replace(line, "", 1)

    if "  interactionType:" not in block:
        block = block.replace(
            '  displayName: "\\u5236\\u4F5C\\u4EBA\\u5458\\u9E23\\u8C22"\n',
            '  displayName: "\\u5236\\u4F5C\\u4EBA\\u5458\\u9E23\\u8C22"\n'
            '  interactionType: "\\u67E5\\u770B\\u81F4\\u8C22"\n',
            1,
        )

    text = text[:start] + block + text[end:]
    if OLD_POS in text:
        text = text.replace(OLD_POS, NEW_POS, 1)

    SCENE.write_text(text, encoding="utf-8")
    print("Patched FlowerBed_CreditsTrigger in undergarden.unity")


if __name__ == "__main__":
    main()
