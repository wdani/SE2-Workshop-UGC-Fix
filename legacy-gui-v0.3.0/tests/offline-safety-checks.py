"""Offline sanity checks for the *design* of the one-field-only repair.
Does not execute Windows C# or access Steam. Does not contain real Workshop metadata.
"""
import json, re, hashlib, pathlib
pattern = re.compile(r'("Description"\s*:\s*)("(?:\\.|[^"\\])*")')
def examine(obj, requested_id, user_is_owner=True):
    raw=json.dumps(obj, ensure_ascii=False, separators=(',', ':'))
    a=obj['$Value'];f=a['FullMetadata'];b=f['BaseMetadata']
    if not user_is_owner or a['Id'] != requested_id or f['Id']['ServiceName']!='Steam' or f['Id']['Id']!=f['ProjectID']['ModId']['Id']:
        return None
    if len(raw.encode())<=5000 or len(b['Description'])<100:return None
    ms=list(pattern.finditer(raw))
    if len(ms)!=1:return None
    m=ms[0]
    cand=raw[:m.start(2)]+'""'+raw[m.end(2):]
    orig_copy=json.loads(raw); orig_copy['$Value']['FullMetadata']['BaseMetadata']['Description']=''
    if orig_copy!=json.loads(cand) or len(cand.encode())>=5000:return None
    return (raw,cand)
def fixture(desc):
    return {'$Value':{'Id':3820098765, 'FullMetadata':{
        'Id':{'Id':'00000000-0000-0000-0000-000000012345','ServiceName':'Steam'},
        'ProjectID':{'Guid':'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee','ModId':{'Id':'00000000-0000-0000-0000-000000012345'}},
        'BaseMetadata':{'Title':'Synthetic test mod','Description':desc}}}}
x=fixture('abc'*1800)
r=examine(x,3820098765)
assert r is not None, 'Oversized eligible sample must be fixable'
raw,cand=r
assert len(raw.encode())>5000 and len(cand.encode())<5000
assert json.loads(cand)['$Value']['FullMetadata']['BaseMetadata']['Description']==''
assert examine(fixture('abc'),3820098765) is None, 'small case must be rejected'
assert examine(x,1234567) is None, 'wrong item id must be rejected'
assert examine(x,3820098765,False) is None, 'non-owner must be rejected'
y=fixture('abc'*1800);y['$Value']['FullMetadata']['ExtraDescription']='other field'
assert examine(y,3820098765) is not None, 'Only exact Description field should change'
z=fixture('abc'*1800);z['$Value']['FullMetadata']['DifferentField']={'Description':'Other nested field'}
assert examine(z,3820098765) is None, 'Ambiguous Description property must be rejected'
src=pathlib.Path(__file__).resolve().parents[1]/'SE2WorkshopUGCFix'/'Program.cs'
s=src.read_text(encoding='utf8')
assert s.count('SteamUGC.SubmitItemUpdate(')==1
assert s.count('SteamUGC.SetItemMetadata(')==1
for method in ('SetItemContent(', 'SetItemPreview(', 'SetItemDescription(', 'DeleteItem(', 'CreateItem('):
    assert method not in s, f'Unexpected dangerous API call {method}'
assert 'details.m_nConsumerAppID.m_AppId != AppId' in s
assert 'JsonNode.DeepEquals' in (src.parent/'MetadataPatch.cs').read_text()
print('PASS: offline behavioral/static safeguards (synthetic metadata; C# not compiled).')
print('Synthetic oversized data:',len(raw.encode()),'->',len(cand.encode()),'bytes.')
print('Source SHA256:',hashlib.sha256(src.read_bytes()).hexdigest())

# v0.3 preflight must be offline only: no Steamworks mutating APIs.
ui=(src.parent/'PreflightForm.cs').read_text(encoding='utf8')
pre=(src.parent/'DescriptionPreflight.cs').read_text(encoding='utf8')
assert 'DESCRIPTION PREFLIGHT' in s
assert 'PreflightForm' in s
assert 'JsonSerializer.Serialize(description' in pre
assert 'JsonDocument.Parse(raw)' in pre
for unsafe in ('SteamAPI.Init(', 'SteamUGC.', 'SetItemMetadata(', 'SubmitItemUpdate(', 'SetItemDescription('):
    assert unsafe not in pre and unsafe not in ui, f'Preflight must remain offline: {unsafe}'
assert 'public' in ui.lower() and 'if' in ui.lower(), 'Risk warning explaining conditional public/internal description distinction missing'
assert len(list((src.parents[1]/'TEST-DESCRIPTIONS').glob('*.txt'))) >= 4
print('PASS: v0.3 preflight is read-only and includes disposable-mod reproduction kit.')
