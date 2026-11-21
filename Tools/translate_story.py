import argparse
import json
import os
import sys
import time

from deep_translator import GoogleTranslator

sys.stdout.reconfigure(encoding='utf-8')
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

SPEAKER_MAP = {
    '林渡': 'Lin Du',
    '旁白': 'Narrator',
    '迎新学长': 'Orientation Senior',
    '报到老师': 'Registration Staff',
    '沈青禾': 'Shen Qinghe',
    '沈': 'Shen',
    '系统': 'System',
    '周衍': 'Zhou Yan',
    '苏晚': 'Su Wan',
    '顾行': 'Gu Xing',
    '程野': 'Cheng Ye',
    '许知遥': 'Xu Zhiyao',
    '秦屿': 'Qin Yu',
    '陆昭': 'Lu Zhao',
    '唐栀': 'Tang Zhi',
    '方澄': 'Fang Cheng',
    '裴砚': 'Pei Yan',
    '宋知微': 'Song Zhiwei',
    '韩叙': 'Han Xu',
    '温予': 'Wen Yu',
    '谢忱': 'Xie Chen',
    '陈屿': 'Chen Yu',
    '江叙': 'Jiang Xu',
    '白川': 'Bai Chuan',
    '赵屿': 'Zhao Yu',
    '叶知秋': 'Ye Zhiqiu',
}

CHOICE_MAP = {
    '礼貌回应': 'Respond politely',
    '冷淡回应': 'Respond coolly',
    '继续': 'Continue',
    '离开': 'Leave',
}

PROTECTED = {
    '未名之海': 'The Nameless Sea',
    '龙翔大道': 'Longxiang Avenue',
    '香港中文大学（深圳）': 'The Chinese University of Hong Kong, Shenzhen',
    '香港中文大学': 'The Chinese University of Hong Kong, Shenzhen',
    '林渡': 'Lin Du',
    '沈青禾': 'Shen Qinghe',
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

    work = text
    tokens = {}
    for i, (zh, en) in enumerate(PROTECTED.items()):
        if zh in work:
            key = f'__P{i}__'
            tokens[key] = en
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


def translate_docs(src_name, out_name, partial_name):
    src = os.path.join(ROOT, 'Tools', src_name)
    out = os.path.join(ROOT, 'Assets', 'Localization', out_name)
    out_resources = os.path.join(ROOT, 'Assets', 'Resources', 'Localization', out_name)
    partial = os.path.join(ROOT, 'Tools', partial_name)

    with open(src, 'r', encoding='utf-8') as f:
        docs = json.load(f)

    cache = {}
    result = {}
    if os.path.exists(partial):
        with open(partial, 'r', encoding='utf-8') as f:
            result = json.load(f)
        print('resume partial', len(result), 'docs')

    translator = GoogleTranslator(source='zh-CN', target='en')
    total_pages = sum(len(d['pages']) for d in docs)
    done_pages = sum(len(result.get(d['documentId'], {}).get('pages', [])) for d in docs if d['documentId'] in result)

    for doc in docs:
        doc_id = doc['documentId']
        if doc_id in result and len(result[doc_id].get('pages', [])) >= len(doc['pages']):
            continue

        out_doc = result.get(doc_id, {'pages': [], 'choices': []})
        start_page = len(out_doc.get('pages', []))
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
        with open(partial, 'w', encoding='utf-8') as f:
            json.dump(result, f, ensure_ascii=False, indent=2)
        print('done doc', doc_id)

    os.makedirs(os.path.dirname(out), exist_ok=True)
    os.makedirs(os.path.dirname(out_resources), exist_ok=True)
    for target in [out, out_resources]:
        with open(target, 'w', encoding='utf-8') as f:
            json.dump(result, f, ensure_ascii=False, indent=2)
    print('wrote', out, 'docs', len(result))


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--side', action='store_true')
    parser.add_argument('--main', action='store_true')
    args = parser.parse_args()
    if args.side or (not args.side and not args.main):
        translate_docs('side_story_zh.json', 'story_side_en.json', 'story_side_en.partial.json')
    if args.main:
        translate_docs('main_story_zh.json', 'story_main_en.json', 'story_main_en.partial.json')
