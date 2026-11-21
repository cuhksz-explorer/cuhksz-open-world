import json
import os
import re
import sys

sys.stdout.reconfigure(encoding='utf-8')
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PATHS = [
    os.path.join(ROOT, 'Assets', 'Resources', 'Localization', 'story_main_en.json'),
    os.path.join(ROOT, 'Assets', 'Resources', 'Localization', 'story_side_en.json'),
    os.path.join(ROOT, 'Assets', 'Localization', 'story_main_en.json'),
    os.path.join(ROOT, 'Assets', 'Localization', 'story_side_en.json'),
]

SPEAKER_MAP = {
    '林渡': 'Lin Du',
    '旁白': 'Narrator',
    '迎新学长': 'Orientation Senior',
    '报到老师': 'Registration Staff',
    '沈青禾': 'Shen Qinghe',
    '沈': 'Shen',
    '系统': 'System',
}


def decode_value(s):
    if not s:
        return ''
    s = s.strip().strip('"').strip("'")
    try:
        if '\\u' in s:
            s = s.encode('utf-8').decode('unicode_escape')
    except Exception:
        pass
    return s.strip().strip('"').strip("'")


def fix_speaker(name):
    name = decode_value(name)
    if name in SPEAKER_MAP:
        return SPEAKER_MAP[name]
    if name.startswith('（旁白）'):
        return 'Narrator'
    # leftover quoted unicode artifacts
    if re.match(r'^\\u[0-9a-f]{4}', name, re.I):
        try:
            name = bytes(name, 'utf-8').decode('unicode_escape')
        except Exception:
            pass
        return SPEAKER_MAP.get(name, name)
    return name


def main():
    seen = set()
    for path in PATHS:
        if not os.path.exists(path) or path in seen:
            continue
        seen.add(path)
        with open(path, 'r', encoding='utf-8') as f:
            data = json.load(f)

        for doc_id, doc in data.items():
            for page in doc.get('pages', []):
                page['speakerName'] = fix_speaker(page.get('speakerName', ''))
                text = page.get('text', '')
                if 'Lin Du Stand still' in text:
                    page['text'] = text.replace('Lin Du Stand still', 'Lin Du stood still')
                # Remove stray wrapping quotes from dialogue
                if text.startswith('"') and text.endswith('"') and text.count('"') == 2:
                    page['text'] = text[1:-1]

        with open(path, 'w', encoding='utf-8') as f:
            json.dump(data, f, ensure_ascii=False, indent=2)
        print('fixed speakers in', path)


if __name__ == '__main__':
    main()
