import json
import os
import re
import sys
import time

from deep_translator import GoogleTranslator

sys.stdout.reconfigure(encoding='utf-8')
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, 'Tools', 'main_story_zh.json')
OUT = os.path.join(ROOT, 'Assets', 'Localization', 'story_main_en.json')
PARTIAL = os.path.join(ROOT, 'Tools', 'story_main_en.partial.json')

SPEAKER_MAP = {
    '林渡': 'Lin Du',
    '旁白': 'Narrator',
    '迎新学长': 'Orientation Senior',
    '报到老师': 'Registration Staff',
    '沈青禾': 'Shen Qinghe',
    '沈': 'Shen',
    '系统': 'System',
}

CHOICE_MAP = {
    '礼貌回应': 'Respond politely',
    '冷淡回应': 'Respond coolly',
    '继续': 'Continue',
    '离开': 'Leave',
}


def map_speaker(name: str) -> str:
    name = (name or '').strip()
    if not name:
        return ''
    if name in SPEAKER_MAP:
        return SPEAKER_MAP[name]
    if name.startswith('（旁白）') or name == '（旁白）':
        return 'Narrator'
    return name


def translate_text(translator, text: str, cache: dict) -> str:
    text = (text or '').strip()
    if not text:
        return ''
    if text in cache:
        return cache[text]
    # Preserve game-specific proper nouns
    protected = {
        '未名之海': '[[SEA]]',
        '龙翔大道': '[[LONGXIANG]]',
        '香港中文大学（深圳）': '[[CUHKSZ]]',
        '香港中文大学': '[[CUHKSZ]]',
        '林渡': '[[LINDU]]',
    }
    work = text
    tokens = {}
    for i, (zh, token) in enumerate(protected.items()):
        if zh in work:
            key = f'__P{i}__'
            tokens[key] = {
                '[[SEA]]': 'The Nameless Sea',
                '[[LONGXIANG]]': 'Longxiang Avenue',
                '[[CUHKSZ]]': 'The Chinese University of Hong Kong, Shenzhen',
                '[[LINDU]]': 'Lin Du',
            }[token]
            work = work.replace(zh, key)
    try:
        en = translator.translate(work)
    except Exception as ex:
        print('translate failed:', ex, text[:60])
        time.sleep(2)
        en = translator.translate(work)
    for key, val in tokens.items():
        en = en.replace(key, val)
    cache[text] = en
    return en


def main():
    with open(SRC, 'r', encoding='utf-8') as f:
        docs = json.load(f)

    cache = {}
    result = {}
    if os.path.exists(PARTIAL):
        with open(PARTIAL, 'r', encoding='utf-8') as f:
            result = json.load(f)
        print('resume partial', len(result), 'docs')

    translator = GoogleTranslator(source='zh-CN', target='en')
    total_pages = sum(len(d['pages']) for d in docs)
    done_pages = 0

    for doc in docs:
        doc_id = doc['documentId']
        if doc_id in result:
            done_pages += len(result[doc_id].get('pages', []))
            continue

        out_doc = {'pages': [], 'choices': []}
        for page in doc['pages']:
            speaker_zh = page.get('speakerName', '')
            speaker = map_speaker(speaker_zh)
            if speaker == speaker_zh and speaker_zh:
                speaker = translate_text(translator, speaker_zh, cache)
            text = translate_text(translator, page.get('text', ''), cache)
            out_doc['pages'].append({
                'pageNumber': page['pageNumber'],
                'speakerName': speaker,
                'text': text,
            })
            done_pages += 1
            if done_pages % 25 == 0:
                print(f'progress {done_pages}/{total_pages}')
                result[doc_id] = out_doc
                with open(PARTIAL, 'w', encoding='utf-8') as f:
                    json.dump(result, f, ensure_ascii=False, indent=2)
            time.sleep(0.05)

        for label in doc.get('choices', []):
            label = (label or '').strip()
            if not label:
                continue
            en = CHOICE_MAP.get(label)
            if not en:
                en = translate_text(translator, label, cache)
            out_doc['choices'].append(en)

        result[doc_id] = out_doc
        with open(PARTIAL, 'w', encoding='utf-8') as f:
            json.dump(result, f, ensure_ascii=False, indent=2)
        print('done doc', doc_id)

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, 'w', encoding='utf-8') as f:
        json.dump(result, f, ensure_ascii=False, indent=2)
    print('wrote', OUT, 'docs', len(result))


if __name__ == '__main__':
    main()
