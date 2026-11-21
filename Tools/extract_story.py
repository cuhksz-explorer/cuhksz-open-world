import os
import re
import json
import sys

sys.stdout.reconfigure(encoding='utf-8')

def decode_yaml_string(s: str) -> str:
    s = (s or '').strip()
    if not s:
        return ''
    if '\\u' in s:
        try:
            return bytes(s, 'utf-8').decode('unicode_escape')
        except Exception:
            pass
    return s


def extract_docs(category_filter, chapters=None, out_name='story_zh.json'):
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    asset = os.path.join(root, 'Assets/code/GameQuestSystem/Content1StoryDatabase.asset')
    text = open(asset, 'r', encoding='utf-8').read()
    parts = re.split(r'\n  - documentId: ', text)[1:]
    docs = []
    chapters = set(chapters or [])

    for p in parts:
        m_id = re.match(r'(\S+)', p)
        if not m_id:
            continue
        doc_id = m_id.group(1)
        cat = re.search(r'category: (\d+)', p)
        chap = re.search(r'chapterId: (\S+)', p)
        category = int(cat.group(1)) if cat else -1
        chapter = chap.group(1) if chap else ''

        if category != category_filter:
            continue
        if chapters and chapter not in chapters:
            continue

        page_blocks = re.findall(
            r'pageNumber: (\d+)\n      speakerName: (.*?)\n      text: "(.*?)"',
            p,
            re.S,
        )
        choices = re.findall(r'- label: "(.*?)"\n      targetDocumentId:', p)
        pages = []
        for pn, speaker, body in page_blocks:
            pages.append({
                'pageNumber': int(pn),
                'speakerName': decode_yaml_string(speaker),
                'text': decode_yaml_string(body),
            })
        docs.append({
            'documentId': doc_id,
            'chapterId': chapter,
            'pages': pages,
            'choices': [decode_yaml_string(c) for c in choices],
        })

    from collections import Counter
    print('docs:', len(docs))
    print('chapters', dict(Counter(d['chapterId'] for d in docs)))
    print('total pages', sum(len(d['pages']) for d in docs))
    out = os.path.join(root, 'Tools', out_name)
    with open(out, 'w', encoding='utf-8') as f:
        json.dump(docs, f, ensure_ascii=False, indent=2)
    print('wrote', out)
    return docs


if __name__ == '__main__':
    import argparse
    parser = argparse.ArgumentParser()
    parser.add_argument('--side', action='store_true', help='Extract M2/M3/M4 side story')
    parser.add_argument('--main', action='store_true', help='Extract M0/M1/M5 main story')
    args = parser.parse_args()
    if args.side or (not args.side and not args.main):
        extract_docs(1, ['M2', 'M3', 'M4'], 'side_story_zh.json')
    if args.main:
        extract_docs(0, ['M0', 'M1', 'M5'], 'main_story_zh.json')
