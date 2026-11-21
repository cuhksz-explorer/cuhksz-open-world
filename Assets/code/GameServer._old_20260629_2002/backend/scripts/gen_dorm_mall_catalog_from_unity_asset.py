"""One-shot: parse Unity ProductDatabase.asset YAML-ish and write dorm_mall_catalog.json."""
from __future__ import annotations

import codecs
import json
import re
from pathlib import Path

BACKEND = Path(__file__).resolve().parents[1]
# backend -> GameServer -> code ; ProductDatabase 在 Assets/code/Resources/DormMall/
CODE_DIR = BACKEND.parent.parent
ASSET = CODE_DIR / "Resources" / "DormMall" / "ProductDatabase.asset"
OUT = BACKEND / "data" / "dorm_mall_catalog.json"


def decode_unity_quoted(s: str) -> str:
    """Turn \\uXXXX sequences inside a YAML double-quoted string into real unicode."""
    if not s:
        return ""
    try:
        return codecs.decode(s, "unicode_escape")
    except Exception:
        return s


def main() -> None:
    text = ASSET.read_text(encoding="utf-8")
    blocks = re.split(r"\n  - id:", text)[1:]
    products: list[dict] = []
    for b in blocks:
        b = b.lstrip()
        m = re.match(r"^(\d+)\s*", b)
        if not m:
            continue
        pid = int(m.group(1))

        def field(pat: str) -> str:
            mm = re.search(pat, b, re.M)
            return mm.group(1) if mm else ""

        product_name = decode_unity_quoted(field(r'productName: "(.*)"'))
        category = decode_unity_quoted(field(r'category: "(.*)"'))
        description = decode_unity_quoted(field(r'description: "(.*)"'))
        icon_path = decode_unity_quoted(field(r'iconPath: "(.*)"'))
        price_s = field(r"price: ([0-9.]+)")
        price = float(price_s) if price_s else 0.0
        placeable = decode_unity_quoted(field(r'placeablePrefabPath: "(.*)"'))

        products.append(
            {
                "id": pid,
                "productName": product_name,
                "category": category,
                "description": description,
                "iconPath": icon_path,
                "price": price,
                "placeablePrefabPath": placeable,
            }
        )

    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(
        json.dumps({"products": products}, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )
    print(f"wrote {OUT} ({len(products)} products)")


if __name__ == "__main__":
    main()
