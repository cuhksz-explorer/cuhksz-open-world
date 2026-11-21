import os
import re
import json
import sys

sys.stdout.reconfigure(encoding='utf-8')
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
asset = os.path.join(root, 'Assets/code/GameQuestSystem/Content1StoryDatabase.asset')
text = open(asset, 'r', encoding='utf-8').read()
parts = re.split(r'\n  - documentId: ', text)[1:]
main_docs = []
for p in parts:
    m_id = re.match(r'(\S+)', p)
    if not m_id:
        continue
    doc_id = m_id.group(1)
    cat = re.search(r'category: (\d+)', p)
    chap = re.search(r'chapterId: (\S+)', p)
    category = int(cat.group(1)) if cat else -1
    chapter = chap.group(1) if chap else ''
    page_blocks = re.findall(
        r'pageNumber: (\d+)\n      speakerName: (.*?)\n      text: "(.*?)"',
        p,
        re.S,
    )
    choices = re.findall(r'- label: "(.*?)"\n      targetDocumentId:', p)
    if category == 0:
        pages = []
        for pn, speaker, body in page_blocks:
            speaker = speaker.strip()
            pages.append({
                'pageNumber': int(pn),
                'speakerName': speaker,
                'text': bytes(body, 'utf-8').decode('unicode_escape') if '\\u' in body else body,
            })
        main_docs.append({
            'documentId': doc_id,
            'chapterId': chapter,
            'pages': pages,
            'choices': choices,
        })

from collections import Counter
print('category 0 docs:', len(main_docs))
print('chapters', dict(Counter(d['chapterId'] for d in main_docs)))
print('total pages', sum(len(d['pages']) for d in main_docs))
out = os.path.join(root, 'Tools/main_story_zh.json')
with open(out, 'w', encoding='utf-8') as f:
    json.dump(main_docs, f, ensure_ascii=False, indent=2)
print('wrote', out)
