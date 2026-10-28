'use strict';
const DATA_FILES = ['data.json','extended.json','references.json','catalog.json','cheats.json','general.json','pgo.json','summaries.json','quiz-extra.json'];
function validateSnapshot(files) {
 if (!files || typeof files !== 'object') throw Error('Неполная база');
 for (const name of DATA_FILES) if (typeof files[name] !== 'string' || files[name].length > 2_000_000) throw Error('Нет корректного файла '+name);
 const parsed=Object.fromEntries(DATA_FILES.map(name=>[name,JSON.parse(files[name])]));
 if (!Array.isArray(parsed['catalog.json']) || parsed['catalog.json'].length < 100 || !Array.isArray(parsed['extended.json'].popular) || !Array.isArray(parsed['references.json'].upkIndex) || !Array.isArray(parsed['references.json'].adminIndex) || !Array.isArray(parsed['pgo.json']) || !Array.isArray(parsed['cheats.json']) || !parsed['summaries.json'].upk || !parsed['summaries.json'].koap || !parsed['data.json'].documents) throw Error('Некорректная структура базы');
 return files;
}
module.exports={DATA_FILES,validateSnapshot};
