import json

with open('discordEmojiMap.json', encoding="utf-8-sig") as json_file:
    data = json.load(json_file)
    for definition in data['emojiDefinitions']:
        surrogates = definition['surrogates'].encode('unicode-escape').decode('ascii', 'replace')
        assetFileName = definition['assetFileName']
        print(f'["{surrogates}"] = "{assetFileName}",')