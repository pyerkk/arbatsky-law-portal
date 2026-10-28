'use strict';
const {test}=require('node:test');const assert=require('node:assert/strict');const fs=require('node:fs');const path=require('node:path');const {DATA_FILES,validateSnapshot}=require('../database');
const root=path.resolve(__dirname,'../../dist');const all=Object.fromEntries(DATA_FILES.map(f=>[f,fs.readFileSync(path.join(root,f),'utf8')]));
test('база сайта проходит проверку',()=>assert.equal(validateSnapshot(all),all));
test('неполная или испорченная база отклоняется',()=>{const missing={...all};delete missing['pgo.json'];assert.throws(()=>validateSnapshot(missing));assert.throws(()=>validateSnapshot({...all,'catalog.json':'<html>ошибка</html>'}));assert.throws(()=>validateSnapshot({...all,'catalog.json':'[]'}));});
